using System;

namespace Elevator.Core.Application
{
    /// <summary>What a changed project input makes outdated (ARCHITECTURE-PLAN P4).</summary>
    [Flags]
    public enum InputEffect
    {
        None = 0,
        // The validation / drawing design result.
        Design = 1,
        // Basic / Advanced P1–P4.
        Chassis = 2,
        RopeTraction = 4,
        // FF1 / FF2 (their geometry follows Q, SW and SD).
        CarGuideForces = 8,
    }
}
