using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD.Common;
using Elevator.AutoCAD.Plan;
using Elevator.Core.Project;
using Exception=System.Exception;

public sealed class ArchitectureTests
{
    private static List<object> results=new List<object>();
    private static int goldenNumber;
    [CommandMethod("ELEVATORTESTINPUT")]
    public void SetInput()
    {
        var ed=Application.DocumentManager.MdiActiveDocument.Editor;
        var sw=ed.GetDouble("\nTest SW: ");var sd=ed.GetDouble("\nTest SD: ");Input(sw.Value,sd.Value);
    }
    [CommandMethod("ELEVATORGOLDENCHECK")]
    public void CheckGolden()
    {
        int number=++goldenNumber;
        Log("Saved DWG versus pre-refactor golden case "+number,()=>{Assert(Commands.LastOutput!=null,Commands.LastError);Golden(number);});
    }
    private static void Assert(bool condition,string message) {if(!condition)throw new Exception(message);}
    private static void Log(string name,Action action)
    {
        try {action();results.Add(new {status="PASS",test=name});}
        catch(Exception ex) {results.Add(new {status="FAIL",test=name,error=ex.ToString()});}
        Configuration.WriteJson(Path.Combine(Configuration.Root,"tests","architecture-results.json"),results);
    }
    private static void Input(double sw,double sd)
    {
        var db=Application.DocumentManager.MdiActiveDocument.Database;
        using(var tr=db.TransactionManager.StartTransaction())
        {
            LegacyMigration.Ensure(db,tr);
            var p=ProjectDataStorage.ReadProject(db,tr)??new ProjectInput {ProjectName="Regression \u0645\u0635\u0639\u062f",Stops=5,FloorHeights=new double[]{3000,3100,3200,3300},Extensions=new Dictionary<string,object>{{"futureOption","retained"}}};
            p.SW=sw;p.SD=sd;ProjectDataStorage.Write(db,tr,"Project",p);tr.Commit();
        }
    }
    private static void Near(Point3d a,Point3d b,string message) {Assert(a.DistanceTo(b)<0.02,message+": "+a.DistanceTo(b));}
    private static void Compare(Entity a,Entity b,ShaftFrame af,ShaftFrame bf,string key)
    {
        Assert(a.GetRXClass().DxfName==b.GetRXClass().DxfName,"type "+key);
        Assert(a.Layer==b.Layer&&a.Linetype==b.Linetype&&a.LineWeight==b.LineWeight&&a.Color.ColorIndex==b.Color.ColorIndex,"style "+key);
        var pa=a as Polyline;var pb=b as Polyline;
        if(pa!=null)
        {
            Assert(pa.NumberOfVertices==pb.NumberOfVertices&&pa.Closed==pb.Closed,"topology "+key);
            for(int i=0;i<pa.NumberOfVertices;i++) {Near(af.Local(pa.GetPoint3dAt(i)),bf.Local(pb.GetPoint3dAt(i)),key);Assert(Math.Abs(pa.GetBulgeAt(i)-pb.GetBulgeAt(i))<1e-9,"bulge "+key);}
        }
        else if(a is Curve)
        {
            var ca=(Curve)a;var cb=(Curve)b;
            for(int i=0;i<=64;i++) Near(af.Local(ca.GetPointAtParameter(ca.StartParam+(ca.EndParam-ca.StartParam)*i/64)),bf.Local(cb.GetPointAtParameter(cb.StartParam+(cb.EndParam-cb.StartParam)*i/64)),key);
        }
        var da=a as RotatedDimension;var db=b as RotatedDimension;
        if(da!=null)
        {
            DrawingUpdater.AssertNear(da.Measurement,db.Measurement,key);
            Near(af.Local(da.XLine1Point),bf.Local(db.XLine1Point),key+" extension1");Near(af.Local(da.XLine2Point),bf.Local(db.XLine2Point),key+" extension2");
            Near(af.Local(da.DimLinePoint),bf.Local(db.DimLinePoint),key+" dimension line");Near(af.Local(da.TextPosition),bf.Local(db.TextPosition),key+" text location");
            Assert(da.DimensionText==db.DimensionText,"dimension text "+key);
        }
        if(a is MText)
        {
            var x=(MText)a;var y=(MText)b;Assert(x.Contents==y.Contents&&x.TextHeight==y.TextHeight&&x.Width==y.Width,"MText "+key);Near(af.Local(x.Location),bf.Local(y.Location),key);
        }
        if(a is Hatch)
        {
            DrawingUpdater.AssertNear(((Hatch)a).Area,((Hatch)b).Area,key+" hatch");
            Near(af.Local(a.GeometricExtents.MinPoint),bf.Local(b.GeometricExtents.MinPoint),key+" hatch extent min");
            Near(af.Local(a.GeometricExtents.MaxPoint),bf.Local(b.GeometricExtents.MaxPoint),key+" hatch extent max");
        }
        if(a is MLeader)
        {
            var x=(MLeader)a;var y=(MLeader)b;Near(af.Local(x.TextLocation),bf.Local(y.TextLocation),key+" leader text");
            foreach(int leader in x.GetLeaderIndexes())foreach(int line in x.GetLeaderLineIndexes(leader))
            {Near(af.Local(x.GetFirstVertex(line)),bf.Local(y.GetFirstVertex(line)),key);Near(af.Local(x.GetLastVertex(line)),bf.Local(y.GetLastVertex(line)),key);}
        }
    }
    private static void Golden(int number)
    {
        string oldRoot=Path.Combine(Directory.GetParent(Configuration.Root).FullName,"legacy","ElevatorPlanGenerator");
        var rows=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<List<Dictionary<string,object>>>(File.ReadAllText(Path.Combine(oldRoot,"tests","cad-test-results.json")));
        string file=Path.Combine(oldRoot,"output",Path.GetFileName(Convert.ToString(rows.Single(r=>Convert.ToString(r["kind"])=="current-drawing"&&Convert.ToInt32(r["test"])==number)["output"])));
        using(var golden=new Database(false,true))
        using(var saved=new Database(false,true))
        {
            golden.ReadDwgFile(file,FileOpenMode.OpenForReadAndAllShare,true,null);golden.CloseInput(true);
            saved.ReadDwgFile(Commands.LastOutput,FileOpenMode.OpenForReadAndAllShare,true,null);saved.CloseInput(true);
            using(var gt=golden.TransactionManager.StartTransaction())
            using(var st=saved.TransactionManager.StartTransaction())
            {
                LegacyMigration.Ensure(golden,gt);
                var g=DrawingReader.Read(golden,gt);var s=DrawingReader.Read(saved,st);
                Assert(g.ModelEntityCount==s.ModelEntityCount,"golden entity count");
                foreach(var pair in g.Entities)Compare(s.Entities[pair.Key],pair.Value,s.Frame,g.Frame,pair.Key);
                var p=ProjectDataStorage.ReadProject(saved,st);
                Assert(p!=null&&p.SW==Commands.LastElevatorParameters.SW&&p.Stops==5&&p.ProjectName=="Regression \u0645\u0635\u0639\u062f"&&Convert.ToString(p.Extensions["futureOption"])=="retained","project round trip");
            }
        }
    }
    [CommandMethod("ELEVATORARCHITECTURE")]
    public void Run()
    {
        Log("Stored data reuse without prompts",()=>{new ElevatorUpdateCommand().Execute();Assert(Commands.LastOutput!=null,Commands.LastError);});
        Log("Changed handle, changed layer, extra unbound geometry",EditedTemplate);
        Log("Duplicate semantic identifier rejected",()=>InvalidTag(true));
        Log("Missing semantic identifier rejected",()=>InvalidTag(false));
        Log("Future schema rejected",()=>{
            var db=Application.DocumentManager.MdiActiveDocument.Database;
            using(var tr=db.TransactionManager.StartTransaction()) {var p=ProjectDataStorage.ReadProject(db,tr);p.SchemaVersion=2;ProjectDataStorage.Write(db,tr,"Project",p);bool rejected=false;try{ProjectDataStorage.ReadProject(db,tr);}catch(InvalidOperationException){rejected=true;}Assert(rejected,"future schema accepted");}
        });
        Log("Unconfigured family rejected",()=>{bool rejected=false;try{CommonRules.ValidateProject(new ProjectInput{ElevatorType=ElevatorType.Hydraulic},ElevatorProjectCommand.Family(new ProjectInput{ElevatorType=ElevatorType.Hydraulic}));}catch(ArgumentException){rejected=true;}Assert(rejected,"pending family accepted");});
        Log("Prepared master written",PrepareMaster);
    }
    private static void EditedTemplate()
    {
        var db=Application.DocumentManager.MdiActiveDocument.Database;
        string key="Plan.CarGuides.Left.Contact";
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var s=DrawingReader.Read(db,tr);var entity=s.Entities[key];string old=entity.Handle.ToString();
            var model=(BlockTableRecord)tr.GetObject(entity.OwnerId,OpenMode.ForWrite);
            var clone=(Entity)entity.Clone();model.AppendEntity(clone);tr.AddNewlyCreatedDBObject(clone,true);clone.Layer="0";
            entity.UpgradeOpen();entity.Erase();Assert(clone.Handle.ToString()!=old,"handle did not change");
            var extra=new Line(new Point3d(1,2,0),new Point3d(3,4,0));model.AppendEntity(extra);tr.AddNewlyCreatedDBObject(extra,true);tr.Commit();
        }
        Input(1800,2000);new UpdateAllCommand().Execute();Assert(Commands.LastOutput!=null,Commands.LastError);
        using(var saved=new Database(false,true))
        {
            saved.ReadDwgFile(Commands.LastOutput,FileOpenMode.OpenForReadAndAllShare,true,null);saved.CloseInput(true);
            using(var tr=saved.TransactionManager.StartTransaction()) {var s=DrawingReader.Read(saved,tr);Assert(s.ModelEntityCount==522,"unbound entity removed");Assert(s.Entities[key].Layer=="0","layer edit reverted");DrawingUpdater.Validate(s,Commands.LastElevatorParameters,Commands.LastDimensions);}
        }
    }
    private static void InvalidTag(bool duplicate)
    {
        var db=Application.DocumentManager.MdiActiveDocument.Database;
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var s=DrawingReader.Read(db,tr);var entity=s.Entities["Plan.Shaft.InnerOutline.EntranceDatum"];
            if(duplicate) {var model=(BlockTableRecord)tr.GetObject(entity.OwnerId,OpenMode.ForWrite);var clone=(Entity)entity.Clone();model.AppendEntity(clone);tr.AddNewlyCreatedDBObject(clone,true);}
            else {entity.UpgradeOpen();using(var rb=new ResultBuffer(new TypedValue(1001,DrawingReader.RegApp)))entity.XData=rb;}
            bool rejected=false;try{DrawingReader.Read(db,tr);}catch(InvalidOperationException){rejected=true;}Assert(rejected,"invalid semantic binding accepted");
        }
    }
    private static void PrepareMaster()
    {
        string source=Path.Combine(Directory.GetParent(Configuration.Root).FullName,"legacy","ElevatorPlanGenerator","templates","plan codex.dwg");
        using(var db=new Database(false,true))
        {
            db.ReadDwgFile(source,FileOpenMode.OpenForReadAndAllShare,true,null);db.CloseInput(true);
            using(var tr=db.TransactionManager.StartTransaction()) {LegacyMigration.Ensure(db,tr);tr.Commit();}
            db.SaveAs(Path.Combine(Configuration.Root,"Templates","ExistingPlan_Master.dwg"),DwgVersion.AC1032);
        }
    }
}
