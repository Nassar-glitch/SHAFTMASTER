using System;
using System.Collections.Generic;

namespace Elevator.Core.Steel
{
    // Geometry of the steel shaft as plain data (mm), for the drawing sessions. Plan axes as in the project: X along the shaft width W,
    // Y along the depth D, origin at the front-left corner of the clear rectangle, the entrance face at Y = 0 (the front). The steel
    // lies outside the clear rectangle. Face elevations use u along the face (X for Front and Back, Y for Left and Right) and z, the
    // elevation from the lowest floor.

    public sealed class SteelPoint
    {
        public double X;
        public double Y;

        public SteelPoint()
        {
        }

        public SteelPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    public sealed class SteelRect
    {
        public double X0;
        public double Y0;
        public double X1;
        public double Y1;

        public SteelRect()
        {
        }

        public SteelRect(double x0, double y0, double x1, double y1)
        {
            X0 = Math.Min(x0, x1);
            Y0 = Math.Min(y0, y1);
            X1 = Math.Max(x0, x1);
            Y1 = Math.Max(y0, y1);
        }

        public double Width
        {
            get { return X1 - X0; }
        }

        public double Height
        {
            get { return Y1 - Y0; }
        }

        public double CenterX
        {
            get { return (X0 + X1) / 2; }
        }

        public double CenterY
        {
            get { return (Y0 + Y1) / 2; }
        }

        /// <summary>True when the interiors overlap (touching edges do not count).</summary>
        public bool Overlaps(SteelRect o)
        {
            const double e = 1e-6;
            return X0 < o.X1 - e && o.X0 < X1 - e && Y0 < o.Y1 - e && o.Y0 < Y1 - e;
        }

        public SteelRect Grow(double d)
        {
            return new SteelRect(X0 - d, Y0 - d, X1 + d, Y1 + d);
        }
    }

    public sealed class SteelSegment
    {
        public double X0;
        public double Y0;
        public double X1;
        public double Y1;

        public SteelSegment()
        {
        }

        public SteelSegment(double x0, double y0, double x1, double y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        public double Length
        {
            get { return Math.Sqrt((X1 - X0) * (X1 - X0) + (Y1 - Y0) * (Y1 - Y0)); }
        }
    }

    public sealed class SteelCircle
    {
        public double X;
        public double Y;
        public double DiameterMm;
    }

    /// <summary>A corner column in plan: the channel outline (web outside, flanges into the shaft), its base plate and bolts.</summary>
    public sealed class SteelColumnPlan
    {
        /// <summary>FrontLeft, FrontRight, BackLeft or BackRight.</summary>
        public string Corner;
        public string Profile;
        /// <summary>The side of the shaft the web is on: Left (flanges point to +X) or Right (flanges point to -X).</summary>
        public string WebSide;
        public SteelRect Box;
        /// <summary>The channel outline, closed, counter-clockwise from the web's outer corner (parallel flanges; the 8 % slope and radii are not drawn).</summary>
        public List<SteelPoint> Outline = new List<SteelPoint>();
        public SteelRect Plate;
        public List<SteelCircle> Bolts = new List<SteelCircle>();
    }

    /// <summary>A ring beam in plan on one face.</summary>
    public sealed class SteelBeamPlan
    {
        /// <summary>Front, Back, Left or Right.</summary>
        public string Face;
        public string Profile;
        /// <summary>The beam section's footprint: flange width across the face, the run along the face between the columns.</summary>
        public SteelRect Box;
        public SteelSegment CenterLine;
    }

    public sealed class SteelPlan
    {
        public SteelRect Clear;
        public string EntranceFace = "Front";
        public List<SteelColumnPlan> Columns = new List<SteelColumnPlan>();
        public List<SteelBeamPlan> Beams = new List<SteelBeamPlan>();
        /// <summary>The smallest rectangle around the four base plates.</summary>
        public SteelRect PlateFootprint;
        /// <summary>The most any base plate reaches into the clear rectangle (0 when none does).</summary>
        public double PlateIntrusionMm;
    }

    /// <summary>The concrete base under the steel: outline, thickness and the bars both ways.</summary>
    public sealed class SteelBaseBlock
    {
        public SteelRect Plan;
        public double TopMm;
        public double BottomMm;
        public double ThicknessMm;
        public double BarDiameterMm;
        public double CoverMm;
        /// <summary>Bars running along X, spread across Y (plan lines).</summary>
        public List<SteelSegment> BarsAlongX = new List<SteelSegment>();
        /// <summary>Bars running along Y, spread across X (plan lines).</summary>
        public List<SteelSegment> BarsAlongY = new List<SteelSegment>();
        public double VolumeM3;
    }

    public sealed class SteelBeamLine
    {
        public string Key;
        public SteelLevelKind Kind;
        public double ElevationMm;
        public double U0;
        public double U1;
        /// <summary>The beam section's depth band around the level (the channel web height).</summary>
        public double Z0;
        public double Z1;
    }

    /// <summary>The double cross (X) of one panel: two diagonals from corner to corner.</summary>
    public sealed class SteelBrace
    {
        public int Panel;
        public double Z0;
        public double Z1;
        public double U0;
        public double U1;
        public SteelSegment DiagonalUp;
        public SteelSegment DiagonalDown;
        public double LengthMm;
    }

    /// <summary>One face of the tower in elevation.</summary>
    public sealed class SteelFace
    {
        public string Name;
        public bool IsEntrance;
        public double LengthMm;
        public List<SteelRect> Columns = new List<SteelRect>();
        public List<SteelRect> Plates = new List<SteelRect>();
        public List<SteelBeamLine> Beams = new List<SteelBeamLine>();
        public List<SteelBrace> Braces = new List<SteelBrace>();
        /// <summary>Gusset plates at the panel corners of a braced face (u, z).</summary>
        public List<SteelPoint> Gussets = new List<SteelPoint>();
        /// <summary>The concrete base seen from this face (u, z).</summary>
        public SteelRect BaseBlock;
    }

    /// <summary>The top of the tower for the machine: the last beam ring, within the shaft footprint, with no outriggers.</summary>
    public sealed class SteelTowerTop
    {
        public double ElevationMm;
        public SteelRect Clear;
        /// <summary>The area the machine must stay inside (the clear rectangle: the machine is within the shaft footprint).</summary>
        public SteelRect MachineEnvelope;
        public bool Outriggers;
        public List<SteelColumnPlan> Columns = new List<SteelColumnPlan>();
        public List<SteelBeamPlan> Beams = new List<SteelBeamPlan>();
        /// <summary>The smallest rectangle around the columns and beams.</summary>
        public SteelRect Outline;
    }

    /// <summary>One row of the steel member list.</summary>
    public sealed class SteelMember
    {
        public string Item;
        public string Profile;
        public double LengthMm;
        public int Count;
        public double MassPerPieceKg;

        public double TotalMassKg
        {
            get { return MassPerPieceKg * Count; }
        }
    }
}
