using System;

namespace Elevator.Core.GuideRails
{
    /// <summary>The EN 81-50:2014 5.10 formulas, one function each (GUIDE-RAILS.md 5 to 6).</summary>
    public static class GuideRailMath
    {
        /// <summary>gn, the standard acceleration of free fall.</summary>
        public const double Gn = 9.81;

        /// <summary>ω for Rm = 370 N/mm² (EN 81-50 5.10.3). Defined for 20 ≤ λ ≤ 250.</summary>
        public static double Omega370(double lambda)
        {
            if (lambda <= 60)
                return 0.00012920 * Math.Pow(lambda, 1.89) + 1;
            if (lambda <= 85)
                return 0.00004627 * Math.Pow(lambda, 2.14) + 1;
            if (lambda <= 115)
                return 0.00001711 * Math.Pow(lambda, 2.35) + 1.04;
            return 0.00016887 * Math.Pow(lambda, 2.00);
        }

        /// <summary>ω for Rm = 520 N/mm² (EN 81-50 5.10.3). Defined for 20 ≤ λ ≤ 250.</summary>
        public static double Omega520(double lambda)
        {
            if (lambda <= 50)
                return 0.00008240 * Math.Pow(lambda, 2.06) + 1.021;
            if (lambda <= 70)
                return 0.00001895 * Math.Pow(lambda, 2.41) + 1.05;
            if (lambda <= 89)
                return 0.00002447 * Math.Pow(lambda, 2.36) + 1.03;
            return 0.00025330 * Math.Pow(lambda, 2.00);
        }

        /// <summary>ω for an Rm of 370 to 520 N/mm²: linear between the two columns (EN 81-50 5.10.3).</summary>
        public static double Omega(double lambda, double rm)
        {
            double a = Omega370(lambda);
            if (rm <= 370)
                return a;
            double b = Omega520(lambda);
            return (b - a) / (520.0 - 370.0) * (rm - 370) + a;
        }

        /// <summary>M = 3·F·l / 16 (EN 81-50 5.10.2.1); σ = M / W.</summary>
        public static double BendingStress(double forceN, double spacingMm, double modulusMm3)
        {
            return 3.0 * forceN * spacingMm / (16.0 * modulusMm3);
        }

        /// <summary>σF (EN 81-50 5.10.5): sliding or roller guide shoes.</summary>
        public static double FlangeBending(RailProfile p, double fxN)
        {
            if (p.ShoeType == RailProfile.Roller)
                return 1.85 * fxN / (p.NeckMm * p.NeckMm);
            return 6.0 * fxN * (p.HeightMm - p.ShoeHalfWidthMm - p.FootMm)
                / (p.NeckMm * p.NeckMm * (p.ShoeLiningLengthMm + 2.0 * (p.HeightMm - p.FootMm)));
        }

        /// <summary>δ = 0.7·F·l³ / (48·E·I) (EN 81-50 5.10.6), without the building-structure term.</summary>
        public static double Deflection(double forceN, double spacingMm, double e, double inertiaMm4)
        {
            return 0.7 * forceN * spacingMm * spacingMm * spacingMm / (48.0 * e * inertiaMm4);
        }
    }
}
