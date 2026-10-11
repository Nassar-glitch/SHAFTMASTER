using Elevator.Core.Calculations;
using Elevator.Core.Project;

namespace Elevator.Core.Doors
{
    /// <summary>
    /// Adds the automatic door to an input fingerprint (the door changes the front zone, so the car depth). A Manual door returns the
    /// fingerprint unchanged, so every existing result and golden fingerprint stays exactly as it was.
    /// </summary>
    internal static class DoorFingerprint
    {
        internal static string Extend(string hash, ElevatorProject p, DesignContext c)
        {
            if (hash == null || p == null || p.Doors == null || !DoorTypes.IsAutomatic(p.Doors.Type))
                return hash;
            return CalculationFingerprint.Hash(new
            {
                Base = hash,
                Door = DoorTypes.Normalize(p.Doors.Type),
                Settings = DoorSettings.Read(p).ForFingerprint(),
                // No catalog in the context means the shipped default: the same hash as a context that carries it.
                Catalog = c == null || c.Doors == null ? DoorCatalog.Default() : c.Doors
            });
        }
    }
}
