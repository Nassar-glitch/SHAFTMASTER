using Autodesk.AutoCAD.Runtime;
namespace Elevator.AutoCAD {
 public sealed class UpdateAllCommand {
  // Add validated view implementations here; all receive the same calculated design.
  [CommandMethod("UPDATEALL",CommandFlags.Modal)]
  public void Execute() {
   var views=new System.Collections.Generic.List<IViewUpdater>{new PlanUpdater()};
   var db=Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Database;
   using(var tr=db.TransactionManager.StartTransaction()) {
    if(MachineRoomDrawing.IsPresent(db,tr))views.Add(new MachineRoomUpdater());
    if(MachineRoomSectionDrawing.IsPresent(db,tr))views.Add(new MachineRoomSectionUpdater());
   }
   Commands.Run("UPDATEALL",views);
  }
 }
}
