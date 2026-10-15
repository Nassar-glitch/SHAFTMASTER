using System;
using System.Collections.Generic;

namespace Elevator.Core.GuideRails
{
    /// <summary>
    /// The guide-rail bracket of one rail profile (config/presets.json, GuideRails.Profiles[].Bracket; docs/design/details/BRACKETS.md):
    /// the two angle arms, the connecting plate, the clips that hold the rail foot, and the bolts and anchors. All mm.
    /// The angle, plate, assembly bolts and anchors are one family for every rail (the template library shows one); the clips and
    /// their bolts follow the rail blade (5 / 9 / 16 mm).
    /// </summary>
    public sealed class RailBracketPreset
    {
        /// <summary>Angle arm leg that lies on the wall (the long leg).</summary>
        public double AngleLegAMm = 50;
        /// <summary>Angle arm leg that carries the plate.</summary>
        public double AngleLegBMm = 40;
        public double AngleThicknessMm = 4;
        /// <summary>Length of one angle arm, from the wall to the rail.</summary>
        public double AngleLengthMm = 270;
        /// <summary>Connecting plate between the two arms (length = the arm length, width = the long leg).</summary>
        public double PlateThicknessMm = 4;
        /// <summary>Bolts that join the arms and the plate (the library: 4 x M10x40).</summary>
        public double AssemblyBoltDiameterMm = 10;
        public double AssemblyBoltLengthMm = 40;
        public double ClipLengthMm = 50;
        public double ClipWidthMm = 30;
        public double ClipThicknessMm = 6;
        /// <summary>Rail-to-bracket bolt: diameter of the bolt that holds a clip (two per clip).</summary>
        public double ClipBoltDiameterMm = 10;
        public double ClipBoltCount = 2;
        /// <summary>Anchor into the concrete (the library: chemical anchor Ø12 x 150).</summary>
        public double AnchorDiameterMm = 12;
        public double AnchorLengthMm = 150;

        public RailBracketPreset Clone()
        {
            return (RailBracketPreset)MemberwiseClone();
        }

        /// <summary>The bracket of the BRACKETS.md row of a blade width: the nearest row at or above the blade (5, 9 or 16 mm).</summary>
        public static RailBracketPreset ForBlade(double bladeMm)
        {
            var p = new RailBracketPreset();
            if (bladeMm <= 5.0 + 1e-9)
            {
                p.ClipLengthMm = 40; p.ClipWidthMm = 25; p.ClipThicknessMm = 5; p.ClipBoltDiameterMm = 8;
            }
            else if (bladeMm <= 9.0 + 1e-9)
            {
                p.ClipLengthMm = 50; p.ClipWidthMm = 30; p.ClipThicknessMm = 6; p.ClipBoltDiameterMm = 10;
            }
            else
            {
                p.ClipLengthMm = 60; p.ClipWidthMm = 35; p.ClipThicknessMm = 8; p.ClipBoltDiameterMm = 12;
            }
            return p;
        }

        public List<string> Validate(string owner)
        {
            var list = new List<string>();
            Action<string, double> pos = (n, v) =>
            {
                if (Double.IsNaN(v) || Double.IsInfinity(v) || v <= 0)
                    list.Add(owner + ".Bracket." + n + " must be positive");
            };
            pos("AngleLegAMm", AngleLegAMm); pos("AngleLegBMm", AngleLegBMm); pos("AngleThicknessMm", AngleThicknessMm);
            pos("AngleLengthMm", AngleLengthMm); pos("PlateThicknessMm", PlateThicknessMm);
            pos("AssemblyBoltDiameterMm", AssemblyBoltDiameterMm); pos("AssemblyBoltLengthMm", AssemblyBoltLengthMm);
            pos("ClipLengthMm", ClipLengthMm); pos("ClipWidthMm", ClipWidthMm); pos("ClipThicknessMm", ClipThicknessMm);
            pos("ClipBoltDiameterMm", ClipBoltDiameterMm); pos("ClipBoltCount", ClipBoltCount);
            pos("AnchorDiameterMm", AnchorDiameterMm); pos("AnchorLengthMm", AnchorLengthMm);
            return list;
        }
    }
}
