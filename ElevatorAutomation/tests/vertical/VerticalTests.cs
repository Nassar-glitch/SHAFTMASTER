using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD.Common;
using Elevator.AutoCAD.MachineRoom;
using Elevator.AutoCAD.Plan;
using Elevator.AutoCAD.SectionBB;
using Elevator.AutoCAD.Vertical;
using Elevator.Core.Geometry;
using Elevator.Core.MachineRoom;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Shaft;
public sealed class VerticalTests
{
    static List<object> results=new List<object>();
    static string Out {get{return VerticalExperimentDrawing.OutputDirectory;}}
    static void Check(bool value,string message){if(!value)throw new System.Exception(message);}
    static void Log(string name,Action action){try{action();results.Add(new{test=name,status="PASS"});}catch(System.Exception ex){results.Add(new{test=name,status="FAIL",error=ex.ToString()});}Configuration.WriteJson(Path.Combine(Out,"vertical-native-results.json"),results);}
    public static VerticalProjectData Fixture(int stops,string convention,int spacing,bool unequal,bool noStop,VerticalProjectData previous)
    {
        var d=new VerticalProjectData{FixtureLabel="SYNTHETIC REGRESSION FIXTURE - NOT PROJECT DEFAULTS",NamingConvention=convention,FirstLevel=convention=="BG"?"B":"-2",RequestedBracketMaxSpacingMm=spacing};
        int normals=stops+(noStop?1:0);var heights=Enumerable.Range(0,normals-1).Select(i=>unequal?2800.0+(i%3)*350:3000.0).ToArray();d.Floors=FloorStackGenerator.Create(normals+1,convention,d.FirstLevel,heights,previous==null?null:previous.Floors);
        foreach(var f in d.Floors)f.Served=!f.IsRoof;if(noStop)d.Floors[1].Served=false;
        d.CarReferenceFloorId=(d.Floors.FirstOrDefault(f=>f.Name=="G"&&f.Served)??d.Floors.First(f=>f.Served)).FloorId;return d;
    }
    static string ProtectedFingerprint(Database db,Transaction tr)
    {
        var plan=DrawingReader.Read(db,tr);var section=MachineRoomSectionDrawing.Read(db,tr);var mr=MachineRoomDrawing.Read(db,tr);
        var all=plan.Entities.Concat(section.Entities).Concat(mr.Entities).OrderBy(p=>p.Key).Select(p=>new{p.Key,Type=p.Value.GetRXClass().DxfName,p.Value.Visible,Points=MachineRoomDependencies.Points(p.Value,plan.Frame)}).ToArray();
        return VerticalExperimentDrawing.Json(all)+VerticalExperimentDrawing.Json(ProjectDataStorage.ReadProject(db,tr));
    }
    [CommandMethod("VERTICALTESTGENERATE")]
    public void Generate()
    {
        Directory.CreateDirectory(Out);var db=Application.DocumentManager.MdiActiveDocument.Database;string protectedBefore;
        using(var tr=db.TransactionManager.StartTransaction())protectedBefore=ProtectedFingerprint(db,tr);
        VerticalProjectData previous=null;string baseline=null;int index=0;
        foreach(int stops in new[]{2,3,5,8,12,3,12,3})
        {
            int number=++index;var data=Fixture(stops,number%2==0?"Numeric":"BG",new[]{3000,2000,1500,1000}[(number-1)%4],number%2==0,number==3||number==4,previous);previous=data;
            Log("Native generation/save/reopen "+number+" ("+stops+" stops)",()=>{
                var audit=VerticalExperimentDrawing.Generate(db,data,new Point3d(20000,0,0));Check(audit.Status=="PASS","Native geometry audit");Check(audit.ServedStops==stops,"Served stop count");Check(audit.MachineRoomEntitiesChecked>690,"Complete B-B clone");
                string path=Path.Combine(Out,"Fixture_"+number+"_"+stops+"Stops.dwg");db.SaveAs(path,DwgVersion.AC1032);
                using(var reopened=new Database(false,true)){reopened.ReadDwgFile(path,FileOpenMode.OpenForReadAndAllShare,true,null);reopened.CloseInput(true);using(var tr=reopened.TransactionManager.StartTransaction()){var saved=VerticalSectionGeometryAudit.Run(reopened,tr);Check(saved.EntitiesChecked==audit.EntitiesChecked,"Save/reopen geometry count");Check(ProtectedFingerprint(reopened,tr)==protectedBefore,"Original three views changed");}}
                Export(db,Path.Combine(Out,"Fixture_"+number+".geometry.json"));
            });
            if(number==1)baseline=VerticalExperimentDrawing.Json(data);
        }
        Log("Few -> many -> original: exact original inputs and geometry",()=>{
            var d=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<VerticalProjectData>(baseline);var a=VerticalExperimentDrawing.Generate(db,d,null);var before=Snapshot(db);VerticalExperimentDrawing.Generate(db,Fixture(12,"Numeric",1000,true,false,d),null);VerticalExperimentDrawing.Generate(db,d,null);Check(Snapshot(db)==before,"Regeneration not deterministic");
        });
        Log("Translated and rotated experimental frame",()=>{
            using(var tr=db.TransactionManager.StartTransaction()){var all=VerticalExperimentDrawing.Find(db,tr);var frame=VerticalExperimentDrawing.Frame(all);var transform=Matrix3d.Displacement(new Vector3d(237,-519,0))*Matrix3d.Rotation(.31,Vector3d.ZAxis,frame.Origin);foreach(var e in all.Values){e.UpgradeOpen();e.TransformBy(transform);}VerticalSectionGeometryAudit.Run(db,tr); /* rollback pose */}
        });
        foreach(string suffix in new[]{"Car.Envelope","Pit.FinishedFloor","Dimension.Pit","Bracket.Car.Left.0","Rope.CarDrop"})
        {string target=suffix;Log("Audit detects moved "+target,()=>{using(var tr=db.TransactionManager.StartTransaction()){var all=VerticalExperimentDrawing.Find(db,tr);var e=all[VerticalExperimentDrawing.Prefix+target];e.UpgradeOpen();e.TransformBy(Matrix3d.Displacement(new Vector3d(7,11,0)));bool caught=false;try{VerticalSectionGeometryAudit.Run(db,tr);}catch{caught=true;}Check(caught,"Moved geometry escaped audit");}});}
        foreach(bool duplicate in new[]{false,true}){bool dup=duplicate;Log(dup?"Duplicate semantic ID rejected":"Missing semantic ID rejected",()=>{using(var tr=db.TransactionManager.StartTransaction()){var all=VerticalExperimentDrawing.Find(db,tr);var e=all[VerticalExperimentDrawing.Prefix+"Car.Envelope"];if(dup){var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);var copy=(Entity)e.Clone();ms.AppendEntity(copy);tr.AddNewlyCreatedDBObject(copy,true);}else{e.UpgradeOpen();e.Erase();}bool caught=false;try{VerticalSectionGeometryAudit.Run(db,tr);}catch{caught=true;}Check(caught,"Missing/duplicate escaped audit");}});}
        Log("Input edit marks view stale",()=>{using(var tr=db.TransactionManager.StartTransaction()){var d=ProjectDataStorage.Read<VerticalProjectData>(db,tr,VerticalExperimentDrawing.DataKey);d.PitDepthMm+=100;ProjectDataStorage.Write(db,tr,VerticalExperimentDrawing.DataKey,d);bool caught=false;try{VerticalSectionGeometryAudit.Run(db,tr);}catch{caught=true;}Check(caught,"Stale inputs escaped audit");}});
        Log("Final five-stop review drawing and original-view isolation",()=>{
            var review=Fixture(5,"BG",1500,true,true,previous);VerticalExperimentDrawing.Generate(db,review,null);db.SaveAs(Path.Combine(Out,"FullVertical_V1_Review.dwg"),DwgVersion.AC1032);Export(db,Path.Combine(Out,"review-native-geometry.json"));using(var tr=db.TransactionManager.StartTransaction()){Check(ProtectedFingerprint(db,tr)==protectedBefore,"Original drawing/project modified");Configuration.WriteJson(Path.Combine(Out,"review-audit.json"),VerticalSectionGeometryAudit.Run(db,tr));}
        });
        Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nVertical native tests recorded: "+results.Count+"\n");
    }
    static int workflowStep;
    static List<object> workflowRows=new List<object>();
    [CommandMethod("VERTICALTESTWORKFLOWCHECK")]
    public void Workflow()
    {
        int step=++workflowStep;var db=Application.DocumentManager.MdiActiveDocument.Database;
        try{using(var tr=db.TransactionManager.StartTransaction())
        {
            var d=ProjectDataStorage.Read<VerticalProjectData>(db,tr,VerticalExperimentDrawing.DataKey);var a=VerticalSectionGeometryAudit.Run(db,tr);
            Check(String.Join(",",d.Floors.Select(f=>f.Name))=="B,G,R","Interactive floor names");Check(d.RequestedBracketMaxSpacingMm==1500,"Explicit bracket selection");Check(a.CarFloorZ==3000,"G displayed car reference");
            Check(step<3?!d.CWBottomAbovePitFloorAtHighestServedLandingMm.HasValue:d.CWBottomAbovePitFloorAtHighestServedLandingMm==450,"Enter must not silently approve provisional CW reference");
            workflowRows.Add(new{step=step,status="PASS",calibration=d.CWBottomAbovePitFloorAtHighestServedLandingMm,referenceStatus=d.CWAbsolutePositionStatus});
        }}catch(System.Exception ex){workflowRows.Add(new{step=step,status="FAIL",error=ex.ToString()});}
        Configuration.WriteJson(Path.Combine(Out,"vertical-interactive-results.json"),workflowRows);
    }
    [CommandMethod("VERTICALTESTRESIZE")]
    public void Resize()
    {
        var doc=Application.DocumentManager.MdiActiveDocument;var db=doc.Database;var rows=new List<object>();string small=null;var data=Fixture(3,"BG",2000,false,false,null);int index=0;
        foreach(var size in new[]{new[]{1200.0,1400},new[]{1800.0,2000},new[]{1200.0,1400}})
        {
            int step=++index;try
            {
                using(var tr=db.TransactionManager.StartTransaction())
                {
                    var input=ProjectDataStorage.ReadProject(db,tr);input.SW=size[0];input.SD=size[1];ProjectDataStorage.Write(db,tr,"Project",input);
                    var p=DrawingReader.Read(db,tr);var mr=MachineRoomDrawing.Read(db,tr);var sec=MachineRoomSectionDrawing.Read(db,tr);
                    var d=DesignRules.Calculate(input.SW,input.SD,Configuration.Read<EquipmentProfile>(ElevatorProjectCommand.Family(input).ProfileFile),p.ElevatorParameters.CwDepth);d.MachineRoom=MachineRoomRules.Calculate(input,d,mr.Extensions);
                    d.MachineRoomSection=MachineRoomSectionRules.Calculate(input,d,sec.Manifest.Reference,(MachineRoomSectionGeometryAudit.SupportOffset(mr,"P2")+MachineRoomSectionGeometryAudit.SupportOffset(mr,"P4"))/2,(MachineRoomSectionGeometryAudit.SupportOffset(mr,"P1")+MachineRoomSectionGeometryAudit.SupportOffset(mr,"P3"))/2,mr.PitchProjection,mr.Entities[MachineRoomDrawing.Pitch].Visible);
                    new PlanUpdater().Update(db,tr,d);new MachineRoomUpdater().Update(db,tr,d);new MachineRoomSectionUpdater().Update(db,tr,d);tr.Commit();
                }
                doc.Editor.Regen();var audit=VerticalExperimentDrawing.Generate(db,data,new Point3d(20000,0,0));string snapshot=Snapshot(db);if(step==1)small=snapshot;double drift=0;
                if(step==3)
                {
                    File.WriteAllText(Path.Combine(Out,"resize-small-before.json"),small);File.WriteAllText(Path.Combine(Out,"resize-small-after.json"),snapshot);
                    var json=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=Int32.MaxValue};var first=json.Deserialize<Signature[]>(small);var last=json.Deserialize<Signature[]>(snapshot);
                    Check(first.Length==last.Length,"Resize changed semantic component count");
                    for(int i=0;i<first.Length;i++){Check(first[i].Key==last[i].Key&&first[i].Points.Length==last[i].Points.Length,"Resize changed identity/topology");for(int j=0;j<first[i].Points.Length;j++){var a=first[i].Points[j];var b=last[i].Points[j];drift=Math.Max(drift,Math.Max(Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y)));}}
                    Check(drift<1e-7,"Small -> large -> small changed native geometry by "+drift+" mm");
                }
                string path=Path.Combine(Out,"Resize_"+step+"_SW"+size[0]+"_SD"+size[1]+".dwg");db.SaveAs(path,DwgVersion.AC1032);rows.Add(new{step=step,status="PASS",SW=size[0],SD=size[1],MaxRoundTripCoordinateDriftMm=drift,audit=audit});
            }
            catch(System.Exception ex){rows.Add(new{step=step,status="FAIL",error=ex.ToString()});}
        }
        Configuration.WriteJson(Path.Combine(Out,"vertical-resize-results.json"),rows);
    }
    public sealed class Signature {public string Key;public LocalPoint[] Points;}
    static string Snapshot(Database db)
    {
        using(var tr=db.TransactionManager.StartTransaction()){var all=VerticalExperimentDrawing.Find(db,tr);var frame=VerticalExperimentDrawing.Frame(all);return VerticalExperimentDrawing.Json(all.OrderBy(p=>p.Key).Select(p=>new{p.Key,Points=MachineRoomDependencies.Points(p.Value,frame)}).ToArray());}
    }
    [CommandMethod("VERTICALTESTREOPEN")]
    public void Reopen()
    {
        var db=Application.DocumentManager.MdiActiveDocument.Database;var rows=new List<object>();try{using(var tr=db.TransactionManager.StartTransaction()){var a=VerticalSectionGeometryAudit.Run(db,tr);rows.Add(new{test="Fresh AutoCAD process native audit",status="PASS",audit=a});}var before=Snapshot(db);VerticalProjectData d;using(var tr=db.TransactionManager.StartTransaction())d=ProjectDataStorage.Read<VerticalProjectData>(db,tr,VerticalExperimentDrawing.DataKey);VerticalExperimentDrawing.Generate(db,d,null);Check(before==Snapshot(db),"Fresh regeneration changed geometry");rows.Add(new{test="Fresh process repeat generation exact geometry",status="PASS"});}catch(System.Exception ex){rows.Add(new{test="Fresh process",status="FAIL",error=ex.ToString()});}Configuration.WriteJson(Path.Combine(Out,"vertical-fresh-process-results.json"),rows);
    }
    [CommandMethod("VERTICALTESTEXPORT")] public void ExportCommand(){try{Export(Application.DocumentManager.MdiActiveDocument.Database,Path.Combine(Out,"review-native-geometry.json"));}catch(System.Exception ex){File.WriteAllText(Path.Combine(Out,"export-error.txt"),ex.ToString());}} public static void Export(Database db,string file)
    {
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var all=VerticalExperimentDrawing.Find(db,tr);var frame=VerticalExperimentDrawing.Frame(all);var data=ProjectDataStorage.Read<VerticalProjectData>(db,tr,VerticalExperimentDrawing.DataKey);SectionSnapshot section;var source=VerticalExperimentDrawing.ReadSource(db,tr,out section);var geometry=VerticalSectionCalculator.Calculate(data,source);var draw=new List<object>();
            foreach(var p in all)if(p.Value.Visible)Extract(p.Value,p.Key,frame,draw,0);
            Configuration.WriteJson(file,new{Data=data,Source=source,Geometry=geometry,Audit=VerticalSectionGeometryAudit.Run(db,tr),NativeVisible=draw});
        }
    }
    static void Extract(Entity e,string key,ShaftFrame f,List<object> rows,int depth)
    {
        if(depth>5)return;Func<Point3d,double[]> p=v=>{var q=f.Local(v);return new[]{q.X,q.Y};};string layer=e.Layer;
        var text=e as MText;if(text!=null){rows.Add(new{Key=key,Kind="Text",Layer=layer,Position=p(text.Location),Text=text.Contents,Height=text.TextHeight,Width=text.Width,Rotation=text.Rotation-Math.Atan2(f.XAxis.Y,f.XAxis.X)});return;}
        var dt=e as DBText;if(dt!=null){rows.Add(new{Key=key,Kind="Text",Layer=layer,Position=p(dt.Position),Text=dt.TextString,Height=dt.Height,Width=0,Rotation=dt.Rotation-Math.Atan2(f.XAxis.Y,f.XAxis.X)});return;}
        var nativeLine=e as Line;if(nativeLine!=null){rows.Add(new{Key=key,Kind="Path",Layer=layer,Points=new[]{p(nativeLine.StartPoint),p(nativeLine.EndPoint)}});return;} var nativePoly=e as Polyline;if(nativePoly!=null){var vertices=new List<double[]>();for(int k=0;k<nativePoly.NumberOfVertices;k++){vertices.Add(p(nativePoly.GetPoint3dAt(k)));if(Math.Abs(nativePoly.GetBulgeAt(k))>1e-9 && (nativePoly.Closed||k<nativePoly.NumberOfVertices-1))for(int j=1;j<16;j++)vertices.Add(p(nativePoly.GetPointAtParameter(k+j/16.0)));}if(nativePoly.Closed)vertices.Add(vertices[0]);rows.Add(new{Key=key,Kind="Path",Layer=layer,Points=vertices});return;} var curve=e as Curve;if(curve!=null){var line=e as Line;var poly=e as Polyline;int n=line!=null?1:poly!=null?Math.Max(1,poly.NumberOfVertices*8):96;var pts=new List<double[]>();for(int i=0;i<=n;i++)try{pts.Add(p(curve.GetPointAtParameter(i==n?curve.EndParam:curve.StartParam+(curve.EndParam-curve.StartParam)*i/n)));}catch(System.Exception ex){throw new System.Exception("Export curve "+key+" "+e.GetRXClass().DxfName+" start="+curve.StartParam+" end="+curve.EndParam+" i="+i,ex);};rows.Add(new{Key=key,Kind="Path",Layer=layer,Points=pts});return;}
        var hatch=e as Hatch;if(hatch!=null){for(int i=0;i<hatch.NumberOfLoops;i++){var loop=hatch.GetLoopAt(i);if(loop.IsPolyline)rows.Add(new{Key=key,Kind="Hatch",Layer=layer,Points=loop.Polyline.Cast<BulgeVertex>().Select(v=>p(new Point3d(v.Vertex.X,v.Vertex.Y,0))).ToArray()});}return;}
        var solid=e as Solid;if(solid!=null){rows.Add(new{Key=key,Kind="Fill",Layer=layer,Points=new[]{0,1,3,2}.Select(i=>p(solid.GetPointAt((short)i))).ToArray()});return;}
        using(var parts=new DBObjectCollection()){try{e.Explode(parts);foreach(DBObject o in parts){using(o){var child=o as Entity;if(child!=null)Extract(child,key,f,rows,depth+1);}}}catch(System.Exception){ /* Unsupported entity remains covered by native audit and native PDF plot. */}}
    }
    [CommandMethod("VERTICALTESTPLOT")]
    public void Plot()
    {
        var doc=Application.DocumentManager.MdiActiveDocument;var db=doc.Database;var rows=new List<object>();
        using(var tr=db.TransactionManager.StartTransaction())
        {
            var all=VerticalExperimentDrawing.Find(db,tr);var f=VerticalExperimentDrawing.Frame(all);var d=ProjectDataStorage.Read<VerticalProjectData>(db,tr,VerticalExperimentDrawing.DataKey);SectionSnapshot s;var source=VerticalExperimentDrawing.ReadSource(db,tr,out s);var g=VerticalSectionCalculator.Calculate(d,source);
            var windows=new Dictionary<string,double[]>{{"01-full-height",new[]{source.RoomRearX-1700,g.PitFloorZ-450,4700,g.CeilingZ+1400}},{"02-pit",new[]{-source.Plan.SD-600,g.PitFloorZ-250,2450,Math.Min(g.CarTopZ,2500)}} ,{"03-typical-landing",new[]{-source.Plan.SD-300,g.Floors[2].FflZ-450,2450,g.Floors[2].FflZ+2600}},{"04-car-cw-rails-brackets",new[]{source.RoomRearX-1700,Math.Min(g.CarBottomZ,g.CwBottomZ)-300,1800,Math.Max(g.CarTopZ,g.CwTopZ)+300}},{"05-top-landing-oh",new[]{-source.Plan.SD-500,g.HighestServedZ-450,2450,g.RoofZ+450}},{"06-machine-room",new[]{source.RoomRearX-600,g.RoofZ-550,2900,g.CeilingZ+550}}};
            foreach(var pair in windows)
            {
                try
                {
                    var hidden=new[]{"ReviewSummary","Rails.Label","Provenance","Car.DetailStatus","Pit.Review","Governor.ReferenceNote","CW.Label"};foreach(string suffix in hidden){var annotation=all[VerticalExperimentDrawing.Prefix+suffix];annotation.UpgradeOpen();annotation.Visible=pair.Key=="01-full-height"||(suffix=="CW.Label"&&pair.Key=="04-car-cw-rails-brackets");} var layout=(Layout)tr.GetObject(LayoutManager.Current.GetLayoutId(LayoutManager.Current.CurrentLayout),OpenMode.ForRead);
                    using(var ps=new PlotSettings(layout.ModelType))using(var pi=new PlotInfo())using(var validator=new PlotInfoValidator())
                    {
                        ps.CopyFrom(layout);var v=PlotSettingsValidator.Current;v.SetPlotConfigurationName(ps,"DWG To PDF.pc3",null);v.RefreshLists(ps);var media=v.GetCanonicalMediaNameList(ps).Cast<string>().First(n=>n.Contains("A1")&&n.Contains("841"));v.SetCanonicalMediaName(ps,media);
                        var a=f.World(new Point3d(pair.Value[0],pair.Value[1],0));var b=f.World(new Point3d(pair.Value[2],pair.Value[3],0));v.SetPlotWindowArea(ps,new Extents2d(a.X,a.Y,b.X,b.Y));v.SetPlotType(ps,Autodesk.AutoCAD.DatabaseServices.PlotType.Window);v.SetUseStandardScale(ps,true);v.SetStdScaleType(ps,StdScaleType.ScaleToFit);v.SetPlotCentered(ps,true);v.SetPlotRotation(ps,PlotRotation.Degrees000);v.SetCurrentStyleSheet(ps,"monochrome.ctb");
                        pi.Layout=layout.ObjectId;pi.OverrideSettings=ps;validator.MediaMatchingPolicy=MatchingPolicy.MatchEnabled;validator.Validate(pi);
                        using(var engine=PlotFactory.CreatePublishEngine())using(var page=new PlotPageInfo())
                        {engine.BeginPlot(null,null);engine.BeginDocument(pi,doc.Name,null,1,true,Path.Combine(Out,pair.Key+".pdf"));engine.BeginPage(page,pi,true,null);engine.BeginGenerateGraphics(null);engine.EndGenerateGraphics(null);engine.EndPage(null);engine.EndDocument(null);engine.EndPlot(null);}
                    }
                    rows.Add(new{view=pair.Key,status="PASS"});
                }
                catch(System.Exception ex){rows.Add(new{view=pair.Key,status="FAIL",error=ex.ToString()});}
            }
        }
        Configuration.WriteJson(Path.Combine(Out,"native-plot-results.json"),rows);
    }
}







