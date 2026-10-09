using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Geometry;
using Elevator.Core.Shaft;

namespace Elevator.Core.GuideRails
{
    public static class BracketLayoutSolver
    {
        public static BracketLayout Solve(
            double bottom,
            double top,
            double maximum,
            IEnumerable<VerticalFloor> floors,
            double slabThickness,
            double openingHeight)
        {
            if (!FloorStackGenerator.Finite(maximum) || maximum <= 0)
                throw new ArgumentException("Bracket maximum spacing must be explicitly selected and positive.");
            if (top <= bottom)
                throw new ArgumentException("Invalid rail run.");
            if ((top - bottom) / maximum > 100000)
                throw new ArgumentException("Requested bracket density exceeds the V1 drawing resource limit (100000 intervals).");
            var r = new BracketLayout();
            var anchors = new SortedSet<double> { bottom, top };
            foreach (var f in floors.Where(f => !f.IsRoof))
            {
                // Known floor/slab boundaries are anchors; no unproven structural capacity implied.
                if (f.FflZ - slabThickness > bottom && f.FflZ - slabThickness < top)
                    anchors.Add(f.FflZ - slabThickness);
                if (f.FflZ > bottom && f.FflZ < top)
                    anchors.Add(f.FflZ);
                if (f.Served)
                    r.Observations.Add(f.FloorId
                        + ": landing opening "
                        + f.FflZ.ToString("R", CultureInfo.InvariantCulture)
                        + ".."
                        + (f.FflZ + openingHeight).ToString("R", CultureInfo.InvariantCulture)
                        + "; brackets are on Plan side-guide supports, not the entrance wall. Fixing capacity/exclusions unverified.");
            }
            double[] a = anchors.ToArray();
            r.Elevations.Add(a[0]);
            for (int i = 1; i < a.Length; i++)
            {
                int n = (int)Math.Ceiling((a[i] - a[i - 1]) / maximum);
                for (int j = 1; j <= n; j++)
                    r.Elevations.Add(a[i - 1] + (a[i] - a[i - 1]) * j / n);
            }
            for (int i = 1; i < r.Elevations.Count; i++)
            {
                double gap = r.Elevations[i] - r.Elevations[i - 1];
                r.ConsecutiveSpacings.Add(gap);
                if (gap > maximum + 1e-7)
                    r.SpacingStatus = "FAIL";
            }
            r.Observations.Add("Endpoint markers are provisional bracket positions; rail-end/anchor/joint suitability NOT_YET_EVALUABLE.");
            return r;
        }
    }
}
