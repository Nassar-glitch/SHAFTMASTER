using Elevator.Core.MachineRoom;
using Elevator.Core.Project;

namespace Elevator.Core.Geometry
{
    /// <summary>The coordinated plan and B-B results that feed the vertical section.</summary>
    public sealed class VerticalSource
    {
        public ElevatorParameters Plan;
        public MachineRoomSectionParameters Section;
        public double FrontWallThicknessMm;
        public double RearWallThicknessMm;
        public double RoomFrontX;
        public double RoomRearX;
        public double[] CarRailProjectedEdges;
        public double[] CwRailProjectedEdges;
        public double[] CarBracketProjectedEdges;
        public double[] CwBracketProjectedEdges;
        public double LandingDoorBackX;
        public double LandingDoorFrontX;
        public double CarDoorBackX;
        public double CarDoorFrontX;
        public double CarRearOuterX;
        public double? GovernorProjectedAxisX;
        public string CodeDiameterRatioStatus;
        public string GovernorStatus;
    }
}
