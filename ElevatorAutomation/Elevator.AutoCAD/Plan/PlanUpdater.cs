using Autodesk.AutoCAD.DatabaseServices;
using Elevator.AutoCAD.Common;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Plan
{
    public sealed class PlanUpdater : IViewUpdater
    {
        public string ViewId
        {
            get
            {
                return "Plan";
            }
        }

        public ViewUpdateResult Update(Database db, Transaction tr, ElevatorParameters design)
        {
            var snapshot = DrawingReader.Read(db, tr);
            var calculator = new PlanGeometryAdapter(snapshot, design);
            var gaps = DrawingUpdater.Preflight(snapshot, calculator);
            var input = ProjectDataStorage.ReadProject(db, tr);
            var annotations = PlanAnnotations.Prepare(db, tr, snapshot, input, design, gaps);
            var hatches = PlanHatches.Prepare(snapshot, calculator);
            var expected = DrawingUpdater.Apply(db, tr, snapshot, calculator);
            PlanAnnotations.Apply(snapshot, annotations);
            PlanHatches.EvaluateAndValidate(snapshot, hatches);
            var updated = DrawingReader.Read(db, tr);
            DrawingUpdater.Validate(updated, design, expected);
            PlanAnnotations.Validate(updated, annotations);
            return new ViewUpdateResult
            {
                Dimensions = expected,
                Clearances = gaps,
                Annotations = annotations,
                Coverage = PlanCoverage.Read(db, tr, updated)
            };
        }
    }
}
