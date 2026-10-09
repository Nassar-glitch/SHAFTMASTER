using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Elevator.AutoCAD.Plan
{
    /// <summary>The local frame of the shaft: origin and axes.</summary>
    public sealed class ShaftFrame
    {
        public Point3d Origin;
        public Vector3d XAxis;
        public Vector3d YAxis;
        public Vector3d ZAxis;

        /// <summary>Converts a world point to the local frame.</summary>
        public Point3d Local(Point3d p)
        {
            Vector3d d = p - Origin;
            return new Point3d(d.DotProduct(XAxis), d.DotProduct(YAxis), d.DotProduct(ZAxis));
        }

        /// <summary>Converts a local point to the world frame.</summary>
        public Point3d World(Point3d p)
        {
            return Origin + p.X * XAxis + p.Y * YAxis + p.Z * ZAxis;
        }

        /// <summary>A vector of the local frame in the world frame.</summary>
        public Vector3d Vector(double x, double y)
        {
            return x * XAxis + y * YAxis;
        }
    }
}
