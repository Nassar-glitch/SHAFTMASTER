using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    /// <summary>How a native chassis contour follows the shared design dimensions.</summary>
    public sealed class ChassisDependency
    {
        public string Behavior;
        public string[] References;
        public LocalPoint[] Offsets;
        public double? CircleRadius;
    }
}
