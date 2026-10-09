using Autodesk.AutoCAD.DatabaseServices;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Common
{
    /// <summary>Updates one drawing view from the shared design.</summary>
    public interface IViewUpdater
    {
        string ViewId { get; }

        ViewUpdateResult Update(Database db, Transaction tr, ElevatorParameters design);
    }
}
