namespace Elevator.Core.MachineRoom
{
    /// <summary>Results of the B-B machine-room section: supports, beam, deflector, reeving arrangement and levels.</summary>
    public sealed class MachineRoomSectionParameters
    {
        public MachineBeamSelection Beam;
        public VerticalArrangementResult Arrangement;
        public SectionReference Reference;
        public double FrontSupportX;
        public double RearSupportX;
        public double SteelBottomZ;
        public double SteelTopZ;
        public double TractionX;
        public double TractionZ;
        public double DeflectorX;
        public double DeflectorZ;
        public double DeflectorPitchMm;
        public double CarDropX;
        public double CwDropX;
        public double CarOpeningWidth;
        public double CwOpeningWidth;
        public double ConcreteHeight;
        public bool DeflectorVisible;
        public bool HasCalculatedPath;
        public string LayoutStatus;
        public string ReevingGeometryStatus;
        public string RopeClearanceStatus = "NOT YET EVALUABLE";
        public string ConcreteBeamStructuralStatus = "NOT EVALUATED";
        public string OpeningStatus = "OPENING SIZE ENGINEERING REVIEW REQUIRED";

        public string MachineBeamStructuralStatus
        {
            get
            {
                return Beam == null ? "NOT EVALUATED" : Beam.StructuralStatus;
            }
        }
    }
}
