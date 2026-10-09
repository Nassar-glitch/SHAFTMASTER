using System;
using System.Linq;

namespace Elevator.Core
{
    public sealed class EquipmentProfile
    {
        public string Version
        {
            get; set;
        }

        public double CarRailSideAllowanceMm
        {
            get; set;
        }

        public double CarWidthDeductionMm
        {
            get; set;
        }

        public double CarFrontAllowanceMm
        {
            get; set;
        }

        public double CarRearAllowanceMm
        {
            get; set;
        }

        public double ShaftRearAllowanceMm
        {
            get; set;
        }

        public double TzAdditionMm
        {
            get; set;
        }

        public double CounterweightRailRearAllowanceMm
        {
            get; set;
        }

        public double CounterweightRailSideAllowanceMm
        {
            get; set;
        }

        public double CounterweightRailToFrameEndMm
        {
            get; set;
        }

        public double MinimumCabinDoorReturnMm
        {
            get; set;
        }

        public double DoorJambWidthMm
        {
            get; set;
        }

        public double[] PreferredCounterweightDbgMm
        {
            get; set;
        }

        public double[] PreferredDoorWidthsMm
        {
            get; set;
        }
    }

}
