using Autodesk.AutoCAD.Runtime;

namespace Elevator.AutoCAD
{
    public sealed class ElevatorUpdateCommand
    {
        [CommandMethod("ELEVATORUPDATE", CommandFlags.Modal)]
        public void Execute()
        {
            Commands.Run("ELEVATORUPDATE", new IViewUpdater[] { new PlanUpdater() });
        }
    }
}
