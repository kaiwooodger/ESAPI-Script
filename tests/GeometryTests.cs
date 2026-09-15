using System;
using System.Collections.Generic;
using System.Linq;
using LatticePlacement;

internal static class GeometryTests
{
    private static int passed, failed;
    private static readonly Point Origin = new Point(0,0,0);
    private static Box Bounds(double half) { return new Box(new Point(-half,-half,-half),new Point(half,half,half)); }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Near(double a, double b, double tolerance) { Assert(Math.Abs(a-b) <= tolerance,"Expected " + b + ", got " + a); }
    private static void Throws(Action a) { bool did = false; try { a(); } catch (ArgumentException) { did = true; } catch (InvalidOperationException) { did = true; } Assert(did,"Expected rejection"); }
    private static void Test(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    private static void CheckSpacing(IList<Point> p, double ctc)
    {
        for (int i=0;i<p.Count;i++) for (int j=i+1;j<p.Count;j++)
            Assert(Geometry.Distance(p[i],p[j]) >= ctc-1e-7,"Pair too close");
    }
    private static double PolygonArea(Point[] p)
    {
        double sum=0;
        for(int i=0;i<p.Length;i++) { var q=p[(i+1)%p.Length]; sum+=p[i].X*q.Y-q.X*p[i].Y; }
        return Math.Abs(sum)/2;
    }
    public static int Main()
    {
        Test("parameter validation", () => {
            new Settings().Validate();
            foreach (double v in new[] { 0.0,-1.0,Double.NaN,Double.PositiveInfinity }) {
                Throws(() => new Settings { VertexDiameterMm=v }.Validate());
                Throws(() => new Settings { CtcMm=v }.Validate());
            }
            Throws(() => new Settings { VertexDiameterMm=35 }.Validate());
            Throws(() => new Settings { TargetContractionMm=51 }.Validate());
            Throws(() => new Settings { OutputPrefix="bad_name" }.Validate());
        });
        Test("IDs fit 16-character limit", () => {
            var s=new Settings { OutputPrefix="ABCDEFGH" };s.Validate();
            Assert(s.VertexId(999).Length<=16 && s.UnionId.Length<=16,"ID too long");
            Assert(s.VertexId(1)!=s.VertexId(2),"Duplicate IDs");
        });
        Test("regular lattice includes centroid exactly once", () => {
            var p=Geometry.Candidates(Origin,Bounds(100),35).ToList();
            Assert(p.Count(q=>Geometry.Distance(q,Origin)<1e-7)==1,"Centroid omitted/duplicated");
            Assert(p.Select(q=>q.X+","+q.Y+","+q.Z).Distinct().Count()==p.Count,"Duplicate points");
        });
        Test("requested CTC is nearest-neighbour distance for all paper configurations", () => {
            foreach(double ctc in new[] {20.0,25,30,35,40,45,50}) {
                var p=Geometry.Candidates(Origin,Bounds(60),ctc).ToList();CheckSpacing(p,ctc);
                Near(p.Where(q=>Geometry.Distance(q,Origin)>1e-7).Min(q=>Geometry.Distance(q,Origin)),ctc,1e-7);
            }
        });
        Test("AP/SI parity offsets agree for negative indices", () => {
            double d=35/Math.Sqrt(2);
            var p=Geometry.Candidates(Origin,Bounds(60),35).ToList();
            Assert(p.Any(q=>Math.Abs(q.X-d)<1e-7 && Math.Abs(q.Y+d)<1e-7 && Math.Abs(q.Z)<1e-7),"Negative odd row offset wrong");
            Assert(p.Any(q=>Math.Abs(q.X)<1e-7 && Math.Abs(q.Y-d)<1e-7 && Math.Abs(q.Z-d)<1e-7),"Combined parity wrong");
        });
        Test("bounds are derived dynamically beyond 100 indices", () => {
            var b=new Box(new Point(-1000,0,0),new Point(1000,0,0));
            var p=Geometry.Candidates(Origin,b,2).ToList();
            Assert(p.Count>200,"Hard-coded range truncates lattice");
            Assert(p.All(q=>b.ContainsSphere(q,0)),"Candidate outside bounds");
        });
        Test("translated coordinates preserve lattice", () => {
            var o=new Point(700,-350,1200);var b=new Box(new Point(640,-410,1140),new Point(760,-290,1260));
            var p=Geometry.Candidates(o,b,35).ToList();CheckSpacing(p,35);
            Assert(p.Count==Geometry.Candidates(Origin,Bounds(60),35).Count(),"Translation changed count");
        });
        Test("circles lie on exact analytic sphere", () => {
            var c=new Point(10,20,30);
            foreach(double z in new[] {23.0,27,30,34,37})
                foreach(var p in Geometry.Circle(c,7.5,z,96)) { Near(Geometry.Distance(p,c),7.5,1e-10);Near(p.Z,z,0); }
            Assert(Geometry.Circle(c,7.5,37.5,96).Length==0,"Tangent plane must be empty");
            Assert(Geometry.Circle(c,7.5,38,96).Length==0,"Outside plane must be empty");
        });
        Test("polygon and slice-sampling volume converge to sphere", () => {
            double step=0.05, volume=0, r=7.5;
            for(double z=-r+step/2;z<r;z+=step) volume+=PolygonArea(Geometry.Circle(Origin,r,z,96))*step;
            double analytic=4*Math.PI*r*r*r/3;
            Assert(Math.Abs(volume/analytic-1)<0.001,"Sphere volume error > 0.1% on fine grid");
        });
        Test("mesh centroid differs correctly from bounding box centre", () => {
            var p=new List<Point> {Origin,new Point(4,0,0),new Point(0,8,0),new Point(0,0,12)};
            var t=new List<int> {0,2,1,0,1,3,0,3,2,1,2,3};
            var c=Geometry.MeshCentroid(p,t,new Point(2,4,6));Near(c.X,1,1e-12);Near(c.Y,2,1e-12);Near(c.Z,3,1e-12);
            t.Reverse();var reversed=Geometry.MeshCentroid(p,t,new Point(2,4,6));Near(Geometry.Distance(c,reversed),0,1e-12);
        });
        Test("degenerate centroid fails explicitly", () => {
            Throws(()=>Geometry.MeshCentroid(new List<Point> {Origin},new List<int> {0,0,0},Origin));
        });
        Test("complete-sphere containment rejects centre-only false positives", () => {
            var s=new Settings { FillEndSlices=false,RetryInitialSlice=false };
            // Analytic sphere target radius 55, contracted radius 50. Second-shell centres
            // at 49.497 mm are inside, but their complete 7.5 mm-radius spheres protrude.
            var p=Geometry.Plan(s,Origin,Bounds(55),new double[0],c=>Geometry.Distance(c,Origin)+7.5<=50);
            Assert(p.Centres.Count>1,"No analytic lattice");CheckSpacing(p.Centres,s.CtcMm);
            Assert(p.Centres.All(c=>Geometry.Distance(c,Origin)+7.5<=50),"Protruding sphere accepted");
            var centreOnly=Geometry.Candidates(Origin,Bounds(55),35).Where(c=>Geometry.Distance(c,Origin)<=50).ToList();
            Assert(centreOnly.Count>p.Centres.Count,"Fixture does not exercise boundary rejection");
        });
        Test("non-convex analytic target exclusion is respected", () => {
            var s=new Settings {FillEndSlices=false,RetryInitialSlice=false};
            var hole=new Point(35/Math.Sqrt(2),35/Math.Sqrt(2),0);
            var p=Geometry.Plan(s,Origin,Bounds(60),new double[0],c=>Geometry.Distance(c,hole)>=17.5);
            Assert(p.Centres.All(c=>Geometry.Distance(c,hole)>=17.5),"Hole ignored");
        });
        Test("zero-fit slice retry finds a different SI phase", () => {
            var s=new Settings {FillEndSlices=false};
            var p=Geometry.Plan(s,Origin,Bounds(60),new[] {-2.0,2.0},c=>Math.Abs(c.Z-2)<1e-7);
            Assert(p.Centres.Count>1,"Retry failed");Near(p.InitialSliceShiftMm,2,1e-7);CheckSpacing(p.Centres,s.CtcMm);
        });
        Test("zero-fit and one-fit cases remain distinguishable", () => {
            var s=new Settings {FillEndSlices=false,RetryInitialSlice=false};
            Assert(Geometry.Plan(s,Origin,Bounds(60),new double[0],c=>false).Centres.Count==0,"False fit");
            Assert(Geometry.Plan(s,Origin,Bounds(60),new double[0],c=>Geometry.Distance(c,Origin)<1e-7).Centres.Count==1,"One fit miscounted");
        });
        Test("end-slice fill adds vertices while maintaining every pair's CTC", () => {
            var s=new Settings();var b=Bounds(100);
            var slices=Enumerable.Range(-45,91).Select(i=>i*2.0).ToList();
            Func<Point,bool> fits=c=>Math.Abs(c.Z)<1e-7 || Math.Abs(c.Z-80)<1e-7 || Math.Abs(c.Z+80)<1e-7;
            var p=Geometry.Plan(s,Origin,b,slices,fits);
            Assert(p.EndVertices>0,"No end fill");CheckSpacing(p.Centres,s.CtcMm);
            s.FillEndSlices=false;var regular=Geometry.Plan(s,Origin,b,slices,fits);
            Assert(p.Centres.Count>regular.Centres.Count,"Fill failed to improve count");
        });
        Test("fit limits and callback failures propagate", () => {
            Throws(()=>Geometry.Plan(new Settings {MaxFitTests=1},Origin,Bounds(60),new double[0],c=>false));
            Throws(()=>Geometry.Plan(new Settings(),Origin,Bounds(60),new double[0],c=> {throw new InvalidOperationException("Simulated engine failure");}));
            Throws(()=>Geometry.Plan(new Settings {MaxVertices=2},Origin,Bounds(60),new double[0],c=>true));
        });
        Test("analytic sphere phantom produces reproducible centres", () => {
            var s=new Settings {FillEndSlices=false,RetryInitialSlice=false};
            var p=Geometry.Plan(s,Origin,Bounds(50),new double[0],c=>Geometry.Distance(c,Origin)+7.5<=45);
            Assert(p.Centres.Count==13,"Expected centroid and 12 nearest neighbours");
            Console.WriteLine("PHANTOM radius=50 mm, contraction=5 mm, diameter=15 mm, CTC=35 mm: "+p.Centres.Count+" vertices");
        });
        Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed. Geometry tests only; Eclipse not executed.");
        return failed==0?0:1;
    }
}
