namespace Elevator.Core.GuideRails
{
    /// <summary>Inputs of FF1 and FF2.</summary>
    public sealed class CarGuideForceInputs
    {
        // User-approved interpretation: vertical centre-to-centre upper-to-lower car
        // guide-shoe spacing. The native drawing does not establish this dimension. Null stays missing.
        public double? GuideShoeSpacingMm;
    }
}
