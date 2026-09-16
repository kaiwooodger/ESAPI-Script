using System;
using System.Globalization;
using System.IO;
using System.Linq;
using LatticePlacement;

internal static class PhantomExport
{
    public static int Main(string[] args)
    {
        if (args.Length != 1) { Console.Error.WriteLine("Usage: PhantomExport <output.csv>"); return 2; }
        const double targetRadius = 60.0;
        const double contraction = 5.0;
        const double diameter = 15.0;
        const double ctc = 40.0;
        var settings = new Settings {
            VertexDiameterMm=diameter, CtcMm=ctc, TargetContractionMm=contraction,
            RetryInitialSlice=false, FillEndSlices=false
        };
        var origin = new Point(0,0,0);
        var bounds = new Box(new Point(-targetRadius,-targetRadius,-targetRadius),
                             new Point(targetRadius,targetRadius,targetRadius));
        var result = Geometry.Plan(settings,origin,bounds,new double[0],
            p => Geometry.Distance(p,origin)+diameter/2 <= targetRadius-contraction+1e-9);
        double minimum = Double.PositiveInfinity;
        for (int i=0;i<result.Centres.Count;i++) for (int j=i+1;j<result.Centres.Count;j++)
            minimum=Math.Min(minimum,Geometry.Distance(result.Centres[i],result.Centres[j]));
        using (var writer=new StreamWriter(args[0]))
        {
            writer.WriteLine("id,x_mm,y_mm,z_mm,distance_from_origin_mm");
            for (int i=0;i<result.Centres.Count;i++) {
                Point p=result.Centres[i];
                writer.WriteLine(String.Format(CultureInfo.InvariantCulture,"LAT_V{0:D3},{1:F6},{2:F6},{3:F6},{4:F6}",
                    i+1,p.X,p.Y,p.Z,Geometry.Distance(p,origin)));
            }
        }
        Console.WriteLine(String.Format(CultureInfo.InvariantCulture,
            "PASS diameter={0:F1} mm CTC={1:F1} mm target_radius={2:F1} mm contraction={3:F1} mm vertices={4} min_pair_distance={5:F6} mm fit_tests={6}",
            diameter,ctc,targetRadius,contraction,result.Centres.Count,minimum,result.FitTests));
        return Math.Abs(minimum-ctc)<1e-6 && result.Centres.Count==13 ? 0 : 1;
    }
}
