using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core;

namespace Elevator.AutoCAD
{
    public sealed class MachineRoomBinding
    {
        public string Key,Type,Role;
        public bool OriginalVisible;
        public ChassisDependency Dependency;
        public double[] StructuralYOffsets;
        public bool[] StructuralRear;
    }
    public sealed class MachineRoomManifest
    {
        public int SchemaVersion=1;
        public string TemplateId="machine-room-reference-v1";
        public List<MachineRoomBinding> Entities=new List<MachineRoomBinding>();
        public int DependencyVersion;
    }
    public sealed class MachineRoomSeed
    {
        public string SourceSha256;public double SW,SD;
        public List<MachineRoomSeedEntity> Entities;
    }
    public sealed class MachineRoomSeedEntity
    {
        public string Key,Type,Role,Prefix;
        public double MinX,MinY,MaxX,MaxY;
    }
    public sealed class MachineRoomSnapshot
    {
        public ShaftFrame Frame;
        public MachineRoomManifest Manifest;
        public Dictionary<string,Entity> Entities;
        public double SW,SD,CarRow,CwRow,PitchProjection,GovernorOffset,GovernorStartOffset;
        public MachineRoomExtensions Extensions;
        public T Get<T>(string id) where T:Entity{return (T)Entities[id];}
    }
    public static class MachineRoomDrawing
    {
        public const string Record="MachineRoomBindings";
        public const string LeftFace="MachineRoom.Shaft.LeftFace",RightFace="MachineRoom.Shaft.RightFace";
        public const string CarAxis="MachineRoom.Reference.CarAxis",CwAxis="MachineRoom.Reference.CounterweightAxis";
        public const string Pitch="MachineRoom.DeflectorSheave.LeftPitchProjection";
        public static bool IsPresent(Database db,Transaction tr){return ProjectDataStorage.Read<MachineRoomManifest>(db,tr,Record)!=null;}
        public static MachineRoomSnapshot Read(Database db,Transaction tr)
        {
            var m=ProjectDataStorage.Read<MachineRoomManifest>(db,tr,Record);
            if(m==null||m.SchemaVersion!=1)throw new InvalidOperationException("Missing/unsupported MachineRoom binding manifest.");
            var byKey=new Dictionary<string,Entity>();
            var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace],OpenMode.ForRead);
            foreach(ObjectId id in model)
            {
                var e=(Entity)tr.GetObject(id,OpenMode.ForRead);
                using(var data=e.GetXDataForApplication(DrawingReader.RegApp))
                {
                    if(data==null||data.AsArray().Length<2)continue;
                    string key=Convert.ToString(data.AsArray()[1].Value);if(!key.StartsWith("MachineRoom."))continue;
                    if(byKey.ContainsKey(key))throw new InvalidOperationException("Duplicate MachineRoom semantic ID: "+key);
                    byKey.Add(key,e);
                }
            }
            var entities=new Dictionary<string,Entity>();
            var declared=new HashSet<string>(m.Entities.Select(b=>b.Key));
            if(byKey.Keys.Any(k=>!declared.Contains(k)))throw new InvalidOperationException("Orphan MachineRoom semantic component outside manifest.");
            foreach(var b in m.Entities)
            {
                Entity e;if(!byKey.TryGetValue(b.Key,out e))throw new InvalidOperationException("Missing MachineRoom semantic ID: "+b.Key);
                if(e.GetRXClass().DxfName!=b.Type)throw new InvalidOperationException("Changed MachineRoom entity type: "+b.Key);
                entities.Add(b.Key,e);
            }
            var left=(Line)entities[LeftFace];var right=(Line)entities[RightFace];
            Vector3d x=right.StartPoint-left.StartPoint,y=left.EndPoint-left.StartPoint;
            if(x.Length<1||y.Length<1||Math.Abs(x.GetNormal().DotProduct(y.GetNormal()))>1e-6||x.GetNormal().CrossProduct(y.GetNormal()).DotProduct(Vector3d.ZAxis)<.999999)
                throw new InvalidOperationException("MachineRoom shaft faces must retain a rectangular, unmirrored XY frame.");
            var f=new ShaftFrame {Origin=left.StartPoint,XAxis=x.GetNormal(),YAxis=y.GetNormal(),ZAxis=Vector3d.ZAxis};
            var room=(Polyline)entities["MachineRoom.Room.InnerOutline"];
            var pts=Enumerable.Range(0,room.NumberOfVertices).Select(i=>f.Local(room.GetPoint3dAt(i))).ToArray();
            var projection=(Polyline)entities[Pitch];var pp=Enumerable.Range(0,projection.NumberOfVertices).Select(i=>f.Local(projection.GetPoint3dAt(i)).Y).ToArray();
            var governorDiameter=(RotatedDimension)entities["MachineRoom.Governor.DiameterDimension"];
            double governorStart=Math.Min(f.Local(governorDiameter.XLine1Point).Y,f.Local(governorDiameter.XLine2Point).Y)-f.Local(((Line)entities[CarAxis]).StartPoint).Y;
            return new MachineRoomSnapshot {Frame=f,Manifest=m,Entities=entities,SW=x.Length,SD=y.Length,CarRow=f.Local(((Line)entities[CarAxis]).StartPoint).Y,CwRow=f.Local(((Line)entities[CwAxis]).StartPoint).Y,PitchProjection=pp.Max()-pp.Min(),GovernorOffset=((RotatedDimension)entities["MachineRoom.Governor.OffsetDimension"]).Measurement,GovernorStartOffset=governorStart,Extensions=new MachineRoomExtensions {Left=-pts.Min(p=>p.X),Right=pts.Max(p=>p.X)-x.Length,Front=-pts.Min(p=>p.Y),Rear=pts.Max(p=>p.Y)-y.Length}};
        }
        public static void InitializeVerifiedSource(Database db,Transaction tr)
        {
            if(IsPresent(db,tr))return;
            var seed=Configuration.Read<MachineRoomSeed>("machine-room-seed.json");
            string hash;using(var sha=SHA256.Create())using(var file=File.Open(db.Filename,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");
            if(hash!=seed.SourceSha256)throw new InvalidOperationException("MachineRoom import requires the verified reference or a prepared semantic master.");
            var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace],OpenMode.ForRead);
            var all=model.Cast<ObjectId>().Select(id=>(Entity)tr.GetObject(id,OpenMode.ForRead)).ToArray();
            var wd=all.OfType<RotatedDimension>().Single(d=>d.DimensionText.StartsWith("SHAFT WIDTH"));
            var dd=all.OfType<RotatedDimension>().Single(d=>d.DimensionText.StartsWith("SHAFT DEPTH"));
            // One-time semantic discovery from dimension labels and native shaft faces, not handles or world coordinates.
            var x=(wd.XLine2Point-wd.XLine1Point).GetNormal();var y=Vector3d.ZAxis.CrossProduct(x);
            var origin=wd.XLine1Point-dd.Measurement*y;
            var f=new ShaftFrame{Origin=origin,XAxis=x,YAxis=y,ZAxis=Vector3d.ZAxis};
            ComponentBinding.Register(db,tr);var manifest=new MachineRoomManifest();var critical=new HashSet<string>();
            foreach(var entity in all)
            {
                var ext=entity.GeometricExtents;var lo=f.Local(ext.MinPoint);var hi=f.Local(ext.MaxPoint);
                var matches=seed.Entities.Where(b=>b.Type==entity.GetRXClass().DxfName&&Math.Abs(b.MinX-lo.X)<.03&&Math.Abs(b.MinY-lo.Y)<.03&&Math.Abs(b.MaxX-hi.X)<.03&&Math.Abs(b.MaxY-hi.Y)<.03).ToArray();
                if(matches.Length==0||matches.Select(b=>b.Role+"|"+b.Prefix+"|"+b.Key).Distinct().Count()!=1)throw new InvalidOperationException("Unrecognized/ambiguous machine-room geometry fingerprint: "+entity.GetRXClass().DxfName+" "+lo);
                var match=matches[0];string key=match.Key??match.Prefix+"."+Guid.NewGuid().ToString("N");
                if(!critical.Add(key))throw new InvalidOperationException("Duplicate critical source geometry: "+key);
                ComponentBinding.Tag(entity,key,match.Role);
                manifest.Entities.Add(new MachineRoomBinding {Key=key,Role=match.Role,Type=match.Type,OriginalVisible=entity.Visible});
            }
            ProjectDataStorage.Write(db,tr,Record,manifest);MachineRoomDependencies.Prepare(db,tr,Read(db,tr));
        }
        public static void Import(Database destination,string source,Point3d origin,Vector3d xAxis)
        {
            using(var check=destination.TransactionManager.StartTransaction())if(IsPresent(destination,check))throw new InvalidOperationException("This project already contains a machine-room view.");
            using(var importTransaction=destination.TransactionManager.StartTransaction())
            {
            using(var src=new Database(false,true))
            {
                src.ReadDwgFile(source,FileOpenMode.OpenForReadAndAllShare,true,null);src.CloseInput(true);
                MachineRoomSnapshot snapshot;Dictionary<string,ObjectId> sourceIds;
                using(var tr=src.TransactionManager.StartTransaction()){InitializeVerifiedSource(src,tr);snapshot=Read(src,tr);sourceIds=snapshot.Entities.ToDictionary(p=>p.Key,p=>p.Value.ObjectId);tr.Commit();}
                var ids=new ObjectIdCollection();
                using(var tr=src.TransactionManager.StartTransaction()){var bt=(BlockTable)tr.GetObject(src.BlockTableId,OpenMode.ForRead);foreach(ObjectId id in (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead))ids.Add(id);}
                ObjectId target;using(var tr=destination.TransactionManager.StartTransaction()){var bt=(BlockTable)tr.GetObject(destination.BlockTableId,OpenMode.ForRead);target=bt[BlockTableRecord.ModelSpace];}
                var mapping=new IdMapping();src.WblockCloneObjects(ids,target,mapping,DuplicateRecordCloning.MangleName,false);
                using(var tr=destination.TransactionManager.StartTransaction())
                {
                    double angle=Math.Atan2(xAxis.Y,xAxis.X)-Math.Atan2(snapshot.Frame.XAxis.Y,snapshot.Frame.XAxis.X);
                    var pose=Matrix3d.Displacement(origin-snapshot.Frame.Origin)*Matrix3d.Rotation(angle,Vector3d.ZAxis,snapshot.Frame.Origin);
                    foreach(ObjectId id in ids)if(mapping.Contains(id))((Entity)tr.GetObject(mapping[id].Value,OpenMode.ForWrite)).TransformBy(pose);
                    ComponentBinding.Register(destination,tr);
                    foreach(var b in snapshot.Manifest.Entities)ComponentBinding.Tag((Entity)tr.GetObject(mapping[sourceIds[b.Key]].Value,OpenMode.ForWrite),b.Key,b.Role);
                    ProjectDataStorage.Write(destination,tr,Record,snapshot.Manifest);Read(destination,tr);tr.Commit();
                }
            }
            importTransaction.Commit();
            }
        }
    }
}
