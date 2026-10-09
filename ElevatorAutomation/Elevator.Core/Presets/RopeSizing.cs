namespace Elevator.Core.Presets
{
    public sealed class RopeSizing
    {
        public string Status; // NotApplicable, NotEvaluable, Selected, NoPass
        public int? StartCount;
        public int? RopeCount;
        public double? StartDiameterMm;
        public double? RopeDiameterMm;
        public double? StartSheaveMm;
        public double? SheaveMm;
        public double? StartUndercutAngleDeg;
        public double? UndercutAngleDeg;
        public double? StartBalanceFactor;
        public double? BalanceFactor;
        public string LimitingCheck;
        public int Trials;
        public long Milliseconds;

        public RopeSizing Copy()
        {
            return (RopeSizing)MemberwiseClone();
        }
    }
}
