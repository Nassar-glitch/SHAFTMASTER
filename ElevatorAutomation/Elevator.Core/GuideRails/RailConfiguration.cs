namespace Elevator.Core.GuideRails
{
    /// <summary>Guide-rail inputs of the project.</summary>
    public sealed class RailConfiguration
    {
        public string RequestedRailType;
        public double? BracketMaximumSpacingMm;
        public string BracketSource = "NOT PROVIDED";

        // Guide-rail check inputs (DESIGN-FLOW "Guide rails"): preset fields that follow the recommendation until edited.
        public bool? MachineOnRails;
        public string CarProfile;
        public double? CarSpacingMm;
        public string CwProfile;
        public double? CwSpacingMm;
    }
}
