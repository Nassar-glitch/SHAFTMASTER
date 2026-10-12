namespace Elevator.Core.Layouts
{
    /// <summary>The system type code of the file names (DESIGN-FLOW "Layouts and PDF").</summary>
    public enum ElevatorSystemType
    {
        /// <summary>Geared traction (gearbox): the current system.</summary>
        GB,
        /// <summary>Gearless traction (later).</summary>
        GL,
        /// <summary>Hydraulic (later).</summary>
        HY,
        /// <summary>Machine-room-less (later).</summary>
        MRL
    }

    /// <summary>The counterweight position code of the file names (DESIGN-FLOW "Layouts and PDF").</summary>
    public enum CounterweightPositionCode
    {
        /// <summary>Rear counterweight.</summary>
        RCW,
        /// <summary>Side counterweight, left.</summary>
        SCWL,
        /// <summary>Side counterweight, right.</summary>
        SCWR
    }
}
