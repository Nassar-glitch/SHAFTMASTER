using System.Collections.Generic;

namespace Elevator.Core.Project
{
    public sealed class DrawingGenerationResult
    {
        public ElevatorDesignResult Design;
        public Dictionary<string, int> EntityCounts = new Dictionary<string, int>();
        public string OutputPath;
    }
}
