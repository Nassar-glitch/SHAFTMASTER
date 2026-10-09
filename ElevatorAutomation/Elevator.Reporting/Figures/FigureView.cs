using System;

namespace Elevator.Reporting.Figures
{
    /// <summary>Model-to-paper transform: model mm, y up → paper mm, y down; uniform scale.</summary>
    public sealed class FigureView
    {
        public double Scale;
        public double MinX;
        public double MaxY;
        public double OffsetX;
        public double OffsetY;

        public FigureView(
            double minX,
            double minY,
            double maxX,
            double maxY,
            double left,
            double top,
            double widthMm,
            double heightMm,
            bool alignTop = false)
        {
            Scale = Math.Min(widthMm / (maxX - minX), heightMm / (maxY - minY));
            MinX = minX;
            MaxY = maxY;
            OffsetX = left + (widthMm - (maxX - minX) * Scale) / 2;
            OffsetY = alignTop ? top : top + (heightMm - (maxY - minY) * Scale) / 2;
        }

        /// <summary>Converts model coordinates to paper coordinates.</summary>
        public FigurePoint P(double x, double y)
        {
            return new FigurePoint(OffsetX + (x - MinX) * Scale, OffsetY + (MaxY - y) * Scale);
        }
    }
}
