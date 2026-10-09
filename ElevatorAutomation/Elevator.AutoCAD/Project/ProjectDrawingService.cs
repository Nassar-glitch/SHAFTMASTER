using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Presets;
using Elevator.Core.MachineRoom;
using Elevator.Core.Geometry;
using Elevator.Core.Project;
namespace Elevator.AutoCAD
{
    // AutoCAD document/command context is an adapter concern. UI calls this service, not commands.
    public sealed class ProjectDrawingService : IProjectDrawingGenerator
    {
        private readonly Document boundDocument;
        public ProjectDrawingService(){ }
        public ProjectDrawingService(Document document){boundDocument=document;}
        public DrawingGenerationResult GenerateProjectDrawings(ElevatorProject project,GenerationOptions options)
        {
            if(boundDocument==null)throw new InvalidOperationException("The AutoCAD host must bind a document before UI generation.");
            return GenerateProjectDrawings(boundDocument,project,options);
        }
        public DrawingGenerationResult GenerateProjectDrawings(Document document,ElevatorProject project,GenerationOptions options=null)
        {
            if(document==null)throw new ArgumentNullException("document");
            options=options??new GenerationOptions();
            var failures=ProjectValidator.Validate(project,options.Views).Where(v=>v.BlocksGeneration).ToArray();
            if(failures.Length>0)throw new ArgumentException(String.Join("; ",failures.Select(v=>v.Field+": "+v.Message)));
            if(options.Views==0||(options.Views&~DrawingViews.All)!=0)throw new ArgumentException("Select supported drawing views.");
            if(!document.Database.TileMode)throw new InvalidOperationException("Generation requires model space.");
            if(!String.IsNullOrWhiteSpace(options.OutputPath)&&File.Exists(options.OutputPath))throw new IOException("Output already exists; choose a new project filename.");
            var db=document.Database;var result=new DrawingGenerationResult();bool imported=false;
            // Imports are confined to the requested project document. Existing source DWGs are read-only.
            using(document.LockDocument())
            {
                using(var tr=db.TransactionManager.StartTransaction())
                {
                    LegacyMigration.Ensure(db,tr);var plan=DrawingReader.Read(db,tr);
                    if(ComponentBinding.ReadManifest(db,tr).TemplateId!=project.Elevator.TemplateId)throw new ArgumentException("Project/template mismatch.");
                    bool room=(options.Views&~DrawingViews.Plan)!=0;
                    bool section=(options.Views&(DrawingViews.SectionAA|DrawingViews.SectionBB))!=0;
                    if(room&&!MachineRoomDrawing.IsPresent(db,tr)){MachineRoomDrawing.Import(db,Path.Combine(Configuration.Root,"Templates","MachineRoom_Master.dwg"),plan.Frame.Origin,plan.Frame.XAxis);imported=true;}
                    if(section&&!MachineRoomSectionDrawing.IsPresent(db,tr)){MachineRoomSectionDrawing.Import(db,Path.Combine(Configuration.Root,"Templates","MachineRoomSection_Master.dwg"),plan.Frame.Origin,plan.Frame.XAxis);imported=true;}
                    tr.Commit();
                }
                // Existing adapter handles obsolete native dimension associations in AutoCAD context.
                if((options.Views&(DrawingViews.SectionAA|DrawingViews.SectionBB))!=0)MachineRoomSectionDrawing.DetachSourceDimensionAssociations(document);
                using(var tr=db.TransactionManager.StartTransaction())
                {
                    var plan=DrawingReader.Read(db,tr);
                    var context=new DesignContext{Equipment=Configuration.Read<EquipmentProfile>(ElevatorProjectCommand.Family(LegacyProjectAdapter.ToLegacy(project)).ProfileFile),CounterweightDepthMm=plan.ElevatorParameters.CwDepth};
                    if((options.Views&~DrawingViews.Plan)!=0)
                    {
                        var mr=MachineRoomDrawing.Read(db,tr);MachineRoomDependencies.Prepare(db,tr,mr);context.RoomExtensions=mr.Extensions;
                        context.ChassisEvidence=ChassisCalculationGeometryReader.Read(mr);
                        if((options.Views&(DrawingViews.SectionAA|DrawingViews.SectionBB))!=0)
                        {
                            context.SectionReference=MachineRoomSectionDrawing.Read(db,tr).Manifest.Reference;
                            context.FrontSupportDepthMm=(MachineRoomSectionGeometryAudit.SupportOffset(mr,"P2")+MachineRoomSectionGeometryAudit.SupportOffset(mr,"P4"))/2;
                            context.RearSupportOffsetMm=(MachineRoomSectionGeometryAudit.SupportOffset(mr,"P1")+MachineRoomSectionGeometryAudit.SupportOffset(mr,"P3"))/2;
                            context.RetainedDeflectorPitchMm=mr.PitchProjection;context.RetainedDeflectorVisible=mr.Entities[MachineRoomDrawing.Pitch].Visible;
                        }
                    }
                    result.Design=new ElevatorDesignEngine().Calculate(project,context,options.Views);
                    if(!result.Design.CanGenerate)throw new ArgumentException(String.Join("; ",result.Design.Validation.Where(v=>v.BlocksGeneration).Select(v=>v.Message)));
                    UnifiedProjectStorage.Save(db,tr,project);
                    // Dependencies are updated in order even if only a dependent view was selected.
                    new PlanUpdater().Update(db,tr,result.Design.Plan);
                    if((options.Views&~DrawingViews.Plan)!=0)new MachineRoomUpdater().Update(db,tr,result.Design.Plan);
                    if((options.Views&(DrawingViews.SectionAA|DrawingViews.SectionBB))!=0)new MachineRoomSectionUpdater().Update(db,tr,result.Design.Plan);
                    if((options.Views&DrawingViews.SectionAA)!=0)
                    {
                        bool exists=SectionAADrawing.Find(db,tr).Count>0;
                        if(!exists){SectionAADrawing.ImportLibrary(db);imported=true;}
                        SectionAADrawing.Rebuild(db,LegacyProjectAdapter.ToSectionAA(project),exists?(Point3d?)null:plan.Frame.Origin);
                    }
                    if(options.ArrangeViews||imported)Arrange(db,tr,project.Drawing.ViewMarginMm);
                    foreach(var group in Entities(db,tr))result.EntityCounts[group.Key.ToString()]=group.Value.Count;
                    tr.Commit();
                }
                document.Editor.Regen();
                if(!String.IsNullOrWhiteSpace(options.OutputPath)){db.SaveAs(options.OutputPath,DwgVersion.AC1032);result.OutputPath=options.OutputPath;}
            }
            return result;
        }
        static Dictionary<DrawingViews,List<Entity>> Entities(Database db,Transaction tr)
        {
            var groups=new Dictionary<DrawingViews,List<Entity>>();var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
            foreach(ObjectId id in (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead))
            {
                var e=(Entity)tr.GetObject(id,OpenMode.ForRead);var tag=SectionAADrawing.Tag(e);if(tag==null||tag.Length==0)continue;
                DrawingViews view;
                if(tag[0].StartsWith("Plan."))view=DrawingViews.Plan;
                else if(tag[0].StartsWith("MachineRoom."))view=DrawingViews.MachineRoomPlan;
                else if(tag[0].StartsWith("MachineRoomSection."))view=DrawingViews.SectionBB;
                else if(tag[0].StartsWith("SectionAA."))view=DrawingViews.SectionAA;else continue;
                if(!groups.ContainsKey(view))groups[view]=new List<Entity>();groups[view].Add(e);
            }
            return groups;
        }
        static void Arrange(Database db,Transaction tr,double margin)
        {
            var groups=Entities(db,tr);var bounds=new List<ViewBounds>();
            foreach(var group in groups)
            {
                Extents3d? ext=null;
                foreach(var entity in group.Value.Where(e=>e.Visible))
                {
                    // Native anomalous spline extents remain real drawing extents; never alter geometry.
                    try{var e=entity.GeometricExtents;if(ext.HasValue){var combined=ext.Value;combined.AddExtents(e);ext=combined;}else ext=e;}
                    catch(Autodesk.AutoCAD.Runtime.Exception){ }
                }
                if(!ext.HasValue)throw new InvalidOperationException("No measurable view extents: "+group.Key);
                bounds.Add(new ViewBounds{View=group.Key,MinX=ext.Value.MinPoint.X,MinY=ext.Value.MinPoint.Y,MaxX=ext.Value.MaxPoint.X,MaxY=ext.Value.MaxPoint.Y});
            }
            foreach(var placement in DrawingLayoutService.Arrange(bounds,margin))foreach(var e in groups[placement.View]){e.UpgradeOpen();e.TransformBy(Matrix3d.Displacement(new Vector3d(placement.DeltaX,placement.DeltaY,0)));}
        }
    }
}
