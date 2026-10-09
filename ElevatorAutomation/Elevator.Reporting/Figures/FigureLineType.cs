namespace Elevator.Reporting.Figures
{
    /// <summary>
    /// Engineering figure model in paper millimetres (origin top-left, y downwards), rendered to SVG and PNG. Drawing standard: white
    /// background; geometry black/dark grey; loads in one accent colour (red); ISO line weights; centre lines dash-dot, hidden lines
    /// dashed; ANSI31 hatch; 2.5 mm text, 3.5 mm titles.
    /// </summary>
    public enum FigureLineType
    {
        Continuous,
        Centre,
        Hidden
    }
}
