namespace Elevator.Core.Geometry
{
    /// <summary>One floor level of the vertical section: elevation, served flag and roof flag.</summary>
    public sealed class VerticalFloor
    {
        public string FloorId;
        public string Name;
        public bool Served = true;
        public bool IsRoof;
        public double FflZ;
    }
}
