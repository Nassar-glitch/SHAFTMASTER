using System.Linq;
using Elevator.Core.Presets;

namespace Elevator.Core.Doors
{
    /// <summary>Selects the car door width from the shaft, car and jamb widths.</summary>
    public static class DoorRules
    {
        /// <summary>The largest preferred door width that fits the car and the jambs; 0 when none fits.</summary>
        public static double SelectWidth(double sw, double carWidth, double jambWidth, EquipmentProfile p)
        {
            return p.PreferredDoorWidthsMm
                .OrderByDescending(v => v)
                .FirstOrDefault(v => v > 0 && v <= carWidth - 2 * p.MinimumCabinDoorReturnMm + 1e-7 && v + 2 * jambWidth <= sw + 1e-7);
        }
    }
}
