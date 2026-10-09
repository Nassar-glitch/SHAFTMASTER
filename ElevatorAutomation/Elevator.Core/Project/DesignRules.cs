using System;
using Elevator.Core.Doors;
using Elevator.Core.GuideRails;
using Elevator.Core.Presets;

namespace Elevator.Core.Project
{
    public static class DesignRules
    {
        public static ElevatorParameters Calculate(double sw, double sd, EquipmentProfile p, double existingCwDepth)
        {
            if (Double.IsNaN(sw) || Double.IsInfinity(sw) || Double.IsNaN(sd) || Double.IsInfinity(sd) || sw <= 0 || sd <= 0)
                throw new ArgumentException("SW and SD must be finite positive millimetre values.");
            if (p == null || p.PreferredCounterweightDbgMm == null || p.PreferredDoorWidthsMm == null)
                throw new ArgumentException("The equipment profile is incomplete.");
            ElevatorParameters n = new ElevatorParameters();
            n.SW = sw;
            n.SD = sd;
            n.CarDbg = sw - 2 * p.CarRailSideAllowanceMm;
            n.CarWidth = n.CarDbg - p.CarWidthDeductionMm;
            n.CarDepth = sd - p.CarFrontAllowanceMm - p.CarRearAllowanceMm - p.ShaftRearAllowanceMm;
            if (n.CarWidth <= 0 || n.CarDepth <= 0)
                throw new ArgumentException("The shaft is too small for the configured cabin allowances.");
            n.CarFront = p.CarFrontAllowanceMm;
            n.CarRear = n.CarFront + n.CarDepth;
            n.TZ = n.CarDepth / 2 + p.TzAdditionMm;
            n.CwRow = sd - p.CounterweightRailRearAllowanceMm;
            n.CarRow = n.CwRow - n.TZ;
            n.CwDbg = RailRules.SelectCounterweightDbg(sw, p);
            if (n.CwDbg <= 0)
                throw new ArgumentException("No preferred CW DBG fits the shaft with the configured side allowances.");
            n.CwWidth = n.CwDbg - 2 * p.CounterweightRailToFrameEndMm;
            n.CwDepth = existingCwDepth;
            n.CwFront = n.CwRow - n.CwDepth / 2;
            n.CwRear = n.CwRow + n.CwDepth / 2;
            n.JambWidth = p.DoorJambWidthMm;
            n.DoorWidth = DoorRules.SelectWidth(sw, n.CarWidth, n.JambWidth, p);
            if (n.DoorWidth <= 0)
                throw new ArgumentException(
                    "No preferred door fits. The smallest door needs at least 1150 mm shaft width with this profile.");
            if (n.CwDepth <= 0
                || n.CwWidth <= 0
                || n.CwRear > sd + 1e-6
                || n.CwFront < n.CarRear
                || n.CarRow <= n.CarFront
                || n.CarRow >= n.CarRear)
                throw new ArgumentException("The calculated component positions do not fit this shaft depth.");
            return n;
        }
    }
}
