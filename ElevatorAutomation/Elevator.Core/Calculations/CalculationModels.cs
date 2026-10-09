using System;
using System.Collections.Generic;
namespace Elevator.Core.Calculations
{
    // Optional canonical inputs. Null is unavailable; zero is only an explicit supplied value.
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
    public sealed class CalculationIssue
    {
        public string Field;
        public string Message;
        public CalculationIssue()
        {
        }
        public CalculationIssue(string f, string m)
        {
            Field = f;
            Message = m;
        }
    }
    public sealed class BearingSeat
    {
        public string Id;
        public string Evidence;
        public LocalPoint[] ContactBoundary;
        public LocalPoint Centre;
    }
    public sealed class ChassisMember
    {
        public string Id;
        public string Evidence;
        public LocalPoint Front;
        public LocalPoint Rear;
    }
    public sealed class ChassisCalculationGeometry
    {
        public string Recipe = "Advanced";
        public ChassisGeometryProvenance Provenance
        {
            get; private set;
        }
        internal void BindSource(GeometrySourceSnapshot source)
        {
            Provenance = new ChassisGeometryProvenance(source, this);
        }
        public string Units = "mm";
        public string Axes = "X right; Y entrance to rear; Z above machine-room slab";
        public List<BearingSeat> Bearings = new List<BearingSeat>();
        public List<ChassisMember> LongitudinalMembers = new List<ChassisMember>();
        public LocalPoint TractionCentre;
        public LocalPoint DeflectorCentre;
        public LocalPoint[] NativeMachineMountingContours; // Geometric region only, not feet/CG.
        public double ShaftWidthMm;
        public double ShaftDepthMm;
        public double BearingPlaneZMm;
        public double HangerAttachmentZMm;
        public MachineRoomSectionParameters Section;
        public List<CalculationIssue> Issues = new List<CalculationIssue>();
    }
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
    public sealed class MemberTransfer
    {
        public string LoadId;
        public string MemberId;
        public double FrontDownwardN;
        public double RearDownwardN;
        public LocalPoint FrontPointMm;
        public LocalPoint RearPointMm;
    }
    public sealed class BearingReaction
    {
        public string Id; public LocalPoint PositionMm; public double UpwardN;
    }
    public sealed class ChassisCalculationResult
    {
        public string Mode = "Advanced";
        public double? TotalMassKg;
        public double? TotalVerticalLoadN;
        public Dictionary<string, double> MassCompositionKg;
        public SimplifiedDistances SimplifiedGeometry;
        public string Status = "MISSING_INPUT";
        public string ModelVersion = "ReleasedTwoStageWithRigidSpreader-v1";
        public string InputFingerprint;
        public string Units = "mm, kg, N, N.mm (divide N.mm by 1000 for N.m)";
        public string VerificationStatus = "ENGINEERING REVIEW REQUIRED - simplified reactions only";
        public List<CalculationIssue> Issues = new List<CalculationIssue>();
        public List<LoadNode> Loads = new List<LoadNode>();
        public List<MemberTransfer> Transfers = new List<MemberTransfer>();
        public List<BearingReaction> Reactions; // Null means unavailable; never four fabricated zeros.
        public double? VerticalResidualN;
        public double? MomentXResidualNmm;
        public double? MomentYResidualNmm;
        public LocalPoint UnverifiedLateralForceN;
        public double UnverifiedYawMomentNmm;
        public double? CarBundleTensionN;
        public double? CounterweightBundleTensionN;
        public double? CarPerRopeTensionN;
        public double? CounterweightPerRopeTensionN;
        public List<string> Assumptions = new List<string>();
    }
}
