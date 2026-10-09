using System.Collections.Generic;

namespace Elevator.Core.Geometry
{
    /// <summary>Inputs of the A-A section: floors, pit, overhead and bracket spacing.</summary>
    public sealed class SectionAAProject
    {
        public int SchemaVersion = 1;
        public List<VerticalFloor> Floors = new List<VerticalFloor>();
        public string DisplayFloorId;
        public double Pit = 1000;
        public double OH = 4200;
        public double BracketMaximum = 1500;
        public double ReferenceCarZ = 0;
        public double ReferenceCWBottomZ = 12347.128322572622;
        public string CWCalibrationStatus = "PROVISIONAL_SOURCE_REFERENCE_POSE";
        public string BracketSource = "PROJECT INPUT PENDING FUTURE CALCULATION SHEET";
    }
}
