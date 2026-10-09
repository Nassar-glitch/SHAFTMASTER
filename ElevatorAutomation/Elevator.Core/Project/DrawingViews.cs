using System;

namespace Elevator.Core.Project
{
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
