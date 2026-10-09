using System;
using System.Linq;
namespace Elevator.Core
{
    // Every target axis is read from the single shared design; no SW/SD/TZ sizing formulas here.
    public sealed class MachineRoomGeometry
    {
        public readonly ElevatorParameters Design;
        public readonly double OldSW,OldSD,OldCarRow,OldCwRow,OldPitch,Pitch;
        public readonly MachineRoomExtensions Extensions;
        public readonly double OldGovernorOffset,GovernorOffset;
        public MachineRoomGeometry(ElevatorParameters design,double sw,double sd,double car,double cw,double pitch,MachineRoomExtensions extensions,double oldGovernorOffset)
        {
            Design=design;OldSW=sw;OldSD=sd;OldCarRow=car;OldCwRow=cw;OldPitch=pitch;Extensions=extensions;
            Pitch=design.MachineRoom.Deflector.PitchDiameterMm??pitch;
            OldGovernorOffset=oldGovernorOffset;GovernorOffset=design.MachineRoom.Governor.OffsetFromCarGuideAxisMm??oldGovernorOffset;
        }
        public double MapX(double value)
        {
            double[] old={0,OldSW/2,OldSW,-Extensions.Left,OldSW+Extensions.Right};
            double[] next={0,Design.Center,Design.SW,-Extensions.Left,Design.SW+Extensions.Right};
            int i=Enumerable.Range(0,old.Length).OrderBy(k=>Math.Abs(value-old[k])).First();return value+next[i]-old[i];
        }
        public double MapY(double value)
        {
            double[] old={0,OldCarRow,OldCwRow,OldSD,-Extensions.Front,OldSD+Extensions.Rear};
            double[] next={0,Design.CarRow,Design.CwRow,Design.SD,-Extensions.Front,Design.SD+Extensions.Rear};
            int i=Enumerable.Range(0,old.Length).OrderBy(k=>Math.Abs(value-old[k])).First();return value+next[i]-old[i];
        }
        private double RoomX(double value)
        {
            double[] old={-Extensions.Left,OldSW/2,OldSW+Extensions.Right};
            double[] next={-Extensions.Left,Design.Center,Design.SW+Extensions.Right};
            int i=Enumerable.Range(0,old.Length).OrderBy(k=>Math.Abs(value-old[k])).First();return value+next[i]-old[i];
        }
        public LocalPoint Move(LocalPoint p,string role)
        {
            double dx=Design.Center-OldSW/2,dy=Design.CarRow-OldCarRow;
            switch(role)
            {
                case "room":return new LocalPoint(RoomX(p.X),MapY(p.Y),p.Z);
                case "dimension":case "mainAxis":return new LocalPoint(MapX(p.X),MapY(p.Y),p.Z);
                case "shaftLeft":return new LocalPoint(p.X,MapY(p.Y),p.Z);
                case "shaftRight":return new LocalPoint(p.X+Design.SW-OldSW,MapY(p.Y),p.Z);
                case "frontBeam":return new LocalPoint(MapX(p.X),p.Y,p.Z);
                case "carAxis":return new LocalPoint(MapX(p.X),p.Y+dy,p.Z);
                case "cwAxis":return new LocalPoint(MapX(p.X),p.Y+Design.CwRow-OldCwRow,p.Z);
                case "machine":break;
                case "governor":dx=0;dy+=GovernorOffset-OldGovernorOffset;break;
                case "cw":case "deflectorBracket":dy=Design.CwRow-OldCwRow;break;
                case "deflectorProfile":return new LocalPoint(p.X+dx,Design.CwRow+(p.Y-OldCwRow)*Pitch/OldPitch,p.Z);
                case "deflectorCenter":case "deflectorLabel":dy=Design.CwRow-OldCwRow-(Pitch-OldPitch)/2;break;
                case "ventLeft":dx=0;break;
                case "ventRight":dx=Design.SW-OldSW;break;
                case "panel":dx=0;dy=0;break;
                case "front":dy=0;break;
                case "rear":dy=Design.SD-OldSD;break;
                default:throw new ArgumentException("Unsupported machine-room role: "+role);
            }
            return new LocalPoint(p.X+dx,p.Y+dy,p.Z);
        }
        public bool Flexible(string role){return new[]{"room","dimension","mainAxis","shaftLeft","shaftRight","frontBeam","carAxis","cwAxis","deflectorProfile"}.Contains(role);}
    }
}
