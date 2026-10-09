using System;

namespace Elevator.Core
{
    public sealed class SectionReference
    {
        public double SourceCarRow;
        public double SourceCwRow;
        public double SourceSD;
        public double SourceTractionX;
        public double SourceTractionZ;
        public double SourceDeflectorX;
        public double SourceDeflectorZ;
        public double SourceSteelBottomZ;
        public double SourceSteelTopZ;
        public double SourceConcreteHeight;
        public double SourceFrontSupportX;
        public double SourceRearSupportX;
        public double SourceSlabThickness;
        public double CarOpeningWidth;
        public double CwOpeningWidth;
        public double HangerTopBelowSteelTop;
        public double HangerBottomAboveDeflectorCenter;
        public double SourceStandBottomZ;
    }

    public sealed class MachineRoomSectionParameters
    {
        public MachineBeamSelection Beam;
        public VerticalArrangementResult Arrangement;
        public SectionReference Reference;
        public double FrontSupportX;
        public double RearSupportX;
        public double SteelBottomZ;
        public double SteelTopZ;
        public double TractionX;
        public double TractionZ;
        public double DeflectorX;
        public double DeflectorZ;
        public double DeflectorPitchMm;
        public double CarDropX;
        public double CwDropX;
        public double CarOpeningWidth;
        public double CwOpeningWidth;
        public double ConcreteHeight;
        public bool DeflectorVisible;
        public bool HasCalculatedPath;
        public string LayoutStatus;
        public string ReevingGeometryStatus;
        public string RopeClearanceStatus = "NOT YET EVALUABLE";
        public string ConcreteBeamStructuralStatus = "NOT EVALUATED";
        public string OpeningStatus = "OPENING SIZE ENGINEERING REVIEW REQUIRED";

        public string MachineBeamStructuralStatus
        {
            get
            {
                return Beam == null ? "NOT EVALUATED" : Beam.StructuralStatus;
            }
        }
    }

    public sealed class MachineBeamSelection
    {
        public string SectionDesignation;
        public string SelectionMethod = "PreliminaryProjectRule";
        public string StructuralStatus;
        public double NominalDepthMm;
        public double SpanMm;
        public double CapacityKg;
        public double? ReactionN;
        public double? MaxBendingMomentNm;
        public double? MaxShearN;
        public double? BendingStressMPa;
        public double? DeflectionMm;
        public double? Utilization;
    }

    public sealed class ReevingSolution
    {
        public LocalPoint TractionSheaveCenter;
        public LocalPoint DeflectorSheaveCenter;
        public LocalPoint TractionTangentPoint;
        public LocalPoint DeflectorTangentPoint;
        public LocalPoint RopeEntryDirection;
        public LocalPoint RopeExitDirection;
        public double TractionSheavePitchDiameterMm;
        public double DeflectorSheavePitchDiameterMm;
        public double HorizontalCenterOffsetMm;
        public double VerticalCenterOffsetMm;
        public double CenterDistanceMm;
        public double? CalculatedWrapAngleDeg;
        public double? RopeEntryAngleDeg;
        public double? RopeExitAngleDeg;
        public double TargetWrapAngleDeg = 152;
        public string GeometryStatus;
        public string WrapAngleStatus;
    }

    public sealed class VerticalArrangementInput
    {
        public double CarDropX;
        public double CwDropX;
        public double TractionDiameterMm;
        public double DeflectorDiameterMm;
        public double MachineCenterAboveSteelMm;
        public double FixedLevelingDepthMm;
        public double SteelDepthMm;
        public double HangerTopBelowSteelTopMm;
        public double HangerBottomAboveDeflectorCenterMm;
        public double? MinimumDeflectorSlabClearanceMm;
        public int StackedSteelLevels;
        public double MinConcreteHeightMm = 350;
        public double MaxConcreteHeightMm = 1000;
        public double TargetWrapAngleDeg = 152;
        public bool AdjustableDeflectorHangerConfirmed;
        public double? PreferredHeightIncrementMm;
        public double? ReferenceConcreteHeightMm;
    }

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
