using System.Collections.Generic;

namespace Elevator.Core.Geometry
{
    /// <summary>Experimental schema, deliberately independent of the validated ProjectInput schema.</summary>
    public sealed class VerticalProjectData
    {
        public int SchemaVersion = 1;
        public string NamingConvention = "BG";
        public string FirstLevel = "B";
        public List<VerticalFloor> Floors = new List<VerticalFloor>();
        public double PitDepthMm = 1000;
        public double OverheadMm = 4200;
        public double MachineRoomClearHeightMm = 2200;
        public double CarInternalHeightMm = 2200;
        public double CarOverallHeightMm = 2400;
        public double DoorClearHeightMm = 2000;
        public double DoorOpeningHeightMm = 2160;
        public double LandingSlabGraphicalThicknessMm = 250;
        public double CounterweightOverallHeightMm = 3000;
        public double RatedSpeedMps = 1;
        public double CarEnvelopeBottomBelowCarFFLMm = 100;
        public string CarEnvelopeOffsetSource = "PROVISIONAL_V1_DRAWING_DEFAULT";
        public double? CWBottomAbovePitFloorAtHighestServedLandingMm;
        public double ProvisionalCWCalibrationMm = 300;

        public string CWAbsolutePositionStatus
        {
            get
            {
                return CWBottomAbovePitFloorAtHighestServedLandingMm.HasValue
                    ? "PROJECT_REFERENCE_SUPPLIED"
                    : "PROVISIONAL_REFERENCE_INPUT_REQUIRED";
            }
        }

        public string CarReferenceFloorId;
        public double LevelingOffsetMm;
        public double RequestedBracketMaxSpacingMm; // Mandatory: no default.
        public string BracketSpacingSource = "ProjectInputPendingFutureCalculation";
        public string SuspensionTopology = "1:1";
        public string PitDatumDefinition = "Lowest modeled normal-floor FFL (Z=0) to finished pit floor";
        public string OverheadDatumDefinition = "Highest served FFL to underside of MR floor slab";
        public string FloorDatumDefinition = "FFL = graphical slab top; R = coordinated MR slab top";
        public string FixtureLabel;
    }
}
