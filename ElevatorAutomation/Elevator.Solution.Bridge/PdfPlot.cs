using System;

namespace Elevator.Solution.Bridge
{
    /// <summary>The plot job the client leaves for the Core Console host (plot-request.json).</summary>
    public sealed class PdfPlotRequest
    {
        public string Layout;
        public string Pdf;
    }

    /// <summary>The host's answer (plot.json).</summary>
    public sealed class PdfPlotResult
    {
        public bool Success;
        public string Error;
        public string Media;
        public double ElapsedSeconds;
    }
}
