using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using Elevator.Core.Serialization;
using Elevator.Core.Project;
using Elevator.Core.Presets;
using Elevator.Core.MachineRoom;
using Elevator.Core.Geometry;

namespace Elevator.AutoCAD
{
    public sealed class VerticalManifest
    {
        public int SchemaVersion = 1;
        public List<string> Keys = new List<string>();
        public string InputJson;
    }

    public sealed class VerticalNativeAudit
    {
        public string Status = "PASS";
        public string LayoutStatus;
        public string CWAbsolutePositionStatus;
        public string CodeDiameterRatioStatus;
        public string WrapAngleStatus;
        public string DeflectorSlabClearanceStatus;
        public string HangerMechanicalStatus = "MECHANICAL REVIEW REQUIRED";
        public double? CalculatedWrapAngleDeg;
        public double? DeflectorToSlabClearanceMm;
        public int EntitiesChecked;
        public int MachineRoomEntitiesChecked;
        public int DimensionCount;
        public int NormalFloors;
        public int ServedStops;
        public int BracketCount;
        public double MaxActualBracketSpacing;
        public double TravelMm;
        public double RoofZ;
        public double CarFloorZ;
        public double CwBottomZ;
        public List<string> ReviewItems;
        public string RailEndAdequacyStatus = "NOT_YET_EVALUABLE";
        public string BracketFixingStatus = "NOT_YET_EVALUABLE";
        public string RailJointDetailStatus = "NOT_YET_IMPLEMENTED";
    }

    public static class VerticalExperimentDrawing
    {
        public const string Prefix = "VerticalExperiment.";
        public const string DataKey = "VerticalExperimentProject";
        public const string ManifestKey = "VerticalExperimentManifest";
        public const string FrameKey = Prefix + "Reference.Frame";

        public static string OutputDirectory
        {
            get
            {
                return Path.Combine(Configuration.Root, "output", "experimental-vertical");
            }
        }

        public static string Json(object value)
        {
            return Elevator.Core.Serialization.CoreJson.Create().Serialize(value);
        }

