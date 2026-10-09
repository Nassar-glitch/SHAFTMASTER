using System;
using System.Linq;

namespace ElevatorPlanGenerator
{
    public sealed class EquipmentProfile
    {
        public string Version { get; set; }
        public double CarRailSideAllowanceMm { get; set; }
        public double CarWidthDeductionMm { get; set; }
        public double CarFrontAllowanceMm { get; set; }
        public double CarRearAllowanceMm { get; set; }
        public double ShaftRearAllowanceMm { get; set; }
        public double TzAdditionMm { get; set; }
        public double CounterweightRailRearAllowanceMm { get; set; }
        public double CounterweightRailSideAllowanceMm { get; set; }
        public double CounterweightRailToFrameEndMm { get; set; }
        public double MinimumCabinDoorReturnMm { get; set; }
        public double DoorJambWidthMm { get; set; }
        public double[] PreferredCounterweightDbgMm { get; set; }
        public double[] PreferredDoorWidthsMm { get; set; }
    }

    public sealed class Layout
    {
        public double SW, SD, CarDbg, CarWidth, CarDepth, CwDbg, CwWidth, CwDepth, DoorWidth, TZ;
        public double CarFront, CarRear, CarRow, CwRow, CwFront, CwRear, JambWidth;
        public double Center { get { return SW / 2; } }
        public double X(string anchor)
        {
            switch (anchor) {
                case "left": return 0; case "right": return SW; case "center": return Center;
                case "carLeft": return Center-CarWidth/2; case "carRight": return Center+CarWidth/2;
                case "railLeft": return Center-CarDbg/2; case "railRight": return Center+CarDbg/2;
                case "cwRailLeft": return Center-CwDbg/2; case "cwRailRight": return Center+CwDbg/2;
                case "cwLeft": return Center-CwWidth/2; case "cwRight": return Center+CwWidth/2;
                case "doorLeft": return Center-DoorWidth/2; case "doorRight": return Center+DoorWidth/2;
                case "jambLeft": return Center-DoorWidth/2-JambWidth;
                case "jambRight": return Center+DoorWidth/2+JambWidth;
                default: throw new ArgumentException("Unknown X anchor: "+anchor);
            }
        }
        public double Y(string anchor)
        {
            switch(anchor) {
                case "front":return 0; case "rear":return SD;
                case "carFront":return CarFront; case "carRear":return CarRear;
                case "carRow":return CarRow; case "cwRow":return CwRow;
                case "cwFront":return CwFront; case "cwRear":return CwRear;
                default:throw new ArgumentException("Unknown Y anchor: "+anchor);
            }
        }
    }

    public static class DesignRules
    {
        public static Layout Calculate(double sw, double sd, EquipmentProfile p, double existingCwDepth)
        {
            if(Double.IsNaN(sw)||Double.IsInfinity(sw)||Double.IsNaN(sd)||Double.IsInfinity(sd)||sw<=0||sd<=0)
                throw new ArgumentException("SW and SD must be finite positive millimetre values.");
            if(p==null||p.PreferredCounterweightDbgMm==null||p.PreferredDoorWidthsMm==null)
                throw new ArgumentException("The equipment profile is incomplete.");
            Layout n=new Layout();n.SW=sw;n.SD=sd;
            n.CarDbg=sw-2*p.CarRailSideAllowanceMm;
            n.CarWidth=n.CarDbg-p.CarWidthDeductionMm;
            n.CarDepth=sd-p.CarFrontAllowanceMm-p.CarRearAllowanceMm-p.ShaftRearAllowanceMm;
            if(n.CarWidth<=0||n.CarDepth<=0) throw new ArgumentException("The shaft is too small for the configured cabin allowances.");
            n.CarFront=p.CarFrontAllowanceMm;n.CarRear=n.CarFront+n.CarDepth;
            n.TZ=n.CarDepth/2+p.TzAdditionMm;
            n.CwRow=sd-p.CounterweightRailRearAllowanceMm;n.CarRow=n.CwRow-n.TZ;
            n.CwDbg=p.PreferredCounterweightDbgMm.OrderByDescending(v=>v).FirstOrDefault(v=>v>0&&v<=sw-2*p.CounterweightRailSideAllowanceMm+1e-7);
            if(n.CwDbg<=0) throw new ArgumentException("No preferred CW DBG fits the shaft with the configured side allowances.");
            n.CwWidth=n.CwDbg-2*p.CounterweightRailToFrameEndMm;
            n.CwDepth=existingCwDepth;n.CwFront=n.CwRow-n.CwDepth/2;n.CwRear=n.CwRow+n.CwDepth/2;
            n.JambWidth=p.DoorJambWidthMm;
            n.DoorWidth=p.PreferredDoorWidthsMm.OrderByDescending(v=>v).FirstOrDefault(v=>v>0&&v<=n.CarWidth-2*p.MinimumCabinDoorReturnMm+1e-7&&v+2*n.JambWidth<=sw+1e-7);
            if(n.DoorWidth<=0) throw new ArgumentException("No preferred door fits. The smallest door needs at least 1150 mm shaft width with this profile.");
            if(n.CwDepth<=0||n.CwWidth<=0||n.CwRear>sd+1e-6||n.CwFront<n.CarRear||n.CarRow<=n.CarFront||n.CarRow>=n.CarRear)
                throw new ArgumentException("The calculated component positions do not fit this shaft depth.");
            return n;
        }
    }
}
