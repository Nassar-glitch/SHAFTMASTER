using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD;
using Elevator.Core;
using Exception=System.Exception;

public sealed class PlanCompletenessTests
{
    private static List<object> results=new List<object>();
    private static int checkNumber;
    private static Dictionary<string,string> beforeFailure;
    private const string Summary="Plan.Annotation.ProjectSummary";
    private const string Marker="Plan.CarMark.Part005";
    private const string Recipe="P6 / {{CarWidth}}x{{CarDepth}} / DBG {{CarDBG}} / TZ {{TZ}}";
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Log(string name,Action action)
    {
        try{action();results.Add(new{status="PASS",test=name});}
        catch(Exception ex){results.Add(new{status="FAIL",test=name,error=ex.ToString()});}
        Configuration.WriteJson(Path.Combine(Configuration.Root,"tests","plan-results.json"),results);
    }
    [CommandMethod("ELEVATORPLANSETUPTEST")]
    public void Setup()
    {
        Log("Configure project-driven MText, leader and new semantic annotation",()=>{
            var db=Application.DocumentManager.MdiActiveDocument.Database;
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var s=DrawingReader.Read(db,tr);var p=ProjectDataStorage.ReadProject(db,tr);
                Check(p.PlanTitle=="ELEVATOR PLAN"&&p.ProjectName=="North Tower","Plan options command did not store inputs.");
                p.ProjectName="North {A}\\Tower\n\u0645\u0635\u0639\u062f";ProjectDataStorage.Write(db,tr,"Project",p);
                var manifest=ComponentBinding.ReadManifest(db,tr);
                manifest.Entities.Single(b=>b.Key==Marker).AnnotationTemplate=Recipe;
                manifest.Entities.Single(b=>b.Key=="Plan.DoorLeader.Part001").AnnotationTemplate="{{DoorWidth}}";
                var title=s.Get<MText>(PlanAnnotations.TitleId);var text=(MText)title.Clone();
                text.Location=title.Location-title.TextHeight*3*s.Frame.YAxis;
                var model=(BlockTableRecord)tr.GetObject(title.OwnerId,OpenMode.ForWrite);model.AppendEntity(text);tr.AddNewlyCreatedDBObject(text,true);
                ComponentBinding.Tag(text,Summary,"centerFront");
                manifest.Entities.Add(new Binding{Key=Summary,Type="MTEXT",Role="centerFront",AnnotationTemplate="Project: {{ProjectName}} / SW {{SW}} SD {{SD}} / return {{Clearance.cabinDoorReturnMm}}"});
                var extra=(Line)s.Get<Line>("Plan.Cabin.Inner.Left").Clone();model.AppendEntity(extra);tr.AddNewlyCreatedDBObject(extra,true);
                using(var rb=new ResultBuffer(new TypedValue(1001,DrawingReader.RegApp)))extra.XData=rb;
                ProjectDataStorage.Write(db,tr,"PlanBindings",manifest);tr.Commit();
            }
        });
    }
    [CommandMethod("ELEVATORPLANCHECKTEST")]
    public void Current()
    {
        int number=++checkNumber;
        Log("Annotations, hatch loops, coverage and saved reopen "+number,()=>{
            Check(Commands.LastOutput!=null,Commands.LastError);
            using(var saved=new Database(false,true))
            {
                saved.ReadDwgFile(Commands.LastOutput,FileOpenMode.OpenForReadAndAllShare,true,null);saved.CloseInput(true);
                using(var tr=saved.TransactionManager.StartTransaction())
                {
                    var s=DrawingReader.Read(saved,tr);var p=ProjectDataStorage.ReadProject(saved,tr);
                    DrawingUpdater.Validate(s,Commands.LastElevatorParameters,Commands.LastDimensions);
                    double width=number==1?1450:1800;double depth=number==1?1600:2000;
                    Check(p.SW==width&&p.SD==depth,"Unexpected stored input.");
                    Check(PlanAnnotations.Text(s.Entities[Marker])==(number==1?"P6 / 1050x1200 / DBG 1150 / TZ 750":"P6 / 1400x1600 / DBG 1500 / TZ 950"),"Derived label mismatch.");
                    Check(PlanAnnotations.Text(s.Entities["Plan.DoorLeader.Part001"])==(number==1?"900":"1000"),"Leader text mismatch.");
                    string title=PlanAnnotations.Text(s.Entities[PlanAnnotations.TitleId]);
                    Check(title.Contains("ELEVATOR PLAN 1:8")&&title.Contains("\\C6;"),"Title/style/scale mismatch.");
                    string summary=PlanAnnotations.Text(s.Entities[Summary]);
                    Check(summary.Contains("North \\{A\\}\\\\Tower\\P\u0645\u0635\u0639\u062f"),"Unicode or MText escaping failed.");
                    Check(summary.EndsWith(number==1?"return 75":"return 200"),"Clearance annotation mismatch.");
                    var coverage=PlanCoverage.Read(saved,tr,s);
                    Check(coverage.BoundEntities==522&&coverage.ModelEntities==523&&coverage.UnboundEntities==1&&coverage.UnboundByType["LINE"]==1,"Coverage mismatch.");
                    Check(coverage.DrivenAnnotations==4&&coverage.Hatches==8&&coverage.Dimensions==31,"Component counts changed.");
                    foreach(var hatch in s.Entities.Values.OfType<Hatch>())Check(hatch.NumberOfLoops>0&&hatch.Area>0,"Empty/invalid hatch.");
                    Check(ComponentBinding.ReadManifest(saved,tr).Entities.Single(b=>b.Key==Summary).AnnotationTemplate.Contains("{{SW}}"),"Recipe lost on save.");
                }
            }
        });
    }
    [CommandMethod("ELEVATORPLANPOSETEST")]
    public void Pose()
    {
        var db=Application.DocumentManager.MdiActiveDocument.Database;
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace],OpenMode.ForRead);
            var matrix=Matrix3d.Displacement(new Vector3d(10000,-20000,0))*Matrix3d.Rotation(0.73,Vector3d.ZAxis,Point3d.Origin);
            foreach(ObjectId id in model)((Entity)tr.GetObject(id,OpenMode.ForWrite)).TransformBy(matrix);
            tr.Commit();
        }
    }
    private static Dictionary<string,string> State(DrawingSnapshot snapshot)
    {
        var state=new Dictionary<string,string>();
        foreach(var pair in snapshot.Entities)
        {
            var e=pair.Value;string value=e.GeometricExtents.ToString();
            if(e is MText||e is MLeader)value+=PlanAnnotations.Text(e);
            if(e is Dimension)value+=((Dimension)e).Measurement.ToString("R");
            var pl=e as Polyline;if(pl!=null)for(int i=0;i<pl.NumberOfVertices;i++)value+=pl.GetPoint3dAt(i).ToString();
            state[pair.Key]=value;
        }
        return state;
    }
    [CommandMethod("ELEVATORPLANBADTOKENTEST")]
    public void BadToken()
    {
        var db=Application.DocumentManager.MdiActiveDocument.Database;
        using(var tr=db.TransactionManager.StartTransaction())
        {
            beforeFailure=State(DrawingReader.Read(db,tr));var manifest=ComponentBinding.ReadManifest(db,tr);
            manifest.Entities.Single(b=>b.Key==Marker).AnnotationTemplate="{{NoSuchParameter}}";
            ProjectDataStorage.Write(db,tr,"PlanBindings",manifest);tr.Commit();
        }
    }
    [CommandMethod("ELEVATORPLANREJECTTEST")]
    public void Rejected()
    {
        Log("Unknown annotation token rejects update without geometry/text changes",()=>{
            Check(Commands.LastError!=null&&Commands.LastError.Contains("NoSuchParameter")&&Commands.LastOutput==null,"Bad token not rejected.");
            var db=Application.DocumentManager.MdiActiveDocument.Database;
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var current=State(DrawingReader.Read(db,tr));foreach(var pair in beforeFailure)Check(current[pair.Key]==pair.Value,"Failed update changed "+pair.Key);
                var m=ComponentBinding.ReadManifest(db,tr);m.Entities.Single(b=>b.Key==Marker).AnnotationTemplate=Recipe;ProjectDataStorage.Write(db,tr,"PlanBindings",m);
                var p=ProjectDataStorage.ReadProject(db,tr);p.PlanTitle=null;ProjectDataStorage.Write(db,tr,"Project",p);tr.Commit();
            }
        });
    }
    [CommandMethod("ELEVATORPLANRESETCHECK")]
    public void Reset()
    {
        Log("Clearing optional title restores original caption and preserves recipes",()=>{
            Check(Commands.LastOutput!=null,Commands.LastError);var db=Application.DocumentManager.MdiActiveDocument.Database;
            using(var tr=db.TransactionManager.StartTransaction()) {var s=DrawingReader.Read(db,tr);Check(PlanAnnotations.Text(s.Entities[PlanAnnotations.TitleId])=="{\\Fcent|c0;\\L\\C6;SHAFT AND CAR 1:8}","Default title not restored.");}
        });
        Log("Nonfinite dimension assertions fail",()=>{bool rejected=false;try{DrawingUpdater.AssertNear(Double.NaN,100,"test");}catch(InvalidOperationException){rejected=true;}Check(rejected,"NaN accepted.");});
    }
}
