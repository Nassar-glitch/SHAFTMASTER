using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Plan
{
    public sealed class DrawingSnapshot
    {
        public ShaftFrame Frame;
        public ElevatorParameters ElevatorParameters;
        public Dictionary<string, Entity> Entities;
        public Dictionary<string, Binding> Bindings;
        public int ModelEntityCount;

        public T Get<T>(string key) where T : Entity
        {
            return (T)Entities[key];
        }
    }
}
