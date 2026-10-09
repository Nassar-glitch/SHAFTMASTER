using Elevator.Core.Project;

namespace Elevator.Core.Geometry
{
    /// <summary>Bounds of one drawing view.</summary>
    public sealed class ViewBounds
    {
        public DrawingViews View;
        public double MinX;
        public double MinY;
        public double MaxX;
        public double MaxY;
    }
}
