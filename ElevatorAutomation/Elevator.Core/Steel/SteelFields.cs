using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.Core.Steel
{
    /// <summary>
    /// The steel-shaft inputs of the project (DESIGN-FLOW "Steel shaft structure"): construction, profiles, beam spacing, plates, bolts,
    /// gusset, steel grade and the concrete base. Each follows its preset (SteelShaft in config/presets.json) until the user edits it,
    /// which locks it; Reset returns it to the preset. An edited value lives in the project extension "steel", so a project without
    /// one (every concrete project and every older file) is unchanged byte for byte. The beam-level edits live in the same extension.
    /// </summary>
    public static class SteelFields
    {
        public const string ConstructionKey = "Steel.Construction";
        public const string SystemKey = "Steel.System";
        public const string ColumnProfileKey = "Steel.ColumnProfile";
        public const string BeamProfileKey = "Steel.BeamProfile";
        public const string BracingProfileKey = "Steel.BracingProfile";
        public const string BracingKey = "Steel.Bracing";
        public const string PlateWidthKey = "Steel.BasePlateWidthMm";
        public const string PlateDepthKey = "Steel.BasePlateDepthMm";
        public const string PlateThicknessKey = "Steel.BasePlateThicknessMm";
        public const string BoltCountKey = "Steel.BoltCount";
        public const string BoltDiameterKey = "Steel.BoltDiameterMm";
        public const string GussetLengthKey = "Steel.GussetLengthMm";
        public const string GussetWidthKey = "Steel.GussetWidthMm";
        public const string GussetThicknessKey = "Steel.GussetThicknessMm";
        public const string SteelGradeKey = "Steel.SteelGrade";
        public const string BaseMarginKey = "Steel.Base.MarginMm";
        public const string BaseThicknessKey = "Steel.Base.ThicknessMm";
        public const string BarDiameterKey = "Steel.Base.BarDiameterMm";
        public const string BarCountAlongXKey = "Steel.Base.BarCountAlongX";
        public const string BarCountAlongYKey = "Steel.Base.BarCountAlongY";
        public const string SlabEdgeDepthKey = "Steel.SlabEdgeDepthMm";
        public const string SlabEdgeWidthKey = "Steel.SlabEdgeWidthMm";
        public const string HousingHeightKey = "Steel.HousingHeightMm";
        public const string HousingOverhangKey = "Steel.HousingOverhangMm";

        const string ExtensionKey = "steel";
        const string LevelEditsMember = "levelEdits";
        const string LevelRemovedMember = "levelRemoved";

        // ---------- storage ----------
        static IDictionary<string, object> Read(ElevatorProject p)
        {
            object raw;
            if (p == null || p.Extensions == null || !p.Extensions.TryGetValue(ExtensionKey, out raw))
                return null;
            return raw as IDictionary<string, object>;
        }

        static object Get(ElevatorProject p, string member)
        {
            var d = Read(p);
            object v;
            return d != null && d.TryGetValue(member, out v) ? v : null;
        }

        static IDictionary<string, object> Write(ElevatorProject p)
        {
            if (p.Extensions == null)
                p.Extensions = new Dictionary<string, object>();
            var d = Read(p);
            if (d == null)
            {
                d = new Dictionary<string, object>();
                p.Extensions[ExtensionKey] = d;
            }
            return d;
        }

        static void Set(ElevatorProject p, string member, object value)
        {
            if (value == null)
            {
                var d = Read(p);
                if (d == null)
                    return;
                d.Remove(member);
                if (d.Count == 0)
                    p.Extensions.Remove(ExtensionKey);
                return;
            }
            Write(p)[member] = value;
        }

        static double ToDouble(object v)
        {
            return Convert.ToDouble(v, CultureInfo.InvariantCulture);
        }

        // ---------- the preset fields ----------
        static PresetField Text(string member, string label, Func<SteelShaftPreset, string> preset)
        {
            return new PresetField
            {
                Page = "SHAFT",
                Key = "Steel." + member,
                Label = label,
                Preset = c => preset(c.Library.SteelShaftOrDefault()),
                Stored = p => Get(p, member) as string,
                Store = (p, v) => Set(p, member, v as string)
            };
        }

        static PresetField Number(string member, string label, Func<PresetContext, object> preset)
        {
            return new PresetField
            {
                Page = "SHAFT",
                Key = "Steel." + member,
                Label = label,
                Preset = preset,
                Stored = p =>
                {
                    var v = Get(p, member);
                    return v == null ? (object)null : ToDouble(v);
                },
                Store = (p, v) => Set(p, member, v == null ? null : (object)ToDouble(v))
            };
        }

        static PresetField Lib(string member, string label, Func<SteelShaftPreset, double> preset)
        {
            return Number(member, label, c => (object)preset(c.Library.SteelShaftOrDefault()));
        }

        public static readonly PresetField Construction = Text("Construction", "Shaft construction", t => t.Construction);
        public static readonly PresetField System = Text("System", "Steel system", t => t.System);
        public static readonly PresetField ColumnProfile = Text("ColumnProfile", "Column profile", t => t.ColumnProfile);
        public static readonly PresetField BeamProfile = Text("BeamProfile", "Beam profile", t => t.BeamProfile);
        public static readonly PresetField BracingProfile = Text("BracingProfile", "Bracing profile", t => t.BracingProfile);
        public static readonly PresetField Bracing = Text("Bracing", "Bracing", t => t.Bracing);
        public static readonly PresetField PlateWidth = Lib("BasePlateWidthMm", "Base plate width (mm)", t => t.BasePlateWidthMm);
        public static readonly PresetField PlateDepth = Lib("BasePlateDepthMm", "Base plate depth (mm)", t => t.BasePlateDepthMm);
        public static readonly PresetField PlateThickness = Lib("BasePlateThicknessMm", "Base plate thickness (mm)", t => t.BasePlateThicknessMm);
        public static readonly PresetField BoltCount = Lib("BoltCount", "Bolts per base plate", t => t.BoltCount);
        public static readonly PresetField BoltDiameter = Lib("BoltDiameterMm", "Bolt diameter (mm)", t => t.BoltDiameterMm);
        public static readonly PresetField GussetLength = Lib("GussetLengthMm", "Gusset plate length (mm)", t => t.GussetLengthMm);
        public static readonly PresetField GussetWidth = Lib("GussetWidthMm", "Gusset plate width (mm)", t => t.GussetWidthMm);
        public static readonly PresetField GussetThickness = Lib("GussetThicknessMm", "Gusset plate thickness (mm)", t => t.GussetThicknessMm);
        public static readonly PresetField SteelGrade = Text("SteelGrade", "Steel grade", t => t.SteelGrade);
        public static readonly PresetField BaseMargin = Lib("Base.MarginMm", "Concrete base margin (mm)", t => t.Base.MarginMm);
        public static readonly PresetField BaseThickness = Lib("Base.ThicknessMm", "Concrete base thickness (mm)", t => t.Base.ThicknessMm);
        public static readonly PresetField BarDiameter = Lib("Base.BarDiameterMm", "Base bar diameter (mm)", t => t.Base.BarDiameterMm);
        // The bar counts follow the block size at the preset spacing until typed.
        public static readonly PresetField BarCountAlongX = Number("Base.BarCountAlongX", "Base bars along the width", c => SteelStructure.PresetBarCount(c, true));
        public static readonly PresetField BarCountAlongY = Number("Base.BarCountAlongY", "Base bars along the depth", c => SteelStructure.PresetBarCount(c, false));

        // The building slab edge in front of the entrance (its width follows the shaft width until typed) and the machine housing.
        public static readonly PresetField SlabEdgeDepth = Lib("SlabEdgeDepthMm", "Building slab edge depth (mm)", t => t.SlabEdgeDepthMm);
        public static readonly PresetField SlabEdgeWidth = Number("SlabEdgeWidthMm", "Building slab edge width (mm)", c =>
        {
            var shaft = c.Project == null ? null : c.Project.Shaft;
            return shaft == null ? (object)null : shaft.WidthMm + 2 * c.Library.SteelShaftOrDefault().SlabEdgeSideMm;
        });
        public static readonly PresetField HousingHeight = Lib("HousingHeightMm", "Machine housing height (mm)", t => t.HousingHeightMm);
        public static readonly PresetField HousingOverhang = Lib("HousingOverhangMm", "Housing roof overhang (mm)", t => t.HousingOverhangMm);

        /// <summary>Every steel field, in the order of the page.</summary>
        public static readonly PresetField[] All =
        {
            Construction, System, ColumnProfile, BeamProfile, BracingProfile, Bracing, PlateWidth, PlateDepth, PlateThickness,
            BoltCount, BoltDiameter, GussetLength, GussetWidth, GussetThickness, SteelGrade, BaseMargin, BaseThickness, BarDiameter,
            BarCountAlongX, BarCountAlongY, SlabEdgeDepth, SlabEdgeWidth, HousingHeight, HousingOverhang
        };

        /// <summary>The field with a key, or null.</summary>
        public static PresetField Find(string key)
        {
            return All.FirstOrDefault(f => f.Key == key);
        }

        /// <summary>The value in effect (the edited value when locked, otherwise the preset) as text; null when none.</summary>
        public static string Text_(PresetField field, ElevatorProject project, PresetLibrary library)
        {
            return field.Effective(new PresetContext { Project = project, Library = library }) as string;
        }

        /// <summary>True when the project is a steel structure: the effective construction is Steel. Never throws.</summary>
        public static bool IsSteel(ElevatorProject project, PresetLibrary library)
        {
            if (project == null || library == null)
                return false;
            return Text_(Construction, project, library) == SteelShaftPreset.SteelStructure;
        }

        // ---------- the beam levels the user edited (per face) ----------
        /// <summary>The faces of the tower; the level edits are kept per face.</summary>
        public static readonly string[] Faces = { "Front", "Back", "Left", "Right" };

        static string FaceKey(string face, string key)
        {
            return face + "|" + key;
        }

        static bool OfFace(string stored, string face, out string key)
        {
            string prefix = face + "|";
            key = null;
            if (stored == null || !stored.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            key = stored.Substring(prefix.Length);
            return true;
        }

        /// <summary>The user's level edits of one face (locked levels), in storage order; empty when the rule is untouched.</summary>
        public static List<SteelLevelEdit> LevelEdits(ElevatorProject p, string face)
        {
            var list = new List<SteelLevelEdit>();
            string key;
            var edits = Get(p, LevelEditsMember) as IDictionary<string, object>;
            if (edits != null)
            {
                foreach (var kv in edits)
                {
                    if (kv.Value != null && OfFace(kv.Key, face, out key))
                        list.Add(new SteelLevelEdit { Key = key, ElevationMm = ToDouble(kv.Value) });
                }
            }
            var removed = Get(p, LevelRemovedMember) as IEnumerable;
            if (removed != null && !(removed is string))
            {
                foreach (var item in removed)
                {
                    var s = item as string;
                    if (s != null && OfFace(s, face, out key))
                        list.Add(new SteelLevelEdit { Key = key, Removed = true });
                }
            }
            return list;
        }

        /// <summary>Moves a level of a face (or adds one with key C:n) and locks it.</summary>
        public static void EditLevel(ElevatorProject p, string face, string key, double elevationMm)
        {
            if (String.IsNullOrEmpty(face) || String.IsNullOrEmpty(key) || Double.IsNaN(elevationMm) || Double.IsInfinity(elevationMm))
                throw new ArgumentException("A face, a level key and a finite elevation are required.");
            var edits = Get(p, LevelEditsMember) as IDictionary<string, object>;
            if (edits == null)
            {
                edits = new Dictionary<string, object>();
                Write(p)[LevelEditsMember] = edits;
            }
            string stored = FaceKey(face, key);
            edits[stored] = elevationMm;
            var removed = Get(p, LevelRemovedMember) as IList;
            if (removed != null)
            {
                while (removed.Contains(stored))
                    removed.Remove(stored);
            }
        }

        /// <summary>Removes a level of a face and locks the removal.</summary>
        public static void RemoveLevel(ElevatorProject p, string face, string key)
        {
            if (String.IsNullOrEmpty(face) || String.IsNullOrEmpty(key))
                throw new ArgumentException("A face and a level key are required.");
            string stored = FaceKey(face, key);
            var edits = Get(p, LevelEditsMember) as IDictionary<string, object>;
            if (edits != null)
                edits.Remove(stored);
            var removed = Get(p, LevelRemovedMember) as IList;
            if (removed == null)
            {
                removed = new List<object>();
                Write(p)[LevelRemovedMember] = removed;
            }
            if (!removed.Contains(stored))
                removed.Add(stored);
        }

        /// <summary>Gives one level of a face back to the rule.</summary>
        public static void ResetLevel(ElevatorProject p, string face, string key)
        {
            string stored = FaceKey(face, key);
            var edits = Get(p, LevelEditsMember) as IDictionary<string, object>;
            if (edits != null)
            {
                edits.Remove(stored);
                if (edits.Count == 0)
                    Set(p, LevelEditsMember, null);
            }
            var removed = Get(p, LevelRemovedMember) as IList;
            if (removed != null)
            {
                while (removed.Contains(stored))
                    removed.Remove(stored);
                if (removed.Count == 0)
                    Set(p, LevelRemovedMember, null);
            }
        }

        /// <summary>Reset for the whole list of one face (or of every face when face is null): the levels follow the rule again.</summary>
        public static void ResetLevels(ElevatorProject p, string face = null)
        {
            if (face == null)
            {
                Set(p, LevelEditsMember, null);
                Set(p, LevelRemovedMember, null);
                return;
            }
            foreach (var e in LevelEdits(p, face))
                ResetLevel(p, face, e.Key);
        }

        /// <summary>True when any level of a face (any face when null) is locked by an edit.</summary>
        public static bool LevelsEdited(ElevatorProject p, string face = null)
        {
            if (face != null)
                return LevelEdits(p, face).Count > 0;
            return Faces.Any(f => LevelEdits(p, f).Count > 0);
        }
    }
}