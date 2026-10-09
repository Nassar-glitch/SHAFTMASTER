using Elevator.Core.GuideRails;
using Elevator.Core.SafetyGear;
using Elevator.Core.Suspension;

namespace Elevator.Core.Calculations
{
    /// <summary>Optional canonical inputs. Null is unavailable; zero is only an explicit supplied value.</summary>
    public sealed class ChassisCalculationInputs
    {
        public string Mode; // Basic / Advanced. Null legacy projects resolve without discarding data.
        public SafetyPitInputs SafetyPit; // Independent event workflow; null in older projects.
        public CarGuideForceInputs CarGuideForces; // Independent FF note workflow; no fabricated shoe spacing.

        // Independent rope / traction workflow (EN 81-20 5.5, EN 81-50 5.11/5.12); null in older projects.
        public RopeTractionInputs RopeTraction;
        public string LoadCase; // StaticRatedLoad1To1 only (no acceleration/braking factors).
        public double? CarMassKg;
        public double? CounterweightMassKg;
        public double? MachineMassKg;
        public double? CarSideAdditionalSuspendedMassKg;
        public double? CounterweightSideAdditionalSuspendedMassKg;

        // Relative to calculated traction centre, never its contour or an inferred CG.
        public double? MachineCgOffsetXMm;
        public double? MachineCgOffsetYMm;
        public double? MachineCgOffsetZMm;
        public string MachineCgSource;
        public string LeftCarryingMemberId;
        public string RightCarryingMemberId;
        public string TransferModel; // ReleasedTwoStageWithRigidSpreader-v1
        public string TransferAssumptionSource;
        public double? HangerAttachmentOffsetXMm;
        public double? HangerAttachmentOffsetYMm;
        public string HangerAttachmentSource;
        public bool? IncludeChassisSelfWeight;
        public string ChassisWeightSourceOrExclusionReason;
        public double? ChassisMassKg;
        public double? ChassisCgOffsetXMm;
        public double? ChassisCgOffsetYMm;
        public double? ChassisCgOffsetZMm;
        public bool? IncludeHangerAssemblyWeight;
        public string HangerWeightSourceOrExclusionReason;
        public double? HangerAssemblyMassKg;
        public double? HangerCgOffsetXMm;
        public double? HangerCgOffsetYMm;
        public double? HangerCgOffsetZMm;
    }
}
