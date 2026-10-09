using System.Collections.Generic;
using Elevator.Core.Calculations;
using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    public sealed class ChassisCalculationGeometry
    {
        public string Recipe = "Advanced";

        public ChassisGeometryProvenance Provenance { get; private set; }

        internal void BindSource(GeometrySourceSnapshot source)
        {
            Provenance = new ChassisGeometryProvenance(source, this);
        }

        public string Units = "mm";
        public string Axes = "X right; Y entrance to rear; Z above machine-room slab";
        public List<BearingSeat> Bearings = new List<BearingSeat>();
        public List<ChassisMember> LongitudinalMembers = new List<ChassisMember>();
        public LocalPoint TractionCentre;
        public LocalPoint DeflectorCentre;
        public LocalPoint[] NativeMachineMountingContours; // Geometric region only, not feet/CG.
        public double ShaftWidthMm;
        public double ShaftDepthMm;
        public double BearingPlaneZMm;
        public double HangerAttachmentZMm;
        public MachineRoomSectionParameters Section;
        public List<CalculationIssue> Issues = new List<CalculationIssue>();
    }
}
