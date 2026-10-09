using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.AutoCAD.Common;
using Elevator.AutoCAD.MachineRoom;
using Elevator.AutoCAD.Plan;
using Elevator.AutoCAD.SectionBB;
using Elevator.Core.Geometry;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Serialization;

namespace Elevator.AutoCAD.Vertical
{
    public static class VerticalSectionGeometryAudit
    {
        static void Near(double a, double b, string label)
        {
            if (Math.Abs(a - b) > .015)
                throw new InvalidOperationException("Geometry mismatch: " + label + " (" + a + " / " + b + ")");
        }

        public static void Compare(Entity actual, Entity expected, ShaftFrame f, string id)
        {
            if (actual.GetRXClass().DxfName != expected.GetRXClass().DxfName || actual.Visible != expected.Visible)
                throw new InvalidOperationException("Type/visibility mismatch: " + id);
            var linear = actual as RotatedDimension;
            var expectedLinear = expected as RotatedDimension;
            if (linear != null)
            {
                Near(linear.XLine1Point.DistanceTo(expectedLinear.XLine1Point), 0, id);
                Near(linear.XLine2Point.DistanceTo(expectedLinear.XLine2Point), 0, id);
                Near(linear.Rotation, expectedLinear.Rotation, id);
                if (linear.DimensionText != expectedLinear.DimensionText)
                    throw new InvalidOperationException("Dimension override mismatch: " + id);
                return;
            }
            var ap = MachineRoomDependencies.Points(actual, f);
            var ep = MachineRoomDependencies.Points(expected, f);
            if (ap.Length != ep.Length)
                throw new InvalidOperationException("Topology mismatch: " + id);
            var polyline = actual as Polyline;
            if (polyline != null)
            {
                var target = (Polyline)expected;
                if (polyline.Closed != target.Closed)
                    throw new InvalidOperationException("Polyline closure mismatch: " + id);
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                    Near(polyline.GetBulgeAt(i), target.GetBulgeAt(i), id);
            }
            for (int i = 0; i < ap.Length; i++)
            {
                Near(ap[i].X, ep[i].X, id);
                Near(ap[i].Y, ep[i].Y, id);
            }
            var c = actual as Circle;
            if (c != null)
                Near(c.Radius, ((Circle)expected).Radius, id);
            var arc = actual as Arc;
            if (arc != null)
            {
                Near(arc.Radius, ((Arc)expected).Radius, id);
                Near(arc.TotalAngle, ((Arc)expected).TotalAngle, id);
            }
            var text = actual as MText;
            if (text != null && text.Contents != ((MText)expected).Contents)
                throw new InvalidOperationException("Annotation mismatch: " + id);
            var dim = actual as Dimension;
            if (dim != null)
            {
                Near(dim.Measurement, ((Dimension)expected).Measurement, id);
                if (dim.DimensionText != ((Dimension)expected).DimensionText)
                    throw new InvalidOperationException("Dimension override mismatch: " + id);
            }
        }

