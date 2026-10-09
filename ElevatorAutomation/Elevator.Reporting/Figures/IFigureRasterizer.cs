namespace Elevator.Reporting.Figures
{
    /// <summary>
    /// PNG rendering and text measurement for figures, supplied by the application (WPF in ShaftMaster: WpfFigureRasterizer).
    /// </summary>
    public interface IFigureRasterizer
    {
        // Sets Figure.Measure and FigureFonts.Resolve; called once, before the first figure is built.
        void Initialise();
        byte[] Png(Figure f, double dpi);
    }
}
