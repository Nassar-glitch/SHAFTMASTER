using System;
using System.Collections.Generic;
using System.Linq;

namespace Elevator.Core.Steel
{
    /// <summary>
    /// The steel-shaft group of config/presets.json (DESIGN-FLOW "Steel shaft structure"): the section catalogues and the design defaults
    /// of the steel tower. Every value is an editable design default.
    /// </summary>
    public sealed class SteelShaftPreset
    {
        public const string Concrete = "Concrete";
        public const string SteelStructure = "Steel";
        public const string Upn = "UPN";
        public const string DoubleCross = "DoubleCross";

        /// <summary>Where the catalogue values come from (catalogue maintenance; not printed).</summary>
        public string Source;
        public string Construction;
        public string System;
        public string ColumnProfile;
        public string BeamProfile;
        public string BracingProfile;
        public string Bracing;
        public double BeamSpacingMm;
        public double BasePlateWidthMm;
        public double BasePlateDepthMm;
        public double BasePlateThicknessMm;
        public int BoltCount;
        public double BoltDiameterMm;
        /// <summary>Distance between the bolts of the pattern (square pattern about the plate centre).</summary>
        public double BoltPitchMm;
        public double GussetLengthMm;
        public double GussetWidthMm;
        public double GussetThicknessMm;
        public string SteelGrade;
        public SteelBasePreset Base;
        public List<ChannelProfile> Channels;
        public List<AngleProfile> Angles;

        public SteelShaftPreset Clone()
        {
            return (SteelShaftPreset)MemberwiseClone();
        }

        public ChannelProfile Channel(string name)
        {
            return Channels == null ? null : Channels.FirstOrDefault(c => c != null && c.Name == name);
        }

        public AngleProfile Angle(string name)
        {
            return Angles == null ? null : Angles.FirstOrDefault(a => a != null && a.Name == name);
        }

        public List<string> Validate()
        {
            var p = new List<string>();
            if (Construction != Concrete && Construction != SteelStructure)
                p.Add("SteelShaft.Construction must be " + Concrete + " or " + SteelStructure);
            if (System != Upn)
                p.Add("SteelShaft.System must be " + Upn);
            if (Bracing != DoubleCross)
                p.Add("SteelShaft.Bracing must be " + DoubleCross);
            Action<string, double> pos = (name, v) =>
            {
                if (!ChannelProfile.P(v))
                    p.Add("SteelShaft." + name + " must be positive");
            };
            pos("BeamSpacingMm", BeamSpacingMm);
            pos("BasePlateWidthMm", BasePlateWidthMm);
            pos("BasePlateDepthMm", BasePlateDepthMm);
            pos("BasePlateThicknessMm", BasePlateThicknessMm);
            pos("BoltDiameterMm", BoltDiameterMm);
            pos("BoltPitchMm", BoltPitchMm);
            pos("GussetLengthMm", GussetLengthMm);
            pos("GussetWidthMm", GussetWidthMm);
            pos("GussetThicknessMm", GussetThicknessMm);
            if (BoltCount != 4 && BoltCount != 6 && BoltCount != 8)
                p.Add("SteelShaft.BoltCount must be 4, 6 or 8");
            if (String.IsNullOrWhiteSpace(SteelGrade))
                p.Add("SteelShaft.SteelGrade is required");
            if (Base == null)
                p.Add("SteelShaft.Base is required");
            else
                p.AddRange(Base.Validate());
            if (Channels == null || Channels.Count == 0 || Channels.Any(c => c == null))
                p.Add("SteelShaft.Channels is required");
            else
            {
                foreach (var c in Channels)
                    p.AddRange(c.Validate());
                if (Channels.Select(c => c.Name).Distinct().Count() != Channels.Count)
                    p.Add("SteelShaft.Channels names must be unique");
                if (Channel(ColumnProfile) == null)
                    p.Add("SteelShaft.ColumnProfile is not in the channel catalogue");
                if (Channel(BeamProfile) == null)
                    p.Add("SteelShaft.BeamProfile is not in the channel catalogue");
            }
            if (Angles == null || Angles.Count == 0 || Angles.Any(a => a == null))
                p.Add("SteelShaft.Angles is required");
            else
            {
                foreach (var a in Angles)
                    p.AddRange(a.Validate());
                if (Angles.Select(a => a.Name).Distinct().Count() != Angles.Count)
                    p.Add("SteelShaft.Angles names must be unique");
                if (Angle(BracingProfile) == null)
                    p.Add("SteelShaft.BracingProfile is not in the angle catalogue");
            }
            return p;
        }
    }

    /// <summary>The concrete base under the steel tower (presets): margin around the steel footprint, thickness and the bars.</summary>
    public sealed class SteelBasePreset
    {
        /// <summary>Margin of the block around the footprint of the base plates, each side.</summary>
        public double MarginMm;
        public double ThicknessMm;
        public double BarDiameterMm;
        /// <summary>Bar spacing: the preset of the bar counts follows the block size at this spacing.</summary>
        public double BarSpacingMm;
        /// <summary>Concrete cover to the bars.</summary>
        public double CoverMm;

        public List<string> Validate()
        {
            var p = new List<string>();
            if (!ChannelProfile.P(MarginMm) || !ChannelProfile.P(ThicknessMm) || !ChannelProfile.P(BarDiameterMm)
                || !ChannelProfile.P(BarSpacingMm) || !ChannelProfile.P(CoverMm))
                p.Add("SteelShaft.Base values must be positive");
            return p;
        }
    }
}
