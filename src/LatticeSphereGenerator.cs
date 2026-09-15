// Single-file, write-enabled Eclipse plug-in. ESAPI 16.1 API surface, C# 5 syntax.
// Geometry tests compile this SAME file with GEOMETRY_ONLY; no ESAPI replacement is shipped.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
#if !GEOMETRY_ONLY
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
[assembly: ESAPIScript(IsWriteable = true)]
#endif

namespace LatticePlacement
{
    public struct Point
    {
        public readonly double X, Y, Z;
        public Point(double x, double y, double z) { X = x; Y = y; Z = z; }
    }

    public struct Box
    {
        public readonly Point Min, Max;
        public Box(Point min, Point max) { Min = min; Max = max; }
        public bool ContainsSphere(Point p, double r)
        {
            return p.X - r >= Min.X && p.X + r <= Max.X &&
                p.Y - r >= Min.Y && p.Y + r <= Max.Y &&
                p.Z - r >= Min.Z && p.Z + r <= Max.Z;
        }
    }

    public sealed class Settings
    {
        // EDIT PARAMETERS HERE. Distances in mm; these are research defaults, not a prescription.
        public string TargetId = "GTV_lattice";
        public double VertexDiameterMm = 15.0;
        public double CtcMm = 35.0;
        public double TargetContractionMm = 5.0;
        public int CirclePoints = 96;
        public bool CreateUnionStructure = true;
        public bool RetryInitialSlice = true;
        public bool FillEndSlices = true;
        public int MaxFitTests = 20000;
        public int MaxVertices = 999;
        // Choose a different prefix to keep multiple runs. Existing structures are never overwritten.
        public string OutputPrefix = "LAT";

        public void Validate()
        {
            if (String.IsNullOrWhiteSpace(TargetId)) throw new ArgumentException("Target ID is required.");
            if (!Geometry.Finite(VertexDiameterMm) || VertexDiameterMm <= 0 ||
                !Geometry.Finite(CtcMm) || CtcMm <= VertexDiameterMm)
                throw new ArgumentException("Require finite 0 < diameter < CTC spacing.");
            if (!Geometry.Finite(TargetContractionMm) || TargetContractionMm < 0 || TargetContractionMm > 50)
                throw new ArgumentException("Contraction must be between 0 and 50 mm.");
            if (CirclePoints < 32 || CirclePoints > 720) throw new ArgumentException("Circle points must be 32..720.");
            if (MaxFitTests < 1 || MaxVertices < 2 || MaxVertices > 999)
                throw new ArgumentException("Invalid resource limits.");
            if (String.IsNullOrEmpty(OutputPrefix) || OutputPrefix.Length > 8 ||
                OutputPrefix.Any(c => !Char.IsLetterOrDigit(c) || c > 127))
                throw new ArgumentException("Prefix must be 1..8 ASCII letters/digits.");
        }
        public string VertexId(int index) { return OutputPrefix + "_V" + index.ToString("D3", CultureInfo.InvariantCulture); }
        public string UnionId { get { return OutputPrefix + "_ALL"; } }
    }

    public sealed class Placement
    {
        public List<Point> Centres = new List<Point>();
        public int FitTests;
        public double InitialSliceShiftMm;
        public int EndVertices;
    }

