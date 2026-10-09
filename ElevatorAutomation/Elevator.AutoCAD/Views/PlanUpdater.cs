using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.Core;

namespace Elevator.AutoCAD
{
    public sealed class ViewUpdateResult
    {
        public Dictionary<string,double> Dimensions, Clearances;
        public Dictionary<string,string> Annotations;
        public PlanCoverageReport Coverage;
    }
    public interface IViewUpdater
    {
        string ViewId {get;}
        ViewUpdateResult Update(Database db, Transaction tr, ElevatorParameters design);
    }
    public sealed class PlanUpdater : IViewUpdater
    {
        public string ViewId {get {return "Plan";} }
        public ViewUpdateResult Update(Database db, Transaction tr, ElevatorParameters design)
        {
            var snapshot = DrawingReader.Read(db, tr);
            var calculator = new PlanGeometryAdapter(snapshot, design);
            var gaps = DrawingUpdater.Preflight(snapshot, calculator);
            var input=ProjectDataStorage.ReadProject(db,tr);
            var annotations=PlanAnnotations.Prepare(db,tr,snapshot,input,design,gaps);
            var hatches=PlanHatches.Prepare(snapshot,calculator);
            var expected = DrawingUpdater.Apply(db, tr, snapshot, calculator);
            PlanAnnotations.Apply(snapshot,annotations);
            PlanHatches.EvaluateAndValidate(snapshot,hatches);
            var updated=DrawingReader.Read(db,tr);
            DrawingUpdater.Validate(updated, design, expected);
            PlanAnnotations.Validate(updated,annotations);
            return new ViewUpdateResult {Dimensions=expected, Clearances=gaps,Annotations=annotations,Coverage=PlanCoverage.Read(db,tr,updated)};
        }
    }
}
