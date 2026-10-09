using System.Collections.Generic;

namespace Elevator.Core.Suspension
{
    public sealed class TractionCaseResult
    {
        public string Id;
        public string Title;
        public string Clause;
        public string Condition;
        public double Mu;
        public double F;
        public double AlphaDeg;
        public double EFAlpha;
        public double T1N;
        public double T2N;
        public double Ratio;
        public bool Ok;
        public string MuEquation;
        public string FEquation;
        public List<TractionPosition> Positions = new List<TractionPosition>();
    }
}
