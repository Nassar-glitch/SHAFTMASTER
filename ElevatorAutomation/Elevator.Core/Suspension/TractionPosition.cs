namespace Elevator.Core.Suspension
{
    /// <summary>One load position of a traction case with T1, T2 and their ratio.</summary>
    public sealed class TractionPosition
    {
        public string Name;
        public double T1N;
        public double T2N;
        public double Ratio;
        public string T1Equation;
        public string T2Equation;
    }
}
