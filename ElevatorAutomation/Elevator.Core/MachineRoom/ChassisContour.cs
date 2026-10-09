using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    public sealed class ChassisContour
    {
        public string Id;
        public string Role;
        public bool Straight;
        public LocalPoint[] Points;
        public ChassisDependency Dependency;
    }
}
