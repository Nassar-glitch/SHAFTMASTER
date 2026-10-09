using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    public sealed class MemberTransfer
    {
        public string LoadId;
        public string MemberId;
        public double FrontDownwardN;
        public double RearDownwardN;
        public LocalPoint FrontPointMm;
        public LocalPoint RearPointMm;
    }
}
