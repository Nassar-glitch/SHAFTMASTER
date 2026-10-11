using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Geometry;
using Elevator.Core.GuideRails;
using Elevator.Core.MachineRoom;
using Elevator.Core.Doors;
using Elevator.Core.Project;
using Elevator.Core.SafetyGear;

namespace Elevator.Reporting.Figures
{
    /// <summary>
    /// Phase A figures F1–F6, built only from the same plan geometry and results as the calculation sheet. Shapes, symbols, styles and
    /// layout are fixed; every dimension, position, rail location and force value is project data. See docs/calculations/CALCULATION-SHEET-FIGURES.md.
    /// </summary>
    public static partial class CalculationFigures
    {
        const double Wall = 200; // symbolic wall band in plan figures (not dimensioned, not a project value)

        static FigurePoint Pt(double x, double y)
        {
            return new FigurePoint(x, y);
        }

        static List<FigurePoint> Map(FigureView v, IEnumerable<FigurePoint> model)
        {
            return model.Select(p => v.P(p.X, p.Y)).ToList();
        }

        static void Finish(Figure f, double bottom)
        {
            f.HeightMm = Math.Ceiling(bottom + 4);
        }

        static void Axes(Figure f, FigurePoint origin)
        {
            f.Line(origin, origin + Pt(10, 0), FigureLayer.Thin);
            f.Arrowhead(origin + Pt(10, 0), Pt(1, 0), FigureLayer.Thin, 2.0, 0.5);
            f.Line(origin, origin + Pt(0, -10), FigureLayer.Thin);
            f.Arrowhead(origin + Pt(0, -10), Pt(0, -1), FigureLayer.Thin, 2.0, 0.5);
            f.Text("X", origin + Pt(11.5, 1), "start");
            f.Text("Y", origin + Pt(0, -11.5), "middle");
        }

        // Rail outline in model plan coordinates: blade tip at (tipX, lineY), blade pointing in direction 'dir' (+1 → +X).
        static List<FigurePoint> RailPlan(IRailSection rail, double tipX, double lineY, int dir, double k = 1)
        {
            return rail.Outline().Select(q => Pt(tipX - dir * (rail.HeightMm - q.Y) * k, lineY + q.X * k)).ToList();
        }

        /// <summary>True scale, enlarged about the blade tip only when the symbol would be smaller than about 3 mm on paper.</summary>
        public const double MinRailSymbolMm = 3;

        /// <summary>The enlargement of a rail symbol that keeps it visible at a small scale.</summary>
        public static double RailSymbolFactor(IRailSection rail, FigureView v)
        {
            return Math.Max(1, MinRailSymbolMm / (Math.Max(rail.BaseWidthMm, rail.HeightMm) * v.Scale));
        }

        static void LoadPoint(Figure f, FigurePoint c, string tag)
        {
            f.Circle(c, 1.6, FigureLayer.Load, false, tag);
            f.Line(c + Pt(-1.1, -1.1), c + Pt(1.1, 1.1), FigureLayer.Load);
            f.Line(c + Pt(-1.1, 1.1), c + Pt(1.1, -1.1), FigureLayer.Load);
        }

        // ---------- F1 shaft plan ----------
        /// <summary>Figure F1: shaft plan with car, counterweight, rails and dimensions.</summary>
        public static Figure ShaftPlan(ElevatorParameters p, IRailSection carRail, IRailSection cwRail, DoorLayout automaticDoor = null)
        {
            var f = new Figure { Id = "F1", Title = "Shaft plan", Schematic = false };
            var v = new FigureView(-Wall, -Wall, p.SW + Wall, p.SD + Wall, 24, 16, 100, 136);
            f.View = v;
            // An automatic door (2S, 2C) has its own opening position (the 2S opening is shifted from the shaft centre).
            bool automatic = automaticDoor != null && DoorTypes.IsAutomatic(automaticDoor.Type) && automaticDoor.OpeningMm > 0;
            double dl = automatic ? automaticDoor.OpeningLeftX : p.X("doorLeft");
            double dr = automatic ? automaticDoor.OpeningRightX : p.X("doorRight");
            bool door = p.DoorWidth > 0 && dl > 0 && dr < p.SW;
            // Walls (symbolic hatched band) with the entrance opening in the front wall, from the plan geometry.
            DrawWalls(p, f, v, dl, dr, door);
            // Car (internal) and counterweight.
            double cl = p.X("carLeft"), cr = p.X("carRight");
            f.Poly(
                Map(v, new[] { Pt(cl, p.CarFront), Pt(cr, p.CarFront), Pt(cr, p.CarRear), Pt(cl, p.CarRear) }),
                true,
                FigureLayer.Secondary,
                "car").Value = Figure.Mm(p.CarWidth) + "," + Figure.Mm(p.CarDepth);
            f.Poly(Map(v, new[]
            {
                Pt(p.X("cwLeft"), p.CwFront),
                Pt(p.X("cwRight"), p.CwFront),
                Pt(p.X("cwRight"), p.CwRear),
                Pt(p.X("cwLeft"), p.CwRear)
            }), true, FigureLayer.Secondary, "counterweight");
            // Car rail line (centre line) and simplified T-profile rails at their positions, blades towards the car / counterweight.
            f.Line(v.P(-Wall - 60, p.CarRow), v.P(p.SW + Wall + 60, p.CarRow), FigureLayer.Centre, "car-rail-line").Value =
                Figure.Mm(p.CarRow);
            double kc = RailSymbolFactor(carRail, v);
            f.Poly(Map(v, RailPlan(carRail, p.X("railLeft"), p.CarRow, +1, kc)), true, FigureLayer.Outline, "car-rail-left", true).Value =
                Figure.Mm(p.X("railLeft"));
            var carRight =
                f.Poly(Map(v, RailPlan(carRail, p.X("railRight"), p.CarRow, -1, kc)), true, FigureLayer.Outline, "car-rail-right", true);
            carRight.Value = Figure.Mm(p.X("railRight"));
            FigurePolyline cwRight = null;
            if (cwRail != null)
            {
                double kw = RailSymbolFactor(cwRail, v);
                f.Poly(
                    Map(v, RailPlan(cwRail, p.X("cwRailLeft"), p.CwRow, +1, kw)),
                    true,
                    FigureLayer.Outline,
                    "cw-rail-left",
                    true).Value = Figure.Mm(p.X("cwRailLeft"));
                cwRight =
                    f.Poly(Map(v, RailPlan(cwRail, p.X("cwRailRight"), p.CwRow, -1, kw)), true, FigureLayer.Outline, "cw-rail-right", true);
                cwRight.Value = Figure.Mm(p.X("cwRailRight"));
            }
            if (automatic)
                DrawAutomaticDoor(p, f, v, automaticDoor);
            var label = v.P(p.Center, (p.CarFront + p.CarRow) / 2);
            f.Text("CAR", label + Pt(0, -1.5), "middle");
            f.Text("internal W × D", label + Pt(0, 2.5), "middle");
            f.Text("CW", v.P(p.Center, p.CwRow) + Pt(0, 1.2), "middle");
            f.Text("car rail line", v.P(p.SW + Wall + 60, p.CarRow) + Pt(1.5, 1), "start");
            // Rail designations as leader labels outside the walls on the right.
            DrawRailLabels(p, carRail, cwRail, f, v, carRight, cwRight);
            // Dimensions outside the walls: W, car DBG and SW below; CW DBG above; D and SD on the left.
            double front = v.P(0, -Wall).Y, rear = v.P(0, p.SD + Wall).Y, left = v.P(-Wall, 0).X;
            DrawShaftDimensions(p, cwRail, f, v, cl, cr, front, rear, left);
            Axes(f, Pt(v.P(p.SW + Wall, 0).X + 8, front + 21));
            if (automatic)
            {
                // Clear opening and stacking side of the automatic door.
                string handing = automaticDoor.Type == DoorTypes.Automatic2S ? ", stacking " + automaticDoor.StackingSide.ToLowerInvariant() : "";
                f.DimH(
                    v.P(dl, 0),
                    v.P(dr, 0),
                    front + 28,
                    "door " + Figure.Mm(automaticDoor.OpeningMm) + " x 2000" + handing,
                    "dim-door",
                    Figure.Mm(automaticDoor.OpeningMm));
                Finish(f, front + 31);
                return f;
            }
            if (door)
            {
                Finish(f, front + 24);
                return f;
            }
            var note = f.Text("Entrance at the bottom", Pt(left - 14, front + 29), "start");
            Finish(f, note.At.Y);
            return f;
        }

