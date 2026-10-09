using Elevator.Core.Geometry;

namespace Elevator.Core.Suspension
{
    /// <summary>Reeving geometry of the machine room: sheave diameters, centres, rope directions and wrap angle.</summary>
    public sealed class ReevingGeometry
    {
        public double? TractionSheavePitchDiameterMm;
        public double? DeflectorSheavePitchDiameterMm;
        public LocalPoint? TractionSheaveCenter;
        public LocalPoint? DeflectorSheaveCenter;
        public double? HorizontalCenterOffsetMm;
        public double? VerticalCenterOffsetMm;
        public LocalPoint? RopeEntryDirection;
        public LocalPoint? RopeExitDirection;
        public double TargetTractionWrapAngleDeg = 152.0;
        public double? CalculatedWrapAngleDeg;
        public double? RopeEntryAngleDeg;
        public double? RopeExitAngleDeg;
        public double? RequiredChassisElevationAdjustmentMm;
        public string WrapAngleStatus = "NOT YET EVALUABLE";

        public bool AllowDeflectorDiameterSubstitution
        {
            get
            {
                return false;
            }
        }

        public string FutureOptimizationStrategy
        {
            get
            {
                return "Solve center separation using the adjustable hanger; raise concrete supports to provide slab space, retaining the "
                    + "TZ-selected 250 or 400 mm diameter. Section B-B evaluates the permitted geometry; mechanical and full traction "
                    + "checks remain separate.";
            }
        }

        public string TractionVerificationResult = "NOT YET EVALUABLE - vertical reeving geometry and EN 81-50 calculation required";
    }
}
