using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core;

namespace Elevator.AutoCAD
{
    public sealed class MachineRoomUpdater:IViewUpdater
    {
        public string ViewId{get{return "MachineRoom";}}
        private static Point3d Map(Point3d point,MachineRoomSnapshot s,MachineRoomGeometry g,string role)
        {
            var p=s.Frame.Local(point);var n=g.Move(new LocalPoint(p.X,p.Y,p.Z),role);return s.Frame.World(new Point3d(n.X,n.Y,n.Z));
        }
        public ViewUpdateResult Update(Database db,Transaction tr,ElevatorParameters design)
        {
            if(design.MachineRoom==null)throw new InvalidOperationException("MachineRoom requires the shared Core design result.");
            var s=MachineRoomDrawing.Read(db,tr);
            MachineRoomDependencies.Prepare(db,tr,s);
            var g=new MachineRoomGeometry(design,s.SW,s.SD,s.CarRow,s.CwRow,s.PitchProjection,s.Extensions,s.GovernorStartOffset);
            bool review=design.MachineRoom.Deflector.SelectionStatus=="REQUIRES TRACTION/REEVING REVIEW";
            bool show=review?s.Entities[MachineRoomDrawing.Pitch].Visible:design.MachineRoom.Deflector.IsRequired;
            var expected=new Dictionary<string,double>();
            foreach(var b in s.Manifest.Entities)
            {
                var e=s.Entities[b.Key];e.UpgradeOpen();
                if(b.Dependency!=null){MachineRoomDependencies.Apply(e,b,s,design);continue;}
                var dim=e as RotatedDimension;
                if(dim!=null)
                {
                    if(b.Key=="MachineRoom.Governor.OffsetDimension")
                    {
                        var ga=s.Frame.Local(dim.XLine1Point);var gc=s.Frame.Local(dim.XLine2Point);
                        double dy=design.CarRow-s.CarRow;
                        bool firstHigher=ga.Y>gc.Y;
                        double lower=design.MachineRoom.Governor.OffsetFromCarGuideAxisMm.HasValue?design.CarRow:Math.Min(ga.Y,gc.Y)+dy;
                        double span=design.MachineRoom.Governor.OffsetFromCarGuideAxisMm??s.GovernorOffset;
                        dim.XLine1Point=s.Frame.World(new Point3d(ga.X,lower+(firstHigher?span:0),ga.Z));
                        dim.XLine2Point=s.Frame.World(new Point3d(gc.X,lower+(firstHigher?0:span),gc.Z));
                        double midDelta=lower+span/2-(ga.Y+gc.Y)/2;
                        dim.DimLinePoint+=s.Frame.Vector(0,midDelta);dim.TextPosition+=s.Frame.Vector(0,midDelta);dim.RecomputeDimensionBlock(true);
                        expected[b.Key]=span;continue;
                    }
                    var a=Map(dim.XLine1Point,s,g,b.Role);var c=Map(dim.XLine2Point,s,g,b.Role);
                    dim.XLine1Point=a;dim.XLine2Point=c;dim.DimLinePoint=Map(dim.DimLinePoint,s,g,b.Role);dim.TextPosition=Map(dim.TextPosition,s,g,b.Role);dim.RecomputeDimensionBlock(true);
                    expected[b.Key]=dim.Measurement;
                }
                else if(g.Flexible(b.Role))
                {
                    var line=e as Line;var poly=e as Polyline;
                    if(line!=null){var a=Map(line.StartPoint,s,g,b.Role);var c=Map(line.EndPoint,s,g,b.Role);line.StartPoint=a;line.EndPoint=c;}
                    else if(poly!=null)
                    {
                        if((poly.Normal-Vector3d.ZAxis).Length>1e-6)throw new InvalidOperationException("MachineRoom polyline plane changed: "+b.Key);
                        var pts=Enumerable.Range(0,poly.NumberOfVertices).Select(i=>Map(poly.GetPoint3dAt(i),s,g,b.Role)).ToArray();
                        for(int i=0;i<pts.Length;i++)poly.SetPointAt(i,new Point2d(pts[i].X,pts[i].Y));
                    }
                    else throw new InvalidOperationException("Unsupported flexible machine-room detail: "+b.Key);
                }
                else
                {
                    var moved=g.Move(new LocalPoint(0,0,0),b.Role);
                    e.TransformBy(Matrix3d.Displacement(s.Frame.Vector(moved.X,moved.Y)));
                }
                if(b.Key.StartsWith("MachineRoom.DeflectorSheave."))e.Visible=show&&b.OriginalVisible;
                if(b.Role=="deflectorLabel")
                {
                    var text=(MText)e;
                    text.Contents="{\\Fcomplex9|c0;%%C"+g.Pitch.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+"}";
                }
            }
            var updated=MachineRoomDrawing.Read(db,tr);Validate(updated,design);
            ProjectDataStorage.Write(db,tr,"MachineRoomGeometryAudit",MachineRoomGeometryAudit.Run(updated,design));
            design.MachineRoom.LayoutStatus=review?"REQUIRES TRACTION/REEVING REVIEW":"PASS";
            ProjectDataStorage.Write(db,tr,"MachineRoomValidation",new {LayoutStatus=design.MachineRoom.LayoutStatus,WrapAngleStatus=design.MachineRoom.Reeving.WrapAngleStatus,CodeDiameterRatioStatus=design.MachineRoom.CodeDiameterRatioStatus});
            ProjectDataStorage.Write(db,tr,"MachineRoomEngineeringRules",design.MachineRoom.Rules);
            ProjectDataStorage.Write(db,tr,"MachineRoomReeving",design.MachineRoom.Reeving);
            return new ViewUpdateResult {Dimensions=expected,Clearances=new Dictionary<string,double>(),Annotations=new Dictionary<string,string>()};
        }
        public static void Validate(MachineRoomSnapshot s,ElevatorParameters design)
        {
            MachineRoomGeometryAudit.Run(s,design);
            DrawingUpdater.AssertNear(s.SW,design.SW,"MachineRoom shared SW");DrawingUpdater.AssertNear(s.SD,design.SD,"MachineRoom shared SD");
            DrawingUpdater.AssertNear(s.CarRow,design.CarRow,"MachineRoom shared car axis");DrawingUpdater.AssertNear(s.CwRow,design.CwRow,"MachineRoom shared CW axis");
            DrawingUpdater.AssertNear(s.CwRow-s.CarRow,design.TZ,"MachineRoom shared TZ");
            var m=design.MachineRoom;
            if(m.Governor.OffsetFromCarGuideAxisMm.HasValue)
            {
                DrawingUpdater.AssertNear(s.GovernorOffset,m.Governor.OffsetFromCarGuideAxisMm.Value,"Governor offset dimension");
                DrawingUpdater.AssertNear(s.GovernorStartOffset,m.Governor.OffsetFromCarGuideAxisMm.Value,"Governor diameter start from actual car-guide axis");
            }
            DrawingUpdater.AssertNear(s.SW+s.Extensions.Left+s.Extensions.Right,m.RoomWidth,"Machine room width");
            DrawingUpdater.AssertNear(s.SD+s.Extensions.Front+s.Extensions.Rear,m.RoomDepth,"Machine room depth");
            if(m.Deflector.PitchDiameterMm.HasValue)DrawingUpdater.AssertNear(s.PitchProjection,m.Deflector.PitchDiameterMm.Value,"Deflector pitch projection");
            if(m.Deflector.SelectionStatus!="REQUIRES TRACTION/REEVING REVIEW")
                foreach(var b in s.Manifest.Entities.Where(b=>b.Key.StartsWith("MachineRoom.DeflectorSheave.")))
                    if(s.Entities[b.Key].Visible!=(m.Deflector.IsRequired&&b.OriginalVisible))throw new InvalidOperationException("Orphan/suppressed deflector object: "+b.Key);
            foreach(var pair in new Dictionary<string,double>{{"MachineRoom.Dimension.ShaftWidth",design.SW},{"MachineRoom.Dimension.ShaftDepth",design.SD},{"MachineRoom.Dimension.TZ",design.TZ},{"MachineRoom.Dimension.RoomWidth",m.RoomWidth},{"MachineRoom.Dimension.RoomDepth",m.RoomDepth},{"MachineRoom.Dimension.FrontToCar",design.CarRow},{"MachineRoom.Dimension.RearToCounterweight",design.SD-design.CwRow},{"MachineRoom.Governor.DiameterDimension",250},{"MachineRoom.Governor.OffsetDimension",s.GovernorOffset},{"MachineRoom.Dimension.FrontExtension",m.Extensions.Front},{"MachineRoom.Dimension.LeftExtension",m.Extensions.Left},{"MachineRoom.Dimension.RightExtension",m.Extensions.Right},{"MachineRoom.Dimension.LeftHalfShaft",design.Center},{"MachineRoom.Dimension.RightHalfShaft",design.Center}})
                DrawingUpdater.AssertNear(s.Get<RotatedDimension>(pair.Key).Measurement,pair.Value,pair.Key);
        }
    }
}
