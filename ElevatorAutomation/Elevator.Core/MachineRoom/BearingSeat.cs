using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    public sealed class BearingSeat
    {
        public string Id;
        public string Evidence;
        public LocalPoint[] ContactBoundary;
        public LocalPoint Centre;
    }
}
