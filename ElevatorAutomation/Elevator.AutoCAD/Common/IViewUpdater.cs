using Autodesk.AutoCAD.DatabaseServices;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Common
{
    public interface IViewUpdater
    {
        string ViewId { get; }

        ViewUpdateResult Update(Database db, Transaction tr, ElevatorParameters design);
    }
}
