using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Geometry;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Plan
{
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

        public double DX(string a)
        {
            return calculator.DX(a);
        }

        public double DY(string a)
        {
            return calculator.DY(a);
        }

        public double MapX(double x, params string[] a)
        {
            return calculator.MapX(x, a);
        }

        public double MapY(double y, params string[] a)
        {
            return calculator.MapY(y, a);
        }

        public bool IsFlexible(string role)
        {
            return calculator.IsFlexible(role);
        }

        public Vector3d Translation(string role)
        {
            var d = calculator.Translation(role);
            return Frame.Vector(d.X, d.Y);
        }

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
