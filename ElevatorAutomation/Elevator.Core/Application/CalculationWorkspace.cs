using System.Linq;
using Elevator.Core.Calculations;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;

namespace Elevator.Core.Application
{
    // In-memory results only. Geometry is issued by the shared engine, never read from JSON.
    public sealed class CalculationWorkspace
    {
        public ChassisCalculationGeometry Geometry { get; private set; }
        public ChassisCalculationResult Result { get; private set; }
        public ElevatorDesignResult SourceDesign { get; private set; }
        public int GeometryGenerationCount { get; private set; }
        public string DisplayStatus { get; private set; }

        public CalculationWorkspace()
        {
            DisplayStatus = "Calculate to show P1–P4 for the current inputs.";
        }

        public void Invalidate()
        {
            Result = null;
            DisplayStatus = "STALE — inputs changed; recalculate.";
        }

        public bool IsCurrent(ElevatorProject project, DesignContext context)
        {
            return CalculationModes.Resolve(project.Calculations) == "Basic"
                ? SimplifiedCalculationEngine.IsCurrent(Result, project, Geometry, context)
                : ChassisCalculationEngine.IsCurrent(Result, project, Geometry, context) && Result.Mode == "Advanced";
        }

        public bool EnsureGeometry(ElevatorProject project, DesignContext context)
        {
            string mode = CalculationModes.Resolve(project.Calculations);
            if (Geometry != null && Geometry.Recipe == mode && ChassisGeometryProvenance.Validate(project, Geometry, context).IsValid)
                return true;
            Invalidate();
            Geometry = null;
            SourceDesign = mode == "Basic"
                ? new ElevatorDesignEngine().CalculateBasicChassisGeometry(project, context)
                : new ElevatorDesignEngine().Calculate(project, context, DrawingViews.MachineRoomPlan | DrawingViews.SectionBB);
            GeometryGenerationCount++;
            if (SourceDesign.CanGenerate && ChassisGeometryProvenance.Validate(project, SourceDesign.ChassisGeometry, context).IsValid)
            {
                Geometry = SourceDesign.ChassisGeometry;
                return true;
            }
            return false;
        }

        public ChassisCalculationResult Calculate(ElevatorProject project, DesignContext context)
        {
            if (!EnsureGeometry(project, context))
            {
                Result = new ChassisCalculationResult
                {
                    Mode = CalculationModes.Resolve(project.Calculations),
                    Status = "INVALID_OR_UNAVAILABLE_GEOMETRY"
                };
                var inputCheck = ChassisGeometryProvenance.Validate(project, null, context);
                if (inputCheck.Status == "INVALID_INPUT")
                {
                    Result.Status = inputCheck.Status;
                    Result.Issues.AddRange(inputCheck.Issues);
                }
                if (SourceDesign != null)
                {
                    Result.Issues.AddRange(
                        SourceDesign.Validation.Where(v => v.BlocksGeneration).Select(v => new CalculationIssue(v.Field, v.Message)));
                    if (SourceDesign.ChassisGeometry != null)
                        Result.Issues.AddRange(SourceDesign.ChassisGeometry.Issues);
                }
                if (Result.Issues.Count == 0)
                    Result.Issues.Add(new CalculationIssue(
                        "Geometry",
                        "Shared engine could not produce source-bound chassis geometry for the current context."));
            }
            else if (CalculationModes.Resolve(project.Calculations) == "Basic")
                Result = new SimplifiedCalculationEngine().Calculate(project, Geometry, context);
            else if (CalculationModes.Resolve(project.Calculations) == "Advanced")
                Result = new ChassisCalculationEngine().Calculate(project, Geometry, context);
            else
            {
                Result = new ChassisCalculationResult { Status = "INVALID_INPUT" };
                Result.Issues.Add(new CalculationIssue("Calculations.Mode", "Unsupported calculation mode."));
            }
            DisplayStatus = Result.Status;
            return Result;
        }
    }
}
