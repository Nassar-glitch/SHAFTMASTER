using System;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD.Common;
using Elevator.AutoCAD.Plan;

namespace Elevator.AutoCAD.MachineRoom
{
    /// <summary>The AutoCAD command that adds the machine room.</summary>
    public sealed class MachineRoomCommand
    {
        /// <summary>Command entry.</summary>
        [CommandMethod("ELEVATORMACHINEROOM", CommandFlags.Modal)]
        public void Add()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                ShaftFrame frame;
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    if (ProjectDataStorage.ReadProject(doc.Database, tr) == null)
                        throw new InvalidOperationException("Run ELEVATORPROJECT first.");
                    frame = DrawingReader.Read(doc.Database, tr).Frame;
                    if (MachineRoomDrawing.IsPresent(doc.Database, tr))
                        throw new InvalidOperationException("Machine-room view already exists; run UPDATEALL.");
                }
                var point = doc.Editor.GetPoint("\nPlace the machine-room shaft front-left datum in an empty drawing area: ");
                if (point.Status != PromptStatus.OK)
                    return;
                string source = Path.Combine(Configuration.Root, "Templates", "MachineRoom_Master.dwg");
                MachineRoomDrawing.Import(
                    doc.Database,
                    source,
                    point.Value.TransformBy(doc.Editor.CurrentUserCoordinateSystem),
                    frame.XAxis);
                doc.Editor.Regen();
                doc.Editor.WriteMessage(
                    "\nMachineRoom reference imported with semantic bindings. Run UPDATEALL. Engineering review remains required.\n");
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage("\nMachineRoom import stopped: " + ex.Message + "\n");
            }
        }
    }
}
