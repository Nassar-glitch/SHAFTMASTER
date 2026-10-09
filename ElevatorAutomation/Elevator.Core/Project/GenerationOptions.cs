namespace Elevator.Core.Project
{
    /// <summary>Options of a drawing generation run: views, arrangement and output path.</summary>
    public sealed class GenerationOptions
    {
        public DrawingViews Views = DrawingViews.All;
        public bool ArrangeViews;
        public string OutputPath;
    }
}
