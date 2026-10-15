using System;
using System.Collections.Generic;

namespace Elevator.Core.Steel
{
    /// <summary>
    /// One channel of the UPN catalogue (config/presets.json, SteelShaft.Channels; DIN 1026-1 / EN 10279). Dimensions in mm, area in cm²,
    /// mass in kg/m, second moments in cm⁴, section moduli in cm³, as the producer tables print them. Ix and Wx are about the web-parallel
    /// (strong) axis, Iy and Wy about the weak axis; e is the distance of the weak axis from the back of the web. Editable.
    /// </summary>
    public sealed class ChannelProfile
    {
        public string Name;
        /// <summary>h: depth of the section (web direction).</summary>
        public double HeightMm;
        /// <summary>b: flange width.</summary>
        public double WidthMm;
        /// <summary>tw: web thickness.</summary>
        public double WebMm;
        /// <summary>tf: flange thickness (mean, the flange is inclined 8 %).</summary>
        public double FlangeMm;
        /// <summary>r1: root radius.</summary>
        public double RootRadiusMm;
        public double AreaCm2;
        public double MassKgPerM;
        public double IxCm4;
        public double WxCm3;
        public double IyCm4;
        public double WyCm3;
        /// <summary>e: centroid distance from the back of the web.</summary>
        public double EccentricityMm;
        /// <summary>The values of this row that are not confirmed against a producer table (catalogue maintenance only; never printed).</summary>
        public string Check;

        public ChannelProfile Clone()
        {
            return (ChannelProfile)MemberwiseClone();
        }

        public List<string> Validate()
        {
            var p = new List<string>();
            string n = "SteelShaft.Channels " + (Name ?? "?") + ": ";
            if (String.IsNullOrWhiteSpace(Name))
                p.Add("SteelShaft.Channels rows need a Name");
            if (!(P(HeightMm) && P(WidthMm) && P(WebMm) && P(FlangeMm) && P(RootRadiusMm) && P(AreaCm2) && P(MassKgPerM)))
                p.Add(n + "dimensions, area and mass per metre must be positive");
            if (!(P(IxCm4) && P(WxCm3) && P(IyCm4) && P(WyCm3) && P(EccentricityMm)))
                p.Add(n + "second moments, section moduli and e must be positive");
            if (P(HeightMm) && P(WebMm) && P(FlangeMm) && 2 * FlangeMm >= HeightMm)
                p.Add(n + "the flanges must be thinner than half the depth");
            if (P(WidthMm) && P(WebMm) && WebMm >= WidthMm)
                p.Add(n + "the web must be thinner than the flange width");
            return p;
        }

        internal static bool P(double v)
        {
            return !Double.IsNaN(v) && !Double.IsInfinity(v) && v > 0;
        }
    }

    /// <summary>One equal angle of the catalogue (SteelShaft.Angles; EN 10056-1). Units as in ChannelProfile; Ix = Iy.</summary>
    public sealed class AngleProfile
    {
        public string Name;
        /// <summary>a: leg length.</summary>
        public double LegMm;
        /// <summary>t: thickness.</summary>
        public double ThicknessMm;
        public double RootRadiusMm;
        public double ToeRadiusMm;
        public double AreaCm2;
        public double MassKgPerM;
        public double IxCm4;
        /// <summary>e: centroid distance from the back of a leg.</summary>
        public double EccentricityMm;
        public string Check;

        public AngleProfile Clone()
        {
            return (AngleProfile)MemberwiseClone();
        }

        public List<string> Validate()
        {
            var p = new List<string>();
            string n = "SteelShaft.Angles " + (Name ?? "?") + ": ";
            if (String.IsNullOrWhiteSpace(Name))
                p.Add("SteelShaft.Angles rows need a Name");
            if (!(ChannelProfile.P(LegMm) && ChannelProfile.P(ThicknessMm) && ChannelProfile.P(AreaCm2) && ChannelProfile.P(MassKgPerM)))
                p.Add(n + "dimensions, area and mass per metre must be positive");
            if (!(ChannelProfile.P(IxCm4) && ChannelProfile.P(EccentricityMm)))
                p.Add(n + "Ix and e must be positive");
            if (ChannelProfile.P(LegMm) && ChannelProfile.P(ThicknessMm) && ThicknessMm >= LegMm)
                p.Add(n + "the thickness must be smaller than the leg");
            return p;
        }
    }
}
