using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Geometry;
using Elevator.Core.Serialization;

namespace Elevator.AutoCAD.SectionAA
{
    public sealed class AAAudit
    {
        public string Status = "PASS";
        public string LayoutStatus;
        public string CWCalibrationStatus;
        public string RailEndAdequacy = "NOT_YET_EVALUABLE";
        public string BracketHardwareStatus = "NOT_IMPLEMENTED - STATION DATA ONLY";
        public string CodeDiameterRatioStatus;
        public string WrapAngleStatus;
        public string SlabClearanceStatus;
        public string HangerStatus = "MECHANICAL REVIEW REQUIRED";
        public int EntityCount;
        public int FloorCount;
        public int DoorCount;
        public int Dimensions;
        public int BracketStations;
        public double Travel;
        public double Pit;
        public double OH;
        public double CarEnvelope;
        public double CWEnvelope;
        public double MaxBracketSpacing;
    }
}
