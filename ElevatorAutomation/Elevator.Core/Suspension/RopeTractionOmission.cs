namespace Elevator.Core.Suspension
{
    /// <summary>A rope or traction check that could not be done, with the reason.</summary>
    public sealed class RopeTractionOmission
    {
        public string Item;
        public string Reason;
        public string Kind;
    } // Kind: MISSING_INPUT, INVALID_INPUT, INVALID_GEOMETRY, NOT_APPLICABLE
}
