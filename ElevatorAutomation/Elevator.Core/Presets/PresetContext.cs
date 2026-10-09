using Elevator.Core.Project;

namespace Elevator.Core.Presets
{
    // Generic preset / lock / follow mechanism (DESIGN-FLOW principles 2–4). A field is locked once the user edits it
    // (its key is listed in ElevatorProject.UserEditedFields); an unlocked field always takes its preset, which may
    // follow an upstream driver. Reset removes the lock. Older projects have no list: every field is unlocked.
    public sealed class PresetContext
    {
        public ElevatorProject Project;
        public PresetLibrary Library;
        public double? DeflectorPitchMm; // driver for the deflector mass preset (from the shared deflector rule)
    }
}
