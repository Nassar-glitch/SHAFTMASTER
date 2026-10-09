using System;
using System.Linq;
using System.Collections.Generic;
using Elevator.Core.SectionAA;

namespace Elevator.Core.Project
{
    [Flags]
    public enum DrawingViews
    {
        Plan = 1,
        MachineRoomPlan = 2,
        SectionAA = 4,
        SectionBB = 8,
        All = 15
    }

    public sealed class GenerationOptions
    {
        public DrawingViews Views = DrawingViews.All;
        public bool ArrangeViews;
        public string OutputPath;
    }

    public sealed class DrawingGenerationResult
    {
        public ElevatorDesignResult Design;
        public Dictionary<string, int> EntityCounts = new Dictionary<string, int>();
        public string OutputPath;
    }

    public interface IProjectDrawingGenerator
    {
        DrawingGenerationResult GenerateProjectDrawings(ElevatorProject project, GenerationOptions options);
    }

    public sealed class DesignContext
    {
        public EquipmentProfile Equipment;
        public double CounterweightDepthMm;
        public MachineRoomExtensions RoomExtensions;
        public SectionReference SectionReference;
        public double FrontSupportDepthMm;
        public double RearSupportOffsetMm;
        public double RetainedDeflectorPitchMm;
        public bool RetainedDeflectorVisible;
        public Elevator.Core.Calculations.ChassisGeometryEvidence ChassisEvidence;
    }

    public sealed class ElevatorDesignResult
    {
        public ElevatorParameters Plan;
        public SectionAAGeometry SectionAA;
        public List<FloorElevation> Floors;
        public List<ValidationIssue> Validation = new List<ValidationIssue>();
        public Elevator.Core.Calculations.ChassisCalculationGeometry ChassisGeometry;
        public Elevator.Core.Calculations.ChassisCalculationResult ChassisCalculation;
        public ValidationStatus LayoutStatus = ValidationStatus.NOT_YET_EVALUABLE;
        public ValidationStatus WrapAngleStatus = ValidationStatus.NOT_YET_EVALUABLE;
        public ValidationStatus CodeDiameterRatioStatus = ValidationStatus.NOT_YET_EVALUABLE;

        public bool CanGenerate
        {
            get
            {
                return !Validation.Any(v => v.BlocksGeneration);
            }
        }

        // Accepted V1 component dimensions are results of the selected native template, not editable inputs.
        public double CarInternalHeightMm = 2200;
        public double CarOverallHeightMm = 2400;
        public double CounterweightHeightMm = 3000;
        public double DoorClearHeightMm = 2000;
        public double DoorConstructionHeightMm = 2160;
        public double LandingSlabThicknessMm = 250;
        public double MachineRoomClearHeightMm = 2200;
        public string CarRailProfile = "T70/65/9";
        public string CounterweightRailProfile = "T50/50/5";
    }

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

    public sealed class ElevatorDesignEngine
    {
        public static ValidationStatus Status(string text)
        {
            text = (text ?? "").ToUpperInvariant();
            if (text.StartsWith("FAIL"))
                return ValidationStatus.FAIL;
            if (text.Contains("WARNING"))
                return ValidationStatus.WARNING;
            if (text.Contains("REVIEW") || text.Contains("PRELIMINARY") || text.Contains("CALCULATION REQUIRED"))
                return ValidationStatus.REVIEW_REQUIRED;
            if (text.StartsWith("PASS") || text.StartsWith("SELECTED") || text == "NOT REQUIRED")
                return ValidationStatus.PASS;
            return ValidationStatus.NOT_YET_EVALUABLE;
        }

        public ElevatorDesignResult Calculate(ElevatorProject project, DesignContext context, DrawingViews views = DrawingViews.All)
        {
            return CalculateInternal(project, context, views, false);
        }

        // Same geometry recipes and authoritative source inputs. Non-geometric rope
        // metadata/floor summaries are not prerequisites for Basic vertical statics.
        public ElevatorDesignResult CalculateBasicChassisGeometry(ElevatorProject project, DesignContext context)
        {
            return CalculateInternal(project, context, DrawingViews.MachineRoomPlan | DrawingViews.SectionBB, true);
        }

        // FF loads need only authoritative internal plan dimensions. Unrelated
        // masses, safety/pit choices, floors or reeving metadata are not sizing
        // inputs. Use the SAME plan recipe and bind its sources before it runs.
        public Elevator.Core.Calculations.CarGuideForceGeometry CalculateCarGuideForceGeometry(
            ElevatorProject project,
            DesignContext context)
        {
            return new Elevator.Core.Calculations.CarGuideForceGeometry(
                project,
                context,
                () => DesignRules.Calculate(project.Shaft.WidthMm, project.Shaft.DepthMm, context.Equipment, context.CounterweightDepthMm));
        }

