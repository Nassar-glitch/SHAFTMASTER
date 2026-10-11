using System;
using System.Globalization;
using Elevator.Core.Project;

namespace Elevator.Core.Doors
{
    /// <summary>
    /// The Doors Offset of an automatic door (2S, 2C) as the user sees it: the distance of the opening centre line from the shaft centre
    /// line and its side, Left or Right as seen in the plan drawing (standing at the landing, facing the door), the range in which it
    /// may be placed, and whether a typed value is outside it. The position itself is in <see cref="DoorLayout"/>: a typed value outside
    /// the range is kept here and the layout places the opening at the nearest allowed position.
    /// </summary>
    public sealed class DoorOffset
    {
        const double Eps = 1e-7;

        /// <summary>Centre shift (plan X, positive to the right) of the smallest allowed position.</summary>
        public double MinShiftMm;

        /// <summary>Centre shift of the largest allowed position.</summary>
        public double MaxShiftMm;

        /// <summary>True when no position is allowed (the door does not fit the shaft at all; the fit message names the cause).</summary>
        public bool RangeEmpty;

        /// <summary>True when the user has set the offset (typed value or a value of an earlier file).</summary>
        public bool Locked;

        /// <summary>The requested centre shift: the typed value, or the preset position before the narrow-shaft shift.</summary>
        public double RequestedShiftMm;

        /// <summary>The centre shift of the drawn position (the nearest allowed one).</summary>
        public double PlacedShiftMm;

        /// <summary>True when the offset was typed and lies outside the range.</summary>
        public bool OutOfRange;

        /// <summary>The reason the left limit is where it is, in plain words.</summary>
        public string LeftReason;

        /// <summary>The reason the right limit is where it is.</summary>
        public string RightReason;

        /// <summary>The type name of the door, for the notes.</summary>
        public string TypeName;

        /// <summary>The side of a centre shift: Left, Right, or the given side at zero.</summary>
        public static string SideOf(double shiftMm, string whenZero)
        {
            return shiftMm < -Eps ? DoorTypes.Left : shiftMm > Eps ? DoorTypes.Right : whenZero;
        }

        static string Num(double v)
        {
            double r = Math.Round(Math.Abs(v), 1);
            return r.ToString("0.#", CultureInfo.InvariantCulture);
        }

        // A signed shift as "140 Left", "60 Right" or "0".
        static string Text(double shift)
        {
            if (Math.Abs(shift) < 0.05)
                return "0";
            return Num(shift) + " " + (shift < 0 ? DoorTypes.Left : DoorTypes.Right);
        }

        // The largest value of the range that is at or below the limit, to 0.1 mm, so that a typed value equal to the shown one is in.
        static double Down(double v)
        {
            return Math.Floor(v * 10 + 1e-6) / 10;
        }

        static double Up(double v)
        {
            return Math.Ceiling(v * 10 - 1e-6) / 10;
        }

        /// <summary>The smallest allowed shift as shown (rounded to 0.1 mm towards the inside of the range).</summary>
        public double ShownMin
        {
            get { return Up(MinShiftMm); }
        }

        /// <summary>The largest allowed shift as shown.</summary>
        public double ShownMax
        {
            get { return Down(MaxShiftMm); }
        }

        /// <summary>The side shown for the position in use (Left or Right; at zero the side the preset points to).</summary>
        public string PlacedSide(string whenZero)
        {
            return SideOf(PlacedShiftMm, whenZero);
        }

        /// <summary>The line shown under the field, for example "Allowed: 140 Left … 60 Right"; null when no position is allowed.</summary>
        public string AllowedText
        {
            get
            {
                if (RangeEmpty)
                    return null;
                return "Allowed: " + Text(ShownMin) + " … " + Text(ShownMax);
            }
        }

        /// <summary>The short field message when the typed value is outside the range, for example "out of range: max 140 Left".</summary>
        public string Message
        {
            get
            {
                if (!OutOfRange)
                    return null;
                return "out of range: " + Limit();
            }
        }

        // "max 140 Left" / "max 60 Right"; when the allowed range lies wholly on one side, "min 30 Right".
        string Limit()
        {
            if (RequestedShiftMm < MinShiftMm)
                return ShownMin <= 0 ? "max " + Text(ShownMin) : "min " + Text(ShownMin);
            return ShownMax >= 0 ? "max " + Text(ShownMax) : "min " + Text(ShownMax);
        }

        /// <summary>The single Design Notes line (reason and limit) for a typed value outside the range, or null.</summary>
        public string DesignNote
        {
            get
            {
                if (!OutOfRange)
                    return null;
                bool low = RequestedShiftMm < MinShiftMm;
                return TypeName + " door: Doors Offset " + Text(RequestedShiftMm) + " is out of range; the "
                    + (low ? (ShownMin <= 0 ? "maximum" : "minimum") : (ShownMax >= 0 ? "maximum" : "minimum"))
                    + " is " + Text(low ? ShownMin : ShownMax) + " (" + (low ? LeftReason : RightReason) + "). "
                    + "The drawings and the STEP model use " + Text(PlacedShiftMm) + ".";
            }
        }

