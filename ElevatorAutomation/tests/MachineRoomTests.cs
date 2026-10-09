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
public sealed class MachineRoomTests
{
    private static List<object> results=new List<object>();
    private static int sequence;
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Log(string name,Action action)
    {
        try{action();results.Add(new {status="PASS",test=name});}
        catch(Exception ex){results.Add(new {status="FAIL",test=name,error=ex.ToString()});}
        Configuration.WriteJson(Path.Combine(Configuration.Root,"tests","machine-room-results.json"),results);
    }
    [CommandMethod("MRTESTPREPARE")]
    public void Prepare()
    {
        // The reference drawing is a local project file kept outside the repository (ignored); without it the suite is skipped.
        string reference=Path.Combine(Directory.GetParent(Configuration.Root).FullName,"Machine room codex.dwg");
        if(!File.Exists(reference))
        {
            results.Add(new {status="SKIP",test="Prepare semantic master and import second view",reason="Local reference drawing not found (not in the repository): "+reference});
            Configuration.WriteJson(Path.Combine(Configuration.Root,"tests","machine-room-results.json"),results);
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nSKIPPED: local reference drawing not found: "+reference+"\n");
            return;
        }
        Log("Prepare semantic master and import second view",()=>{
            string source=reference;
            string master=Path.Combine(Configuration.Root,"Templates","MachineRoom_Master.dwg");
            using(var db=new Database(false,true))
            {
                db.ReadDwgFile(source,FileOpenMode.OpenForReadAndAllShare,true,null);db.CloseInput(true);
                using(var tr=db.TransactionManager.StartTransaction()) {MachineRoomDrawing.InitializeVerifiedSource(db,tr);var s=MachineRoomDrawing.Read(db,tr);Check(s.Entities.Count==1354,"Reference entity count");Check(s.Manifest.Entities.Count(b=>b.Key.StartsWith("MachineRoom.DeflectorSheave."))==7,"Deflector assembly classification");tr.Commit();}
                db.SaveAs(master,DwgVersion.AC1032);
            }
            var doc=Application.DocumentManager.MdiActiveDocument;
            Point3d position;Vector3d axis;
            using(var tr=doc.Database.TransactionManager.StartTransaction()) {var plan=DrawingReader.Read(doc.Database,tr);position=plan.Frame.World(new Point3d(plan.ElevatorParameters.SW*3,0,0));axis=plan.Frame.XAxis;}
            MachineRoomDrawing.Import(doc.Database,master,position,axis);
        });
    }
    [CommandMethod("MRTESTCHECK")]
    public void Verify()
    {
        int number=++sequence;
        Log("Both views, sheave assembly and saved reopen "+number,()=>{
            Check(Commands.LastError==null&&Commands.LastOutput!=null,Commands.LastError??"Missing output");
            using(var db=new Database(false,true))
            {
                db.ReadDwgFile(Commands.LastOutput,FileOpenMode.OpenForReadAndAllShare,true,null);db.CloseInput(true);
                using(var tr=db.TransactionManager.StartTransaction())
                {
                    var p=DrawingReader.Read(db,tr);var mr=MachineRoomDrawing.Read(db,tr);var design=Commands.LastElevatorParameters;
                    DrawingUpdater.Validate(p,design,Commands.LastDimensions);MachineRoomUpdater.Validate(mr,design);
                    DrawingUpdater.AssertNear(p.ElevatorParameters.CarRow,mr.CarRow,"Cross-view car axis");DrawingUpdater.AssertNear(p.ElevatorParameters.CwRow,mr.CwRow,"Cross-view CW axis");
                    Check(mr.Entities.Count==1354,"Machine-room entity lost");
                    DrawingUpdater.AssertNear(mr.Extensions.Left,525,"Left extension");DrawingUpdater.AssertNear(mr.Extensions.Right,525,"Right extension");DrawingUpdater.AssertNear(mr.Extensions.Front,900,"Front extension");DrawingUpdater.AssertNear(mr.Extensions.Rear,0,"Rear extension");
                    var rules=ProjectDataStorage.Read<List<EngineeringRule>>(db,tr,"MachineRoomEngineeringRules");Check(rules!=null&&rules.Any(r=>r.RuleId=="MR-DS-001"&&r.RequirementType==RequirementType.Project),"Rule provenance missing");
                    var statuses=ProjectDataStorage.Read<Dictionary<string,string>>(db,tr,"MachineRoomValidation");
                    Check(statuses!=null&&statuses["WrapAngleStatus"]=="NOT YET EVALUABLE","Uncalculated wrap status persisted");
                    Check(statuses["LayoutStatus"]==(design.TZ>1100?"REQUIRES TRACTION/REEVING REVIEW":"PASS"),"Independent layout status persisted");
                    Check(statuses["CodeDiameterRatioStatus"]==design.MachineRoom.CodeDiameterRatioStatus,"Independent code status persisted");
                    if(design.MachineRoom.Deflector.PitchDiameterMm==250)Check(rules.Any(r=>r.RuleId=="MR-ROPE-001"&&r.Status=="FAIL"),"250 mm code conflict hidden");
                    Check(design.MachineRoom.Reeving.VerticalCenterOffsetMm==null&&design.MachineRoom.Reeving.CalculatedWrapAngleDeg==null,"Invented vertical reeving");
                    foreach(var b in mr.Manifest.Entities.Where(b=>b.Role=="governor"))Check(mr.Entities[b.Key].Visible==b.OriginalVisible,"Governor suppressed with deflector");
                    var coverage=PlanCoverage.Read(db,tr,p);Check(coverage.OtherViewEntities==1354&&coverage.UnboundEntities==0,"Other view incorrectly reported unbound");
                    var room=mr.Get<Polyline>("MachineRoom.Room.InnerOutline");
                    DrawingUpdater.AssertNear(Math.Abs(mr.Frame.Local(room.GetPoint3dAt(0)).X-mr.Frame.Local(room.GetPoint3dAt(5)).X),900.0201911137,"Room entrance retained width");
                    VerifyRetainedAssemblies(mr);
                    Configuration.WriteJson(Path.Combine(Configuration.Root,"tests","machine-room","chassis-evidence-"+number+".json"),new {SW=design.SW,SD=design.SD,TZ=design.TZ,Audit=MachineRoomGeometryAudit.Run(mr,design),Groups=mr.Manifest.Entities.Where(b=>b.Dependency!=null).GroupBy(b=>b.Key.Substring(0,b.Key.LastIndexOf('.'))+" / "+b.Dependency.Behavior).Select(g=>new {Group=g.Key,Count=g.Count()}).ToArray(),Geometry=mr.Manifest.Entities.Where(b=>mr.Entities[b.Key] is Polyline||mr.Entities[b.Key] is Line).Select(b=>new {b.Key,b.Role,Visible=mr.Entities[b.Key].Visible,Points=MachineRoomDependencies.Points(mr.Entities[b.Key],mr.Frame)}).ToArray()});
                }
            }
        });
    }
    private static void VerifyRetainedAssemblies(MachineRoomSnapshot current)
    {
        using(var db=new Database(false,true))
        {
            db.ReadDwgFile(Path.Combine(Configuration.Root,"Templates","MachineRoom_Master.dwg"),FileOpenMode.OpenForReadAndAllShare,true,null);db.CloseInput(true);
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var source=MachineRoomDrawing.Read(db,tr);
                foreach(var binding in source.Manifest.Entities)
                {
                    var before=source.Entities[binding.Key];var after=current.Entities[binding.Key];
                    Check(before.Color==after.Color&&before.LineWeight==after.LineWeight,"Native appearance changed: "+binding.Key);
                    var hatch=before as Hatch;
                    if(hatch!=null)
                    {
                        var updated=(Hatch)after;
                        Check(hatch.PatternName==updated.PatternName&&hatch.NumberOfLoops==updated.NumberOfLoops,"Native hatch lost: "+binding.Key);
                        DrawingUpdater.AssertNear(hatch.PatternScale,updated.PatternScale,"Hatch scale");
                    }
                    if(binding.Role!="machine"&&binding.Role!="governor")continue;
                    var dependency=current.Manifest.Entities.Single(b=>b.Key==binding.Key).Dependency;
                    if(dependency!=null&&dependency.Behavior!="FixedToMachine")continue;
                    var curve=before as Curve;var moved=after as Curve;
                    if(curve==null)continue;
                    double dx=binding.Role=="machine"?(current.SW-source.SW)/2:0;
                    double dy=current.CarRow-source.CarRow+(binding.Role=="governor"?current.GovernorStartOffset-source.GovernorStartOffset:0);
                    var circle=curve as Circle;
                    if(circle!=null)
                    {
                        var target=(Circle)moved;var a=source.Frame.Local(circle.Center);var b=current.Frame.Local(target.Center);
                        DrawingUpdater.AssertNear(b.X,a.X+dx,"Circle center X");DrawingUpdater.AssertNear(b.Y,a.Y+dy,"Circle center Y");
                        DrawingUpdater.AssertNear(target.Radius,circle.Radius,"Circle radius");continue;
                    }
                    var poly=curve as Polyline;var movedPoly=moved as Polyline;
                    int samples=poly==null?5:poly.NumberOfVertices;
                    if(poly!=null)Check(poly.NumberOfVertices==movedPoly.NumberOfVertices,"Assembly topology changed");
                    for(int i=0;i<samples;i++)
                    {
                        double fraction=i/4.0;
                        var a=source.Frame.Local(poly!=null?poly.GetPoint3dAt(i):curve.GetPointAtParameter(i==4?curve.EndParam:curve.StartParam+(curve.EndParam-curve.StartParam)*fraction));
                        var b=current.Frame.Local(movedPoly!=null?movedPoly.GetPoint3dAt(i):moved.GetPointAtParameter(i==4?moved.EndParam:moved.StartParam+(moved.EndParam-moved.StartParam)*fraction));
                        if(poly!=null)DrawingUpdater.AssertNear(poly.GetBulgeAt(i),movedPoly.GetBulgeAt(i),"Assembly arc bulge");
                        DrawingUpdater.AssertNear(b.X,a.X+dx,"Retained assembly X "+binding.Key);
                        DrawingUpdater.AssertNear(b.Y,a.Y+dy,"Retained assembly Y "+binding.Key);
                        DrawingUpdater.AssertNear(b.Z,a.Z,"Retained assembly Z "+binding.Key);
                    }
                }
            }
        }
    }
    [CommandMethod("MRTESTPOSE")]
    public void Pose()
    {
        var db=Application.DocumentManager.MdiActiveDocument.Database;
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var model=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);
            var matrix=Matrix3d.Displacement(new Vector3d(10000,-23000,0))*Matrix3d.Rotation(.47,Vector3d.ZAxis,Point3d.Origin);
            foreach(ObjectId id in model)((Entity)tr.GetObject(id,OpenMode.ForWrite)).TransformBy(matrix);tr.Commit();
        }
    }
    [CommandMethod("MRTESTBINDINGS")]
    public void BindingTests()
    {
        var doc=Application.DocumentManager.MdiActiveDocument;var db=doc.Database;
        foreach(bool duplicate in new[]{false,true})
        {
            bool dup=duplicate;
            Log(dup?"Duplicate semantic ID rejects both-view update":"Missing semantic ID rejects both-view update",()=>{
                using(var tr=db.TransactionManager.StartTransaction())
                {
                    var mr=MachineRoomDrawing.Read(db,tr);var plan=DrawingReader.Read(db,tr);double before=plan.ElevatorParameters.SW;
                    var entity=mr.Entities[MachineRoomDrawing.LeftFace];
                    if(dup){var model=(BlockTableRecord)tr.GetObject(entity.OwnerId,OpenMode.ForWrite);var clone=(Entity)entity.Clone();model.AppendEntity(clone);tr.AddNewlyCreatedDBObject(clone,true);}
                    else {entity.UpgradeOpen();using(var data=new ResultBuffer(new TypedValue(1001,DrawingReader.RegApp)))entity.XData=data;}
                    new UpdateAllCommand().Execute();Check(Commands.LastOutput==null&&Commands.LastError!=null,"Invalid bindings accepted");
                    var shaft=plan.Get<Polyline>("Plan.Shaft.InnerOutline.EntranceDatum");
                    DrawingUpdater.AssertNear(shaft.GetPoint3dAt(4).DistanceTo(shaft.GetPoint3dAt(1)),before,"Failed update changed plan");
                }
            });
        }
        Log("Replacement native object keeps semantic identity",()=>{
            using(var tr=db.TransactionManager.StartTransaction())
            {
                var mr=MachineRoomDrawing.Read(db,tr);var entity=mr.Entities[MachineRoomDrawing.LeftFace];ObjectId old=entity.ObjectId;
                var owner=(BlockTableRecord)tr.GetObject(entity.OwnerId,OpenMode.ForWrite);var replacement=(Entity)entity.Clone();owner.AppendEntity(replacement);tr.AddNewlyCreatedDBObject(replacement,true);entity.UpgradeOpen();entity.Erase();
                Check(replacement.ObjectId!=old,"Replacement identity unchanged");tr.Commit();
            }
            new UpdateAllCommand().Execute();Check(Commands.LastOutput!=null,Commands.LastError);
        });
    }
    [CommandMethod("MRTESTCORRUPTION")]
    public void Corruption()
    {
        foreach(string fault in new[]{"span","support","machine","opening","centerline","annotation","dimension","visibility","beam"})
        {
            Log("Geometry audit rejects detached "+fault,()=>{
                var db=Application.DocumentManager.MdiActiveDocument.Database;
                using(var tr=db.TransactionManager.StartTransaction())
                {
                    var s=MachineRoomDrawing.Read(db,tr);MachineRoomBinding b;
                    if(fault=="span")b=s.Manifest.Entities.First(v=>v.Dependency!=null&&v.Dependency.Behavior=="SpanBetweenReferences");
                    else if(fault=="support")b=s.Manifest.Entities.First(v=>v.Key.StartsWith("MachineRoom.Support.P1."));
                    else if(fault=="machine")b=s.Manifest.Entities.First(v=>v.Key.StartsWith("MachineRoom.Machine.ReferenceDetail."));
                    else if(fault=="opening")b=s.Manifest.Entities.First(v=>v.Key.StartsWith("MachineRoom.RopeOpening.Counterweight."));
                    else if(fault=="centerline")b=s.Manifest.Entities.Single(v=>v.Key=="MachineRoom.DeflectorSheave.CenterMark");
                    else if(fault=="annotation")b=s.Manifest.Entities.Single(v=>v.Key.StartsWith("MachineRoom.Reference.CarLabel."));
                    else if(fault=="dimension")b=s.Manifest.Entities.Single(v=>v.Key=="MachineRoom.Dimension.ShaftDepth");
                    else if(fault=="beam")b=s.Manifest.Entities.First(v=>v.Role=="frontBeam");
                    else b=s.Manifest.Entities.First(v=>v.Key.StartsWith("MachineRoom.Support.P2."));
                    var entity=s.Entities[b.Key];entity.UpgradeOpen();
                    if(fault=="dimension")((RotatedDimension)entity).DimensionText="999";
                    else if(fault=="visibility")entity.Visible=!entity.Visible;
                    else entity.TransformBy(Matrix3d.Displacement(s.Frame.Vector(0,25)));
                    bool rejected=false;try{MachineRoomUpdater.Validate(MachineRoomDrawing.Read(db,tr),Commands.LastElevatorParameters);}catch(InvalidOperationException){rejected=true;}
                    Check(rejected,"Final geometry corruption escaped audit: "+fault);
                    // Disposing the uncommitted transaction restores the valid drawing.
                }
            });
        }
    }
}
