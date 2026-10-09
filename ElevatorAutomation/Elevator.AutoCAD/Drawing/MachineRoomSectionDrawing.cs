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
    public sealed class SectionBinding
    {
        public string Key,Role,Type;public LocalPoint[] SourcePoints;public bool Visible;
        public double[] Bounds;public double? Radius;
    }
    public sealed class SectionManifest
    {
        public int SchemaVersion=1;public SectionReference Reference;public List<SectionBinding> Entities=new List<SectionBinding>();
    }
    public sealed class SectionSeed
    {
        public string SourceSha256;public SectionReference Reference;public List<MachineRoomSeedEntity> Entities;
    }
    public sealed class SectionSnapshot
    {
        public ShaftFrame Frame;public SectionManifest Manifest;public Dictionary<string,Entity> Entities;
    }
    public static class MachineRoomSectionDrawing
    {
        public static void DetachSourceDimensionAssociations(Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            ObjectId[] dimensions;
            using(var tr=doc.Database.TransactionManager.StartTransaction())
            {
                if(!IsPresent(doc.Database,tr))return;
                var s=Read(doc.Database,tr);
                dimensions=s.Entities.Values.OfType<Dimension>().Where(d=>d.GetPersistentReactorIds().Cast<ObjectId>().Any(id=>!id.IsErased&&id.ObjectClass.DxfName=="DIMASSOC")).Select(d=>d.ObjectId).ToArray();
            }
            // The semantic recipe owns these dimension endpoints. Native source associations
            // otherwise reattach them to obsolete source osnaps at command boundaries.
            if(dimensions.Length>0)doc.Editor.Command("_.DIMDISASSOCIATE",Autodesk.AutoCAD.EditorInput.SelectionSet.FromObjectIds(dimensions),"");
        }
        public const string Record="MachineRoomSectionBindings",FrameKey="MachineRoomSection.Reference.Frame";
        public static bool IsPresent(Database db,Transaction tr){return ProjectDataStorage.Read<SectionManifest>(db,tr,Record)!=null;}
        public static SectionSnapshot Read(Database db,Transaction tr)
        {
            var m=ProjectDataStorage.Read<SectionManifest>(db,tr,Record);if(m==null||m.SchemaVersion!=1)throw new InvalidOperationException("Section manifest missing or unsupported.");
            var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);var all=new Dictionary<string,Entity>();
            foreach(ObjectId id in model)
            {
                var e=(Entity)tr.GetObject(id,OpenMode.ForRead);using(var xd=e.GetXDataForApplication(DrawingReader.RegApp))
                {
                    if(xd==null||xd.AsArray().Length<2)continue;string key=Convert.ToString(xd.AsArray()[1].Value);if(!key.StartsWith("MachineRoomSection."))continue;
                    if(all.ContainsKey(key))throw new InvalidOperationException("Duplicate section semantic ID: "+key);all.Add(key,e);
                }
            }
            foreach(var b in m.Entities){if(!all.ContainsKey(b.Key))throw new InvalidOperationException("Missing section semantic ID: "+b.Key);if(all[b.Key].GetRXClass().DxfName!=b.Type)throw new InvalidOperationException("Changed section entity type: "+b.Key);}
            if(all.Count!=m.Entities.Count)throw new InvalidOperationException("Orphan section semantic component.");
            var axis=(Line)all[FrameKey];var x=(axis.EndPoint-axis.StartPoint).GetNormal();if(Math.Abs(x.Z)>1e-8)throw new InvalidOperationException("Section frame must remain in the XY drawing plane.");
            return new SectionSnapshot{Manifest=m,Entities=all,Frame=new ShaftFrame{Origin=axis.StartPoint,XAxis=x,YAxis=Vector3d.ZAxis.CrossProduct(x),ZAxis=Vector3d.ZAxis}};
        }
        public static void Prepare(Database db,Transaction tr)
        {
            if(IsPresent(db,tr))return;var seed=Configuration.Read<SectionSeed>("section-seed.json");string hash;
            using(var sha=SHA256.Create())using(var f=File.Open(db.Filename,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))hash=BitConverter.ToString(sha.ComputeHash(f)).Replace("-","");
            if(hash!=seed.SourceSha256)throw new InvalidOperationException("Section bootstrap requires the reviewed source snapshot or a semantic master.");
            var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);var entities=model.Cast<ObjectId>().Select(id=>(Entity)tr.GetObject(id,OpenMode.ForRead)).ToArray();
            var horizontal=entities.OfType<RotatedDimension>().Single(d=>Math.Abs(d.Measurement-675)<.02);var height=entities.OfType<RotatedDimension>().Single(d=>Math.Abs(d.Measurement-450)<.02);
            var x=Vector3d.XAxis.RotateBy(horizontal.Rotation,Vector3d.ZAxis);var y=Vector3d.ZAxis.CrossProduct(x);
            var origin=horizontal.XLine2Point+y*((height.XLine1Point-horizontal.XLine2Point).DotProduct(y));
            var frame=new ShaftFrame{Origin=origin,XAxis=x,YAxis=y,ZAxis=Vector3d.ZAxis};var manifest=new SectionManifest{Reference=seed.Reference};ComponentBinding.Register(db,tr);
            foreach(var e in entities)
            {
                var ex=e.GeometricExtents;var lo=frame.Local(ex.MinPoint);var hi=frame.Local(ex.MaxPoint);
                var matches=seed.Entities.Where(b=>b.Type==e.GetRXClass().DxfName&&Math.Abs(b.MinX-lo.X)<.03&&Math.Abs(b.MinY-lo.Y)<.03&&Math.Abs(b.MaxX-hi.X)<.03&&Math.Abs(b.MaxY-hi.Y)<.03).ToArray();
                if(matches.Length==0||matches.Select(b=>b.Role+"|"+b.Key+"|"+b.Prefix).Distinct().Count()!=1)throw new InvalidOperationException("Unreviewed source section entity: "+e.GetRXClass().DxfName+" "+lo);
                var found=matches[0];string key=found.Key??found.Prefix+"."+Guid.NewGuid().ToString("N");
                ComponentBinding.Tag(e,key,found.Role);
                manifest.Entities.Add(new SectionBinding{Key=key,Role=found.Role,Type=found.Type,Visible=e.Visible,SourcePoints=MachineRoomDependencies.Points(e,frame),Bounds=new[]{lo.X,lo.Y,hi.X,hi.Y},Radius=e is Circle?(double?)((Circle)e).Radius:null});
            }
            model.UpgradeOpen();var axis=new Line(origin,origin+x);axis.SetDatabaseDefaults(db);axis.Layer="Defpoints";model.AppendEntity(axis);tr.AddNewlyCreatedDBObject(axis,true);ComponentBinding.Tag(axis,FrameKey,"frame");manifest.Entities.Add(new SectionBinding{Key=FrameKey,Role="frame",Type="LINE",Visible=true});
            foreach(string key in new[]{"MachineRoomSection.Rope.TractionWrap","MachineRoomSection.Rope.DeflectorWrap"})
            {
                var arc=new Arc(origin,Vector3d.ZAxis,1,0,Math.PI);arc.SetDatabaseDefaults(db);arc.Layer="CL";model.AppendEntity(arc);tr.AddNewlyCreatedDBObject(arc,true);ComponentBinding.Tag(arc,key,"generatedRope");manifest.Entities.Add(new SectionBinding{Key=key,Role="generatedRope",Type="ARC",Visible=true});
            }
            var note=new MText{Location=origin,TextHeight=20,Width=900,Contents="SECTION REQUIRES UPDATEALL"};note.SetDatabaseDefaults(db);model.AppendEntity(note);tr.AddNewlyCreatedDBObject(note,true);ComponentBinding.Tag(note,"MachineRoomSection.Annotation.EngineeringStatus","status");manifest.Entities.Add(new SectionBinding{Key="MachineRoomSection.Annotation.EngineeringStatus",Role="status",Type="MTEXT",Visible=true});
            ProjectDataStorage.Write(db,tr,Record,manifest);Read(db,tr);
        }
        public static void Import(Database destination,string source,Point3d origin,Vector3d xAxis)
        {
            using(var outer=destination.TransactionManager.StartTransaction())
            using(var src=new Database(false,true))
            {
                if(IsPresent(destination,outer))throw new InvalidOperationException("Section already installed.");src.ReadDwgFile(source,FileOpenMode.OpenForReadAndAllShare,true,null);src.CloseInput(true);
                SectionSnapshot s;Dictionary<string,ObjectId> ids;ObjectIdCollection all=new ObjectIdCollection();
                using(var tr=src.TransactionManager.StartTransaction()){Prepare(src,tr);s=Read(src,tr);ids=s.Entities.ToDictionary(p=>p.Key,p=>p.Value.ObjectId);foreach(var id in ids.Values)all.Add(id);tr.Commit();}
                var bt=(BlockTable)outer.GetObject(destination.BlockTableId,OpenMode.ForRead);var target=bt[BlockTableRecord.ModelSpace];var map=new IdMapping();src.WblockCloneObjects(all,target,map,DuplicateRecordCloning.MangleName,false);
                double angle=Math.Atan2(xAxis.Y,xAxis.X)-Math.Atan2(s.Frame.XAxis.Y,s.Frame.XAxis.X);var pose=Matrix3d.Displacement(origin-s.Frame.Origin)*Matrix3d.Rotation(angle,Vector3d.ZAxis,s.Frame.Origin);
                ComponentBinding.Register(destination,outer);
                foreach(var b in s.Manifest.Entities){var e=(Entity)outer.GetObject(map[ids[b.Key]].Value,OpenMode.ForWrite);e.TransformBy(pose);ComponentBinding.Tag(e,b.Key,b.Role);}
                ProjectDataStorage.Write(destination,outer,Record,s.Manifest);Read(destination,outer);outer.Commit();
            }
        }
    }
}

