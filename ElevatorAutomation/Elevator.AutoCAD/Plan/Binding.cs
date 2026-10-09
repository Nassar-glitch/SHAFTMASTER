using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace Elevator.AutoCAD.Plan
{
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
