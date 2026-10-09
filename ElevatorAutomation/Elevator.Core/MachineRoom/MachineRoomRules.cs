using System;
using Elevator.Core.Project;
using Elevator.Core.SafetyGear;
using Elevator.Core.Suspension;

namespace Elevator.Core.MachineRoom
{
    public static class MachineRoomRules
    {
        public static GovernorOffsetSelection SelectGovernorOffset(double tz)
        {
            if (Double.IsNaN(tz) || Double.IsInfinity(tz) || tz < 0)
                throw new ArgumentException("TZ must be finite and nonnegative.");
            double? value = tz < 700 ? 100 : (tz < 850 ? 150 : (tz <= 1100 ? (double?)200 : null));
            return new GovernorOffsetSelection
            {
                OffsetFromCarGuideAxisMm = value,
                SelectionStatus = value.HasValue ? "SELECTED BY PROJECT RULE" : "REVIEW - GOVERNOR OFFSET OUT OF RANGE",
                RuleSource = "Engineer-confirmed project rule: <700:100; 700<=TZ<850:150; 850<=TZ<=1100:200; >1100 review."
            };
        }

        public static DeflectorSheaveSelection SelectDeflector(double tz)
        {
            if (Double.IsNaN(tz) || Double.IsInfinity(tz) || tz < 0)
                throw new ArgumentException("TZ must be finite and nonnegative.");
            return new DeflectorSheaveSelection
            {
                IsRequired = tz >= 520,
                PitchDiameterMm = tz < 520 || tz > 1100 ? (double?)null : (tz <= 800 ? 250 : 400),
                SelectionStatus = tz > 1100 ? "REQUIRES TRACTION/REEVING REVIEW" : (tz < 520 ? "NOT REQUIRED" : "SELECTED BY PROJECT RULE"),
                RuleSource = "Project Design Rule MR-DS-001, supplied by engineer; not an EN 81-20 threshold"
            };
        }

        public static MachineRoomParameters Calculate(ProjectInput input, ElevatorParameters shared, MachineRoomExtensions extensions)
        {
            if (extensions == null)
                throw new ArgumentNullException("extensions");
            SuspensionProjectRules.Validate(input.SuspensionRopeDiameterMm, input.NumberOfSuspensionRopes);
            double?[] requested = { input.MachineRoomLeftExtension,
                input.MachineRoomRightExtension,
                input.MachineRoomFrontExtension,
                input.MachineRoomRearExtension };
            double[] measured = { extensions.Left, extensions.Right, extensions.Front, extensions.Rear };
            for (int i = 0; i < requested.Length; i++)
                if (requested[i].HasValue && (Double.IsNaN(requested[i].Value)
                    || Double.IsInfinity(requested[i].Value)
                    || Math.Abs(requested[i].Value - measured[i]) > .02))
                    throw new ArgumentException(
                        "Machine-room extensions must match the measured reference in this version; changing them requires "
                        + "template/engineering review.");
            foreach (double? v in new[] { input.SuspensionRopeDiameterMm, input.TractionSheavePitchDiameterMm })
                if (v.HasValue && (Double.IsNaN(v.Value) || Double.IsInfinity(v.Value) || v.Value <= 0))
                    throw new ArgumentException("Supplied rope/sheave diameters must be finite and positive.");
            // Extensions are measured from the supplied view, not newly selected room dimensions.
            var result = new MachineRoomParameters
            {
                Extensions = extensions,
                RoomWidth = shared.SW + extensions.Left + extensions.Right,
                RoomDepth = shared.SD + extensions.Front + extensions.Rear,
                Deflector = SelectDeflector(shared.TZ),
                Governor = SelectGovernorOffset(shared.TZ),
                Reeving = new ReevingGeometry { TractionSheavePitchDiameterMm = input.TractionSheavePitchDiameterMm }
            };
            result.Reeving.DeflectorSheavePitchDiameterMm = result.Deflector.PitchDiameterMm;
            result.Rules = MachineRoomRuleRegistry.Evaluate(input, shared, result);
            result.CodeDiameterRatioStatus = result.Rules.Find(r => r.RuleId == "MR-ROPE-001").Status;
            return result;
        }
    }
}
