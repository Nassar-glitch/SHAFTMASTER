using Elevator.Core.Project;

namespace Elevator.Core.Geometry
{
    /// <summary>Offset that places one drawing view on the sheet.</summary>
    public sealed class ViewPlacement
    {
        public DrawingViews View;
        public double DeltaX;
        public double DeltaY;
    }
}
