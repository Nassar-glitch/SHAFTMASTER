using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.Core.Layouts
{
    /// <summary>
    /// The title-block values a project can edit: control and supply. Each follows its preset (TitleBlock in config/presets.json) until
    /// the user edits it, which locks it; Reset returns it to the preset. An edited value lives in the project extension "titleBlock",
    /// so a project without one (every older file) is unchanged byte for byte. Never a calculation or drawing-geometry input.
    /// </summary>
    public static class TitleBlockFields
    {
        /// <summary>Key of the control field.</summary>
        public const string ControlKey = "TitleBlock.Control";
        /// <summary>Key of the supply field.</summary>
        public const string SupplyKey = "TitleBlock.Supply";

        const string ExtensionKey = "titleBlock";

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
            return d != null && d.TryGetValue(member, out v) ? v as string : null;
        }

        static void Set(ElevatorProject p, string member, object value)
        {
            string text = value as string;
            var d = Read(p);
            if (text == null)
            {
                if (d == null)
                    return;
                d.Remove(member);
                if (d.Count == 0)
                    p.Extensions.Remove(ExtensionKey);
                return;
            }
            if (p.Extensions == null)
                p.Extensions = new Dictionary<string, object>();
            if (d == null)
            {
                d = new Dictionary<string, object>();
                p.Extensions[ExtensionKey] = d;
            }
            d[member] = text;
        }

        static PresetField Text(string member, string label, Func<TitleBlockPreset, string> preset)
        {
            return new PresetField
            {
                Page = "GENERAL",
                Key = "TitleBlock." + member,
                Label = label,
                Preset = c => preset(c.Library.TitleBlockOrDefault()),
                Stored = p => Get(p, member),
                Store = (p, v) => Set(p, member, v)
            };
        }

        /// <summary>The control text, for example "SIMPLEX COLLECTIVE DOWN.".</summary>
        public static readonly PresetField Control = Text("Control", "Control", t => t.Control);

        /// <summary>The supply text, for example "3 PHASE 380 V. 50 Hz.".</summary>
        public static readonly PresetField Supply = Text("Supply", "Supply", t => t.Supply);

        /// <summary>The type of work, for example "أعمال مصاعد".</summary>
        public static readonly PresetField TypeOfWork = Text("TypeOfWork", "Type of work", t => t.TypeOfWorkOrDefault());

        /// <summary>The phase: optional, no preset (blank until the user types one).</summary>
        public static readonly PresetField Phase = Text("Phase", "Phase", t => null);

        /// <summary>Every title-block field.</summary>
        public static readonly PresetField[] All = { Control, Supply, TypeOfWork, Phase };

        /// <summary>The field with a key, or null.</summary>
        public static PresetField Find(string key)
        {
            return All.FirstOrDefault(f => f.Key == key);
        }

        /// <summary>The value in effect: the edited value when locked, otherwise the preset ("" when neither exists).</summary>
        public static string Effective(PresetField field, ElevatorProject project, PresetLibrary library)
        {
            if (field == null || project == null)
                return "";
            string v = field.Effective(new PresetContext { Project = project, Library = library }) as string;
            return v ?? "";
        }
    }
}
