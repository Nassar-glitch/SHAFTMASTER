using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Elevator.AutoCAD.Plan
{
    public sealed class ShaftFrame
    {
        public Point3d Origin;
        public Vector3d XAxis;
        public Vector3d YAxis;
        public Vector3d ZAxis;

        public Point3d Local(Point3d p)
        {
            Vector3d d = p - Origin;
            return new Point3d(d.DotProduct(XAxis), d.DotProduct(YAxis), d.DotProduct(ZAxis));
        }

        public Point3d World(Point3d p)
        {
            return Origin + p.X * XAxis + p.Y * YAxis + p.Z * ZAxis;
        }

        public Vector3d Vector(double x, double y)
        {
            return x * XAxis + y * YAxis;
        }
    }
}
