using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.AutoCAD.Common;
using Elevator.AutoCAD.MachineRoom;
using Elevator.Core.Geometry;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.SectionBB
{
    /// <summary>Updates the B-B view from the shared design.</summary>
    public sealed class MachineRoomSectionUpdater : IViewUpdater
    {
        public string ViewId
        {
            get
            {
                return "MachineRoomSection";
            }
        }

        /// <summary>Converts a local B-B point to the world frame.</summary>
        public static Point3d World(SectionSnapshot s, LocalPoint p)
        {
            return s.Frame.World(new Point3d(p.X, p.Y, p.Z));
        }

        /// <summary>Moves the B-B entities to the new design.</summary>
        public ViewUpdateResult Update(Database db, Transaction tr, ElevatorParameters design)
        {
            Steel.SteelDrawing.ClearSection(db, tr);
            var s = MachineRoomSectionDrawing.Read(db, tr);
            var p = design.MachineRoomSection;
            var g = new MachineRoomSectionGeometry(design);
            if (p == null)
                throw new InvalidOperationException("Shared section calculation missing.");
            var removed = new List<string>();
            foreach (var b in s.Manifest.Entities)
            {
                var e = s.Entities[b.Key];
                e.UpgradeOpen();
                if (b.Role == "frame")
                    continue;
                if (b.Role == "status")
                {
                    // No status / remark text is drawn (DESIGN-FLOW: remarks go to the Design Notes file only): the master's note is
                    // erased and leaves the manifest, so the drawing stays consistent.
                    e.Erase();
                    removed.Add(b.Key);
                    continue;
                }
                if (b.Role == "dimension")
                {
                    UpdateDimension((Dimension)e, b.Key, s, design);
                    continue;
                }
                if (b.Role == "rope")
                {
                    UpdateRope(s, p, b, e);
                    continue;
                }
                if (b.Role == "generatedRope")
                {
                    UpdateWrapArc(s, p, b, e);
                    continue;
                }
                UpdateNativeEntity(s, p, g, b, e);
            }
            if (removed.Count > 0)
            {
                s.Manifest.Entities.RemoveAll(v => removed.Contains(v.Key));
                ProjectDataStorage.Write(db, tr, MachineRoomSectionDrawing.Record, s.Manifest);
            }
            var audit =
                MachineRoomSectionGeometryAudit.Run(MachineRoomSectionDrawing.Read(db, tr), design, MachineRoomDrawing.Read(db, tr));
            ProjectDataStorage.Write(db, tr, "MachineRoomSectionDesign", p);
            ProjectDataStorage.Write(db, tr, "MachineRoomSectionGeometryAudit", audit);
            // A steel shaft: the machine stands on the steel top frame in place of the slab and the concrete blocks.
            var steel = Steel.SteelDrawing.Read(db, tr);
            if (steel != null)
                Steel.SteelDrawing.DrawSection(db, tr, s.Frame, steel, p.SteelBottomZ);
            return new ViewUpdateResult
            {
                Dimensions = new Dictionary<string, double>(),
                Annotations = new Dictionary<string, string>(),
                Clearances = new Dictionary<string, double>()
            };
        }

        private void UpdateRope(SectionSnapshot s, MachineRoomSectionParameters p, SectionBinding b, Entity e)
        {
            var line = (Line)e;
            line.Visible = p.HasCalculatedPath;
            line.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1);
            line.Linetype = "Continuous";
            line.LineWeight = LineWeight.LineWeight030;
            if (p.HasCalculatedPath)
            {
                LocalPoint a, c;
                if (b.Key.EndsWith("CommonTangent"))
                {
                    a = p.Arrangement.Reeving.TractionTangentPoint;
                    c = p.Arrangement.Reeving.DeflectorTangentPoint;
                }
                else
                {
                    double x = b.Key.EndsWith("CarDrop") ? p.CarDropX : p.CwDropX;
                    double z = b.Key.EndsWith("CarDrop") ? p.TractionZ : p.DeflectorZ;
                    a = new LocalPoint(x, b.SourcePoints.Min(v => v.Y), 0);
                    c = new LocalPoint(x, z, 0);
                }
                line.StartPoint = World(s, a);
                line.EndPoint = World(s, c);
            }
        }

        private void UpdateWrapArc(SectionSnapshot s, MachineRoomSectionParameters p, SectionBinding b, Entity e)
        {
            var arc = (Arc)e;
            bool traction = b.Key.EndsWith("TractionWrap");
            double beta = p.HasCalculatedPath ? p.Arrangement.Reeving.CalculatedWrapAngleDeg.Value * Math.PI / 180 : 0;
            arc.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1);
            arc.Linetype = "Continuous";
            arc.LineWeight = LineWeight.LineWeight030;
            arc.Visible = p.HasCalculatedPath && (traction || Math.Abs(beta - Math.PI) > 1e-8);
            if (arc.Visible)
            {
                arc.Center = World(
                    s,
                    traction ? new LocalPoint(p.TractionX, p.TractionZ, 0) : new LocalPoint(p.DeflectorX, p.DeflectorZ, 0));
                arc.Radius = traction ? 260 : p.DeflectorPitchMm / 2;
                double angle = Math.Atan2(s.Frame.XAxis.Y, s.Frame.XAxis.X);
                arc.StartAngle = angle + (traction ? 0 : beta);
                arc.EndAngle = angle + (traction ? beta : Math.PI);
            }
        }

        private void UpdateNativeEntity(
            SectionSnapshot s,
            MachineRoomSectionParameters p,
            MachineRoomSectionGeometry g,
            SectionBinding b,
            Entity e)
        {
            e.Visible = b.Visible;
            if (b.Role == "deflector" || b.Role == "hanger")
                e.Visible = p.DeflectorVisible && b.Visible;
            if (b.Role == "steelDetail" && p.Beam.NominalDepthMm != 140)
                e.Visible = false;
            var poly = e as Polyline;
            var nativeLine = e as Line;
            var hatch = e as Hatch;
            if (g.Flexible(b.Role) && (poly != null || nativeLine != null))
            {
                var mapped = b.SourcePoints.Select(v => World(s, g.Map(v, b.Role))).ToArray();
                if (poly != null)
                {
                    if (poly.NumberOfVertices != mapped.Length)
                        throw new InvalidOperationException("Section topology changed: " + b.Key);
                    for (int i = 0; i < mapped.Length; i++)
                        poly.SetPointAt(i, new Point2d(mapped[i].X, mapped[i].Y));
                }
                else
                {
                    nativeLine.StartPoint = mapped[0];
                    nativeLine.EndPoint = mapped[1];
                }
            }
            else if (hatch != null && (b.Role == "slab" || b.Role == "concrete"))
            {
                var corners = new[]
                {
                        new LocalPoint(b.Bounds[0], b.Bounds[1], 0),
                        new LocalPoint(b.Bounds[2], b.Bounds[1], 0),
                        new LocalPoint(b.Bounds[2], b.Bounds[3], 0),
                        new LocalPoint(b.Bounds[0], b.Bounds[3], 0)
                    };
                hatch.Associative = false;
                while (hatch.NumberOfLoops > 0)
                    hatch.RemoveLoopAt(0);
                var vertices = new Point2dCollection();
                var bulges = new DoubleCollection();
                foreach (var v in corners)
                {
                    var w = World(s, g.Map(v, b.Role));
                    vertices.Add(new Point2d(w.X, w.Y));
                    bulges.Add(0);
                }
                hatch.AppendLoop(HatchLoopTypes.External, vertices, bulges);
                hatch.EvaluateHatch(true);
            }
            else
            {
                var target = g.Map(b.SourcePoints[0], b.Role);
                var actual = MachineRoomDependencies.Points(e, s.Frame)[0];
                e.TransformBy(Matrix3d.Displacement(s.Frame.Vector(target.X - actual.X, target.Y - actual.Y)));
            }
            if (b.Key == "MachineRoomSection.TractionSheave.PitchCircle")
                ((Circle)e).Radius = 260;
            if (b.Key == "MachineRoomSection.DeflectorSheave.PitchCircle")
                ((Circle)e).Radius = p.DeflectorPitchMm / 2;
        }

        private static void UpdateDimension(Dimension dimension, string key, SectionSnapshot s, ElevatorParameters d)
        {
            var p = d.MachineRoomSection;
            string name = key.Substring(key.LastIndexOf('.') + 1);
            var linear = dimension as RotatedDimension;
            var diameter = dimension as DiametricDimension;
            var angular = dimension as LineAngularDimension2;
            dimension.Visible = true;
            double frameAngle = Math.Atan2(s.Frame.XAxis.Y, s.Frame.XAxis.X);
            if (linear != null)
            {
                LocalPoint a, b, where;
                bool horizontal = name == "TZ" || name == "FrontToCar" || name == "RearToCW";
                if (horizontal)
                {
                    double x1 = name == "RearToCW" ? -d.SD : (name == "TZ" ? p.CwDropX : p.CarDropX);
                    double x2 = name == "RearToCW" ? p.CwDropX : (name == "TZ" ? p.CarDropX : 0);
                    a = new LocalPoint(x1, 0, 0);
                    b = new LocalPoint(x2, 0, 0);
                    where = new LocalPoint((x1 + x2) / 2, -330, 0);
                }
                else
                {
                    double z1 = 0, z2 = p.ConcreteHeight, x = 250;
                    if (name == "SteelDepth")
                    {
                        z1 = p.SteelBottomZ;
                        z2 = p.SteelTopZ;
                    }
                    if (name == "StandDepth")
                    {
                        z1 = p.SteelBottomZ - 140.0032021011732;
                        z2 = p.SteelBottomZ;
                    }
                    if (name == "CenterSeparation")
                    {
                        z1 = p.DeflectorZ;
                        z2 = p.TractionZ;
                        x = p.CwDropX - 180;
                        dimension.Visible = p.HasCalculatedPath;
                    }
                    a = new LocalPoint(x, z1, 0);
                    b = new LocalPoint(x, z2, 0);
                    where = new LocalPoint(x + 100, (z1 + z2) / 2, 0);
                }
                linear.Rotation = frameAngle + (horizontal ? 0 : Math.PI / 2);
                linear.XLine1Point = World(s, a);
                linear.XLine2Point = World(s, b);
                linear.DimLinePoint = World(s, where);
                linear.TextPosition = World(s, where);
            }
            else if (diameter != null)
            {
                bool car = name == "TractionDiameter";
                double x = car ? p.TractionX : p.DeflectorX, z = car ? p.TractionZ : p.DeflectorZ, r = car ? 260 : p.DeflectorPitchMm / 2;
                diameter.ChordPoint = World(s, new LocalPoint(x + r, z, 0));
                diameter.FarChordPoint = World(s, new LocalPoint(x - r, z, 0));
                diameter.TextPosition = World(s, new LocalPoint(x - r / 2, z - r / 4, 0));
                dimension.Visible = car || p.DeflectorVisible;
            }
            else if (angular != null)
            {
                dimension.Visible = p.HasCalculatedPath;
                if (p.HasCalculatedPath)
                {
                    var t = new LocalPoint(p.TractionX, p.TractionZ, 0);
                    angular.XLine1Start = World(s, t);
                    angular.XLine1End = World(s, new LocalPoint(t.X + 260, t.Y, 0));
                    angular.XLine2Start = World(s, t);
                    angular.XLine2End = World(s, p.Arrangement.Reeving.TractionTangentPoint);
                    double half = p.Arrangement.Reeving.CalculatedWrapAngleDeg.Value * Math.PI / 360;
                    angular.ArcPoint = World(s, new LocalPoint(t.X + 360 * Math.Cos(half), t.Y + 360 * Math.Sin(half), 0));
                    angular.TextPosition = angular.ArcPoint;
                }
            }
            dimension.RecomputeDimensionBlock(true);
        }
    }
}
