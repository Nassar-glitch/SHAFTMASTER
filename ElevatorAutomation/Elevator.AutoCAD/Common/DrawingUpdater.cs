using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.AutoCAD.Plan;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Common
{
    public static class DrawingUpdater
    {
        public static void ApplyShape(Entity e, string role, PlanGeometryAdapter g)
        {
            if (!g.IsFlexible(role))
            {
                e.TransformBy(Matrix3d.Displacement(g.Translation(role)));
                return;
            }
            Line line = e as Line;
            if (line != null)
            {
                Point3d a = g.MapPoint(line.StartPoint, role), b = g.MapPoint(line.EndPoint, role);
                line.StartPoint = a;
                line.EndPoint = b;
                return;
            }
            Polyline pl = e as Polyline;
            if (pl != null)
            {
                // Normal +Z was checked by the reader. OCS XY equals world XY for these polylines.
                var pts = new Point3d[pl.NumberOfVertices];
                for (int i = 0; i < pts.Length; i++)
                    pts[i] = g.MapPoint(pl.GetPoint3dAt(i), role);
                for (int i = 0; i < pts.Length; i++)
                    pl.SetPointAt(i, new Point2d(pts[i].X, pts[i].Y));
                return;
            }
            MLeader ml = e as MLeader;
            if (ml != null && role == "doorLeader")
            {
                // Preserve the existing leader and arrow style; adjust its attachment points.
                var first = new Dictionary<int, Point3d>();
                var last = new Dictionary<int, Point3d>();
                foreach (int leader in ml.GetLeaderIndexes())
                {
                    foreach (int index in ml.GetLeaderLineIndexes(leader))
                    {
                        first[index] = g.MapPoint(ml.GetFirstVertex(index), role);
                        last[index] = g.MapPoint(ml.GetLastVertex(index), role);
                    }
                }
                ml.TextLocation = g.MapPoint(ml.TextLocation, role);
                foreach (int index in first.Keys)
                {
                    ml.SetLastVertex(index, last[index]);
                    ml.SetFirstVertex(index, first[index]);
                }
                return;
            }
            throw new InvalidOperationException("Unsupported flexible entity: " + role + " / " + e.GetRXClass().DxfName);
        }

        private static readonly string[] YAnchors = { "front", "rear", "carFront", "carRear", "carRow", "cwRow", "cwFront", "cwRear" };

        private static readonly string[] XAnchors =
            { "left", "right", "carLeft", "carRight", "cwLeft", "cwRight", "railLeft", "railRight" };

        public static double UpdateDimension(RotatedDimension d, Binding b, PlanGeometryAdapter g)
        {
            var f = g.Frame;
            Point3d a = f.Local(d.XLine1Point), c = f.Local(d.XLine2Point), p = f.Local(d.DimLinePoint), t = f.Local(d.TextPosition);
            bool horizontal = b.Axis == "x";
            double av = horizontal ? a.X : a.Y, cv = horizontal ? c.X : c.Y;
            string ka = b.A, kb = b.B;
            double oa = horizontal ? g.Old.X(ka) : g.Old.Y(ka), ob = horizontal ? g.Old.X(kb) : g.Old.Y(kb);
            if (Math.Abs(av - oa) + Math.Abs(cv - ob) > Math.Abs(av - ob) + Math.Abs(cv - oa))
            {
                string swap = ka;
                ka = kb;
                kb = swap;
                double z = oa;
                oa = ob;
                ob = z;
            }
            double na = horizontal ? g.Next.X(ka) : g.Next.Y(ka), nb = horizontal ? g.Next.X(kb) : g.Next.Y(kb);
            Point3d aa, cc, pp, tt;
            if (horizontal)
            {
                aa = new Point3d(a.X + na - oa, g.MapY(a.Y, YAnchors), a.Z);
                cc = new Point3d(c.X + nb - ob, g.MapY(c.Y, YAnchors), c.Z);
                double deltaMid = (aa.X + cc.X - a.X - c.X) / 2, deltaBand = g.DY(b.Band);
                pp = new Point3d(p.X + deltaMid, p.Y + deltaBand, p.Z);
                tt = new Point3d(t.X + deltaMid, t.Y + deltaBand, t.Z);
            }
            else
            {
                aa = new Point3d(g.MapX(a.X, XAnchors), a.Y + na - oa, a.Z);
                cc = new Point3d(g.MapX(c.X, XAnchors), c.Y + nb - ob, c.Z);
                double deltaMid = (aa.Y + cc.Y - a.Y - c.Y) / 2, deltaBand = g.DX(b.Band);
                pp = new Point3d(p.X + deltaBand, p.Y + deltaMid, p.Z);
                tt = new Point3d(t.X + deltaBand, t.Y + deltaMid, t.Z);
            }
            d.XLine1Point = f.World(aa);
            d.XLine2Point = f.World(cc);
            d.DimLinePoint = f.World(pp);
            d.TextPosition = f.World(tt);
            d.RecomputeDimensionBlock(true);
            return Math.Abs(horizontal ? cc.X - aa.X : cc.Y - aa.Y);
        }

        public static Dictionary<string, double> Preflight(DrawingSnapshot snapshot, PlanGeometryAdapter g)
        {
            var gaps = new Dictionary<string, double>();
            double minLeft = Double.MaxValue, minRight = Double.MaxValue, minRear = Double.MaxValue, minFront = Double.MaxValue;
            foreach (var pair in snapshot.Entities)
            {
                Binding b = snapshot.Bindings[pair.Key];
                if (b.Role == "dimension")
                    continue;
                using (Entity copy = (Entity)pair.Value.Clone())
                {
                    ApplyShape(copy, b.Role, g);
                    bool equipment = new[]
                    {
                        "carLeft",
                        "carRight",
                        "carSupportLeft",
                        "carSupportRight",
                        "cwLeft",
                        "cwRight",
                        "cwOutline",
                        "governor"
                    }.Contains(b.Role);
                    // Text/leader extents are not equipment envelopes.
                    if (!equipment || copy is MText || copy is MLeader)
                        continue;
                    // Evaluate native extents in the shaft frame, including arcs, bulges, splines and hatch loops.
                    // This avoids world-axis hatch boxes and incomplete curve sampling after rotation.
                    double angle = Math.Atan2(g.Frame.XAxis.Y, g.Frame.XAxis.X);
                    copy.TransformBy(
                        Matrix3d.Rotation(-angle, Vector3d.ZAxis, Point3d.Origin) * Matrix3d.Displacement(Point3d.Origin - g.Frame.Origin));
                    Extents3d e = copy.GeometricExtents;
                    foreach (Point3d p in new[] { e.MinPoint, e.MaxPoint })
                    {
                        minLeft = Math.Min(minLeft, p.X);
                        minRight = Math.Min(minRight, g.Next.SW - p.X);
                        minRear = Math.Min(minRear, g.Next.SD - p.Y);
                        minFront = Math.Min(minFront, p.Y);
                        if (p.X < -0.05 || p.X > g.Next.SW + 0.05 || p.Y < -0.05 || p.Y > g.Next.SD + 0.05)
                        {
                            throw new InvalidOperationException("Shaft does not fit existing "
                                                            + b.Role
                                                            + " detail "
                                                            + b.Key
                                                            + ". Increase SW/SD; no drawing changes were made.");
                        }
                    }
                }
            }
            gaps["equipmentToLeftShaftMinMm"] = minLeft;
            gaps["equipmentToRightShaftMinMm"] = minRight;
            gaps["equipmentToRearShaftMinMm"] = minRear;
            gaps["equipmentToFrontShaftMinMm"] = minFront;
            gaps["cabinDoorReturnMm"] = (g.Next.CarWidth - g.Next.DoorWidth) / 2;
            gaps["cwRailToShaftSideMm"] = (g.Next.SW - g.Next.CwDbg) / 2;
            gaps["cwFrameToRearNominalMm"] = g.Next.SD - g.Next.CwRear;
            // Read the original outer rear face, preserving its actual wall offset.
            double wallOffset = g.Frame.Local(snapshot.Get<Line>("Plan.Cabin.Outer.Rear").StartPoint).Y - g.Old.CarRear;
            double cwSkew = g.Frame.Local(snapshot.Get<Polyline>("Plan.Counterweight.Outline").GetPoint3dAt(1)).Y
                - g.Frame.Local(snapshot.Get<Polyline>("Plan.Counterweight.Outline").GetPoint3dAt(0)).Y;
            gaps["cabinOuterRearToCwMinMm"] = g.Next.CwFront + Math.Min(0, cwSkew) - g.Next.CarRear - wallOffset;
            if (gaps["cabinOuterRearToCwMinMm"] <= 0)
                throw new InvalidOperationException("Cabin and counterweight outlines overlap.");
            return gaps;
        }

        public static Dictionary<string, double> Apply(Database db, Transaction tr, DrawingSnapshot s, PlanGeometryAdapter g)
        {
            RegAppTable apps = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (!apps.Has(DrawingReader.RegApp))
            {
                apps.UpgradeOpen();
                var app = new RegAppTableRecord { Name = DrawingReader.RegApp };
                apps.Add(app);
                tr.AddNewlyCreatedDBObject(app, true);
            }
            foreach (var p in s.Entities)
            {
                if (s.Bindings[p.Key].Role == "dimension")
                    continue;
                p.Value.UpgradeOpen();
                ApplyShape(p.Value, s.Bindings[p.Key].Role, g);
                Tag(p.Value, p.Key, s.Bindings[p.Key].Role);
            }
            var expected = new Dictionary<string, double>();
            foreach (var p in s.Entities)
            {
                if (s.Bindings[p.Key].Role == "dimension")
                {
                    var d = (RotatedDimension)p.Value;
                    d.UpgradeOpen();
                    expected[p.Key] = UpdateDimension(d, s.Bindings[p.Key], g);
                    Tag(d, p.Key, "dimension");
                }
            }
            return expected;
        }

        private static void Tag(Entity e, string key, string role)
        {
            using (var rb =
                new ResultBuffer(new TypedValue(1001, DrawingReader.RegApp), new TypedValue(1000, key), new TypedValue(1000, role)))
                e.XData = rb;
        }

        public static void Validate(DrawingSnapshot s, ElevatorParameters target, Dictionary<string, double> expected)
        {
            AssertNear(s.ElevatorParameters.SW, target.SW, "Shaft width");
            AssertNear(s.ElevatorParameters.SD, target.SD, "Shaft depth");
            AssertNear(s.ElevatorParameters.CarWidth, target.CarWidth, "Cabin width");
            AssertNear(s.ElevatorParameters.CarDepth, target.CarDepth, "Cabin depth");
            AssertNear(s.ElevatorParameters.CarDbg, target.CarDbg, "Car DBG");
            AssertNear(s.ElevatorParameters.CwDbg, target.CwDbg, "CW DBG");
            AssertNear(s.ElevatorParameters.CwWidth, target.CwWidth, "CW frame width");
            AssertNear(s.ElevatorParameters.DoorWidth, target.DoorWidth, "Door width");
            AssertNear(s.ElevatorParameters.TZ, target.TZ, "TZ");
            AssertNear(s.ElevatorParameters.CarRow, target.CarRow, "Car rail row");
            AssertNear(s.ElevatorParameters.CwRow, target.CwRow, "CW rail row");
            foreach (var e in expected)
                AssertNear(s.Get<RotatedDimension>(e.Key).Measurement, e.Value, "Dimension " + e.Key);
            // Independent formula checks prevent a wrong binding from validating its own result.
            double outerRearOffset = s.Frame.Local(s.Get<Line>("Plan.Cabin.Outer.Rear").StartPoint).Y - s.ElevatorParameters.CarRear;
            double doorReturn = (target.CarWidth - target.DoorWidth) / 2, shaftDoorSide =
                (target.SW - target.DoorWidth) / 2, cwSide = (target.SW - target.CwDbg) / 2;
            var dimensions = new Dictionary<string, double> {
                {"Plan.Dimension.Part001",target.CwDbg/2},
                    {"Plan.Dimension.Part002",target.CwDbg/2},
                    {"Plan.Dimension.Part003",target.CwDbg},
                    {"Plan.Dimension.Part004",cwSide},
                    {"Plan.Dimension.Part005",cwSide},
                {"Plan.Dimension.Part006",target.DoorWidth/2},
                    {"Plan.Dimension.Part007",target.DoorWidth/2},
                    {"Plan.Dimension.Part008",target.DoorWidth},
                    {"Plan.Dimension.Part009",doorReturn},
                    {"Plan.Dimension.Part010",target.DoorWidth},
                    {"Plan.Dimension.Part011",doorReturn},
                {"Plan.Dimension.Part012",shaftDoorSide},
                    {"Plan.Dimension.Part013",shaftDoorSide},
                    {"Plan.Dimension.Part014",target.SW/2},
                    {"Plan.Dimension.Part015",target.SW/2},
                    {"Plan.Dimension.Part016",target.SW},
                {"Plan.Dimension.Part017",target.SD-target.CwRow},
                    {"Plan.Dimension.Part018",target.CarRow},
                    {"Plan.Dimension.Part019",target.TZ},
                    {"Plan.Dimension.Part020",target.CarDepth},
                {"Plan.Dimension.Part021",target.CarRow-target.CarFront},
                    {"Plan.Dimension.Part022",target.CarRear-target.CarRow},
                    {"Plan.Dimension.Part023",target.SD-target.CwRow},
                {"Plan.Dimension.Part024",target.CarDbg},
                    {"Plan.Dimension.Part025",target.CarWidth},
                    {"Plan.Dimension.Part026",target.SD},
                    {"Plan.Dimension.Part027",target.CwRow-target.CarRear},
                {"Plan.Dimension.Part028",target.CwDepth},
                    {"Plan.Dimension.Part029",target.CwFront-target.CarRear-outerRearOffset},
                    {"Plan.Dimension.Part030",target.SD-target.CwRear},
                    {"Plan.Dimension.Part031",target.CarFront}
            };
            foreach (var d in dimensions)
                AssertNear(s.Get<RotatedDimension>(d.Key).Measurement, d.Value, "Independent dimension " + d.Key);
        }

        public static void AssertNear(double value, double expected, string label)
        {
            if (Double.IsNaN(value)
                || Double.IsInfinity(value)
                || Double.IsNaN(expected)
                || Double.IsInfinity(expected)
                || Math.Abs(value - expected)
                > 0.02)
                throw new InvalidOperationException(label + " verification failed: " + value + " versus " + expected + " mm.");
        }
    }
}
