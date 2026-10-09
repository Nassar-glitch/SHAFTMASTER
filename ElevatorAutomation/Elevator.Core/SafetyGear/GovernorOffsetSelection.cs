namespace Elevator.Core.SafetyGear
{
    /// <summary>The governor offset selected from TZ.</summary>
    public sealed class GovernorOffsetSelection
    {
        public double? OffsetFromCarGuideAxisMm;
        public string SelectionStatus;
        public string RuleSource;
    }
}