        // Automatic door in plan, in the style of the plan drawing (D3): the landing door frame and the car door frame each as one
        // outline, the fixed-side jamb posts, the panels inside their frames and the bands of the parked panels, and the car front
        // returns. Positions from the door layout, so the Doors Offset and the handing are followed.
        private static void DrawAutomaticDoor(ElevatorParameters p, Figure f, FigureView v, DoorLayout l)
        {
            const double carReturn = 40;
            const double boxDepth = 70;
            const double bandInset = 4;
            double t = l.PanelThicknessMm, pw = l.PanelWidthMm, o = l.OpeningMm;
            double lz = l.LandingZoneMm, cd = l.LandingZoneMm + l.SillGapMm;
            double cdEnd = p.CarFront - carReturn;
            double sw = p.SW;
            bool right = l.StackingSide == DoorTypes.Right;
            Func<double, double> m = x => right ? x : sw - x;
            Action<double, double, double, double, FigureLayer, string> rect =
                (x0, x1, d0, d1, layer, tag) =>
                {
                    double a = Math.Min(x0, x1), b = Math.Max(x0, x1);
                    f.Poly(Map(v, new[] { Pt(a, d0), Pt(b, d0), Pt(b, d1), Pt(a, d1) }), true, layer, tag);
                };
            // Band of a parked panel: from the closed end of the panel to the end of the frame.
            Action<double, double, double, double, string> band =
                (xClosed, xEnd, d0, d1, tag) =>
                {
                    if (Math.Abs(xEnd - xClosed) >= 2)
                        rect(xClosed, xEnd, d0, d1, FigureLayer.Thin, tag);
                };
            // Car front returns: from the car side wall to the opening edge.
            double carLeft = p.X("carLeft"), carRight = p.X("carRight");
            rect(carLeft, l.OpeningLeftX, p.CarFront - carReturn, p.CarFront, FigureLayer.Outline, "door-car-return-left");
            rect(l.OpeningRightX, carRight, p.CarFront - carReturn, p.CarFront, FigureLayer.Outline, "door-car-return-right");
            if (l.Type == DoorTypes.Automatic2S)
            {
                double uf = right ? l.OpeningLeftX : sw - l.OpeningRightX, us = uf + o;
                double ul = right ? l.LandingLeftX : sw - l.LandingRightX, ur = right ? l.LandingRightX : sw - l.LandingLeftX;
                double ot = Math.Max(0, 2 * pw - o - l.PanelOverlapMm);
                // Frames: one rectangle each, from the outer edge of the fixed-side jamb to the end of the stacking pocket.
                rect(m(ul), m(ur), 0, lz, FigureLayer.Outline, "door-frame-landing");
                rect(m(ul), m(ur), cd, cdEnd, FigureLayer.Outline, "door-frame-car");
                if (uf - ul > 0.5)
                {
                    rect(m(ul), m(uf), 0, lz, FigureLayer.Secondary, "door-jamb-post-landing");
                    rect(m(ul), m(uf), cd, cdEnd, FigureLayer.Secondary, "door-jamb-post-car");
                }
                rect(m(uf), m(uf + pw), lz - t, lz, FigureLayer.Secondary, "door-landing-panel-1");
                rect(m(us + ot - pw), m(us + ot), lz - 2 * t - 5, lz - t - 5, FigureLayer.Secondary, "door-landing-panel-2");
                rect(m(uf), m(uf + pw), cd, cd + t, FigureLayer.Secondary, "door-car-panel-1");
                rect(m(us + ot - pw), m(us + ot), cd + t + 5, cd + 2 * t + 5, FigureLayer.Secondary, "door-car-panel-2");
                double end = ur - bandInset;
                band(m(uf + pw), m(end), lz - t, lz, "door-landing-band-1");
                band(m(us + ot), m(end), lz - 2 * t - 5, lz - t - 5, "door-landing-band-2");
                band(m(uf + pw), m(end), cd, cd + t, "door-car-band-1");
                band(m(us + ot), m(end), cd + t + 5, cd + 2 * t + 5, "door-car-band-2");
            }
            else
            {
                // Boxes across both stacking sides, centred on the opening (the Doors Offset moves them with it).
                rect(l.LandingLeftX, l.LandingRightX, Math.Max(0, lz - boxDepth), lz, FigureLayer.Outline, "door-frame-landing");
                rect(l.LandingLeftX, l.LandingRightX, cd, cdEnd, FigureLayer.Outline, "door-frame-car");
                double dl = lz - Math.Min(5, Math.Max(0, (lz - t) / 2)) - t;
                double dc = cd + Math.Min(12, Math.Max(0, (cdEnd - cd - t) / 2));
                rect(l.OpeningLeftX, l.OpeningLeftX + pw, dl, dl + t, FigureLayer.Secondary, "door-landing-panel-1");
                rect(l.OpeningRightX - pw, l.OpeningRightX, dl, dl + t, FigureLayer.Secondary, "door-landing-panel-2");
                rect(l.OpeningLeftX, l.OpeningLeftX + pw, dc, dc + t, FigureLayer.Secondary, "door-car-panel-1");
                rect(l.OpeningRightX - pw, l.OpeningRightX, dc, dc + t, FigureLayer.Secondary, "door-car-panel-2");
                double e0 = l.LandingLeftX + bandInset, e1 = l.LandingRightX - bandInset;
                band(l.OpeningLeftX, e0, dl, dl + t, "door-landing-band-1");
                band(l.OpeningRightX, e1, dl, dl + t, "door-landing-band-2");
                band(l.OpeningLeftX, e0, dc, dc + t, "door-car-band-1");
                band(l.OpeningRightX, e1, dc, dc + t, "door-car-band-2");
            }
        }

        private static void DrawWalls(ElevatorParameters p, Figure f, FigureView v, double dl, double dr, bool door)
        {
            var bands = new List<double[]> { new[] { -Wall, p.SD, p.SW + Wall, p.SD + Wall },
                new[] { -Wall, 0, 0, p.SD },
                new[] { p.SW, 0, p.SW + Wall, p.SD } };
            if (door)
            {
                bands.Add(new[] { -Wall, -Wall, dl, 0 });
                bands.Add(new[] { dr, -Wall, p.SW + Wall, 0 });
            }
            else
                bands.Add(new[] { -Wall, -Wall, p.SW + Wall, 0 });
            foreach (var band in bands)
            {
                var poly = Map(v, new[] { Pt(band[0], band[1]), Pt(band[2], band[1]), Pt(band[2], band[3]), Pt(band[0], band[3]) });
                f.Hatch(poly);
            }
            if (door)
            {
                f.Poly(Map(v, new[]
                {
                    Pt(dl, -Wall),
                    Pt(-Wall, -Wall),
                    Pt(-Wall, p.SD + Wall),
                    Pt(p.SW + Wall, p.SD + Wall),
                    Pt(p.SW + Wall, -Wall),
                    Pt(dr, -Wall)
                }), false, FigureLayer.Thin);
                f.Poly(
                    Map(v, new[] { Pt(dl, 0), Pt(0, 0), Pt(0, p.SD), Pt(p.SW, p.SD), Pt(p.SW, 0), Pt(dr, 0) }),
                    false,
                    FigureLayer.Outline,
                    "shaft").Value = Figure.Mm(p.SW) + "," + Figure.Mm(p.SD);
                f.Line(v.P(dl, -Wall), v.P(dl, 0), FigureLayer.Outline, "door-jamb-left").Value = Figure.Mm(dl);
                f.Line(v.P(dr, -Wall), v.P(dr, 0), FigureLayer.Outline, "door-jamb-right").Value = Figure.Mm(dr);
                f.Text(
                    "ENTRANCE",
                    v.P(p.Center, -Wall / 2) + Pt(0, 1.2),
                    "middle",
                    FigureLayer.Text,
                    2.5,
                    false,
                    "entrance",
                    Figure.Mm(p.DoorWidth));
            }
            else
            {
                f.Poly(
                    Map(v, new[] { Pt(-Wall, -Wall), Pt(p.SW + Wall, -Wall), Pt(p.SW + Wall, p.SD + Wall), Pt(-Wall, p.SD + Wall) }),
                    true,
                    FigureLayer.Thin);
                f.Poly(Map(v, new[] { Pt(0, 0), Pt(p.SW, 0), Pt(p.SW, p.SD), Pt(0, p.SD) }), true, FigureLayer.Outline, "shaft").Value =
                    Figure.Mm(p.SW) + "," + Figure.Mm(p.SD);
            }
        }

        private static void DrawRailLabels(
            ElevatorParameters p,
            IRailSection carRail,
            IRailSection cwRail,
            Figure f,
            FigureView v,
            FigurePolyline carRight,
            FigurePolyline cwRight)
        {
            double lx = v.P(p.SW + Wall + 60, 0).X + 1.5;
            Action<FigurePolyline, double, string, string> leader =
                (rail, y, text, tag) =>
                {
                    var c = Pt(rail.Points.Average(q => q.X), rail.Points.Average(q => q.Y));
                    f.Poly(
                new[] { c, Pt(lx - 3, y - 0.9), Pt(lx - 0.8, y - 0.9) },
                false,
                FigureLayer.Leader,
                tag + "-leader");
                    f.Text(text, Pt(lx, y), "start", FigureLayer.Text, 2.5, false, tag, null);
                };
            leader(carRight, v.P(0, p.CarRow).Y - 5, "car rail " + carRail.Designation, "car-rail-label");
            if (cwRight != null)
                leader(cwRight, v.P(0, p.CwRow).Y + 1, "CW rail " + cwRail.Designation, "cw-rail-label");
        }

