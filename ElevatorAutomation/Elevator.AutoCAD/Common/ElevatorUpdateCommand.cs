using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD.Plan;

namespace Elevator.AutoCAD.Common
{
    /// <summary>The AutoCAD command that updates the plan.</summary>
    public sealed class ElevatorUpdateCommand
    {
        /// <summary>Command entry.</summary>
        [CommandMethod("ELEVATORUPDATE", CommandFlags.Modal)]
        public void Execute()
        {
            Commands.Run("ELEVATORUPDATE", new IViewUpdater[] { new PlanUpdater() });
        }
    }
}
