using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.AutoCAD.Common;

namespace Elevator.AutoCAD.Plan
{
    public static class PlanHatches
    {
        public static Dictionary<string, HatchExpectation> Prepare(DrawingSnapshot snapshot, PlanGeometryAdapter geometry)
        {
            var expected = new Dictionary<string, HatchExpectation>();
            foreach (var pair in snapshot.Entities)
            {
                var hatch = pair.Value as Hatch;
                if (hatch == null)
                    continue;
                using (var copy = (Hatch)hatch.Clone())
                {
                    DrawingUpdater.ApplyShape(copy, snapshot.Bindings[pair.Key].Role, geometry);
                    expected[pair.Key] = new HatchExpectation
                    {
                        Area = copy.Area,
                        Scale = copy.PatternScale,
                        Angle = copy.PatternAngle,
                        Loops = copy.NumberOfLoops,
                        Pattern = copy.PatternName,
                        Min = copy.GeometricExtents.MinPoint,
                        Max = copy.GeometricExtents.MaxPoint
                    };
                }
            }
            return expected;
        }

        public static void EvaluateAndValidate(DrawingSnapshot snapshot, Dictionary<string, HatchExpectation> expected)
        {
            foreach (var pair in expected)
            {
                var hatch = snapshot.Get<Hatch>(pair.Key);
                hatch.UpgradeOpen();
                hatch.EvaluateHatch(true);
                var e = pair.Value;
                if (hatch.NumberOfLoops != e.Loops || hatch.PatternName != e.Pattern)
                    throw new InvalidOperationException("Hatch topology/pattern changed: " + pair.Key);
                DrawingUpdater.AssertNear(hatch.Area, e.Area, "Hatch area " + pair.Key);
                if (Math.Abs(hatch.PatternScale - e.Scale) > 1e-9
                    || Math.Abs(hatch.PatternAngle - e.Angle) > 1e-9
                    || hatch.GeometricExtents.MinPoint.DistanceTo(e.Min) > 0.02
                    || hatch.GeometricExtents.MaxPoint.DistanceTo(e.Max) > 0.02)
                    throw new InvalidOperationException("Hatch geometry/style verification failed: " + pair.Key);
            }
        }
    }
}
