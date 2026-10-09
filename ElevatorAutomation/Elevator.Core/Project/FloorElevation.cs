namespace Elevator.Core.Project
{
    /// <summary>A floor with its calculated elevation.</summary>
    public sealed class FloorElevation
    {
        public string FloorId;
        public string Name;
        public double ElevationMm;
        public bool IsServed;
    }
}
