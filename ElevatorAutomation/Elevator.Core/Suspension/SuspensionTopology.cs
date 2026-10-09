namespace Elevator.Core.Suspension
{
    public static class SuspensionTopology
    {
        public static double CounterweightBottom(double pitZ, double calibration, double highestServedZ, double carFloorZ)
        {
            return pitZ + calibration + highestServedZ - carFloorZ;
        }
    }
}
