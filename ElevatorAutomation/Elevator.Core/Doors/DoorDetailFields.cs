using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.Core.Doors
{
    /// <summary>
    /// The choices of the landing door detail (VIEW at D-D and SEC. C-C, stage G2) that the door data do not give. The detail is the
    /// user's master door (Templates/DoorMasters.json) stretched to the project's opening and height; these fields choose the master
    /// (door panel), the hinge side of a swing door, the entrance height and the structural opening margin. Every value follows its
    /// preset until edited (CAR &amp; DOORS page), stored in the project extension "doorDetail" (docs/design/details/DOORS-G2.md).
    /// </summary>
    public static class DoorDetailFields
    {
        const string ExtensionKey = "doorDetail";

        /// <summary>The clear height of the entrance (the plan's "DOOR W x 2000").</summary>
        public const double DefaultClearHeightMm = 2000;

        /// <summary>The frame jamb of the masters each side of the clear opening (fixed, from the user's drawing).</summary>
        public const double FrameJambMm = 120;

        public const string Solid = "Solid", Glass = "Glass", VisionWindow = "VisionWindow";
        public const string Left = "Left", Right = "Right";

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

        static bool Automatic(PresetContext c)
        {
            return c.Project != null && c.Project.Doors != null && DoorTypes.IsAutomatic(c.Project.Doors.Type);
        }

        static PresetField Field(string member, string label, Func<PresetContext, object> preset, bool number)
        {
            string key = "Doors.Detail." + member;
            return new PresetField
            {
                Page = "CAR & DOORS",
                Key = key,
                Label = label,
                Preset = preset,
                Stored = p =>
                {
                    var d = Read(p);
                    object v;
                    if (d == null || !d.TryGetValue(key, out v) || v == null)
                        return null;
                    return number ? (object)Convert.ToDouble(v, CultureInfo.InvariantCulture) : Convert.ToString(v, CultureInfo.InvariantCulture);
                },
                Store = (p, v) => Set(p, key, v == null ? null : number ? (object)Convert.ToDouble(v, CultureInfo.InvariantCulture) : Convert.ToString(v, CultureInfo.InvariantCulture))
            };
        }

        public static readonly PresetField ClearHeight = Field("ClearHeightMm", "Entrance height (mm)", c => DefaultClearHeightMm, true);
        public static readonly PresetField StructuralMargin = Field("StructuralMarginMm", "Structural opening beyond the clear opening, each side (mm)", c => 150.0, true);
        /// <summary>Automatic: Solid / Glass (default Solid); swing: VisionWindow / Glass (default VisionWindow).</summary>
        public static readonly PresetField DoorPanel = Field("Panel", "Door panel", c => Automatic(c) ? Solid : VisionWindow, false);
        /// <summary>The hinge side of a swing door as seen from the landing (the masters: handle at the left, hinge at the right).</summary>
        public static readonly PresetField HingeSide = Field("HingeSide", "Hinge side", c => Right, false);

        public static readonly PresetField[] All = { DoorPanel, HingeSide, ClearHeight, StructuralMargin };
        public static readonly PresetField[] Numbers = { ClearHeight, StructuralMargin };

        public static PresetField Find(string key)
        {
            return All.FirstOrDefault(f => f.Key == key);
        }

        /// <summary>The door panel choices of a door type (the first is the preset).</summary>
        public static string[] PanelChoices(string doorType)
        {
            return DoorTypes.IsAutomatic(doorType) ? new[] { Solid, Glass } : new[] { VisionWindow, Glass };
        }

        public static string PanelName(string id)
        {
            return id == VisionWindow ? "Vision window" : id == Glass ? "Glass panel" : "Solid";
        }

        /// <summary>The values in effect for a project.</summary>
        public static DoorDetailSizes Effective(ElevatorProject project, PresetLibrary library)
        {
            var c = new PresetContext { Project = project, Library = library };
            Func<PresetField, double> v = f => Convert.ToDouble(f.Effective(c), CultureInfo.InvariantCulture);
            string type = DoorTypes.Normalize(project == null || project.Doors == null ? null : project.Doors.Type);
            string panel = Convert.ToString(DoorPanel.Effective(c), CultureInfo.InvariantCulture);
            if (!PanelChoices(type).Contains(panel))
                panel = PanelChoices(type)[0];
            string hinge = Convert.ToString(HingeSide.Effective(c), CultureInfo.InvariantCulture);
            return new DoorDetailSizes
            {
                ClearHeightMm = v(ClearHeight),
                StructuralMarginMm = v(StructuralMargin),
                Panel = panel,
                HingeRight = hinge != Left
            };
        }
    }

    /// <summary>The landing door detail values in effect.</summary>
    public sealed class DoorDetailSizes
    {
        public double ClearHeightMm, StructuralMarginMm;
        public string Panel;
        public bool HingeRight;

        /// <summary>The structural opening for a clear opening when the plan has none: the margin each side of the opening (the master:
        /// 800 gives 1100, 30 beyond the 120 frame).</summary>
        public double StructuralWidth(double openingMm)
        {
            return openingMm + 2 * StructuralMarginMm;
        }
    }
}