    public static class Geometry
    {
        public static bool Finite(double x) { return !Double.IsNaN(x) && !Double.IsInfinity(x); }
        public static double Distance(Point a, Point b)
        {
            return Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)+(a.Z-b.Z)*(a.Z-b.Z));
        }
        public static bool Spaced(Point p, IEnumerable<Point> existing, double ctc)
        {
            return existing.All(q => Distance(p,q) >= ctc - 1e-7);
        }
        public static IEnumerable<Point> Candidates(Point origin, Box bounds, double ctc)
        {
            if (!Finite(ctc) || ctc <= 0) throw new ArgumentException("Invalid CTC.");
            double d = ctc / Math.Sqrt(2), lr = 2 * d;
            int k0 = Index(bounds.Min.Z, origin.Z, d, true), k1 = Index(bounds.Max.Z, origin.Z, d, false);
            int j0 = Index(bounds.Min.Y, origin.Y, d, true), j1 = Index(bounds.Max.Y, origin.Y, d, false);
            for (int k = k0; k <= k1; k++)
            for (int j = j0; j <= j1; j++)
            {
                double shift = ((j + k) & 1) == 0 ? 0 : d;
                int i0 = Index(bounds.Min.X, origin.X + shift, lr, true);
                int i1 = Index(bounds.Max.X, origin.X + shift, lr, false);
                for (int i = i0; i <= i1; i++)
                    yield return new Point(origin.X + i*lr + shift, origin.Y + j*d, origin.Z + k*d);
            }
        }
        private static int Index(double limit, double origin, double step, bool lower)
        {
            double v = (limit-origin)/step;
            v = lower ? Math.Ceiling(v - 1e-10) : Math.Floor(v + 1e-10);
            if (!Finite(v) || Math.Abs(v) > 100000) throw new ArgumentException("Excessive lattice index range.");
            return (int)v;
        }
        public static Point[] Circle(Point centre, double r, double planeZ, int count)
        {
            double dz = planeZ-centre.Z, r2 = r*r-dz*dz;
            if (r2 <= 0) return new Point[0]; // Tangent planes have zero area.
            double rr = Math.Sqrt(r2);
            var points = new Point[count];
            for (int i = 0; i < count; i++)
            {
                double a = 2*Math.PI*i/count;
                points[i] = new Point(centre.X+rr*Math.Cos(a), centre.Y+rr*Math.Sin(a), planeZ);
            }
            return points;
        }

        // Signed tetrahedral volume centroid of a consistently wound closed triangle mesh.
        // Reference translation reduces cancellation for large DICOM coordinates.
        public static Point MeshCentroid(IList<Point> p, IList<int> triangles, Point reference)
        {
            if (triangles.Count == 0 || triangles.Count % 3 != 0) throw new ArgumentException("Invalid target mesh.");
            double total = 0, x = 0, y = 0, z = 0;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Point a = Relative(p[triangles[i]],reference), b = Relative(p[triangles[i+1]],reference), c = Relative(p[triangles[i+2]],reference);
                double v = a.X*(b.Y*c.Z-b.Z*c.Y)-a.Y*(b.X*c.Z-b.Z*c.X)+a.Z*(b.X*c.Y-b.Y*c.X);
                total += v; x += v*(a.X+b.X+c.X)/4; y += v*(a.Y+b.Y+c.Y)/4; z += v*(a.Z+b.Z+c.Z)/4;
            }
            if (!Finite(total) || Math.Abs(total) < 1e-6) throw new ArgumentException("Target mesh has zero/invalid signed volume.");
            var result = new Point(reference.X+x/total,reference.Y+y/total,reference.Z+z/total);
            if (!Finite(result.X) || !Finite(result.Y) || !Finite(result.Z)) throw new ArgumentException("Invalid centroid.");
            return result;
        }
        private static Point Relative(Point p, Point r) { return new Point(p.X-r.X,p.Y-r.Y,p.Z-r.Z); }

        public static Placement Plan(Settings s, Point centroid, Box targetBounds, IList<double> imagePlaneZ, Func<Point,bool> fits)
        {
            s.Validate();
            var result = new Placement();
            Func<Point,bool> attempt = p =>
            {
                if (!targetBounds.ContainsSphere(p,s.VertexDiameterMm/2)) return false;
                if (++result.FitTests > s.MaxFitTests) throw new InvalidOperationException("Fit-test limit exceeded; no final outputs committed.");
                return fits(p);
            };
            Action<Point> accept = p =>
            {
                if (result.Centres.Count >= s.MaxVertices) throw new InvalidOperationException("Vertex limit exceeded.");
                result.Centres.Add(p);
            };
            Action<double> regular = shift =>
            {
                Point o = new Point(centroid.X,centroid.Y,centroid.Z+shift);
                // Test the centroid first, then traverse the remaining finite bounding box.
                if (attempt(o)) accept(o);
                foreach (Point p in Candidates(o,targetBounds,s.CtcMm))
                    if (Distance(p,o) > 1e-7 && attempt(p)) accept(p);
            };
            regular(0);
            if (result.Centres.Count == 0 && s.RetryInitialSlice)
            {
                // A single SI period covers distinct phases. Paper does not specify retry order.
                foreach (double shift in imagePlaneZ.Select(z => z-centroid.Z)
                    .Where(d => Math.Abs(d) > 1e-7 && Math.Abs(d) <= s.CtcMm/Math.Sqrt(2)/2)
                    .Distinct().OrderBy(d => Math.Abs(d)).ThenBy(d => d))
                {
                    regular(shift);
                    if (result.Centres.Count > 0) { result.InitialSliceShiftMm = shift; break; }
                }
            }
            if (result.Centres.Count > 0 && s.FillEndSlices)
            {
                double lo = result.Centres.Min(p => p.Z), hi = result.Centres.Max(p => p.Z);
                // Try CT planes outside the occupied band, from the bounding box ends inward.
                foreach (double z in imagePlaneZ.Where(z => z < lo-1e-7 || z > hi+1e-7)
                    .OrderByDescending(z => Math.Min(Math.Abs(z-lo),Math.Abs(z-hi))).ThenBy(z => z))
                for (int phase = 0; phase < 2; phase++)
                {
                    double d = s.CtcMm/Math.Sqrt(2);
                    Point o = new Point(centroid.X+phase*d,centroid.Y,z);
                    Box plane = new Box(new Point(targetBounds.Min.X,targetBounds.Min.Y,z),new Point(targetBounds.Max.X,targetBounds.Max.Y,z));
                    foreach (Point p in Candidates(o,plane,s.CtcMm))
                        if (Spaced(p,result.Centres,s.CtcMm) && attempt(p)) { accept(p); result.EndVertices++; }
                }
            }
            return result;
        }
    }
}

