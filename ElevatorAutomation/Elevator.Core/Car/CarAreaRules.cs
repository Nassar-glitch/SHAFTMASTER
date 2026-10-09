using System;

namespace Elevator.Core.Car
{
    // EN 81-20:2014 5.4.2.1, Table 6: rated load and maximum available car area (linear interpolation; beyond 2500 kg add
    // 0.16 m² per extra 100 kg).
    public static class CarAreaRules
    {
        static readonly double[,] Table6 = { { 100, 0.37 },
            { 180, 0.58 },
            { 225, 0.70 },
            { 300, 0.90 },
            { 375, 1.10 },
            { 400, 1.17 },
            { 450, 1.30 },
            { 525, 1.45 },
            { 600, 1.60 },
            { 630, 1.66 },
            { 675, 1.75 },
            { 750, 1.90 },
            { 800, 2.00 },
            { 825, 2.05 },
            { 900, 2.20 },
                { 975, 2.35 },
                { 1000, 2.40 },
                { 1050, 2.50 },
                { 1125, 2.65 },
                { 1200, 2.80 },
                { 1250, 2.90 },
                { 1275, 2.95 },
                { 1350, 3.10 },
                { 1425, 3.25 },
                { 1500, 3.40 },
                { 1600, 3.56 },
                { 2000, 4.20 },
                { 2500, 5.00 } };

        public static double? MaximumAvailableAreaM2(double ratedLoadKg)
        {
            if (Double.IsNaN(ratedLoadKg) || ratedLoadKg < 100)
                return null;
            int n = Table6.GetLength(0);
            if (ratedLoadKg > 2500)
                return 5.00 + 0.16 * (ratedLoadKg - 2500) / 100;
            for (int i = 0; i + 1 < n; i++)
            {
                if (ratedLoadKg <= Table6[i + 1, 0])
                {
                    return Table6[i, 1]
                                            + (Table6[i + 1, 1] - Table6[i, 1]) * (ratedLoadKg - Table6[i, 0])
                                                / (Table6[i + 1, 0] - Table6[i, 0]);
                }
            }
            return Table6[n - 1, 1];
        }

        // Inverse of Table 6: the smallest rated load whose maximum available area is at least the given car area
        // (same linear interpolation; beyond 5.00 m² the 2500 kg rule). Rounded up to whole kilograms.
        public static double? MinimumRatedLoadKg(double areaM2)
        {
            if (Double.IsNaN(areaM2) || areaM2 <= 0)
                return null;
            int n = Table6.GetLength(0);
            if (areaM2 <= Table6[0, 1])
                return Table6[0, 0];
            double q;
            if (areaM2 > Table6[n - 1, 1])
                q = 2500 + (areaM2 - 5.00) / 0.16 * 100;
            else
            {
                q = Table6[n - 1, 0];
                for (int i = 0; i + 1 < n; i++)
                {
                    if (areaM2 <= Table6[i + 1, 1])
                    {
                        q = Table6[i, 0] + (Table6[i + 1, 0] - Table6[i, 0]) * (areaM2 - Table6[i, 1]) / (Table6[i + 1, 1] - Table6[i, 1]);
                        break;
                    }
                }
            }
            return Math.Ceiling(q - 1e-6);
        }
    }
}
