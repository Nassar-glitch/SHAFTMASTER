using System.Collections.Generic;

namespace Elevator.Core.Doors
{
    /// <summary>One reason an automatic door does not fit, with the dimension that would fix it.</summary>
    public sealed class DoorProblem
    {
        /// <summary>opening-size, car-width, landing-width, sill-gap, car-to-wall or clash.</summary>
        public string Code;

        /// <summary>Short reason, for example "needs car width 1350 mm (now 1320 mm)".</summary>
        public string Message;

        /// <summary>The car width that would fix it, or null.</summary>
        public double? NeededCarWidthMm;

        /// <summary>The shaft width that would fix it, or null.</summary>
        public double? NeededShaftWidthMm;
    }

    /// <summary>
    /// The resolved plan layout of the car and landing doors: every effective clearance, the opening position and the checks. X is
    /// measured from the shaft left wall, Y (zones) from the shaft front wall.
    /// </summary>
    public sealed class DoorLayout
    {
        public string Type = DoorTypes.Manual;

        /// <summary>The stacking side, "Left" or "Right" (2S); the governor-opposite side for 2C is not used.</summary>
        public string StackingSide;

        public double OpeningMm;

        /// <summary>True when the size was selected by the rules (no size locked).</summary>
        public bool AutoSelected;

        /// <summary>True when no check fails.</summary>
        public bool Fits = true;

        public double CarZoneMm;
        public double SillGapMm;
        public double LandingZoneMm;

        /// <summary>Car zone + sill gap + landing zone: the distance from the shaft front wall to the car interior.</summary>
        public double FrontZoneMm;

        /// <summary>Minimum car returns (preset or locked).</summary>
        public double FixedReturnMm;
        public double StackReturnMm;

        /// <summary>Returns the layout gives the car: car width, opening and offset.</summary>
        public double ActualFixedReturnMm;
        public double ActualStackReturnMm;

        /// <summary>Opening centre shifted from the car centre towards the fixed side.</summary>
        public double OpeningOffsetMm;

        /// <summary>Shift of the opening centre from the shaft centre in +X (zero for centre opening).</summary>
        public double CentreShiftMm;

        public double OpeningLeftX;
        public double OpeningRightX;
        public double LandingLeftX;
        public double LandingRightX;
        public double LandingFixedJambMm;
        public double LandingStackPocketMm;
        public double LandingWallClearanceMm;
        public double PanelWidthMm;
        public double PanelThicknessMm;
        public double PanelOverlapMm;

        /// <summary>Shaft front wall to the outer face of the car door (EN 81-20 5.2.5.2.1).</summary>
        public double CarToWallMm;

        /// <summary>The smallest car width and shaft width at which this size fits (null: not limited by it).</summary>
        public double? NeededCarWidthMm;
        public double? NeededShaftWidthMm;

        public List<DoorProblem> Problems = new List<DoorProblem>();
    }
}
