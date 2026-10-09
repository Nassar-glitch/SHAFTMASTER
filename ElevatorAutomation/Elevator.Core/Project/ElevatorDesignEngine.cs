using System;
using System.Collections.Generic;
using Elevator.Core.Calculations;
using Elevator.Core.Geometry;
using Elevator.Core.GuideRails;
using Elevator.Core.MachineRoom;

namespace Elevator.Core.Project
{
    /// <summary>The shared design: validates a project and calculates the plan, machine-room, sections and chassis geometry.</summary>
    public sealed class ElevatorDesignEngine
    {
        /// <summary>Maps a rule status text to a validation status.</summary>
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

        /// <summary>Runs the shared design for the requested views.</summary>
        public ElevatorDesignResult Calculate(ElevatorProject project, DesignContext context, DrawingViews views = DrawingViews.All)
        {
            return CalculateInternal(project, context, views, false);
        }

        /// <summary>
        /// Same geometry recipes and authoritative source inputs. Non-geometric rope metadata/floor summaries are not prerequisites for
        /// Basic vertical statics.
        /// </summary>
        public ElevatorDesignResult CalculateBasicChassisGeometry(ElevatorProject project, DesignContext context)
        {
            return CalculateInternal(project, context, DrawingViews.MachineRoomPlan | DrawingViews.SectionBB, true);
        }

        /// <summary>
        /// FF loads need only authoritative internal plan dimensions. Unrelated masses, safety/pit choices, floors or reeving metadata are
        /// not sizing inputs. Use the SAME plan recipe and bind its sources before it runs.
        /// </summary>
        public CarGuideForceGeometry CalculateCarGuideForceGeometry(
            ElevatorProject project,
            DesignContext context)
        {
            return new CarGuideForceGeometry(
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
            GeometrySourceSnapshot chassisSource = null;
            try
            {
                if (context == null || context.Equipment == null)
                    throw new ArgumentException("Reviewed equipment/template context is required.");
                chassisSource = new GeometrySourceSnapshot(project, context);
                var input = LegacyProjectAdapter.ToLegacy(project, !basicGeometry);
                if (basicGeometry)
                {
                    input.SuspensionRopeDiameterMm = null;
                    input.NumberOfSuspensionRopes = null;
                }
                result.Plan = DesignRules.Calculate(input.SW, input.SD, context.Equipment, context.CounterweightDepthMm);
                result.LayoutStatus = ValidationStatus.PASS;
                CalculateMachineRoom(context, views, result, input);
                result.Floors = basicGeometry ? new List<FloorElevation>() : ProjectFloors.Elevations(project.Building);
                CalculateSectionAA(project, context, views, result);
            }
            catch (ArgumentException e)
            {
                Failure(result, e.Message);
            }
            catch (InvalidOperationException e)
            {
                Failure(result, e.Message);
            }
            // Calculation availability never changes the established drawing validation/geometry.
            result.ChassisGeometry = ChassisGeometry.Calculate(
                context == null ? null : context.ChassisEvidence,
                result.Plan,
                basicGeometry);
            if (result.CanGenerate && chassisSource != null && result.ChassisGeometry.Issues.Count == 0)
                result.ChassisGeometry.BindSource(chassisSource);
            result.ChassisCalculation = CalculationModes.Resolve(project.Calculations) == "Basic"
                ? new SimplifiedCalculationEngine().Calculate(project, result.ChassisGeometry, context)
                : new ChassisCalculationEngine().Calculate(project, result.ChassisGeometry, context);
            return result;
        }

        private void CalculateMachineRoom(DesignContext context, DrawingViews views, ElevatorDesignResult result, ProjectInput input)
        {
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
                {
                    result.Validation.Add(new ValidationIssue
                    {
                        Code = rule.RuleId,
                        Field = "Design",
                        Status = Status(rule.Status),
                        Message = rule.Status + ": " + rule.EngineeringNote,
                        BlocksGeneration = false
                    });
                }
            }
        }

        private void CalculateSectionAA(ElevatorProject project, DesignContext context, DrawingViews views, ElevatorDesignResult result)
        {
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
