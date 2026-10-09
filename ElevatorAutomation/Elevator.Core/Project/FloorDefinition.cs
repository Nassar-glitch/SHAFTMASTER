using System;
using Elevator.Core.Doors;

namespace Elevator.Core.Project
{
    /// <summary>One floor of the building: name, height to the next floor, served flag and landing door.</summary>
    public sealed class FloorDefinition
    {
        public string FloorId = Guid.NewGuid().ToString("N");
        public string Name;

        // Interval to NEXT normal floor; last is null. Structural R is calculated.
        public double? HeightToNextMm;
        public bool IsServed = true;
        public LandingDoorConfiguration LandingDoor = new LandingDoorConfiguration();
    }
}
