using System;
namespace Elevator.Core {
    public sealed class ElevatorParameters
    {
        public MachineRoomParameters MachineRoom {get;set;}
        public MachineRoomSectionParameters MachineRoomSection {get;set;}
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

}
