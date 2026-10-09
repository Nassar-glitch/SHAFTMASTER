using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Elevator.AutoCAD.Plan
{
    /// <summary>Expected loops and area of a plan hatch.</summary>
    public sealed class HatchExpectation
    {
        public double Area;
        public double Scale;
        public double Angle;
        public int Loops;
        public string Pattern;
        public Point3d Min;
        public Point3d Max;
    }
}
