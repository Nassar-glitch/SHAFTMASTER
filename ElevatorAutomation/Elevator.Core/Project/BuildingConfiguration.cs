using System.Collections.Generic;

namespace Elevator.Core.Project
{
    /// <summary>Building inputs of the project: the floors.</summary>
    public sealed class BuildingConfiguration
    {
        public List<FloorDefinition> Floors = new List<FloorDefinition>();
    }
}
