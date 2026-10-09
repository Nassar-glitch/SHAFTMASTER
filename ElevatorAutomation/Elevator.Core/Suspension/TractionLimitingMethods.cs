namespace Elevator.Core.Suspension
{
    /// <summary>EN 81-20:2014 5.5.3 c): stalled-condition protection by rope slip (1) or by an electric safety device (2).</summary>
    public static class TractionLimitingMethods
    {
        /// <summary>Traction is limited by rope slip.</summary>
        public const string RopesSlip = "RopesSlip";
        /// <summary>Traction is limited by an electric safety device.</summary>
        public const string ElectricSafetyDevice = "ElectricSafetyDevice";
        public static readonly string[] All = { RopesSlip, ElectricSafetyDevice };

        /// <summary>The display name of a traction limiting method.</summary>
        public static string Label(string m)
        {
            return m == RopesSlip
                ? "Ropes slip (EN 81-20 5.5.3 c) 1))"
                : m == ElectricSafetyDevice ? "Electric safety device (EN 81-20 5.5.3 c) 2))" : m;
        }
    }
}
