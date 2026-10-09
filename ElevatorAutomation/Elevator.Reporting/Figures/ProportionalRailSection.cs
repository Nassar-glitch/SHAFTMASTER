using System;
using System.Collections.Generic;
using System.Globalization;

namespace Elevator.Reporting.Figures
{
    public sealed class ProportionalRailSection : IRailSection
    {
        public string Designation
        {
            get; private set;
        }

        public double BaseWidthMm
        {
            get; private set;
        }

        public double HeightMm
        {
            get; private set;
        }

        public double BladeWidthMm
        {
            get; private set;
        }

        public bool Proportional
        {
            get
            {
                return true;
            }
        }

        // "T70/65/9" → b1 = 70, h1 = 65, k = 9. Returns null if the designation does not have that form.
        public static ProportionalRailSection Parse(string designation)
        {
            if (String.IsNullOrWhiteSpace(designation))
                return null;
            var m = System.Text.RegularExpressions.Regex.Match(
                designation.Trim(),
                @"^T\s*(\d+(?:\.\d+)?)\s*/\s*(\d+(?:\.\d+)?)\s*/\s*(\d+(?:\.\d+)?)$");
            if (!m.Success)
                return null;
            double b = Double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), h = Double.Parse(
                m.Groups[2].Value,
                CultureInfo.InvariantCulture), k = Double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (b <= 0 || h <= 0 || k <= 0 || k >= b || k >= h)
                return null;
            return new ProportionalRailSection { Designation = designation.Trim(), BaseWidthMm = b, HeightMm = h, BladeWidthMm = k };
        }

        // Proportional shape (undimensioned features): foot thickness 0.12 h1 tapering to 0.09 h1 at the edges, root width 1.6 k.
        public IList<FigurePoint> Outline()
        {
            double b = BaseWidthMm / 2, h = HeightMm, k = BladeWidthMm / 2, foot = 0.12 * h, edge = 0.09 * h, root = 0.8 * BladeWidthMm;
            return new List<FigurePoint> { new FigurePoint(-b, 0),
                new FigurePoint(b, 0),
                new FigurePoint(b, edge),
                new FigurePoint(root, foot),
                new FigurePoint(k, foot + 0.6 * root),
                new FigurePoint(k, h),
                new FigurePoint(-k, h),
                new FigurePoint(-k, foot + 0.6 * root),
                new FigurePoint(-root, foot),
                new FigurePoint(-b, edge) };
        }
    }
}
