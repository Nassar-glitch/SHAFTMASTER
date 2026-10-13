using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Calculations;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.Core.GuideRails
{
    /// <summary>
    /// The recommendations of the rails page (DESIGN-FLOW "Guide rails", GUIDE-RAILS.md 9 and 13.3): the smallest profile that passes at the
    /// largest spacing, then the largest spacing in steps that passes with the chosen profile. A choice that fails is kept; these
    /// functions only propose.
    /// </summary>
    public static class GuideRailRecommendation
    {
        /// <summary>The recommended profile name: the first of the catalog order that passes at the largest spacing, else the largest profile.</summary>
        public static string Profile(GuideRailInputs g, bool car)
        {
            var list = g.Preset.Profiles;
            foreach (var p in list)
            {
                if (GuideRailCheckEngine.Line(g, car, p, g.Preset.MaxSpacingMm).Ok)
                    return p.Name;
            }
            return list[list.Count - 1].Name;
        }

        /// <summary>The recommended spacing: the largest of MaxSpacingMm, MaxSpacingMm − step … MinSpacingMm that passes; MinSpacingMm when none.</summary>
        public static double Spacing(GuideRailInputs g, bool car, RailProfile profile)
        {
            double? passing = LargestPassing(g, car, profile, g.Preset.MaxSpacingMm);
            return passing ?? g.Preset.MinSpacingMm;
        }

        /// <summary>The largest spacing at or below a limit, on the preset steps, for which the profile passes; null when none does.</summary>
        public static double? LargestPassing(GuideRailInputs g, bool car, RailProfile profile, double limitMm)
        {
            foreach (double l in g.Preset.Spacings())
            {
                if (l <= limitMm + 1e-9 && GuideRailCheckEngine.Line(g, car, profile, l).Ok)
                    return l;
            }
            return null;
        }

        /// <summary>The smallest profile of the catalog order that passes at a spacing; null when none does.</summary>
        public static RailProfile SmallestPassing(GuideRailInputs g, bool car, double spacingMm)
        {
            return g.Preset.Profiles.FirstOrDefault(p => GuideRailCheckEngine.Line(g, car, p, spacingMm).Ok);
        }

        /// <summary>
        /// The recommendation for a rail field of the project (Rails.CarProfile, Rails.CarSpacingMm, Rails.CwProfile, Rails.CwSpacingMm); null when the
        /// inputs of the check are not available.
        /// </summary>
        public static object ForField(PresetContext c, string key)
        {
            if (c.Design == null || c.Library == null)
                return null;
            var issues = new List<CalculationIssue>();
            var g = GuideRailInputs.Build(c.Project, c.Design, c.Library, issues);
            if (g == null)
                return null;
            bool car = key.StartsWith("Rails.Car", StringComparison.Ordinal);
            if (key == ProjectPresets.CwSpacingKey)
                return ProjectPresets.Find(ProjectPresets.CarSpacingKey).Effective(c);
            if (key == ProjectPresets.CwProfileKey)
            {
                // The counterweight spacing follows the car spacing, so the profile is the smallest that passes at that spacing.
                var followed = ProjectPresets.Find(ProjectPresets.CarSpacingKey).Effective(c);
                if (followed != null)
                {
                    var smallest = SmallestPassing(g, false, Convert.ToDouble(followed, System.Globalization.CultureInfo.InvariantCulture));
                    return smallest != null ? smallest.Name : g.Preset.Profiles[g.Preset.Profiles.Count - 1].Name;
                }
            }
            if (key.EndsWith("Profile", StringComparison.Ordinal))
                return Profile(g, car);
            string name = ProjectPresets.Find(car ? "Rails.CarProfile" : "Rails.CwProfile").Effective(c) as string;
            var profile = name == null ? null : g.Preset.Profile(name);
            return profile == null ? (object)null : Spacing(g, car, profile);
        }
    }
}
