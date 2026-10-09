using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using Elevator.Core.Geometry;
using Elevator.Core.Project;

namespace Elevator.Solution.Bridge
{
    /// <summary>Response of the host: statuses, entity counts, validation and the design.</summary>
    public sealed class GenerationResponse
    {
        public int ProtocolVersion = BridgeContract.Version;
        public string RequestId;
        public string InputHash;
        public string OutputPath;
        public string Error;
        public bool Success;
        public double ElapsedSeconds;
        public string[] ViewsGenerated;
        public Dictionary<string, int> EntityCounts = new Dictionary<string, int>();
        public List<ValidationIssue> Validation = new List<ValidationIssue>();
        public string LayoutStatus;
        public string WrapAngleStatus;
        public string CodeDiameterRatioStatus;
        public string DesignJson;
        public DesignContext Context;
        public List<ViewBounds> Bounds = new List<ViewBounds>();
    }
}
