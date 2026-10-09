using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Suspension;

namespace Elevator.Reporting.Figures
{
    /// <summary>
    /// F10 traction sheave (schematic, NTS): rope wrap with α dimensioned from the reeving geometry, T1 / T2 on the rope runs, and the
    /// groove profile detail with β / γ from the project inputs. Shapes are fixed; α, β, γ, Dt and the groove type are project data.
    /// </summary>
    public static partial class CalculationFigures
    {
        /// <summary>Traction sheave centre X of figure F10, paper mm.</summary>
        public const double F10SheaveX = 52;
        /// <summary>Traction sheave centre Y of figure F10, paper mm.</summary>
        public const double F10SheaveY = 50;
        /// <summary>Traction sheave radius of figure F10, paper mm.</summary>
        public const double F10SheaveR = 26;

        // Angular dimension: arc of radius r about c from a0 to a1 (math degrees, counter-clockwise, paper y down) with arrowheads and
        // text.
        static void AngleDim(
            Figure f,
            FigurePoint c,
            double r,
            double a0,
            double a1,
            string text,
            IList<FigurePoint> labelAt,
            string anchor,
            string tag,
            string value)
        {
            var arc = Arc(c, r, a0, a1, 24);
            f.Poly(arc, false, FigureLayer.Dimension, tag).Value = value;
            Func<double, double, FigurePoint> tangent =
                (a, sign) =>
                {
                    double t = a * Math.PI / 180;
                    return Pt(-Math.Sin(t) * sign, -Math.Cos(t) * sign);
                };
            double s = a1 > a0 ? 1 : -1;
            f.Arrowhead(arc[0], tangent(a0, -s), FigureLayer.Dimension);
            f.Arrowhead(arc[arc.Count - 1], tangent(a1, s), FigureLayer.Dimension);
            f.Text(text, labelAt, anchor, FigureLayer.Text, 2.5, false, tag + "-text", value);
        }

        static FigurePoint Polar(FigurePoint c, double r, double deg)
        {
            double t = deg * Math.PI / 180;
            return c + Pt(r * Math.Cos(t), -r * Math.Sin(t));
        }

        /// <summary>Figure F10: wrap angle and groove of the traction sheave.</summary>
        public static Figure TractionSheave(RopeTractionResult r)
        {
            var x = r.Inputs;
            var g = r.Geometry;
            var f = new Figure { Id = "F10", Title = "Traction sheave — wrap angle and groove", Schematic = true };
            DrawSheaveWrap(x, g, f);
            // Groove detail.
            DrawGrooveDetail(x, f);
            var note = f.Text("T1 and T2 act on the rope runs at the traction sheave", Pt(4, 92), "start");
            Finish(f, note.At.Y);
            return f;
        }

