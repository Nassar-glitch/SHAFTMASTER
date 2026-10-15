using System;

namespace Elevator.Core.GuideRails
{
    /// <summary>The fixing of a rail group (car or counterweight) to the shaft: a bracket from the wall behind the rail, or a cross arm.</summary>
    public enum RailFixingType
    {
        /// <summary>A bracket whose arm reaches from the surface behind the rail back (side wall, slab edge or UPN beam) to the rail.</summary>
        WallBracket,
        /// <summary>A short member fixed to the nearest wall or beam the rail can be reached from, with the bracket on its end.</summary>
        CrossArm
    }

    /// <summary>The preset library of the fixing (config/presets.json, GuideRails.Fixing).</summary>
    public sealed class RailFixingPreset
    {
        /// <summary>Car rails: Auto gives a wall bracket when the side surface stands behind the rail back within this reach (mm).</summary>
        public double CarReachMm = 300;
        /// <summary>Counterweight rails (at the rear in RCW): Auto gives a wall bracket from the side wall only when the gap from the rail
        /// back to it is at most this reach (mm); otherwise a cross arm from the rear wall, as the plan draws it.</summary>
        public double CwSideReachMm = 100;
        /// <summary>The cross arm section: UPE 120 as drawn in the template library (the separation-beam detail, fixed at the wall with a
        /// 100 x 100 x 10 end plate and two M12 anchors).</summary>
        public string ArmProfile = "UPE 120";
        public double ArmHeightMm = 120;
        public double ArmWidthMm = 60;
        public double ArmWebMm = 5;
        public double ArmFlangeMm = 8;
        public double ArmEndPlateMm = 100;
        public double ArmEndPlateThicknessMm = 10;
        public double ArmAnchorDiameterMm = 12;
        public double ArmAnchorCount = 2;

        public RailFixingPreset Clone()
        {
            return (RailFixingPreset)MemberwiseClone();
        }
    }

    /// <summary>The numbers the plan dimensions that decide how the rails of one group are fixed.</summary>
    public sealed class RailFixingFacts
    {
        /// <summary>The rail group: Car or CW.</summary>
        public string Family;
        /// <summary>Shaft width, mm.</summary>
        public double ShaftWidthMm;
        /// <summary>Distance between the rails of the group (DBG), mm.</summary>
        public double DbgMm;
        /// <summary>Rail height h1 (back to blade tip) and width b1, mm.</summary>
        public double RailHeightMm, RailWidthMm;
        /// <summary>Rail row to the rear wall (SD - row) for the counterweight rails; the car rails have no rear fixing, mm.</summary>
        public double RearWallToRowMm;
        /// <summary>True when the surfaces are UPN beams (a steel shaft).</summary>
        public bool Steel;

        /// <summary>Plan dimension from the side wall (or beam) to the guiding point of the rail, (SW - DBG) / 2.</summary>
        public double SideToDbgMm
        {
            get { return (ShaftWidthMm - DbgMm) / 2; }
        }

        /// <summary>The gap from the rail back to the side surface: the plan dimension less the rail height.</summary>
        public double SideGapMm
        {
            get { return Math.Max(0, SideToDbgMm - RailHeightMm); }
        }

        /// <summary>The length a cross arm from the rear surface needs to reach the rail: the plan dimension to the rail row less half
        /// the rail width; 0 when there is no rear dimension.</summary>
        public double RearArmMm
        {
            get { return RearWallToRowMm > 0 ? Math.Max(0, RearWallToRowMm - RailWidthMm / 2) : 0; }
        }
    }

    /// <summary>The fixing of one rail group, decided from the plan numbers.</summary>
    public sealed class RailFixingDecision
    {
        public RailFixingType Type;
        /// <summary>The value decided by Auto (true) or chosen by the user (false).</summary>
        public bool Automatic;
        /// <summary>The surface the rail is fixed from: "side wall", "side beam", "rear wall" or "rear beam".</summary>
        public string Surface;
        /// <summary>Wall bracket: the arm length = the gap from the surface to the rail back. Cross arm: the arm length to the rail.</summary>
        public double ArmLengthMm;
        /// <summary>The gap from the rail back to the side surface (always reported; it is the plan's number).</summary>
        public double SideGapMm;
    }

    public static class RailFixing
    {
        public const string Auto = "Auto";
        public const string WallBracketName = "Wall bracket";
        public const string CrossArmName = "Cross arm";
        public static readonly string[] Choices = { Auto, WallBracketName, CrossArmName };

        /// <summary>
        /// Auto: a wall bracket when a surface stands behind the rail back within the reach, otherwise a cross arm from the rear surface.
        /// A chosen value is kept (a wall bracket is drawn with the gap as its arm, a cross arm with the length from the plan).
        /// </summary>
        public static RailFixingDecision Decide(RailFixingFacts f, string setting, double reachMm)
        {
            if (f == null)
                throw new ArgumentNullException("f");
            bool auto = String.IsNullOrWhiteSpace(setting) || setting == Auto;
            RailFixingType type;
            if (auto)
                type = f.SideGapMm <= reachMm + 1e-9 || f.RearArmMm <= 0 ? RailFixingType.WallBracket : RailFixingType.CrossArm;
            else
                type = setting == CrossArmName && f.RearArmMm > 0 ? RailFixingType.CrossArm : RailFixingType.WallBracket;
            var d = new RailFixingDecision { Type = type, Automatic = auto, SideGapMm = f.SideGapMm };
            if (type == RailFixingType.WallBracket)
            {
                d.Surface = f.Steel ? "side beam" : "side wall";
                d.ArmLengthMm = f.SideGapMm;
            }
            else
            {
                d.Surface = f.Steel ? "rear beam" : "rear wall";
                d.ArmLengthMm = f.RearArmMm;
            }
            return d;
        }
    }
}
