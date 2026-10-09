namespace Elevator.Core.Suspension
{
    /// <summary>One check of the rope and traction calculation: clause, equation, required and actual values, result.</summary>
    public sealed class RopeCheck
    {
        public string Id;
        public string Title;
        public string Clause;
        public string Equation;
        public string Substitution;
        public string Required;
        public string Actual;
        public string Unit;
        public double? RequiredValue;
        public double? ActualValue;
        public bool? Ok; // null = computed requirement (no comparison)
    }
}