        private static void DrawSheaveWrap(RopeTractionEffectiveInputs x, RopeTractionGeometry g, Figure f)
        {
            var C = Pt(F10SheaveX, F10SheaveY);
            double R = F10SheaveR;
            double alpha =
                g.WrapAngleDeg.HasValue && (!g.DeflectorPresent || g.DeflectorPathValid) ? g.WrapAngleDeg.Value : 180, exit = 180 - alpha;
            f.Text("Traction sheave", Pt(F10SheaveX, 8), "middle", FigureLayer.Text, 3.5, false, null, null, true);
            // Sheave: rim, pitch circle (centre line), hub.
            f.Circle(C, R + 2.5, FigureLayer.Outline, false, "sheave");
            f.Circle(C, R, FigureLayer.Centre, false, "pitch-circle");
            f.Circle(C, 3, FigureLayer.Secondary);
            f.Line(C + Pt(-5, 0), C + Pt(5, 0), FigureLayer.Centre);
            f.Line(C + Pt(0, -5), C + Pt(0, 5), FigureLayer.Centre);
            // Rope: car side vertical at the left, wrap over the top through α, counterweight side tangent at the exit angle.
            var exitPoint = Polar(C, R, exit);
            double t = exit * Math.PI / 180;
            var exitDir = Pt(Math.Sin(t), Math.Cos(t));
            var rope = new List<FigurePoint> { C + Pt(-R, 36) };
            rope.AddRange(Arc(C, R, 180, exit, 36));
            rope.Add(exitPoint + exitDir * 34);
            f.Poly(rope, false, FigureLayer.Outline, "rope").Value = Figure.Num(alpha, "0.##");
            // α dimension outside the rim with radial extension lines at the rope contact limits.
            foreach (var a in new[] { 180, exit })
            {
                f.Line(Polar(C, R + 3.5, a), Polar(C, R + 12, a), FigureLayer.Dimension);
            }
            AngleDim(
                f,
                C,
                R + 10,
                180,
                exit,
                "α " + Figure.Num(alpha, "0.#") + "°",
                new[] { Polar(C, R + 13, 90) + Pt(0, -1.2), Polar(C, R + 13, 120) },
                "middle",
                "dim-alpha",
                Figure.Num(alpha, "0.##"));
            // T1 on the car side, T2 on the counterweight side, along the rope runs (forces from the ropes on the sheave).
            var carRope = C + Pt(-R, 0);
            f.Load(
                carRope + Pt(-4, 12),
                carRope + Pt(-4, 30),
                "T1",
                new[] { carRope + Pt(-6, 22), carRope + Pt(-6, 16) },
                "end",
                "rope-t1",
                null);
            f.Text("car side", carRope + Pt(-6, 27), "end");
            var n = Pt(exitDir.Y, -exitDir.X);
            var cw0 = exitPoint + exitDir * 14 + n * 4;
            f.Load(
                cw0,
                cw0 + exitDir * 16,
                "T2",
                new[] { cw0 + exitDir * 10 + n * 3, cw0 + exitDir * 4 + n * 3 },
                "start",
                "rope-t2",
                null);
            var ropeEnd = exitPoint + exitDir * 34;
            f.Text(g.DeflectorPresent ? "to deflector" : "counterweight side", ropeEnd + Pt(0, 4.5), "middle");
            if (Positive(x.SheavePitchDiameterMm))
            {
                var lead = Polar(C, R, 285);
                var knee = Pt(lead.X + 4, C.Y + R + 9);
                f.Poly(new[] { lead, knee, knee + Pt(3, 0) }, false, FigureLayer.Leader);
                f.Text(
                    "Dt " + Figure.Mm(x.SheavePitchDiameterMm.Value),
                    knee + Pt(4, 1),
                    "start",
                    FigureLayer.Text,
                    2.5,
                    false,
                    "dt",
                    Figure.Mm(x.SheavePitchDiameterMm.Value));
            }
        }

        private static void DrawGrooveDetail(RopeTractionEffectiveInputs x, Figure f)
        {
            f.Line(Pt(108, 4), Pt(108, 88), FigureLayer.Thin, "view-split");
            f.Text("Groove detail", Pt(137, 8), "middle", FigureLayer.Text, 3.5, false, null, null, true);
            f.Text(
                GrooveTypes.Label(x.GrooveType).Replace("-", " "),
                Pt(137, 13),
                "middle",
                FigureLayer.Text,
                2.5,
                false,
                "groove-type",
                x.GrooveType);
            var O = Pt(137, 52);
            double rr = 9;
            f.Circle(O, rr, FigureLayer.Outline, false, "groove-rope");
            f.Line(O + Pt(-13, 0), O + Pt(13, 0), FigureLayer.Centre);
            f.Line(O + Pt(0, -13), O + Pt(0, 14), FigureLayer.Centre);
            double gamma = x.GrooveAngleDeg ?? 0, beta = GrooveTypes.NeedsUndercut(x.GrooveType) ? (x.UndercutAngleDeg ?? 0) : 0;
            bool v = x.GrooveType != GrooveTypes.UndercutU;
            // Flanks: V-groove flanks tangent to the rope; U-groove seat follows the rope to its horizontal diameter, then the flanks open.
            double half = gamma / 2 * Math.PI / 180;
            List<FigurePoint> profile = GrooveProfile(O, rr, beta, v, half);
            f.Poly(profile, false, FigureLayer.Outline, "groove-profile");
            // γ: groove angle between the flank directions (V) / at the rope centre between the flank starts (U); β: undercut angle at the
            // rope centre.
            DrawGrooveAngles(f, O, rr, gamma, beta, v, half);
        }

