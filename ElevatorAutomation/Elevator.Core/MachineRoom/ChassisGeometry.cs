using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Calculations;
using Elevator.Core.Geometry;
using Elevator.Core.Project;

namespace Elevator.Core.MachineRoom
{
    public static class ChassisGeometry
    {
        const double Tol = .02;

        sealed class Segment
        {
            public string Id;
            public double Fixed;
            public double Low;
            public double High;
        }

        sealed class Edge
        {
            public double Fixed;
            public double Low;
            public double High;
            public double Coverage;
            public List<Segment> Segments;
        }

        static bool Finite(double v)
        {
            return !Double.IsNaN(v) && !Double.IsInfinity(v);
        }

        public static LocalPoint ContactCentroid(LocalPoint[] polygon)
        {
            if (polygon == null || polygon.Length < 3)
                throw new ArgumentException("Contact boundary is missing.");
            double a = 0, x = 0, y = 0;
            for (int i = 0; i < polygon.Length; i++)
            {
                var p = polygon[i];
                var q = polygon[(i + 1) % polygon.Length];
                if (!Finite(p.X) || !Finite(p.Y) || !Finite(p.Z) || Math.Abs(p.Z - polygon[0].Z) > Tol)
                    throw new ArgumentException("Invalid contact boundary.");
                double cross = p.X * q.Y - q.X * p.Y;
                a += cross;
                x += (p.X + q.X) * cross;
                y += (p.Y + q.Y) * cross;
            }
            if (Math.Abs(a) < Tol)
                throw new ArgumentException("Degenerate bearing contact region.");
            return new LocalPoint(x / (3 * a), y / (3 * a), polygon[0].Z);
        }

        static List<Edge> Edges(List<Segment> segments)
        {
            var result = new List<Edge>();
            foreach (var s in segments.OrderBy(s => s.Fixed))
            {
                var e = result.LastOrDefault();
                if (e == null || Math.Abs(e.Fixed - s.Fixed) > Tol)
                {
                    e = new Edge { Fixed = s.Fixed, Segments = new List<Segment>() };
                    result.Add(e);
                }
                e.Segments.Add(s);
            }
            foreach (var e in result)
            {
                var ordered = e.Segments.OrderBy(s => s.Low).ToArray();
                e.Low = ordered[0].Low;
                e.High = ordered.Max(s => s.High);
                double lo = ordered[0].Low, hi = ordered[0].High, total = 0;
                foreach (var s in ordered.Skip(1))
                {
                    if (s.Low <= hi + Tol)
                        hi = Math.Max(hi, s.High);
                    else
                    {
                        total += hi - lo;
                        lo = s.Low;
                        hi = s.High;
                    }
                }
                e.Coverage = total + hi - lo;
            }
            return result;
        }

        public static BearingSeat ReadContactSeat(string id, IEnumerable<ChassisContour> contours, double bearingZ)
        {
            var vertical = new List<Segment>();
            var horizontal = new List<Segment>();
            foreach (var c in contours.Where(c => c.Straight && c.Id.StartsWith("MachineRoom.Support." + id + ".")))
            {
                for (int i = 1; i < c.Points.Length; i++)
                {
                    var a = c.Points[i - 1];
                    var b = c.Points[i];
                    double dx = Math.Abs(a.X - b.X), dy = Math.Abs(a.Y - b.Y);
                    if (dx < Tol && dy > Tol)
                    {
                        vertical.Add(
                                                    new Segment
                                                    {
                                                        Id = c.Id,
                                                        Fixed = (a.X + b.X) / 2,
                                                        Low = Math.Min(a.Y, b.Y),
                                                        High = Math.Max(a.Y, b.Y)
                                                    });
                    }
                    if (dy < Tol && dx > Tol)
                    {
                        horizontal.Add(
                                                    new Segment
                                                    {
                                                        Id = c.Id,
                                                        Fixed = (a.Y + b.Y) / 2,
                                                        Low = Math.Min(a.X, b.X),
                                                        High = Math.Max(a.X, b.X)
                                                    });
                    }
                }
            }
            // Reviewed exploded dashed contact perimeter; diagonal contours and hole axes excluded.
            var vs = Edges(vertical).Where(e => e.Segments.Count >= 4 && e.Coverage > (e.High - e.Low) * .6).ToArray();
            var candidates = new List<BearingSeat>();
            for (int i = 0; i < vs.Length; i++)
            {
                for (int j = i + 1; j < vs.Length; j++)
                {
                    double left = vs[i].Fixed, right = vs[j].Fixed, width = right - left;
                    if (width < Tol)
                        continue;
                    var hs = Edges(horizontal.Where(s => s.Low >= left - Tol && s.High <= right + Tol).ToList()).Where(e => e.Segments.Count
                        >= 4 && e.Coverage > width * .6 && Math.Abs(e.Low - left) < Tol && Math.Abs(e.High - right) < Tol).ToArray();
                    if (hs.Length != 2)
                        continue;
                    double bottom = hs[0].Fixed, top = hs[1].Fixed;
                    if (top - bottom < Tol
                        || vs[i].Low > bottom + Tol
                        || vs[j].Low > bottom + Tol
                        || vs[i].High < top - Tol
                        || vs[j].High < top - Tol)
                        continue;
                    var p = new[]
                    {
                        new LocalPoint(left, bottom, bearingZ),
                        new LocalPoint(right, bottom, bearingZ),
                        new LocalPoint(right, top, bearingZ),
                        new LocalPoint(left, top, bearingZ)
                    };
                    candidates.Add(new BearingSeat
                    {
                        Id = id,
                        ContactBoundary = p,
                        Centre = ContactCentroid(p),
                        Evidence = "Exploded contact perimeter: "
                        + String.Join(
                            ";",
                            vs[i].Segments.Concat(vs[j].Segments).Concat(hs.SelectMany(e => e.Segments)).Select(s => s.Id).Distinct())
                    });
                }
            }
            if (candidates.Count != 1)
                throw new ArgumentException(id + ": contact perimeter missing/ambiguous; no bounding-box fallback.");
            return candidates[0];
        }

