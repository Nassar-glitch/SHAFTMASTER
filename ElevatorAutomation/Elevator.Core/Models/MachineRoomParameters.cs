using System.Collections.Generic;
namespace Elevator.Core
{
    public sealed class DeflectorSheaveSelection
    {
        public bool IsRequired {get;set;}
        public double? PitchDiameterMm {get;set;}
        public string SelectionStatus {get;set;}
        public string RuleSource {get;set;}
    }
    public sealed class MachineRoomExtensions
    {
        public double Left,Right,Front,Rear;
    }
    public sealed class MachineRoomParameters
    {
        public MachineRoomExtensions Extensions;
        public double RoomWidth,RoomDepth;
        public DeflectorSheaveSelection Deflector;
        public GovernorOffsetSelection Governor;
        public ReevingGeometry Reeving;
        public List<EngineeringRule> Rules;
        // CAD validation sets PASS only after the current view has been checked.
        public string LayoutStatus="NOT YET EVALUATED";
        public string CodeDiameterRatioStatus="NOT YET EVALUABLE";
        public double[] AvailableDeflectorPitchDiametersMm {get{return new double[]{250,400};}}
    }
    public sealed class GovernorOffsetSelection
    {
        public double? OffsetFromCarGuideAxisMm;
        public string SelectionStatus,RuleSource;
    }
    public sealed class ReevingGeometry
    {
        public double? TractionSheavePitchDiameterMm,DeflectorSheavePitchDiameterMm;
        public LocalPoint? TractionSheaveCenter,DeflectorSheaveCenter;
        public double? HorizontalCenterOffsetMm,VerticalCenterOffsetMm;
        public LocalPoint? RopeEntryDirection,RopeExitDirection;
        public double TargetTractionWrapAngleDeg=152.0;
        public double? CalculatedWrapAngleDeg;
        public double? RopeEntryAngleDeg,RopeExitAngleDeg;
        public double? RequiredChassisElevationAdjustmentMm;
        public string WrapAngleStatus="NOT YET EVALUABLE";
        public bool AllowDeflectorDiameterSubstitution {get{return false;}}
        public string FutureOptimizationStrategy {get{return "Solve center separation using the adjustable hanger; raise concrete supports to provide slab space, retaining the TZ-selected 250 or 400 mm diameter. Section B-B evaluates the permitted geometry; mechanical and full traction checks remain separate.";}}
        public string TractionVerificationResult="NOT YET EVALUABLE - vertical reeving geometry and EN 81-50 calculation required";
    }
    public enum RequirementType {Code,Manufacturer,Company,Project,EngineeringAssumption}
    public sealed class EngineeringRule
    {
        public string RuleId,Description,SourceDocument,Edition,Clause;
        public RequirementType RequirementType;
        public Dictionary<string,object> InputParameters;
        public string CalculatedValue,RequiredValue,Status,EngineeringNote;
    }
}
