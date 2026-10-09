using System;

namespace Elevator.Reporting.Docx
{
    /// <summary>
    /// Date and time used by the reports (calculation sheet, Design Notes, DOCX package): the local time in the application; tests replace
    /// it with a fixed value.
    /// </summary>
    public static class ReportClock
    {
        public static Func<DateTime> Now = () => DateTime.Now;
    }
}
