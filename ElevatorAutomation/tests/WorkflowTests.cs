using System;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD;
using Elevator.Core;
public sealed class WorkflowTests
{
 [CommandMethod("ELEVATORWORKFLOWCHECK")]
 public void Check()
 {
  string status="PASS",error=null;
  try {
   if(Commands.LastError!=null||Commands.LastOutput==null)throw new System.Exception("No saved update: "+Commands.LastError);
   using(var db=new Database(false,true)) {
    db.ReadDwgFile(Commands.LastOutput,FileOpenMode.OpenForReadAndAllShare,true,null);db.CloseInput(true);
    using(var tr=db.TransactionManager.StartTransaction()) {
     var p=ProjectDataStorage.ReadProject(db,tr);
     if(p==null||p.ElevatorType!=ElevatorType.ExistingPlan||p.SW!=1800||p.SD!=2000)throw new System.Exception("Interactive project edits not persisted.");
     DrawingUpdater.Validate(DrawingReader.Read(db,tr),Commands.LastElevatorParameters,Commands.LastDimensions);
    }
   }
  } catch(System.Exception ex) {status="FAIL";error=ex.ToString();}
  Configuration.WriteJson(Path.Combine(Configuration.Root,"tests","workflow-results.json"),new {status=status,error=error,output=Commands.LastOutput});
 }
}
