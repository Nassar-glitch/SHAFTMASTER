using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Elevator.AutoCAD.Plan
{
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
