using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Project;

namespace Elevator.Core.Doors
{
    /// <summary>
    /// Lock, follow and Reset of the door inputs (DESIGN-FLOW principle 4). The door type, the clear opening, the handing and every
    /// clearance start from a catalog preset; a value the user edits is locked and the others follow the shaft and the type.
    /// UI-free: the application refreshes its editors after these calls.
    /// </summary>
    public static class DoorFlow
    {
        /// <summary>Label of a door key, as shown next to its editor.</summary>
        public static string Label(string key)
        {
            switch (key)
            {
                case DoorSettings.OpeningKey:
                    return "Clear opening (mm)";
                case DoorSettings.HandingKey:
                    return "Handing";
                case DoorSettings.CarZoneKey:
                    return "Car door zone (mm)";
                case DoorSettings.SillGapKey:
                    return "Sill gap (mm)";
                case DoorSettings.LandingZoneKey:
                    return "Landing door zone (mm)";
                case DoorSettings.FixedReturnKey:
                    return "Car return, fixed side, preferred (mm)";
                case DoorSettings.StackReturnKey:
                    return "Car return, stacking side, preferred (mm)";
                case DoorSettings.OffsetKey:
                    return "Opening offset, preferred (mm)";
                case DoorSettings.LandingJambKey:
                    return "Landing jamb, fixed side, preferred (mm)";
                case DoorSettings.LandingPocketKey:
                    return "Landing stacking pocket (mm)";
                case DoorSettings.WallClearanceKey:
                    return "Landing frame to wall, minimum (mm)";
                case DoorSettings.MinFixedReturnKey:
                    return "Car return, fixed side, minimum (mm)";
                case DoorSettings.MinStackReturnKey:
                    return "Car return, stacking side, minimum (mm)";
                case DoorSettings.LandingJambMinKey:
                    return "Landing jamb, fixed side, minimum (mm)";
                default:
                    return key;
            }
        }

        /// <summary>True when the key is locked (edited by the user).</summary>
        public static bool IsLocked(ElevatorProject p, string key)
        {
            return p != null && p.UserEditedFields != null && p.UserEditedFields.Contains(key);
        }

        static void Lock(ElevatorProject p, string key)
        {
            if (p.UserEditedFields == null)
                p.UserEditedFields = new List<string>();
            if (!p.UserEditedFields.Contains(key))
                p.UserEditedFields.Add(key);
        }

        static void Unlock(ElevatorProject p, string key)
        {
            if (p.UserEditedFields == null)
                return;
            p.UserEditedFields.Remove(key);
            if (p.UserEditedFields.Count == 0)
                p.UserEditedFields = null;
        }

        /// <summary>Sets the door type. Manual stores no type (the project is then as a project without automatic doors).</summary>
        public static void SetType(ElevatorProject p, string type)
        {
            string t = DoorTypes.Normalize(type);
            if (p.Doors == null)
                p.Doors = new DoorConfiguration();
            p.Doors.Type = t == DoorTypes.Manual ? null : t;
        }

        /// <summary>The door type of a project: Manual, Automatic2S or Automatic2C.</summary>
        public static string TypeOf(ElevatorProject p)
        {
            return p == null || p.Doors == null ? DoorTypes.Manual : DoorTypes.Normalize(p.Doors.Type);
        }

        /// <summary>Locks a numeric door value; null resets it to its preset.</summary>
        public static void SetNumber(ElevatorProject p, string key, double? value)
        {
            if (!value.HasValue)
            {
                Reset(p, key);
                return;
            }
            Lock(p, key);
            var s = DoorSettings.Read(p);
            s.SetNumber(key, value);
            s.Write(p);
        }

        /// <summary>Locks the handing (Left or Right); Auto resets it.</summary>
        public static void SetHanding(ElevatorProject p, string handing)
        {
            if (handing != DoorTypes.Left && handing != DoorTypes.Right)
            {
                Reset(p, DoorSettings.HandingKey);
                return;
            }
            Lock(p, DoorSettings.HandingKey);
            var s = DoorSettings.Read(p);
            s.Handing = handing;
            s.Write(p);
        }

        /// <summary>Reset to preset: the value unlocks and follows again.</summary>
        public static void Reset(ElevatorProject p, string key)
        {
            Unlock(p, key);
            var s = DoorSettings.Read(p);
            if (key == DoorSettings.HandingKey)
                s.Handing = null;
            else
                s.SetNumber(key, null);
            s.Write(p);
        }

        /// <summary>The locked door keys of a project.</summary>
        public static List<string> LockedKeys(ElevatorProject p)
        {
            return DoorSettings.Keys.Where(k => IsLocked(p, k)).ToList();
        }