        private static void DrawShaftDimensions(
            ElevatorParameters p,
            IRailSection cwRail,
            Figure f,
            FigureView v,
            double cl,
            double cr,
            double front,
            double rear,
            double left)
        {
            f.DimH(v.P(cl, p.CarFront), v.P(cr, p.CarFront), front + 7, "W " + Figure.Mm(p.CarWidth), "dim-W", Figure.Mm(p.CarWidth));
            f.DimH(
                v.P(p.X("railLeft"), p.CarRow),
                v.P(p.X("railRight"), p.CarRow),
                front + 14,
                "car DBG " + Figure.Mm(p.CarDbg),
                "dim-DBG",
                Figure.Mm(p.CarDbg));
            f.DimH(v.P(0, 0), v.P(p.SW, 0), front + 21, "SW " + Figure.Mm(p.SW), "dim-SW", Figure.Mm(p.SW));
            if (cwRail != null)
            {
                f.DimH(
                                    v.P(p.X("cwRailLeft"), p.CwRow),
                                    v.P(p.X("cwRailRight"), p.CwRow),
                                    rear - 6,
                                    "CW DBG " + Figure.Mm(p.CwDbg),
                                    "dim-CWDBG",
                                    Figure.Mm(p.CwDbg));
            }
            f.DimV(v.P(cl, p.CarFront), v.P(cl, p.CarRear), left - 7, "D " + Figure.Mm(p.CarDepth), "dim-D", Figure.Mm(p.CarDepth));
            f.DimV(v.P(0, 0), v.P(0, p.SD), left - 14, "SD " + Figure.Mm(p.SD), "dim-SD", Figure.Mm(p.SD));
        }

        // ---------- F2 chassis plan ----------
        /// <summary>Figure F2: plan of the chassis bearings with reactions and loads.</summary>
        public static Figure ChassisPlan(ChassisCalculationResult r, bool basic)
        {
            var f = new Figure { Id = "F2", Title = "Machine-room bearing reactions P1–P4 — plan", Schematic = false };
            var seats = r.Reactions.ToDictionary(b => b.Id, b => b.PositionMm);
            var pts = seats.Values.Select(q => Pt(q.X, q.Y)).ToList();
            if (basic)
                pts.Add(Pt(r.SimplifiedGeometry.LoadReference.X, r.SimplifiedGeometry.LoadReference.Y));
            else
                pts.AddRange(r.Loads.Select(l => Pt(l.PositionMm.X, l.PositionMm.Y)));
            double minX = pts.Min(q => q.X), maxX = pts.Max(q => q.X), minY = pts.Min(q => q.Y), maxY = pts.Max(q => q.Y);
            double pad = 0.08 * Math.Max(maxX - minX, maxY - minY);
            var v = new FigureView(minX - pad, minY - pad, maxX + pad, maxY + pad, 38, 16, 90, 90);
            f.View = v;
            Func<string, FigurePoint> S = id => v.P(seats[id].X, seats[id].Y);
            f.Poly(new[] { S("P4"), S("P2"), S("P1"), S("P3") }, true, FigureLayer.Secondary, "chassis");
            DrawReactionLabels(r, f, S);
            double top = pts.Select(q => v.P(q.X, q.Y).Y).Min(), bottom = pts.Select(q => v.P(q.X, q.Y).Y).Max(), leftX =
                pts.Select(q => v.P(q.X, q.Y).X).Min(), rightX = pts.Select(q => v.P(q.X, q.Y).X).Max();
            if (basic)
            {
                DrawBasicLoad(r, f, v, S, top, bottom, leftX, rightX);
            }
            else
            {
                // Load labels outside the chassis outline, stacked top to bottom, each joined to its point by a leader.
                DrawLoadLabels(r, f, seats, v, S, top, bottom, leftX, rightX);
            }
            Axes(f, Pt(8, bottom + 20));
            var note = f.Text("Plan on the machine room axes, entrance at the bottom; upward reactions", Pt(24, bottom + 22), "start");
            Finish(f, note.At.Y);
            return f;
        }

        private static void DrawReactionLabels(ChassisCalculationResult r, Figure f, Func<string, FigurePoint> S)
        {
            foreach (var b in r.Reactions)
            {
                var c = S(b.Id);
                f.Rect(c.X - 1.6, c.Y - 1.6, c.X + 1.6, c.Y + 1.6, FigureLayer.Outline, "seat-" + b.Id).Value =
                    Figure.Mm(b.PositionMm.X) + "," + Figure.Mm(b.PositionMm.Y);
                bool right = b.Id == "P1" || b.Id == "P2", rear = b.Id == "P1" || b.Id == "P3";
                // The kg line stands under the value: for the rear row the value moves one line up, so both lines stay clear of the seat.
                var at = c + Pt(right ? 3.5 : -3.5, rear ? -6.9 : 6);
                f.Text(
                    b.Id + " " + Figure.Kn(b.UpwardN),
                    new[] { at, at + Pt(0, rear ? -3.4 : 3.4), at + Pt(0, rear ? -6.8 : 6.8) },
                    right ? "start" : "end",
                    FigureLayer.LoadText,
                    2.5,
                    false,
                    "reaction-" + b.Id,
                    Figure.Num(b.UpwardN / 1000, "0.###"));
            }
        }

        private static void DrawBasicLoad(
            ChassisCalculationResult r,
            Figure f,
            FigureView v,
            Func<string, FigurePoint> S,
            double top,
            double bottom,
            double leftX,
            double rightX)
        {
            var g = r.SimplifiedGeometry;
            var w = v.P(g.LoadReference.X, g.LoadReference.Y);
            f.Line(w + Pt(-8, 0), w + Pt(8, 0), FigureLayer.Centre);
            f.Line(w + Pt(0, -8), w + Pt(0, 8), FigureLayer.Centre);
            LoadPoint(f, w, "load-W");
            // Labels outside the chassis outline with a leader, so no text crosses a member line.
            f.Line(w + Pt(1.6, 0), Pt(rightX + 3, w.Y), FigureLayer.Leader);
            f.Text(
                "W " + Figure.Kn(r.TotalVerticalLoadN.Value),
                Pt(rightX + 4, w.Y - 1),
                "start",
                FigureLayer.LoadText,
                2.5,
                false,
                "load-W-text",
                Figure.Num(r.TotalVerticalLoadN.Value / 1000, "0.###"));
            f.Text("traction sheave centre", Pt(rightX + 4, w.Y + 3.6), "start");
            // a and b chain on the left, L on the right, s(f) below, s(r) above.
            double x = leftX - 14;
            f.DimV(
                v.P(g.LoadReference.X, g.FrontRowY),
                v.P(g.LoadReference.X, g.LoadReference.Y),
                x,
                "a " + Figure.Mm(g.LoadFromFrontMm),
                "dim-a",
                Figure.Mm(g.LoadFromFrontMm));
            f.DimV(
                v.P(g.LoadReference.X, g.LoadReference.Y),
                v.P(g.LoadReference.X, g.RearRowY),
                x,
                "b " + Figure.Mm(g.LoadToRearMm),
                "dim-b",
                Figure.Mm(g.LoadToRearMm));
            f.DimV(S("P2"), S("P1"), rightX + 38, "L " + Figure.Mm(g.LongitudinalSpanMm), "dim-L", Figure.Mm(g.LongitudinalSpanMm));
            f.DimH(
                S("P4"),
                S("P2"),
                bottom + 12,
                "s(f) " + Figure.Mm(g.FrontTransverseSpanMm),
                "dim-sf",
                Figure.Mm(g.FrontTransverseSpanMm));
            f.DimH(
                S("P3"),
                S("P1"),
                top - 10,
                "s(r) " + Figure.Mm(g.RearTransverseSpanMm),
                "dim-sr",
                Figure.Mm(g.RearTransverseSpanMm));
        }

        private static void DrawLoadLabels(
            ChassisCalculationResult r,
            Figure f,
            Dictionary<string, LocalPoint> seats,
            FigureView v,
            Func<string, FigurePoint> S,
            double top,
            double bottom,
            double leftX,
            double rightX)
        {
            double labelY = top + 4;
            foreach (var l in r.Loads.OrderByDescending(q => q.PositionMm.Y))
            {
                var c = v.P(l.PositionMm.X, l.PositionMm.Y);
                LoadPoint(f, c, "load-" + l.Id);
                string name = l.Id == "MachineWeight" ? "machine weight" : l.Id == "TractionRopeAction"
                    ? "rope forces at traction sheave"
                    : l.Id == "DeflectorHangerAction" ? "rope forces at deflector hanger" : l.Id;
                labelY = Math.Max(labelY, c.Y);
                var at = Pt(rightX + 6, labelY);
                f.Poly(new[] { c + Pt(1.6, 0), Pt(rightX + 2, labelY - 0.9), Pt(rightX + 5, labelY - 0.9) }, false, FigureLayer.Leader);
                f.Text(
                    name + " Fz " + Figure.Kn(-l.ForceN.Z),
                    at,
                    "start",
                    FigureLayer.LoadText,
                    2.5,
                    false,
                    "load-" + l.Id + "-text",
                    Figure.Num(-l.ForceN.Z / 1000, "0.###"));
                labelY += 4.6;
            }
            f.DimV(
                S("P4"),
                S("P3"),
                leftX - 14,
                "L " + Figure.Mm(seats["P1"].Y - seats["P2"].Y),
                "dim-L",
                Figure.Mm(seats["P1"].Y - seats["P2"].Y));
            f.DimH(
                S("P4"),
                S("P2"),
                bottom + 12,
                "s(f) " + Figure.Mm(seats["P2"].X - seats["P4"].X),
                "dim-sf",
                Figure.Mm(seats["P2"].X - seats["P4"].X));
            f.DimH(
                S("P3"),
                S("P1"),
                top - 10,
                "s(r) " + Figure.Mm(seats["P1"].X - seats["P3"].X),
                "dim-sr",
                Figure.Mm(seats["P1"].X - seats["P3"].X));
        }

