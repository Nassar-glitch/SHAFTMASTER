using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD.Plan;

namespace Elevator.AutoCAD.Common
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