        public static ChassisCalculationGeometry Calculate(ChassisGeometryEvidence evidence, ElevatorParameters design, bool basic = false)
        {
            var g = new ChassisCalculationGeometry { Recipe = basic ? "Basic" : "Advanced" };
            if (evidence == null)
            {
                g.Issues.Add(new CalculationIssue("Geometry", "Native semantic chassis evidence not supplied."));
                return g;
            }
            g.Issues.AddRange(evidence.Issues);
            if (design == null || design.MachineRoomSection == null)
            {
                g.Issues.Add(
                    new CalculationIssue("Geometry.SectionBB", "Calculated B-B is required for load elevations and rope tangencies."));
                return g;
            }
            var s = design.MachineRoomSection;
            g.Section = s;
            g.ShaftWidthMm = design.SW;
            g.ShaftDepthMm = design.SD;
            if (s.Reference == null || s.Arrangement == null)
            {
                g.Issues.Add(
                    new CalculationIssue("Geometry.SectionBB", "B-B reference/arrangement missing; no inferred attachment elevation."));
                return g;
            }
            // Explicit projection assumption: transverse bearing plane is B-B steel-bottom datum.
            g.BearingPlaneZMm = s.SteelBottomZ;
            g.HangerAttachmentZMm = s.SteelTopZ - s.Reference.HangerTopBelowSteelTop;
            List<ChassisContour> target = MapContoursToDesign(evidence, design);
            try
            {
                ReadBearings(g, target);
                FindLongitudinalMembers(basic, g, s, target);
                LocateSheaves(basic, g, s, target);
            }
            catch (ArgumentException ex)
            {
                g.Issues.Add(new CalculationIssue("Geometry.ContactOrMembers", ex.Message));
            }
            return g;
        }

        private static List<ChassisContour> MapContoursToDesign(ChassisGeometryEvidence evidence, ElevatorParameters design)
        {
            var move = new MachineRoomGeometry(
                            design,
                            evidence.SW,
                            evidence.SD,
                            evidence.CarRow,
                            evidence.CwRow,
                            evidence.Pitch,
                            evidence.Extensions,
                            evidence.GovernorStartOffset);
            var target = evidence.Contours.Select(
                c => new ChassisContour
                {
                    Id = c.Id,
                    Role = c.Role,
                    Straight = c.Straight,
                    Dependency = c.Dependency,
                    Points = c.Points
                .Select((p, i) => c.Dependency != null
                    ? ChassisDependencies.Target(c.Dependency, i, design.Center, design.CarRow, design.SD, design.CwRow)
                    : move.Move(p, c.Role))
                .ToArray()
                }).ToList();
            return target;
        }

        private static void ReadBearings(ChassisCalculationGeometry g, List<ChassisContour> target)
        {
            foreach (string id in new[] { "P1", "P2", "P3", "P4" })
                g.Bearings.Add(ReadContactSeat(id, target, g.BearingPlaneZMm));
        }

