using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Geometry;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.MachineRoom
{
    /// <summary>The reviewed chassis topology of the machine-room template.</summary>
    public sealed class ChassisTopology
    {
        public double SourceSW;
        public double SourceSD;
        public double SourceCarRow;
        public double SourceCwRow;
        public double MachineFrontEnd;
        public double MachineRearEnd;
        public double[] FrontMemberEnds;
        public double[] RearMemberEnds;
        public int ExpectedFullSpans;
        public int ExpectedFrontSpans;
        public int ExpectedRearSpans;
        public double[][] RearSupportLabelWindows;
        public int ExpectedRearLabelContours;
    }
}
