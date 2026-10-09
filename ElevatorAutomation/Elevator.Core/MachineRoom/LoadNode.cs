using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    /// <summary>A load applied to the chassis: position, force and couple.</summary>
    public sealed class LoadNode
    {
        public string Id;
        public string Source;
        public string Kind;
        public LocalPoint PositionMm;
        public LocalPoint ForceN;
        public LocalPoint CoupleNmm;
        public bool AssumedLocation;
    }
}
