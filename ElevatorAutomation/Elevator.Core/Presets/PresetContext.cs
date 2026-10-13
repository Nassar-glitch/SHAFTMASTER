using Elevator.Core.Project;

namespace Elevator.Core.Presets
{
    /// <summary>
    /// Generic preset / lock / follow mechanism (DESIGN-FLOW principles 2–4). A field is locked once the user edits it (its key is listed
    /// in ElevatorProject.UserEditedFields); an unlocked field always takes its preset, which may follow an upstream driver. Reset removes
    /// the lock. Older projects have no list: every field is unlocked.
    /// </summary>
    public sealed class PresetContext
    {
        public ElevatorProject Project;
        public PresetLibrary Library;
        public DesignContext Design; // the design context (plan dimensions) for the guide-rail recommendations; null when not known
        public double? DeflectorPitchMm; // driver for the deflector mass preset (from the shared deflector rule)
    }
}
