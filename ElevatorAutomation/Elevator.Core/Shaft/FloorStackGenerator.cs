using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Geometry;

namespace Elevator.Core.Shaft
{
    /// <summary>Creates and checks the floor stack of the vertical section.</summary>
    public static class FloorStackGenerator
    {
        /// <summary>Creates the modeled floor levels from the floor heights.</summary>
        public static List<VerticalFloor> Create(
            int modeledLevels,
            string convention,
            string first,
            double[] heights,
            List<VerticalFloor> previous)
        {
            if (modeledLevels < 3 || modeledLevels > 100)
                throw new ArgumentException("Use 3..100 modeled levels including R (at least two normal levels).");
            if (heights == null || heights.Length != modeledLevels - 2 || heights.Any(h => !Finite(h) || h <= 0))
                throw new ArgumentException("One positive interval is required between each pair of normal floors; R is derived from OH.");
            int start = 0;
            if (convention == "Numeric")
            {
                if (!Int32.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out start))
                    throw new ArgumentException("Numeric first level must be an integer.");
            }
            else if (convention == "BG")
            {
                if (first == "B")
                    start = -1;
                else if (first == "G")
                    start = 0;
                else if (!Int32.TryParse(first, out start) || start < 1)
                    throw new ArgumentException("B/G naming starts at B, G, or a positive integer.");
            }
            else
                throw new ArgumentException("Unsupported naming convention.");
            var result = new List<VerticalFloor>();
            double z = 0;
            for (int i = 0; i < modeledLevels; i++)
            {
                bool roof = i == modeledLevels - 1;
                int n = start + i;
                string name = roof ? "R" : convention == "BG"
                    ? (n == -1 ? "B" : n == 0 ? "G" : n.ToString(CultureInfo.InvariantCulture))
                    : n.ToString(CultureInfo.InvariantCulture);
                // Preserve IDs for retained normal levels and the roof across edits.
                var old = previous == null ? null : previous.SingleOrDefault(f => f.Name == name && f.IsRoof == roof);
                if (previous != null && previous.Count == modeledLevels && previous[i].IsRoof == roof)
                    old = previous[i];
                result.Add(new VerticalFloor
                {
                    FloorId = old == null ? Guid.NewGuid().ToString("N") : old.FloorId,
                    Name = name,
                    IsRoof = roof,
                    Served = !roof && (old == null || old.Served),
                    FflZ = z
                });
                if (i < heights.Length)
                    z += heights[i];
            }
            return result;
        }

        /// <summary>True for a finite number.</summary>
        public static bool Finite(double x)
        {
            return !Double.IsNaN(x) && !Double.IsInfinity(x);
        }
    }
}
