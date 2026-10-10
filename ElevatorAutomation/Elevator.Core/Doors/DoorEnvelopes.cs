namespace Elevator.Core.Doors
{
    /// <summary>Plan envelopes used by the door clash checks (mm), measured from the reference drawings.</summary>
    public sealed class DoorEnvelopes
    {
        /// <summary>Guide rail and bracket: depth from the rail blade face outwards.</summary>
        public double? RailDepthMm;

        /// <summary>Guide rail and bracket: width along the shaft depth.</summary>
        public double? RailWidthMm;

        /// <summary>Speed governor: gap from the shaft wall to the governor.</summary>
        public double? GovernorGapToWallMm;

        /// <summary>Speed governor: width across the shaft.</summary>
        public double? GovernorWidthMm;

        /// <summary>Speed governor: start, from the car guide axis towards the rear.</summary>
        public double? GovernorFromGuideAxisMm;

        /// <summary>Speed governor: length along the shaft depth.</summary>
        public double? GovernorLengthMm;
    }
}
