namespace Elevator.Core.Shaft
{
    /// <summary>Shaft inputs of the project: width, depth, pit and overhead.</summary>
    public sealed class ShaftConfiguration
    {
        public double WidthMm;
        public double DepthMm;
        public double? PitDepthMm;
        public double? OverheadMm;
    }
}
