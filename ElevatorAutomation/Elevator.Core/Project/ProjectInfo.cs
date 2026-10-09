using System;

namespace Elevator.Core.Project
{
    /// <summary>Project identity: id and name.</summary>
    public sealed class ProjectInfo
    {
        public string ProjectId = Guid.NewGuid().ToString("N");
        public string Name;
    }
}
