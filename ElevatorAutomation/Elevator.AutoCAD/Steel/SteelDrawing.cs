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
        public static void Hide(Database db, Transaction tr, string keyPrefix, ICollection<string> roles, ICollection<string> keys = null)
        {
            foreach (ObjectId id in Model(db, tr, OpenMode.ForRead))
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var tag = Tag(e);
                if (tag == null || !tag[0].StartsWith(keyPrefix, StringComparison.Ordinal) || !e.Visible)
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
        }

        /// <summary>Draws the steel shaft in the plan (call after the plan update). The template walls are hidden.</summary>
        public static void DrawPlan(Database db, Transaction tr, DrawingSnapshot s, SteelStructure st)
        {
            Hide(db, tr, "Plan.", PlanRoles);
            var f = s.Frame;
            var w = new Writer(db, tr, PlanPrefix, "steel", (u, v) => f.World(new Point3d(u, v, 0)));
            PlanTower(w, st, st.Plan.Columns, st.Plan.Beams, true);
        }

        // The columns (solid sections), the base plates with their bolts and the beam ring, in plan; labels in the style of the guide-rail
        // labels of the plan.
        static void PlanTower(Writer w, SteelStructure st, IList<SteelColumnPlan> columns, IList<SteelBeamPlan> beams, bool plates)
        {
            foreach (var b in beams)
            {
                w.Rect(b.Box.X0, b.Box.Y0, b.Box.X1, b.Box.Y1);
                // the web: a line on the outer face of the channel
                var c = b.CenterLine;
                if (Math.Abs(c.Y0 - c.Y1) < 1e-6)
                {
                    double outer = c.Y0 < 0 ? b.Box.Y0 + st.Inputs.Beam.WebMm : b.Box.Y1 - st.Inputs.Beam.WebMm;
                    w.Seg(b.Box.X0, outer, b.Box.X1, outer);
                }
                else
                {
                    double outer = c.X0 < 0 ? b.Box.X0 + st.Inputs.Beam.WebMm : b.Box.X1 - st.Inputs.Beam.WebMm;
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
            var br = columns.First(c => c.Corner == "BackRight");
            // the labels sit clear of the dimension rows: the column to the left of the plan, the beam to its right
            w.LabelLeft(fl.Box.X0, fl.Box.CenterY, fl.Box.X0 - 800, fl.Box.CenterY, PlanTextHeight, st.Inputs.Column.Name + " STEEL COLUMN");
            var right = beams.First(b => b.Face == "Right");
            double ly = right.Box.Y0 + (right.Box.Y1 - right.Box.Y0) * 0.25;
            w.Label(right.Box.X1, ly, right.Box.X1 + 500, ly, PlanTextHeight, st.Inputs.Beam.Name + " STEEL BEAM");
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

        /// <summary>Clears the steel entities of the machine-room plan and shows the room again.</summary>
        public static void ClearMachineRoom(Database db, Transaction tr)
        {
            Clear(db, tr, MachineRoomPrefix, "MachineRoom.", MachineRoomRoles, MachineRoomKeys);
        }

        /// <summary>Draws the top of the tower in place of the machine room (call after the machine-room update).</summary>
        public static void DrawMachineRoom(Database db, Transaction tr, ShaftFrame frame, SteelStructure st)
        {
            Hide(db, tr, "MachineRoom.", MachineRoomRoles, MachineRoomKeys);
            var w = new Writer(db, tr, MachineRoomPrefix, "MachineRoom", (u, v) => frame.World(new Point3d(u, v, 0)));
            var top = st.TowerTop;
            PlanTower(w, st, top.Columns, top.Beams, false);
            // under the tower, clear of the dimensions and the labels
            double x = top.Outline.X0, y = top.Outline.Y0 - 450;
            w.Text(x, y, PlanTextHeight, "CONTROLLER AND SWITCHES AT THE TOP FLOOR,");
            w.Text(x, y - PlanTextHeight * 1.6, PlanTextHeight, "RIGHT OF THE DOOR AS SEEN FROM THE LANDING");
        }

        // ---------- section B-B (the machine on the steel top frame) ----------
        static readonly string[] SectionRoles = { "slab", "concrete" };

        /// <summary>Clears the steel entities of B-B and shows the slab and the concrete blocks again.</summary>
        public static void ClearSection(Database db, Transaction tr)
        {
            Clear(db, tr, SectionPrefix, "MachineRoomSection.", SectionRoles);
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
            Hide(db, tr, "MachineRoomSection.", SectionRoles);
            var w = new Writer(db, tr, SectionPrefix, "MachineRoomSection", (u, v) => frame.World(new Point3d(u, v, 0)));
            double depth = st.Inputs.DepthMm;
            var beam = st.Inputs.Beam;
            var col = st.Inputs.Column;
            double zTop = machineBeamBottom, zBottom = machineBeamBottom - beam.HeightMm;
            // front beam: x 0..flange width, web on the outside (+x); back beam mirrored about the back face (x = -depth)
            ChannelSection(w, beam.WidthMm, 0, zBottom, zTop, beam.WebMm, beam.FlangeMm);
            ChannelSection(w, -depth - beam.WidthMm, -depth, zBottom, zTop, beam.WebMm, beam.FlangeMm);
            // column stubs below the beams with a break
            double stub = 500, zFoot = zBottom - stub;
            double[][] columns = { new[] { 0.0, col.HeightMm }, new[] { -depth - col.HeightMm, -depth } };
            foreach (var c in columns)
            {
                w.Seg(c[0], zBottom, c[0], zFoot);
                w.Seg(c[1], zBottom, c[1], zFoot);
                w.Seg(c[0], zFoot, c[0] + (c[1] - c[0]) * 0.35, zFoot - 40);
                w.Seg(c[0] + (c[1] - c[0]) * 0.35, zFoot - 40, c[0] + (c[1] - c[0]) * 0.65, zFoot + 40);
                w.Seg(c[0] + (c[1] - c[0]) * 0.65, zFoot + 40, c[1], zFoot);
            }
            w.Label(col.HeightMm / 2, zFoot + stub / 2, col.HeightMm + 250, zFoot + stub / 2, ElevationTextHeight / 1.5, col.Name + " STEEL COLUMN");
            w.LabelLeft(-depth - beam.WidthMm, zTop - beam.HeightMm / 2, -depth - 700, zTop - beam.HeightMm / 2 - 300, ElevationTextHeight / 1.5, beam.Name + " STEEL BEAM");
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
            foreach (var g in face.Gussets)
            {
                double gw = i.GussetWidthMm, gl = i.GussetLengthMm;
                double u0 = g.X < len / 2 ? 0 : len - gw, u1 = u0 + gw;
                w.Rect(-u1, g.Y - gl / 2, -u0, g.Y + gl / 2);
            }
            // the platform on top: a deck line over the top ring beams
            double top = st.TopMm + i.Beam.HeightMm / 2;
            double colDepth = i.Column.HeightMm;
            w.Seg(-len - colDepth, top, colDepth, top);
            // labels in the reference style, stacked in the free band right of the tower under the tower top
            double h = ElevationTextHeight;
            double tx = 350, tz = st.TopMm - 700;
            var topBeam = face.Beams.OrderByDescending(b => b.ElevationMm).First();
            w.Label(0, topBeam.ElevationMm, tx, tz, h, i.Beam.Name + " STEEL BEAM");
            w.Label(colDepth / 2, st.TopMm - 1100, tx, tz - 600, h, i.Column.Name + " STEEL COLUMN");
            var braced = face.Braces.OrderByDescending(b => b.Z1).ElementAt(Math.Min(1, face.Braces.Count - 1));
            double bu = -len * 0.12, bv = braced.Z0 + (braced.Z1 - braced.Z0) * 0.12;
            w.Label(bu, bv, tx, tz - 1200, h, i.Brace.Name.Replace("x", "X") + "mm.");        }
    }
}
