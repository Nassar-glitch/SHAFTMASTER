namespace Elevator.Core.Suspension
{
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