        private static void FindLongitudinalMembers(
            bool basic,
            ChassisCalculationGeometry g,
            MachineRoomSectionParameters s,
            List<ChassisContour> target)
        {
            double front = g.Bearings.Where(b => b.Id == "P2" || b.Id == "P4").Average(b => b.Centre.Y), rear =
                                g.Bearings.Where(b => b.Id == "P1" || b.Id == "P3").Average(b => b.Centre.Y);
            var spans = target.Where(c => c.Dependency != null
                && (c.Dependency.Behavior == "SpanBetweenReferences" || c.Dependency.Behavior.StartsWith("Stretch"))).ToArray();
            Func<ChassisContour, LocalPoint[]> leg =
                c => c.Points.Skip(c.Points.Length == 3 && Math.Abs(c.Points[0].Y - c.Points[1].Y) < Tol ? 1 : 0).ToArray();
            var fronts =
                spans.Where(c => c.Dependency.References.Contains("FrontBeam")).OrderBy(c => leg(c).Average(p => p.X)).ToArray();
            var rears = spans.Where(c => c.Dependency.References.Contains("RearBeam")).OrderBy(c => leg(c).Average(p => p.X)).ToArray();
            if (!basic && (fronts.Length != 9 || rears.Length != 9))
                throw new ArgumentException("Reviewed three-profile chassis topology changed; member identification requires review.");
            for (int i = 0; i < (fronts.Length == 9 && rears.Length == 9 ? 3 : 0); i++)
            {
                Func<ChassisContour, double, double> axisAt = (c, y) =>
                {
                    var p = leg(c);
                    var a = p.OrderBy(v => v.Y).First();
                    var b =
                    p.OrderBy(v => v.Y).Last();
                    return a.X + (b.X - a.X) * (y - a.Y) / (b.Y - a.Y);
                };
                double xf = (axisAt(fronts[i * 3], front) + axisAt(fronts[i * 3 + 2], front)) / 2;
                double xr = (axisAt(rears[i * 3], rear) + axisAt(rears[i * 3 + 2], rear)) / 2;
                g.LongitudinalMembers.Add(new ChassisMember
                {
                    Id = "Longitudinal." + new[] { "Left", "Middle", "Right" }[i],
                    Front = new LocalPoint(xf, front, s.SteelTopZ),
                    Rear = new LocalPoint(xr, rear, s.SteelTopZ),
                    Evidence = "Reviewed three-edge profile midline (not CG): " + String.Join(
                        ";",
                        fronts.Skip(i * 3).Take(3).Concat(rears.Skip(i * 3).Take(3)).Select(c => c.Id).Distinct())
                });
            }
        }

        private static void LocateSheaves(
            bool basic,
            ChassisCalculationGeometry g,
            MachineRoomSectionParameters s,
            List<ChassisContour> target)
        {
            double tx = PitchPlane(
                                target,
                                "MachineRoom.TractionSheave.",
                                s.Arrangement.Reeving == null ? 520 : s.Arrangement.Reeving.TractionSheavePitchDiameterMm);
            double dx = basic ? tx : PitchPlane(target, "MachineRoom.DeflectorSheave.", s.DeflectorPitchMm);
            if (!basic && Math.Abs(tx - dx) > Tol)
                throw new ArgumentException("Sheave pitch planes differ; planar reeving forces are not applicable.");
            g.TractionCentre = new LocalPoint(tx, -s.TractionX, s.TractionZ);
            g.DeflectorCentre = new LocalPoint(dx, -s.DeflectorX, s.DeflectorZ);
            g.NativeMachineMountingContours =
                target.Where(c => c.Id.StartsWith("MachineRoom.MachineChassis.")).SelectMany(c => c.Points).ToArray();
        }

        static double PitchPlane(List<ChassisContour> cs, string prefix, double diameter)
        {
            var edges = cs.Where(c => c.Id.StartsWith(prefix)
                && c.Straight
                && c.Points.Length > 1
                && c.Points.Max(p => p.X) - c.Points.Min(p => p.X) < Tol
                && Math.Abs(c.Points.Max(p => p.Y) - c.Points.Min(p => p.Y) - diameter) < Tol).ToArray();
            if (edges.Length != 2)
                throw new ArgumentException(prefix + ": expected two explicit pitch-profile edges, not a bounding box.");
            return edges.Average(c => c.Points[0].X);
        }
    }
}
