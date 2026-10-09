using System.Collections.Generic;
using System.Linq;
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
    public sealed class VerticalManifest
    {
        public int SchemaVersion = 1;
        public List<string> Keys = new List<string>();
        public string InputJson;
    }
}
