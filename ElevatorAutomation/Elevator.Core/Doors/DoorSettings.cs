using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Project;

namespace Elevator.Core.Doors
{
    /// <summary>
    /// The door values the user has edited (locked). Every other door value follows the catalog preset. They live in the project
    /// extension "doors", so a project without them (every Manual project, every older file) is unchanged byte for byte; the lock of
    /// each key is the key's entry in UserEditedFields, as for every other preset field.
    /// </summary>
    public sealed class DoorSettings
    {
        /// <summary>Key of the clear opening (blank: automatic).</summary>
        public const string OpeningKey = "Doors.OpeningMm";
        /// <summary>Key of the handing (2S).</summary>
        public const string HandingKey = "Doors.Handing";
        /// <summary>Key of the car door zone.</summary>
        public const string CarZoneKey = "Doors.CarZoneMm";
        /// <summary>Key of the sill gap.</summary>
        public const string SillGapKey = "Doors.SillGapMm";
        /// <summary>Key of the landing door zone.</summary>
        public const string LandingZoneKey = "Doors.LandingZoneMm";
        /// <summary>Key of the car return, fixed side.</summary>
        public const string FixedReturnKey = "Doors.FixedReturnMm";
        /// <summary>Key of the car return, stacking side.</summary>
        public const string StackReturnKey = "Doors.StackReturnMm";
        /// <summary>
        /// Key of the opening offset of earlier project files: the distance of the opening centre towards the fixed side (2S only).
        /// Still read, with the same geometric result; a new Doors Offset replaces it.
        /// </summary>
        public const string LegacyOffsetKey = "Doors.OpeningOffsetMm";
        /// <summary>Key of the Doors Offset: the distance in mm from the shaft centre line to the opening centre line.</summary>
        public const string OffsetKey = "Doors.OffsetMm";
        /// <summary>Key of the Doors Offset side, Left or Right as seen in the plan drawing. Locked together with <see cref="OffsetKey"/>.</summary>
        public const string OffsetSideKey = "Doors.OffsetSide";
        /// <summary>Key of the landing jamb, fixed side.</summary>
        public const string LandingJambKey = "Doors.LandingFixedJambMm";
        /// <summary>Key of the landing stacking pocket.</summary>
        public const string LandingPocketKey = "Doors.LandingStackPocketMm";
        /// <summary>Key of the minimum landing frame distance to the shaft wall.</summary>
        public const string WallClearanceKey = "Doors.LandingWallClearanceMm";
        /// <summary>Key of the minimum car return, fixed side (2C: each side).</summary>
        public const string MinFixedReturnKey = "Doors.MinFixedReturnMm";
        /// <summary>Key of the minimum car return, stacking side.</summary>
        public const string MinStackReturnKey = "Doors.MinStackReturnMm";
        /// <summary>Key of the minimum landing jamb, fixed side.</summary>
        public const string LandingJambMinKey = "Doors.LandingFixedJambMinMm";

        /// <summary>Every door key, in page order.</summary>
        public static readonly string[] Keys =
        {
            OpeningKey, HandingKey, CarZoneKey, SillGapKey, LandingZoneKey, FixedReturnKey, StackReturnKey, LegacyOffsetKey, OffsetKey, OffsetSideKey,
            LandingJambKey, LandingPocketKey, WallClearanceKey, MinFixedReturnKey, MinStackReturnKey, LandingJambMinKey
        };

        const string ExtensionKey = "doors";

        public double? OpeningMm;
        public string Handing;
        public double? CarZoneMm;
        public double? SillGapMm;
        public double? LandingZoneMm;
        public double? FixedReturnMm;
        public double? StackReturnMm;
        /// <summary>The opening offset of an earlier file, towards the fixed side (2S).</summary>
        public double? OpeningOffsetMm;
        /// <summary>The Doors Offset distance (never negative); with <see cref="OffsetSide"/>.</summary>
        public double? OffsetMm;
        /// <summary>The Doors Offset side, Left or Right in the plan drawing.</summary>
        public string OffsetSide;
        public double? LandingFixedJambMm;
        public double? LandingStackPocketMm;
        public double? LandingWallClearanceMm;
        public double? MinFixedReturnMm;
        public double? MinStackReturnMm;
        public double? LandingFixedJambMinMm;

        /// <summary>The locked numeric value of a key (null: unlocked).</summary>
        public double? Number(string key)
        {
            switch (key)
            {
                case OpeningKey:
                    return OpeningMm;
                case CarZoneKey:
                    return CarZoneMm;
                case SillGapKey:
                    return SillGapMm;
                case LandingZoneKey:
                    return LandingZoneMm;
                case FixedReturnKey:
                    return FixedReturnMm;
                case StackReturnKey:
                    return StackReturnMm;
                case LegacyOffsetKey:
                    return OpeningOffsetMm;
                case OffsetKey:
                    return OffsetMm;
                case LandingJambKey:
                    return LandingFixedJambMm;
                case LandingPocketKey:
                    return LandingStackPocketMm;
                case WallClearanceKey:
                    return LandingWallClearanceMm;
                case MinFixedReturnKey:
                    return MinFixedReturnMm;
                case MinStackReturnKey:
                    return MinStackReturnMm;
                case LandingJambMinKey:
                    return LandingFixedJambMinMm;
                default:
                    return null;
            }
        }

