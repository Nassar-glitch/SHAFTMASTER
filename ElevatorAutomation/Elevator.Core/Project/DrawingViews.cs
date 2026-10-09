using System;

namespace Elevator.Core.Project
{
    /// <summary>The drawing views, combinable as flags.</summary>
    [Flags]
    public enum DrawingViews
    {
        Plan = 1,
        MachineRoomPlan = 2,
        SectionAA = 4,
        SectionBB = 8,
        All = 15
    }
}
