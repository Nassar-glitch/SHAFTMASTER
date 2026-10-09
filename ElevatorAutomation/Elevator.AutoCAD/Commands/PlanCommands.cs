using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Elevator.Core.Project;
using Elevator.Core.Presets;
using Elevator.Core.Geometry;

namespace Elevator.AutoCAD
{
    public sealed class PlanCommands
    {
        [CommandMethod("ELEVATORPLANOPTIONS",CommandFlags.Modal)]
        public void Options()
        {
            var doc=Application.DocumentManager.MdiActiveDocument;
            try
            {
                using(var tr=doc.Database.TransactionManager.StartTransaction())
                {
                    var input=RequireProject(doc.Database,tr);
                    string name,title;
                    if(!Ask(doc.Editor,"Project name",input.ProjectName,out name)||!Ask(doc.Editor,"Plan title (printed scale stays unchanged)",input.PlanTitle,out title))return;
                    input.ProjectName=name;input.PlanTitle=title;
                    ProjectDataStorage.Write(doc.Database,tr,"Project",input);tr.Commit();
                    doc.Editor.WriteMessage("\nPlan options stored. Run UPDATEALL to apply annotations and save.\n");
                }
            }
            catch(System.Exception ex){doc.Editor.WriteMessage("\nPlan options stopped: "+ex.Message+"\n");}
        }
        private static ProjectInput RequireProject(Database db,Transaction tr)
        {
            var p=ProjectDataStorage.ReadProject(db,tr);
            if(p==null)throw new InvalidOperationException("Run ELEVATORPROJECT first.");return p;
        }
        private static bool Ask(Editor ed,string name,string current,out string value)
        {
            var result=ed.GetString(new PromptStringOptions("\n"+name+" [Enter=keep, -=clear] <"+(current??"default")+">: "){AllowSpaces=true});
            value=current;if(result.Status!=PromptStatus.OK)return false;
            if(result.StringResult=="-")value=null;else if(result.StringResult.Length>0)value=result.StringResult;
            return true;
        }
        [CommandMethod("ELEVATORANNOTATION",CommandFlags.Modal)]
        public void Annotation()
        {
            var doc=Application.DocumentManager.MdiActiveDocument;var ed=doc.Editor;
            try
            {
                var select=new PromptEntityOptions("\nSelect plan MText or multileader to drive from project data: ");
                select.SetRejectMessage("\nSelect MText or an MText-content multileader.");select.AddAllowedClass(typeof(MText),true);select.AddAllowedClass(typeof(MLeader),true);
                var picked=ed.GetEntity(select);if(picked.Status!=PromptStatus.OK)return;
                using(var tr=doc.Database.TransactionManager.StartTransaction())
                {
                    var input=RequireProject(doc.Database,tr);var snapshot=DrawingReader.Read(doc.Database,tr);
                    var entity=(Entity)tr.GetObject(picked.ObjectId,OpenMode.ForRead);
                    var table=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);
                    if(entity.OwnerId!=table[BlockTableRecord.ModelSpace])throw new InvalidOperationException("Only model-space plan annotations are supported.");
                    string existing=PlanAnnotations.Text(entity);
                    var manifest=ComponentBinding.ReadManifest(doc.Database,tr);
                    string key=snapshot.Entities.Where(p=>p.Value.ObjectId==entity.ObjectId).Select(p=>p.Key).SingleOrDefault();
                    Binding binding;
                    if(key==null)
                    {
                        var name=ed.GetString("\nUnique annotation name (letters/digits/dots, e.g. ProjectSummary): ");if(name.Status!=PromptStatus.OK)return;
                        if(!Regex.IsMatch(name.StringResult,@"^[A-Za-z][A-Za-z0-9_.]*$"))throw new InvalidOperationException("Invalid semantic annotation name.");
                        key="Plan.Annotation."+name.StringResult;
                        if(manifest.Entities.Any(b=>b.Key==key))throw new InvalidOperationException("Annotation name already exists.");
                        var anchors=new Dictionary<string,string>{{"Front","centerFront"},{"Rear","centerRear"},{"Cabin","carMark"},{"Counterweight","cwMark"},{"DoorLeft","doorLeft"},{"DoorRight","doorRight"},{"CarLeft","carLeft"},{"CarRight","carRight"}};
                        var anchor=new PromptKeywordOptions("\nMove annotation with");foreach(string option in anchors.Keys)anchor.Keywords.Add(option);
                        anchor.Keywords.Default="Front";var choice=ed.GetKeywords(anchor);if(choice.Status!=PromptStatus.OK&&choice.Status!=PromptStatus.None)return;
                        binding=new Binding{Key=key,Type=entity.GetRXClass().DxfName,Role=anchors[choice.Status==PromptStatus.None?"Front":choice.StringResult]};
                        manifest.Entities.Add(binding);
                    }
                    else binding=manifest.Entities.Single(b=>b.Key==key);
                    ed.WriteMessage("\nUse {{SW}}, {{SD}}, {{CarWidth}}, {{CarDepth}}, {{CarDBG}}, {{CWDBG}}, {{DoorWidth}}, {{TZ}}, {{ProjectName}}, {{PlanTitle}} or {{Clearance.cabinDoorReturnMm}}.\n");
                    string recipe;
                    if(!Ask(ed,"Text template",binding.AnnotationTemplate??existing,out recipe))return;
                    if(recipe!=null)
                    {
                        var family=ElevatorProjectCommand.Family(input);CommonRules.ValidateProject(input,family);
                        var profile=Configuration.Read<EquipmentProfile>(family.ProfileFile);
                        var design=DesignRules.Calculate(input.SW,input.SD,profile,snapshot.ElevatorParameters.CwDepth);
                        var gaps=DrawingUpdater.Preflight(snapshot,new PlanGeometryAdapter(snapshot,design));
                        PlanAnnotationRules.Render(recipe,PlanAnnotationRules.Values(input,design,gaps),PlanAnnotations.Escape);
                    }
                    binding.AnnotationTemplate=recipe;
                    if(key==PlanAnnotations.TitleId&&recipe==null) {input.PlanTitle=null;ProjectDataStorage.Write(doc.Database,tr,"Project",input);}
                    ComponentBinding.Register(doc.Database,tr);ComponentBinding.Tag(entity,key,binding.Role);
                    ProjectDataStorage.Write(doc.Database,tr,"PlanBindings",manifest);tr.Commit();
                    ed.WriteMessage("\nSemantic annotation configured: "+key+". Run UPDATEALL.\n");
                }
            }
            catch(System.Exception ex){ed.WriteMessage("\nAnnotation configuration stopped: "+ex.Message+"\n");}
        }
        [CommandMethod("ELEVATORPLANAUDIT",CommandFlags.Modal)]
        public void Audit()
        {
            var doc=Application.DocumentManager.MdiActiveDocument;
            try
            {
                using(var tr=doc.Database.TransactionManager.StartTransaction())
                {
                    var snapshot=DrawingReader.Read(doc.Database,tr);var report=PlanCoverage.Read(doc.Database,tr,snapshot);
                    string directory=Path.Combine(Configuration.Root,"output");Directory.CreateDirectory(directory);
                    string file=Path.Combine(directory,"PlanAudit_"+Guid.NewGuid().ToString("N")+".json");Configuration.WriteJson(file,report);
                    doc.Editor.WriteMessage("\nPLAN: "+report.BoundEntities+"/"+report.ModelEntities+" entities bound; "+report.UnboundEntities+" unbound; "+report.Dimensions+" dimensions; "+report.Hatches+" hatches; "+report.DrivenAnnotations+" driven text templates; "+report.StaticAnnotations.Length+" static annotations.\nReport: "+file+"\n");
                }
            }
            catch(System.Exception ex){doc.Editor.WriteMessage("\nPlan audit stopped: "+ex.Message+"\n");}
        }
    }
}
