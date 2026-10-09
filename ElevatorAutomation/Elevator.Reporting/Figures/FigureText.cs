namespace Elevator.Reporting.Figures
{
    /// <summary>A text of a figure.</summary>
    public sealed class FigureText : FigureEntity
    {
        public FigurePoint At;
        public string Content;
        public double HeightMm = 2.5;
        public string Anchor = "start";
        public bool Vertical;
        public bool Title;
        public double WidthMm; // measured with the figure font

        /// <summary>Axis-aligned box in paper mm; At is on the baseline.</summary>
        public void Box(out double x0, out double y0, out double x1, out double y1)
        {
            double ascent = HeightMm * 1.05, descent = HeightMm * 0.3;
            if (!Vertical)
            {
                double s = Anchor == "middle" ? At.X - WidthMm / 2 : Anchor == "end" ? At.X - WidthMm : At.X;
                x0 = s;
                x1 = s + WidthMm;
                y0 = At.Y - ascent;
                y1 = At.Y + descent;
            }
            else
            {
                double s = Anchor == "middle" ? At.Y + WidthMm / 2 : Anchor == "end" ? At.Y + WidthMm : At.Y;
                y1 = s;
                y0 = s - WidthMm;
                x0 = At.X - ascent;
                x1 = At.X + descent;
            }
        }
    }
}
