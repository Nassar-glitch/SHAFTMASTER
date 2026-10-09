using Autodesk.AutoCAD.DatabaseServices;

namespace Elevator.AutoCAD.Plan
{
    /// <summary>Binding of one plan entity: key, type, role and annotation template.</summary>
    public sealed class Binding
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public string Role { get; set; }
        public string Axis { get; set; }
        public string A { get; set; }
        public string B { get; set; }
        public string Band { get; set; }
        public string AnnotationTemplate { get; set; }
    }
}
