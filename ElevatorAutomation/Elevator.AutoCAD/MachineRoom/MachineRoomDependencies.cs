using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.AutoCAD.Common;
using Elevator.AutoCAD.Plan;
using Elevator.Core.Geometry;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.MachineRoom
{
    /// <summary>Dependencies of the machine-room chassis contours on the shared design.</summary>
    public static class MachineRoomDependencies
    {
        /// <summary>The points of an entity in the local frame.</summary>
        public static LocalPoint[] Points(Entity e, ShaftFrame frame)
        {
            Point3d[] pts;
            var poly = e as Polyline;
            var line = e as Line;
            var circle = e as Circle;
            var curve = e as Curve;
            if (poly != null)
                pts = Enumerable.Range(0, poly.NumberOfVertices).Select(poly.GetPoint3dAt).ToArray();
            else if (line != null)
                pts = new[] { line.StartPoint, line.EndPoint };
            else if (circle != null)
                pts = new[] { circle.Center };
            else if (curve != null)
            {
                pts = Enumerable.Range(0, 5).Select(i => curve.GetPointAtParameter(
                                    i == 4 ? curve.EndParam : curve.StartParam + (curve.EndParam - curve.StartParam) * i / 4)).ToArray();
            }
            else
            {
                using (var clone = (Entity)e.Clone())
                {
                    clone.TransformBy(Matrix3d.AlignCoordinateSystem(
                        frame.Origin,
                        frame.XAxis,
                        frame.YAxis,
                        frame.ZAxis,
                        Point3d.Origin,
                        Vector3d.XAxis,
                        Vector3d.YAxis,
                        Vector3d.ZAxis));
                    var ex = clone.GeometricExtents;
                    var c = ex.MinPoint + (ex.MaxPoint - ex.MinPoint) / 2;
                    return new[] { new LocalPoint(c.X, c.Y, c.Z) };
                }
            }
            return pts.Select(p => frame.Local(p)).Select(p => new LocalPoint(p.X, p.Y, p.Z)).ToArray();
        }

        private static bool Near(double a, double b)
        {
            return Math.Abs(a - b) < .02;
        }

        /// <summary>Derives and stores the dependency of each chassis contour.</summary>
        public static void Prepare(Database db, Transaction tr, MachineRoomSnapshot s)
        {
            if (s.Manifest.DependencyVersion == 1)
                return;
            if (s.Manifest.DependencyVersion != 0)
                throw new InvalidOperationException("Unsupported chassis dependency version.");
            var t = Configuration.Read<ChassisTopology>("chassis-topology.json");
            int full = 0, front = 0, rear = 0, labels = 0;
            foreach (var b in s.Manifest.Entities.Where(b => b.Role == "machine" || b.Role == "cw"))
            {
                ClassifyContour(s, t, ref full, ref front, ref rear, ref labels, b);
            }
            CheckTopology(t, full, front, rear, labels);
            foreach (var b in s.Manifest.Entities.Where(b => new[] { "room", "frontBeam", "shaftLeft", "shaftRight" }.Contains(b.Role)))
            {
                var points = Points(s.Entities[b.Key], s.Frame);
                b.StructuralRear = points.Select(p => Math.Abs(p.Y - s.SD) < Math.Abs(p.Y)).ToArray();
                b.StructuralYOffsets = points.Select((p, i) => p.Y - (b.StructuralRear[i] ? s.SD : 0)).ToArray();
            }
            s.Manifest.DependencyVersion = 1;
            ProjectDataStorage.Write(db, tr, MachineRoomDrawing.Record, s.Manifest);
        }

        private static void ClassifyContour(
            MachineRoomSnapshot s,
            ChassisTopology t,
            ref int full,
            ref int front,
            ref int rear,
            ref int labels,
            MachineRoomBinding b)
        {
            var e = s.Entities[b.Key];
            var points = Points(e, s.Frame);
            // Recover the reviewed source frame for legacy rigid translations, without replacing semantic IDs.
            var original = points.Select(p => new LocalPoint(
                p.X - (s.SW - t.SourceSW) / 2,
                p.Y - (b.Role == "cw" ? s.CwRow - t.SourceCwRow : s.CarRow - t.SourceCarRow),
                p.Z)).ToArray();
            double lo = original.Min(p => p.Y), hi = original.Max(p => p.Y);
            bool straight = (e is Line) || (e is Polyline
                && Enumerable.Range(0, ((Polyline)e).NumberOfVertices).All(i => Math.Abs(((Polyline)e).GetBulgeAt(i)) < 1e-9));
            bool longitudinal = straight && original.Max(p => p.X) - original.Min(p => p.X) < 2;
            // Reviewed open L: rear crosspiece plus a right-side leg ending at the machine crosspiece.
            bool rearOutline = straight
                && original.Length == 3
                && Near(original[0].Y, original[1].Y)
                && Math.Abs(original[1].X - original[2].X) < 2
                && Near(original[2].Y, t.MachineRearEnd);
            bool atFront = t.FrontMemberEnds.Any(v => Near(v, lo)), atRear = t.RearMemberEnds.Any(v => Near(v, hi));
            string behavior = "FixedToMachine", reference = "Machine";
            string[] refs = null;
            if (b.Role == "cw")
            {
                behavior = "RigidTranslate";
                reference = "Counterweight";
            }
            else if (longitudinal && atFront && atRear)
            {
                behavior = "SpanBetweenReferences";
                full++;
                refs = original.Select(p => Near(p.Y, lo) ? "FrontBeam" : "RearBeam").ToArray();
            }
            else if (longitudinal && atFront && Near(hi, t.MachineFrontEnd))
            {
                behavior = "StretchFromCarSide";
                front++;
                refs = original.Select(p => Near(p.Y, hi) ? "Machine" : "FrontBeam").ToArray();
            }
            else if ((longitudinal || rearOutline) && Near(lo, t.MachineRearEnd) && atRear)
            {
                behavior = "StretchFromCWRearSide";
                rear++;
                refs = original.Select(p => Near(p.Y, lo) ? "Machine" : "RearBeam").ToArray();
            }
            else if (b.Key.StartsWith("MachineRoom.Support.P1.") || b.Key.StartsWith("MachineRoom.Support.P3."))
            {
                behavior = "FixedToBeam";
                reference = "RearBeam";
            }
            else if (b.Key.StartsWith("MachineRoom.Support.P2.") || b.Key.StartsWith("MachineRoom.Support.P4."))
            {
                behavior = "FixedToBeam";
                reference = "FrontBeam";
            }
            else if (straight && hi < 0)
            {
                behavior = "FixedToBeam";
                reference = "FrontBeam";
            }
            else if (straight && lo > t.SourceSD)
            {
                behavior = "FixedToBeam";
                reference = "RearBeam";
            }
            else if (b.Key.StartsWith("MachineRoom.Chassis.ReferenceDetail.")
                && t.RearSupportLabelWindows.Any(w => original.All(p => p.X >= w[0] && p.Y >= w[1] && p.X <= w[2] && p.Y <= w[3])))
            {
                behavior = "FixedToBeam";
                reference = "RearBeam";
                labels++;
            }
            if (refs == null)
                refs = original.Select(p => reference).ToArray();
            b.Dependency =
                new ChassisDependency
                {
                    Behavior = behavior,
                    References = refs,
                    Offsets = original.Select((p, i) => new LocalPoint(
                p.X - t.SourceSW / 2,
                p.Y - ChassisDependencies.Datum(refs[i], t.SourceCarRow, t.SourceSD, t.SourceCwRow),
                p.Z)).ToArray()
                };
            if (e is Circle)
                b.Dependency.CircleRadius = ((Circle)e).Radius;
        }

        private static void CheckTopology(ChassisTopology t, int full, int front, int rear, int labels)
        {
            if (full != t.ExpectedFullSpans || front != t.ExpectedFrontSpans || rear != t.ExpectedRearSpans)
            {
                throw new InvalidOperationException("Chassis topology differs from the reviewed reference: spans "
                                    + full
                                    + "/"
                                    + front
                                    + "/"
                                    + rear
                                    + ". Review required; no proximity fallback.");
            }
            if (labels != t.ExpectedRearLabelContours)
                throw new InvalidOperationException("Reviewed P1/P3 annotation topology differs; no automatic label inference.");
        }

        /// <summary>Moves an entity to follow its dependency.</summary>
        public static void Apply(Entity e, MachineRoomBinding b, MachineRoomSnapshot s, ElevatorParameters design)
        {
            var d = b.Dependency;
            var target = Enumerable.Range(0, d.Offsets.Length)
                .Select(i => ChassisDependencies.Target(d, i, design.Center, design.CarRow, design.SD, design.CwRow))
                .ToArray();
            bool span = d.Behavior == "SpanBetweenReferences" || d.Behavior.StartsWith("Stretch");
            if (span)
            {
                Func<int, Point3d> world = i => s.Frame.World(new Point3d(target[i].X, target[i].Y, target[i].Z));
                var line = e as Line;
                var poly = e as Polyline;
                if (line != null)
                {
                    line.StartPoint = world(0);
                    line.EndPoint = world(1);
                }
                else if (poly != null && poly.NumberOfVertices == target.Length)
                {
                    for (int i = 0; i < target.Length; i++)
                    {
                        var p = world(i);
                        poly.SetPointAt(i, new Point2d(p.X, p.Y));
                    }
                }
                else
                    throw new InvalidOperationException("Chassis span topology changed: " + b.Key);
            }
            else
            {
                var p = Points(e, s.Frame)[0];
                e.TransformBy(Matrix3d.Displacement(s.Frame.Vector(target[0].X - p.X, target[0].Y - p.Y)));
            }
        }
    }
}