        ElevatorDesignResult CalculateInternal(ElevatorProject project, DesignContext context, DrawingViews views, bool basicGeometry)
        {
            var result = new ElevatorDesignResult();
            result.Validation = ProjectValidator.Validate(project, views, basicGeometry);
            if (!result.CanGenerate)
                return result;
            Elevator.Core.Calculations.GeometrySourceSnapshot chassisSource = null;
            try
            {
                if (context == null || context.Equipment == null)
                    throw new ArgumentException("Reviewed equipment/template context is required.");
                chassisSource = new Elevator.Core.Calculations.GeometrySourceSnapshot(project, context);
                var input = LegacyProjectAdapter.ToLegacy(project, !basicGeometry);
                if (basicGeometry)
                {
                    input.SuspensionRopeDiameterMm = null;
                    input.NumberOfSuspensionRopes = null;
                }
                result.Plan = DesignRules.Calculate(input.SW, input.SD, context.Equipment, context.CounterweightDepthMm);
                result.LayoutStatus = ValidationStatus.PASS;
                if ((views & ~DrawingViews.Plan) != 0)
                {
                    result.Plan.MachineRoom = MachineRoomRules.Calculate(input, result.Plan, context.RoomExtensions);
                    result.CodeDiameterRatioStatus = Status(result.Plan.MachineRoom.CodeDiameterRatioStatus);
                    if ((views & (DrawingViews.SectionBB | DrawingViews.SectionAA)) != 0)
                    {
                        if (context.SectionReference == null)
                            throw new ArgumentException("Reviewed B-B template reference is required.");
                        result.Plan.MachineRoomSection = MachineRoomSectionRules.Calculate(
                            input,
                            result.Plan,
                            context.SectionReference,
                            context.FrontSupportDepthMm,
                            context.RearSupportOffsetMm,
                            context.RetainedDeflectorPitchMm,
                            context.RetainedDeflectorVisible);
                        result.LayoutStatus = Status(result.Plan.MachineRoomSection.LayoutStatus);
                    }
                    result.WrapAngleStatus = Status(result.Plan.MachineRoom.Reeving.WrapAngleStatus);
                    foreach (var rule in result.Plan.MachineRoom.Rules)
                        result.Validation.Add(new ValidationIssue
                        {
                            Code = rule.RuleId,
                            Field = "Design",
                            Status = Status(rule.Status),
                            Message = rule.Status + ": " + rule.EngineeringNote,
                            BlocksGeneration = false
                        });
                }
                result.Floors = basicGeometry ? new List<FloorElevation>() : ProjectFloors.Elevations(project.Building);
                if ((views & DrawingViews.SectionAA) != 0)
                {
                    result.SectionAA = SectionAACalculator.Calculate(
                        LegacyProjectAdapter.ToSectionAA(project),
                        context.SectionReference.SourceSlabThickness);
                    if (result.SectionAA.LayoutStatus.StartsWith("WARNING"))
                    {
                        result.LayoutStatus = ValidationStatus.WARNING;
                        result.Validation.Add(new ValidationIssue
                        {
                            Code = "CW_POSE",
                            Field = "Counterweight",
                            Status = ValidationStatus.WARNING,
                            Message = result.SectionAA.LayoutStatus
                        });
                    }
                }
            }
            catch (ArgumentException e) { Failure(result, e.Message); }
            catch (InvalidOperationException e) { Failure(result, e.Message); }
            // Calculation availability never changes the established drawing validation/geometry.
            result.ChassisGeometry = Elevator.Core.Calculations.ChassisGeometry.Calculate(
                context == null ? null : context.ChassisEvidence,
                result.Plan,
                basicGeometry);
            if (result.CanGenerate && chassisSource != null && result.ChassisGeometry.Issues.Count == 0)
                result.ChassisGeometry.BindSource(chassisSource);
            result.ChassisCalculation = Elevator.Core.Calculations.CalculationModes.Resolve(project.Calculations) == "Basic"
                ? new Elevator.Core.Calculations.SimplifiedCalculationEngine().Calculate(project, result.ChassisGeometry, context)
                : new Elevator.Core.Calculations.ChassisCalculationEngine().Calculate(project, result.ChassisGeometry, context);
            return result;
        }

        static void Failure(ElevatorDesignResult r, string message)
        {
            r.LayoutStatus = ValidationStatus.FAIL;
            r.Validation.Add(new ValidationIssue
            {
                Code = "GEOMETRY_INPUT",
                Field = "Design",
                Status = ValidationStatus.FAIL,
                BlocksGeneration = true,
                Message = message
            });
        }
    }
}
