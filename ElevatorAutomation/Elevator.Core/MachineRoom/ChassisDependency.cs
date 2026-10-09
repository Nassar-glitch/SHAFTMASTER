using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    public sealed class ChassisDependency
    {
        public string Behavior;
        public string[] References;
        public LocalPoint[] Offsets;
        public double? CircleRadius;
    }
}
