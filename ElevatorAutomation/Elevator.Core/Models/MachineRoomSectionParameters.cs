using System;
namespace Elevator.Core
{
    public sealed class SectionReference
    {
        public double SourceCarRow,SourceCwRow,SourceSD,SourceTractionX,SourceTractionZ,SourceDeflectorX,SourceDeflectorZ;
        public double SourceSteelBottomZ,SourceSteelTopZ,SourceConcreteHeight,SourceFrontSupportX,SourceRearSupportX,SourceSlabThickness;
        public double CarOpeningWidth,CwOpeningWidth,HangerTopBelowSteelTop,HangerBottomAboveDeflectorCenter;
        public double SourceStandBottomZ;
    }
    public sealed class MachineRoomSectionParameters
    {
        public MachineBeamSelection Beam;
        public VerticalArrangementResult Arrangement;
        public SectionReference Reference;
        public double FrontSupportX,RearSupportX,SteelBottomZ,SteelTopZ,TractionX,TractionZ,DeflectorX,DeflectorZ,DeflectorPitchMm;
        public double CarDropX,CwDropX,CarOpeningWidth,CwOpeningWidth,ConcreteHeight;
        public bool DeflectorVisible,HasCalculatedPath;
        public string LayoutStatus,ReevingGeometryStatus,RopeClearanceStatus="NOT YET EVALUABLE",ConcreteBeamStructuralStatus="NOT EVALUATED";
        public string OpeningStatus="OPENING SIZE ENGINEERING REVIEW REQUIRED";
        public string MachineBeamStructuralStatus {get{return Beam==null?"NOT EVALUATED":Beam.StructuralStatus;}}
    }
    public sealed class MachineBeamSelection
    {
        public string SectionDesignation,SelectionMethod="PreliminaryProjectRule",StructuralStatus;
        public double NominalDepthMm,SpanMm,CapacityKg;
        public double? ReactionN,MaxBendingMomentNm,MaxShearN,BendingStressMPa,DeflectionMm,Utilization;
    }
    public sealed class ReevingSolution
    {
        public LocalPoint TractionSheaveCenter,DeflectorSheaveCenter,TractionTangentPoint,DeflectorTangentPoint;
        public LocalPoint RopeEntryDirection,RopeExitDirection;
        public double TractionSheavePitchDiameterMm,DeflectorSheavePitchDiameterMm;
        public double HorizontalCenterOffsetMm,VerticalCenterOffsetMm,CenterDistanceMm;
        public double? CalculatedWrapAngleDeg,RopeEntryAngleDeg,RopeExitAngleDeg;
        public double TargetWrapAngleDeg=152;
        public string GeometryStatus,WrapAngleStatus;
    }
    public sealed class VerticalArrangementInput
    {
        public double CarDropX,CwDropX,TractionDiameterMm,DeflectorDiameterMm;
        public double MachineCenterAboveSteelMm,FixedLevelingDepthMm,SteelDepthMm;
        public double HangerTopBelowSteelTopMm,HangerBottomAboveDeflectorCenterMm;
        public double? MinimumDeflectorSlabClearanceMm;
        public int StackedSteelLevels;
        public double MinConcreteHeightMm=350,MaxConcreteHeightMm=1000,TargetWrapAngleDeg=152;
        public bool AdjustableDeflectorHangerConfirmed;
        public double? PreferredHeightIncrementMm;
        public double? ReferenceConcreteHeightMm;
    }
    public sealed class VerticalArrangementResult
    {
        public ReevingSolution Reeving;
        public double? ConcreteBeamHeightMm,RequiredConcreteBeamHeightMm,RequiredChassisElevationAdjustmentMm;
        public double? RequiredVerticalCenterSeparationMm,RequiredDeflectorCenterZ,RequiredTractionSheaveCenterZ,RequiredHangerLengthMm,DeflectorToSlabClearanceMm;
        public string DeflectorSlabClearanceStatus="NOT YET EVALUABLE",HangerMechanicalStatus="MECHANICAL REVIEW REQUIRED";
        public string Status,ConcreteBeamStructuralStatus="NOT EVALUATED";
        public double? ConcreteSupportHeightMm {get{return ConcreteBeamHeightMm;}}
        public double? CalculatedWrapAngleDeg {get{return Reeving==null?null:Reeving.CalculatedWrapAngleDeg;}}
    }
}
