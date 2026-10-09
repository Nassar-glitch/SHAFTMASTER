using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.MachineRoom
{
    public sealed class MachineRoomAuditResult
    {
        public string Status = "PASS";
        public int CheckedEntities;
        public int CheckedReferencePoints;
        public int CheckedSpanConnections;

        public string EngineeringAdequacy =
            "NOT EVALUATED - geometric continuity is not structural, bearing-capacity, rope-path or clearance approval.";

        public string[] Unevaluated = { "Manufacturer machine/deflector mounting relationship and minimum working gaps",
            "Detailed suspension rope paths and cutout envelopes",
            "Unspecified minimum bearing/structural adequacy",
            "General interference of unknown equipment and annotation text collision" };
    }
}