        public static Dictionary<string, Entity> Find(Database db, Transaction tr)
        {
            var all = new Dictionary<string, Entity>();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in model)
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                using (var xd = e.GetXDataForApplication(DrawingReader.RegApp))
                {
                    if (xd == null
                    || xd.AsArray().Length < 2)
                        continue;
                    string key = Convert.ToString(xd.AsArray()[1].Value);
                    if (!key.StartsWith(
                    Prefix,
                    StringComparison.Ordinal))
                        continue;
                    if (all.ContainsKey(
                        key))
                        throw new InvalidOperationException("Duplicate experimental identity: " + key);
                    all.Add(key, e);
                }
            }
            return all;
        }

        public static ShaftFrame Frame(Dictionary<string, Entity> all)
        {
            if (!all.ContainsKey(FrameKey) || !(all[FrameKey] is Line))
                throw new InvalidOperationException("Experimental placement reference missing.");
            var l = (Line)all[FrameKey];
            var x = (l.EndPoint - l.StartPoint).GetNormal();
            if (Math.Abs(x.Z) > 1e-8)
                throw new InvalidOperationException("Experimental frame must remain in XY.");
            return new ShaftFrame { Origin = l.StartPoint, XAxis = x, YAxis = Vector3d.ZAxis.CrossProduct(x), ZAxis = Vector3d.ZAxis };
        }

        public static VerticalSource ReadSource(Database db, Transaction tr, out SectionSnapshot section)
        {
            var input = ProjectDataStorage.ReadProject(db, tr);
            if (input == null)
                throw new InvalidOperationException("Open a coordinated project with Plan, Machine Room and B-B; project data missing.");
            var plan = DrawingReader.Read(db, tr);
            var mr = MachineRoomDrawing.Read(db, tr);
            section = MachineRoomSectionDrawing.Read(db, tr);
            var design = DesignRules.Calculate(
                input.SW,
                input.SD,
                Configuration.Read<EquipmentProfile>(ElevatorProjectCommand.Family(input).ProfileFile),
                plan.ElevatorParameters.CwDepth);
            if (Math.Abs(design.SW - plan.ElevatorParameters.SW) > .01 || Math.Abs(design.SD - plan.ElevatorParameters.SD) > .01)
                throw new InvalidOperationException("Coordinated Plan is stale. Run its existing UPDATEALL workflow first.");
            design.MachineRoom = MachineRoomRules.Calculate(input, design, mr.Extensions);
            design.MachineRoomSection = MachineRoomSectionRules.Calculate(
                input,
                design,
                section.Manifest.Reference,
                (MachineRoomSectionGeometryAudit.SupportOffset(mr, "P2") + MachineRoomSectionGeometryAudit.SupportOffset(mr, "P4")) / 2,
                (MachineRoomSectionGeometryAudit.SupportOffset(mr, "P1") + MachineRoomSectionGeometryAudit.SupportOffset(mr, "P3")) / 2,
                mr.PitchProjection,
                mr.Entities[MachineRoomDrawing.Pitch].Visible);
            MachineRoomSectionGeometryAudit.Run(section, design, mr);
            Func<string, double[]> projected =
                key => MachineRoomDependencies.Points(plan.Entities[key], plan.Frame).Select(p => -p.Y).ToArray();
            var walls = plan.Bindings
                .Values
                .Where(b => b.Role == "wall")
                .SelectMany(b => MachineRoomDependencies.Points(plan.Entities[b.Key], plan.Frame))
                .ToArray();
            double front = -walls.Min(p => p.Y), rear = walls.Max(p => p.Y) - design.SD;
            if (front <= 0 || rear <= 0)
                throw new InvalidOperationException("Plan wall faces could not be derived.");
            var carSupport = plan.Bindings
                .Values
                .Where(b => b.Role == "carSupportLeft" || b.Role == "carSupportRight")
                .SelectMany(b => projected(b.Key))
                .ToArray();
            var door = projected("Plan.DoorSpan.Part001");
            double[] carRail = projected("Plan.CarGuides.Left.Contact"), cwRail = projected("Plan.CounterweightGuides.Left.Contact");
            // Contact-line endpoints are the validated transverse projected width. The CW support footprint
            // uses the native support geometry in its physical left assembly, excluding annotation entities.
            var cwSupport = plan.Bindings
                .Values
                .Where(b => b.Role == "cwLeft" && (plan.Entities[b.Key] is Line || plan.Entities[b.Key] is Polyline))
                .SelectMany(b => projected(b.Key))
                .ToArray();
            var governor = mr.Entities["MachineRoom.Governor.DiameterDimension"] as DiametricDimension;
            var governorSpan = mr.Entities["MachineRoom.Governor.DiameterDimension"] as RotatedDimension;
            double? governorAxis = governor != null
                ? (double?)-mr.Frame.Local(governor.ChordPoint + (governor.FarChordPoint - governor.ChordPoint) / 2).Y
                : governorSpan != null
                    ? (double?)-mr.Frame.Local(governorSpan.XLine1Point + (governorSpan.XLine2Point - governorSpan.XLine1Point) / 2).Y
                    : null;
            return new VerticalSource
            {
                Plan = plan.ElevatorParameters,
                Section = design.MachineRoomSection,
                FrontWallThicknessMm = front,
                RearWallThicknessMm = rear,
                RoomFrontX = mr.Extensions.Front,
                RoomRearX = -design.SD - mr.Extensions.Rear,
                CarRailProjectedEdges = new[] { carRail.Min(), carRail.Max() },
                CwRailProjectedEdges = new[] { cwRail.Min(), cwRail.Max() },
                CarBracketProjectedEdges = new[] { carSupport.Min(), carSupport.Max() },
                CwBracketProjectedEdges = new[] { cwSupport.Min(), cwSupport.Max() },
                LandingDoorBackX = door.Min(),
                LandingDoorFrontX = door.Max(),
                CarDoorBackX = -design.CarFront,
                CarDoorFrontX = door.Min(),
                CarRearOuterX = projected("Plan.Cabin.Outer.Rear").Min(),
                GovernorProjectedAxisX = governorAxis,
                CodeDiameterRatioStatus = design.MachineRoom.CodeDiameterRatioStatus,
                GovernorStatus = design.MachineRoom.Governor.SelectionStatus
            };
        }

        static ObjectId Model(Database db, Transaction tr)
        {
            return ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace];
        }

        static void Layers(Database db, Transaction tr)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            string[] names =
                { "Structure", "Concrete", "Datum", "Door", "Car", "CW", "Envelope", "Rail", "Bracket", "Rope", "Dimension", "Annotation" };
            foreach (string name in names)
            {
                string n = "EVX_" + name;
                if (lt.Has(n))
                    continue;
                lt.UpgradeOpen();
                var l = new LayerTableRecord
                {
                    Name = n,
                    Color = Color.FromColorIndex(
                        ColorMethod.ByAci,
                        (short)(name == "Rope" ? 1 : name == "Rail" ? 4 : name == "Bracket" ? 3 : name == "Datum" ? 8 : 7)),
                    LineWeight = name == "Structure"
                        ? LineWeight.LineWeight035
                        : name == "Bracket" ? LineWeight.LineWeight050 : LineWeight.LineWeight018
                };
                lt.Add(l);
                tr.AddNewlyCreatedDBObject(l, true);
            }
        }

        public static Entity Create(VerticalPrimitive p, Database db, ShaftFrame f)
        {
            var a = p.Points;
            Func<double, double, Point3d> w = (x, y) => f.World(new Point3d(x, y, 0));
            Entity e;
            if (p.Kind == "Line")
                e = new Line(w(a[0], a[1]), w(a[2], a[3]));
            else if (p.Kind == "Box")
            {
                var poly = new Polyline();
                double[] coords = { a[0], a[1], a[2], a[1], a[2], a[3], a[0], a[3] };
                for (int i = 0; i < 4; i++)
                {
                    var v = w(coords[2 * i], coords[2 * i + 1]);
                    poly.AddVertexAt(i, new Point2d(v.X, v.Y), 0, 0, 0);
                }
                poly.Closed = true;
                e = poly;
            }
            else if (p.Kind == "Text")
            {
                e = new MText
                {
                    Location = w(a[0], a[1]),
                    Contents = p.Text,
                    TextHeight = p.TextHeight,
                    Width = p.Id.EndsWith("Car.Label") ? 200 : p.Id.EndsWith("CW.Label") ? 1100 : 2200,
                    Rotation = Math.Atan2(f.XAxis.Y, f.XAxis.X),
                    Attachment = AttachmentPoint.TopLeft
                };
            }
            else if (p.Kind == "Dimension")
            {
                e = new RotatedDimension(
                    Math.Atan2(f.XAxis.Y, f.XAxis.X) + Math.PI / 2,
                    w(a[0], a[1]),
                    w(a[2], a[3]),
                    w(a[4], (a[1] + a[3]) / 2),
                    p.Text ?? "",
                    db.Dimstyle)
                { Dimtxt = 45, Dimasz = 35, Dimscale = 1, Dimdec = 2, Dimzin = 8, Dimexe = 40, Dimexo = 25, Dimtad = 1 };
            }
            else
                throw new InvalidOperationException("Unknown vertical primitive: " + p.Kind);
            e.SetDatabaseDefaults(db);
            e.Layer = "EVX_" + p.Layer;
            return e;
        }

        static void Append(Database db, Transaction tr, BlockTableRecord model, Entity e, string key, string role)
        {
            model.AppendEntity(e);
            tr.AddNewlyCreatedDBObject(e, true);
            ComponentBinding.Tag(e, key, role);
        }

        public static bool CloneInFullSection(SectionBinding b)
        {
            // Existing B-B drop tails are replaced by continuous drops ending at the actual car/CW envelopes.
            return b.Role != "frame"
                && b.Role != "status"
                && !(b.Role == "rope" && (b.Key.EndsWith("CarDrop") || b.Key.EndsWith("CwDrop")));
        }

        public static Matrix3d SectionPose(SectionSnapshot section, ShaftFrame f, double roofZ)
        {
            return Matrix3d.AlignCoordinateSystem(
                section.Frame.Origin,
                section.Frame.XAxis,
                section.Frame.YAxis,
                section.Frame.ZAxis,
                f.World(new Point3d(0, roofZ, 0)),
                f.XAxis,
                f.YAxis,
                f.ZAxis);
        }

        public static Entity CloneCoordinatedEntity(Entity original)
        {
            var clone = (Entity)original.Clone();
            var caption = clone as MText;
            // Source viewport scale label is not applicable to the new full-height view. Keep its
            // semantic identity, suppress only this copied caption; never edit the source B-B.
            if (caption != null && caption.Contents.Contains("SEC AT B-B"))
                caption.Visible = false;
            return clone;
        }

        public static VerticalNativeAudit Generate(Database db, VerticalProjectData data, Point3d? insertion)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                SectionSnapshot section;
                var source = ReadSource(db, tr, out section);
                var g = VerticalSectionCalculator.Calculate(data, source);
                var all = Find(db, tr);
                ShaftFrame frame;
                if (all.Count > 0)
                    frame = Frame(all);
                else
                {
                    if (!insertion.HasValue)
                        throw new InvalidOperationException("Choose an insertion datum for the experiment.");
                    var plan = DrawingReader.Read(db, tr);
                    frame = new ShaftFrame
                    {
                        Origin = insertion.Value,
                        XAxis = plan.Frame.XAxis,
                        YAxis = plan.Frame.YAxis,
                        ZAxis = plan.Frame.ZAxis
                    };
                }
                // Rebuild owned entities atomically from inputs. No cumulative transforms and no source edits.
                foreach (var e in all.Values)
                {
                    e.UpgradeOpen();
                    e.Erase();
                }
                Layers(db, tr);
                ComponentBinding.Register(db, tr);
                var model = (BlockTableRecord)tr.GetObject(Model(db, tr), OpenMode.ForWrite);
                var axis = new Line(frame.Origin, frame.Origin + frame.XAxis) { Visible = false };
                axis.SetDatabaseDefaults(db);
                Append(db, tr, model, axis, FrameKey, "frame");
                foreach (var p in g.Entities)
                {
                    var e = Create(p, db, frame);
                    Append(db, tr, model, e, p.Id, p.Kind);
                    if (p.Kind == "Box"
                        && (p.Layer == "Concrete" || p.Layer == "Envelope")
                        && Math.Abs((p.Points[2] - p.Points[0]) * (p.Points[3] - p.Points[1])) > .001)
                    {
                        var h = new Hatch();
                        h.SetDatabaseDefaults(db);
                        h.Layer = e.Layer;
                        Append(db, tr, model, h, p.Id + ".Hatch", "Hatch");
                        h.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
                        h.PatternScale = p.Layer == "Concrete" ? 25 : 12;
                        h.Associative = false;
                        var poly = (Polyline)e;
                        var pts = new Point2dCollection();
                        var bulges = new DoubleCollection();
                        for (int i = 0; i <= 4; i++)
                        {
                            pts.Add(poly.GetPoint2dAt(i % 4));
                            bulges.Add(0);
                        }
                        h.AppendLoop(HatchLoopTypes.External, pts, bulges);
                        h.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
                        h.EvaluateHatch(true);
                    }
                }
                var pose = SectionPose(section, frame, g.RoofZ);
                foreach (var b in section.Manifest.Entities.Where(CloneInFullSection))
                {
                    var clone = CloneCoordinatedEntity(section.Entities[b.Key]);
                    clone.TransformBy(pose);
                    Append(db, tr, model, clone, Prefix + "MR." + b.Key, b.Role);
                    var dim = clone as Dimension;
                    if (dim != null)
                        dim.RecomputeDimensionBlock(true);
                }
                data.Floors = g.Floors;
                ProjectDataStorage.Write(db, tr, DataKey, data);
                var manifest = new VerticalManifest
                {
                    Keys = Find(db, tr).Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                    InputJson = Json(data)
                };
                ProjectDataStorage.Write(db, tr, ManifestKey, manifest);
                var audit = VerticalSectionGeometryAudit.Run(db, tr);
                ProjectDataStorage.Write(db, tr, "VerticalExperimentAudit", audit);
                tr.Commit();
                return audit;
            }
        }
    }

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


