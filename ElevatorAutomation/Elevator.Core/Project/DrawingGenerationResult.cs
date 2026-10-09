using System.Collections.Generic;

namespace Elevator.Core.Project
{
    /// <summary>Result of a drawing generation run.</summary>
    public sealed class DrawingGenerationResult
    {
        public ElevatorDesignResult Design;
        public Dictionary<string, int> EntityCounts = new Dictionary<string, int>();
        public string OutputPath;
    }
}
