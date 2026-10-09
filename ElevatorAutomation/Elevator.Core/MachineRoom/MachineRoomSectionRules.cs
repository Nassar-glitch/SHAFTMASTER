using System;
using System.Collections.Generic;
using Elevator.Core.Project;
using Elevator.Core.Suspension;

namespace Elevator.Core.MachineRoom
{
    public static class MachineRoomSectionRules
    {
        public static MachineRoomSectionParameters Calculate(
            ProjectInput input,
            ElevatorParameters shared,
            SectionReference r,
            double frontSupportDepth,
            double rearSupportOffset,
            double retainedPitch,
            bool retainedVisible)
        {
            double rearSupportDepth = shared.SD + rearSupportOffset;
            if (!input.Capacity.HasValue)
                throw new ArgumentException("Section requires stored Capacity (kg). Run ELEVATORREEVINGDATA once.");
            SuspensionProjectRules.Validate(input.SuspensionRopeDiameterMm, input.NumberOfSuspensionRopes);
            if (input.TractionSheavePitchDiameterMm.HasValue && Math.Abs(input.TractionSheavePitchDiameterMm.Value - 520) > .02)
            {
                throw new ArgumentException(
                                    "The coordinated native machine has a 520 mm traction projection; another machine requires a reviewed "
                                    + "template.");
            }
            var m = shared.MachineRoom;
            var p = new MachineRoomSectionParameters
            {
                Reference = r,
                FrontSupportX = -frontSupportDepth,
                RearSupportX = -rearSupportDepth,
                CarDropX = -shared.CarRow,
                CwDropX = -shared.CwRow,
                CarOpeningWidth = r.CarOpeningWidth,
                CwOpeningWidth = r.CwOpeningWidth
            };
            p.Beam = new PreliminaryMachineBeamSelector().Select(input.Capacity.Value, rearSupportDepth - frontSupportDepth);
            p.DeflectorVisible =
                m.Deflector.SelectionStatus == "REQUIRES TRACTION/REEVING REVIEW" ? retainedVisible : m.Deflector.IsRequired;
            p.DeflectorPitchMm = m.Deflector.PitchDiameterMm ?? retainedPitch;
            if (Math.Abs(p.DeflectorPitchMm - 250) < .02)
                p.DeflectorPitchMm = 250;
            else if (Math.Abs(p.DeflectorPitchMm - 400) < .02)
                p.DeflectorPitchMm = 400;
            if (p.DeflectorPitchMm != 250 && p.DeflectorPitchMm != 400)
                throw new ArgumentException("Retained deflector is outside the supported 250/400 market sizes.");
            double fixedStack = r.SourceSteelBottomZ - r.SourceStandBottomZ, machineOffset = r.SourceTractionZ - r.SourceSteelTopZ;
            if (m.Deflector.PitchDiameterMm.HasValue)
            {
                p.Arrangement = ReevingSolver.SolveVertical(new VerticalArrangementInput
                {
                    CarDropX = p.CarDropX,
                    CwDropX = p.CwDropX,
                    TractionDiameterMm = 520,
                    DeflectorDiameterMm = p.DeflectorPitchMm,
                    MachineCenterAboveSteelMm = machineOffset,
                    FixedLevelingDepthMm = fixedStack,
                    SteelDepthMm = p.Beam.NominalDepthMm,
                    StackedSteelLevels = 1,
                    HangerTopBelowSteelTopMm = r.HangerTopBelowSteelTop,
                    HangerBottomAboveDeflectorCenterMm = r.HangerBottomAboveDeflectorCenter,
                    MinimumDeflectorSlabClearanceMm = input.MinimumDeflectorSlabClearanceMm,
                    AdjustableDeflectorHangerConfirmed = true,
                    ReferenceConcreteHeightMm = r.SourceConcreteHeight
                });
                if (p.Arrangement.Status != "PASS")
                    throw new InvalidOperationException(p.Arrangement.Status);
                p.ConcreteHeight = p.Arrangement.ConcreteBeamHeightMm.Value;
                p.TractionZ = p.Arrangement.RequiredTractionSheaveCenterZ.Value;
                p.DeflectorZ = p.Arrangement.RequiredDeflectorCenterZ.Value;
                p.HasCalculatedPath = true;
                p.LayoutStatus = "PASS";
                p.ReevingGeometryStatus = "PASS";
            }
            else
            {
                p.ConcreteHeight = 350;
                p.TractionZ = 350 + fixedStack + p.Beam.NominalDepthMm + machineOffset;
                p.DeflectorZ = r.SourceDeflectorZ;
                p.LayoutStatus = "ENGINEERING REVIEW REQUIRED";
                p.ReevingGeometryStatus = m.Deflector.IsRequired
                    ? "REEVING/TRACTION REVIEW - TZ OUT OF RANGE"
                    : "DIRECT ROPE ROUTING REVIEW - TZ IS SMALLER THAN THE 520 MM TRACTION DIAMETER";
                p.Arrangement = new VerticalArrangementResult { Status = p.ReevingGeometryStatus, ConcreteBeamHeightMm = 350 };
            }
            p.SteelBottomZ = p.ConcreteHeight + fixedStack;
            p.SteelTopZ = p.SteelBottomZ + p.Beam.NominalDepthMm;
            p.TractionX = p.CarDropX - 260;
            p.DeflectorX = p.CwDropX + p.DeflectorPitchMm / 2;
            if (p.HasCalculatedPath)
            {
                var s = p.Arrangement.Reeving;
                var v = m.Reeving;
                v.TractionSheavePitchDiameterMm = 520;
                v.DeflectorSheavePitchDiameterMm = p.DeflectorPitchMm;
                v.TractionSheaveCenter = s.TractionSheaveCenter;
                v.DeflectorSheaveCenter = s.DeflectorSheaveCenter;
                v.HorizontalCenterOffsetMm = s.HorizontalCenterOffsetMm;
                v.VerticalCenterOffsetMm = s.VerticalCenterOffsetMm;
                v.RopeEntryDirection = s.RopeEntryDirection;
                v.RopeExitDirection = s.RopeExitDirection;
                v.RopeEntryAngleDeg = s.RopeEntryAngleDeg;
                v.RopeExitAngleDeg = s.RopeExitAngleDeg;
                v.CalculatedWrapAngleDeg = s.CalculatedWrapAngleDeg;
                v.WrapAngleStatus = s.WrapAngleStatus;
                v.RequiredChassisElevationAdjustmentMm = p.Arrangement.RequiredChassisElevationAdjustmentMm;
                v.TractionVerificationResult =
                    "NOT YET EVALUABLE - STANDARD DEPENDENCY: EN 81-50 REQUIRED; loads, friction/grooves, rope construction and safety "
                    + "factors are not verified by wrap geometry.";
            }
            m.Rules = MachineRoomRuleRegistry.Evaluate(input, shared, m);
            m.Rules.Add(new EngineeringRule
            {
                RuleId = "SECTION-HANGER-001",
                RequirementType = RequirementType.Project,
                Description = "Adjustable hanger sets center separation; concrete height supplies slab space",
                SourceDocument = "Engineer clarification",
                RequiredValue = "152 degree wrap; concrete 350..1000 mm",
                Status = p.LayoutStatus,
                EngineeringNote =
                    "No fixed deflector elevation. Missing slab-clearance minimum stays NOT YET EVALUABLE. Hanger mechanical design "
                    + "requires review."
            });
            m.Rules.Add(new EngineeringRule
            {
                RuleId = "SECTION-BEAM-001",
                RequirementType = RequirementType.Project,
                Description = "Preliminary machine beam selection",
                SourceDocument = "Engineer project table",
                InputParameters = new Dictionary<string, object> { { "CapacityKg", input.Capacity.Value }, { "SpanMm", p.Beam.SpanMm } },
                CalculatedValue = p.Beam.SectionDesignation,
                Status = p.Beam.StructuralStatus,
                EngineeringNote = "No bending, shear, deflection, reinforcement or bearing capacity calculation."
            });
            m.Rules.Add(new EngineeringRule
            {
                RuleId = "SECTION-SLAB-001",
                RequirementType = RequirementType.Project,
                Description = "Deflector pitch-circle to slab geometric clearance",
                SourceDocument = "Engineer clarification / optional project minimum",
                InputParameters =
                    new Dictionary<string, object> { { "MinimumDeflectorSlabClearanceMm", input.MinimumDeflectorSlabClearanceMm } },
                CalculatedValue = p.Arrangement.DeflectorToSlabClearanceMm.HasValue
                    ? p.Arrangement.DeflectorToSlabClearanceMm.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : null,
                Status = p.Arrangement.DeflectorSlabClearanceStatus,
                EngineeringNote =
                    "No minimum is invented. Pitch-circle separation is not a manufacturer outer-envelope clearance check; hanger "
                    + "mechanical design remains under review."
            });
            return p;
        }
    }
}