        // ---------- F3 chassis elevation / free body (Basic) ----------
        /// <summary>Figure F3: free body of the chassis (Basic).</summary>
        public static Figure ChassisElevation(ChassisCalculationResult r)
        {
            var f = new Figure { Id = "F3", Title = "Machine-room bearing reactions — free body", Schematic = true };
            var g = r.SimplifiedGeometry;
            double span = g.LongitudinalSpanMm, scale = 120 / span, x0 = 22, beam = 34;
            Func<double, double> X = y => x0 + (y - g.FrontRowY) * scale;
            double xf = X(g.FrontRowY), xr = X(g.RearRowY), xw = X(g.LoadReference.Y);
            f.Line(Pt(xf - 6, beam), Pt(xr + 6, beam), FigureLayer.Outline, "beam");
            foreach (var x in new[] { xf, xr })
            {
                f.Poly(new[] { Pt(x, beam), Pt(x - 3, beam + 5), Pt(x + 3, beam + 5) }, true, FigureLayer.Secondary);
                f.Line(Pt(x - 4.5, beam + 5), Pt(x + 4.5, beam + 5), FigureLayer.Secondary);
            }
            f.Load(
                Pt(xw, beam - 22),
                Pt(xw, beam),
                "W " + Figure.Kn(r.TotalVerticalLoadN.Value),
                new[] { Pt(xw + 2, beam - 18), Pt(xw + 2, beam - 14) },
                "start",
                "load-W",
                Figure.Num(r.TotalVerticalLoadN.Value / 1000, "0.###"));
            f.Load(
                Pt(xf, beam + 24),
                Pt(xf, beam + 6),
                "front row (P2 + P4) " + Figure.Kn(g.FrontLoadN),
                new[] { Pt(xf + 2, beam + 22), Pt(xf + 2, beam + 26) },
                "start",
                "reaction-front",
                Figure.Num(g.FrontLoadN / 1000, "0.###"));
            f.Load(
                Pt(xr, beam + 24),
                Pt(xr, beam + 6),
                "rear row (P1 + P3) " + Figure.Kn(g.RearLoadN),
                new[] { Pt(xr - 2, beam + 22), Pt(xr - 2, beam + 26) },
                "end",
                "reaction-rear",
                Figure.Num(g.RearLoadN / 1000, "0.###"));
            f.Text("front", Pt(xf, beam - 3), "middle");
            f.Text("rear", Pt(xr, beam - 3), "middle");
            double d = beam + 34;
            f.DimH(Pt(xf, beam + 8), Pt(xw, beam + 8), d, "a " + Figure.Mm(g.LoadFromFrontMm), "dim-a", Figure.Mm(g.LoadFromFrontMm));
            f.DimH(Pt(xw, beam + 2), Pt(xr, beam + 8), d, "b " + Figure.Mm(g.LoadToRearMm), "dim-b", Figure.Mm(g.LoadToRearMm));
            f.DimH(Pt(xf, beam + 8), Pt(xr, beam + 8), d + 8, "L " + Figure.Mm(span), "dim-L", Figure.Mm(span));
            var note = f.Text(
                "Side view along the machine room Y axis (entrance on the left); horizontal distances to scale",
                Pt(x0 - 6, d + 16),
                "start");
            Finish(f, note.At.Y);
            return f;
        }

        // ---------- F4 FF load cases, plan ----------
        // Load offset convention (statics; forces drawn on the rail, from the shoe). Rated load on half the floor, resultant at
        // ex = ff1Side·W/4 (FF1) or ey = ff2Side·D/4 (FF2) from the car centre. The shoe couple balances the load moment:
        // FF1: upper level pushes the rail on the loaded side outward, lower level the opposite rail outward.
        // FF2: upper level pushes both rails towards the loaded side, lower level the other way.
        // The sheet uses +1 / +1 (load towards +X, right, and +Y, rear); tests also build −1.
        /// <summary>Figure F4: load cases FF1 and FF2 in plan.</summary>
        public static Figure GuideForcePlan(
            ElevatorParameters p,
            CarGuideForceResult ff,
            IRailSection rail,
            int ff1Side = 1,
            int ff2Side = 1)
        {
            int s1 = Math.Sign(ff1Side) < 0 ? -1 : 1, s2 = Math.Sign(ff2Side) < 0 ? -1 : 1;
            var f = new Figure { Id = "F4", Title = "Car guide-rail forces FF1 / FF2 — load cases (plan)", Schematic = false };
            double W =
                ff.InternalWidthMm.Value, D = ff.InternalDepthMm.Value, dbg = p.CarDbg / 2, ry = p.CarRow - (p.CarFront + p.CarRear) / 2;
            double minX = -dbg - rail.HeightMm - 60, maxX = dbg + rail.HeightMm + 60;
            for (int panel = 0; panel < 2; panel++)
            {
                DrawGuideForcePanel(ff, rail, s1, s2, f, W, D, dbg, ry, minX, maxX, panel);
            }
            double noteY = f.Entities.OfType<FigurePolyline>().SelectMany(e => e.Points).Max(q => q.Y) + 8.5;
            f.Text("Hatched: rated load on half the car floor", Pt(4, noteY), "start");
            f.Text(
                "FF1 / FF2: force per guide rail at one guide shoe level",
                Pt(4, noteY + 3.8),
                "start",
                FigureLayer.Text,
                2.5,
                false,
                "ff-note",
                null);
            var note = f.Text(
                "Directions reverse between the upper and lower shoe levels",
                Pt(4, noteY + 7.6),
                "start",
                FigureLayer.Text,
                2.5,
                false,
                "ff-note-2",
                null);
            Finish(f, note.At.Y);
            return f;
        }

        private static void DrawGuideForcePanel(
            CarGuideForceResult ff,
            IRailSection rail,
            int s1,
            int s2,
            Figure f,
            double W,
            double D,
            double dbg,
            double ry,
            double minX,
            double maxX,
            int panel)
        {
            bool ff1 = panel == 0;
            double left = panel * 82.5;
            var v = new FigureView(minX, -D / 2, maxX, D / 2, left + 23.75, 19, 35, 84, true);
            if (panel == 0)
                f.View = v;
            f.Text(
                ff1 ? "Load case FF1" : "Load case FF2",
                Pt(left + 41.25, 9),
                "middle",
                FigureLayer.Text,
                3.5,
                false,
                null,
                null,
                true);
            // Eccentric half-area load: resultant W/4 (FF1) or D/4 (FF2) from the car centre.
            var half = ff1
                ? new[] { Pt(0, -D / 2), Pt(s1 * W / 2, -D / 2), Pt(s1 * W / 2, D / 2), Pt(0, D / 2) }
                : new[] { Pt(-W / 2, 0), Pt(W / 2, 0), Pt(W / 2, s2 * D / 2), Pt(-W / 2, s2 * D / 2) };
            f.Hatch(Map(v, half), 2.0);
            f.Poly(
                Map(v, new[] { Pt(-W / 2, -D / 2), Pt(W / 2, -D / 2), Pt(W / 2, D / 2), Pt(-W / 2, D / 2) }),
                true,
                FigureLayer.Outline,
                ff1 ? "car-ff1" : "car-ff2").Value = Figure.Mm(W) + "," + Figure.Mm(D);
            f.Line(v.P(-W / 2 - 80, 0), v.P(W / 2 + 80, 0), FigureLayer.Centre);
            f.Line(v.P(0, -D / 2 - 80), v.P(0, D / 2 + 80), FigureLayer.Centre);
            // Car-rail line, rails and guide shoes.
            f.Line(v.P(minX + 20, ry), v.P(maxX - 20, ry), FigureLayer.Centre, "car-rail-line").Value = Figure.Mm(ry);
            DrawRailsAndShoes(rail, f, dbg, ry, v);
            var q = ff1 ? v.P(s1 * W / 4, 0) : v.P(0, s2 * D / 4);
            LoadPoint(f, q, ff1 ? "resultant-ff1" : "resultant-ff2");
            DrawRailForces(ff, s1, s2, f, dbg, ry, ff1, v);
            DrawGuideForceDimensions(s1, s2, f, W, D, maxX, ff1, left, v);
        }

        private static void DrawRailsAndShoes(IRailSection rail, Figure f, double dbg, double ry, FigureView v)
        {
            foreach (int side in new[] { -1, 1 })
            {
                f.Poly(
                    Map(v, RailPlan(rail, side * dbg, ry, -side)),
                    true,
                    FigureLayer.Outline,
                    side < 0 ? "rail-left" : "rail-right",
                    true).Value = Figure.Mm(side * dbg);
                var tip = v.P(side * dbg, ry);
                f.Rect(
                    Math.Min(tip.X, tip.X - side * 3.2),
                    tip.Y - 3.8,
                    Math.Max(tip.X, tip.X - side * 3.2),
                    tip.Y + 3.8,
                    FigureLayer.Secondary,
                    side < 0 ? "shoe-left" : "shoe-right");
            }
        }

