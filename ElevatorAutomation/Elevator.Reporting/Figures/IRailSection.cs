using System.Collections.Generic;

namespace Elevator.Reporting.Figures
{
    // Rail cross-section geometry. Today derived proportionally from the designation (only b1, h1, k are known);
    // a future table (designation → full dimensions) can implement this interface without changing figure code.
    public interface IRailSection
    {
        string Designation { get; }
        double BaseWidthMm { get; }   // b1
        double HeightMm { get; }      // h1
        double BladeWidthMm { get; }  // k
        bool Proportional { get; }    // true: features other than b1, h1, k are drawn proportionally and not dimensioned

        IList<FigurePoint> Outline(); // closed outline in section mm: x across the foot (centre 0), y from foot underside upwards
    }
}
