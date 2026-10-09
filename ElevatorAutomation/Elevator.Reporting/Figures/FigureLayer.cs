namespace Elevator.Reporting.Figures
{
    public sealed class FigureLayer
    {
        public string Name;
        public double WeightMm;
        public FigureLineType LineType;
        public string Colour;
        public static readonly FigureLayer Outline = new FigureLayer { Name = "outline", WeightMm = 0.5, Colour = "000000" };
        public static readonly FigureLayer Secondary = new FigureLayer { Name = "secondary", WeightMm = 0.35, Colour = "2B2B2B" };
        public static readonly FigureLayer Thin = new FigureLayer { Name = "thin", WeightMm = 0.25, Colour = "2B2B2B" };
        public static readonly FigureLayer Dimension = new FigureLayer { Name = "dimension", WeightMm = 0.18, Colour = "000000" };
        public static readonly FigureLayer Hatch = new FigureLayer { Name = "hatch", WeightMm = 0.18, Colour = "4A4A4A" };
        public static readonly FigureLayer Leader = new FigureLayer { Name = "leader", WeightMm = 0.25, Colour = "000000" };

        public static readonly FigureLayer Centre =
            new FigureLayer { Name = "centre", WeightMm = 0.25, LineType = FigureLineType.Centre, Colour = "2B2B2B" };

        public static readonly FigureLayer Hidden =
            new FigureLayer { Name = "hidden", WeightMm = 0.25, LineType = FigureLineType.Hidden, Colour = "2B2B2B" };

        public static readonly FigureLayer Load = new FigureLayer { Name = "load", WeightMm = 0.35, Colour = "D0021B" };
        public static readonly FigureLayer Text = new FigureLayer { Name = "text", WeightMm = 0, Colour = "000000" };
        public static readonly FigureLayer LoadText = new FigureLayer { Name = "load-text", WeightMm = 0, Colour = "D0021B" };
    }
}
