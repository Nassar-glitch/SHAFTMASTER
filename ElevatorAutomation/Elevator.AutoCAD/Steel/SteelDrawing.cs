using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.AutoCAD.Common;
using Elevator.AutoCAD.Plan;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Steel;

namespace Elevator.AutoCAD.Steel
{
    /// <summary>
    /// The steel tower in the four views (DESIGN-FLOW "Steel shaft structure"): the walls, slabs and the machine-room walls of a
    /// concrete shaft are hidden (never edited: they stay bound and keep following the shared design) and the tower of the
    /// <see cref="SteelStructure"/> is drawn in their place from its data. Every added entity carries a key of the view it belongs to
    /// ("Plan.Steel.NNN", "Steel.MachineRoom.NNN", "Steel.Section.NNN", "SectionAA.Steel.NNN"); Clear erases them and shows the
    /// hidden geometry again, so a concrete project is never touched.
    /// </summary>
    public static class SteelDrawing
    {
        /// <summary>Layer of the steel tower.</summary>
        public const string LayerName = "Steel";

        public const string PlanPrefix = "Plan.Steel.";
        public const string MachineRoomPrefix = "Steel.MachineRoom.";
        public const string SectionPrefix = "Steel.Section.";
        public const string AAPrefix = "SectionAA.Steel.";

        // Text heights, model mm: they print at 2.5 mm (plan 1:10) and 2.5 mm (A-A and B-B at 1:50 / 1:10).
        const double PlanTextHeight = 40;
        const double ElevationTextHeight = 110;

        static PresetLibrary library;

        /// <summary>The preset library of the installation (config/presets.json); null when it cannot be read.</summary>
        public static PresetLibrary Library()
        {
            if (library == null)
            {
                try
                {
                    library = PresetLibrary.Load(Path.Combine(Configuration.Root, "config", "presets.json"));
                }
                catch (Exception)
                {
                    return null;
                }
            }
            return library;
        }

