namespace Elevator.Core.GuideRails
{
    /// <summary>Guide-rail inputs of the project.</summary>
    public sealed class RailConfiguration
    {
        public string RequestedRailType;
        public double? BracketMaximumSpacingMm;
        public string BracketSource = "NOT PROVIDED";
    }
}
