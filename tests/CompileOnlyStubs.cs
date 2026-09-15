// COMPILATION ONLY. These signatures check syntax in the Eclipse adapter on non-Windows hosts.
// Not Varian assemblies, not an ESAPI simulator, and not evidence of Eclipse compatibility.
// Never deploy this file to Eclipse. Every operational member deliberately throws.
using System;
using System.Collections.Generic;
namespace System.Windows
{
    public class Window
    {
        public string Title { get; set; } public double Width { get; set; } public double Height { get; set; } public object Content { get; set; }
        public bool? ShowDialog() { throw new NotSupportedException(); }
    }
    public static class MessageBox { public static void Show(string message, string title) { throw new NotSupportedException(); } }
}
namespace System.Windows.Controls
{
    public enum ScrollBarVisibility { Auto }
    public class TextBox
    {
        public string Text { get; set; } public bool IsReadOnly { get; set; } public bool AcceptsReturn { get; set; }
        public ScrollBarVisibility VerticalScrollBarVisibility { get; set; } public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; }
    }
}
namespace VMS.TPS.Common.Model.Types
{
    public struct VVector { public double x,y,z; public VVector(double a,double b,double c) {x=a;y=b;z=c;} }
}
namespace VMS.TPS.Common.Model.API
{
    using VMS.TPS.Common.Model.Types;
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ESAPIScriptAttribute : Attribute { public bool IsWriteable { get; set; } }
    public class ScriptContext { public Patient Patient {get;set;} public StructureSet StructureSet {get;set;} }
    public class Patient { public void BeginModifications() {throw new NotSupportedException();} }
    public class Image
    {
        public int XSize {get;set;} public int YSize {get;set;} public int ZSize {get;set;}
        public double XRes {get;set;} public double YRes {get;set;} public double ZRes {get;set;}
        public VVector Origin {get;set;} public VVector XDirection {get;set;} public VVector YDirection {get;set;} public VVector ZDirection {get;set;}
    }
    public class StructureSet
    {
        public Image Image {get;set;} public IEnumerable<Structure> Structures {get;set;}
        public bool CanAddStructure(string type,string id) {throw new NotSupportedException();}
        public Structure AddStructure(string type,string id) {throw new NotSupportedException();}
        public void RemoveStructure(Structure structure) {throw new NotSupportedException();}
    }
    public struct MeshPoint {public double X,Y,Z;}
    public struct MeshBounds {public double X,Y,Z,SizeX,SizeY,SizeZ;}
    public class Mesh {public MeshBounds Bounds {get;set;} public IList<MeshPoint> Positions {get;set;} public IList<int> TriangleIndices {get;set;} }
    public class Structure
    {
        public string Id {get;set;} public bool IsEmpty {get;set;} public bool HasSegment {get;set;} public bool IsHighResolution {get;set;}
        public double Volume {get;set;} public Mesh MeshGeometry {get;set;} public SegmentVolume SegmentVolume {get;set;}
        public bool CanEditSegmentVolume(out string reason) {throw new NotSupportedException();}
        public bool CanConvertToHighResolution() {throw new NotSupportedException();}
        public void ConvertToHighResolution() {throw new NotSupportedException();}
        public void ClearAllContoursOnImagePlane(int plane) {throw new NotSupportedException();}
        public void AddContourOnImagePlane(VVector[] points,int plane) {throw new NotSupportedException();}
    }
    public class SegmentVolume
    {
        public SegmentVolume Margin(double mm) {throw new NotSupportedException();}
        public SegmentVolume Sub(SegmentVolume other) {throw new NotSupportedException();}
        public SegmentVolume Or(SegmentVolume other) {throw new NotSupportedException();}
    }
}
