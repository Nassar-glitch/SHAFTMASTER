using System;

namespace Elevator.Core.Project
{
    public sealed class ProjectInfo
    {
        public string ProjectId = Guid.NewGuid().ToString("N");
        public string Name;
    }
}
