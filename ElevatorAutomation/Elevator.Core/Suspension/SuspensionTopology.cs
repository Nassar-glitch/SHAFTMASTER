namespace Elevator.Core.Suspension
{
    /// <summary>Suspension relations of the vertical section (1:1).</summary>
    public static class SuspensionTopology
    {
        /// <summary>
        /// The counterweight bottom level from the pit level, the calibration, the highest served level and the car floor level.
        /// </summary>
        public static double CounterweightBottom(double pitZ, double calibration, double highestServedZ, double carFloorZ)
        {
            return pitZ + calibration + highestServedZ - carFloorZ;
        }
    }
}