        private static void DrawRailForces(CarGuideForceResult ff, int s1, int s2, Figure f, double dbg, double ry, bool ff1, FigureView v)
        {
            double fv = ff1 ? ff.FF1DesignN.Value : ff.FF2DesignN.Value;
            string name = ff1 ? "FF1 " : "FF2 ";
            foreach (int side in new[] { -1, 1 })
            {
                var tip = v.P(side * dbg, ry);
                string tag = "force-" + (ff1 ? "ff1" : "ff2") + (side < 0 ? "-left" : "-right"), anchor = side < 0 ? "end" : "start";
                // FF1: a couple between the two rails, both outward: upper level on the loaded side, lower level on the other.
                // FF2: both rails at the upper level, towards the loaded side; the forces reverse at the lower level.
                string level = ff1 ? (side == s1 ? "upper level" : "lower level") : "upper level";
                if (ff1)
                {
                    f.Load(
                        tip + Pt(-side * 10, 3.0),
                        tip + Pt(-side * 3.4, 3.0),
                        name + Figure.Kn(fv),
                        new[] { tip + Pt(side * 3, -3.5), tip + Pt(side * 3, -7) },
                        anchor,
                        tag,
                        Figure.Num(fv / 1000, "0.###"));
                    f.Text(
                        level,
                        new[] { tip + Pt(side * 3, 5.2), tip + Pt(side * 3, 8.6) },
                        anchor,
                        FigureLayer.LoadText,
                        2.5,
                        false,
                        "level-ff1" + (side < 0 ? "-left" : "-right"),
                        level);
                }
                else
                {
                    double u = s2;
                    f.Load(
                        tip + Pt(side * 1.6, u * 15),
                        tip + Pt(side * 1.6, u * 4.2),
                        name + Figure.Kn(fv),
                        new[] { tip + Pt(side * 3.2, s2 > 0 ? 11 : -15), tip + Pt(side * 3.2, s2 > 0 ? 14 : -18) },
                        anchor,
                        tag,
                        Figure.Num(fv / 1000, "0.###"));
                    f.Text(
                        level,
                        new[] { tip + Pt(side * 3.2, s2 > 0 ? 15 : -11.5), tip + Pt(side * 3.2, s2 > 0 ? 18 : -8.5) },
                        anchor,
                        FigureLayer.LoadText,
                        2.5,
                        false,
                        "level-ff2" + (side < 0 ? "-left" : "-right"),
                        level);
                }
            }
        }

        private static void DrawGuideForceDimensions(
            int s1,
            int s2,
            Figure f,
            double W,
            double D,
            double maxX,
            bool ff1,
            double left,
            FigureView v)
        {
            double top = v.P(0, D / 2).Y, bottom = v.P(0, -D / 2).Y;
            if (ff1)
            {
                f.DimH(
                                        v.P(Math.Min(0, s1 * W / 4), D / 2 + 80),
                                        v.P(Math.Max(0, s1 * W / 4), D / 2),
                                        top - 4,
                                        "W/4 " + Figure.Mm(W / 4),
                                        "dim-W4",
                                        Figure.Mm(W / 4));
            }
            else
            {
                f.DimV(
                                        v.P(W / 2 + 80, Math.Min(0, s2 * D / 4)),
                                        v.P(W / 2 + 80, Math.Max(0, s2 * D / 4)),
                                        v.P(maxX, 0).X + 3,
                                        "D/4 " + Figure.Mm(D / 4),
                                        "dim-D4",
                                        Figure.Mm(D / 4));
            }
            if (ff1)
                f.Line(v.P(s1 * W / 4, 0), v.P(s1 * W / 4, D / 2 + 80), FigureLayer.Centre);
            else
                f.Line(v.P(0, s2 * D / 4), v.P(W / 2 + 80, s2 * D / 4), FigureLayer.Centre);
            f.DimH(v.P(-W / 2, -D / 2), v.P(W / 2, -D / 2), bottom + 8, "W " + Figure.Mm(W), ff1 ? "dim-W" : "dim-W-2", Figure.Mm(W));
            if (!ff1)
            {
                f.Text(
                                        "forces reverse at the lower level",
                                        Pt(left + 41.25, bottom + 15),
                                        "middle",
                                        FigureLayer.LoadText,
                                        2.5,
                                        false,
                                        "ff2-reverse-note",
                                        null);
            }
        }

        // ---------- F5 guide-shoe forces: (a) front elevation with both rails, (b) side view of one rail (schematic) ----------
        // Every force is drawn ON a rail: the shoe pushes the rail blade (tip in FF1, side face in FF2); none points away from a rail.
        /// <summary>Upper guide-shoe level of figure F5, paper mm.</summary>
        public const double F5UpperY = 30;
        /// <summary>Lower guide-shoe level of figure F5, paper mm.</summary>
        public const double F5LowerY = 80;
        /// <summary>Left rail face of figure F5, paper mm.</summary>
        public const double F5LeftRailFace = 24;
        /// <summary>Right rail face of figure F5, paper mm.</summary>
        public const double F5RightRailFace = 84;
        /// <summary>Front rail face of the side view of figure F5, paper mm.</summary>
        public const double F5SideFrontFace = 132;
        /// <summary>Rear rail face of the side view of figure F5, paper mm.</summary>
        public const double F5SideRearFace = 136;
        /// <summary>Position of the split between the two views of figure F5, paper mm.</summary>
        public const double F5ViewSplitX = 104;

        /// <summary>Figure F5: guide-shoe forces in front and side elevation.</summary>
        public static Figure GuideForceElevation(CarGuideForceResult ff, int ff1Side = 1, int ff2Side = 1)
        {
            int s1 = Math.Sign(ff1Side) < 0 ? -1 : 1, s2 = Math.Sign(ff2Side) < 0 ? -1 : 1;
            var f = new Figure { Id = "F5", Title = "Guide-shoe forces — front and side elevations", Schematic = true };
            double h =
                ff.GuideShoeSpacingMm.Value, yu = F5UpperY, yl = F5LowerY, xl = F5LeftRailFace, xr = F5RightRailFace, top = 20, bottom = 90;
            string ff1 = Figure.Kn(ff.FF1DesignN.Value), ff2 = Figure.Kn(ff.FF2DesignN.Value), v1 =
                Figure.Num(ff.FF1DesignN.Value / 1000, "0.###"), v2 = Figure.Num(ff.FF2DesignN.Value / 1000, "0.###");
            // (a) Front elevation, seen from the entrance: both car rails, car frame between them.
            DrawFrontView(s1, f, h, yu, yl, xl, xr, top, bottom, ff1, v1);
            // (b) Side view of one car rail, seen along the car rail line: blade side faces (front left, rear right).
            DrawSideView(s2, f, h, yu, yl, top, bottom, ff2, v2);
            f.Line(Pt(F5ViewSplitX, 4), Pt(F5ViewSplitX, bottom + 8), FigureLayer.Thin, "view-split");
            var note = f.Text(
                "FF1 and FF2 act as couples between the upper and lower guide shoes; h centre to centre of the shoes",
                Pt(4, bottom + 12),
                "start");
            Finish(f, note.At.Y);
            return f;
        }

        private static void DrawFrontView(
            int s1,
            Figure f,
            double h,
            double yu,
            double yl,
            double xl,
            double xr,
            double top,
            double bottom,
            string ff1,
            string v1)
        {
            f.Text("a) Front elevation", Pt(54, 8), "middle", FigureLayer.Text, 3.5, false, null, null, true);
            f.Text("seen from the entrance", Pt(54, 13), "middle");
            f.Rect(xl - 3.5, top, xl, bottom, FigureLayer.Outline, "rail-left");
            f.Rect(xr, top, xr + 3.5, bottom, FigureLayer.Outline, "rail-right");
            f.Rect(xl + 6, top + 3, xr - 6, bottom - 3, FigureLayer.Secondary, "car-frame");
            foreach (var y in new[] { yu, yl })
            {
                f.Rect(xl + 0.4, y - 2.5, xl + 6, y + 2.5, FigureLayer.Outline, y == yu ? "shoe-left-upper" : "shoe-left-lower");
                f.Rect(xr - 6, y - 2.5, xr - 0.4, y + 2.5, FigureLayer.Outline, y == yu ? "shoe-right-upper" : "shoe-right-lower");
            }
            f.Line(Pt(xl - 8, yu), Pt(xr + 8, yu), FigureLayer.Centre, "level-upper");
            f.Line(Pt(xl - 8, yl), Pt(xr + 8, yl), FigureLayer.Centre, "level-lower");
            // Rated load offset towards the s1 side (schematic position, W/4 from the car centre).
            double cx = (xl + xr) / 2, qx = cx + s1 * 14;
            f.Load(
                Pt(qx, yu + 8),
                Pt(qx, yu + 22),
                "Q at W/4",
                new[] { Pt(qx - s1 * 1.5, yu + 15), Pt(qx - s1 * 1.5, yu + 19) },
                s1 > 0 ? "end" : "start",
                "load-q-ff1",
                Figure.Num(s1, "0"));
            // FF1 couple, both outward: the upper shoe pushes the rail on the loaded side, the lower shoe the opposite rail.
            foreach (var lv in new[]
            {
                new
                {
                    Side = s1,
                    Y = yu,
                    Tag = "force-ff1-upper",
                    Dy = -2.6,
                    Dy2 = -6.0
                },
                new
                {
                    Side = -s1,
                    Y = yl,
                    Tag = "force-ff1-lower",
                    Dy = 5.2,
                    Dy2 = 8.6
                }
            })
            {
                double face = lv.Side < 0 ? xl : xr;
                f.Load(
                    Pt(face - lv.Side * 16, lv.Y),
                    Pt(face, lv.Y),
                    "FF1 " + ff1,
                    new[] { Pt(face - lv.Side * 8, lv.Y + lv.Dy), Pt(face - lv.Side * 8, lv.Y + lv.Dy2) },
                    lv.Side < 0 ? "start" : "end",
                    lv.Tag,
                    v1);
            }
            f.Text("upper shoe level", Pt(54, yu + 6), "middle", FigureLayer.Text, 2.5, false, "label-upper", null);
            f.Text("lower shoe level", Pt(54, yl - 3.6), "middle", FigureLayer.Text, 2.5, false, "label-lower", null);
            f.Text("car frame", Pt(54, (yu + yl) / 2 + 1), "middle");
            f.Text("left rail", Pt(xl - 1.75, bottom + 5), "middle");
            f.Text("right rail", Pt(xr + 1.75, bottom + 5), "middle");
            f.DimV(Pt(xl - 3.5, yu), Pt(xl - 3.5, yl), xl - 14, "h " + Figure.Mm(h), "dim-h", Figure.Mm(h));
        }

