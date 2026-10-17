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

        // The layers of the steel tower (DESIGN-FLOW principle 11: meaningful layers). They are plain layers of the drawing's default colour,
        // so the plotted look is the one of the single steel layer before.
        public const string LayerColumn = "S-COLUMN";
        public const string LayerBeam = "S-BEAM";
        public const string LayerBrace = "S-BRACE";
        public const string LayerPlate = "S-PLATE";
        public const string LayerBase = "S-BASE";
        public const string LayerHousing = "S-HOUSING";
        public const string LayerSlab = "A-SLAB";
        public const string LayerAnno = "A-ANNO";
        public const string LayerDims = "A-DIMS";
        public const string LayerEquip = "A-EQUIP";

        internal static void EnsureLayer(Database db, Transaction tr, string name)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name))
                return;
            lt.UpgradeOpen();
            var l = new LayerTableRecord { Name = name, IsPlottable = true };
            lt.Add(l);
            tr.AddNewlyCreatedDBObject(l, true);
        }

        // ---------- named blocks and the leader style ----------
        const string LeaderStyleName = "SM_LEADER";

        // The multileader style of every pointing label: no arrow head, no landing, the text beside the end of the leader.
        internal static ObjectId LeaderStyle(Database db, Transaction tr)
        {
            var dict = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead);
            if (dict.Contains(LeaderStyleName))
                return dict.GetAt(LeaderStyleName);
            var style = new MLeaderStyle();
            style.ContentType = ContentType.MTextContent;
            style.EnableLanding = true;
            style.EnableDogleg = true;
            style.DoglegLength = 0.3 * PlanTextHeight;
            style.ExtendLeaderToText = false;
            style.ArrowSize = 1;
            style.TextHeight = PlanTextHeight;
            style.LandingGap = 0;
            style.TextAttachmentType = TextAttachmentType.AttachmentMiddle;
            dict.UpgradeOpen();
            var id = dict.SetAt(LeaderStyleName, style);
            tr.AddNewlyCreatedDBObject(style, true);
            return id;
        }

        // A named block, created once from the geometry the callback puts into it (local coordinates, origin at the insertion point).
        internal static ObjectId Block(Database db, Transaction tr, string name, Action<BlockTableRecord> fill)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (bt.Has(name))
                return bt[name];
            bt.UpgradeOpen();
            var btr = new BlockTableRecord { Name = name };
            var id = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);
            fill(btr);
            return id;
        }

        internal static T Put<T>(BlockTableRecord btr, Transaction tr, T e, string layer) where T : Entity
        {
            EnsureLayer(btr.Database, tr, layer);
            e.Layer = layer;
            btr.AppendEntity(e);
            tr.AddNewlyCreatedDBObject(e, true);
            return e;
        }

        internal static string Clean(string name)
        {
            return new string(name.Select(c => Char.IsLetterOrDigit(c) ? c : '_').ToArray());
        }

        // The channel section of a column in plan, origin at the lower left corner of its box: the closed outline and its solid fill. The
        // web is at the left edge for the left columns (flanges to the right) and at the right edge for the right ones (_R).
        static ObjectId ColumnBlock(Database db, Transaction tr, ChannelProfile c, bool webRight)
        {
            string name = "SM_COLUMN_" + Clean(c.Name) + "_" + L(c.WidthMm) + "x" + L(c.HeightMm) + "x" + L(c.WebMm) + "x" + L(c.FlangeMm)
                + (webRight ? "_R" : "_L");
            return Block(db, tr, name, btr =>
            {
                double b = c.WidthMm, h = c.HeightMm, tw = c.WebMm, tf = c.FlangeMm;
                var pl = new Polyline();
                double[][] pts = { new[] { 0.0, 0 }, new[] { b, 0 }, new[] { b, tf }, new[] { tw, tf }, new[] { tw, h - tf }, new[] { b, h - tf }, new[] { b, h }, new[] { 0.0, h } };
                if (webRight)
                    pts = pts.Select(q => new[] { b - q[0], q[1] }).Reverse().ToArray();
                for (int i = 0; i < pts.Length; i++)
                    pl.AddVertexAt(i, new Point2d(pts[i][0], pts[i][1]), 0, 0, 0);
                pl.Closed = true;
                Put(btr, tr, pl, LayerColumn);
                var hatch = new Hatch();
                hatch.SetDatabaseDefaults(db);
                Put(btr, tr, hatch, LayerColumn);
                hatch.Normal = Vector3d.ZAxis;
                hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                hatch.Associative = true;
                hatch.AppendLoop(HatchLoopTypes.Default, new ObjectIdCollection(new[] { pl.ObjectId }));
                hatch.EvaluateHatch(true);
            });
        }

        // The base plate with its bolt circles, origin at the plate centre.
        static ObjectId PlateBlock(Database db, Transaction tr, SteelInputs i, IList<SteelCircle> bolts, double cx, double cy)
        {
            string name = "SM_BASE_PLATE_" + L(i.PlateWidthMm) + "x" + L(i.PlateDepthMm) + "x" + L(i.PlateThicknessMm) + "_" + bolts.Count + "B" + L(i.BoltDiameterMm);
            return Block(db, tr, name, btr =>
            {
                double hw = i.PlateWidthMm / 2, hd = i.PlateDepthMm / 2;
                var pl = new Polyline();
                double[][] pts = { new[] { -hw, -hd }, new[] { hw, -hd }, new[] { hw, hd }, new[] { -hw, hd } };
                for (int k = 0; k < 4; k++)
                    pl.AddVertexAt(k, new Point2d(pts[k][0], pts[k][1]), 0, 0, 0);
                pl.Closed = true;
                Put(btr, tr, pl, LayerPlate);
                foreach (var bolt in bolts)
                    Put(btr, tr, new Circle(new Point3d(bolt.X - cx, bolt.Y - cy, 0), Vector3d.ZAxis, bolt.DiameterMm / 2), LayerPlate);
            });
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
            readonly double angle;
            int count;

            /// <summary>The layer of the entities added next (see the S- and A- layers).</summary>
            public string Layer = LayerAnno;

            public Writer(Database db, Transaction tr, string prefix, string role, Func<double, double, Point3d> world)
            {
                this.db = db;
                this.tr = tr;
                this.prefix = prefix;
                this.role = role;
                this.world = world;
                var o = world(0, 0);
                unit = world(1, 0) - o;
                angle = Math.Atan2(unit.Y, unit.X);
                ComponentBinding.Register(db, tr);
                model = Model(db, tr, OpenMode.ForWrite);
            }

            public int Count
            {
                get { return count; }
            }

            public Database Database
            {
                get { return db; }
            }

            public Transaction Transaction
            {
                get { return tr; }
            }

            T Add<T>(T e) where T : Entity
            {
                count++;
                if (e.Layer == "0" || String.IsNullOrEmpty(e.Layer) || e.Layer == LayerName)
                {
                    EnsureLayer(db, tr, Layer);
                    e.Layer = Layer;
                }
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

            /// <summary>Adds an entity built by the caller (on the current layer unless it has its own, and tagged).</summary>
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
                string own = source.Layer;
                e = Add(e);
                e.Layer = own;      // a copy keeps the layer of the entity it copies
                return e;
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

            /// <summary>An insertion of a named block at a local point, mirrored in x when scaleX is -1.</summary>
            public BlockReference Insert(ObjectId block, double u, double v, double scaleX, double extraRotation)
            {
                var br = new BlockReference(world(u, v), block);
                br.ScaleFactors = new Scale3d(scaleX, 1, 1);
                br.Rotation = angle + extraRotation;
                return Add(br);
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
                h.Associative = true;
                h.AppendLoop(HatchLoopTypes.Default, new ObjectIdCollection(new[] { boundary.ObjectId }));
                h.EvaluateHatch(true);
            }

            /// <summary>A label: one multileader (SM_LEADER) from (u0, v0) to its text, which starts beside (u1, v1).</summary>
            public void Label(double u0, double v0, double u1, double v1, double height, string text)
            {
                Pointer(u0, v0, u1, v1, height, text, false);
            }

            /// <summary>A label whose text ends at the end of its leader (the leader goes to the left of the member).</summary>
            public void LabelLeft(double u0, double v0, double u1, double v1, double height, string text)
            {
                Pointer(u0, v0, u1, v1, height, text, true);
            }

            void Pointer(double u0, double v0, double u1, double v1, double height, string text, bool textLeft)
            {
                // The leader runs from the member to (u1, v1), then a short landing of 0.3 text heights in the direction of the text,
                // which stands at the end of the landing (middle left of the text for a leader coming from the left, middle right
                // otherwise): AutoCAD places the text itself from the landing, so a label stays one editable object.
                double gap = 0.3 * height;
                var ml = new MLeader();
                ml.SetDatabaseDefaults(db);
                ml.MLeaderStyle = LeaderStyle(db, tr);
                ml.ContentType = ContentType.MTextContent;
                var mt = new MText();
                mt.SetDatabaseDefaults(db);
                mt.Contents = text;
                mt.TextHeight = height;
                mt.Attachment = AttachmentPoint.MiddleLeft;
                var dir = new Vector3d(textLeft ? -unit.X : unit.X, textLeft ? -unit.Y : unit.Y, 0);
                var landingEnd = world(u1, v1) + dir * gap;
                // AutoCAD takes the text location as the near edge of the text frame on the leader's side: for a text left of its leader
                // that is the left edge, one text width further out
                var up = world(0, 1) - world(0, 0);
                // and AutoCAD takes the location as the top of the frame: half a text height up puts the landing at (u1, v1)
                var textAt = (textLeft ? landingEnd + dir * (text.Length * height * CharFactor) : landingEnd) + up * (height / 2);
                mt.Location = textAt;
                ml.MText = mt;
                ml.TextHeight = height;
                ml.EnableLanding = true;
                ml.EnableDogleg = true;
                ml.DoglegLength = gap;
                ml.LandingGap = 0;
                ml.SetTextAttachmentType(TextAttachmentType.AttachmentMiddle, LeaderDirectionType.LeftLeader);
                ml.SetTextAttachmentType(TextAttachmentType.AttachmentMiddle, LeaderDirectionType.RightLeader);
                ml.ArrowSize = 1;
                ml.ExtendLeaderToText = false;
                int line = ml.AddLeaderLine(world(u0, v0));
                ml.AddLastVertex(line, world(u1, v1));
                ml.SetDogleg(0, dir);
                ml.TextLocation = textAt;
                Layer = LayerAnno;
                Add(ml);
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

        // Width of a character of the steel labels as a fraction of their height (measured on the generated sheets: about 0.97).
        internal const double CharFactor = 1.0;

        // ---------- label placement (no text on text) ----------
        /// <summary>
        /// The texts of a drawing (text, multi-line text and the texts inside the dimension blocks) and the outlines a label must not lie
        /// on, in the local frame of a view. Labels are placed with the same no-overlap rule as the dimension rows: a label takes the
        /// nearest position (leader length and offset) whose text box is clear of every text and outline by a gap and whose leader crosses
        /// no text.
        /// </summary>
        internal sealed class Obstacles
        {
            /// <summary>An empty set of obstacles (a detail that is not a view of the shaft): the caller adds its own texts and outlines.</summary>
            internal Obstacles()
            {
            }

            readonly List<double[]> texts = new List<double[]>();
            readonly List<string> names = new List<string>();
            string current = "";
            readonly List<double[]> outlines = new List<double[]>();
            // the dimension lines, extension lines and the axes of the view: a label text box may not touch one (its leader may cross)
            readonly List<double[]> segments = new List<double[]>();
            readonly ShaftFrame frame;
            // the leaders already placed (a new leader may not cross one of them)
            readonly List<double[]> leaders = new List<double[]>();

            // Only the entities of the view the labels belong to (the views stand on top of each other until they are arranged).
            public Obstacles(Database db, Transaction tr, ShaftFrame frame, params string[] viewPrefixes)
            {
                this.frame = frame;
                foreach (ObjectId id in Model(db, tr, OpenMode.ForRead))
                {
                    var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (e == null || !e.Visible || e.Layer == HiddenLayer)
                        continue;
                    var tg = Tag(e);
                    if (tg == null || !viewPrefixes.Any(v => tg[0].StartsWith(v, StringComparison.Ordinal)))
                        continue;
                    current = tg[0];
                    if (e is Line && (tg[1] == "mainAxis" || tg[1] == "carAxis" || tg[1] == "cwAxis"))
                    {
                        var axis = (Line)e;
                        AddSegment(axis.StartPoint, axis.EndPoint);
                    }
                    if (e is MText || e is DBText)
                        AddWorld(e);
                    else
                    {
                        var d = e as Dimension;
                        if (d == null)
                            continue;
                        int before = texts.Count;
                        if (!d.DimBlockId.IsNull)
                        {
                            var block = tr.GetObject(d.DimBlockId, OpenMode.ForRead) as BlockTableRecord;
                            if (block != null)
                            {
                                foreach (ObjectId bid in block)
                                {
                                    var t = tr.GetObject(bid, OpenMode.ForRead) as Entity;
                                    if (t is MText || t is DBText)
                                        AddWorld(t);
                                }
                            }
                        }
                        AddDimensionText(d);
                        AddDimensionLines(d);
                    }
                }
            }

            internal void AddSegment(double x0, double y0, double x1, double y1)
            {
                segments.Add(new[] { x0, y0, x1, y1 });
            }

            void AddSegment(Point3d a, Point3d b)
            {
                var p = frame.Local(a);
                var q = frame.Local(b);
                segments.Add(new[] { p.X, p.Y, q.X, q.Y });
            }

            // The dimension line and the two extension lines of a rotated dimension, from its definition points.
            void AddDimensionLines(Dimension d)
            {
                var r = d as RotatedDimension;
                var al = d as AlignedDimension;
                Point3d p1, p2, dp;
                double c, s;
                if (r != null)
                {
                    c = Math.Cos(r.Rotation);
                    s = Math.Sin(r.Rotation);
                    p1 = r.XLine1Point;
                    p2 = r.XLine2Point;
                    dp = r.DimLinePoint;
                }
                else if (al != null)
                {
                    var v = al.XLine2Point - al.XLine1Point;
                    if (v.Length < 1e-9)
                        return;
                    c = v.X / v.Length;
                    s = v.Y / v.Length;
                    p1 = al.XLine1Point;
                    p2 = al.XLine2Point;
                    dp = al.DimLinePoint;
                }
                else
                    return;
                var n = new Vector3d(-s, c, 0);
                var feet = new List<Point3d>();
                foreach (var x in new[] { p1, p2 })
                {
                    double off = (dp - x).DotProduct(n);
                    var foot = x + n * off;
                    feet.Add(foot);
                    AddSegment(x, foot);
                }
                AddSegment(feet[0], feet[1]);
            }

            // The dimension block is not there yet (the dimension was just written): the text box is estimated from its position, its
            // string and the direction of its dimension line.
            void AddDimensionText(Dimension d)
            {
                string t = String.IsNullOrEmpty(d.DimensionText) || d.DimensionText == "<>"
                    ? Math.Round(d.Measurement).ToString(CultureInfo.InvariantCulture)
                    : d.DimensionText.Replace("<>", Math.Round(d.Measurement).ToString(CultureInfo.InvariantCulture));
                double h = Math.Max(d.Dimtxt, 40);
                double w = t.Length * 0.75 * h, hh = 1.4 * h;
                double angle = Math.Abs(d.TextRotation) > 1e-9 ? d.TextRotation : (d is RotatedDimension ? ((RotatedDimension)d).Rotation : 0);
                bool vertical = Math.Abs(Math.Sin(angle)) > 0.7;
                var c = frame.Local(d.TextPosition);
                double hw = (vertical ? hh : w) / 2, hhh = (vertical ? w : hh) / 2;
                texts.Add(new[] { c.X - hw, c.Y - hhh, c.X + hw, c.Y + hhh });
                names.Add(current + "(est)");
            }

            void AddWorld(Entity e)
            {
                try
                {
                    var b = e.GeometricExtents;
                    var a = frame.Local(b.MinPoint);
                    var c = frame.Local(b.MaxPoint);
                    texts.Add(new[] { Math.Min(a.X, c.X), Math.Min(a.Y, c.Y), Math.Max(a.X, c.X), Math.Max(a.Y, c.Y) });
                    names.Add(current);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                }
            }

            /// <summary>An outline (a rectangle) a label must not lie on.</summary>
            public void AddOutline(double x0, double y0, double x1, double y1)
            {
                const double t = 2;
                outlines.Add(new[] { x0, y0 - t, x1, y0 + t });
                outlines.Add(new[] { x0, y1 - t, x1, y1 + t });
                outlines.Add(new[] { x0 - t, y0, x0 + t, y1 });
                outlines.Add(new[] { x1 - t, y0, x1 + t, y1 });
            }

            public int Count
            {
                get { return texts.Count; }
            }

            public string Dump(double xMax)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < texts.Count; i++)
                {
                    var t = texts[i];
                    if (t[2] < xMax && t[0] > -4000 && t[3] > 1000 && t[1] < 1800 && t[0] < -600)
                        sb.Append(" [" + t[0].ToString("0") + "," + t[1].ToString("0") + "," + t[2].ToString("0") + "," + t[3].ToString("0") + " " + names[i] + "]");
                }
                return sb.ToString();
            }

            internal void AddLeader(double x0, double y0, double x1, double y1)
            {
                leaders.Add(new[] { x0, y0, x1, y1 });
            }

            static bool SegmentsCross(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
            {
                double d1 = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax), d2 = (bx - ax) * (dy - ay) - (by - ay) * (dx - ax);
                double d3 = (dx - cx) * (ay - cy) - (dy - cy) * (ax - cx), d4 = (dx - cx) * (by - cy) - (dy - cy) * (bx - cx);
                return d1 * d2 < 0 && d3 * d4 < 0;
            }

            public void AddText(double[] box)
            {
                names.Add("label");
                texts.Add(box);
            }

            static bool Hit(double[] a, double[] b, double gap)
            {
                return a[0] < b[2] + gap && b[0] < a[2] + gap && a[1] < b[3] + gap && b[1] < a[3] + gap;
            }

            /// <summary>True when the text box lies on no text (within the gap) and no outline.</summary>
            public bool BoxClear(double[] box, double gap)
            {
                foreach (var t in texts)
                    if (Hit(box, t, gap))
                        return false;
                foreach (var o in outlines)
                    if (Hit(box, o, 0))
                        return false;
                foreach (var g in segments)
                    if (SegmentHits(g[0], g[1], g[2], g[3], box[0] - 3, box[1] - 3, box[2] + 3, box[3] + 3))
                        return false;
                return true;
            }

            /// <summary>True when the segment crosses no text.</summary>
            public bool LeaderClear(double x0, double y0, double x1, double y1)
            {
                foreach (var l in leaders)
                {
                    if (SegmentsCross(x0, y0, x1, y1, l[0], l[1], l[2], l[3]))
                        return false;
                }
                foreach (var t in texts)
                {
                    if (SegmentHits(x0, y0, x1, y1, t[0], t[1], t[2], t[3]))
                        return false;
                }
                return true;
            }

            static bool SegmentHits(double x0, double y0, double x1, double y1, double bx0, double by0, double bx1, double by1)
            {
                double dx = x1 - x0, dy = y1 - y0, t0 = 0, t1 = 1;
                double[] p = { -dx, dx, -dy, dy };
                double[] q = { x0 - bx0, bx1 - x0, y0 - by0, by1 - y0 };
                for (int i = 0; i < 4; i++)
                {
                    if (Math.Abs(p[i]) < 1e-12)
                    {
                        if (q[i] < 0)
                            return false;
                    }
                    else
                    {
                        double r = q[i] / p[i];
                        if (p[i] < 0)
                        {
                            if (r > t1)
                                return false;
                            t0 = Math.Max(t0, r);
                        }
                        else
                        {
                            if (r < t0)
                                return false;
                            t1 = Math.Min(t1, r);
                        }
                    }
                }
                return t0 < t1 - 1e-9;
            }
        }

        // A label with a leader. The anchors are the points of the member the leader may start from (the preferred one first); for
        // each, the candidate positions are leader lengths from len0 and offsets across from dy0; the cheapest candidate whose text
        // box is clear of every text and outline and whose leader crosses no text wins. The text stands at the end of the leader (to its
        // left for toLeft, otherwise to its right).
        internal static void PlaceLabel(Writer w, Obstacles ob, IList<double[]> anchors, bool toLeft, double h, string text, double len0, double dy0)
        {
            double width = text.Length * CharFactor * h, height = 1.4 * h, gap = 0.25 * h;
            var steps = new List<double[]>();
            for (int k = 0; k <= 40; k++)
            {
                for (int j = 0; j <= 32; j++)
                {
                    foreach (int sign in new[] { 1, -1 })
                    {
                        if (j == 0 && sign == -1)
                            continue;
                        double dy = dy0 + sign * j * 50;
                        steps.Add(new[] { k * 100 + 2 * Math.Abs(dy - dy0), len0 + k * 100, dy });
                    }
                }
            }
            steps.Sort((a, b) => a[0].CompareTo(b[0]));
            double bestCost = double.MaxValue;
            double[] best = null;
            double bax = 0, bay = 0;
            for (int ai = 0; ai < anchors.Count; ai++)
            {
                double ax = anchors[ai][0], ay = anchors[ai][1];
                double penalty = ai * 150;           // the preferred anchor is kept unless another one is clearly better
                double minAcross = anchors[ai].Length > 2 ? anchors[ai][2] : 0;
                foreach (var c in steps)
                {
                    if (c[0] + penalty >= bestCost)
                        break;
                    if (Math.Abs(c[2] - dy0) < minAcross)
                        continue;
                    double ex = toLeft ? ax - c[1] : ax + c[1], ey = ay + c[2];
                    double[] box = toLeft
                        ? new[] { ex - 0.3 * h - width, ey - height / 2, ex - 0.3 * h, ey + height / 2 }
                        : new[] { ex + 0.3 * h, ey - height / 2, ex + 0.3 * h + width, ey + height / 2 };
                    if (ob.BoxClear(box, gap) && ob.LeaderClear(ax, ay, ex, ey))
                    {
                        bestCost = c[0] + penalty;
                        best = c;
                        bax = ax;
                        bay = ay;
                        break;
                    }
                }
            }
            if (best == null)
            {
                best = new[] { 0, len0, dy0 };
                bax = anchors[0][0];
                bay = anchors[0][1];
            }
            double endX = toLeft ? bax - best[1] : bax + best[1], endY = bay + best[2];
            ob.AddLeader(bax, bay, endX, endY);
            if (toLeft)
            {
                w.LabelLeft(bax, bay, endX, endY, h, text);
                ob.AddText(new[] { endX - 0.3 * h - width, endY - height / 2, endX - 0.3 * h, endY + height / 2 });
            }
            else
            {
                w.Label(bax, bay, endX, endY, h, text);
                ob.AddText(new[] { endX + 0.3 * h, endY - height / 2, endX + 0.3 * h + width, endY + height / 2 });
            }
        }

        static double[][] Along(double x, double y0, double y1, params double[] fractions)
        {
            // an anchor at an end of the member keeps its leader off the member's own outline: at least 100 mm across
            return fractions.Select(f => f <= 0 || f >= 1 ? new[] { x, y0 + (y1 - y0) * f, 100.0 } : new[] { x, y0 + (y1 - y0) * f }).ToArray();
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
            var ob = new Obstacles(db, tr, f, "Plan.");
            ob.AddOutline(st.SlabEdge.X0, st.SlabEdge.Y0, st.SlabEdge.X1, st.SlabEdge.Y1);
            PlanTower(w, ob, st, st.Plan.Columns, st.Plan.Beams, true);
            w.Layer = LayerSlab;
            w.Rect(st.SlabEdge.X0, st.SlabEdge.Y0, st.SlabEdge.X1, st.SlabEdge.Y1);
        }

        // The columns (solid sections), the base plates with their bolts and the beam ring, in plan; labels in the style of the guide-rail
        // labels of the plan.
        static void PlanTower(Writer w, Obstacles ob, SteelStructure st, IList<SteelColumnPlan> columns, IList<SteelBeamPlan> beams, bool plates)
        {
            var colLeft = ColumnBlock(w.Database, w.Transaction, st.Inputs.Column, false);
            var colRight = ColumnBlock(w.Database, w.Transaction, st.Inputs.Column, true);
            w.Layer = LayerBeam;
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
                // the channel section: one block reference (a right column is the mirrored block)
                w.Layer = LayerColumn;
                w.Insert(c.WebSide == "Left" ? colLeft : colRight, c.Box.X0, c.Box.Y0, 1, 0);
                if (plates)
                {
                    // the base plate with its bolts: one block reference at the plate centre
                    w.Layer = LayerPlate;
                    var plateBlock = PlateBlock(w.Database, w.Transaction, st.Inputs, c.Bolts, c.Plate.CenterX, c.Plate.CenterY);
                    w.Insert(plateBlock, c.Plate.CenterX, c.Plate.CenterY, 1, 0);
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
            PlaceLabel(w, ob, Along(right.Box.X1, right.Box.Y0, right.Box.Y1, 0.25, 0.5, 0.75, 0.1, 0.9), false, PlanTextHeight, beamText, 150, 0);
            if (plates)
            {
                // shaft plan: the columns at the left corners, the beam of the left face
                PlaceLabel(w, ob, Along(fl.Box.X0, fl.Box.Y0, fl.Box.Y1, 0.5, 0.2, 0.8, 0.0, 1.0), true, PlanTextHeight, columnText, 800, 0);
                PlaceLabel(w, ob, Along(bl.Box.X0, bl.Box.Y0, bl.Box.Y1, 0.5, 0.2, 0.8, 1.0, 0.0), true, PlanTextHeight, columnText, 800, 0);
                PlaceLabel(w, ob, Along(left.Box.X0, left.Box.Y0, left.Box.Y1, 0.5, 0.35, 0.65, 0.2, 0.8, 0.1, 0.9), true, PlanTextHeight, beamText, 560, 0);
            }
            else
            {
                // tower-top plan: the two front columns, leaders running down and outwards
                PlaceLabel(w, ob, new[] { new[] { fl.Box.X0, fl.Box.Y0 } }, true, PlanTextHeight, columnText, 500, -500);
                PlaceLabel(w, ob, new[] { new[] { fr.Box.X1, fr.Box.Y0 } }, false, PlanTextHeight, columnText, 500, -500);
            }
            if (plates)
            {
                var plate = br.Plate;
                PlaceLabel(w, ob, new[] { new[] { plate.X1, plate.Y1 } }, false, PlanTextHeight,
                    "BASE PL " + L(st.Inputs.PlateWidthMm) + "X" + L(st.Inputs.PlateDepthMm) + "X" + L(st.Inputs.PlateThicknessMm) + "MM., "
                    + st.Inputs.BoltCount + " BOLTS " + L(st.Inputs.BoltDiameterMm) + "MM.", 150, 150);
            }
        }

        internal static string L(double v)
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
            var ob = new Obstacles(db, tr, frame, "MachineRoom.", "Steel.MachineRoom.");
            ob.AddOutline(st.SlabEdge.X0, st.SlabEdge.Y0, st.SlabEdge.X1, st.SlabEdge.Y1);
            PlanTower(w, ob, st, top.Columns, top.Beams, false);
            w.Layer = LayerSlab;
            w.Rect(st.SlabEdge.X0, st.SlabEdge.Y0, st.SlabEdge.X1, st.SlabEdge.Y1);
            double lowest = DrawController(db, tr, frame, st, top, ob);
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
            EnsureLayer(db, tr, LayerDims);
            dim.Layer = LayerDims;
            w.Adopt(dim);
        }

        const string ControllerPrefix = "Steel.MachineRoom.Controller.";

        // The control panel of the concrete machine room (the same drawing symbol, copied) beside the tower top on the door side, right
        // of the door as seen from the landing, with its short label.
        static double DrawController(Database db, Transaction tr, ShaftFrame frame, SteelStructure st, SteelTowerTop top, Obstacles ob)
        {
            var symbol = new List<Polyline>();
            var origin0 = frame.World(new Point3d(0, 0, 0));
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
            var w = new Writer(db, tr, ControllerPrefix, "controller", (u, v) => frame.World(new Point3d(u, v, 0)));
            // the symbol of the master as one named block (origin at its lower left corner), inserted turned a quarter
            var panel = Block(db, tr, "SM_CONTROL_PANEL", btr =>
            {
                foreach (var e in symbol)
                {
                    var pl = new Polyline();
                    for (int k = 0; k < e.NumberOfVertices; k++)
                    {
                        var v = frame.Local(e.GetPoint3dAt(k));
                        pl.AddVertexAt(k, new Point2d(v.X - minX, v.Y - minY), e.GetBulgeAt(k), 0, 0);
                    }
                    pl.Closed = e.Closed;
                    Put(btr, tr, pl, LayerEquip);
                }
            });
            w.Layer = LayerEquip;
            w.Insert(panel, x0, y0 + heightR, 1, -Math.PI / 2);
            // the label stands left of the symbol with a leader to it, clear of the column labels on the right
            PlaceLabel(w, ob, new[] { new[] { x0 + widthR * 0.3, y0 + heightR * 0.2 } }, true, PlanTextHeight, "CONTROL PANEL", 300 + widthR * 0.3, -120 - heightR * 0.2);
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
            w.Layer = LayerHousing;
            double xFront = bb, xBack = -depth - bb;
            double deck = machineBeamBottom, roofBottom = deck + hs.HeightMm, roofTop = roofBottom + hs.RoofMm;
            w.Rect(xFront - hs.WallMm, deck, xFront, roofBottom);
            w.Rect(xBack, deck, xBack + hs.WallMm, roofBottom);
            w.Rect(xBack - hs.OverhangMm, roofBottom, xFront + hs.OverhangMm, roofTop);
            double stub = 500, zFoot = zBottom - stub;
            w.Layer = LayerColumn;
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
            w.Layer = LayerBase;
            var blockOutline = w.Rect(-block.X1, block.Y0, -block.X0, block.Y1);
            w.Fill(blockOutline, "AR-CONC", 25);
            w.Layer = LayerPlate;
            foreach (var p in face.Plates)
                w.Rect(-p.X1, p.Y0, -p.X0, p.Y1);
            // columns
            w.Layer = LayerColumn;
            foreach (var c in face.Columns)
                w.Rect(-c.X1, c.Y0, -c.X0, c.Y1);
            // the beams of the face: a double line across between the columns
            w.Layer = LayerBeam;
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
            w.Layer = LayerBrace;
            foreach (var br in face.Braces)
            {
                w.Strip(0, br.Z0, -len, br.Z1, angle);
                w.Strip(0, br.Z1, -len, br.Z0, angle);
            }
            w.Layer = LayerPlate;
            foreach (var g in face.Gussets.Where(x => x.Y > st.BaseMm + 1e-6))
            {
                double gw = i.GussetWidthMm, gl = i.GussetLengthMm;
                double u0 = g.X < len / 2 ? 0 : len - gw, u1 = u0 + gw;
                w.Rect(-u1, g.Y - gl / 2, -u0, g.Y + gl / 2);
            }
            // the machine housing on the platform: walls on the column envelope, the roof plate overhangs it
            var hs = st.Housing;
            w.Layer = LayerHousing;
            double xFront = i.Beam.WidthMm, xBack = -len - i.Beam.WidthMm;
            w.Rect(xFront - hs.WallMm, hs.DeckMm, xFront, hs.RoofBottomMm);
            w.Rect(xBack, hs.DeckMm, xBack + hs.WallMm, hs.RoofBottomMm);
            w.Rect(xBack - hs.OverhangMm, hs.RoofBottomMm, xFront + hs.OverhangMm, hs.RoofTopMm);
            // the platform on top: a deck line over the top ring beams
            double top = st.TopMm + i.Beam.HeightMm / 2;
            double colDepth = i.Column.HeightMm;
            w.Layer = LayerBeam;
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