#if !GEOMETRY_ONLY
namespace VMS.TPS
{
    public class Script
    {
        public void Execute(ScriptContext context)
        {
            var settings = new LatticePlacement.Settings();
            var owned = new List<Structure>();
            StructureSet ss = context.StructureSet;
            try
            {
                settings.Validate();
                if (context.Patient == null || ss == null || ss.Image == null)
                    throw new InvalidOperationException("Load a patient, structure set and CT image.");
                Image image = ss.Image;
                ValidateImage(image, settings.VertexDiameterMm);
                Structure target = ss.Structures.SingleOrDefault(s => s.Id.Equals(settings.TargetId,StringComparison.OrdinalIgnoreCase));
                if (target == null || target.IsEmpty || !target.HasSegment) throw new InvalidOperationException("Target missing or empty: " + settings.TargetId);
                var mesh = target.MeshGeometry;
                var b = mesh.Bounds;
                var bounds = new LatticePlacement.Box(new LatticePlacement.Point(b.X,b.Y,b.Z),new LatticePlacement.Point(b.X+b.SizeX,b.Y+b.SizeY,b.Z+b.SizeZ));
                var reference = new LatticePlacement.Point(b.X+b.SizeX/2,b.Y+b.SizeY/2,b.Z+b.SizeZ/2);
                var centroid = LatticePlacement.Geometry.MeshCentroid(mesh.Positions.Select(p => new LatticePlacement.Point(p.X,p.Y,p.Z)).ToList(),mesh.TriangleIndices.ToList(),reference);
                if (!bounds.ContainsSphere(centroid,0)) throw new InvalidOperationException("Mesh centroid is outside target bounds.");
                string[] scratchIds = { settings.OutputPrefix + "_TMP", settings.OutputPrefix + "_OUT", settings.OutputPrefix + "_IN" };
                var required = scratchIds.Concat(Enumerable.Range(1,settings.MaxVertices).Select(settings.VertexId));
                if (settings.CreateUnionStructure) required = required.Concat(new[] { settings.UnionId });
                foreach (string id in required)
                    if (ss.Structures.Any(s => s.Id.Equals(id,StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("Output ID already exists: " + id + ". Change OutputPrefix for a new run.");
                context.Patient.BeginModifications();
                Structure sphere = Add(ss,scratchIds[0],target.IsHighResolution,owned);
                Structure outside = Add(ss,scratchIds[1],target.IsHighResolution,owned);
                Structure contracted = Add(ss,scratchIds[2],target.IsHighResolution,owned);
                contracted.SegmentVolume = target.SegmentVolume.Margin(-settings.TargetContractionMm);
                if (contracted.IsEmpty) throw new InvalidOperationException("Contracted target is empty.");
                var planeZ = Enumerable.Range(0,image.ZSize).Select(i => image.Origin.z+i*image.ZRes*image.ZDirection.z).ToList();
                var field = ImageBounds(image);
                Func<LatticePlacement.Point,bool> fits = centre =>
                {
                    if (!field.ContainsSphere(centre,settings.VertexDiameterMm/2)) return false;
                    Draw(sphere,centre,settings,image);
                    if (sphere.IsEmpty || sphere.Volume <= 0) return false;
                    outside.SegmentVolume = sphere.SegmentVolume.Sub(contracted.SegmentVolume);
                    return outside.IsEmpty; // Strict containment of ESAPI's discretized segment, no volume-loss tolerance.
                };
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var placement = LatticePlacement.Geometry.Plan(settings,centroid,bounds,planeZ,fits);
                if (placement.Centres.Count < 2)
                    throw new InvalidOperationException("Only " + placement.Centres.Count + " vertex fits; paper requires at least two. No lattice retained.");
                var vertices = new List<Structure>();
                foreach (var centre in placement.Centres)
                {
                    Structure vertex = Add(ss,settings.VertexId(vertices.Count+1),target.IsHighResolution,owned);
                    Draw(vertex,centre,settings,image);
                    outside.SegmentVolume = vertex.SegmentVolume.Sub(contracted.SegmentVolume);
                    if (vertex.IsEmpty || !outside.IsEmpty) throw new InvalidOperationException("Final vertex containment verification failed.");
                    vertices.Add(vertex);
                }
                double minDistance = Double.PositiveInfinity;
                for (int i = 0; i < placement.Centres.Count; i++)
                for (int j = i+1; j < placement.Centres.Count; j++)
                    minDistance = Math.Min(minDistance,LatticePlacement.Geometry.Distance(placement.Centres[i],placement.Centres[j]));
                if (minDistance < settings.CtcMm-1e-7) throw new InvalidOperationException("Final CTC verification failed.");
                if (settings.CreateUnionStructure)
                {
                    Structure union = Add(ss,settings.UnionId,target.IsHighResolution,owned);
                    SegmentVolume combined = vertices[0].SegmentVolume;
                    foreach (Structure vertex in vertices.Skip(1)) combined = combined.Or(vertex.SegmentVolume);
                    union.SegmentVolume = combined;
                    outside.SegmentVolume = union.SegmentVolume.Sub(contracted.SegmentVolume);
                    if (union.IsEmpty || !outside.IsEmpty) throw new InvalidOperationException("Union containment verification failed.");
                    // Verify both set differences against the independently rebuilt union.
                    outside.SegmentVolume = union.SegmentVolume.Sub(combined);
                    if (!outside.IsEmpty) throw new InvalidOperationException("Union has extra segment volume.");
                    outside.SegmentVolume = combined.Sub(union.SegmentVolume);
                    if (!outside.IsEmpty) throw new InvalidOperationException("Union is missing segment volume.");
                }
                foreach (Structure temp in new[] { sphere,outside,contracted })
                {
                    ss.RemoveStructure(temp); owned.Remove(temp);
                }
                watch.Stop();
                var report = new System.Text.StringBuilder();
                report.AppendLine("Lattice created; ESAPI segment containment and requested-centre spacing checks passed.");
                report.AppendLine("Target: " + target.Id + "; prefix: " + settings.OutputPrefix);
                report.AppendLine(String.Format(CultureInfo.InvariantCulture,"Diameter={0} mm; CTC={1} mm; contraction={2} mm",settings.VertexDiameterMm,settings.CtcMm,settings.TargetContractionMm));
                report.AppendLine(String.Format(CultureInfo.InvariantCulture,"Vertices={0}; fit tests={1}; minimum centre distance={2:F4} mm",vertices.Count,placement.FitTests,minDistance));
                report.AppendLine(String.Format(CultureInfo.InvariantCulture,"Initial SI shift={0:F4} mm; end vertices={1}; time={2:F1} s",placement.InitialSliceShiftMm,placement.EndVertices,watch.Elapsed.TotalSeconds));
                report.AppendLine("ID,requested_DICOM_x_mm,requested_DICOM_y_mm,requested_DICOM_z_mm,ESAPI_volume_cc");
                for (int i = 0; i < vertices.Count; i++)
                {
                    var c = placement.Centres[i];
                    report.AppendLine(String.Format(CultureInfo.InvariantCulture,"{0},{1:F4},{2:F4},{3:F4},{4:F6}",vertices[i].Id,c.X,c.Y,c.Z,vertices[i].Volume));
                }
                report.AppendLine("Volumes and realized boundaries depend on CT sampling. Review contours before saving patient changes.");
                // Copyable, local-only review log. No patient identifiers or automatic file export.
                var text = new System.Windows.Controls.TextBox { Text = report.ToString(), IsReadOnly = true, AcceptsReturn = true,
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto };
                new Window { Title = "ESAPI lattice verification", Width = 850, Height = 550, Content = text }.ShowDialog();
            }
            catch (Exception ex)
            {
                var failures = new List<string>();
                if (ss != null) foreach (Structure s in owned.AsEnumerable().Reverse())
                    try { ss.RemoveStructure(s); } catch (Exception cleanup) { failures.Add(s.Id + ": " + cleanup.Message); }
                MessageBox.Show("Lattice generation failed: " + ex.Message +
                    (failures.Count == 0 ? "\nCreated structures removed." : "\nCleanup incomplete; inspect structures:\n" + String.Join("\n",failures)),"ESAPI lattice");
            }
        }
        private static Structure Add(StructureSet ss, string id, bool highResolution, List<Structure> owned)
        {
            if (!ss.CanAddStructure("CONTROL",id)) throw new InvalidOperationException("Cannot add structure: " + id);
            Structure s = ss.AddStructure("CONTROL",id); owned.Add(s);
            string reason;
            if (!s.CanEditSegmentVolume(out reason)) throw new InvalidOperationException(id + ": " + reason);
            if (highResolution && !s.IsHighResolution)
            {
                if (!s.CanConvertToHighResolution()) throw new InvalidOperationException("Cannot match target resolution: " + id);
                s.ConvertToHighResolution();
            }
            return s;
        }
        private static void Draw(Structure s, LatticePlacement.Point centre, LatticePlacement.Settings settings, Image image)
        {
            for (int i = 0; i < image.ZSize; i++) s.ClearAllContoursOnImagePlane(i);
            for (int i = 0; i < image.ZSize; i++)
            {
                double z = image.Origin.z+i*image.ZRes*image.ZDirection.z;
                var circle = LatticePlacement.Geometry.Circle(centre,settings.VertexDiameterMm/2,z,settings.CirclePoints);
                if (circle.Length > 0) s.AddContourOnImagePlane(circle.Select(p => new VVector(p.X,p.Y,p.Z)).ToArray(),i);
            }
        }
        private static void ValidateImage(Image image, double diameter)
        {
            // AddContourOnImagePlane ignores supplied z; explicitly restrict to axis-aligned axial datasets.
            if (Math.Abs(Math.Abs(image.XDirection.x)-1) > 1e-6 || Math.Abs(image.XDirection.y) > 1e-6 || Math.Abs(image.XDirection.z) > 1e-6 ||
                Math.Abs(Math.Abs(image.YDirection.y)-1) > 1e-6 || Math.Abs(image.YDirection.x) > 1e-6 || Math.Abs(image.YDirection.z) > 1e-6 ||
                Math.Abs(Math.Abs(image.ZDirection.z)-1) > 1e-6 || Math.Abs(image.ZDirection.x) > 1e-6 || Math.Abs(image.ZDirection.y) > 1e-6)
                throw new InvalidOperationException("Only axis-aligned axial images are supported; oblique images rejected.");
            if (image.XSize < 2 || image.YSize < 2 || image.ZSize < 3 ||
                !LatticePlacement.Geometry.Finite(image.XRes) || !LatticePlacement.Geometry.Finite(image.YRes) || !LatticePlacement.Geometry.Finite(image.ZRes) ||
                image.XRes <= 0 || image.YRes <= 0 || image.ZRes <= 0)
                throw new InvalidOperationException("Invalid image dimensions or spacing.");
            if (diameter < 3*Math.Max(image.ZRes,Math.Max(image.XRes,image.YRes)))
                throw new InvalidOperationException("Diameter must span at least three largest voxel spacings; refine the image or increase diameter.");
        }
        private static LatticePlacement.Box ImageBounds(Image image)
        {
            double x = image.Origin.x+(image.XSize-1)*image.XRes*image.XDirection.x;
            double y = image.Origin.y+(image.YSize-1)*image.YRes*image.YDirection.y;
            double z = image.Origin.z+(image.ZSize-1)*image.ZRes*image.ZDirection.z;
            return new LatticePlacement.Box(new LatticePlacement.Point(Math.Min(x,image.Origin.x),Math.Min(y,image.Origin.y),Math.Min(z,image.Origin.z)),
                new LatticePlacement.Point(Math.Max(x,image.Origin.x),Math.Max(y,image.Origin.y),Math.Max(z,image.Origin.z)));
        }
    }
}
#endif
