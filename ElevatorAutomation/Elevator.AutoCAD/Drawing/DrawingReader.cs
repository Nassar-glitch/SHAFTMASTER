using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Project;

namespace Elevator.AutoCAD
{
    public sealed class ShaftFrame
    {
        public Point3d Origin;
        public Vector3d XAxis;
        public Vector3d YAxis;
        public Vector3d ZAxis;

        public Point3d Local(Point3d p)
        {
            Vector3d d = p - Origin;
            return new Point3d(d.DotProduct(XAxis), d.DotProduct(YAxis), d.DotProduct(ZAxis));
        }

        public Point3d World(Point3d p)
        {
            return Origin + p.X * XAxis + p.Y * YAxis + p.Z * ZAxis;
        }

        public Vector3d Vector(double x, double y)
        {
            return x * XAxis + y * YAxis;
        }
    }

    public sealed class DrawingSnapshot
    {
        public ShaftFrame Frame;
        public ElevatorParameters ElevatorParameters;
        public Dictionary<string, Entity> Entities;
        public Dictionary<string, Binding> Bindings;
        public int ModelEntityCount;

        public T Get<T>(string key) where T : Entity
        {
            return (T)Entities[key];
        }
    }

    public static class DrawingReader
    {
        public const string RegApp = "ELEVATOR_AUTOMATION";

        public static DrawingSnapshot Read(Database db, Transaction tr)
        {
            var manifest = ComponentBinding.ReadManifest(db, tr);
            if (db.Insunits != UnitsValue.Millimeters)
                throw new InvalidOperationException("This template must use millimetres (INSUNITS=4).");
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            int count = 0;
            var byKey = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId id in model)
            {
                Entity e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                count++;
                using (ResultBuffer rb = e.GetXDataForApplication(RegApp))
                {
                    if (rb != null)
                    {
                        var values = rb.AsArray();
                        if (values.Length > 1)
                        {
                            string key = Convert.ToString(values[1].Value);
                            if (byKey.ContainsKey(key))
                                throw new InvalidOperationException("Duplicate component tag: " + key);
                            byKey[key] = e;
                        }
                    }
                }
            }
            DrawingSnapshot s = new DrawingSnapshot();
            s.Entities = new Dictionary<string, Entity>();
            s.Bindings = manifest.Entities.ToDictionary(b => b.Key);
            s.ModelEntityCount = count;
            foreach (Binding b in manifest.Entities)
            {
                Entity e;
                if (!byKey.TryGetValue(b.Key, out e))
                    throw new InvalidOperationException("Template component missing: " + b.Key + ".");
                if (e.GetRXClass().DxfName != b.Type)
                    throw new InvalidOperationException("Template component signature changed: " + b.Key + ". Expected " + b.Type + ".");
                if (e is Polyline && (((Polyline)e).Normal - Vector3d.ZAxis).Length > 1e-6)
                    throw new InvalidOperationException("Tilted/mirrored polyline planes are not supported by this template version.");
                s.Entities.Add(b.Key, e);
            }
            Polyline shaft = s.Get<Polyline>("Plan.Shaft.InnerOutline.EntranceDatum");
            if (shaft.NumberOfVertices != 6)
                throw new InvalidOperationException("The shaft outline topology has changed; expected its original six vertices.");
            Point3d origin = shaft.GetPoint3dAt(1);
            Vector3d xv = shaft.GetPoint3dAt(4) - origin, yv = shaft.GetPoint3dAt(2) - origin;
            if (xv.Length < 1 || yv.Length < 1 || Math.Abs(xv.GetNormal().DotProduct(yv.GetNormal())) > 1e-6)
                throw new InvalidOperationException("Shaft inner faces are not rectangular.");
            s.Frame = new ShaftFrame
            {
                Origin = origin,
                XAxis = xv.GetNormal(),
                YAxis = yv.GetNormal(),
                ZAxis = xv.GetNormal().CrossProduct(yv.GetNormal())
            };
            if (s.Frame.ZAxis.DotProduct(Vector3d.ZAxis) < 0.999999)
            {
                throw new InvalidOperationException(
                                    "The shaft must be a plan in the XY plane; in-plane translation and rotation are supported.");
            }
            var f = s.Frame;
            var l = new ElevatorParameters();
            l.SW = xv.Length;
            l.SD = yv.Length;
            double left = f.Local(s.Get<Line>("Plan.Cabin.Inner.Left").StartPoint).X, right =
                f.Local(s.Get<Line>("Plan.Cabin.Inner.Right").StartPoint).X;
            l.CarWidth = right - left;
            l.CarFront = f.Local(s.Get<Line>("Plan.Cabin.Inner.Front").StartPoint).Y;
            l.CarRear = f.Local(s.Get<Line>("Plan.Cabin.Inner.Rear").StartPoint).Y;
            l.CarDepth = l.CarRear - l.CarFront;
            l.CarRow = f.Local(s.Get<Line>("Plan.CarGuides.ReferenceRow").StartPoint).Y;
            l.CwRow = f.Local(s.Get<Line>("Plan.CounterweightGuides.ReferenceRow").StartPoint).Y;
            l.CarDbg = Math.Abs(f.Local(s.Get<Line>("Plan.CarGuides.Right.Contact").StartPoint).X
                - f.Local(s.Get<Line>("Plan.CarGuides.Left.Contact").StartPoint).X);
            l.CwDbg = Math.Abs(f.Local(s.Get<Line>("Plan.CounterweightGuides.Right.Contact").StartPoint).X
                - f.Local(s.Get<Line>("Plan.CounterweightGuides.Left.Contact").StartPoint).X);
            Polyline cw = s.Get<Polyline>("Plan.Counterweight.Outline");
            if (cw.NumberOfVertices != 4)
                throw new InvalidOperationException("Counterweight outline must have four vertices.");
            Point3d cw0 = f.Local(cw.GetPoint3dAt(0)), cw1 = f.Local(cw.GetPoint3dAt(1)), cw3 = f.Local(cw.GetPoint3dAt(3));
            l.CwWidth = cw1.X - cw0.X;
            l.CwDepth = cw3.Y - cw0.Y;
            l.CwFront = cw0.Y;
            l.CwRear = cw3.Y;
            Line opening = s.Get<Line>("Plan.Door.Opening");
            l.DoorWidth = Math.Abs(f.Local(opening.EndPoint).X - f.Local(opening.StartPoint).X);
            l.JambWidth = l.X("doorLeft") - f.Local(shaft.GetPoint3dAt(0)).X;
            l.TZ = l.CwRow - l.CarRow;
            if (Math.Abs((left + right) / 2 - l.Center) > 0.01 || l.CarWidth <= 0 || l.CwWidth <= 0 || l.JambWidth <= 0)
                throw new InvalidOperationException("The current drawing no longer matches the centered template geometry.");
            s.ElevatorParameters = l;
            return s;
        }
    }
}
