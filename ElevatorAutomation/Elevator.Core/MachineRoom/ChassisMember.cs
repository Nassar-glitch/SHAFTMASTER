using Elevator.Core.Geometry;

namespace Elevator.Core.MachineRoom
{
    /// <summary>A selected upper longitudinal member of the chassis: front and rear points.</summary>
    public sealed class ChassisMember
    {
        public string Id;
        public string Evidence;
        public LocalPoint Front;
        public LocalPoint Rear;
    }
}
