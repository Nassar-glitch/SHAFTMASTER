using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
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
