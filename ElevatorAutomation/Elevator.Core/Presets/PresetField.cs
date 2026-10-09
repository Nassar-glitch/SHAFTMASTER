using System;
using System.Collections.Generic;
using Elevator.Core.Project;

namespace Elevator.Core.Presets
{
    public sealed class PresetField
    {
        public string Key;
        public string Label;
        public Func<PresetContext, object> Preset;
        public Func<ElevatorProject, object> Stored;
        public Action<ElevatorProject, object> Store;

        // Materialized fields (stage 2) are ordinary project values that ProjectPresets.Apply writes while unlocked.
        // A project without FollowsPresets (older files) treats every materialized field as edited: it opens unchanged.
        public bool Materialized;
        public string Page;

        public bool IsEdited(ElevatorProject p)
        {
            if (p == null)
                return false;
            if (Materialized && p.FollowsPresets != true)
                return true;
            return p.UserEditedFields != null && p.UserEditedFields.Contains(Key);
        }

        public bool IsBlank(ElevatorProject p)
        {
            var v = Stored(p);
            if (v == null)
                return true;
            if (v is string)
                return String.IsNullOrWhiteSpace((string)v);
            if (v is double && (Key == "Shaft.WidthMm" || Key == "Shaft.DepthMm"))
                return (double)v <= 0; // non-nullable shaft size: 0 is blank
            return false;
        }

        public object PresetValue(PresetContext c)
        {
            return c == null || c.Library == null ? null : Preset(c);
        }

        public object Effective(PresetContext c)
        {
            return IsEdited(c.Project) ? Stored(c.Project) : PresetValue(c);
        }

        public void Edit(ElevatorProject p, object value)
        {
            Store(p, value);
            if (p.UserEditedFields == null)
                p.UserEditedFields = new List<string>();
            if (!p.UserEditedFields.Contains(Key))
                p.UserEditedFields.Add(Key);
        }

        // Materialized fields: the caller runs ProjectPresets.Apply afterwards to write the preset.
        public void Reset(ElevatorProject p)
        {
            if (Materialized)
            {
                ProjectPresets.Unlock(p, new[] { Key });
                return;
            }
            if (p.UserEditedFields != null)
            {
                p.UserEditedFields.Remove(Key);
                if (p.UserEditedFields.Count == 0)
                    p.UserEditedFields = null;
            }
            Store(p, null);
        }
    }
}
