using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LatticePlacement;

internal static class ParameterSweep
{
    private struct Lobe { public double X,Y,Z,A,B,C; public Lobe(double x,double y,double z,double a,double b,double c){X=x;Y=y;Z=z;A=a;B=b;C=c;} }
    private struct Config { public double Diameter,Ctc; public Config(double d,double c){Diameter=d;Ctc=c;} }
    private static readonly Lobe[] Lobes={new Lobe(0,0,0,55,43,48),new Lobe(38,9,7,38,29,34),new Lobe(-34,-17,-13,33,35,29),new Lobe(5,31,-18,31,25,27)};
    private static readonly double[] Scales={.75,.95,1.15,1.35,1.60};
    private static readonly Point[] Phases={new Point(0,0,0),new Point(5,0,0),new Point(0,5,0),new Point(0,0,5),new Point(-5,-5,-5)};
    private static readonly Config[] Configs={
        new Config(10,20),new Config(10,25),new Config(10,30),new Config(10,35),new Config(10,40),
        new Config(15,25),new Config(15,30),new Config(15,35),new Config(15,40),new Config(15,45),
        new Config(20,30),new Config(20,35),new Config(20,40),new Config(20,45),new Config(20,50)};
    private static Point[] Directions(int n){var a=new List<Point>{new Point(1,0,0),new Point(-1,0,0),new Point(0,1,0),new Point(0,-1,0),new Point(0,0,1),new Point(0,0,-1)};double g=Math.PI*(3-Math.Sqrt(5));for(int i=0;i<n;i++){double z=1-2*(i+.5)/n,r=Math.Sqrt(1-z*z),q=g*i;a.Add(new Point(r*Math.Cos(q),r*Math.Sin(q),z));}return a.ToArray();}
    private static bool Inside(Point p,double s){foreach(var l in Lobes){double x=(p.X-s*l.X)/(s*l.A),y=(p.Y-s*l.Y)/(s*l.B),z=(p.Z-s*l.Z)/(s*l.C);if(x*x+y*y+z*z<=1+1e-12)return true;}return false;}
    private static bool BallInside(Point c,double r,double s,Point[] d){if(!Inside(c,s))return false;foreach(var q in d)if(!Inside(new Point(c.X+r*q.X,c.Y+r*q.Y,c.Z+r*q.Z),s))return false;return true;}
    private static Box Bounds(double s){return new Box(new Point(Lobes.Min(l=>l.X-l.A)*s,Lobes.Min(l=>l.Y-l.B)*s,Lobes.Min(l=>l.Z-l.C)*s),new Point(Lobes.Max(l=>l.X+l.A)*s,Lobes.Max(l=>l.Y+l.B)*s,Lobes.Max(l=>l.Z+l.C)*s));}
    private static void BaseProperties(out double volume,out Point centroid){Box b=Bounds(1);long n=0;double x=0,y=0,z=0;for(double k=b.Min.Z+.5;k<b.Max.Z;k++)for(double j=b.Min.Y+.5;j<b.Max.Y;j++)for(double i=b.Min.X+.5;i<b.Max.X;i++){var p=new Point(i,j,k);if(!Inside(p,1))continue;n++;x+=i;y+=j;z+=k;}volume=n;centroid=new Point(x/n,y/n,z/n);}
    private static double MinSpacing(IList<Point> c){if(c.Count<2)return Double.NaN;double m=Double.PositiveInfinity;for(int i=0;i<c.Count;i++)for(int j=i+1;j<c.Count;j++)m=Math.Min(m,Geometry.Distance(c[i],c[j]));return m;}
    private static double Extra(Point c,double required,double scale,Point[] dirs){if(!BallInside(c,required,scale,dirs))return -1;double lo=required,hi=80*scale;for(int i=0;i<15;i++){double m=(lo+hi)/2;if(BallInside(c,m,scale,dirs))lo=m;else hi=m;}return lo-required;}
    public static int Main(string[] args)
    {
        if(args.Length!=1){Console.Error.WriteLine("Usage: ParameterSweep output.csv");return 2;}
        double baseVolume;Point baseCentroid;BaseProperties(out baseVolume,out baseCentroid);Point[] fit=Directions(2048),verify=Directions(8192);bool pass=true;int trials=0,valid=0,noLattice=0;
        using(var w=new StreamWriter(args[0])){
            w.WriteLine("diameter_mm,ctc_mm,scale,volume_cc,equivalent_diameter_mm,phase_id,vertex_count,min_spacing_mm,spacing_error_mm,min_extra_clearance_mm,analytic_sphere_volume_cc,status");
            foreach(var cfg in Configs)foreach(double scale in Scales){Box bounds=Bounds(scale);Point centroid=new Point(baseCentroid.X*scale,baseCentroid.Y*scale,baseCentroid.Z*scale);double vol=baseVolume*scale*scale*scale/1000,eq=Math.Pow(6*vol*1000/Math.PI,1.0/3.0);var planes=new List<double>();for(double z=bounds.Min.Z;z<=bounds.Max.Z+1e-9;z+=2)planes.Add(z);
                for(int phase=0;phase<Phases.Length;phase++){trials++;Point q=Phases[phase],origin=new Point(centroid.X+q.X,centroid.Y+q.Y,centroid.Z+q.Z);var settings=new Settings{VertexDiameterMm=cfg.Diameter,CtcMm=cfg.Ctc,TargetContractionMm=5,RetryInitialSlice=true,FillEndSlices=true,MaxFitTests=100000};double required=cfg.Diameter/2+5;
                    // Surface sampling is approximate. A 0.5 mm guard in candidate fitting
                    // prevents sparse sampling from admitting marginal false positives;
                    // independent verification checks the actual required radius densely.
                    Placement result=Geometry.Plan(settings,origin,bounds,planes,p=>BallInside(p,required+0.5,scale,fit));double spacing=MinSpacing(result.Centres),error=Double.IsNaN(spacing)?Double.NaN:spacing-cfg.Ctc,clear=result.Centres.Count==0?Double.NaN:result.Centres.Min(p=>Extra(p,required,scale,verify));string status=result.Centres.Count<2?"NO_LATTICE":(error>=-1e-6&&clear>=-1e-6?"PASS":"FAIL");if(status=="PASS")valid++;else if(status=="NO_LATTICE")noLattice++;else pass=false;
                    double sphereVolume=4*Math.PI*Math.Pow(cfg.Diameter/2,3)/3000;
                    w.WriteLine(String.Format(CultureInfo.InvariantCulture,"{0:F1},{1:F1},{2:F2},{3:F3},{4:F3},{5},{6},{7:F6},{8:F6},{9:F6},{10:F6},{11}",cfg.Diameter,cfg.Ctc,scale,vol,eq,phase,result.Centres.Count,spacing,error,clear,sphereVolume,status));
                }
            }
        }
        Console.WriteLine(String.Format(CultureInfo.InvariantCulture,"{0} parameter sweep: configurations={1}, sizes={2}, phases={3}, trials={4}, valid_lattices={5}, no_lattice={6}, verification_directions={7}",pass?"PASS":"FAIL",Configs.Length,Scales.Length,Phases.Length,trials,valid,noLattice,verify.Length));return pass?0:1;
    }
}