        /// <summary>Sets the numeric value of a key (null clears it).</summary>
        public void SetNumber(string key, double? v)
        {
            switch (key)
            {
                case OpeningKey:
                    OpeningMm = v;
                    break;
                case CarZoneKey:
                    CarZoneMm = v;
                    break;
                case SillGapKey:
                    SillGapMm = v;
                    break;
                case LandingZoneKey:
                    LandingZoneMm = v;
                    break;
                case FixedReturnKey:
                    FixedReturnMm = v;
                    break;
                case StackReturnKey:
                    StackReturnMm = v;
                    break;
                case LegacyOffsetKey:
                    OpeningOffsetMm = v;
                    break;
                case OffsetKey:
                    OffsetMm = v;
                    break;
                case LandingJambKey:
                    LandingFixedJambMm = v;
                    break;
                case LandingPocketKey:
                    LandingStackPocketMm = v;
                    break;
                case WallClearanceKey:
                    LandingWallClearanceMm = v;
                    break;
                case MinFixedReturnKey:
                    MinFixedReturnMm = v;
                    break;
                case MinStackReturnKey:
                    MinStackReturnMm = v;
                    break;
                case LandingJambMinKey:
                    LandingFixedJambMinMm = v;
                    break;
            }
        }

        /// <summary>True when nothing is locked.</summary>
        public bool IsEmpty
        {
            get
            {
                return String.IsNullOrEmpty(Handing)
                    && String.IsNullOrEmpty(OffsetSide)
                    && Keys.All(k => k == HandingKey || k == OffsetSideKey || !Number(k).HasValue);
            }
        }

        /// <summary>
        /// The members that enter an input fingerprint: the members of the first door version always (null when not locked), the
        /// Doors Offset only when locked, so a project without one keeps its earlier fingerprint.
        /// </summary>
        internal Dictionary<string, object> ForFingerprint()
        {
            var d = new Dictionary<string, object>();
            // Every member of the first door version is present, null when not locked, with the read-only IsEmpty that the
            // serializer wrote with them.
            Action<string, object> put = (k, v) => d[k] = v;
            put("OpeningMm", OpeningMm);
            put("Handing", Handing);
            put("CarZoneMm", CarZoneMm);
            put("SillGapMm", SillGapMm);
            put("LandingZoneMm", LandingZoneMm);
            put("FixedReturnMm", FixedReturnMm);
            put("StackReturnMm", StackReturnMm);
            put("OpeningOffsetMm", OpeningOffsetMm);
            put("LandingFixedJambMm", LandingFixedJambMm);
            put("LandingStackPocketMm", LandingStackPocketMm);
            put("LandingWallClearanceMm", LandingWallClearanceMm);
            put("MinFixedReturnMm", MinFixedReturnMm);
            put("MinStackReturnMm", MinStackReturnMm);
            put("LandingFixedJambMinMm", LandingFixedJambMinMm);
            put("IsEmpty", IsEmpty);
            if (OffsetMm.HasValue)
            {
                d["OffsetMm"] = OffsetMm;
                d["OffsetSide"] = OffsetSide;
            }
            return d;
        }

        static bool Locked(ElevatorProject p, string key)
        {
            return p.UserEditedFields != null && p.UserEditedFields.Contains(key);
        }

        static double? ToNumber(object v)
        {
            if (v == null)
                return null;
            try
            {
                double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                return Double.IsNaN(d) || Double.IsInfinity(d) ? (double?)null : d;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (InvalidCastException)
            {
                return null;
            }
        }

        /// <summary>The locked door values of a project (never null).</summary>
        public static DoorSettings Read(ElevatorProject project)
        {
            var s = new DoorSettings();
            if (project == null || project.Extensions == null || !project.Extensions.ContainsKey(ExtensionKey))
                return s;
            var raw = project.Extensions[ExtensionKey] as IDictionary<string, object>;
            if (raw == null)
                return s;
            foreach (string key in Keys)
            {
                object v;
                if (!raw.TryGetValue(key, out v) || v == null || !Locked(project, key))
                    continue;
                if (key == HandingKey || key == OffsetSideKey)
                {
                    string h = v as string;
                    if (h == DoorTypes.Left || h == DoorTypes.Right)
                    {
                        if (key == HandingKey)
                            s.Handing = h;
                        else
                            s.OffsetSide = h;
                    }
                }
                else
                    s.SetNumber(key, ToNumber(v));
            }
            return s;
        }

        /// <summary>Stores the locked values; nothing is stored (and any earlier entry removed) when no value is locked.</summary>
        public void Write(ElevatorProject project)
        {
            if (project.Extensions == null)
                project.Extensions = new Dictionary<string, object>();
            var d = new Dictionary<string, object>();
            foreach (string key in Keys)
            {
                if (key == HandingKey || key == OffsetSideKey)
                {
                    string h = key == HandingKey ? Handing : OffsetSide;
                    if (h == DoorTypes.Left || h == DoorTypes.Right)
                        d[key] = h;
                }
                else if (Number(key).HasValue)
                    d[key] = Number(key).Value;
            }
            if (d.Count == 0)
                project.Extensions.Remove(ExtensionKey);
            else
                project.Extensions[ExtensionKey] = d;
        }
    }
}