        /// <summary>The value in effect for a numeric key in a layout (null for the handing and for keys the type does not use).</summary>
        public static double? Effective(DoorLayout l, string key)
        {
            if (l == null || l.Type == DoorTypes.Manual)
                return null;
            bool side = l.Type == DoorTypes.Automatic2S;
            switch (key)
            {
                case DoorSettings.OpeningKey:
                    return l.OpeningMm;
                case DoorSettings.CarZoneKey:
                    return l.CarZoneMm;
                case DoorSettings.SillGapKey:
                    return l.SillGapMm;
                case DoorSettings.LandingZoneKey:
                    return l.LandingZoneMm;
                case DoorSettings.FixedReturnKey:
                    return l.FixedReturnMm;
                case DoorSettings.StackReturnKey:
                    return side ? l.StackReturnMm : (double?)null;
                case DoorSettings.OffsetKey:
                    return side ? l.PreferredOffsetMm : (double?)null;
                case DoorSettings.LandingJambKey:
                    return side ? l.LandingFixedJambPreferredMm : (double?)null;
                case DoorSettings.MinFixedReturnKey:
                    return l.FixedReturnMinMm;
                case DoorSettings.MinStackReturnKey:
                    return side ? l.StackReturnMinMm : (double?)null;
                case DoorSettings.LandingJambMinKey:
                    return side ? l.LandingFixedJambMinMm : (double?)null;
                case DoorSettings.LandingPocketKey:
                    return l.LandingStackPocketMm;
                case DoorSettings.WallClearanceKey:
                    return l.LandingWallClearanceMm;
                default:
                    return null;
            }
        }

        // A needed width rounded up to a whole millimetre.
        static string Up(double v)
        {
            return Math.Ceiling(v - 1e-9).ToString("0", CultureInfo.InvariantCulture);
        }

        /// <summary>The short field message "does not fit: …" of a layout, or null when it fits.</summary>
        public static string FitMessage(DoorLayout l)
        {
            if (l == null || l.Fits || l.Problems.Count == 0)
                return null;
            // One needed car width and shaft width for all width problems, then the other reasons.
            var parts = new List<string>();
            if (l.NeededCarWidthMm.HasValue || l.NeededShaftWidthMm.HasValue)
            {
                var widths = new List<string>();
                if (l.NeededCarWidthMm.HasValue)
                    widths.Add("car width " + Up(l.NeededCarWidthMm.Value) + " mm");
                if (l.NeededShaftWidthMm.HasValue)
                    widths.Add("shaft width " + Up(l.NeededShaftWidthMm.Value) + " mm");
                parts.Add("needs " + String.Join(", ", widths));
            }
            parts.AddRange(l.Problems.Where(p => !p.NeededCarWidthMm.HasValue && !p.NeededShaftWidthMm.HasValue).Select(p => p.Message));
            return "does not fit: " + String.Join("; ", parts);
        }

        /// <summary>The Design Notes advice for a door that does not fit, or null: one line with the needed car or shaft width.</summary>
        public static string DesignNote(DoorLayout l)
        {
            if (l == null || l.Fits || l.Problems.Count == 0)
                return null;
            string size = Math.Round(l.OpeningMm, 1).ToString("0.#", CultureInfo.InvariantCulture);
            string name = l.AutoSelected
                ? DoorTypes.Display(l.Type) + " door: no standard clear opening fits, the smallest (" + size + " mm)"
                : DoorTypes.Display(l.Type) + " door, " + size + " mm clear opening";
            string advice;
            if (l.NeededCarWidthMm.HasValue || l.NeededShaftWidthMm.HasValue)
            {
                var parts = new List<string>();
                if (l.NeededCarWidthMm.HasValue)
                {
                    parts.Add("a car width of at least " + Up(l.NeededCarWidthMm.Value) + " mm");
                }
                if (l.NeededShaftWidthMm.HasValue)
                {
                    parts.Add("a shaft width of at least " + Up(l.NeededShaftWidthMm.Value) + " mm");
                }
                advice = "needs " + String.Join(" and ", parts) + ".";
                var others = l.Problems.Where(p => !p.NeededCarWidthMm.HasValue && !p.NeededShaftWidthMm.HasValue).ToList();
                if (others.Count > 0)
                    advice += " Also: " + String.Join("; ", others.Select(p => p.Message)) + ".";
            }
            else
                advice = String.Join("; ", l.Problems.Select(p => p.Message)) + ".";
            return name + (l.AutoSelected ? " " : " does not fit: ") + advice;
        }
    }
}
