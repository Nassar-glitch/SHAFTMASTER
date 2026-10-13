using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.AutoCAD.Common;
using Elevator.Core.Doors;
using Elevator.Core.Geometry;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Plan
{
    /// <summary>
    /// Automatic doors (2S, 2C) in the native plan (D2). The manual door geometry of the template is hidden (never edited: the
    /// template entities stay bound and keep following the shared plan) and replaced by parametric geometry from the
    /// <see cref="DoorLayout"/>: car door panels (closed), car sill line, landing frame with jamb and stacking pocket(s), landing panels,
    /// car front returns, the wall opening and the dimensions of the reference drawings. Every new entity is tagged
    /// "Plan.AutoDoor.NNN"; <see cref="Clear"/> erases them and shows the manual geometry again, so a Manual project is untouched.
    /// </summary>
    public static class AutomaticDoorPlan
    {
        /// <summary>Key prefix of the automatic door entities.</summary>
        public const string KeyPrefix = "Plan.AutoDoor.";

        // Car front wall thickness (return) and the offsets of the 2C panels inside their frames (references), mm.
        const double CarReturn = 40;
        const double BoxDepth = 70;
        const double CarBoxPanelOffset = 12;
        const double LandingBoxPanelOffset = 5;
        // Panel bands end this far inside the frame end (the references' parked-panel block inset).
        const double ParkedBlockInset = 4;
        // Inset line of the jamb posts, mm.
        const double PostInset = 3;

        const string LayerCar = "Car";
        const string LayerGuides = "step&guides";
        const string LayerFrame = "0";
        const string LayerSill = "DL";

        static readonly string[] HiddenRoles = { "doorLeft", "doorRight", "doorSpan", "doorLeader", "entrance" };

        // The returns and the door dimensions are redrawn; the template wall pieces are redrawn with the opening where the door is.
        // Plan.CenterFront.Part001-013 is the centre mark device of the manual landing door (not in the automatic door references).
        static readonly string[] HiddenKeys =
        {
            "Plan.CenterFront.Part001",
            "Plan.CenterFront.Part002",
            "Plan.CenterFront.Part003",
            "Plan.CenterFront.Part004",
            "Plan.CenterFront.Part005",
            "Plan.CenterFront.Part006",
            "Plan.CenterFront.Part007",
            "Plan.CenterFront.Part008",
            "Plan.CenterFront.Part009",
            "Plan.CenterFront.Part010",
            "Plan.CenterFront.Part011",
            "Plan.CenterFront.Part012",
            "Plan.CenterFront.Part013",
            "Plan.Cabin.Inner.Front",
            "Plan.Cabin.Part002",
            "Plan.Cabin.Part003",
            "Plan.Cabin.Part004",
            "Plan.Dimension.Part006",
            "Plan.Dimension.Part007",
            "Plan.Dimension.Part008",
            "Plan.Dimension.Part009",
            "Plan.Dimension.Part010",
            "Plan.Dimension.Part011",
            "Plan.Dimension.Part012",
            "Plan.Dimension.Part013",
            // CAR DEPTH and the front zone total: redrawn on one dimension line (D3), their extension lines from the car front return.
            "Plan.Dimension.Part020",
            "Plan.Dimension.Part031"
        };

        static readonly string[] WallKeys =
        {
            "Plan.Wall.Part001",
            "Plan.Wall.Part004",
            "Plan.Wall.Part005",
            "Plan.Wall.Part006",
            "Plan.Shaft.InnerOutline.EntranceDatum"
        };

        /// <summary>The door input of the project stored in the drawing; null for a Manual door or when no project is stored.</summary>
        public static DoorInput InputFor(Database db, Transaction tr)
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
            return DoorInput.From(project, Configuration.DoorCatalog());
        }

        /// <summary>The layout of the stored project's automatic door on a plan; null for a Manual door.</summary>
        public static DoorLayout LayoutFor(Database db, Transaction tr, ElevatorParameters plan)
        {
            var input = InputFor(db, tr);
            return input == null ? null : DoorRules.Select(input, plan);
        }

        static bool IsHidden(string key, string role)
        {
            return HiddenRoles.Contains(role) || HiddenKeys.Contains(key) || WallKeys.Contains(key);
        }

        static string[] ReadTag(Entity e)
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

        /// <summary>
        /// Removes the automatic door entities and shows the hidden template geometry again. Does nothing (reads only) when the drawing
        /// has no automatic door, so a Manual drawing is never touched.
        /// </summary>
        public static void Clear(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var mine = new List<ObjectId>();
            var hidden = new List<ObjectId>();
            foreach (ObjectId id in model)
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var tag = ReadTag(e);
                if (tag == null)
                    continue;
                if (tag[0].StartsWith(KeyPrefix))
                    mine.Add(id);
                else if (!e.Visible && IsHidden(tag[0], tag[1]))
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

        /// <summary>Draws the automatic door of a layout on the updated plan (call after <see cref="Clear"/> and the plan update).</summary>
        public static void Draw(Database db, Transaction tr, DrawingSnapshot s, DoorLayout l, bool steel = false)
        {
            new Builder(db, tr, s, l) { Steel = steel }.Run();
        }

        sealed class Builder
        {
            readonly Database db;
            readonly Transaction tr;
            readonly DrawingSnapshot s;
            readonly DoorLayout l;
            readonly ShaftFrame f;
            readonly BlockTableRecord model;
            int count;
            double jambDepth;
            /// <summary>A steel tower: there is no wall to open, the door frame is fixed to the beams.</summary>
            public bool Steel;

            public Builder(Database db, Transaction tr, DrawingSnapshot s, DoorLayout l)
            {
                this.db = db;
                this.tr = tr;
                this.s = s;
                this.l = l;
                f = s.Frame;
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            }

            Point3d P(double x, double d)
            {
                return f.World(new Point3d(x, d, 0));
            }

            string NextKey()
            {
                count++;
                return KeyPrefix + count.ToString("000", System.Globalization.CultureInfo.InvariantCulture);
            }

            void Tag(Entity e)
            {
                using (var rb = new ResultBuffer(
                    new TypedValue(1001, DrawingReader.RegApp),
                    new TypedValue(1000, NextKey()),
                    new TypedValue(1000, "autoDoor")))
                    e.XData = rb;
            }

            T Add<T>(T e, string layer) where T : Entity
            {
                if (layer != null)
                    e.Layer = layer;
                model.AppendEntity(e);
                tr.AddNewlyCreatedDBObject(e, true);
                Tag(e);
                return e;
            }

            void Rect(double x0, double x1, double d0, double d1, string layer)
            {
                var pl = new Polyline();
                pl.AddVertexAt(0, Pt(P(Math.Min(x0, x1), d0)), 0, 0, 0);
                pl.AddVertexAt(1, Pt(P(Math.Max(x0, x1), d0)), 0, 0, 0);
                pl.AddVertexAt(2, Pt(P(Math.Max(x0, x1), d1)), 0, 0, 0);
                pl.AddVertexAt(3, Pt(P(Math.Min(x0, x1), d1)), 0, 0, 0);
                pl.Closed = true;
                pl.Elevation = f.Origin.Z;
                Add(pl, layer);
            }

            void Seg(double x0, double d0, double x1, double d1, string layer)
            {
                Add(new Line(P(x0, d0), P(x1, d1)), layer);
            }

            static Point2d Pt(Point3d p)
            {
                return new Point2d(p.X, p.Y);
            }

            T Source<T>(string key) where T : Entity
            {
                return (T)s.Entities[key];
            }

            // A copy of a hidden template entity, with a new key (same layer, line type, style).
            T CloneOf<T>(Entity original) where T : Entity
            {
                var c = (T)original.Clone();
                model.AppendEntity(c);
                tr.AddNewlyCreatedDBObject(c, true);
                c.Visible = true;
                Tag(c);
                return c;
            }

            public void Run()
            {
                foreach (var b in s.Bindings.Values)
                {
                    if (IsHidden(b.Key, b.Role))
                    {
                        var e = s.Entities[b.Key];
                        e.UpgradeOpen();
                        e.Visible = false;
                    }
                }
                if (!Steel)
                    Wall();
                Jambs();
                if (l.Type == DoorTypes.Automatic2S)
                    SideOpening();
                else
                    CentreOpening();
                CarReturns();
                Sills();
                Dimensions();
            }

            // Wall pieces: the same entities, with the two jamb edges of the wall opening where the door is.
            void Wall()
            {
                var datum = Source<Polyline>("Plan.Shaft.InnerOutline.EntranceDatum");
                double jl = f.Local(datum.GetPoint3dAt(0)).X, jr = f.Local(datum.GetPoint3dAt(5)).X;
                double jamb = s.ElevatorParameters.JambWidth;
                double nl = l.OpeningLeftX - jamb, nr = l.OpeningRightX + jamb;
                foreach (string key in WallKeys)
                {
                    var c = CloneOf<Entity>(s.Entities[key]);
                    var line = c as Line;
                    if (line != null)
                    {
                        line.StartPoint = Moved(line.StartPoint, jl, jr, nl, nr);
                        line.EndPoint = Moved(line.EndPoint, jl, jr, nl, nr);
                        continue;
                    }
                    var pl = c as Polyline;
                    if (pl != null)
                    {
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                        {
                            var w = Moved(pl.GetPoint3dAt(i), jl, jr, nl, nr);
                            pl.SetPointAt(i, new Point2d(w.X, w.Y));
                        }
                    }
                }
            }

            Point3d Moved(Point3d world, double jl, double jr, double nl, double nr)
            {
                var p = f.Local(world);
                if (Math.Abs(p.X - jl) < 1e-6)
                    return f.World(new Point3d(nl, p.Y, p.Z));
                if (Math.Abs(p.X - jr) < 1e-6)
                    return f.World(new Point3d(nr, p.Y, p.Z));
                return world;
            }

            // Jamb blocks in the wall opening, double line (outer outline and a 3 mm inset), as the template ones, at the opening edges.
            void Jambs()
            {
                double j = s.ElevatorParameters.JambWidth;
                var template = Source<Polyline>("Plan.DoorLeft.Part001");
                double depth = Math.Abs(f.Local(template.GetPoint3dAt(2)).Y - f.Local(template.GetPoint3dAt(0)).Y);
                if (depth < 1)
                    depth = 63.1;
                jambDepth = depth;
                foreach (double[] block in new[] { new[] { l.OpeningLeftX - j, l.OpeningLeftX }, new[] { l.OpeningRightX, l.OpeningRightX + j } })
                {
                    Rect(block[0], block[1], -depth, 0, LayerFrame);
                    Rect(block[0] + 3, block[1] - 3, -depth + 3, -3, LayerFrame);
                }
            }

            double Mirror(double x)
            {
                return l.StackingSide == DoorTypes.Right ? x : s.ElevatorParameters.SW - x;
            }

            // A panel in the style of the references: outline (layer given) and three construction lines at 10 mm in the panel.
            void Panel(double x0, double x1, double d0, double d1, string outline)
            {
                Rect(x0, x1, d0, d1, outline);
                double a = Math.Min(x0, x1), b = Math.Max(x0, x1);
                for (int k = 1; k <= 3; k++)
                    Seg(a, d0 + 10 * k, b, d0 + 10 * k, LayerFrame);
            }

            void PanelU(double u0, double u1, double d0, double d1, string outline)
            {
                Panel(Mirror(u0), Mirror(u1), d0, d1, outline);
            }

            void Open(string layer, double[] x, double[] d)
            {
                var pl = new Polyline();
                for (int i = 0; i < x.Length; i++)
                    pl.AddVertexAt(i, Pt(P(x[i], d[i])), 0, 0, 0);
                pl.Closed = false;
                pl.Elevation = f.Origin.Z;
                Add(pl, layer);
            }

            // The band of a panel row (D3, the user's edit of the 2S drawing): the panel continues from its closed end into the stacking
            // pocket / box up to the end of the frame. Outline and a 1 mm inset line, both open at the closed end (1 mm stubs on the
            // outline), so that the panel and its band read as one bar inside the frame.
            void Band(double xClosed, double xEnd, double d0, double d1, string layer)
            {
                if (Math.Abs(xEnd - xClosed) < 2)
                    return;
                // The inset line is 1 mm short of the right-hand end only, as in the references (and the user's 2S and 2C edits).
                double inner = xEnd > xClosed ? xEnd - 1 : xEnd;
                Open(layer, new[] { xClosed, xClosed, xEnd, xEnd, xClosed, xClosed }, new[] { d0 + 1, d0, d0, d1, d1, d1 - 1 });
                Open(layer, new[] { xClosed, inner, inner, xClosed }, new[] { d0 + 1, d0 + 1, d1 - 1, d1 - 1 });
            }

            // A fixed-side jamb post of a frame: outline, and an inset line 3 mm inside it that is closed at the opening edge.
            void Post(double xOuter, double xOpening, double d0, double d1)
            {
                Rect(xOuter, xOpening, d0, d1, LayerFrame);
                if (Math.Abs(xOpening - xOuter) > 2 * PostInset)
                {
                    double inset = xOuter + (xOuter < xOpening ? PostInset : -PostInset);
                    Rect(inset, xOpening, d0 + PostInset, d1 - PostInset, LayerFrame);
                }
            }

            void SideOpening()
            {
                double t = l.PanelThicknessMm, pw = l.PanelWidthMm, o = l.OpeningMm;
                double ot = Math.Max(0, 2 * pw - o - l.PanelOverlapMm);
                double lz = l.LandingZoneMm, cd = l.LandingZoneMm + l.SillGapMm;
                double cdEnd = s.ElevatorParameters.CarFront - CarReturn;
                // u runs from the fixed-side wall: the opening spans [uf, uf + o].
                double uf = l.StackingSide == DoorTypes.Right ? l.OpeningLeftX : s.ElevatorParameters.SW - l.OpeningRightX;
                double us = uf + o;
                double ul = l.StackingSide == DoorTypes.Right ? l.LandingLeftX : s.ElevatorParameters.SW - l.LandingRightX;
                double ur = l.StackingSide == DoorTypes.Right ? l.LandingRightX : s.ElevatorParameters.SW - l.LandingLeftX;
                // Frames: the landing door frame over the landing zone and the car door frame from the sill gap to the car front return,
                // each one rectangle from the outer edge of the fixed-side jamb to the end of the stacking pocket.
                Rect(Mirror(ul), Mirror(ur), 0, lz, LayerFrame);
                Rect(Mirror(ul), Mirror(ur), cd, cdEnd, LayerFrame);
                // Fixed-side jamb posts (landing and car), as wide as the landing fixed jamb.
                if (uf - ul > 0.5)
                {
                    Post(Mirror(ul), Mirror(uf), 0, lz);
                    Post(Mirror(ul), Mirror(uf), cd, cdEnd);
                }
                // Panels (closed): the fixed-side panel next to the car and the leading panel behind it, landing and car.
                PanelU(uf, uf + pw, lz - t, lz, LayerCar);
                PanelU(us + ot - pw, us + ot, lz - 2 * t - 5, lz - t - 5, LayerCar);
                PanelU(uf, uf + pw, cd, cd + t, LayerCar);
                PanelU(us + ot - pw, us + ot, cd + t + 5, cd + 2 * t + 5, LayerCar);
                // Bands of the four panel rows, from the closed end of the panel to the end of the pocket.
                double end = ur - ParkedBlockInset;
                Band(Mirror(uf + pw), Mirror(end), lz - t, lz, LayerCar);
                Band(Mirror(us + ot), Mirror(end), lz - 2 * t - 5, lz - t - 5, LayerCar);
                Band(Mirror(uf + pw), Mirror(end), cd, cd + t, LayerCar);
                Band(Mirror(us + ot), Mirror(end), cd + t + 5, cd + 2 * t + 5, LayerCar);
            }

            void CentreOpening()
            {
                double t = l.PanelThicknessMm, pw = l.PanelWidthMm;
                double lz = l.LandingZoneMm, cd = l.LandingZoneMm + l.SillGapMm;
                double cdEnd = s.ElevatorParameters.CarFront - CarReturn;
                // Frames across both stacking sides, centred on the opening, aligned with each other.
                // The landing box keeps the reference depth (the user's 2C edit left it 10 mm off the wall face); the car frame runs
                // from the sill gap to the car front return.
                Rect(l.LandingLeftX, l.LandingRightX, Math.Max(0, lz - BoxDepth), lz, LayerFrame);
                Rect(l.LandingLeftX, l.LandingRightX, cd, cdEnd, LayerFrame);
                // The two centre panels of the landing and of the car, inside their frames (the references draw them on layer 0).
                double dl = lz - Math.Min(LandingBoxPanelOffset, Math.Max(0, (lz - t) / 2)) - t;
                double dc = cd + Math.Min(CarBoxPanelOffset, Math.Max(0, (cdEnd - cd - t) / 2));
                Panel(l.OpeningLeftX, l.OpeningLeftX + pw, dl, dl + t, LayerFrame);
                Panel(l.OpeningRightX - pw, l.OpeningRightX, dl, dl + t, LayerFrame);
                Panel(l.OpeningLeftX, l.OpeningLeftX + pw, dc, dc + t, LayerFrame);
                Panel(l.OpeningRightX - pw, l.OpeningRightX, dc, dc + t, LayerFrame);
                // Bands from the closed panels to both ends of the frame.
                double e0 = l.LandingLeftX + ParkedBlockInset, e1 = l.LandingRightX - ParkedBlockInset;
                foreach (double d in new[] { dl, dc })
                {
                    Band(l.OpeningLeftX, e0, d, d + t, LayerFrame);
                    Band(l.OpeningRightX, e1, d, d + t, LayerFrame);
                }
            }

            // Thickness of the car side wall (the car outer face to the inner face).
            double CarWallThickness()
            {
                return s.Frame.Local(s.Get<Line>("Plan.Cabin.Outer.Rear").StartPoint).Y - s.ElevatorParameters.CarRear;
            }

            // Car front returns: from the car side wall (outer face) to the opening edge.
            void CarReturns()
            {
                var p = s.ElevatorParameters;
                double wall = CarWallThickness();
                double left = p.X("carLeft"), right = p.X("carRight");
                double d0 = p.CarFront - CarReturn, d1 = p.CarFront;
                Rect(left - wall, l.OpeningLeftX, d0, d1, LayerGuides);
                Rect(l.OpeningRightX, right + wall, d0, d1, LayerGuides);
                // Centre lines of the opening edges across the return, as in the references.
                Seg(l.OpeningLeftX, d0, l.OpeningLeftX, d1, "CL");
                Seg(l.OpeningRightX, d0, l.OpeningRightX, d1, "CL");
            }

            // Landing sill at the shaft front wall face and car sill at the outer face of the car front.
            void Sills()
            {
                Seg(l.OpeningLeftX, 0, l.OpeningRightX, 0, LayerSill);
                Seg(l.OpeningLeftX, s.ElevatorParameters.CarFront - CarReturn, l.OpeningRightX, s.ElevatorParameters.CarFront - CarReturn, LayerSill);
            }

            // One dimension of a chain: the template dimension it is a copy of, the two measured positions along the measuring axis
            // (A < B) and the positions of the extension line origins across it (null: the template's).
            sealed class Link
            {
                public string Source;
                public double A;
                public double B;
                public double? CrossA;
                public double? CrossB;

                public Link(string source, double a, double b, double? crossA, double? crossB)
                {
                    Source = source;
                    A = a;
                    B = b;
                    CrossA = crossA;
                    CrossB = crossB;
                }
            }

            // Draws a chain of contiguous dimensions on ONE dimension line (DESIGN-FLOW principle 8): vertical = measured along the depth,
            // the line at x = line; otherwise measured along x, the line at depth = line. The text of an interval too small for it is
            // placed by DimensionLayout outside the chain / beside it; towardsGeometry (+1 / -1 along the axis across the line) is the
            // direction in which a dimension stepped out of the chain moves.
            void Chain(bool vertical, double line, int towardsGeometry, IList<Link> links, IList<double[]> blocked)
            {
                int n = links.Count;
                var dims = new RotatedDimension[n];
                var boundaries = new double[n + 1];
                var texts = new string[n];
                double z = 0, height = 0, arrow = 0;
                for (int i = 0; i < n; i++)
                {
                    var k = links[i];
                    if (i > 0 && Math.Abs(links[i - 1].B - k.A) > 1e-6)
                        throw new ArgumentException("The dimensions of a chain are contiguous.");
                    var src = Source<RotatedDimension>(k.Source);
                    var a = f.Local(src.XLine1Point);
                    var c = f.Local(src.XLine2Point);
                    var d = CloneOf<RotatedDimension>(src);
                    double ca = k.CrossA ?? (vertical ? a.X : a.Y);
                    double cb = k.CrossB ?? (vertical ? c.X : c.Y);
                    z = a.Z;
                    d.XLine1Point = vertical ? f.World(new Point3d(ca, k.A, a.Z)) : f.World(new Point3d(k.A, ca, a.Z));
                    d.XLine2Point = vertical ? f.World(new Point3d(cb, k.B, c.Z)) : f.World(new Point3d(k.B, cb, c.Z));
                    dims[i] = d;
                    boundaries[i] = k.A;
                    boundaries[i + 1] = k.B;
                    texts[i] = DimensionLayout.DisplayText(d.DimensionText, Math.Abs(k.B - k.A));
                    height = Math.Max(height, TextHeightOf(d));
                    arrow = Math.Max(arrow, d.Dimasz * d.Dimscale);
                }
                var place = DimensionLayout.PlaceChainTexts(boundaries, texts, height, arrow, blocked);
                for (int i = 0; i < n; i++)
                {
                    double mid = (links[i].A + links[i].B) / 2;
                    var d = dims[i];
                    // A text moved off its dimension line stays where it is put (free text), the line does not follow it.
                    if (place[i].LateralMm > 0)
                        d.Dimtmove = 2;
                    double own = line + towardsGeometry * place[i].LineOffsetMm;
                    if (vertical)
                    {
                        d.DimLinePoint = f.World(new Point3d(own, mid, z));
                        d.TextPosition = f.World(new Point3d(own - place[i].LateralMm, mid + place[i].AlongMm, z));
                    }
                    else
                    {
                        d.DimLinePoint = f.World(new Point3d(mid, own, z));
                        d.TextPosition = f.World(new Point3d(mid + place[i].AlongMm, own + place[i].LateralMm, z));
                    }
                    d.RecomputeDimensionBlock(true);
                }
            }

            // The height of the dimension text: the fixed height of its text style when it has one, else the dimension style's.
            double TextHeightOf(Dimension d)
            {
                var style = tr.GetObject(d.TextStyleId, OpenMode.ForRead) as TextStyleTableRecord;
                if (style != null && style.TextSize > 0)
                    return style.TextSize;
                return d.Dimtxt * d.Dimscale;
            }

            double LineOf(string key, bool vertical)
            {
                var p = f.Local(Source<RotatedDimension>(key).DimLinePoint);
                return vertical ? p.X : p.Y;
            }

            void Dimensions()
            {
                var p = s.ElevatorParameters;
                double sw = p.SW, front = p.CarFront;
                double ol = l.OpeningLeftX, orr = l.OpeningRightX, centre = (ol + orr) / 2;
                double carLeft = p.X("carLeft"), carRight = p.X("carRight");
                double wall = CarWallThickness();
                double lz = l.LandingZoneMm, cd = l.LandingZoneMm + l.SillGapMm;
                double edge = -jambDepth;

                // Above the car front: car return | opening | car return on the one line of the template opening dimension. The car side
                // walls block the texts; a return too small for its text gets it outside the chain, beyond the wall.
                Chain(false, LineOf("Plan.Dimension.Part010", false), -1,
                    new List<Link>
                    {
                        new Link("Plan.Dimension.Part009", carLeft, ol, null, null),
                        new Link("Plan.Dimension.Part010", ol, orr, null, null),
                        new Link("Plan.Dimension.Part011", orr, carRight, null, null)
                    },
                    new List<double[]> { new[] { carLeft - wall, carLeft }, new[] { carRight, carRight + wall } });

                // Below the front wall, rows at the template's spacing: wall | DOOR | wall at the template door row, the two halves of
                // the opening one row nearer, and (opening off the shaft centre, 2S or 2C) the Doors Offset one row nearer still. The extension
                // lines start at the wall inner face and at the bottom of the wall jamb blocks.
                double doorRow = LineOf("Plan.Dimension.Part008", false);
                double halvesRow = LineOf("Plan.Dimension.Part006", false);
                double offsetRow = DimensionLayout.RowBefore(halvesRow, doorRow);
                Chain(false, doorRow, 1,
                    new List<Link>
                    {
                        new Link("Plan.Dimension.Part013", 0, ol, 0, edge),
                        new Link("Plan.Dimension.Part008", ol, orr, edge, edge),
                        new Link("Plan.Dimension.Part012", orr, sw, edge, 0)
                    },
                    null);
                Chain(false, halvesRow, 1,
                    new List<Link>
                    {
                        new Link("Plan.Dimension.Part006", ol, centre, edge, 0),
                        new Link("Plan.Dimension.Part007", centre, orr, 0, edge)
                    },
                    null);
                if (Math.Abs(centre - sw / 2) > 0.05)
                {
                    bool openingFirst = centre < sw / 2;
                    Chain(false, offsetRow, 1,
                        new List<Link>
                        {
                            new Link("Plan.Dimension.Part006", Math.Min(centre, sw / 2), Math.Max(centre, sw / 2),
                                openingFirst ? 0 : edge, openingFirst ? edge : 0)
                        },
                        null);
                }

                // Left of the shaft: the front zone split (landing door zone | sill gap | car door zone) on one line, and, one column
                // further out, the zone total and CAR DEPTH on one line. The extension lines start at the frame corners and at the
                // car front return, not at the wall.
                double totalColumn = LineOf("Plan.Dimension.Part020", true);
                double chainColumn = totalColumn + DimensionLayout.ColumnPitchMm;
                double wallX = f.Local(Source<RotatedDimension>("Plan.Dimension.Part031").XLine1Point).X;
                double frameX = l.LandingLeftX;
                double returnX = (carLeft - wall + ol) / 2;
                double rearY = f.Local(Source<RotatedDimension>("Plan.Dimension.Part020").XLine2Point).Y;
                Chain(true, chainColumn, 1,
                    new List<Link>
                    {
                        new Link("Plan.Dimension.Part031", 0, lz, wallX, frameX),
                        new Link("Plan.Dimension.Part031", lz, cd, frameX, frameX),
                        new Link("Plan.Dimension.Part031", cd, front, frameX, returnX)
                    },
                    null);
                Chain(true, totalColumn, 1,
                    new List<Link>
                    {
                        new Link("Plan.Dimension.Part031", 0, front, wallX, returnX),
                        new Link("Plan.Dimension.Part020", front, rearY, returnX, null)
                    },
                    null);
            }
        }
    }
}