        private static void DrawSideView(int s2, Figure f, double h, double yu, double yl, double top, double bottom, string ff2, string v2)
        {
            double fx = F5SideFrontFace, rx = F5SideRearFace;
            f.Text("b) Side view of one rail", Pt(134, 8), "middle", FigureLayer.Text, 3.5, false, null, null, true);
            f.Text("seen along the car rail line", Pt(134, 13), "middle");
            f.Rect(fx, top, rx, bottom, FigureLayer.Outline, "rail-side");
            foreach (var y in new[] { yu, yl })
                f.Rect(fx - 3.5, y - 2.5, rx + 3.5, y + 2.5, FigureLayer.Secondary, y == yu ? "shoe-side-upper" : "shoe-side-lower");
            f.Line(Pt(fx - 12, yu), Pt(rx + 12, yu), FigureLayer.Centre);
            f.Line(Pt(fx - 12, yl), Pt(rx + 12, yl), FigureLayer.Centre);
            // FF2 couple on the same rail: upper shoe towards the loaded side (rear for s2 = +1, as in F4), lower shoe the other way.
            foreach (var lv in new[]
            {
                new
                {
                    Dir = s2,
                    Y = yu,
                    Tag = "force-ff2-upper"
                },
                new
                {
                    Dir = -s2,
                    Y = yl,
                    Tag = "force-ff2-lower"
                }
            })
            {
                double face = lv.Dir > 0 ? fx : rx;
                f.Load(
                    Pt(face - lv.Dir * 14, lv.Y),
                    Pt(face, lv.Y),
                    "FF2 " + ff2,
                    new[] { Pt(face - lv.Dir * 4.5, lv.Y - 2.6), Pt(face - lv.Dir * 4.5, lv.Y - 6) },
                    lv.Dir > 0 ? "end" : "start",
                    lv.Tag,
                    v2);
            }
            f.DimV(Pt(fx - 15, yu), Pt(fx - 3.5, yl), F5ViewSplitX + 10, "h " + Figure.Mm(h), "dim-h-side", Figure.Mm(h));
            f.Text("front", Pt(fx - 4, bottom + 5), "end");
            f.Text("rear", Pt(rx + 4, bottom + 5), "start");
        }

        // ---------- F6 rail cross-section ----------
        /// <summary>Figure F6: section of the car guide rail.</summary>
        public static Figure RailSection(IRailSection rail, CarGuideForceResult ff)
        {
            var f = new Figure { Id = "F6", Title = "Car guide rail " + rail.Designation + " — section", Schematic = rail.Proportional };
            double b = rail.BaseWidthMm, h = rail.HeightMm, k = rail.BladeWidthMm, s = Math.Min(48 / b, 48 / h), cx = 60, baseY = 76;
            Func<double, double, FigurePoint> P = (x, y) => Pt(cx + x * s, baseY - y * s);
            var outline = rail.Outline().Select(q => P(q.X, q.Y)).ToList();
            f.Hatch(outline);
            f.Poly(outline, true, FigureLayer.Outline, "rail-section").Value = rail.Designation;
            // Centroid (area-weighted) for the x-x axis.
            double area = 0, cy = 0;
            var o = rail.Outline();
            for (int i = 0; i < o.Count; i++)
            {
                var a = o[i];
                var c = o[(i + 1) % o.Count];
                double cross = a.X * c.Y - c.X * a.Y;
                area += cross;
                cy += (a.Y + c.Y) * cross;
            }
            cy /= 3 * area;
            f.Line(P(-b / 2 - 10 / s, cy), P(b / 2 + 10 / s, cy), FigureLayer.Centre, "axis-x");
            f.Text("x", P(-b / 2 - 10 / s, cy) + Pt(-1.5, 1), "end");
            f.Text("x", P(b / 2 + 10 / s, cy) + Pt(1.5, 1), "start");
            f.Line(P(0, -6 / s), P(0, h + 8 / s), FigureLayer.Centre, "axis-y");
            f.Text("y", P(0, -6 / s) + Pt(0, 3.6), "middle");
            // FF1 = Fy on the blade tip, FF2 = Fx on the blade side.
            var tip = P(0, h);
            f.Load(
                tip + Pt(0, -24),
                tip + Pt(0, -0.4),
                "FF1 (Fy) " + Figure.Kn(ff.FF1DesignN.Value),
                new[] { tip + Pt(2, -20), tip + Pt(2, -16) },
                "start",
                "force-fy",
                Figure.Num(ff.FF1DesignN.Value / 1000, "0.###"));
            var side = P(k / 2, h * 0.8);
            f.Load(
                side + Pt(26, 0),
                side + Pt(0.4, 0),
                "FF2 (Fx) " + Figure.Kn(ff.FF2DesignN.Value),
                new[] { side + Pt(9, -2.5), side + Pt(9, 5.5) },
                "start",
                "force-fx",
                Figure.Num(ff.FF2DesignN.Value / 1000, "0.###"));
            // Only b1, h1 and k are dimensioned (from the designation).
            f.DimH(P(-b / 2, 0), P(b / 2, 0), baseY + 16, "b1 " + Figure.Mm(b), "dim-b1", Figure.Mm(b));
            f.DimV(P(-b / 2, 0), P(-k / 2, h), P(-b / 2, 0).X - 10, "h1 " + Figure.Mm(h), "dim-h1", Figure.Mm(h));
            var kl = P(-k / 2, h);
            var kr = P(k / 2, h);
            double ky = kl.Y - 6;
            foreach (var q in new[] { kl, kr })
                f.Line(q + Pt(0, -0.8), Pt(q.X, ky - 1.5), FigureLayer.Dimension, "dim-k-ext");
            f.Line(Pt(kl.X - 6, ky), Pt(kl.X, ky), FigureLayer.Dimension, "dim-k");
            f.Line(Pt(kr.X, ky), Pt(kr.X + 6, ky), FigureLayer.Dimension, "dim-k");
            f.Arrowhead(Pt(kl.X, ky), Pt(1, 0), FigureLayer.Dimension);
            f.Arrowhead(Pt(kr.X, ky), Pt(-1, 0), FigureLayer.Dimension);
            f.Text(
                "k " + Figure.Mm(k),
                new[] { Pt(kl.X - 7, ky + 1), Pt(kl.X - 7, ky - 3) },
                "end",
                FigureLayer.Text,
                2.5,
                false,
                "dim-k-text",
                Figure.Mm(k));
            var label = f.Text(
                "Section " + rail.Designation,
                Pt(cx, baseY + 25),
                "middle",
                FigureLayer.Text,
                3.5,
                false,
                "designation",
                rail.Designation,
                true);
            Finish(f, label.At.Y);
            return f;
        }

        // ===================== Phase B figures F7–F9 (schematic, NTS; no dimension without a project value) =====================
        static List<FigurePoint> Arc(FigurePoint c, double r, double fromDeg, double toDeg, int n = 16)
        {
            var pts = new List<FigurePoint>();
            for (int i = 0; i <= n; i++)
            {
                double a = (fromDeg + (toDeg - fromDeg) * i / n) * Math.PI / 180;
                pts.Add(c + Pt(r * Math.Cos(a), -r * Math.Sin(a)));
            }
            return pts;
        }

        static void HatchedBand(Figure f, double x0, double y0, double x1, double y1, string tag = null)
        {
            var poly = new[] { Pt(x0, y0), Pt(x1, y0), Pt(x1, y1), Pt(x0, y1) };
            f.Hatch(poly);
            f.Poly(poly, true, FigureLayer.Secondary, tag);
        }

