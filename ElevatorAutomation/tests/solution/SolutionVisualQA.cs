using System;using System.IO;using System.Linq;using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;using Autodesk.AutoCAD.DatabaseServices;using Autodesk.AutoCAD.Geometry;using Autodesk.AutoCAD.Runtime;using Autodesk.AutoCAD.PlottingServices;
using Elevator.AutoCAD;using Elevator.Core.Project;using Elevator.Solution.Bridge;using Elevator.Solution.Host;
public sealed class SolutionVisualQA
{
 [CommandMethod("SOLUTIONVISUALQA")]
 public void Run()
 {
  var doc=Application.DocumentManager.MdiActiveDocument;string directory=doc.Editor.GetString(new Autodesk.AutoCAD.EditorInput.PromptStringOptions("\nEvidence directory: "){AllowSpaces=true}).StringResult;
  var report=new List<object>();
  try{
   var project=ProjectJson.Deserialize(File.ReadAllText(Path.Combine(directory,"project.elevator.json")));
   using(var tr=doc.Database.TransactionManager.StartTransaction()){
    ProjectDataStorage.Write(doc.Database,tr,"Project",LegacyProjectAdapter.ToLegacy(project));
    ProjectDataStorage.Write(doc.Database,tr,SectionAADrawing.DataKey,LegacyProjectAdapter.ToSectionAA(project));tr.Commit();
   }
   Commands.Run("Phase2Baseline",new IViewUpdater[]{new PlanUpdater(),new MachineRoomUpdater(),new MachineRoomSectionUpdater()});
   if(Commands.LastError!=null)throw new InvalidOperationException(Commands.LastError);
   SectionAADrawing.Rebuild(doc.Database,LegacyProjectAdapter.ToSectionAA(project),null);
   var expected=Snapshot(doc.Database);
   using(var actualDb=new Database(false,true)){
    actualDb.ReadDwgFile(File.ReadAllText(Path.Combine(directory,"generated-path.txt")),FileOpenMode.OpenForReadAndAllShare,false,null);actualDb.CloseInput(true);
    var actual=Snapshot(actualDb);try{
     if(!expected.Keys.OrderBy(k=>k).SequenceEqual(actual.Keys.OrderBy(k=>k)))throw new InvalidDataException("Semantic component set mismatch.");
     var frame=new ShaftFrame{Origin=Point3d.Origin,XAxis=Vector3d.XAxis,YAxis=Vector3d.YAxis,ZAxis=Vector3d.ZAxis};
     foreach(var pair in expected)VerticalSectionGeometryAudit.Compare(actual[pair.Key],pair.Value,frame,pair.Key);
     report.Add(new{Status="PASS",Test="All four views match accepted legacy workflow after normalizing view frames",Entities=actual.Count});
    }finally{foreach(var e in actual.Values)e.Dispose();foreach(var e in expected.Values)e.Dispose();}
   }
   // The second stage opens the published DWG itself for native plots, independently of this baseline.
  }catch(System.Exception e){report.Add(new{Status="FAIL",Error=e.ToString()});}
  Wire.Write(Path.Combine(directory,"geometry-comparison.json"),report);
 }
 static Dictionary<string,Entity> Snapshot(Database db)
 {
  using(var tr=db.TransactionManager.StartTransaction()){
   var frames=new Dictionary<DrawingViews,ShaftFrame>{{DrawingViews.Plan,DrawingReader.Read(db,tr).Frame},{DrawingViews.MachineRoomPlan,MachineRoomDrawing.Read(db,tr).Frame},{DrawingViews.SectionBB,MachineRoomSectionDrawing.Read(db,tr).Frame},{DrawingViews.SectionAA,SectionAADrawing.Frame(SectionAADrawing.Find(db,tr))}};
   var result=new Dictionary<string,Entity>();
   foreach(var group in HostEntry.Groups(db,tr))foreach(var e in group.Value){var f=frames[group.Key];var clone=(Entity)e.Clone();clone.TransformBy(Matrix3d.AlignCoordinateSystem(f.Origin,f.XAxis,f.YAxis,f.ZAxis,Point3d.Origin,Vector3d.XAxis,Vector3d.YAxis,Vector3d.ZAxis));result.Add(SectionAADrawing.Tag(e)[0],clone);}return result;
  }
 }
 [CommandMethod("SOLUTIONPLOT")]
 public void Plot()
 {
  var doc=Application.DocumentManager.MdiActiveDocument;string directory=doc.Editor.GetString(new Autodesk.AutoCAD.EditorInput.PromptStringOptions("\nEvidence directory: "){AllowSpaces=true}).StringResult;
  var report=new List<object>();
  try{using(var tr=doc.Database.TransactionManager.StartTransaction()){
   var groups=HostEntry.Groups(doc.Database,tr);var windows=new Dictionary<string,Extents3d>();Extents3d? total=null;
   foreach(var group in groups){Extents3d? ext=null;foreach(var entity in group.Value.Where(e=>e.Visible)){try{var box=entity.GeometricExtents;if(ext.HasValue){var b=ext.Value;b.AddExtents(box);ext=b;}else ext=box;}catch(Autodesk.AutoCAD.Runtime.Exception){}}
    if(ext.HasValue){windows[group.Key.ToString()]=ext.Value;if(total.HasValue){var b=total.Value;b.AddExtents(ext.Value);total=b;}else total=ext;}}
   windows["AllFour"]=total.Value;
   var layout=(Layout)tr.GetObject(LayoutManager.Current.GetLayoutId(LayoutManager.Current.CurrentLayout),OpenMode.ForRead);
   foreach(var pair in windows){using(var ps=new PlotSettings(layout.ModelType))using(var pi=new PlotInfo())using(var validator=new PlotInfoValidator()){
    ps.CopyFrom(layout);var v=PlotSettingsValidator.Current;v.SetPlotConfigurationName(ps,"DWG To PDF.pc3",null);v.RefreshLists(ps);v.SetCanonicalMediaName(ps,v.GetCanonicalMediaNameList(ps).Cast<string>().First(n=>n.Contains("A1")&&n.Contains("841")));
    var a=pair.Value.MinPoint;var b=pair.Value.MaxPoint;double margin=Math.Max(b.X-a.X,b.Y-a.Y)*0.025;
    v.SetPlotWindowArea(ps,new Extents2d(a.X-margin,a.Y-margin,b.X+margin,b.Y+margin));v.SetPlotType(ps,Autodesk.AutoCAD.DatabaseServices.PlotType.Window);v.SetUseStandardScale(ps,true);v.SetStdScaleType(ps,StdScaleType.ScaleToFit);v.SetPlotCentered(ps,true);v.SetPlotRotation(ps,PlotRotation.Degrees000);v.SetCurrentStyleSheet(ps,"monochrome.ctb");pi.Layout=layout.ObjectId;pi.OverrideSettings=ps;validator.MediaMatchingPolicy=MatchingPolicy.MatchEnabled;validator.Validate(pi);
    using(var engine=PlotFactory.CreatePublishEngine())using(var page=new PlotPageInfo()){engine.BeginPlot(null,null);engine.BeginDocument(pi,doc.Name,null,1,true,Path.Combine(directory,pair.Key+".pdf"));engine.BeginPage(page,pi,true,null);engine.BeginGenerateGraphics(null);engine.EndGenerateGraphics(null);engine.EndPage(null);engine.EndDocument(null);engine.EndPlot(null);}
    report.Add(new{Status="PASS",View=pair.Key});
   }}
  }}catch(System.Exception e){report.Add(new{Status="FAIL",Error=e.ToString()});}
  Wire.Write(Path.Combine(directory,"native-plots.json"),report);
 }
}
