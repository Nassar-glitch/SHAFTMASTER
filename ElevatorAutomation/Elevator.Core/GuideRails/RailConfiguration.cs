using System;

namespace Elevator.Core.GuideRails
{
    /// <summary>Guide-rail inputs of the project.</summary>
    public sealed class RailConfiguration
    {
        /// <summary>The profiles of the plan and section labels when no profile is chosen.</summary>
        public const string DefaultCarProfile = "T70/65/9";
        public const string DefaultCwProfile = "T50/50/5";

        public string RequestedRailType;
        public double? BracketMaximumSpacingMm;
        public string BracketSource = "NOT PROVIDED";

        // Guide-rail check inputs (DESIGN-FLOW "Guide rails"): preset fields that follow the recommendation until edited.
        public bool? MachineOnRails;
        public string CarProfile;
        public double? CarSpacingMm;
        public string CwProfile;
        public double? CwSpacingMm;

        // Advanced guide-rail values: the library value until edited (each follows the preset and locks on edit).
        public int? MachineRailCount;
        public double? ClipPushThroughN;
        public double? CwShoeSpacingMm;
        public double? StructureDeflectionMm;
        public string CarShoeType;
        public double? CarShoeLiningMm;
        public string CwShoeType;
        public double? CwShoeLiningMm;
        public string A5Band;

        /// <summary>The car rail profile shown on the drawings and the sheet: the chosen one, else the default.</summary>
        public string CarProfileShown()
        {
            return String.IsNullOrWhiteSpace(CarProfile) ? DefaultCarProfile : CarProfile;
        }

        /// <summary>The counterweight rail profile shown on the drawings and the sheet: the chosen one, else the default.</summary>
        public string CwProfileShown()
        {
            return String.IsNullOrWhiteSpace(CwProfile) ? DefaultCwProfile : CwProfile;
        }
    }
}
