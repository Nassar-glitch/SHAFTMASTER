using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    /// <summary>The load one carrying member passes to the front and rear transverse members.</summary>
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
