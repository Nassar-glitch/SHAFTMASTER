using System;
namespace Elevator.Core
{
    public static class ReevingSolver
    {
        private static double Dot(LocalPoint a, LocalPoint b)
        {
            return a.X * b.X + a.Y * b.Y;
        }
        private static LocalPoint Add(LocalPoint a, LocalPoint b, double scale)
        {
            return new LocalPoint(a.X + scale * b.X, a.Y + scale * b.Y, 0);
        }
        private static bool Finite(double n)
        {
            return !Double.IsNaN(n) && !Double.IsInfinity(n);
        }
        public static ReevingSolution Calculate(
            LocalPoint traction,
            LocalPoint deflector,
            double tractionDiameter,
            double deflectorDiameter,
            double target = 152)
        {
            var result = new ReevingSolution
            {
                TractionSheaveCenter = traction,
                DeflectorSheaveCenter = deflector,
                TractionSheavePitchDiameterMm = tractionDiameter,
                DeflectorSheavePitchDiameterMm = deflectorDiameter,
                TargetWrapAngleDeg = target,
                GeometryStatus = "INFEASIBLE",
                WrapAngleStatus = "NOT YET EVALUABLE"
            };
            foreach (double v in new[] { traction.X, traction.Y, deflector.X, deflector.Y, tractionDiameter, deflectorDiameter, target })
                if (!Finite(v))
                    throw new ArgumentException("Reeving inputs must be finite.");
            if (tractionDiameter <= 0 || deflectorDiameter <= 0 || target <= 0 || target >= 180)
                throw new ArgumentException("Positive pitch diameters and a target between zero and 180 degrees are required.");
            double dx = deflector.X - traction.X, dy =
                deflector.Y - traction.Y, len = Math.Sqrt(dx * dx + dy * dy), r = tractionDiameter / 2, s = deflectorDiameter / 2;
            result.HorizontalCenterOffsetMm = Math.Abs(dx);
            result.VerticalCenterOffsetMm = traction.Y - deflector.Y;
            result.CenterDistanceMm = len;
            if (len <= r + s + 1e-9)
            {
                result.GeometryStatus = "INFEASIBLE - SHEAVE CIRCLES INTERSECT OR TOUCH";
                return result;
            }
            var u = new LocalPoint(dx / len, dy / len, 0);
            double c = (r - s) / len, q = Math.Sqrt(1 - c * c);
            // Common external tangent: n dot (D-T)=R-r. Choose the branch reached
            // counterclockwise from the right-hand vertical car drop in this B-B reference.
            foreach (double sign in new[] { -1.0, 1.0 })
            {
                var n = new LocalPoint(c * u.X - sign * q * u.Y, c * u.Y + sign * q * u.X, 0);
                var a = Add(traction, n, r);
                var b = Add(deflector, n, s);
                var delta = new LocalPoint(b.X - a.X, b.Y - a.Y, 0);
                double l = Math.Sqrt(Dot(delta, delta));
                var direction = new LocalPoint(delta.X / l, delta.Y / l, 0);
                if (Dot(new LocalPoint(-n.Y, n.X, 0), direction) < .999999)
                    continue;
                double angle = Math.Atan2(n.Y, n.X) * 180 / Math.PI;
                if (angle < 0)
                    angle += 360;
                result.TractionTangentPoint = a;
                result.DeflectorTangentPoint = b;
                result.RopeEntryDirection = new LocalPoint(0, 1, 0);
                result.RopeExitDirection = direction;
                result.RopeEntryAngleDeg = 90;
                result.RopeExitAngleDeg = Math.Atan2(direction.Y, direction.X) * 180 / Math.PI;
                result.CalculatedWrapAngleDeg = angle;
                result.WrapAngleStatus = angle + 1e-9 >= target ? "PASS" : "FAIL";
                result.GeometryStatus = "PASS";
                return result;
            }
            return result;
        }
        public static VerticalArrangementResult SolveVertical(VerticalArrangementInput p)
        {
            var result = new VerticalArrangementResult();
            foreach (double value in new[] { p.CarDropX,
                p.CwDropX,
                p.TractionDiameterMm,
                p.DeflectorDiameterMm,
                p.MachineCenterAboveSteelMm,
                p.FixedLevelingDepthMm,
                p.SteelDepthMm,
                p.HangerTopBelowSteelTopMm,
                p.HangerBottomAboveDeflectorCenterMm,
                p.MinConcreteHeightMm,
                p.MaxConcreteHeightMm,
                p.TargetWrapAngleDeg })
                if (!Finite(value))
                    throw new ArgumentException("Vertical arrangement inputs must be finite.");
            if (!p.AdjustableDeflectorHangerConfirmed)
            {
                result.Status = "SUPPORT DEPENDENCY CONFIRMATION REQUIRED";
                return result;
            }
            if (p.TractionDiameterMm <= 0 || p.TargetWrapAngleDeg <= 0 || p.TargetWrapAngleDeg >= 180)
                throw new ArgumentException("Invalid traction diameter or wrap target.");
            if (p.DeflectorDiameterMm != 250 && p.DeflectorDiameterMm != 400)
                throw new ArgumentException("Only 250 and 400 mm deflectors are available.");
            if (p.StackedSteelLevels < 1
                || p.SteelDepthMm <= 0
                || p.MinConcreteHeightMm < 350
                || p.MaxConcreteHeightMm > 1000
                || p.MinConcreteHeightMm > p.MaxConcreteHeightMm)
                throw new ArgumentException("Invalid support arrangement constraints.");
            if (p.PreferredHeightIncrementMm.HasValue)
                throw new ArgumentException("No rounding increment has been approved.");
            double r = p.TractionDiameterMm / 2, s = p.DeflectorDiameterMm / 2;
            double tx = p.CarDropX - r, dx = p.CwDropX + s, h = tx - dx;
            if (h <= 0)
            {
                result.Status = "REEVING REDESIGN REQUIRED - UNSUPPORTED ROPE BRANCH";
                return result;
            }
            double theta = (180 - p.TargetWrapAngleDeg) * Math.PI / 180;
            double v = (h * Math.Cos(theta) - (r - s)) / Math.Sin(theta);
            // Preserve the native hanger's two attachment offsets; its straight legs may
            // change length but must not invert. This is geometry, not an allowable range.
            v = Math.Max(v, p.MachineCenterAboveSteelMm + p.HangerTopBelowSteelTopMm + p.HangerBottomAboveDeflectorCenterMm);
            v = Math.Max(v, p.MachineCenterAboveSteelMm + p.SteelDepthMm + s);
            double above = p.FixedLevelingDepthMm + p.StackedSteelLevels * p.SteelDepthMm + p.MachineCenterAboveSteelMm;
            if (p.MinimumDeflectorSlabClearanceMm.HasValue
                && (!Finite(p.MinimumDeflectorSlabClearanceMm.Value) || p.MinimumDeflectorSlabClearanceMm.Value < 0))
                throw new ArgumentException("Invalid supplied slab clearance.");
            double required = s + (p.MinimumDeflectorSlabClearanceMm ?? 0) + v - above;
            result.RequiredConcreteBeamHeightMm = required;
            double height = Math.Max(p.MinConcreteHeightMm, required);
            if (height > p.MaxConcreteHeightMm + 1e-8)
            {
                result.Status = "REEVING REDESIGN REQUIRED";
                return result;
            }
            result.ConcreteBeamHeightMm = height;
            if (p.ReferenceConcreteHeightMm.HasValue)
                result.RequiredChassisElevationAdjustmentMm = height - p.ReferenceConcreteHeightMm.Value;
            result.RequiredVerticalCenterSeparationMm = v;
            result.RequiredTractionSheaveCenterZ = height + above;
            result.RequiredDeflectorCenterZ = height + above - v;
            result.RequiredHangerLengthMm =
                v - p.MachineCenterAboveSteelMm - p.HangerTopBelowSteelTopMm - p.HangerBottomAboveDeflectorCenterMm;
            result.DeflectorToSlabClearanceMm = result.RequiredDeflectorCenterZ - s;
            if (p.MinimumDeflectorSlabClearanceMm.HasValue)
                result.DeflectorSlabClearanceStatus = result.DeflectorToSlabClearanceMm + 1e-8 >= p.MinimumDeflectorSlabClearanceMm
                    ? "PASS - SUPPLIED GEOMETRIC MINIMUM"
                    : "FAIL";
            result.Reeving = Calculate(
                new LocalPoint(tx, result.RequiredTractionSheaveCenterZ.Value, 0),
                new LocalPoint(dx, result.RequiredDeflectorCenterZ.Value, 0),
                p.TractionDiameterMm,
                p.DeflectorDiameterMm,
                p.TargetWrapAngleDeg);
            result.Status =
                result.Reeving.GeometryStatus == "PASS" && result.Reeving.WrapAngleStatus == "PASS" ? "PASS" : "REEVING REDESIGN REQUIRED";
            return result;
        }
    }
}
