using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD;
using Elevator.Core.Geometry;
using Elevator.Core.Project;
public sealed class ProjectNativeTests
{
    static string Output {get{return Path.Combine(Configuration.Root,"output","phase1");}}
    static List<object> results=new List<object>();
    static string report="project-native-tests.json";
    static void Test(string name,Action action){try{action();results.Add(new{name,status="PASS"});}catch(System.Exception e){results.Add(new{name,status="FAIL",error=e.ToString()});}Configuration.WriteJson(Path.Combine(Output,report),results);}
    static void Check(bool value,string message){if(!value)throw new System.Exception(message);}
    static Dictionary<string,Entity> Snapshot(Database db)
    {
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var map=new Dictionary<string,Entity>();
            foreach(ObjectId id in (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead))
            {var e=(Entity)tr.GetObject(id,OpenMode.ForRead);var tag=SectionAADrawing.Tag(e);if(tag!=null&&tag.Length>0)map.Add(tag[0],(Entity)e.Clone());}
            return map;
        }
    }
    [CommandMethod("PROJECTFOUNDATIONTEST")]
    public void Run()
    {
        Directory.CreateDirectory(Output);var doc=Application.DocumentManager.MdiActiveDocument;var db=doc.Database;
        foreach(var size in new[]{new[]{1450.0,1600},new[]{1650.0,1800},new[]{1850.0,2000}})
        Test("Four_view_equivalence_"+size[0]+"_"+size[1],()=>
        {
            SectionAAProject aa;
            using(var tr=db.TransactionManager.StartTransaction()){var legacy=ProjectDataStorage.ReadProject(db,tr);legacy.SW=size[0];legacy.SD=size[1];ProjectDataStorage.Write(db,tr,"Project",legacy);aa=ProjectDataStorage.Read<SectionAAProject>(db,tr,SectionAADrawing.DataKey);tr.Commit();}
            Commands.Run("BaselineExistingWorkflow",new IViewUpdater[]{new PlanUpdater(),new MachineRoomUpdater(),new MachineRoomSectionUpdater()});
            Check(Commands.LastError==null,Commands.LastError);SectionAADrawing.Rebuild(db,aa,null);
            var before=Snapshot(db);ElevatorProject project;
            using(var tr=db.TransactionManager.StartTransaction())project=UnifiedProjectStorage.Read(db,tr);
            var generated=new ProjectDrawingService().GenerateProjectDrawings(doc,project,new GenerationOptions());
            Check(generated.Design.CanGenerate,"Generation blocked");
            if(size[0]==1450)Check(generated.Design.WrapAngleStatus==ValidationStatus.PASS&&generated.Design.CodeDiameterRatioStatus==ValidationStatus.FAIL,"250 mm code conflict must remain independent of wrap/layout");
            var after=Snapshot(db);try{
                Check(before.Keys.OrderBy(k=>k).SequenceEqual(after.Keys.OrderBy(k=>k)),"Semantic entity set changed");
                var frame=new ShaftFrame{Origin=Point3d.Origin,XAxis=Vector3d.XAxis,YAxis=Vector3d.YAxis,ZAxis=Vector3d.ZAxis};
                foreach(var e in before)VerticalSectionGeometryAudit.Compare(after[e.Key],e.Value,frame,e.Key);
                Check(generated.Design.CodeDiameterRatioStatus==ElevatorDesignEngine.Status(generated.Design.Plan.MachineRoom.CodeDiameterRatioStatus),"Code status independence");
            }finally{foreach(var e in before.Values)e.Dispose();foreach(var e in after.Values)e.Dispose();}
            db.SaveAs(Path.Combine(Output,"Unified_"+size[0]+"_"+size[1]+".dwg"),DwgVersion.AC1032);
        });
        Test("Legacy_command_write_synchronizes_unified",()=>{using(var tr=db.TransactionManager.StartTransaction()){var old=ProjectDataStorage.ReadProject(db,tr);old.ProjectName="Unified roundtrip مشروع";ProjectDataStorage.Write(db,tr,"Project",old);Check(UnifiedProjectStorage.Read(db,tr).Info.Name==old.ProjectName,"Unified data stale");tr.Commit();}});
        Test("A_A_legacy_write_preserves_floor_identity",()=>{using(var tr=db.TransactionManager.StartTransaction()){var aa=ProjectDataStorage.Read<SectionAAProject>(db,tr,SectionAADrawing.DataKey);string id=aa.Floors[1].FloorId;aa.Floors[1].Name="Lobby";ProjectDataStorage.Write(db,tr,SectionAADrawing.DataKey,aa);Check(UnifiedProjectStorage.Read(db,tr).Building.Floors.Single(f=>f.FloorId==id).Name=="Lobby","A-A bridge stale");} });
        Test("JSON_and_DWG_persistence",()=>{using(var tr=db.TransactionManager.StartTransaction()){string json=UnifiedProjectStorage.ExportJson(db,tr);var p=ProjectJson.Deserialize(json);UnifiedProjectStorage.Save(db,tr,p);Check(UnifiedProjectStorage.ExportJson(db,tr)==json,"JSON persistence drift");File.WriteAllText(Path.Combine(Output,"reopen-project.json"),json);tr.Commit();}db.SaveAs(Path.Combine(Output,"Unified_Reopen.dwg"),DwgVersion.AC1032);});
    }
    [CommandMethod("PROJECTFOUNDATIONREOPEN")]
    public void Reopen()
    {
        report="project-reopen-tests.json";
        var db=Application.DocumentManager.MdiActiveDocument.Database;
        Test("Fresh_process_unified_reopen",()=>{using(var tr=db.TransactionManager.StartTransaction()){Check(UnifiedProjectStorage.ExportJson(db,tr)==File.ReadAllText(Path.Combine(Output,"reopen-project.json")),"Reopened project changed");SectionAADrawing.Audit(db,tr);}});
        Configuration.WriteJson(Path.Combine(Output,"project-reopen-tests.json"),results);
    }
    [CommandMethod("PROJECTFOUNDATIONCREATE")]
    public void Create()
    {
        report="project-create-tests.json";var doc=Application.DocumentManager.MdiActiveDocument;
        Test("Generate_all_four_from_plan_template",()=>{
            var project=ProjectJson.Deserialize(File.ReadAllText(Path.Combine(Output,"reopen-project.json")));
            IProjectDrawingGenerator generator=new ProjectDrawingService(doc);
            var generated=generator.GenerateProjectDrawings(project,new GenerationOptions{ArrangeViews=true});
            Check(generated.EntityCounts.Count==4,"Not all four views generated");
            using(var tr=doc.Database.TransactionManager.StartTransaction())SectionAADrawing.Audit(doc.Database,tr);
            doc.Database.SaveAs(Path.Combine(Output,"Unified_AllFour.dwg"),DwgVersion.AC1032);
        });
        Test("Repeated_layout_is_deterministic",()=>{
            var before=Snapshot(doc.Database);ElevatorProject project;using(var tr=doc.Database.TransactionManager.StartTransaction())project=UnifiedProjectStorage.Read(doc.Database,tr);
            new ProjectDrawingService().GenerateProjectDrawings(doc,project,new GenerationOptions{ArrangeViews=true});
            var after=Snapshot(doc.Database);try{var f=new ShaftFrame{Origin=Point3d.Origin,XAxis=Vector3d.XAxis,YAxis=Vector3d.YAxis,ZAxis=Vector3d.ZAxis};Check(before.Count==after.Count,"Count changed");foreach(var pair in before)VerticalSectionGeometryAudit.Compare(after[pair.Key],pair.Value,f,pair.Key);}finally{foreach(var e in before.Values)e.Dispose();foreach(var e in after.Values)e.Dispose();}
        });
    }
}
