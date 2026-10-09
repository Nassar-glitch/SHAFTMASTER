using System;
using System.IO;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using Elevator.Core.Geometry;
using Elevator.Core.Project;

namespace Elevator.Solution.Bridge
{
    /// <summary>Transport envelopes reference the existing versioned project JSON, never a second project model.</summary>
    public sealed class GenerationRequest
    {
        public int ProtocolVersion = BridgeContract.Version;
        public string RequestId = Guid.NewGuid().ToString("N");
        public string ProjectJson;
        public GenerationOptions Options = new GenerationOptions { ArrangeViews = true };
    }
}
