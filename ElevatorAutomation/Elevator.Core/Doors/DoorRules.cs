using System.Linq;
using Elevator.Core.Presets;

namespace Elevator.Core.Doors
{
    public static class DoorRules
    {
        public static double SelectWidth(double sw, double carWidth, double jambWidth, EquipmentProfile p)
        {
            return p.PreferredDoorWidthsMm
                .OrderByDescending(v => v)
                .FirstOrDefault(v => v > 0 && v <= carWidth - 2 * p.MinimumCabinDoorReturnMm + 1e-7 && v + 2 * jambWidth <= sw + 1e-7);
        }
    }
}
