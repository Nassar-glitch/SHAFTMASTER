using Elevator.Core.Calculations;
using Elevator.Core.Project;

namespace Elevator.Core.MachineRoom
{
    internal sealed class GeometrySourceSnapshot
    {
        internal readonly string InputFingerprint;
        internal readonly string ContextFingerprint;
        internal readonly DesignContext OriginContext;

        internal GeometrySourceSnapshot(ElevatorProject project, DesignContext context)
        {
            InputFingerprint = ChassisGeometryProvenance.InputHash(project);
            ContextFingerprint = CalculationFingerprint.Hash(context);
            OriginContext = context;
        }
    }
}
