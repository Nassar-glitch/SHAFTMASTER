using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Geometry;
using Elevator.Core.MachineRoom;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Serialization;

namespace Elevator.AutoCAD.Vertical
{
    public sealed class VerticalNativeAudit
    {
        public string Status = "PASS";
        public string LayoutStatus;
        public string CWAbsolutePositionStatus;
        public string CodeDiameterRatioStatus;
        public string WrapAngleStatus;
        public string DeflectorSlabClearanceStatus;
        public string HangerMechanicalStatus = "MECHANICAL REVIEW REQUIRED";
        public double? CalculatedWrapAngleDeg;
        public double? DeflectorToSlabClearanceMm;
        public int EntitiesChecked;
        public int MachineRoomEntitiesChecked;
        public int DimensionCount;
        public int NormalFloors;
        public int ServedStops;
        public int BracketCount;
        public double MaxActualBracketSpacing;
        public double TravelMm;
        public double RoofZ;
        public double CarFloorZ;
        public double CwBottomZ;
        public List<string> ReviewItems;
        public string RailEndAdequacyStatus = "NOT_YET_EVALUABLE";
        public string BracketFixingStatus = "NOT_YET_EVALUABLE";
        public string RailJointDetailStatus = "NOT_YET_IMPLEMENTED";
    }
}
