using System;
using System.Collections.Generic;
using System.Linq;

namespace Elevator.Core.GuideRails
{
    /// <summary>
    /// The guide-rail group of config/presets.json: the rail catalog and the presets of the EN 81-50 5.10 check (GUIDE-RAILS.md 13).
    /// Every value is an editable design default, never a standard factor.
    /// </summary>
    public sealed class GuideRailPreset
    {
        /// <summary>n: number of guide rails of a line (Q14).</summary>
        public int RailCount;
        /// <summary>E in N/mm² (Q2).</summary>
        public double ElasticModulusNmm2;
        /// <summary>The largest bracket spacing the recommendation returns.</summary>
        public double MaxSpacingMm;
        public double SpacingStepMm;
        /// <summary>The smallest bracket spacing the recommendation returns.</summary>
        public double MinSpacingMm;
        public bool MachineOnRails;
        public double StructureDeflectionXMm;
        public double StructureDeflectionYMm;
        /// <summary>Running impact factor (Table 14 k2).</summary>
        public double RunningImpactFactor;
        /// <summary>n_m: number of rails that carry the machine frame (two car + two counterweight rails; GUIDE-RAILS.md 13.4).</summary>
        public int MachineRailCount;
        public double ProgressiveK1;
        public double InstantaneousK1;
        /// <summary>Fs = factor · gn · Q (EN 81-20 5.7.2.3.6; 0.4 passenger lifts).</summary>
        public double SillForceFactor;
        public double CwShoeSpacingMm;
        public double CwEccentricityWidthPercent;
        public double CwEccentricityDepthPercent;
        /// <summary>Fr: push-through force of all clips of one bracket.</summary>
        public double ClipPushThroughN;
        /// <summary>Travel up to which Fp is ignored (EN 81-20 5.7.2.3.5).</summary>
        public double PushThroughMaxTravelM;
        public double CarDeflectionLimitMm;
        public double CwDeflectionLimitMm;
        public double MaxSlenderness;
        public double MinSlenderness;
        /// <summary>The catalog, in the order of the recommendation (smallest first).</summary>
        public List<RailProfile> Profiles;

        /// <summary>The fixing presets (reach of a wall bracket, the cross arm section); null in an older library: the shipped defaults.</summary>
        public RailFixingPreset Fixing;

        public RailFixingPreset FixingOrDefault()
        {
            return Fixing ?? new RailFixingPreset();
        }

        public GuideRailPreset Clone()
        {
            return (GuideRailPreset)MemberwiseClone();
        }

        /// <summary>The profile with a name, or null.</summary>
        public RailProfile Profile(string name)
        {
            return Profiles == null ? null : Profiles.FirstOrDefault(p => p != null && p.Name == name);
        }

        /// <summary>The spacings of the recommendation, largest first: MaxSpacingMm down to MinSpacingMm in SpacingStepMm.</summary>
        public List<double> Spacings()
        {
            var list = new List<double>();
            if (SpacingStepMm <= 0)
                return list;
            for (double l = MaxSpacingMm; l >= MinSpacingMm - 1e-9; l -= SpacingStepMm)
                list.Add(l);
            return list;
        }

        /// <summary>The problems of the group; empty when valid.</summary>
        public List<string> Validate()
        {
            var p = new List<string>();
            if (RailCount != 2)
                p.Add("GuideRails.RailCount must be 2");
            Action<string, double> pos = (name, v) =>
            {
                if (Double.IsNaN(v) || Double.IsInfinity(v) || v <= 0)
                    p.Add("GuideRails." + name + " must be positive");
            };
            Action<string, double> nonNeg = (name, v) =>
            {
                if (Double.IsNaN(v) || Double.IsInfinity(v) || v < 0)
                    p.Add("GuideRails." + name + " must be zero or positive");
            };
            pos("ElasticModulusNmm2", ElasticModulusNmm2);
            pos("MaxSpacingMm", MaxSpacingMm);
            pos("SpacingStepMm", SpacingStepMm);
            pos("MinSpacingMm", MinSpacingMm);
            nonNeg("StructureDeflectionXMm", StructureDeflectionXMm);
            nonNeg("StructureDeflectionYMm", StructureDeflectionYMm);
            pos("RunningImpactFactor", RunningImpactFactor);
            if (MachineRailCount < 1)
                p.Add("GuideRails.MachineRailCount must be at least 1");
            pos("ProgressiveK1", ProgressiveK1);
            pos("InstantaneousK1", InstantaneousK1);
            pos("SillForceFactor", SillForceFactor);
            pos("CwShoeSpacingMm", CwShoeSpacingMm);
            pos("CwEccentricityWidthPercent", CwEccentricityWidthPercent);
            pos("CwEccentricityDepthPercent", CwEccentricityDepthPercent);
            nonNeg("ClipPushThroughN", ClipPushThroughN);
            nonNeg("PushThroughMaxTravelM", PushThroughMaxTravelM);
            pos("CarDeflectionLimitMm", CarDeflectionLimitMm);
            pos("CwDeflectionLimitMm", CwDeflectionLimitMm);
            pos("MaxSlenderness", MaxSlenderness);
            pos("MinSlenderness", MinSlenderness);
            if (Fixing != null)
            {
                pos("Fixing.CarReachMm", Fixing.CarReachMm);
                pos("Fixing.CwSideReachMm", Fixing.CwSideReachMm);
                pos("Fixing.ArmHeightMm", Fixing.ArmHeightMm);
                pos("Fixing.ArmWidthMm", Fixing.ArmWidthMm);
                pos("Fixing.ArmWebMm", Fixing.ArmWebMm);
                pos("Fixing.ArmFlangeMm", Fixing.ArmFlangeMm);
            }
            if (MinSpacingMm > MaxSpacingMm)
                p.Add("GuideRails.MinSpacingMm must not exceed MaxSpacingMm");
            if (Profiles == null || Profiles.Count == 0 || Profiles.Any(x => x == null))
                p.Add("GuideRails.Profiles is required");
            else
            {
                foreach (var row in Profiles)
                    p.AddRange(row.Validate());
                if (Profiles.Select(x => x.Name).Distinct().Count() != Profiles.Count)
                    p.Add("GuideRails.Profiles names must be unique");
            }
            return p;
        }
    }
}
