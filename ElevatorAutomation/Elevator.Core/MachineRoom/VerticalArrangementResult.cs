using Elevator.Core.Suspension;

namespace Elevator.Core.MachineRoom
{
    /// <summary>Result of the vertical machine arrangement: levels, concrete height, reeving and status.</summary>
    public sealed class VerticalArrangementResult
    {
        public ReevingSolution Reeving;
        public double? ConcreteBeamHeightMm;
        public double? RequiredConcreteBeamHeightMm;
        public double? RequiredChassisElevationAdjustmentMm;
        public double? RequiredVerticalCenterSeparationMm;
        public double? RequiredDeflectorCenterZ;
        public double? RequiredTractionSheaveCenterZ;
        public double? RequiredHangerLengthMm;
        public double? DeflectorToSlabClearanceMm;
        public string DeflectorSlabClearanceStatus = "NOT YET EVALUABLE";
        public string HangerMechanicalStatus = "MECHANICAL REVIEW REQUIRED";
        public string Status;
        public string ConcreteBeamStructuralStatus = "NOT EVALUATED";

        public double? ConcreteSupportHeightMm
        {
            get
            {
                return ConcreteBeamHeightMm;
            }
        }

        public double? CalculatedWrapAngleDeg
        {
            get
            {
                return Reeving == null ? null : Reeving.CalculatedWrapAngleDeg;
            }
        }
    }
}
