using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Suspension;

namespace Elevator.Core.Project
{
    public static class ProjectValidator
    {
        public static List<ValidationIssue> Validate(ElevatorProject p, DrawingViews views, bool chassisGeometryOnly = false)
        {
            var issues = new List<ValidationIssue>();
            Action<string, string, bool> fail = (field, message, block) => issues.Add(new ValidationIssue
            {
                Code = "PROJECT_INPUT",
                Field = field,
                Message = message,
                Status = block ? ValidationStatus.FAIL : ValidationStatus.NOT_YET_EVALUABLE,
                BlocksGeneration = block
            });
            if (p == null
                || p.Info == null
                || p.Elevator == null
                || p.Building == null
                || p.Building.Floors == null
                || p.Building.Floors.Any(f => f == null)
                || p.Shaft == null
                || p.Car == null
                || p.Counterweight == null
                || p.Doors == null
                || p.Machine == null
                || p.Suspension == null
                || p.Rails == null
                || p.MachineRoom == null
                || p.Drawing == null
                || p.Standards == null
                || p.Extensions == null)
            {
                fail("project", "Required project group is missing.", true);
                return issues;
            }
            if (!ProjectFloors.FinitePositive(p.Shaft.WidthMm) || !ProjectFloors.FinitePositive(p.Shaft.DepthMm))
                fail("Shaft", "SW and SD must be finite positive millimetres.", true);
            if (p.Elevator.SystemId != "traction-geared"
                || p.Elevator.LegacyFamily != ElevatorType.ExistingPlan
                || p.Elevator.TemplateId != "existing-plan-v1")
                fail("Elevator.SystemId", "System/template is not registered as supported in V1.", true);
            if (p.Standards.RuleSetId != "accepted-project-rules-v1")
                fail("Standards.RuleSetId", "Unregistered rule set.", true);
            if (!chassisGeometryOnly && (!ProjectFloors.FinitePositive(p.Drawing.ViewMarginMm) || p.Drawing.Arrangement != "TwoByTwo"))
                fail("Drawing", "Positive margin and supported TwoByTwo arrangement required.", true);
            bool section = (views & (DrawingViews.SectionAA | DrawingViews.SectionBB)) != 0;
            if (section && !p.Elevator.CapacityKg.HasValue)
                fail("Elevator.CapacityKg", "NOT PROVIDED; B-B beam selection requires capacity.", true);
            if (p.Elevator.CapacityKg.HasValue && !ProjectFloors.FinitePositive(p.Elevator.CapacityKg.Value))
                fail("Elevator.CapacityKg", "Capacity must be finite and positive.", true);
            if (!chassisGeometryOnly
                && p.Elevator.SpeedMetresPerSecond.HasValue
                && !ProjectFloors.FinitePositive(p.Elevator.SpeedMetresPerSecond.Value))
                fail("Elevator.SpeedMetresPerSecond", "Speed must be finite and positive.", true);
            if (!p.Elevator.SpeedMetresPerSecond.HasValue)
                fail("Elevator.SpeedMetresPerSecond", "NOT PROVIDED; no speed-dependent verification.", false);
            if (p.Suspension.RopingRatio.HasValue && p.Suspension.RopingRatio.Value != 1)
                fail("Suspension.RopingRatio", "Only the accepted 1:1 configuration is supported.", true);
            if (!chassisGeometryOnly)
                try
                {
                    SuspensionProjectRules.Validate(p.Suspension.RopeDiameterMm, p.Suspension.RopeCount);
                }
                catch (ArgumentException e) { fail("Suspension", e.Message, true); }
            if (!p.Suspension.RopeDiameterMm.HasValue)
                fail("Suspension.RopeDiameterMm", "NOT PROVIDED; component validation incomplete.", false);
            if (!p.Suspension.RopeCount.HasValue)
                fail("Suspension.RopeCount", "NOT PROVIDED; do not infer a rope count.", false);
            if (!p.MachineRoom.MinimumDeflectorSlabClearanceMm.HasValue)
                fail("MachineRoom.MinimumDeflectorSlabClearanceMm", "NOT YET EVALUABLE; minimum clearance not supplied.", false);
            else if (Double.IsNaN(p.MachineRoom.MinimumDeflectorSlabClearanceMm.Value)
                || Double.IsInfinity(p.MachineRoom.MinimumDeflectorSlabClearanceMm.Value)
                || p.MachineRoom.MinimumDeflectorSlabClearanceMm.Value < 0)
                fail("MachineRoom.MinimumDeflectorSlabClearanceMm", "Minimum clearance must be finite and nonnegative.", true);
            if ((views & DrawingViews.SectionAA) != 0)
            {
                try
                {
                    var floors = ProjectFloors.Elevations(p.Building);
                    if (floors.Count < 2 || floors.Count > 100 || floors.Count(f => f.IsServed) < 2)
                        fail("Building.Floors", "Provide 2..100 normal floors and at least two served floors.", true);
                    if (!floors.Any(f => f.FloorId == p.Car.DisplayFloorId && f.IsServed))
                        fail("Car.DisplayFloorId", "Select a served FloorId.", true);
                    LegacyProjectAdapter.ToSectionAA(p);
                }
                catch (ArgumentException e) { fail("Building", e.Message, true); }
                if (p.Building.Floors.Any(
                    f => f.LandingDoor != null && (f.LandingDoor.Type != null || f.LandingDoor.EntranceConfiguration != null)))
                    issues.Add(new ValidationIssue
                    {
                        Code = "LANDING_METADATA",
                        Field = "Building.Floors.LandingDoor",
                        Status = ValidationStatus.REVIEW_REQUIRED,
                        Message = "Per-floor door configuration is stored metadata; native V1 uses the approved single door module."
                    });
            }
            return issues;
        }
    }
}
