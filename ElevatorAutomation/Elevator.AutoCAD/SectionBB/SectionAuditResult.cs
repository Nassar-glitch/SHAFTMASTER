using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Geometry;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.SectionBB
{
    /// <summary>Statuses and counts reported by the B-B audit.</summary>
    public sealed class SectionAuditResult
    {
        public string Status;
        public int NativePointsChecked;
        public string RopeClearanceStatus = "NOT YET EVALUABLE";
        public string ConcreteBeamStructuralStatus = "NOT EVALUATED";
        public string HangerMechanicalStatus = "MECHANICAL REVIEW REQUIRED";
        public List<string> GeometricObservations = new List<string>();
    }
}
