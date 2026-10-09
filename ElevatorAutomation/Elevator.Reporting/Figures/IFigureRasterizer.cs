namespace Elevator.Reporting.Figures
{
    // PNG rendering and text measurement for figures, supplied by the application (WPF in ShaftMaster: WpfFigureRasterizer).
    public interface IFigureRasterizer
    {
        // Sets Figure.Measure and FigureFonts.Resolve; called once, before the first figure is built.
        void Initialise();
        byte[] Png(Figure f, double dpi);
    }
}
