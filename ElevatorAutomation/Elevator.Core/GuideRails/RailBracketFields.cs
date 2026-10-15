using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.Core.GuideRails
{
    /// <summary>
    /// The bracket of the car rail and of the counterweight rail (RAILS page, Advanced). Each value follows the bracket preset of
    /// the effective rail profile until the user edits it, which locks it; Reset returns it to the preset. Edits live in the project
    /// extension "railBrackets", so a project without edits is unchanged byte for byte.
    /// </summary>
    public static class RailBracketFields
    {
        const string ExtensionKey = "railBrackets";
        public const string Car = "Car";
        public const string Cw = "CW";

        sealed class Member
        {
            public string Name;
            public string Label;
            public Func<RailBracketPreset, double> Get;
        }

        static readonly Member[] Members =
        {
            new Member { Name = "AngleLegAMm", Label = "Angle leg on the wall (mm)", Get = b => b.AngleLegAMm },
            new Member { Name = "AngleLegBMm", Label = "Angle leg at the plate (mm)", Get = b => b.AngleLegBMm },
            new Member { Name = "AngleThicknessMm", Label = "Angle thickness (mm)", Get = b => b.AngleThicknessMm },
            new Member { Name = "AngleLengthMm", Label = "Bracket arm length (mm)", Get = b => b.AngleLengthMm },
            new Member { Name = "PlateThicknessMm", Label = "Bracket plate thickness (mm)", Get = b => b.PlateThicknessMm },
            new Member { Name = "AssemblyBoltDiameterMm", Label = "Assembly bolt diameter (mm)", Get = b => b.AssemblyBoltDiameterMm },
            new Member { Name = "AssemblyBoltLengthMm", Label = "Assembly bolt length (mm)", Get = b => b.AssemblyBoltLengthMm },
            new Member { Name = "ClipLengthMm", Label = "Clip length (mm)", Get = b => b.ClipLengthMm },
            new Member { Name = "ClipWidthMm", Label = "Clip width (mm)", Get = b => b.ClipWidthMm },
            new Member { Name = "ClipThicknessMm", Label = "Clip thickness (mm)", Get = b => b.ClipThicknessMm },
            new Member { Name = "ClipBoltDiameterMm", Label = "Rail bolt diameter (mm)", Get = b => b.ClipBoltDiameterMm },
            new Member { Name = "ClipBoltCount", Label = "Rail bolts per clip", Get = b => b.ClipBoltCount },
            new Member { Name = "AnchorDiameterMm", Label = "Anchor diameter (mm)", Get = b => b.AnchorDiameterMm },
            new Member { Name = "AnchorLengthMm", Label = "Anchor length (mm)", Get = b => b.AnchorLengthMm },
        };

        static string Key(string family, string member)
        {
            return "Rails.Bracket." + family + "." + member;
        }

        static IDictionary<string, object> Read(ElevatorProject p)
        {
            object raw;
            if (p == null || p.Extensions == null || !p.Extensions.TryGetValue(ExtensionKey, out raw))
                return null;
            return raw as IDictionary<string, object>;
        }

        static void Set(ElevatorProject p, string key, object value)
        {
            var d = Read(p);
            if (value == null)
            {
                if (d == null)
                    return;
                d.Remove(key);
                if (d.Count == 0)
                    p.Extensions.Remove(ExtensionKey);
                return;
            }
            if (d == null)
            {
                if (p.Extensions == null)
                    p.Extensions = new Dictionary<string, object>();
                d = new Dictionary<string, object>();
                p.Extensions[ExtensionKey] = d;
            }
            d[key] = value;
        }

        static RailProfile EffectiveProfile(PresetContext c, string family)
        {
            var field = ProjectPresets.Find(family == Car ? ProjectPresets.CarProfileKey : ProjectPresets.CwProfileKey);
            var name = field.Effective(c) as string;
            var catalog = c.Library.GuideRailsOrDefault();
            return name == null ? null : catalog.Profile(name);
        }

        static PresetField Create(string family, Member m)
        {
            string key = Key(family, m.Name);
            return new PresetField
            {
                Page = "RAILS",
                Key = key,
                Label = (family == Car ? "Car rail: " : "Counterweight rail: ") + m.Label,
                Preset = c =>
                {
                    var profile = EffectiveProfile(c, family);
                    return profile == null ? (object)null : m.Get(profile.BracketOrDefault());
                },
                Stored = p =>
                {
                    var d = Read(p);
                    object v;
                    return d != null && d.TryGetValue(key, out v) && v != null ? (object)Convert.ToDouble(v, CultureInfo.InvariantCulture) : null;
                },
                Store = (p, v) => Set(p, key, v == null ? null : (object)Convert.ToDouble(v, CultureInfo.InvariantCulture))
            };
        }

        /// <summary>Every bracket field: the car rail's, then the counterweight rail's.</summary>
        public static readonly PresetField[] All = new[] { Car, Cw }.SelectMany(f => Members.Select(m => Create(f, m))).ToArray();

        public static PresetField Find(string key)
        {
            return All.FirstOrDefault(f => f.Key == key);
        }

        public static PresetField[] Of(string family)
        {
            string prefix = "Rails.Bracket." + family + ".";
            return All.Where(f => f.Key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        }

        /// <summary>
        /// The bracket in effect for one rail family: the preset of the effective profile with the edited values. Null when the
        /// profile is not in the catalog.
        /// </summary>
        public static RailBracketPreset Effective(ElevatorProject project, PresetLibrary library, string family)
        {
            var c = new PresetContext { Project = project, Library = library };
            var profile = EffectiveProfile(c, family);
            if (profile == null)
                return null;
            var b = profile.BracketOrDefault().Clone();
            var type = typeof(RailBracketPreset);
            foreach (var f in Of(family))
            {
                if (!f.IsEdited(project))
                    continue;
                var v = f.Stored(project);
                if (v == null)
                    continue;
                var member = f.Key.Substring(f.Key.LastIndexOf('.') + 1);
                type.GetField(member).SetValue(b, Convert.ToDouble(v, CultureInfo.InvariantCulture));
            }
            return b;
        }

        /// <summary>True when any bracket value of the project is edited.</summary>
        public static bool AnyEdited(ElevatorProject project)
        {
            return All.Any(f => f.IsEdited(project));
        }
    }
}
