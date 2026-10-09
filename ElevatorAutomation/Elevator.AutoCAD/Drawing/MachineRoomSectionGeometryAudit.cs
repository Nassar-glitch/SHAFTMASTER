using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Project;
using Elevator.Core.MachineRoom;
using Elevator.Core.Geometry;

namespace Elevator.AutoCAD
{
    public sealed class SectionAuditResult
    {
        public string Status;
        public int NativePointsChecked;
        public string RopeClearanceStatus = "NOT YET EVALUABLE";
        public string ConcreteBeamStructuralStatus = "NOT EVALUATED";
        public string HangerMechanicalStatus = "MECHANICAL REVIEW REQUIRED";
        public List<string> GeometricObservations = new List<string>();
    }

    public static class MachineRoomSectionGeometryAudit
    {
        private static void Near(double a, double b, string name)
        {
            DrawingUpdater.AssertNear(a, b, "Section audit: " + name);
        }

        private static void Point(LocalPoint a, LocalPoint b, string name)
        {
            Near(a.X, b.X, name + " X");
            Near(a.Y, b.Y, name + " Z");
        }

        public static double SupportOffset(MachineRoomSnapshot mr, string name)
        {
            var b = mr.Manifest
                .Entities
                .Where(v => v.Key.StartsWith("MachineRoom.Support." + name + ".") && mr.Entities[v.Key] is Polyline)
                .OrderByDescending(v =>
                {
                    var q = v.Dependency.Offsets;
                    return (q.Max(x => x.X) - q.Min(x => x.X)) * (q.Max(x => x.Y) - q.Min(x => x.Y));
                }).First();
            return (b.Dependency.Offsets.Min(v => v.Y) + b.Dependency.Offsets.Max(v => v.Y)) / 2;
        }

