using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Suspension;

namespace Elevator.Core.Project
{
    /// <summary>Validates a project for the requested drawing views.</summary>
    public static class ProjectValidator
    {
        /// <summary>
        /// Validates a project for the requested views; chassisGeometryOnly limits the checks to what the chassis geometry needs.
        /// </summary>
        public static List<ValidationIssue> Validate(ElevatorProject p, DrawingViews views, bool chassisGeometryOnly = false)
        {
            var issues = new IssueList();
            if (!HasRequiredGroups(p))
            {
                issues.Fail("project", "Required project group is missing.", true);
                return issues.Items;
            }
            CheckBasics(p, views, chassisGeometryOnly, issues);
            CheckSuspension(p, chassisGeometryOnly, issues);
            CheckMachineRoom(p, issues);
            if ((views & DrawingViews.SectionAA) != 0)
                CheckSectionAA(p, issues);
            return issues.Items;
        }

        static bool HasRequiredGroups(ElevatorProject p)
        {
            return p != null
                && p.Info != null
                && p.Elevator != null
                && p.Building != null
                && p.Building.Floors != null
                && !p.Building.Floors.Any(f => f == null)
                && p.Shaft != null
                && p.Car != null
                && p.Counterweight != null
                && p.Doors != null
                && p.Machine != null
                && p.Suspension != null
                && p.Rails != null
                && p.MachineRoom != null
                && p.Drawing != null
                && p.Standards != null
                && p.Extensions != null;
        }

        static void CheckBasics(ElevatorProject p, DrawingViews views, bool chassisGeometryOnly, IssueList issues)
        {
            if (!ProjectFloors.FinitePositive(p.Shaft.WidthMm) || !ProjectFloors.FinitePositive(p.Shaft.DepthMm))
                issues.Fail("Shaft", "SW and SD must be finite positive millimetres.", true);
            if (p.Elevator.SystemId != "traction-geared"
                || p.Elevator.LegacyFamily != ElevatorType.ExistingPlan
                || p.Elevator.TemplateId != "existing-plan-v1")
                issues.Fail("Elevator.SystemId", "System/template is not registered as supported in V1.", true);
            if (p.Standards.RuleSetId != "accepted-project-rules-v1")
                issues.Fail("Standards.RuleSetId", "Unregistered rule set.", true);
            if (!chassisGeometryOnly && (!ProjectFloors.FinitePositive(p.Drawing.ViewMarginMm) || p.Drawing.Arrangement != "TwoByTwo"))
                issues.Fail("Drawing", "Positive margin and supported TwoByTwo arrangement required.", true);
            bool section = (views & (DrawingViews.SectionAA | DrawingViews.SectionBB)) != 0;
            if (section && !p.Elevator.CapacityKg.HasValue)
                issues.Fail("Elevator.CapacityKg", "NOT PROVIDED; B-B beam selection requires capacity.", true);
            if (p.Elevator.CapacityKg.HasValue && !ProjectFloors.FinitePositive(p.Elevator.CapacityKg.Value))
                issues.Fail("Elevator.CapacityKg", "Capacity must be finite and positive.", true);
            if (!chassisGeometryOnly
                && p.Elevator.SpeedMetresPerSecond.HasValue
                && !ProjectFloors.FinitePositive(p.Elevator.SpeedMetresPerSecond.Value))
                issues.Fail("Elevator.SpeedMetresPerSecond", "Speed must be finite and positive.", true);
            if (!p.Elevator.SpeedMetresPerSecond.HasValue)
                issues.Fail("Elevator.SpeedMetresPerSecond", "NOT PROVIDED; no speed-dependent verification.", false);
        }

        static void CheckSuspension(ElevatorProject p, bool chassisGeometryOnly, IssueList issues)
        {
            if (p.Suspension.RopingRatio.HasValue && p.Suspension.RopingRatio.Value != 1)
                issues.Fail("Suspension.RopingRatio", "Only the accepted 1:1 configuration is supported.", true);
            if (!chassisGeometryOnly)
            {
                try
                {
                    SuspensionProjectRules.Validate(p.Suspension.RopeDiameterMm, p.Suspension.RopeCount);
                }
                catch (ArgumentException e)
                {
                    issues.Fail("Suspension", e.Message, true);
                }
            }
            if (!p.Suspension.RopeDiameterMm.HasValue)
                issues.Fail("Suspension.RopeDiameterMm", "NOT PROVIDED; component validation incomplete.", false);
            if (!p.Suspension.RopeCount.HasValue)
                issues.Fail("Suspension.RopeCount", "NOT PROVIDED; do not infer a rope count.", false);
        }

        static void CheckMachineRoom(ElevatorProject p, IssueList issues)
        {
            if (!p.MachineRoom.MinimumDeflectorSlabClearanceMm.HasValue)
                issues.Fail("MachineRoom.MinimumDeflectorSlabClearanceMm", "NOT YET EVALUABLE; minimum clearance not supplied.", false);
            else if (Double.IsNaN(p.MachineRoom.MinimumDeflectorSlabClearanceMm.Value)
                || Double.IsInfinity(p.MachineRoom.MinimumDeflectorSlabClearanceMm.Value)
                || p.MachineRoom.MinimumDeflectorSlabClearanceMm.Value < 0)
                issues.Fail("MachineRoom.MinimumDeflectorSlabClearanceMm", "Minimum clearance must be finite and nonnegative.", true);
        }

        static void CheckSectionAA(ElevatorProject p, IssueList issues)
        {
            try
            {
                var floors = ProjectFloors.Elevations(p.Building);
                if (floors.Count < 2 || floors.Count > 100 || floors.Count(f => f.IsServed) < 2)
                    issues.Fail("Building.Floors", "Provide 2..100 normal floors and at least two served floors.", true);
                if (!floors.Any(f => f.FloorId == p.Car.DisplayFloorId && f.IsServed))
                    issues.Fail("Car.DisplayFloorId", "Select a served FloorId.", true);
                LegacyProjectAdapter.ToSectionAA(p);
            }
            catch (ArgumentException e)
            {
                issues.Fail("Building", e.Message, true);
            }
            if (p.Building.Floors.Any(
                f => f.LandingDoor != null && (f.LandingDoor.Type != null || f.LandingDoor.EntranceConfiguration != null)))
            {
                issues.Items.Add(new ValidationIssue
                {
                    Code = "LANDING_METADATA",
                    Field = "Building.Floors.LandingDoor",
                    Status = ValidationStatus.REVIEW_REQUIRED,
                    Message = "Per-floor door configuration is stored metadata; native V1 uses the approved single door module."
                });
            }
        }

        // The issues found so far; Fail adds a blocking issue or a "not yet evaluable" note.
        sealed class IssueList
        {
            public readonly List<ValidationIssue> Items = new List<ValidationIssue>();

            public void Fail(string field, string message, bool block)
            {
                Items.Add(new ValidationIssue
                {
                    Code = "PROJECT_INPUT",
                    Field = field,
                    Message = message,
                    Status = block ? ValidationStatus.FAIL : ValidationStatus.NOT_YET_EVALUABLE,
                    BlocksGeneration = block
                });
            }
        }
    }
}
