using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Plan
{
    /// <summary>The plan entities read from the drawing with their frame and dimensions.</summary>
    public sealed class DrawingSnapshot
    {
        public ShaftFrame Frame;
        public ElevatorParameters ElevatorParameters;
        public Dictionary<string, Entity> Entities;
        public Dictionary<string, Binding> Bindings;
        public int ModelEntityCount;

        /// <summary>An entity of the snapshot by key, as a type.</summary>
        public T Get<T>(string key) where T : Entity
        {
            return (T)Entities[key];
        }
    }
}
