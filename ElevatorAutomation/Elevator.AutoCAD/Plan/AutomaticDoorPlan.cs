using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.AutoCAD.Common;
using Elevator.Core.Doors;
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

        // Car front wall thickness (return) and door box depth of the 2C references, mm.
        const double CarReturn = 40;
        const double BoxDepth = 70;
        const double CarBoxPanelOffset = 12;
        const double LandingBoxPanelOffset = 5;

        const string LayerCar = "Car";
        const string LayerGuides = "step&guides";
        const string LayerFrame = "0";
        const string LayerSill = "DL";

        static readonly string[] HiddenRoles = { "doorLeft", "doorRight", "doorSpan", "doorLeader", "entrance" };

        // The returns and the door dimensions are redrawn; the template wall pieces are redrawn with the opening where the door is.
        static readonly string[] HiddenKeys =
        {
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
            "Plan.Dimension.Part013"
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
        public static void Draw(Database db, Transaction tr, DrawingSnapshot s, DoorLayout l)
        {
            new Builder(db, tr, s, l).Run();
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

            // Jamb blocks, span and leader of the template, moved with the opening.
            void Jambs()
            {
                var move = Matrix3d.Displacement(f.Vector(l.CentreShiftMm, 0));
                foreach (var b in s.Bindings.Values)
                {
                    if (b.Role == "doorLeft" || b.Role == "doorRight" || b.Role == "doorSpan" || b.Role == "doorLeader")
                    {
                        var c = CloneOf<Entity>(s.Entities[b.Key]);
                        c.TransformBy(move);
                    }
                }
            }

            double Mirror(double x)
            {
                return l.StackingSide == DoorTypes.Right ? x : s.ElevatorParameters.SW - x;
            }

            void Panel(double u0, double u1, double d0, double d1, string layer)
            {
                Rect(Mirror(u0), Mirror(u1), d0, d1, layer);
            }

            void SideOpening()
            {
                double t = l.PanelThicknessMm, pw = l.PanelWidthMm, o = l.OpeningMm;
                double ot = Math.Max(0, 2 * pw - o - l.PanelOverlapMm);
                double lz = l.LandingZoneMm, cd = l.LandingZoneMm + l.SillGapMm;
                // u runs from the fixed-side wall: the opening spans [uf, uf + o].
                double uf = l.StackingSide == DoorTypes.Right ? l.OpeningLeftX : s.ElevatorParameters.SW - l.OpeningRightX;
                double us = uf + o;
                double ul = l.StackingSide == DoorTypes.Right ? l.LandingLeftX : s.ElevatorParameters.SW - l.LandingRightX;
                double ur = l.StackingSide == DoorTypes.Right ? l.LandingRightX : s.ElevatorParameters.SW - l.LandingLeftX;
                // Landing frame: fixed-side jamb, stacking pocket, and the line across the opening.
                Panel(ul, uf, 0, lz, LayerFrame);
                Panel(us, ur, 0, lz, LayerFrame);
                Seg(Mirror(uf), lz, Mirror(us), lz, LayerFrame);
                // Landing panels (closed): the fixed-side panel next to the car, the leading panel behind it.
                Panel(uf, uf + pw, lz - t, lz, LayerCar);
                Panel(us + ot - pw, us + ot, lz - 2 * t - 5, lz - t - 5, LayerCar);
                // Car panels (closed).
                Panel(uf, uf + pw, cd, cd + t, LayerCar);
                Panel(us + ot - pw, us + ot, cd + t + 5, cd + 2 * t + 5, LayerCar);
            }

            void CentreOpening()
            {
                double t = l.PanelThicknessMm, pw = l.PanelWidthMm;
                double lz = l.LandingZoneMm, cd = l.LandingZoneMm + l.SillGapMm;
                // Door boxes of the landing and of the car, and the two centre panels of each.
                Rect(l.LandingLeftX, l.LandingRightX, lz - BoxDepth, lz, LayerFrame);
                Rect(l.LandingLeftX, l.LandingRightX, cd, cd + BoxDepth, LayerFrame);
                double dl = lz - LandingBoxPanelOffset - t, dc = cd + CarBoxPanelOffset;
                Rect(l.OpeningLeftX, l.OpeningLeftX + pw, dl, dl + t, LayerCar);
                Rect(l.OpeningRightX - pw, l.OpeningRightX, dl, dl + t, LayerCar);
                Rect(l.OpeningLeftX, l.OpeningLeftX + pw, dc, dc + t, LayerCar);
                Rect(l.OpeningRightX - pw, l.OpeningRightX, dc, dc + t, LayerCar);
            }

            // Car front returns: from the car side wall (outer face) to the opening edge.
            void CarReturns()
            {
                var p = s.ElevatorParameters;
                double wall = s.Frame.Local(s.Get<Line>("Plan.Cabin.Outer.Rear").StartPoint).Y - p.CarRear;
                double left = p.X("carLeft"), right = p.X("carRight");
                double d0 = p.CarFront - CarReturn, d1 = p.CarFront;
                Rect(left - wall, l.OpeningLeftX, d0, d1, LayerGuides);
                Rect(l.OpeningRightX, right + wall, d0, d1, LayerGuides);
            }

            // Landing sill at the shaft front wall face and car sill at the outer face of the car front.
            void Sills()
            {
                Seg(l.OpeningLeftX, 0, l.OpeningRightX, 0, LayerSill);
                Seg(l.OpeningLeftX, s.ElevatorParameters.CarFront - CarReturn, l.OpeningRightX, s.ElevatorParameters.CarFront - CarReturn, LayerSill);
            }

            // A copy of a template dimension with new points; the dimension line and text keep the band of the template.
            void Dim(string source, double x1, double x2, string text)
            {
                var src = Source<RotatedDimension>(source);
                var a = f.Local(src.XLine1Point);
                var c = f.Local(src.XLine2Point);
                var p = f.Local(src.DimLinePoint);
                var t = f.Local(src.TextPosition);
                var d = CloneOf<RotatedDimension>(src);
                double mid = (x1 + x2) / 2;
                d.XLine1Point = f.World(new Point3d(x1, a.Y, a.Z));
                d.XLine2Point = f.World(new Point3d(x2, c.Y, c.Z));
                d.DimLinePoint = f.World(new Point3d(mid, p.Y, p.Z));
                d.TextPosition = f.World(new Point3d(mid, t.Y, t.Z));
                if (text != null)
                    d.DimensionText = text;
                d.RecomputeDimensionBlock(true);
            }

            // A vertical copy of the car-front dimension between two depths, in a column left of the template one.
            void DimDepth(string source, double d0, double d1, double columnShift)
            {
                var src = Source<RotatedDimension>(source);
                var a = f.Local(src.XLine1Point);
                var c = f.Local(src.XLine2Point);
                var p = f.Local(src.DimLinePoint);
                var d = CloneOf<RotatedDimension>(src);
                double mid = (d0 + d1) / 2;
                d.XLine1Point = f.World(new Point3d(a.X, d0, a.Z));
                d.XLine2Point = f.World(new Point3d(c.X, d1, c.Z));
                d.DimLinePoint = f.World(new Point3d(p.X + columnShift, mid, p.Z));
                d.TextPosition = f.World(new Point3d(p.X + columnShift, mid, p.Z));
                d.DimensionText = "";
                d.RecomputeDimensionBlock(true);
            }

            void Dimensions()
            {
                var p = s.ElevatorParameters;
                double sw = p.SW, centre = (l.OpeningLeftX + l.OpeningRightX) / 2;
                double carLeft = p.X("carLeft"), carRight = p.X("carRight");
                // Opening, car returns (above the car front).
                Dim("Plan.Dimension.Part009", carLeft, l.OpeningLeftX, null);
                Dim("Plan.Dimension.Part010", l.OpeningLeftX, l.OpeningRightX, null);
                Dim("Plan.Dimension.Part011", l.OpeningRightX, carRight, null);
                // Wall to opening on both sides, DOOR o x 2000, and the two halves of the opening (below the front wall).
                Dim("Plan.Dimension.Part013", 0, l.OpeningLeftX, null);
                Dim("Plan.Dimension.Part008", l.OpeningLeftX, l.OpeningRightX, null);
                Dim("Plan.Dimension.Part012", l.OpeningRightX, sw, null);
                Dim("Plan.Dimension.Part006", l.OpeningLeftX, centre, null);
                Dim("Plan.Dimension.Part007", centre, l.OpeningRightX, null);
                // Front-zone split: landing door zone, sill gap, car door zone.
                double lz = l.LandingZoneMm, cd = l.LandingZoneMm + l.SillGapMm;
                DimDepth("Plan.Dimension.Part031", 0, lz, -35);
                DimDepth("Plan.Dimension.Part031", lz, cd, -35);
                DimDepth("Plan.Dimension.Part031", cd, p.CarFront, -35);
            }
        }
    }
}