        static string Ratio(double? r)
        {
            return r.HasValue && r.Value > 0 ? Figure.Num(r.Value, "0.##") + ":1" : null;
        }

        // ---------- F7 suspension: 1:1 roping elevation ----------
        // Side elevation, front (car) on the left and rear (counterweight) on the right. The traction sheave stands over the car drop
        // (car rope at its front pitch edge, as in MachineRoomSectionRules: TractionX = CarDropX − 260); the deflector, when the
        // project rule selects one (MachineRoomRules.SelectDeflector(TZ).IsRequired), stands over the counterweight drop
        // (DeflectorX = CwDropX + pitch/2). Without a deflector the counterweight rope drops from the rear pitch edge of the traction
        // sheave.
        /// <summary>Car drop position of figure F7, paper mm.</summary>
        public const double F7CarDropX = 50;
        /// <summary>Traction sheave centre height of figure F7, paper mm.</summary>
        public const double F7SheaveY = 28;
        /// <summary>Traction sheave radius of figure F7, paper mm.</summary>
        public const double F7SheaveR = 9;
        /// <summary>Machine-room slab level of figure F7, paper mm.</summary>
        public const double F7SlabTop = 42;

        /// <summary>Figure F7: 1:1 roping and rope tensions.</summary>
        public static Figure Suspension(
            ChassisCalculationResult r,
            int? ropeCount,
            double? ropingRatio,
            double? sheavePitchMm,
            bool deflector)
        {
            var f = new Figure { Id = "F7", Title = "Suspension — 1:1 roping and rope tensions", Schematic = true };
            double cd = F7CarDropX, ys =
                F7SheaveY, R = F7SheaveR, tx = cd + R, slab = F7SlabTop, ds = 5, dy = ys + 7, wd = deflector ? 112 : tx + R;
            var T = Pt(tx, ys);
            var D = Pt(wd - ds, dy);
            // Machine-room slab with rope openings.
            DrawSlab(f, cd, slab, wd);
            // Traction machine: sheave on its stand.
            f.Circle(T, R, FigureLayer.Outline, false, "traction-sheave");
            f.Circle(T, 1.2, FigureLayer.Secondary);
            f.Poly(
                new[] { T + Pt(-3, 1), T + Pt(-7, slab - ys), T + Pt(7, slab - ys), T + Pt(3, 1) },
                false,
                FigureLayer.Secondary,
                "machine-stand");
            // Rope path: car hitch → front edge of the traction sheave → over the top → (deflector) → counterweight hitch.
            double carTop = 62, cwTop = deflector ? 70 : 66;
            var rope = new List<FigurePoint> { Pt(cd, carTop) };
            if (deflector)
            {
                f.Circle(D, ds, FigureLayer.Outline, false, "deflector-sheave");
                f.Circle(D, 0.9, FigureLayer.Secondary);
                var u = (D - T).Unit;
                double L = (D - T).Length, c = (R - ds) / L, q = Math.Sqrt(1 - c * c);
                // Upper external tangent (normal pointing up on paper).
                var n1 = Pt(c * u.X - q * u.Y, c * u.Y + q * u.X);
                var n2 = Pt(c * u.X + q * u.Y, c * u.Y - q * u.X);
                var n = n1.Y < n2.Y ? n1 : n2;
                double a = Math.Atan2(-n.Y, n.X) * 180 / Math.PI;
                rope.AddRange(Arc(T, R, 180, a));
                rope.AddRange(Arc(D, ds, a, 0, 8));
                rope.Add(Pt(wd, cwTop));
                f.Text("deflector sheave", D + Pt(ds + 3, -ds - 1.5), "start");
            }
            else
            {
                rope.AddRange(Arc(T, R, 180, 0, 24));
                rope.Add(Pt(wd, cwTop));
            }
            f.Poly(rope, false, FigureLayer.Outline, "rope");
            // Car and counterweight with their hitches.
            f.Rect(cd - 10, carTop + 2, cd + 10, carTop + 30, FigureLayer.Secondary, "car");
            f.Text("CAR", Pt(cd, carTop + 17), "middle");
            f.Poly(
                new[] { Pt(cd, carTop), Pt(cd - 2, carTop + 2), Pt(cd + 2, carTop + 2) },
                true,
                FigureLayer.Secondary,
                "car-hitch",
                true);
            f.Rect(wd - 5, cwTop + 2, wd + 5, cwTop + 26, FigureLayer.Secondary, "counterweight");
            f.Text("CW", Pt(wd, cwTop + 15), "middle");
            f.Poly(new[] { Pt(wd, cwTop), Pt(wd - 2, cwTop + 2), Pt(wd + 2, cwTop + 2) }, true, FigureLayer.Secondary, "cw-hitch", true);
            // Rope tensions as arrows beside the rope runs (all ropes; per rope when the count is known).
            DrawRopeTensions(r, ropeCount, f, cd, slab, wd);
            // Roping and sheave labels from the project.
            DrawRopingLabels(ropeCount, ropingRatio, sheavePitchMm, f, cd, ys, R, tx);
            var note = f.Text("Static tensions under rated load acting on the rope runs", Pt(14, carTop + 38), "start");
            Finish(f, note.At.Y);
            return f;
        }

        private static void DrawSlab(Figure f, double cd, double slab, double wd)
        {
            HatchedBand(f, 14, slab, cd - 3, slab + 4, "slab");
            HatchedBand(f, cd + 3, slab, wd - 3, slab + 4);
            HatchedBand(f, wd + 3, slab, 152, slab + 4);
            f.Text("machine room", Pt(16, slab - 2), "start");
        }

        private static void DrawRopeTensions(ChassisCalculationResult r, int? ropeCount, Figure f, double cd, double slab, double wd)
        {
            bool perRope =
                            ropeCount.HasValue
                                && ropeCount.Value > 0
                                && r.CarPerRopeTensionN.HasValue
                                && r.CounterweightPerRopeTensionN.HasValue;
            double ay0 = slab + 7, ay1 = slab + 17;
            f.Load(
                Pt(cd - 3, ay0),
                Pt(cd - 3, ay1),
                "T(car) " + Figure.Kn(r.CarBundleTensionN.Value),
                new[] { Pt(cd - 5, ay0 + 2.5), Pt(cd - 5, ay0) },
                "end",
                "tension-car",
                Figure.Num(r.CarBundleTensionN.Value / 1000, "0.###"));
            f.Load(
                Pt(wd + 3, ay0),
                Pt(wd + 3, ay1),
                "T(cwt) " + Figure.Kn(r.CounterweightBundleTensionN.Value),
                new[] { Pt(wd + 5, ay0 + 2.5), Pt(wd + 5, ay0) },
                "start",
                "tension-cwt",
                Figure.Num(r.CounterweightBundleTensionN.Value / 1000, "0.###"));
            if (perRope)
            {
                f.Text(
                    "per rope " + Figure.Kn(r.CarPerRopeTensionN.Value),
                    Pt(cd - 5, ay0 + 6.4),
                    "end",
                    FigureLayer.LoadText,
                    2.5,
                    false,
                    "tension-car-rope",
                    Figure.Num(r.CarPerRopeTensionN.Value / 1000, "0.###"));
                f.Text(
                    "per rope " + Figure.Kn(r.CounterweightPerRopeTensionN.Value),
                    Pt(wd + 5, ay0 + 6.4),
                    "start",
                    FigureLayer.LoadText,
                    2.5,
                    false,
                    "tension-cwt-rope",
                    Figure.Num(r.CounterweightPerRopeTensionN.Value / 1000, "0.###"));
            }
        }

        private static void DrawRopingLabels(
            int? ropeCount,
            double? ropingRatio,
            double? sheavePitchMm,
            Figure f,
            double cd,
            double ys,
            double R,
            double tx)
        {
            string ratio = Ratio(ropingRatio), label = (ropeCount.HasValue && ropeCount.Value > 0 ? "n = "
                            + ropeCount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + (ropeCount.Value == 1 ? " rope" : " ropes") : null);
            string roping = label == null ? ratio : (ratio == null ? label : label + ", " + ratio);
            if (roping != null)
                f.Text(roping, Pt(tx, ys - R - 3), "middle", FigureLayer.Text, 2.5, false, "roping", roping);
            f.Text("traction sheave", Pt(cd - 3, ys - 1), "end");
            if (sheavePitchMm.HasValue && sheavePitchMm.Value > 0)
            {
                f.Text(
                                    "pitch diameter " + Figure.Mm(sheavePitchMm.Value),
                                    Pt(cd - 3, ys + 3),
                                    "end",
                                    FigureLayer.Text,
                                    2.5,
                                    false,
                                    "sheave-pitch",
                                    Figure.Mm(sheavePitchMm.Value));
            }
        }