        /// <summary>The steel tower of the project stored in the drawing; null for a concrete shaft, no project or a structure that cannot be built.</summary>
        public static SteelStructure Read(Database db, Transaction tr)
        {
            ElevatorProject project;
            try
            {
                project = UnifiedProjectStorage.Read(db, tr);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            var lib = Library();
            if (project == null || lib == null || !SteelFields.IsSteel(project, lib))
                return null;
            var steel = SteelStructure.Build(project, lib);
            return steel.Valid ? steel : null;
        }

        // ---------- hiding and clearing ----------
        static string[] Tag(Entity e)
        {
            using (var rb = e.GetXDataForApplication(DrawingReader.RegApp))
            {
                if (rb == null)
                    return null;
                var values = rb.AsArray();
                if (values.Length < 3)
                    return null;
                return new[] { Convert.ToString(values[1].Value), Convert.ToString(values[2].Value) };
            }
        }

        static BlockTableRecord Model(Database db, Transaction tr, OpenMode mode)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], mode);
        }

        /// <summary>Hides the entities of a view whose tag has one of the roles (and, for the keys, whose key matches).</summary>
        public static void Hide(Database db, Transaction tr, string keyPrefix, ICollection<string> roles, ICollection<string> keys = null, bool keepTexts = false)
        {
            foreach (ObjectId id in Model(db, tr, OpenMode.ForRead))
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var tag = Tag(e);
                if (tag == null || !tag[0].StartsWith(keyPrefix, StringComparison.Ordinal) || !e.Visible)
                    continue;
                if (keepTexts && e is MText && ((MText)e).Contents.Contains("B-B"))
                    continue;
                if (roles.Contains(tag[1]) || (keys != null && keys.Contains(tag[0])))
                {
                    e.UpgradeOpen();
                    e.Visible = false;
                }
            }
        }

        /// <summary>
        /// Erases the steel entities of a view and shows again what they hid. Does nothing (reads only) when the view has no steel
        /// entities, so a concrete drawing is never touched.
        /// </summary>
        public static void Clear(Database db, Transaction tr, string steelPrefix, string viewPrefix, ICollection<string> roles, ICollection<string> keys = null)
        {
            var mine = new List<ObjectId>();
            var hidden = new List<ObjectId>();
            foreach (ObjectId id in Model(db, tr, OpenMode.ForRead))
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var tag = Tag(e);
                if (tag == null)
                    continue;
                if (tag[0].StartsWith(steelPrefix, StringComparison.Ordinal))
                    mine.Add(id);
                else if (!e.Visible && tag[0].StartsWith(viewPrefix, StringComparison.Ordinal) && (roles.Contains(tag[1]) || (keys != null && keys.Contains(tag[0]))))
                    hidden.Add(id);
            }
            if (mine.Count == 0)
                return;
            foreach (var id in mine)
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                e.Erase();
            }
            foreach (var id in hidden)
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                e.Visible = true;
            }
        }

        /// <summary>The layer (switched off, not plotted) of the concrete-room entities the steel tower replaces and that the machine-room audit needs visible.</summary>
        public const string HiddenLayer = "Steel.Hidden";

        public static bool OffLayer(Entity e)
        {
            return e.Layer == HiddenLayer;
        }

        static void EnsureHiddenLayer(Database db, Transaction tr)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(HiddenLayer))
                return;
            lt.UpgradeOpen();
            var l = new LayerTableRecord { Name = HiddenLayer, IsOff = true, IsPlottable = false };
            lt.Add(l);
            tr.AddNewlyCreatedDBObject(l, true);
        }

        static void EnsureLayer(Database db, Transaction tr)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(LayerName))
                return;
            lt.UpgradeOpen();
            var l = new LayerTableRecord { Name = LayerName, IsPlottable = true };
            lt.Add(l);
            tr.AddNewlyCreatedDBObject(l, true);
        }

        // ---------- the writer ----------
        /// <summary>Adds tagged entities to the model space from local (u, v) coordinates of a view.</summary>
        public sealed class Writer
        {
            readonly Database db;
            readonly Transaction tr;
            readonly BlockTableRecord model;
            readonly string prefix;
            readonly string role;
            readonly Func<double, double, Point3d> world;
            readonly Vector3d unit;
            int count;

            public Writer(Database db, Transaction tr, string prefix, string role, Func<double, double, Point3d> world)
            {
                this.db = db;
                this.tr = tr;
                this.prefix = prefix;
                this.role = role;
                this.world = world;
                var o = world(0, 0);
                unit = world(1, 0) - o;
                EnsureLayer(db, tr);
                ComponentBinding.Register(db, tr);
                model = Model(db, tr, OpenMode.ForWrite);
            }

            public int Count
            {
                get { return count; }
            }

            T Add<T>(T e) where T : Entity
            {
                count++;
                e.Layer = LayerName;
                model.AppendEntity(e);
                tr.AddNewlyCreatedDBObject(e, true);
                ComponentBinding.Tag(e, prefix + count.ToString("000", CultureInfo.InvariantCulture), role);
                return e;
            }

            Point2d P2(double u, double v)
            {
                var p = world(u, v);
                return new Point2d(p.X, p.Y);
            }

            public Polyline Poly(IList<double[]> points, bool closed)
            {
                var pl = new Polyline();
                for (int i = 0; i < points.Count; i++)
                    pl.AddVertexAt(i, P2(points[i][0], points[i][1]), 0, 0, 0);
                pl.Closed = closed;
                pl.Elevation = world(0, 0).Z;
                return Add(pl);
            }

            public Polyline Rect(double u0, double v0, double u1, double v1)
            {
                return Poly(new[] { new[] { u0, v0 }, new[] { u1, v0 }, new[] { u1, v1 }, new[] { u0, v1 } }, true);
            }

            /// <summary>Adds an entity built by the caller (it is placed on the steel layer and tagged).</summary>
            public T Adopt<T>(T entity) where T : Entity
            {
                return Add(entity);
            }

            /// <summary>Adds a displaced copy of an existing entity.</summary>
            public T Copy<T>(T source, Matrix3d move) where T : Entity
            {
                var e = (T)source.Clone();
                e.TransformBy(move);
                e.Visible = true;
                return Add(e);
            }

            public Line Seg(double u0, double v0, double u1, double v1)
            {
                return Add(new Line(world(u0, v0), world(u1, v1)));
            }

            public Circle Circ(double u, double v, double diameter)
            {
                return Add(new Circle(world(u, v), Vector3d.ZAxis, diameter / 2));
            }

            public MText Text(double u, double v, double height, string contents)
            {
                var t = new MText { Location = world(u, v), TextHeight = height, Contents = contents };
                t.SetDatabaseDefaults(db);
                t.Attachment = AttachmentPoint.TopLeft;
                return Add(t);
            }

            /// <summary>Fills a closed polyline with a predefined pattern (SOLID, ANSI31, AR-CONC).</summary>
            public void Fill(Polyline boundary, string pattern, double scale)
            {
                var h = new Hatch();
                h.SetDatabaseDefaults(db);
                Add(h);
                h.Normal = Vector3d.ZAxis;
                h.Elevation = boundary.Elevation;
                try
                {
                    h.SetHatchPattern(HatchPatternType.PreDefined, pattern);
                    if (pattern != "SOLID")
                        h.PatternScale = scale;
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    h.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
                    h.PatternScale = scale;
                }
                h.Associative = false;
                h.AppendLoop(HatchLoopTypes.Default, new ObjectIdCollection(new[] { boundary.ObjectId }));
                h.EvaluateHatch(true);
            }

            /// <summary>A label with a leader line from (u0, v0) to the text at (u1, v1).</summary>
            public void Label(double u0, double v0, double u1, double v1, double height, string text)
            {
                Seg(u0, v0, u1, v1);
                // the text sits to the right of the leader end
                Text(u1 + height * 0.3, v1 + height * 0.6, height, text);
            }

            /// <summary>A label whose text ends at the end of its leader (the leader goes to the left of the member).</summary>
            public void LabelLeft(double u0, double v0, double u1, double v1, double height, string text)
            {
                Seg(u0, v0, u1, v1);
                var t = new MText { Location = world(u1 - height * 0.3, v1 + height * 0.2), TextHeight = height, Contents = text };
                t.SetDatabaseDefaults(db);
                t.Attachment = AttachmentPoint.BottomRight;
                Add(t);
            }

            /// <summary>A diagonal member: a strip of the given width between two points.</summary>
            public void Strip(double u0, double v0, double u1, double v1, double width)
            {
                double du = u1 - u0, dv = v1 - v0, len = Math.Sqrt(du * du + dv * dv);
                if (len < 1e-6)
                    return;
                double nx = -dv / len * width / 2, ny = du / len * width / 2;
                Poly(new[]
                {
                    new[] { u0 + nx, v0 + ny }, new[] { u1 + nx, v1 + ny }, new[] { u1 - nx, v1 - ny }, new[] { u0 - nx, v0 - ny }
                }, true);
            }
        }

        // ---------- plan ----------
        static readonly string[] PlanRoles = { "wall" };

        /// <summary>Clears the steel entities of the plan and shows the template walls again.</summary>
        public static void ClearPlan(Database db, Transaction tr)
        {
            Clear(db, tr, PlanPrefix, "Plan.", PlanRoles);
            RestoreFrontRows(db, tr);
        }

        const string FrontRowsRecord = "SteelPlanFrontRows";

        static bool IsFrontRowKey(string key)
        {
            return key.StartsWith("Plan.Dimension.", StringComparison.Ordinal) || key.StartsWith("Plan.CenterFront.", StringComparison.Ordinal);
        }

        // The dimension rows and the title below the front face move down clear of the building slab edge (the slab edge stands directly
        // outside the front beam). The moved keys and the distance are kept so that Clear puts them back.
        static void ShiftFrontRows(Database db, Transaction tr, ShaftFrame f, SteelStructure st)
        {
            var moved = new List<Entity>();
            double nearest = double.NegativeInfinity;
            foreach (ObjectId id in Model(db, tr, OpenMode.ForRead))
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var tag = Tag(e);
                if (tag == null || !e.Visible || !IsFrontRowKey(tag[0]))
                    continue;
                Extents3d box;
                try
                {
                    box = e.GeometricExtents;
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    continue;
                }
                double y1 = Math.Max(f.Local(box.MinPoint).Y, f.Local(box.MaxPoint).Y);
                if (y1 > 1)
                    continue;
                moved.Add(e);
                var d = e as RotatedDimension;
                if (d != null && Math.Abs(Math.Sin(d.Rotation)) < 1e-6)
                    nearest = Math.Max(nearest, f.Local(d.DimLinePoint).Y);
            }
            if (moved.Count == 0 || Double.IsNegativeInfinity(nearest))
                return;
            double target = -(st.Inputs.Beam.WidthMm + st.Inputs.SlabEdgeDepthMm) - 30;
            double delta = target - nearest;
            if (delta > -0.5)
                return;
            var move = Matrix3d.Displacement(f.World(new Point3d(0, delta, 0)) - f.World(new Point3d(0, 0, 0)));
            var keys = new List<string> { delta.ToString("R", CultureInfo.InvariantCulture) };
            foreach (var e in moved)
            {
                e.UpgradeOpen();
                e.TransformBy(move);
                keys.Add(Tag(e)[0]);
            }
            ProjectDataStorage.Write(db, tr, FrontRowsRecord, keys);
        }

        static void RestoreFrontRows(Database db, Transaction tr)
        {
            var keys = ProjectDataStorage.Read<List<string>>(db, tr, FrontRowsRecord);
            if (keys == null || keys.Count < 2)
                return;
            double delta = Double.Parse(keys[0], CultureInfo.InvariantCulture);
            var set = new HashSet<string>(keys.Skip(1));
            var frame = DrawingReader.Read(db, tr).Frame;
            var move = Matrix3d.Displacement(frame.World(new Point3d(0, -delta, 0)) - frame.World(new Point3d(0, 0, 0)));
            foreach (ObjectId id in Model(db, tr, OpenMode.ForRead))
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var tag = Tag(e);
                if (tag == null || !set.Contains(tag[0]))
                    continue;
                e.UpgradeOpen();
                e.TransformBy(move);
            }
            ProjectDataStorage.Write(db, tr, FrontRowsRecord, new List<string>());
        }

        /// <summary>Draws the steel shaft in the plan (call after the plan update). The template walls are hidden.</summary>
        public static void DrawPlan(Database db, Transaction tr, DrawingSnapshot s, SteelStructure st)
        {
            Hide(db, tr, "Plan.", PlanRoles);
            var f = s.Frame;
            ShiftFrontRows(db, tr, f, st);
            var w = new Writer(db, tr, PlanPrefix, "steel", (u, v) => f.World(new Point3d(u, v, 0)));
            PlanTower(w, st, st.Plan.Columns, st.Plan.Beams, true);
            w.Rect(st.SlabEdge.X0, st.SlabEdge.Y0, st.SlabEdge.X1, st.SlabEdge.Y1);
        }

        // The columns (solid sections), the base plates with their bolts and the beam ring, in plan; labels in the style of the guide-rail
        // labels of the plan.
        static void PlanTower(Writer w, SteelStructure st, IList<SteelColumnPlan> columns, IList<SteelBeamPlan> beams, bool plates)
        {
            foreach (var b in beams)
            {
                w.Rect(b.Box.X0, b.Box.Y0, b.Box.X1, b.Box.Y1);
                // the web: a line on the outer face of the channel
                if (b.Face == "Front" || b.Face == "Back")
                {
                    double outer = b.Face == "Front" ? b.Box.Y0 + st.Inputs.Beam.WebMm : b.Box.Y1 - st.Inputs.Beam.WebMm;
                    w.Seg(b.Box.X0, outer, b.Box.X1, outer);
                }
                else
                {
                    double outer = b.Face == "Left" ? b.Box.X0 + st.Inputs.Beam.WebMm : b.Box.X1 - st.Inputs.Beam.WebMm;
                    w.Seg(outer, b.Box.Y0, outer, b.Box.Y1);
                }
            }
            foreach (var c in columns)
            {
                var pts = c.Outline.Select(p => new[] { p.X, p.Y }).ToList();
                var outline = w.Poly(pts, true);
                w.Fill(outline, "SOLID", 1);
                if (plates)
                {
                    w.Rect(c.Plate.X0, c.Plate.Y0, c.Plate.X1, c.Plate.Y1);
                    foreach (var bolt in c.Bolts)
                        w.Circ(bolt.X, bolt.Y, bolt.DiameterMm);
                }
            }
            var fl = columns.First(c => c.Corner == "FrontLeft");
            var bl = columns.First(c => c.Corner == "BackLeft");
            var fr = columns.First(c => c.Corner == "FrontRight");
            var br = columns.First(c => c.Corner == "BackRight");
            string columnText = st.Inputs.Column.Name + " STEEL COLUMN", beamText = st.Inputs.Beam.Name + " STEEL BEAM";
            // the labels sit clear of the dimension rows; the members are named on both sides
            var right = beams.First(b => b.Face == "Right");
            var left = beams.First(b => b.Face == "Left");
            double ly = right.Box.Y0 + (right.Box.Y1 - right.Box.Y0) * 0.25;
            w.Label(right.Box.X1, ly, right.Box.X1 + 500, ly, PlanTextHeight, beamText);
            if (plates)
            {
                // shaft plan: the columns at the left corners, the beam of the left face
                w.LabelLeft(fl.Box.X0, fl.Box.CenterY, fl.Box.X0 - 800, fl.Box.CenterY, PlanTextHeight, columnText);
                w.LabelLeft(bl.Box.X0, bl.Box.CenterY, bl.Box.X0 - 800, bl.Box.CenterY, PlanTextHeight, columnText);
                double lly = left.Box.Y0 + (left.Box.Y1 - left.Box.Y0) * 0.5;
                w.LabelLeft(left.Box.X0, lly, left.Box.X0 - 560, lly, PlanTextHeight, beamText);
            }
            else
            {
                // tower-top plan: the two front columns, leaders running down and outwards
                w.LabelLeft(fl.Box.X0, fl.Box.Y0, fl.Box.X0 - 500, fl.Box.Y0 - 500, PlanTextHeight, columnText);
                w.Label(fr.Box.X1, fr.Box.Y0, fr.Box.X1 + 500, fr.Box.Y0 - 500, PlanTextHeight, columnText);
            }
            if (plates)
            {
                var plate = br.Plate;
                w.Label(plate.X1, plate.Y1, plate.X1 + 150, plate.Y1 + 150, PlanTextHeight,
                    "BASE PL " + L(st.Inputs.PlateWidthMm) + "X" + L(st.Inputs.PlateDepthMm) + "X" + L(st.Inputs.PlateThicknessMm) + "MM., "
                    + st.Inputs.BoltCount + " BOLTS " + L(st.Inputs.BoltDiameterMm) + "MM.");
            }
        }

        static string L(double v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        // ---------- machine-room plan (the tower top) ----------
        static readonly string[] MachineRoomRoles = { "room", "ventLeft", "ventRight", "front", "frontBeam", "panel" };
        static readonly string[] MachineRoomKeys =
        {
            "MachineRoom.Dimension.RoomWidth",
            "MachineRoom.Dimension.RoomDepth",
            "MachineRoom.Dimension.FrontExtension",
            "MachineRoom.Dimension.LeftExtension",
            "MachineRoom.Dimension.RightExtension"
        };

        const string LeftoverRecord = "SteelMachineRoomHidden";

        /// <summary>Clears the steel entities of the machine-room plan and shows the room and the leftovers again.</summary>
        public static void ClearMachineRoom(Database db, Transaction tr)
        {
            Clear(db, tr, MachineRoomPrefix, "MachineRoom.", MachineRoomRoles, MachineRoomKeys);
            var hidden = ProjectDataStorage.Read<List<string>>(db, tr, LeftoverRecord);
            if (hidden == null || hidden.Count == 0)
                return;
            var layers = new Dictionary<string, string>();
            foreach (string h in hidden.Where(h => h.StartsWith("L|", StringComparison.Ordinal)))
            {
                string[] parts = h.Split('|');
                layers[parts[1]] = parts[2];
            }
            foreach (ObjectId id in Model(db, tr, OpenMode.ForRead))
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var tag = Tag(e);
                if (tag == null)
                    continue;
                string layer;
                if (layers.TryGetValue(tag[0], out layer) && e.Layer == HiddenLayer)
                {
                    e.UpgradeOpen();
                    e.Layer = layer;
                }
                else if (!e.Visible && hidden.Contains(tag[0]))
                {
                    e.UpgradeOpen();
                    e.Visible = true;
                }
            }
            ProjectDataStorage.Write(db, tr, LeftoverRecord, new List<string>());
        }

        // Details of the concrete machine room that stand outside the tower: the wall-opening symbol left of the shaft (tagged with
        // the governor in the master): lines and polylines of the governor assembly further than 450 mm outside the left face.
        static List<string> HideLeftovers(Database db, Transaction tr, ShaftFrame frame)
        {
            var keys = new List<string>();
            foreach (ObjectId id in Model(db, tr, OpenMode.ForRead))
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var tag = Tag(e);
                if (tag == null || tag[1] != "governor" || !tag[0].StartsWith("MachineRoom.", StringComparison.Ordinal) || !e.Visible)
                    continue;
                if (!(e is Line) && !(e is Polyline))
                    continue;
                Extents3d box;
                try
                {
                    box = e.GeometricExtents;
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    continue;
                }
                double x1 = Math.Max(frame.Local(box.MinPoint).X, frame.Local(box.MaxPoint).X);
                if (x1 < -450)
                {
                    e.UpgradeOpen();
                    e.Visible = false;
                    keys.Add(tag[0]);
                }
            }
            return keys;
        }

        /// <summary>Draws the top of the tower in place of the machine room (call after the machine-room update).</summary>
        public static void DrawMachineRoom(Database db, Transaction tr, ShaftFrame frame, SteelStructure st)
        {
            Hide(db, tr, "MachineRoom.", MachineRoomRoles, MachineRoomKeys);
            var left = HideLeftovers(db, tr, frame);
            left.AddRange(MachineWithinFootprint(db, tr, frame, st));
            ProjectDataStorage.Write(db, tr, LeftoverRecord, left);
            var w = new Writer(db, tr, MachineRoomPrefix, "MachineRoom", (u, v) => frame.World(new Point3d(u, v, 0)));
            var top = st.TowerTop;
            PlanTower(w, st, top.Columns, top.Beams, false);
            w.Rect(st.SlabEdge.X0, st.SlabEdge.Y0, st.SlabEdge.X1, st.SlabEdge.Y1);
            double lowest = DrawController(db, tr, frame, st, top);
            GovernorDimension(db, tr, frame, w);
            // the title of the view: the sheet writes its scale into it, as into the other titles
            var title = w.Text(st.Inputs.WidthMm / 2, Math.Min(lowest, st.SlabEdge.Y0) - 450, PlanTextHeight * 2, "{\\Fcent|c0;\\L\\C6;TOWER TOP 1:10}");
            title.Attachment = AttachmentPoint.TopCenter;
        }

        const string MachinePrefix = "Steel.MachineRoom.Machine.";

        // The machine supports P1 to P4 and the machine beams of the master end on the walls (beyond the shaft). On a steel tower they
        // stand on the ring beams inside the envelope: the support groups move in by the overshoot of the outermost one, so that its outer
        // edge is on the outer face of the ring beam, and the machine beams are cut at the beam faces. The concrete-room entities are
        // hidden (never edited) and copies are drawn; the hidden keys are returned for the record that Clear uses.
        static List<string> MachineWithinFootprint(Database db, Transaction tr, ShaftFrame frame, SteelStructure st)
        {
            double bb = st.Inputs.Beam.WidthMm, depth = st.Inputs.DepthMm;
            double inset = 10;
            double lo = -bb + inset, hi = depth + bb - inset;
            var groups = new Dictionary<string, List<Entity>>();
            var beams = new List<Entity>();
            var extents = new Dictionary<string, double[]>();
            foreach (ObjectId id in Model(db, tr, OpenMode.ForRead))
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var tag = Tag(e);
                if (tag == null || !e.Visible || tag[1] != "machine")
                    continue;
                string[] parts = tag[0].Split('.');
                if (parts.Length < 3 || parts[0] != "MachineRoom")
                    continue;
                if (parts[1] == "Support" && parts[2].Length == 2 && parts[2][0] == 'P')
                {
                    List<Entity> list;
                    if (!groups.TryGetValue(parts[2], out list))
                        groups[parts[2]] = list = new List<Entity>();
                    list.Add(e);
                    Extents3d box;
                    try
                    {
                        box = e.GeometricExtents;
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception)
                    {
                        continue;
                    }
                    double y0 = Math.Min(frame.Local(box.MinPoint).Y, frame.Local(box.MaxPoint).Y);
                    double y1 = Math.Max(frame.Local(box.MinPoint).Y, frame.Local(box.MaxPoint).Y);
                    double[] ex;
                    if (!extents.TryGetValue(parts[2], out ex))
                        extents[parts[2]] = ex = new[] { double.MaxValue, double.MinValue };
                    ex[0] = Math.Min(ex[0], y0);
                    ex[1] = Math.Max(ex[1], y1);
                }
                else if ((parts[1] == "Chassis" || parts[1] == "MachineChassis") && (e is Polyline || e is Line))
                    beams.Add(e);
            }
            var hidden = new List<string>();
            if (groups.Count == 0)
                return hidden;
            double overshoot = 0;
            var front = new HashSet<string>();
            foreach (var g in extents)
            {
                bool isFront = (g.Value[0] + g.Value[1]) / 2 < depth / 2;
                if (isFront)
                {
                    front.Add(g.Key);
                    overshoot = Math.Max(overshoot, -bb - g.Value[0]);
                }
                else
                    overshoot = Math.Max(overshoot, g.Value[1] - (depth + bb));
            }
            var w = new Writer(db, tr, MachinePrefix, "machine", (u, v) => frame.World(new Point3d(u, v, 0)));
            var origin = frame.World(new Point3d(0, 0, 0));
            if (overshoot > 0.5)
            {
                foreach (var g in groups)
                {
                    double dy = front.Contains(g.Key) ? overshoot : -overshoot;
                    var move = Matrix3d.Displacement(frame.World(new Point3d(0, dy, 0)) - origin);
                    foreach (var e in g.Value)
                    {
                        w.Copy(e, move);
                        hidden.Add(TakeOff(e));
                    }
                }
            }
            // the machine beams: cut at the ring beams
            foreach (var e in beams)
            {
                var copy = CutAt(e, frame, lo, hi);
                if (copy == null)
                    continue;
                w.Adopt(copy);
                hidden.Add(TakeOff(e));
            }
            return hidden;
        }

        // Puts a concrete-room entity on the switched-off layer (it stays visible for the machine-room audit); returns its record entry.
        static string TakeOff(Entity e)
        {
            string key = Tag(e)[0], layer = e.Layer;
            var tr = e.Database.TransactionManager.TopTransaction;
            EnsureHiddenLayer(e.Database, tr);
            e.UpgradeOpen();
            e.Layer = HiddenLayer;
            return "L|" + key + "|" + layer;
        }

        // A copy of a polyline or line with its points clamped to y in [lo, hi] (local); null when nothing reaches beyond.
        static Entity CutAt(Entity e, ShaftFrame frame, double lo, double hi)
        {
            var line = e as Line;
            if (line != null)
            {
                var a = frame.Local(line.StartPoint);
                var b = frame.Local(line.EndPoint);
                if (a.Y >= lo - 1e-6 && a.Y <= hi + 1e-6 && b.Y >= lo - 1e-6 && b.Y <= hi + 1e-6)
                    return null;
                var copy = (Line)line.Clone();
                copy.StartPoint = frame.World(new Point3d(a.X, Math.Max(lo, Math.Min(hi, a.Y)), a.Z));
                copy.EndPoint = frame.World(new Point3d(b.X, Math.Max(lo, Math.Min(hi, b.Y)), b.Z));
                copy.Visible = true;
                return copy;
            }
            var pl = e as Polyline;
            if (pl == null)
                return null;
            bool beyond = false;
            for (int i = 0; i < pl.NumberOfVertices; i++)
            {
                double y = frame.Local(pl.GetPoint3dAt(i)).Y;
                if (y < lo - 1e-6 || y > hi + 1e-6)
                    beyond = true;
            }
            if (!beyond || pl.HasBulges)
                return null;
            var cp = (Polyline)pl.Clone();
            for (int i = 0; i < cp.NumberOfVertices; i++)
            {
                var local = frame.Local(cp.GetPoint3dAt(i));
                var world = frame.World(new Point3d(local.X, Math.Max(lo, Math.Min(hi, local.Y)), local.Z));
                cp.SetPointAt(i, new Point2d(world.X, world.Y));
            }
            cp.Visible = true;
            return cp;
        }

        // The dimension from the governor to the machine (the car centre line), below the governor, text placed by DimensionLayout.
        static void GovernorDimension(Database db, Transaction tr, ShaftFrame frame, Writer w)
        {
            double gx0 = double.MaxValue, gx1 = double.MinValue, gy0 = double.MaxValue;
            var centres = new Dictionary<double, int>();
            double axisX = double.NaN, axisY0 = double.NaN;
            RotatedDimension template = null;
            foreach (ObjectId id in Model(db, tr, OpenMode.ForRead))
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var tag = Tag(e);
                if (tag == null)
                    continue;
                if (tag[1] == "governor" && tag[0].StartsWith("MachineRoom.", StringComparison.Ordinal) && e.Visible && (e is Line || e is Polyline))
                {
                    try
                    {
                        var box = e.GeometricExtents;
                        var a = frame.Local(box.MinPoint);
                        var b = frame.Local(box.MaxPoint);
                        double x1 = Math.Max(a.X, b.X), x0 = Math.Min(a.X, b.X);
                        if (x1 > 0 && x0 < 600)
                        {
                            gx0 = Math.Min(gx0, x0);
                            gx1 = Math.Max(gx1, x1);
                            gy0 = Math.Min(gy0, Math.Min(a.Y, b.Y));
                            // the pulley of the governor: the polylines that share one centre
                            if (e is Polyline && x1 - x0 > 40)
                            {
                                double c = Math.Round((x0 + x1) / 2 / 2) * 2;
                                int n;
                                centres[c] = centres.TryGetValue(c, out n) ? n + 1 : 1;
                            }
                        }
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception)
                    {
                    }
                }
                if (tag[1] == "mainAxis" && tag[0] == "MachineRoom.Reference.ShaftCenterline")
                {
                    var line = e as Line;
                    if (line != null)
                    {
                        axisX = frame.Local(line.StartPoint).X;
                        axisY0 = Math.Min(frame.Local(line.StartPoint).Y, frame.Local(line.EndPoint).Y);
                    }
                }
                if (template == null && e.Visible && e is RotatedDimension && tag[0].StartsWith("MachineRoom.Dimension.", StringComparison.Ordinal))
                {
                    var d = (RotatedDimension)e;
                    if (Math.Abs(Math.Sin(d.Rotation)) < 1e-6)
                        template = d;
                }
            }
            if (gx0 > gx1 || Double.IsNaN(axisX) || template == null)
                return;
            double governorAxis = (gx0 + gx1) / 2;
            if (centres.Count > 0)
                governorAxis = centres.OrderByDescending(k => k.Value).First().Key;
            var dim = (RotatedDimension)template.Clone();
            double line0 = gy0 - 180;
            dim.XLine1Point = frame.World(new Point3d(governorAxis, gy0, 0));
            dim.XLine2Point = frame.World(new Point3d(axisX, gy0, 0));
            dim.DimLinePoint = frame.World(new Point3d((governorAxis + axisX) / 2, line0, 0));
            dim.DimensionText = "";
            dim.Dimdec = 0;
            double height = dim.Dimtxt;
            string text = Math.Round(Math.Abs(axisX - governorAxis)).ToString(CultureInfo.InvariantCulture);
            var place = Elevator.Core.Geometry.DimensionLayout.PlaceChainTexts(new[] { Math.Min(governorAxis, axisX), Math.Max(governorAxis, axisX) }, new[] { text }, height, dim.Dimasz, new List<double[]>());
            var centre = new Point3d((governorAxis + axisX) / 2 + place[0].AlongMm, line0, 0);
            dim.TextPosition = frame.World(new Point3d(centre.X, line0 + height * 0.75, 0));
            dim.Visible = true;
            w.Adopt(dim);
        }

        const string ControllerPrefix = "Steel.MachineRoom.Controller.";

        // The control panel of the concrete machine room (the same drawing symbol, copied) beside the tower top on the door side, right
        // of the door as seen from the landing, with its short label.
        static double DrawController(Database db, Transaction tr, ShaftFrame frame, SteelStructure st, SteelTowerTop top)
        {
            var symbol = new List<Polyline>();
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (ObjectId id in Model(db, tr, OpenMode.ForRead))
            {
                var e = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                if (e == null)
                    continue;
                var tag = Tag(e);
                if (tag == null || tag[1] != "panel" || !tag[0].StartsWith("MachineRoom.ControlPanel.", StringComparison.Ordinal))
                    continue;
                symbol.Add(e);
                var box = e.GeometricExtents;
                var a = frame.Local(box.MinPoint);
                var b = frame.Local(box.MaxPoint);
                minX = Math.Min(minX, Math.Min(a.X, b.X));
                maxX = Math.Max(maxX, Math.Max(a.X, b.X));
                minY = Math.Min(minY, Math.Min(a.Y, b.Y));
                maxY = Math.Max(maxY, Math.Max(a.Y, b.Y));
            }
            if (symbol.Count == 0)
                return top.Outline.Y0;
            // The symbol stands in front of the entrance on the landing, right of the door as seen from the landing, turned a quarter so
            // that its front faces the landing (user's edit of the S3 sheet). Rotating (x, y) by -90 degrees gives x' in [minY, maxY] and
            // y' in [-maxX, -minX].
            double widthR = maxY - minY, heightR = maxX - minX;
            double x0 = st.Inputs.WidthMm / 2 + 800, y0 = st.SlabEdge.Y0 - 30 - heightR;
            var origin = frame.World(new Point3d(0, 0, 0));
            var w = new Writer(db, tr, ControllerPrefix, "controller", (u, v) => frame.World(new Point3d(u, v, 0)));
            var turn = Matrix3d.Rotation(-Math.PI / 2, Vector3d.ZAxis, origin);
            double dx = x0 - minY, dy = y0 + maxX;
            var move = Matrix3d.Displacement(frame.World(new Point3d(dx, dy, 0)) - origin) * turn;
            foreach (var e in symbol)
                w.Copy(e, move);
            // the label stands left of the symbol with a leader to it, clear of the column labels on the right
            w.LabelLeft(x0 + widthR * 0.3, y0 + heightR * 0.2, x0 - 300, y0 - 120, PlanTextHeight, "CONTROL PANEL");
            return y0 - 120;
        }

        // ---------- section B-B (the machine on the steel top frame) ----------
        static readonly string[] SectionRoles = { "slab", "concrete" };

        /// <summary>Clears the steel entities of B-B and shows the slab and the concrete blocks again.</summary>
        public static void ClearSection(Database db, Transaction tr)
        {
            Clear(db, tr, SectionPrefix, "MachineRoomSection.", SectionRoles);
            var record = ProjectDataStorage.Read<List<string>>(db, tr, SectionRecord);
            if (record == null || record.Count == 0)
                return;
            var hidden = new HashSet<string>(record.Where(r => r.StartsWith("H|", StringComparison.Ordinal)).Select(r => r.Substring(2)));
            var decimals = new Dictionary<string, int>();
            foreach (string r in record.Where(r => r.StartsWith("D|", StringComparison.Ordinal)))
            {
                string[] parts = r.Split('|');
                decimals[parts[1]] = Int32.Parse(parts[2], CultureInfo.InvariantCulture);
            }
            foreach (ObjectId id in Model(db, tr, OpenMode.ForRead))
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var tag = Tag(e);
                if (tag == null)
                    continue;
                int dec;
                if (hidden.Contains(tag[0]) && !e.Visible)
                {
                    e.UpgradeOpen();
                    e.Visible = true;
                }
                else if (decimals.TryGetValue(tag[0], out dec) && e is Dimension)
                {
                    e.UpgradeOpen();
                    ((Dimension)e).Dimdec = dec;
                }
            }
            ProjectDataStorage.Write(db, tr, SectionRecord, new List<string>());
        }

        const string SectionRecord = "SteelSectionNotes";

        // B-B of a steel tower has no concrete heights: the concrete-height dimension and the rope-fall box note are hidden, and the
        // dimensions show whole millimetres (the values come from the real geometry).
        static void SectionNotes(Database db, Transaction tr)
        {
            var record = new List<string>();
            foreach (ObjectId id in Model(db, tr, OpenMode.ForRead))
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var tag = Tag(e);
                if (tag == null || !e.Visible || !tag[0].StartsWith("MachineRoomSection.", StringComparison.Ordinal))
                    continue;
                if (tag[0] == "MachineRoomSection.Dimension.ConcreteHeight" || tag[0].StartsWith("MachineRoomSection.Annotation.RopeFallBox.", StringComparison.Ordinal))
                {
                    e.UpgradeOpen();
                    e.Visible = false;
                    record.Add("H|" + tag[0]);
                }
                else if (e is Dimension && tag[0].StartsWith("MachineRoomSection.Dimension.", StringComparison.Ordinal))
                {
                    var d = (Dimension)e;
                    record.Add("D|" + tag[0] + "|" + d.Dimdec.ToString(CultureInfo.InvariantCulture));
                    d.UpgradeOpen();
                    d.Dimdec = 0;
                }
            }
            ProjectDataStorage.Write(db, tr, SectionRecord, record);
        }

        // A channel section of a beam end-on, web on the outer side: u0..u1 across, z0..z1 the height.
        static void ChannelSection(Writer w, double outer, double inner, double z0, double z1, double web, double flange)
        {
            // outer: the x of the web side; inner: the x of the flange tips
            w.Poly(new[]
            {
                new[] { inner, z1 }, new[] { outer, z1 }, new[] { outer, z0 }, new[] { inner, z0 },
                new[] { inner, z0 + flange }, new[] { outer + Math.Sign(inner - outer) * web, z0 + flange },
                new[] { outer + Math.Sign(inner - outer) * web, z1 - flange }, new[] { inner, z1 - flange }
            }, true);
        }

        /// <summary>
        /// Draws the steel top frame in B-B: the front and back top beams (UPN channels cut across) under the ends of the machine beams and
        /// the column stubs below them. The slab and the concrete bearing blocks are hidden. x is the depth axis, front at 0, y the height.
        /// </summary>
        public static void DrawSection(Database db, Transaction tr, ShaftFrame frame, SteelStructure st, double machineBeamBottom)
        {
            // the title of the section stays: it is tagged with the slab of the master
            Hide(db, tr, "MachineRoomSection.", SectionRoles, null, true);
            SectionNotes(db, tr);
            var w = new Writer(db, tr, SectionPrefix, "MachineRoomSection", (u, v) => frame.World(new Point3d(u, v, 0)));
            double depth = st.Inputs.DepthMm;
            double bb = st.Inputs.Beam.WidthMm;
            var beam = st.Inputs.Beam;
            var col = st.Inputs.Column;
            double zBottom = machineBeamBottom - beam.HeightMm;
            // the column stubs below the machine beams with a break (the beams themselves are the machine bearings of the section; the
            // section carries no profile labels). The front column box is x = bb - column depth .. bb (x = -y), the back one mirrored.
            // the machine housing: walls on the column envelope up to the roof plate, which overhangs it
            var hs = st.Housing;
            double xFront = bb, xBack = -depth - bb;
            double deck = machineBeamBottom, roofBottom = deck + hs.HeightMm, roofTop = roofBottom + hs.RoofMm;
            w.Rect(xFront - hs.WallMm, deck, xFront, roofBottom);
            w.Rect(xBack, deck, xBack + hs.WallMm, roofBottom);
            w.Rect(xBack - hs.OverhangMm, roofBottom, xFront + hs.OverhangMm, roofTop);
            double stub = 500, zFoot = zBottom - stub;
            double[][] columns = { new[] { bb - col.HeightMm, bb }, new[] { -depth - bb, -depth - bb + col.HeightMm } };
            foreach (var c in columns)
            {
                w.Seg(c[0], zBottom, c[0], zFoot);
                w.Seg(c[1], zBottom, c[1], zFoot);
                w.Seg(c[0], zFoot, c[0] + (c[1] - c[0]) * 0.35, zFoot - 40);
                w.Seg(c[0] + (c[1] - c[0]) * 0.35, zFoot - 40, c[0] + (c[1] - c[0]) * 0.65, zFoot + 40);
                w.Seg(c[0] + (c[1] - c[0]) * 0.65, zFoot + 40, c[1], zFoot);
            }
        }

        // ---------- section A-A (the tower elevation) ----------
        /// <summary>The rail brackets of a steel shaft sit on the beams of the side and back faces: the bracket levels are their levels.</summary>
        public static Elevator.Core.GuideRails.BracketLayout BracketsOnBeams(SteelStructure st, Elevator.Core.Geometry.SectionAAGeometry g, double maximum)
        {
            var levels = new SortedSet<double> { g.PitZ, g.Underside };
            foreach (string face in SteelStructure.BracketFaces)
            {
                foreach (var l in st.Levels[face])
                {
                    if (l.ElevationMm > g.PitZ + 1e-6 && l.ElevationMm < g.Underside - 1e-6)
                        levels.Add(Math.Round(l.ElevationMm, 3));
                }
            }
            var layout = new Elevator.Core.GuideRails.BracketLayout();
            layout.Elevations.AddRange(levels);
            for (int i = 1; i < layout.Elevations.Count; i++)
            {
                double gap = layout.Elevations[i] - layout.Elevations[i - 1];
                layout.ConsecutiveSpacings.Add(gap);
                if (gap > maximum + 1e-7)
                    layout.SpacingStatus = "FAIL";
            }
            layout.Observations.Add("Brackets sit on the beams of the steel tower.");
            return layout;
        }

        static readonly string[] AARoles = { "Line" };
        static readonly string[] AAKeyPrefixes =
        {
            "SectionAA.Shaft.", "SectionAA.Pit.Floor.", "SectionAA.MR.Wall.", "SectionAA.MR.Ceiling", "SectionAA.MR.Roof",
            "SectionAA.MR.LandingSlab."
        };

        /// <summary>The A-A entities the steel tower replaces: the walls, the pit floor, the slabs, the machine-room walls and the coordinated slab.</summary>
        public static bool ReplacedInAA(string key, string role)
        {
            if (key.StartsWith(AAPrefix, StringComparison.Ordinal))
                return false;
            if (AAKeyPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal)))
                return true;
            // the machine-room label and the hoisting hook of the concrete ceiling
            if (key.StartsWith("SectionAA.MR.Hook.", StringComparison.Ordinal) || key == "SectionAA.MR.Room.Label")
                return true;
            if (key.StartsWith("SectionAA.Floor.", StringComparison.Ordinal))
                return key.Contains(".Slab.");
            if (key.StartsWith("SectionAA.MR.Coordinated.", StringComparison.Ordinal))
                return role == "slab" || role == "concrete";
            return false;
        }

        /// <summary>Hides what the tower replaces in A-A (call before the steel entities are added).</summary>
        public static void HideInAA(Database db, Transaction tr)
        {
            foreach (ObjectId id in Model(db, tr, OpenMode.ForRead))
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var tag = Tag(e);
                if (tag == null || !tag[0].StartsWith("SectionAA.", StringComparison.Ordinal) || !e.Visible)
                    continue;
                if (ReplacedInAA(tag[0], tag[1]))
                {
                    e.UpgradeOpen();
                    e.Visible = false;
                }
            }
        }

        /// <summary>
        /// Draws the tower elevation in A-A, seen on the left face: columns, the beams of the face at its levels, the beams of the entrance
        /// and the back faces end-on, the double cross (X) bracing in every panel with its gusset plates, the base plates on the concrete
        /// base and the platform on top. x is the depth axis (front at 0), z the height.
        /// </summary>
        public static void DrawAA(Database db, Transaction tr, ShaftFrame frame, SteelStructure st)
        {
            var w = new Writer(db, tr, AAPrefix, "steel", (u, v) => frame.World(new Point3d(u, v, 0)));
            var face = st.Faces.First(x => x.Name == "Left");
            double len = face.LengthMm;
            var i = st.Inputs;
            // the base block (reinforced concrete) and the base plates
            var block = face.BaseBlock;
            var blockOutline = w.Rect(-block.X1, block.Y0, -block.X0, block.Y1);
            w.Fill(blockOutline, "AR-CONC", 25);
            foreach (var p in face.Plates)
                w.Rect(-p.X1, p.Y0, -p.X0, p.Y1);
            // columns
            foreach (var c in face.Columns)
                w.Rect(-c.X1, c.Y0, -c.X0, c.Y1);
            // the beams of the face: a double line across between the columns
            foreach (var b in face.Beams)
            {
                w.Rect(-b.U1, b.Z0, -b.U0, b.Z1);
            }
            // the beams of the entrance and the back faces seen end-on
            foreach (var other in st.Faces.Where(x => x.Name == "Front" || x.Name == "Back"))
            {
                double bw = i.Beam.WidthMm;
                foreach (var b in other.Beams)
                {
                    if (other.Name == "Front")
                        w.Rect(0, b.Z0, bw, b.Z1);
                    else
                        w.Rect(-len - bw, b.Z0, -len, b.Z1);
                }
            }
            // bracing: both diagonals in every panel, between the beams
            double angle = i.Brace.LegMm;
            foreach (var br in face.Braces)
            {
                w.Strip(0, br.Z0, -len, br.Z1, angle);
                w.Strip(0, br.Z1, -len, br.Z0, angle);
            }
            foreach (var g in face.Gussets.Where(x => x.Y > st.BaseMm + 1e-6))
            {
                double gw = i.GussetWidthMm, gl = i.GussetLengthMm;
                double u0 = g.X < len / 2 ? 0 : len - gw, u1 = u0 + gw;
                w.Rect(-u1, g.Y - gl / 2, -u0, g.Y + gl / 2);
            }
            // the machine housing on the platform: walls on the column envelope, the roof plate overhangs it
            var hs = st.Housing;
            double xFront = i.Beam.WidthMm, xBack = -len - i.Beam.WidthMm;
            w.Rect(xFront - hs.WallMm, hs.DeckMm, xFront, hs.RoofBottomMm);
            w.Rect(xBack, hs.DeckMm, xBack + hs.WallMm, hs.RoofBottomMm);
            w.Rect(xBack - hs.OverhangMm, hs.RoofBottomMm, xFront + hs.OverhangMm, hs.RoofTopMm);
            // the platform on top: a deck line over the top ring beams
            double top = st.TopMm + i.Beam.HeightMm / 2;
            double colDepth = i.Column.HeightMm;
            w.Seg(-len - i.Beam.WidthMm, top, i.Beam.WidthMm, top);
            // labels in the reference style, stacked in the free band right of the tower under the tower top
            double h = ElevationTextHeight;
            double tx = 350, tz = st.TopMm - 700;
            var topBeam = face.Beams.OrderByDescending(b => b.ElevationMm).First();
            w.Label(0, topBeam.ElevationMm, tx, tz, h, i.Beam.Name + " STEEL BEAM");
            w.Label(i.Beam.WidthMm - colDepth / 2, st.TopMm - 1100, tx, tz - 600, h, i.Column.Name + " STEEL COLUMN");
            var braced = face.Braces.OrderByDescending(b => b.Z1).ElementAt(Math.Min(1, face.Braces.Count - 1));
            double bu = -len * 0.12, bv = braced.Z0 + (braced.Z1 - braced.Z0) * 0.12;
            w.Label(bu, bv, tx, tz - 1200, h, i.Brace.Name.Replace("x", "X") + "mm.");        }
    }
}
