using System;

namespace Elevator.Core.GuideRails
{
    /// <summary>
    /// One guide-rail profile of the catalog (config/presets.json, GuideRails.Profiles): section values, mass per metre, material and
    /// the guide-shoe data of the flange-bending check. Source of the first values: GUIDE-RAILS.md section 8 and 13; editable.
    /// Units: mm, mm², mm³, mm⁴, kg/m, N/mm².
    /// </summary>
    public sealed class RailProfile
    {
        /// <summary>Elongation band Over12: A5 &gt; 12 % (EN 81-20 Table 15 St 2.25 / 1.8).</summary>
        public const string Over12 = "Over12";
        /// <summary>Elongation band Between8And12: 8 % ≤ A5 ≤ 12 % (St 3.75 / 3.0).</summary>
        public const string Between8And12 = "Between8And12";
        /// <summary>Sliding guide shoes (EN 81-50 5.10.5 sliding formula).</summary>
        public const string Sliding = "Sliding";
        /// <summary>Roller guide shoes (EN 81-50 5.10.5 roller formula).</summary>
        public const string Roller = "Roller";

        public string Name;
        /// <summary>h1: rail height, back to blade tip.</summary>
        public double HeightMm;
        /// <summary>b1: rail width (flange).</summary>
        public double WidthMm;
        /// <summary>k: blade thickness.</summary>
        public double BladeMm;
        /// <summary>c: width of the connection of foot and blade (flange bending).</summary>
        public double NeckMm;
        /// <summary>f: foot depth at the connection (flange bending).</summary>
        public double FootMm;
        public double AreaMm2;
        public double MassKgPerM;
        public double IxxMm4;
        public double WxxMm3;
        public double IxMm;
        public double IyyMm4;
        public double WyyMm3;
        public double IyMm;
        public double RmNmm2;
        public string A5Band;
        public string ShoeType;
        /// <summary>l: length of the guide-shoe lining.</summary>
        public double ShoeLiningLengthMm;
        /// <summary>b: half the width of the guide-shoe lining.</summary>
        public double ShoeHalfWidthMm;

        /// <summary>The minimum radius of gyration i.</summary>
        public double MinimumRadiusMm
        {
            get
            {
                return Math.Min(IxMm, IyMm);
            }
        }

        /// <summary>The safety factor St of EN 81-20 Table 15 for the A5 band; null when the band is not valid.</summary>
        public double? SafetyFactor(bool safetyDevice)
        {
            if (A5Band == Over12)
                return safetyDevice ? 1.8 : 2.25;
            if (A5Band == Between8And12)
                return safetyDevice ? 3.0 : 3.75;
            return null;
        }

        /// <summary>The problems of the row; empty when valid.</summary>
        public System.Collections.Generic.List<string> Validate()
        {
            var p = new System.Collections.Generic.List<string>();
            string n = "GuideRails.Profiles " + (Name ?? "?") + ": ";
            if (String.IsNullOrWhiteSpace(Name))
                p.Add("GuideRails.Profiles rows need a Name");
            if (!(P(HeightMm) && P(WidthMm) && P(BladeMm) && P(NeckMm) && P(FootMm) && P(AreaMm2) && P(MassKgPerM)))
                p.Add(n + "dimensions, area and mass per metre must be positive");
            if (!(P(IxxMm4) && P(WxxMm3) && P(IxMm) && P(IyyMm4) && P(WyyMm3) && P(IyMm)))
                p.Add(n + "second moments, section moduli and radii of gyration must be positive");
            if (!(RmNmm2 >= 370 && RmNmm2 <= 520))
                p.Add(n + "RmNmm2 must be 370 to 520 (omega polynomials of EN 81-50 5.10.3)");
            if (A5Band != Over12 && A5Band != Between8And12)
                p.Add(n + "A5Band must be " + Over12 + " or " + Between8And12 + " (A5 below 8 % is too brittle)");
            if (ShoeType != Sliding && ShoeType != Roller)
                p.Add(n + "ShoeType must be " + Sliding + " or " + Roller);
            if (!P(ShoeLiningLengthMm) || !P(ShoeHalfWidthMm))
                p.Add(n + "ShoeLiningLengthMm and ShoeHalfWidthMm must be positive");
            return p;
        }

        static bool P(double v)
        {
            return !Double.IsNaN(v) && !Double.IsInfinity(v) && v > 0;
        }
    }
}
