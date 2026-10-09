using System;
using System.IO;
using System.Linq;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.Core.Application
{
    // Project session (ARCHITECTURE-PLAN P4): the open project, its file, the unsaved-changes state and the drawing options.
    public sealed class ProjectSession
    {
        public ElevatorProject Project;
        public string FilePath;
        public bool Dirty;
        public DrawingViews Views = DrawingViews.All;
        public bool Arrange = true;

        public ProjectSession(ElevatorProject project)
        {
            Project = project;
        }

        // A new project opens complete from config/presets.json (DESIGN-FLOW stage 2); every unlocked value follows the presets.
        // The audited counterweight reference pose and bracket source are kept from the reviewed example (seedPath).
        public static ElevatorProject NewProject(string seedPath, PresetLibrary presets, DesignContext context)
        {
            var seed = ProjectJson.Deserialize(File.ReadAllText(seedPath));
            return ProjectPresets.CreateNew(presets, context, seed.Counterweight, seed.Rails.BracketSource);
        }

        public void Save(string path)
        {
            path = Path.GetFullPath(path);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, ProjectJson.Serialize(Project));
                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            FilePath = path;
            Dirty = false;
        }

        public void Open(string path)
        {
            var p = ProjectJson.Deserialize(File.ReadAllText(path));
            var errors = ProjectValidator.Validate(p, DrawingViews.Plan);
            if (errors.Any(e => e.Message == "Required project group is missing."))
                throw new ArgumentException("Incomplete project structure.");
            Project = p;
            FilePath = path;
            Dirty = false;
        }

        public void MoveIntermediate(string id, int delta)
        {
            var floors = Project.Building.Floors;
            int index = floors.FindIndex(f => f.FloorId == id), target = index + delta;
            if (index <= 0 || index >= floors.Count - 1 || target <= 0 || target >= floors.Count - 1)
                throw new ArgumentException("Only intermediate floors can move; keep the lowest datum and final floor fixed.");
            var clone = ProjectJson.Clone(Project);
            var item = clone.Building.Floors[index];
            clone.Building.Floors.RemoveAt(index);
            clone.Building.Floors.Insert(target, item);
            ProjectFloors.Elevations(clone.Building); // retain interval with floor, validate before committing
            Project.Building = clone.Building;
            Dirty = true;
        }
    }
}
