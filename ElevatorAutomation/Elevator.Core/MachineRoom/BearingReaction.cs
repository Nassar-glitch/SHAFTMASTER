using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    /// <summary>Vertical reaction at one machine-chassis bearing.</summary>
    public sealed class BearingReaction
    {
        public string Id;
        public LocalPoint PositionMm;
        public double UpwardN;
    }
}