        private static List<FigurePoint> GrooveProfile(FigurePoint O, double rr, double beta, bool v, double half)
        {
            var profile = new List<FigurePoint>();
            if (v)
            {
                double dist = rr / Math.Sin(half);
                var apex = O + Pt(0, dist); // flank lines meet below the rope
                var top = new[] { -1, 1 }.Select(sd => apex + Pt(sd * Math.Tan(half) * (dist + 16), -(dist + 16))).ToArray();
                double floorY = beta > 0 ? O.Y + rr * Math.Cos(beta / 2 * Math.PI / 180) : O.Y + rr + 1.5;
                var left = new[] { top[0], apex + Pt(-Math.Tan(half) * (apex.Y - floorY), -(apex.Y - floorY)) };
                var right = new[] { apex + Pt(Math.Tan(half) * (apex.Y - floorY), -(apex.Y - floorY)), top[1] };
                profile.AddRange(left);
                if (beta > 0)
                {
                    double bx = rr * Math.Sin(beta / 2 * Math.PI / 180);
                    profile.Add(Pt(O.X - bx, floorY));
                    profile.Add(Pt(O.X - bx, floorY + 5));
                    profile.Add(Pt(O.X + bx, floorY + 5));
                    profile.Add(Pt(O.X + bx, floorY));
                }
                profile.AddRange(right);
            }
            else
            {
                // Seat arc from the flank start (180°) to the undercut edge, undercut slot, mirrored seat, flank at γ/2 from the vertical.
                double eb = beta > 0 ? 270 - beta / 2 : 270;
                profile.Add(O + Pt(-rr - Math.Tan(half) * 16, -16));
                profile.AddRange(Arc(O, rr + 0.4, 180, eb, 16));
                if (beta > 0)
                {
                    var l = Polar(O, rr + 0.4, eb);
                    var rgt = Polar(O, rr + 0.4, 270 + beta / 2);
                    profile.Add(l + Pt(0, 5));
                    profile.Add(rgt + Pt(0, 5));
                }
                profile.AddRange(Arc(O, rr + 0.4, beta > 0 ? 270 + beta / 2 : 270, 360, 16));
                profile.Add(O + Pt(rr + Math.Tan(half) * 16, -16));
            }
            return profile;
        }

        private static void DrawGrooveAngles(Figure f, FigurePoint O, double rr, double gamma, double beta, bool v, double half)
        {
            if (gamma > 0)
            {
                if (v)
                {
                    double dist = rr / Math.Sin(half);
                    var apex = O + Pt(0, dist);
                    foreach (int sd in new[] { -1, 1 })
                        f.Line(apex, apex + Pt(sd * Math.Tan(half) * (dist - rr + 4), -(dist - rr + 4)), FigureLayer.Dimension);
                    AngleDim(
                        f,
                        apex,
                        dist + 12,
                        90 + gamma / 2,
                        90 - gamma / 2,
                        "γ " + Figure.Num(gamma, "0.#") + "°",
                        new[] { apex + Pt(0, -dist - 14.5) },
                        "middle",
                        "groove-gamma",
                        Figure.Num(gamma, "0.##"));
                }
                else
                {
                    foreach (int sd in new[] { -1, 1 })
                        f.Line(O, O + Pt(sd * Math.Sin(half) * 22, -Math.Cos(half) * 22), FigureLayer.Dimension);
                    AngleDim(
                        f,
                        O,
                        20,
                        90 + gamma / 2,
                        90 - gamma / 2,
                        "γ " + Figure.Num(gamma, "0.#") + "°",
                        new[] { O + Pt(0, -23) },
                        "middle",
                        "groove-gamma",
                        Figure.Num(gamma, "0.##"));
                }
            }
            if (beta > 0)
            {
                foreach (int sd in new[] { -1, 1 })
                    f.Line(O, Polar(O, rr + 11, 270 + sd * beta / 2), FigureLayer.Dimension);
                AngleDim(
                    f,
                    O,
                    rr + 9,
                    270 - beta / 2,
                    270 + beta / 2,
                    "β " + Figure.Num(beta, "0.#") + "°",
                    new[] { O + Pt(0, rr + 14), O + Pt(0, rr + 17.5) },
                    "middle",
                    "groove-beta",
                    Figure.Num(beta, "0.##"));
            }
        }

        static bool Positive(double? v)
        {
            return v.HasValue && !Double.IsNaN(v.Value) && !Double.IsInfinity(v.Value) && v.Value > 0;
        }
    }
}
