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
        /// <summary>Key of the 2S opening offset.</summary>
        public const string OffsetKey = "Doors.OpeningOffsetMm";
        /// <summary>Key of the landing jamb, fixed side.</summary>
        public const string LandingJambKey = "Doors.LandingFixedJambMm";
        /// <summary>Key of the landing stacking pocket.</summary>
        public const string LandingPocketKey = "Doors.LandingStackPocketMm";
        /// <summary>Key of the landing frame clearance to the side walls.</summary>
        public const string WallClearanceKey = "Doors.LandingWallClearanceMm";

        /// <summary>Every door key, in page order.</summary>
        public static readonly string[] Keys =
        {
            OpeningKey, HandingKey, CarZoneKey, SillGapKey, LandingZoneKey, FixedReturnKey, StackReturnKey, OffsetKey,
            LandingJambKey, LandingPocketKey, WallClearanceKey
        };

        const string ExtensionKey = "doors";

        public double? OpeningMm;
        public string Handing;
        public double? CarZoneMm;
        public double? SillGapMm;
        public double? LandingZoneMm;
        public double? FixedReturnMm;
        public double? StackReturnMm;
        public double? OpeningOffsetMm;
        public double? LandingFixedJambMm;
        public double? LandingStackPocketMm;
        public double? LandingWallClearanceMm;

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
                case OffsetKey:
                    return OpeningOffsetMm;
                case LandingJambKey:
                    return LandingFixedJambMm;
                case LandingPocketKey:
                    return LandingStackPocketMm;
                case WallClearanceKey:
                    return LandingWallClearanceMm;
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
                case OffsetKey:
                    OpeningOffsetMm = v;
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
            }
        }

        /// <summary>True when nothing is locked.</summary>
        public bool IsEmpty
        {
            get
            {
                return String.IsNullOrEmpty(Handing) && Keys.All(k => k == HandingKey || !Number(k).HasValue);
            }
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
                if (key == HandingKey)
                {
                    string h = v as string;
                    if (h == DoorTypes.Left || h == DoorTypes.Right)
                        s.Handing = h;
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
                if (key == HandingKey)
                {
                    if (Handing == DoorTypes.Left || Handing == DoorTypes.Right)
                        d[key] = Handing;
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