        public static SectionAuditResult Run(SectionSnapshot s, ElevatorParameters design, MachineRoomSnapshot mr)
        {
            var p = design.MachineRoomSection;
            var result = new SectionAuditResult { Status = p.LayoutStatus };
            var g = new MachineRoomSectionGeometry(design);
            Near(mr.CarRow, -p.CarDropX, "shared car reference");
            Near(mr.CwRow, -p.CwDropX, "shared CW reference");
            Near(mr.CwRow - mr.CarRow, design.TZ, "shared TZ");
            Near(mr.PitchProjection, p.DeflectorPitchMm, "cross-view deflector diameter");
            if (mr.Entities[MachineRoomDrawing.Pitch].Visible != p.DeflectorVisible)
                throw new InvalidOperationException("Section deflector presence contradicts Machine Room.");
            var bearingDepths = new Dictionary<string, double>();
            foreach (string name in new[] { "P1", "P2", "P3", "P4" })
            {
                var support = mr.Manifest
                    .Entities
                    .Where(v => v.Key.StartsWith("MachineRoom.Support." + name + ".") && mr.Entities[v.Key] is Polyline)
                    .OrderByDescending(v =>
                    {
                        var q = v.Dependency.Offsets;
                        return (q.Max(x => x.X) - q.Min(x => x.X)) * (q.Max(x => x.Y) - q.Min(x => x.Y));
                    }).First();
                var points = MachineRoomDependencies.Points(mr.Entities[support.Key], mr.Frame);
                double actualDepth = (points.Min(v => v.Y) + points.Max(v => v.Y)) / 2;
                bearingDepths[name] = actualDepth;
                Near(
                    actualDepth,
                    SupportOffset(mr, name) + (name == "P1" || name == "P3" ? design.SD : 0),
                    "actual cross-view bearing " + name);
            }
            Near(-(bearingDepths["P2"] + bearingDepths["P4"]) / 2, p.FrontSupportX, "projected front bearing row");
            Near(-(bearingDepths["P1"] + bearingDepths["P3"]) / 2, p.RearSupportX, "projected rear bearing row");
            result.GeometricObservations.Add(
                "Section support rows project the mean of the paired native plate centers; native P1/P3 and P2/P4 offsets are retained "
                + "individually in Machine Room. This is a geometric span, not a verified structural effective span.");
            var traction = (Circle)s.Entities["MachineRoomSection.TractionSheave.PitchCircle"];
            var deflector = (Circle)s.Entities["MachineRoomSection.DeflectorSheave.PitchCircle"];
            var t = s.Frame.Local(traction.Center);
            var d = s.Frame.Local(deflector.Center);
            Point(new LocalPoint(t.X, t.Y, 0), new LocalPoint(p.TractionX, p.TractionZ, 0), "actual traction center");
            Point(new LocalPoint(d.X, d.Y, 0), new LocalPoint(p.DeflectorX, p.DeflectorZ, 0), "actual deflector center");
            Near(traction.Diameter, 520, "traction pitch");
            Near(deflector.Diameter, p.DeflectorPitchMm, "deflector pitch");
            foreach (var b in s.Manifest.Entities)
            {
                var e = s.Entities[b.Key];
                if (b.Role == "deflector" || b.Role == "hanger")
                {
                    if (e.Visible != (p.DeflectorVisible && b.Visible))
                        throw new InvalidOperationException("Orphan deflector dependency: " + b.Key);
                }
                if (b.Role == "rope" && e.Visible != p.HasCalculatedPath)
                    throw new InvalidOperationException("Obsolete rope path visibility: " + b.Key);
                if (b.Role == "dimension")
                {
                    string name = b.Key.Substring(b.Key.LastIndexOf('.') + 1);
                    var dim = (Dimension)e;
                    double expected = name == "TZ" ? design.TZ : name == "FrontToCar" ? design.CarRow : name == "RearToCW"
                        ? design.SD - design.CwRow
                        : name == "ConcreteHeight" ? p.ConcreteHeight : name == "SteelDepth" ? p.Beam.NominalDepthMm : name == "StandDepth"
                        ? 140.0032021011732
                        : name == "TractionDiameter" ? 520 : name == "DeflectorDiameter" ? p.DeflectorPitchMm : name == "CenterSeparation"
                            ? p.TractionZ - p.DeflectorZ
                            : (p.HasCalculatedPath ? p.Arrangement.Reeving.CalculatedWrapAngleDeg.Value * Math.PI / 180 : 0);
                    if (dim.Visible)
                    {
                        Near(dim.Measurement, expected, name);
                        if (dim.DimensionText != "" && dim.DimensionText != "TZ <>")
                            throw new InvalidOperationException("Unexpected section dimension override: " + name);
                        var linear = dim as RotatedDimension;
                        if (linear != null)
                        {
                            var first = s.Frame.Local(linear.XLine1Point);
                            double x = name == "RearToCW"
                                ? -design.SD
                                : name == "TZ"
                                    ? p.CwDropX
                                    : name == "FrontToCar" ? p.CarDropX : name == "CenterSeparation" ? p.CwDropX - 180 : 250;
                            Near(first.X, x, name + " reference anchor");
                        }
                        var angular = dim as LineAngularDimension2;
                        if (angular != null)
                        {
                            var center = s.Frame.Local(angular.XLine1Start);
                            Point(
                                new LocalPoint(center.X, center.Y, 0),
                                new LocalPoint(p.TractionX, p.TractionZ, 0),
                                "angular dimension center");
                            Near(angular.XLine1Start.DistanceTo(angular.XLine2Start), 0, "angular common origin");
                        }
                        var diameter = dim as DiametricDimension;
                        if (diameter != null)
                        {
                            var center = s.Frame.Local(diameter.ChordPoint + (diameter.FarChordPoint - diameter.ChordPoint) / 2);
                            Point(
                                new LocalPoint(center.X, center.Y, 0),
                                name == "TractionDiameter"
                                    ? new LocalPoint(p.TractionX, p.TractionZ, 0)
                                    : new LocalPoint(p.DeflectorX, p.DeflectorZ, 0),
                                "diameter dimension center");
                        }
                    }
                    continue;
                }
                if (b.Role == "status")
                {
                    var where = s.Frame.Local(((MText)e).Location);
                    Point(new LocalPoint(where.X, where.Y, 0), new LocalPoint(400, p.TractionZ + 260, 0), "engineering annotation anchor");
                    continue;
                }
                if (b.Role == "frame" || b.Role == "generatedRope" || b.Role == "rope")
                    continue;
                if (b.Radius.HasValue)
                {
                    var circle = e as Circle;
                    var arc = e as Arc;
                    double radius = circle != null ? circle.Radius : arc.Radius;
                    Near(radius, b.Key == "MachineRoomSection.DeflectorSheave.PitchCircle"
                        ? p.DeflectorPitchMm / 2
                        : b.Key == "MachineRoomSection.TractionSheave.PitchCircle" ? 260 : b.Radius.Value, b.Key + " preserved radius");
                }
                var hatch = e as Hatch;
                if (hatch != null && (b.Role == "concrete" || b.Role == "slab"))
                {
                    var low = g.Map(new LocalPoint(b.Bounds[0], b.Bounds[1], 0), b.Role);
                    var high = g.Map(new LocalPoint(b.Bounds[2], b.Bounds[3], 0), b.Role);
                    if (hatch.NumberOfLoops != 1 || !hatch.GetLoopAt(0).IsPolyline)
                        throw new InvalidOperationException("Invalid structural hatch " + b.Key);
                    var loop = hatch.GetLoopAt(0).Polyline;
                    var vertices = new List<Point2d>();
                    foreach (BulgeVertex v in loop)
                    {
                        if (Math.Abs(v.Bulge) > 1e-9)
                            throw new InvalidOperationException("Unexpected curved structural hatch");
                        vertices.Add(v.Vertex);
                    }
                    var expected = new[]
                    {
                        new LocalPoint(low.X, low.Y, 0),
                        new LocalPoint(high.X, low.Y, 0),
                        new LocalPoint(high.X, high.Y, 0),
                        new LocalPoint(low.X, high.Y, 0)
                    };
                    if (vertices.Count != 4)
                        throw new InvalidOperationException("Changed structural hatch corner count");
                    for (int i = 0; i < 4; i++)
                    {
                        var q = s.Frame.Local(new Point3d(vertices[i].X, vertices[i].Y, hatch.Elevation));
                        Point(new LocalPoint(q.X, q.Y, 0), expected[i], b.Key + " actual boundary position");
                    }
                    double area = 0;
                    for (int i = 0; i < vertices.Count; i++)
                    {
                        var a = vertices[i];
                        var next = vertices[(i + 1) % vertices.Count];
                        area += a.X * next.Y - next.X * a.Y;
                    }
                    area = Math.Abs(area) / 2;
                    Near(area, Math.Abs((high.X - low.X) * (high.Y - low.Y)), b.Key + " actual native boundary area");
                    if (area <= 0)
                        throw new InvalidOperationException("Degenerate structural hatch " + b.Key);
                    continue;
                }
                var actual = MachineRoomDependencies.Points(e, s.Frame);
                if (actual.Length != b.SourcePoints.Length)
                    throw new InvalidOperationException("Section topology changed " + b.Key);
                for (int i = 0; i < actual.Length; i++)
                {
                    Point(actual[i], g.Map(b.SourcePoints[i], b.Role), b.Key);
                    result.NativePointsChecked++;
                }
            }
            if (p.HasCalculatedPath)
            {
                var line = (Line)s.Entities["MachineRoomSection.Rope.CommonTangent"];
                var a = s.Frame.Local(line.StartPoint);
                var b = s.Frame.Local(line.EndPoint);
                Point(new LocalPoint(a.X, a.Y, 0), p.Arrangement.Reeving.TractionTangentPoint, "actual traction tangent");
                Point(new LocalPoint(b.X, b.Y, 0), p.Arrangement.Reeving.DeflectorTangentPoint, "actual deflector tangent");
                Near(a.DistanceTo(t), traction.Radius, "traction tangent radius");
                Near(b.DistanceTo(d), deflector.Radius, "deflector tangent radius");
                var direction = (b - a).GetNormal();
                Near((a - t).DotProduct(direction), 0, "traction orthogonality");
                Near((b - d).DotProduct(direction), 0, "deflector orthogonality");
                var wrap = (Arc)s.Entities["MachineRoomSection.Rope.TractionWrap"];
                double actualWrap = wrap.TotalAngle * 180 / Math.PI;
                Near(actualWrap, p.Arrangement.Reeving.CalculatedWrapAngleDeg.Value, "rendered wrap arc");
                Near(wrap.Center.DistanceTo(traction.Center), 0, "rendered wrap center");
                Near(wrap.Radius, traction.Radius, "rendered wrap radius");
                if (!wrap.Visible)
                    throw new InvalidOperationException("Calculated traction wrap hidden");
                var deflectorArc = (Arc)s.Entities["MachineRoomSection.Rope.DeflectorWrap"];
                bool expectedArc = Math.Abs(actualWrap - 180) > 1e-6;
                if (deflectorArc.Visible != expectedArc)
                    throw new InvalidOperationException("Deflector rope-arc dependency visibility");
                if (expectedArc)
                {
                    Near(deflectorArc.Center.DistanceTo(deflector.Center), 0, "deflector wrap center");
                    Near(deflectorArc.Radius, deflector.Radius, "deflector wrap radius");
                    Near(deflectorArc.TotalAngle * 180 / Math.PI, 180 - actualWrap, "deflector wrap angle");
                }
                foreach (string name in new[] { "CarDrop", "CounterweightDrop" })
                {
                    var drop = (Line)s.Entities["MachineRoomSection.Rope." + name];
                    var start = s.Frame.Local(drop.StartPoint);
                    var end = s.Frame.Local(drop.EndPoint);
                    Near(start.X, end.X, "vertical drop");
                    Near(end.X, name == "CarDrop" ? p.CarDropX : p.CwDropX, "rope opening center reference");
                }
                Near(d.Y - deflector.Radius, p.Arrangement.DeflectorToSlabClearanceMm.Value, "actual pitch-circle/slab clearance");
                result.GeometricObservations.Add("Deflector pitch-circle to slab: "
                    + (d.Y - deflector.Radius).ToString("0.###")
                    + " mm; "
                    + p.Arrangement.DeflectorSlabClearanceStatus);
                result.GeometricObservations.Add("Minimum distance between the two vertical rope centerlines: "
                    + Math.Abs(p.CarDropX - p.CwDropX).ToString("0.###")
                    + " mm; rope-bundle spacing and clearance minimum NOT YET EVALUABLE.");
                result.GeometricObservations.Add("Deflector pitch-circle to upper steel-beam underside: "
                    + (p.SteelBottomZ - d.Y - deflector.Radius).ToString("0.###")
                    + " mm; mechanical envelope clearance NOT YET EVALUABLE.");
                // Report section-plane intersections; do not infer out-of-plane interference or a minimum clearance.
                if (Math.Min(a.Y, b.Y) <= p.SteelTopZ && Math.Max(a.Y, b.Y) >= p.SteelBottomZ)
                {
                    result.GeometricObservations.Add(
                                            "INTERSECTION DETECTED: common tangent crosses the steel-beam projection; out-of-plane/opening "
                                            + "relationship "
                                            + "requires engineering review.");
                }
                else
                {
                    result.GeometricObservations.Add(
                                            "NO INTERSECTION DETECTED between common tangent and upper steel-beam elevation band.");
                }
            }
            else
            {
                result.GeometricObservations.Add(
                                    p.ReevingGeometryStatus + ". Uncalculated rope path suppressed; no wrap or clearance pass.");
            }
            return result;
        }
    }
}
