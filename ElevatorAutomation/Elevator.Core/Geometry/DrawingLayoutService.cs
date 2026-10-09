using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Project;

namespace Elevator.Core.Geometry
{
    public static class DrawingLayoutService
    {
        // Top row Plan/MR, bottom row AA/BB; measured extents, no engineering WCS coordinates.
        public static List<ViewPlacement> Arrange(IEnumerable<ViewBounds> bounds, double margin)
        {
            if (!ProjectFloors.FinitePositive(margin))
                throw new ArgumentException("Positive view margin required.");
            var b = bounds.ToDictionary(v => v.View);
            if (b.Values.Any(v => v.MaxX < v.MinX
                || v.MaxY < v.MinY
                || new[] { v.MinX, v.MinY, v.MaxX, v.MaxY }.Any(x => Double.IsNaN(x) || Double.IsInfinity(x))))
                throw new ArgumentException("Invalid view extents.");
            var left = new[] { DrawingViews.Plan, DrawingViews.SectionAA };
            double leftWidth = b.Where(v => left.Contains(v.Key)).Select(v => v.Value.MaxX - v.Value.MinX).DefaultIfEmpty(0).Max();
            double topHeight = b.Where(v => v.Key == DrawingViews.Plan || v.Key == DrawingViews.MachineRoomPlan)
                .Select(v => v.Value.MaxY - v.Value.MinY)
                .DefaultIfEmpty(0)
                .Max();
            return b.Values.OrderBy(v => (int)v.View).Select(v => new ViewPlacement
            {
                View = v.View,
                DeltaX = (left.Contains(v.View) ? 0 : leftWidth + margin) - v.MinX,
                DeltaY = (v.View == DrawingViews.Plan || v.View == DrawingViews.MachineRoomPlan ? 0 : -topHeight - margin) - v.MaxY
            }).ToList();
        }
    }
}