        /// <summary>The line of the export warning for a typed value outside the range, or null.</summary>
        public string Warning
        {
            get
            {
                if (!OutOfRange)
                    return null;
                return "Doors Offset: " + Text(RequestedShiftMm) + " (" + Limit() + ")";
            }
        }

        /// <summary>
        /// The Doors Offset of a layout, or null for a Manual door or a size that is not standard. <paramref name="layout"/> must be the
        /// layout of <paramref name="door"/> on <paramref name="plan"/>.
        /// </summary>
        public static DoorOffset From(DoorInput door, DoorLayout layout, ElevatorParameters plan)
        {
            if (door == null || layout == null || !DoorTypes.IsAutomatic(layout.Type) || plan == null)
                return null;
            if (layout.Problems.Exists(p => p.Code == "opening-size"))
                return null;
            bool side = layout.Type == DoorTypes.Automatic2S;
            bool stackRight = layout.StackingSide == DoorTypes.Right;
            // Fixed side in the plan: left for 2C and when the stack is on the right.
            bool fixedLeft = !side || stackRight;
            double sw = plan.SW, o = layout.OpeningMm, g = layout.CarSideGapMm;
            double wc = layout.LandingWallClearanceMm, pocket = layout.LandingStackPocketMm;
            double reach = Math.Max(pocket, layout.PanelWidthMm);
            double loA, hiA;
            string loReason, hiReason;
            // Limits as the position x of the opening's fixed-side edge (2C: its left edge), from the fixed-side (left) wall.
            if (side)
            {
                double byReturn = g + layout.FixedReturnMinMm;
                loA = Math.Max(byReturn, layout.LandingFixedJambMinMm);
                loReason = byReturn >= layout.LandingFixedJambMinMm
                    ? "the {0} car return would fall below its minimum of " + Num(layout.FixedReturnMinMm) + " mm"
                    : "the {0} landing jamb would fall below its minimum of " + Num(layout.LandingFixedJambMinMm) + " mm";
                double byWall = sw - wc - reach - o, byStack = sw - g - layout.StackReturnMinMm - o;
                hiA = Math.Min(byWall, byStack);
                hiReason = byWall <= byStack
                    ? "the {1} landing frame would come closer than " + Num(wc) + " mm to the {1} wall"
                    : "the {1} car return would fall below its minimum of " + Num(layout.StackReturnMinMm) + " mm";
            }
            else
            {
                double byReturn = g + layout.FixedReturnMinMm, byBox = reach + wc;
                loA = Math.Max(byReturn, byBox);
                hiA = sw - o - loA;
                loReason = byReturn >= byBox
                    ? "the {0} car return would fall below its minimum of " + Num(layout.FixedReturnMinMm) + " mm"
                    : "the {0} landing door box would come closer than " + Num(wc) + " mm to the {0} wall";
                hiReason = byReturn >= byBox
                    ? "the {1} car return would fall below its minimum of " + Num(layout.FixedReturnMinMm) + " mm"
                    : "the {1} landing door box would come closer than " + Num(wc) + " mm to the {1} wall";
            }
            // To plan terms: shift of the opening centre from the shaft centre, positive to the right.
            double uLo = loA + o / 2 - sw / 2, uHi = hiA + o / 2 - sw / 2;
            var r = new DoorOffset { TypeName = DoorTypes.Display(layout.Type), RangeEmpty = hiA < loA - Eps };
            string fixedName = fixedLeft ? DoorTypes.Left : DoorTypes.Right;
            string otherName = fixedLeft ? DoorTypes.Right : DoorTypes.Left;
            if (fixedLeft)
            {
                r.MinShiftMm = uLo;
                r.MaxShiftMm = uHi;
                r.LeftReason = String.Format(CultureInfo.InvariantCulture, loReason, fixedName, otherName);
                r.RightReason = String.Format(CultureInfo.InvariantCulture, hiReason, fixedName, otherName);
            }
            else
            {
                r.MinShiftMm = 0.0 - uHi;
                r.MaxShiftMm = 0.0 - uLo;
                r.LeftReason = String.Format(CultureInfo.InvariantCulture, hiReason, fixedName, otherName);
                r.RightReason = String.Format(CultureInfo.InvariantCulture, loReason, fixedName, otherName);
            }
            var s = door.Settings;
            r.Locked = s.OffsetMm.HasValue || (side && s.OpeningOffsetMm.HasValue);
            // The requested shift: layout.PreferredOffsetMm is towards the fixed side.
            r.RequestedShiftMm = fixedLeft ? 0.0 - layout.PreferredOffsetMm : layout.PreferredOffsetMm;
            r.PlacedShiftMm = layout.CentreShiftMm;
            r.OutOfRange = r.Locked
                && !r.RangeEmpty
                && (r.RequestedShiftMm < r.MinShiftMm - Eps || r.RequestedShiftMm > r.MaxShiftMm + Eps);
            return r;
        }
    }
}
