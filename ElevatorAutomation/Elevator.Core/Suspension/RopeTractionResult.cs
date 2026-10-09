using System.Collections.Generic;
using System.Linq;

namespace Elevator.Core.Suspension
{
    public sealed class RopeTractionResult
    {
        public string Status = "MISSING_INPUT";
        public string RuleVersion = RopeTractionCalculationEngine.RuleVersion;
        public string InputFingerprint;
        public RopeTractionEffectiveInputs Inputs = new RopeTractionEffectiveInputs();
        public RopeTractionGeometry Geometry = new RopeTractionGeometry();
        public double? HangingLengthM;
        public double? TravelM;
        public double? TmaxN;
        public double? SfActual;
        public double? SfMinimum8120;
        public double? Sf8150;
        public double? SfRequired;
        public double? NequivT;
        public double? Kp;
        public double? NequivP;
        public double? Nequiv;
        public double? DtOverDr;
        public double? MdpKg;

        // Deflector Dp / d as a value only (DESIGN-FLOW 11a): not a check row; the smaller deflector enters Sf through Kp (EN 81-50 5.12).
        public double? DpOverDr;
        public List<RopeCheck> Checks = new List<RopeCheck>();
        public List<TractionCaseResult> Traction = new List<TractionCaseResult>();
        public List<RopeTractionOmission> Omissions = new List<RopeTractionOmission>();

        public bool AnyNotOk
        {
            get
            {
                return Checks.Any(c => c.Ok == false) || Traction.Any(t => !t.Ok);
            }
        }

        public bool HasResults
        {
            get
            {
                return Checks.Count > 0 || Traction.Count > 0;
            }
        }

        internal string LibraryHash;
    }
}
