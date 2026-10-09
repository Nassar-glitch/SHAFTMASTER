using System;
using System.Linq;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
namespace Elevator.Core.Application
{
    // DESIGN-FLOW stage 2 (ARCHITECTURE-PLAN P4): project fields follow config/presets.json until the user edits them (lock);
    // Reset unlocks. UI-free: the application refreshes its editors after these calls.
    public sealed class PresetFlow
    {
        readonly DesignContext context;
        public PresetLibrary Library {get;private set;}
        // The reason the preset library could not be read (null when it was read).
        public string Error {get;private set;}
        // The rope sizing of the last full preset application or fill.
        public RopeSizing LastRopeSizing {get;private set;}
        public PresetFlow(PresetLibrary library,string error,DesignContext context){Library=library;Error=error;this.context=context;}
        public static PresetFlow Load(string path,DesignContext context)
        {
            try{return new PresetFlow(PresetLibrary.Load(path),null,context);}
            catch(Exception e){return new PresetFlow(null,e.Message,context);}
        }
        // The preset-managed field of an editor path, or null.
        public static PresetField Managed(string path){return ProjectPresets.Fields.FirstOrDefault(f=>f.Key==path);}
        // Writes the presets of every unlocked field. Rope sizing runs the rope / traction checks, so typing skips it
        // (sizeRopes false). False when nothing follows (no library, or the project does not follow the presets).
        public bool Apply(ElevatorProject project,bool sizeRopes)
        {
            if(Library==null||!ProjectPresets.Follows(project))return false;
            if(sizeRopes)LastRopeSizing=ProjectPresets.Apply(project,Library,context);
            else{var c=new PresetContext{Project=project,Library=Library};foreach(var f in ProjectPresets.Fields)if(!f.IsEdited(project))f.Store(project,f.Preset(c));}
            return true;
        }
        // An edited managed field locks (while the project follows the presets) and the project becomes unsaved.
        public void Edited(ProjectSession session,PresetField field)
        {
            if(ProjectPresets.Follows(session.Project))ProjectPresets.Lock(session.Project,field.Key);
            session.Dirty=true;
        }
        // Reset to preset: the field unlocks and follows again.
        public void Reset(ProjectSession session,PresetField field){field.Reset(session.Project);session.Dirty=true;}
        // True when the page (null: every page) has blank preset fields that Fill blanks would set.
        public bool CanFillBlanks(ElevatorProject project,string page){return Library!=null&&ProjectPresets.AnyBlank(project,page);}
        public void EnsureLibrary(){if(Library==null)throw new ArgumentException("The preset library could not be read: "+Error);}
        public void FillBlanks(ProjectSession session,string page)
        {
            EnsureLibrary();
            LastRopeSizing=ProjectPresets.FillBlanks(session.Project,Library,context,page);
            session.Dirty=true;
        }
        // Persons shown next to Q: floor(Q / 75 kg).
        public int? PersonsFor(double? capacityKg){return Library==null?null:Library.PersonsFor(capacityKg);}
        public PresetContext Context(ElevatorProject project)
        {return new PresetContext{Project=project,Library=Library,DeflectorPitchMm=RopeTractionCalculationEngine.DeflectorPitch(project,context)};}
    }
}
