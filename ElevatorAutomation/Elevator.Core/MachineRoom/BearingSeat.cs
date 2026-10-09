using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    /// <summary>A machine-chassis bearing seat: contact boundary, centre and evidence.</summary>
    public sealed class BearingSeat
    {
        public string Id;
        public string Evidence;
        public LocalPoint[] ContactBoundary;
        public LocalPoint Centre;
    }
}
