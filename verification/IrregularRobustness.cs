using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LatticePlacement;

internal static class IrregularRobustness
{
    private struct Lobe
    {
        public double X,Y,Z,A,B,C;
        public Lobe(double x,double y,double z,double a,double b,double c) {X=x;Y=y;Z=z;A=a;B=b;C=c;}
    }
    private static readonly Lobe[] Lobes = {
        new Lobe(0,0,0,55,43,48),
        new Lobe(38,9,7,38,29,34),
        new Lobe(-34,-17,-13,33,35,29),
        new Lobe(5,31,-18,31,25,27)
    };
    private static readonly double[] Scales = {0.65,0.75,0.85,0.95,1.05,1.15,1.25,1.35,1.45,1.60};
    private static readonly Point[] PhaseOffsets = {
        new Point(0,0,0), new Point(5,0,0),new Point(-5,0,0),
        new Point(0,5,0),new Point(0,-5,0),new Point(0,0,5),new Point(0,0,-5),
        new Point(5,5,5),new Point(-5,-5,-5)
    };
    private static Point[] Directions(int n)
    {
        var result=new List<Point> {new Point(1,0,0),new Point(-1,0,0),new Point(0,1,0),new Point(0,-1,0),new Point(0,0,1),new Point(0,0,-1)};
        double golden=Math.PI*(3-Math.Sqrt(5));
        for(int i=0;i<n;i++) {
            double z=1-2*(i+.5)/n, r=Math.Sqrt(Math.Max(0,1-z*z)), a=golden*i;
            result.Add(new Point(r*Math.Cos(a),r*Math.Sin(a),z));
        }
        return result.ToArray();
    }
    private static bool Inside(Point p,double scale)
    {
        foreach(var l in Lobes) {
            double dx=(p.X-scale*l.X)/(scale*l.A),dy=(p.Y-scale*l.Y)/(scale*l.B),dz=(p.Z-scale*l.Z)/(scale*l.C);
            if(dx*dx+dy*dy+dz*dz<=1+1e-12) return true;
        }
        return false;
    }
    private static bool BallInside(Point c,double radius,double scale,Point[] directions)
    {
        if(!Inside(c,scale)) return false;
        foreach(var d in directions)
            if(!Inside(new Point(c.X+radius*d.X,c.Y+radius*d.Y,c.Z+radius*d.Z),scale)) return false;
        return true;
    }
    private static Box Bounds(double scale)
    {
        return new Box(
            new Point(Lobes.Min(l=>l.X-l.A)*scale,Lobes.Min(l=>l.Y-l.B)*scale,Lobes.Min(l=>l.Z-l.C)*scale),
            new Point(Lobes.Max(l=>l.X+l.A)*scale,Lobes.Max(l=>l.Y+l.B)*scale,Lobes.Max(l=>l.Z+l.C)*scale));
    }
    private static void BaseVolumeCentroid(out double volume,out Point centroid)
    {
        Box b=Bounds(1); const double h=1.0; long count=0; double sx=0,sy=0,sz=0;
        for(double z=b.Min.Z+h/2;z<b.Max.Z;z+=h)
        for(double y=b.Min.Y+h/2;y<b.Max.Y;y+=h)
        for(double x=b.Min.X+h/2;x<b.Max.X;x+=h) {
            var p=new Point(x,y,z); if(!Inside(p,1)) continue;
            count++;sx+=x;sy+=y;sz+=z;
        }
        volume=count*h*h*h; centroid=new Point(sx/count,sy/count,sz/count);
    }
    private static double MinimumSpacing(IList<Point> centres)
    {
        if(centres.Count<2) return Double.NaN; double min=Double.PositiveInfinity;
        for(int i=0;i<centres.Count;i++) for(int j=i+1;j<centres.Count;j++) min=Math.Min(min,Geometry.Distance(centres[i],centres[j]));
        return min;
    }
    private static double ExtraClearance(Point centre,double scale,Point[] directions)
    {
        const double required=12.5; double lo=required,hi=80*scale;
        if(!BallInside(centre,required,scale,directions)) return -1;
        for(int i=0;i<16;i++) {double mid=(lo+hi)/2;if(BallInside(centre,mid,scale,directions))lo=mid;else hi=mid;}
        return lo-required;
    }
    public static int Main(string[] args)
    {
        if(args.Length!=2) {Console.Error.WriteLine("Usage: IrregularRobustness summary.csv centres.csv");return 2;}
        double baseVolume;Point baseCentroid;BaseVolumeCentroid(out baseVolume,out baseCentroid);
        Point[] fitDirections=Directions(2048),verifyDirections=Directions(8192);
        var settings=new Settings {VertexDiameterMm=15,CtcMm=40,TargetContractionMm=5,RetryInitialSlice=true,FillEndSlices=true,MaxFitTests=50000};
        bool pass=true;
        using(var summary=new StreamWriter(args[0])) using(var centres=new StreamWriter(args[1])) {
            summary.WriteLine("scale,volume_cc,equivalent_diameter_mm,phase_id,phase_dx_mm,phase_dy_mm,phase_dz_mm,vertex_count,min_spacing_mm,min_extra_clearance_mm,fit_tests,end_vertices,status");
            centres.WriteLine("scale,phase_id,id,x_mm,y_mm,z_mm");
            foreach(double scale in Scales) {
                Box bounds=Bounds(scale);Point centroid=new Point(baseCentroid.X*scale,baseCentroid.Y*scale,baseCentroid.Z*scale);
                double volume=baseVolume*scale*scale*scale/1000.0,equivalent=Math.Pow(6*volume*1000/Math.PI,1.0/3.0);
                var planes=new List<double>();for(double z=bounds.Min.Z;z<=bounds.Max.Z+1e-9;z+=2)planes.Add(z);
                for(int phase=0;phase<PhaseOffsets.Length;phase++) {
                    Point q=PhaseOffsets[phase],origin=new Point(centroid.X+q.X,centroid.Y+q.Y,centroid.Z+q.Z);
                    Placement result=Geometry.Plan(settings,origin,bounds,planes,p=>BallInside(p,12.5,scale,fitDirections));
                    double minSpacing=MinimumSpacing(result.Centres),minClear=result.Centres.Count==0?Double.NaN:result.Centres.Min(p=>ExtraClearance(p,scale,verifyDirections));
                    string status=result.Centres.Count<2?"NO_LATTICE":(minSpacing+1e-6>=40&&minClear>=-1e-6?"PASS":"FAIL");
                    if(status=="FAIL")pass=false;
                    summary.WriteLine(String.Format(CultureInfo.InvariantCulture,"{0:F2},{1:F3},{2:F3},{3},{4:F1},{5:F1},{6:F1},{7},{8:F6},{9:F6},{10},{11},{12}",
                        scale,volume,equivalent,phase,q.X,q.Y,q.Z,result.Centres.Count,minSpacing,minClear,result.FitTests,result.EndVertices,status));
                    for(int i=0;i<result.Centres.Count;i++) {Point p=result.Centres[i];centres.WriteLine(String.Format(CultureInfo.InvariantCulture,
                        "{0:F2},{1},LAT_V{2:D3},{3:F6},{4:F6},{5:F6}",scale,phase,i+1,p.X,p.Y,p.Z));}
                }
            }
        }
        Console.WriteLine(String.Format(CultureInfo.InvariantCulture,"{0} irregular robustness: sizes={1}, phases={2}, trials={3}, base_volume={4:F3} cc, containment_surface_samples={5}",
            pass?"PASS":"FAIL",Scales.Length,PhaseOffsets.Length,Scales.Length*PhaseOffsets.Length,baseVolume/1000,verifyDirections.Length));
        return pass?0:1;
    }
}
