using System;
using System.Linq;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Suspension;

namespace Elevator.Core.Application
{
    /// <summary>
    /// DESIGN-FLOW stage 2 (ARCHITECTURE-PLAN P4): project fields follow config/presets.json until the user edits them (lock); Reset
    /// unlocks. UI-free: the application refreshes its editors after these calls.
    /// </summary>
    public sealed class PresetFlow
    {
        readonly DesignContext context;

        public PresetLibrary Library { get; private set; }

        // The reason the preset library could not be read (null when it was read).
        public string Error { get; private set; }

        // The rope sizing of the last full preset application or fill.
        public RopeSizing LastRopeSizing { get; private set; }

        public PresetFlow(PresetLibrary library, string error, DesignContext context)
        {
            Library = library;
            Error = error;
            this.context = context;
        }

        /// <summary>Reads the preset library from a file; the flow keeps the error when it cannot be read.</summary>
        public static PresetFlow Load(string path, DesignContext context)
        {
            try
            {
                return new PresetFlow(PresetLibrary.Load(path), null, context);
            }
            catch (Exception e)
            {
                return new PresetFlow(null, e.Message, context);
            }
        }

        /// <summary>The preset-managed field of an editor path, or null.</summary>
        public static PresetField Managed(string path)
        {
            return ProjectPresets.Fields.FirstOrDefault(f => f.Key == path);
        }

        /// <summary>
        /// Writes the presets of every unlocked field. Rope sizing runs the rope / traction checks, so typing skips it (sizeRopes false).
        /// False when nothing follows (no library, or the project does not follow the presets).
        /// </summary>
        public bool Apply(ElevatorProject project, bool sizeRopes)
        {
            if (Library == null || !ProjectPresets.Follows(project))
                return false;
            if (sizeRopes)
                LastRopeSizing = ProjectPresets.Apply(project, Library, context);
            else
            {
                var c = new PresetContext { Project = project, Library = Library };
                foreach (var f in ProjectPresets.Fields)
                {
                    if (!f.IsEdited(project))
                        f.Store(project, f.Preset(c));
                }
            }
            return true;
        }

        /// <summary>An edited managed field locks (while the project follows the presets) and the project becomes unsaved.</summary>
        public void Edited(ProjectSession session, PresetField field)
        {
            if (ProjectPresets.Follows(session.Project))
                ProjectPresets.Lock(session.Project, field.Key);
            session.Dirty = true;
        }

        /// <summary>Reset to preset: the field unlocks and follows again.</summary>
        public void Reset(ProjectSession session, PresetField field)
        {
            field.Reset(session.Project);
            session.Dirty = true;
        }

        /// <summary>True when the page (null: every page) has blank preset fields that Fill blanks would set.</summary>
        public bool CanFillBlanks(ElevatorProject project, string page)
        {
            return Library != null && ProjectPresets.AnyBlank(project, page);
        }

        /// <summary>Throws ArgumentException when the preset library could not be read.</summary>
        public void EnsureLibrary()
        {
            if (Library == null)
                throw new ArgumentException("The preset library could not be read: " + Error);
        }

        /// <summary>Sets the blank preset fields of a page (null: every page) and starts following the presets.</summary>
        public void FillBlanks(ProjectSession session, string page)
        {
            EnsureLibrary();
            LastRopeSizing = ProjectPresets.FillBlanks(session.Project, Library, context, page);
            session.Dirty = true;
        }

        /// <summary>Persons shown next to Q: floor(Q / 75 kg).</summary>
        public int? PersonsFor(double? capacityKg)
        {
            return Library == null ? null : Library.PersonsFor(capacityKg);
        }

        /// <summary>The preset context of a project.</summary>
        public PresetContext Context(ElevatorProject project)
        {
            return new PresetContext
            {
                Project = project,
                Library = Library,
                DeflectorPitchMm = RopeTractionCalculationEngine.DeflectorPitch(project, context)
            };
        }
    }
}
