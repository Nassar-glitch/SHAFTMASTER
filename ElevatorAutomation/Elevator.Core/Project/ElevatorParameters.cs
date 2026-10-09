using System;
using Elevator.Core.MachineRoom;

namespace Elevator.Core.Project
{
    /// <summary>Calculated plan dimensions of the shaft, car, counterweight, rails and door.</summary>
    public sealed class ElevatorParameters
    {
        public MachineRoomParameters MachineRoom { get; set; }
        public MachineRoomSectionParameters MachineRoomSection { get; set; }

        public double SW;
        public double SD;
        public double CarDbg;
        public double CarWidth;
        public double CarDepth;
        public double CwDbg;
        public double CwWidth;
        public double CwDepth;
        public double DoorWidth;
        public double TZ;
        public double CarFront;
        public double CarRear;
        public double CarRow;
        public double CwRow;
        public double CwFront;
        public double CwRear;
        public double JambWidth;

        public double Center
        {
            get
            {
                return SW / 2;
            }
        }

        /// <summary>The X coordinate of a plan anchor.</summary>
        public double X(string anchor)
        {
            switch (anchor)
            {
                case "left":
                    return 0;
                case "right":
                    return SW;
                case "center":
                    return Center;
                case "carLeft":
                    return Center - CarWidth / 2;
                case "carRight":
                    return Center + CarWidth / 2;
                case "railLeft":
                    return Center - CarDbg / 2;
                case "railRight":
                    return Center + CarDbg / 2;
                case "cwRailLeft":
                    return Center - CwDbg / 2;
                case "cwRailRight":
                    return Center + CwDbg / 2;
                case "cwLeft":
                    return Center - CwWidth / 2;
                case "cwRight":
                    return Center + CwWidth / 2;
                case "doorLeft":
                    return Center - DoorWidth / 2;
                case "doorRight":
                    return Center + DoorWidth / 2;
                case "jambLeft":
                    return Center - DoorWidth / 2 - JambWidth;
                case "jambRight":
                    return Center + DoorWidth / 2 + JambWidth;
                default:
                    throw new ArgumentException("Unknown X anchor: " + anchor);
            }
        }

        /// <summary>The Y coordinate of a plan anchor.</summary>
        public double Y(string anchor)
        {
            switch (anchor)
            {
                case "front":
                    return 0;
                case "rear":
                    return SD;
                case "carFront":
                    return CarFront;
                case "carRear":
                    return CarRear;
                case "carRow":
                    return CarRow;
                case "cwRow":
                    return CwRow;
                case "cwFront":
                    return CwFront;
                case "cwRear":
                    return CwRear;
                default:
                    throw new ArgumentException("Unknown Y anchor: " + anchor);
            }
        }
    }
}
