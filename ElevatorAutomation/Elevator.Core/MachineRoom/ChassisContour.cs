using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    /// <summary>A contour of the native chassis drawing with its role and dependency.</summary>
    public sealed class ChassisContour
    {
        public string Id;
        public string Role;
        public bool Straight;
        public LocalPoint[] Points;
        public ChassisDependency Dependency;
    }
}
