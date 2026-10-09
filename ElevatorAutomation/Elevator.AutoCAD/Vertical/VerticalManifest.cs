using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Elevator.Core.Geometry;
using Elevator.Core.MachineRoom;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Serialization;

namespace Elevator.AutoCAD.Vertical
{
    /// <summary>The recorded entities and input of the vertical view.</summary>
    public sealed class VerticalManifest
    {
        public int SchemaVersion = 1;
        public List<string> Keys = new List<string>();
        public string InputJson;
    }
}