        // ---------- F8 progressive safety gear (P5) ----------
        /// <summary>Figure F8: progressive safety gear and P5 on the car guide rails.</summary>
        public static Figure SafetyGear(SafetyPitLoad p5, IRailSection rail)
        {
            var f = new Figure { Id = "F8", Title = "Progressive safety gear — P5 on the car guide rails", Schematic = true };
            int n = p5.ComponentCount.Value;
            double x0 = 26, x1 = 92, top = 20, beam = 56;
            string val = Figure.Num(p5.PerComponentLoadN.Value / 1000, "0.###");
            f.Text("Front elevation, lower car frame", Pt((x0 + x1) / 2, 8), "middle", FigureLayer.Text, 3.5, false, null, null, true);
            var xs = Enumerable.Range(0, n).Select(i => n == 1 ? (x0 + x1) / 2 : x0 + i * (x1 - x0) / (n - 1)).ToList();
            // Car frame: stiles inside the rails and the safety plank across the bottom.
            double fl = xs.First() + 7, fr = xs.Last() - 7;
            f.Rect(fl, top + 6, fl + 3, beam + 6, FigureLayer.Secondary, "stile-left");
            f.Rect(fr - 3, top + 6, fr, beam + 6, FigureLayer.Secondary, "stile-right");
            f.Rect(fl, beam, fr, beam + 6, FigureLayer.Outline, "plank");
            f.Line(Pt(fl, top + 16), Pt(fr, top + 16), FigureLayer.Hidden, "car-floor");
            f.Text("car floor", Pt((fl + fr) / 2, top + 14.5), "middle");
            f.Text("safety plank", Pt((fl + fr) / 2, beam + 4.2), "middle");
            for (int i = 0; i < n; i++)
            {
                double x = xs[i];
                int side = x < (x0 + x1) / 2 ? -1 : 1;
                if (n == 1)
                    side = -1;
                // Rail (blade edge-on) and the safety-gear block clamping the blade, fixed to the plank.
                f.Rect(x - 1.75, top, x + 1.75, beam + 18, FigureLayer.Outline, "rail-" + (i + 1));
                f.Rect(x - 4, beam - 4, x + 4, beam + 8, FigureLayer.Outline, "safety-gear-" + (i + 1));
                f.Line(Pt(x - 4, beam + 2), Pt(x - 1.75, beam - 1), FigureLayer.Thin);
                f.Line(Pt(x + 4, beam + 2), Pt(x + 1.75, beam - 1), FigureLayer.Thin);
                // P5 acts downward on the rail at the safety gear.
                f.Load(
                    Pt(x + side * 3, beam - 22),
                    Pt(x + side * 3, beam - 4.2),
                    "P5 " + Figure.Kn(p5.PerComponentLoadN.Value),
                    new[] { Pt(x + side * 5.5, top + 24), Pt(x + side * 5.5, top + 20) },
                    side < 0 ? "end" : "start",
                    "force-p5-" + (i + 1),
                    val);
            }
            f.Text("per car guide rail", Pt((x0 + x1) / 2, beam + 14), "middle", FigureLayer.LoadText, 2.5, false, "p5-per-rail", null);
            f.Text(
                n.ToString(System.Globalization.CultureInfo.InvariantCulture) + (n == 1 ? " car guide rail" : " car guide rails"),
                Pt((x0 + x1) / 2, beam + 18),
                "middle",
                FigureLayer.Text,
                2.5,
                false,
                "rail-count",
                n.ToString(System.Globalization.CultureInfo.InvariantCulture));
            // Plan detail: the jaws grip both side faces of the blade.
            DrawJawDetail(rail, f, beam);
            var note = f.Text(
                "Rail section " + rail.Designation + " with the safety gear gripping the blade side faces",
                Pt(4, beam + 26),
                "start",
                FigureLayer.Text,
                2.5,
                false,
                "rail-note",
                rail.Designation);
            Finish(f, note.At.Y);
            return f;
        }

        private static void DrawJawDetail(IRailSection rail, Figure f, double beam)
        {
            double cx = 142, cy = 48, s = 24 / Math.Max(rail.BaseWidthMm, rail.HeightMm);
            f.Line(Pt(118, 4), Pt(118, beam + 20), FigureLayer.Thin, "view-split");
            f.Text("Plan detail", Pt(cx, 8), "middle", FigureLayer.Text, 3.5, false, null, null, true);
            var outline = rail.Outline().Select(q => Pt(cx + q.X * s, cy + (rail.HeightMm / 2 - q.Y) * s)).ToList();
            f.Hatch(outline);
            f.Poly(outline, true, FigureLayer.Outline, "detail-rail");
            double k = rail.BladeWidthMm * s / 2, bt = cy - rail.HeightMm / 2 * s;
            foreach (int side in new[] { -1, 1 })
            {
                double xf = cx + side * k;
                f.Poly(
                    new[] { Pt(xf + side * 0.3, bt + 2), Pt(xf + side * 0.3, bt + 12), Pt(xf + side * 5, bt + 14), Pt(xf + side * 5, bt) },
                    true,
                    FigureLayer.Outline,
                    side < 0 ? "jaw-left" : "jaw-right");
                f.Rect(
                    Math.Min(xf + side * 5, xf + side * 9),
                    bt - 2,
                    Math.Max(xf + side * 5, xf + side * 9),
                    bt + 16,
                    FigureLayer.Secondary);
            }
            f.Text("jaws", Pt(cx + k + 10, bt + 6), "start");
            f.Text("blade", Pt(cx, bt - 4), "middle");
        }

        // ---------- F9 pit buffers (P6 / P7) ----------
        /// <summary>Pit floor level of figure F9, paper mm.</summary>
        public const double F9Floor = 72;

        /// <summary>Figure F9: pit buffer loads P6 and P7.</summary>
        public static Figure PitBuffers(SafetyPitLoad p6, SafetyPitLoad p7, double? pitDepthMm)
        {
            var f = new Figure { Id = "F9", Title = "Pit buffer loads P6 / P7 — pit section", Schematic = true };
            double floor = F9Floor, landing = 22;
            HatchedBand(f, 6, floor, 159, floor + 6, "pit-floor");
            HatchedBand(f, 6, 14, 12, floor);
            HatchedBand(f, 153, 14, 159, floor);
            f.Line(Pt(12, floor), Pt(153, floor), FigureLayer.Outline, "pit-floor-line");
            if (pitDepthMm.HasValue && pitDepthMm.Value > 0)
            {
                f.Line(Pt(12, landing), Pt(26, landing), FigureLayer.Outline, "lowest-landing");
                f.Text("landing", Pt(13, landing - 1.5), "start");
                f.DimV(
                    Pt(12, landing),
                    Pt(12, floor),
                    20,
                    "pit depth " + Figure.Mm(pitDepthMm.Value),
                    "dim-pit",
                    Figure.Mm(pitDepthMm.Value));
            }
            Action<SafetyPitLoad, double, double, double, double, string, string> group = (load, gx0, gx1, boxTop, boxBottom, name, tag) =>
            {
                int n = load.ComponentCount.Value;
                string per = Figure.Num(load.PerComponentLoadN.Value / 1000, "0.###");
                f.Rect(gx0, boxTop, gx1, boxBottom, FigureLayer.Secondary, tag == "car" ? "car" : "counterweight");
                f.Text(tag == "car" ? "CAR" : "CW", Pt((gx0 + gx1) / 2, (boxTop + boxBottom) / 2 + 1.2), "middle");
                for (int i = 0; i < n; i++)
                {
                    double x = n == 1 ? (gx0 + gx1) / 2 : gx0 + 6 + i * (gx1 - gx0 - 12) / (n - 1);
                    // Buffer: base plate, body and plunger, standing on the pit floor.
                    f.Rect(x - 4.5, floor - 1.2, x + 4.5, floor, FigureLayer.Outline, tag + "-buffer-base-" + (i + 1));
                    f.Rect(x - 3, floor - 20, x + 3, floor - 1.2, FigureLayer.Outline, tag + "-buffer-" + (i + 1));
                    f.Rect(x - 1.5, floor - 27, x + 1.5, floor - 20, FigureLayer.Secondary);
                    // Per-buffer load on the buffer centre line, at its base, onto the pit floor.
                    f.Load(
                        Pt(x, floor - 14),
                        Pt(x, floor),
                        load.Id + " " + Figure.Kn(load.PerComponentLoadN.Value),
                        new[] { Pt(x, floor - 32.4), Pt(x, floor - 36), Pt(x, floor - 40) },
                        "middle",
                        tag + "-force-" + (i + 1),
                        per);
                }
                f.Text(
                    load.Id + " total " + Figure.Kn(load.GroupLoadN.Value) + " on " + n + " " + name + (n == 1 ? "" : "s"),
                    Pt((gx0 + gx1) / 2, floor + 11),
                    "middle",
                    FigureLayer.LoadText,
                    2.5,
                    false,
                    tag + "-total",
                    Figure.Num(load.GroupLoadN.Value / 1000, "0.###"));
                f.Text(
                    "per buffer " + Figure.Kn(load.PerComponentLoadN.Value),
                    Pt((gx0 + gx1) / 2, floor + 15),
                    "middle",
                    FigureLayer.LoadText,
                    2.5,
                    false,
                    tag + "-per",
                    per);
            };
            if (p6 != null)
                group(p6, 32, 92, 20, 34, "car buffer", "car");
            if (p7 != null)
                group(p7, 102, 142, 14, 30, "CW buffer", "cw");
            var note = f.Text("Loads on the pit floor at each buffer base (buffer positions schematic)", Pt(8, floor + 21), "start");
            Finish(f, note.At.Y);
            return f;
        }
    }
}
