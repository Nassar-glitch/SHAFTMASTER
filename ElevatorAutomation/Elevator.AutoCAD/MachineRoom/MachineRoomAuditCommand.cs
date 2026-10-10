using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD.Common;
using Elevator.AutoCAD.Plan;
using Elevator.Core.MachineRoom;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.MachineRoom
{
    /// <summary>The AutoCAD command that audits the machine-room geometry.</summary>
    public sealed class MachineRoomAuditCommand
    {
        /// <summary>Command entry.</summary>
        [CommandMethod("ELEVATORMRAUDIT", CommandFlags.Modal)]
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var input = ProjectDataStorage.ReadProject(doc.Database, tr);
                    if (input == null)
                        throw new InvalidOperationException("Project data missing.");
                    var family = ElevatorProjectCommand.Family(input);
                    var profile = Configuration.Read<EquipmentProfile>(family.ProfileFile);
                    var plan = DrawingReader.Read(doc.Database, tr);
                    var mr = MachineRoomDrawing.Read(doc.Database, tr);
                    var design = DesignRules.Calculate(
                        input.SW,
                        input.SD,
                        profile,
                        plan.ElevatorParameters.CwDepth,
                        AutomaticDoorPlan.InputFor(doc.Database, tr));
                    design.MachineRoom = MachineRoomRules.Calculate(input, design, mr.Extensions);
                    MachineRoomUpdater.Validate(mr, design);
                    var result = MachineRoomGeometryAudit.Run(mr, design);
                    doc.Editor.WriteMessage("\nMachine-room geometric dependencies PASS: "
                        + result.CheckedReferencePoints
                        + " reference points, "
                        + result.CheckedSpanConnections
                        + " span/support connections. Engineering adequacy and unresolved interferences NOT EVALUATED.\n");
                }
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage("\nMachine-room geometry audit FAILED: " + ex.Message + "\n");
            }
        }
    }
}
