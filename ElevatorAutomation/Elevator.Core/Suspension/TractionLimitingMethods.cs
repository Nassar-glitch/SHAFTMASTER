namespace Elevator.Core.Suspension
{
    // EN 81-20:2014 5.5.3 c): stalled-condition protection by rope slip (1) or by an electric safety device (2).
    public static class TractionLimitingMethods
    {
        public const string RopesSlip = "RopesSlip";
        public const string ElectricSafetyDevice = "ElectricSafetyDevice";
        public static readonly string[] All = { RopesSlip, ElectricSafetyDevice };

        public static string Label(string m)
        {
            return m == RopesSlip
                ? "Ropes slip (EN 81-20 5.5.3 c) 1))"
                : m == ElectricSafetyDevice ? "Electric safety device (EN 81-20 5.5.3 c) 2))" : m;
        }
    }
}
