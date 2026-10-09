namespace Elevator.Core.SafetyGear
{
    /// <summary>The P5, P6 and P7 results.</summary>
    public sealed class SafetyPitCalculationResult
    {
        public string RuleVersion = SafetyPitCalculationEngine.RuleVersion;
        public string InputFingerprint;
        public string RuleBasis = SafetyPitCalculationEngine.RuleBasis;
        public string VerificationStatus = "ADOPTED NOTE RULES — engineering/manufacturer verification remains required";
        public SafetyPitLoad P5 = new SafetyPitLoad { Id = "P5", Application = "Vertical safety-gear load per car guide rail" };
        public SafetyPitLoad P6 = new SafetyPitLoad { Id = "P6", Application = "Car-buffer reaction at pit support" };
        public SafetyPitLoad P7 = new SafetyPitLoad { Id = "P7", Application = "Counterweight-buffer reaction at pit support" };
    }
}
