using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Geometry;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Plan
{
    /// <summary>Maps plan points from the old to the new design dimensions.</summary>
    public sealed class PlanGeometryAdapter
    {
        public readonly ShaftFrame Frame;
        public readonly ElevatorParameters Old;
        public readonly ElevatorParameters Next;
        private readonly GeometryCalculator calculator;

        public PlanGeometryAdapter(DrawingSnapshot s, ElevatorParameters next)
        {
            Frame = s.Frame;
            Old = s.ElevatorParameters;
            Next = next;
            calculator = new GeometryCalculator(Old, Next);
        }

        /// <summary>Horizontal shift of an anchor.</summary>
        public double DX(string a)
        {
            return calculator.DX(a);
        }

        /// <summary>Vertical shift of an anchor.</summary>
        public double DY(string a)
        {
            return calculator.DY(a);
        }

        /// <summary>Maps an X value by the shifts of the given anchors.</summary>
        public double MapX(double x, params string[] a)
        {
            return calculator.MapX(x, a);
        }

        /// <summary>Maps a Y value by the shifts of the given anchors.</summary>
        public double MapY(double y, params string[] a)
        {
            return calculator.MapY(y, a);
        }

        /// <summary>True when the role stretches instead of moving.</summary>
        public bool IsFlexible(string role)
        {
            return calculator.IsFlexible(role);
        }

        /// <summary>The translation of a role.</summary>
        public Vector3d Translation(string role)
        {
            var d = calculator.Translation(role);
            return Frame.Vector(d.X, d.Y);
        }

        /// <summary>Maps a point of the old design to the new design by its role.</summary>
        public Point3d MapPoint(Point3d world, string role)
        {
            // Keep the original rigid world translation to preserve roundoff behavior.
            if (!IsFlexible(role))
                return world + Translation(role);
            var p = Frame.Local(world);
            var n = calculator.MapPoint(new LocalPoint(p.X, p.Y, p.Z), role);
            return Frame.World(new Point3d(n.X, n.Y, n.Z));
        }
    }
}