        public static VerticalNativeAudit Run(Database db, Transaction tr)
        {
            var data = ProjectDataStorage.Read<VerticalProjectData>(db, tr, VerticalExperimentDrawing.DataKey);
            var manifest = ProjectDataStorage.Read<VerticalManifest>(db, tr, VerticalExperimentDrawing.ManifestKey);
            if (data == null || manifest == null || manifest.SchemaVersion != 1)
                throw new InvalidOperationException("Generate the experimental section first.");
            if (VerticalExperimentDrawing.Json(data) != manifest.InputJson)
                throw new InvalidOperationException("Vertical inputs changed; regenerate with ELEVATORVERTICALEXPERIMENT.");
            SectionSnapshot section;
            var source = VerticalExperimentDrawing.ReadSource(db, tr, out section);
            var g = VerticalSectionCalculator.Calculate(data, source);
            var all = VerticalExperimentDrawing.Find(db, tr);
            var f = VerticalExperimentDrawing.Frame(all);
            if (!all.Keys.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(manifest.Keys))
                throw new InvalidOperationException("Missing/orphan experimental component.");
            var expectedKeys = new HashSet<string> { VerticalExperimentDrawing.FrameKey };
            int dims = 0;
            foreach (var p in g.Entities)
            {
                expectedKeys.Add(p.Id);
                if (!all.ContainsKey(p.Id))
                    throw new InvalidOperationException("Missing: " + p.Id);
                using (var expected = VerticalExperimentDrawing.Create(p, db, f))
                    Compare(all[p.Id], expected, f, p.Id);
                if (p.Kind == "Dimension")
                {
                    dims++;
                    Near(((Dimension)all[p.Id]).Measurement, Math.Abs(p.Points[3] - p.Points[1]), p.Id);
                }
                if (p.Kind == "Box"
                    && (p.Layer == "Concrete" || p.Layer == "Envelope")
                    && Math.Abs((p.Points[2] - p.Points[0]) * (p.Points[3] - p.Points[1])) > .001)
                {
                    string id = p.Id + ".Hatch";
                    expectedKeys.Add(id);
                    if (!all.ContainsKey(id) || !(all[id] is Hatch))
                        throw new InvalidOperationException("Missing hatch: " + id);
                    var h = (Hatch)all[id];
                    if (h.NumberOfLoops != 1)
                        throw new InvalidOperationException("Hatch loop mismatch: " + id);
                    var loop = h.GetLoopAt(0);
                    var poly = (Polyline)all[p.Id];
                    if (!loop.IsPolyline || loop.Polyline.Count != 5)
                        throw new InvalidOperationException("Hatch boundary topology: " + id);
                    for (int i = 0; i <= 4; i++)
                    {
                        Near(loop.Polyline[i].Vertex.X, poly.GetPoint2dAt(i % 4).X, id);
                        Near(loop.Polyline[i].Vertex.Y, poly.GetPoint2dAt(i % 4).Y, id);
                    }
                }
            }
            var pose = VerticalExperimentDrawing.SectionPose(section, f, g.RoofZ);
            int mrCount = 0;
            foreach (var b in section.Manifest.Entities.Where(VerticalExperimentDrawing.CloneInFullSection))
            {
                string id = VerticalExperimentDrawing.Prefix + "MR." + b.Key;
                expectedKeys.Add(id);
                if (!all.ContainsKey(id))
                    throw new InvalidOperationException("Missing coordinated MR component: " + id);
                using (var expected = VerticalExperimentDrawing.CloneCoordinatedEntity(section.Entities[b.Key]))
                {
                    expected.TransformBy(pose);
                    Compare(all[id], expected, f, id);
                }
                mrCount++;
            }
            if (!expectedKeys.SetEquals(all.Keys))
                throw new InvalidOperationException("Unexpected experimental geometry.");
            if (g.Brackets.ConsecutiveSpacings.Any(v => v > data.RequestedBracketMaxSpacingMm + 1e-7))
                throw new InvalidOperationException("Bracket maximum exceeded.");
            return new VerticalNativeAudit
            {
                EntitiesChecked = all.Count,
                MachineRoomEntitiesChecked = mrCount,
                DimensionCount = dims,
                NormalFloors = g.Floors.Count - 1,
                ServedStops = g.Floors.Count(v => v.Served),
                BracketCount = g.Brackets.Elevations.Count * 4,
                MaxActualBracketSpacing = g.Brackets.ConsecutiveSpacings.Max(),
                TravelMm = g.TravelMm,
                RoofZ = g.RoofZ,
                CarFloorZ = g.CarFloorZ,
                CwBottomZ = g.CwBottomZ,
                LayoutStatus = g.LayoutStatus,
                CWAbsolutePositionStatus = g.CWAbsolutePositionStatus,
                CodeDiameterRatioStatus = g.CodeDiameterRatioStatus,
                ReviewItems = g.ReviewItems,
                WrapAngleStatus = source.Section.Arrangement.Reeving.WrapAngleStatus,
                CalculatedWrapAngleDeg = source.Section.Arrangement.CalculatedWrapAngleDeg,
                DeflectorSlabClearanceStatus = source.Section.Arrangement.DeflectorSlabClearanceStatus,
                DeflectorToSlabClearanceMm = source.Section.Arrangement.DeflectorToSlabClearanceMm
            };
        }
    }
}
