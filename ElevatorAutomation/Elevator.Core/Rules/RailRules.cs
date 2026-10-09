using System.Linq;

namespace Elevator.Core
{
    public static class RailRules
    {
        public static double SelectCounterweightDbg(double sw, EquipmentProfile p)
        {
            return p.PreferredCounterweightDbgMm
                .OrderByDescending(v => v)
                .FirstOrDefault(v => v > 0 && v <= sw - 2 * p.CounterweightRailSideAllowanceMm + 1e-7);
        }
    }
}
