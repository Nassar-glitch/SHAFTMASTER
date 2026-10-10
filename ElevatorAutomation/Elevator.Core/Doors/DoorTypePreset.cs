using System.Collections.Generic;
using System.Linq;

namespace Elevator.Core.Doors
{
    /// <summary>The presets of one automatic door type: front-zone split, landing clearance, panels and the size rows.</summary>
    public sealed class DoorTypePreset
    {
        /// <summary>Car front-wall inside face to the outer face of the outer car door panel / box.</summary>
        public double? CarZoneMm;

        /// <summary>Outer car door face to the inner landing door face (EN 81-20 sill gap).</summary>
        public double? SillGapMm;

        /// <summary>Inner landing door face to the shaft front wall.</summary>
        public double? LandingZoneMm;

        /// <summary>Smallest distance from the landing door frame to a shaft side wall.</summary>
        public double? LandingWallClearanceMm;

        /// <summary>Panel thickness (kept for the drawing stage).</summary>
        public double? PanelThicknessMm;

        /// <summary>2S: overlap of the two closed panels. 2C: gap between the two panels.</summary>
        public double? PanelOverlapMm;

        /// <summary>2S: how far the closed panels extend beyond the opening on the stacking side.</summary>
        public double? PanelEdgeOvertravelMm;

        public List<DoorSizePreset> Sizes;

        /// <summary>The row of a clear-opening size, or null.</summary>
        public DoorSizePreset Row(double openingMm)
        {
            return Sizes == null ? null : Sizes.FirstOrDefault(r => r != null && System.Math.Abs(r.OpeningMm - openingMm) < 1e-6);
        }
    }
}
