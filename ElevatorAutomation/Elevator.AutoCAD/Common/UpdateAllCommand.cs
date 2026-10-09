using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD.MachineRoom;
using Elevator.AutoCAD.Plan;
using Elevator.AutoCAD.SectionBB;

namespace Elevator.AutoCAD.Common
{
    /// <summary>The AutoCAD command that updates every view.</summary>
    public sealed class UpdateAllCommand
    {
        /// <summary>Add validated view implementations here; all receive the same calculated design.</summary>
        [CommandMethod("UPDATEALL", CommandFlags.Modal)]
        public void Execute()
        {
            var views = new System.Collections.Generic.List<IViewUpdater> { new PlanUpdater() };
            var db = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Database;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                if (MachineRoomDrawing.IsPresent(db, tr))
                    views.Add(new MachineRoomUpdater());
                if (MachineRoomSectionDrawing.IsPresent(db, tr))
                    views.Add(new MachineRoomSectionUpdater());
            }
            Commands.Run("UPDATEALL", views);
        }
    }
}
