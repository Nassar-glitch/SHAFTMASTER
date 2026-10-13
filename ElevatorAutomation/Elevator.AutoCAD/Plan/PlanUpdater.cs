using Autodesk.AutoCAD.DatabaseServices;
using Elevator.AutoCAD.Common;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Plan
{
    /// <summary>Updates the plan view from the shared design.</summary>
    public sealed class PlanUpdater : IViewUpdater
    {
        public string ViewId
        {
            get
            {
                return "Plan";
            }
        }

        /// <summary>Moves and reshapes the plan entities to the new design.</summary>
        public ViewUpdateResult Update(Database db, Transaction tr, ElevatorParameters design)
        {
            // An earlier automatic door is removed first (reads only when the drawing has none).
            AutomaticDoorPlan.Clear(db, tr);
            Steel.SteelDrawing.ClearPlan(db, tr);
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
            // The automatic door (2S, 2C) of the project stored in the drawing; a Manual door leaves the template geometry as it is.
            var door = AutomaticDoorPlan.LayoutFor(db, tr, design);
            var steel = Steel.SteelDrawing.Read(db, tr);
            if (door != null)
                AutomaticDoorPlan.Draw(db, tr, updated, door, steel != null);
            // A steel shaft: the tower is drawn in place of the walls (hidden, not edited).
            if (steel != null)
                Steel.SteelDrawing.DrawPlan(db, tr, updated, steel);
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
