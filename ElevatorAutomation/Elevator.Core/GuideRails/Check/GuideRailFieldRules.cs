using System;
using System.Linq;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.Core.GuideRails
{
    /// <summary>
    /// Which Advanced rail values the page shows: a value is shown only while it affects the result (DESIGN-FLOW "Guide rails").
    /// </summary>
    public static class GuideRailFieldRules
    {
        /// <summary>Values that only matter when the machine is supported on the guide rails (the number of rails carrying it).</summary>
        public static bool MachineValuesShown(PresetContext c)
        {
            var v = ProjectPresets.Find(ProjectPresets.MachineOnRailsKey).Effective(c);
            return v != null && Convert.ToBoolean(v);
        }

        /// <summary>The clip push-through force only matters when the travel exceeds the limit of EN 81-20 5.7.2.3.5.</summary>
        public static bool PushThroughShown(PresetContext c)
        {
            try
            {
                var served = ProjectFloors.Elevations(c.Project.Building).Where(f => f.IsServed).ToList();
                if (served.Count < 2 || c.Library == null)
                    return true;
                double travelM = (served.Last().ElevationMm - served.First().ElevationMm) / 1000.0;
                return travelM > c.Library.GuideRailsOrDefault().PushThroughMaxTravelM + 1e-9;
            }
            catch (ArgumentException)
            {
                return true;
            }
        }

        /// <summary>The lining length only enters the flange-bending formula of sliding guide shoes.</summary>
        public static bool LiningShown(PresetContext c, bool car)
        {
            var v = ProjectPresets.Find(car ? "Rails.CarShoeType" : "Rails.CwShoeType").Effective(c) as string;
            return v != RailProfile.Roller;
        }
    }
}
