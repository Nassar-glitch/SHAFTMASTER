using System.Linq;
using Elevator.Core.Presets;

namespace Elevator.Core.GuideRails
{
    /// <summary>Selects the counterweight rail gauge from the shaft width.</summary>
    public static class RailRules
    {
        /// <summary>The largest preferred counterweight rail gauge that fits the shaft width.</summary>
        public static double SelectCounterweightDbg(double sw, EquipmentProfile p)
        {
            return p.PreferredCounterweightDbgMm
                .OrderByDescending(v => v)
                .FirstOrDefault(v => v > 0 && v <= sw - 2 * p.CounterweightRailSideAllowanceMm + 1e-7);
        }
    }
}
