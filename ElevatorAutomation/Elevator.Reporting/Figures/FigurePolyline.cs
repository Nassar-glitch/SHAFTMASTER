using System.Collections.Generic;

namespace Elevator.Reporting.Figures
{
    /// <summary>A polyline or polygon of a figure.</summary>
    public sealed class FigurePolyline : FigureEntity
    {
        public List<FigurePoint> Points = new List<FigurePoint>();
        public bool Closed;
        public bool Filled;
    }
}
