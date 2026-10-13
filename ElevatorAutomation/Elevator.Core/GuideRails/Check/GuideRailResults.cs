using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Calculations;

namespace Elevator.Core.GuideRails
{
    /// <summary>One item of a rail load case: a force, a stress, the slenderness, the omega value or a deflection.</summary>
    public sealed class RailItem
    {
        public string Id;
        public string Title;
        public string Clause;
        /// <summary>The equation with the numbers substituted, ending in the value.</summary>
        public string Equation;
        public double Value;
        /// <summary>The permissible value; null for a force or the omega value (no limit).</summary>
        public double? Permissible;
        /// <summary>N, N/mm², mm or "" (the slenderness and omega are numbers).</summary>
        public string Unit;
        /// <summary>OK / NOT OK; null when the item has no limit.</summary>
        public bool? Ok;
        /// <summary>False for σx, σy and σk, which are parts of the combined stresses (the Design Notes name the combined item).</summary>
        public bool Primary = true;

        /// <summary>True for a force item (value in N).</summary>
        public bool IsForce
        {
            get
            {
                return Id == "Fx" || Id == "Fy" || Id == "Fv";
            }
        }
    }

    /// <summary>The hand-written formulas of the forces of one load case (one per distribution case).</summary>
    public sealed class RailForce
    {
        public string Name;
        public double FxN;
        public double FyN;
        public string FxEquation;
        public string FyEquation;
    }

    /// <summary>Input of one load case of one rail line.</summary>
    public sealed class RailCaseInput
    {
        public string Id;
        public string Title;
        public string Clause;
        public RailProfile Profile;
        public double SpacingMm;
        public List<RailForce> Forces = new List<RailForce>();
        public double FvN;
        public string FvEquation;
        /// <summary>k3 · Maux, the vertical force of the auxiliary equipment on one rail.</summary>
        public double AuxN;
        /// <summary>The line of k3·Maux with its values; null when the machine is not on the rails.</summary>
        public string AuxEquation;
        public bool SafetyDevice;
        public double DeflectionPermMm;
        /// <summary>The St used: Table 15.</summary>
        public double SafetyFactor;
    }

    /// <summary>The result of one load case of one rail line.</summary>
    public sealed class RailLoadCaseResult
    {
        public string Id;
        public string Title;
        public string Clause;
        public double SafetyFactor;
        public double SigmaPermNmm2;
        public double FvN;
        public double AuxN;
        public string AuxEquation;
        public List<RailItem> Items = new List<RailItem>();

        public bool Ok
        {
            get
            {
                return Items.All(i => i.Ok != false);
            }
        }

        public RailItem Item(string id)
        {
            return Items.FirstOrDefault(i => i.Id == id);
        }
    }

    /// <summary>The result of one rail line (the car rails or the counterweight rails).</summary>
    public sealed class RailLineResult
    {
        /// <summary>"Car" or "Counterweight".</summary>
        public string Name;
        public RailProfile Profile;
        public double SpacingMm;
        /// <summary>Mg: mass of one line of rails (kg).</summary>
        public double LineMassKg;
        public double LineLengthMm;
        public double PushThroughN;
        public int BracketCount;
        public List<RailLoadCaseResult> Cases = new List<RailLoadCaseResult>();

        public bool Ok
        {
            get
            {
                return Cases.Count > 0 && Cases.All(c => c.Ok);
            }
        }

        public RailLoadCaseResult Case(string id)
        {
            return Cases.FirstOrDefault(c => c.Id == id);
        }

        /// <summary>The failed primary items, as "load case title: item title".</summary>
        public IEnumerable<KeyValuePair<RailLoadCaseResult, RailItem>> Failures()
        {
            foreach (var c in Cases)
            {
                foreach (var i in c.Items.Where(x => x.Ok == false && x.Primary))
                    yield return new KeyValuePair<RailLoadCaseResult, RailItem>(c, i);
            }
        }
    }

    /// <summary>The guide-rail check of a project: the car rails and the counterweight rails.</summary>
    public sealed class GuideRailCheckResult
    {
        public string Status = "MISSING_INPUT";
        public List<CalculationIssue> Issues = new List<CalculationIssue>();
        public GuideRailInputs Inputs;
        public RailLineResult Car;
        public RailLineResult Counterweight;

        /// <summary>True when both rail lines were calculated.</summary>
        public bool Available
        {
            get
            {
                return Car != null && Counterweight != null && Issues.Count == 0;
            }
        }

        public bool Ok
        {
            get
            {
                return Available && Car.Ok && Counterweight.Ok;
            }
        }
    }
}
