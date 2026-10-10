using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.Core.Doors
{
    /// <summary>
    /// Door rules: the manual door width (existing recipe), and for the automatic types the opening selection and validation, the front
    /// zone, the opening position, the landing door geometry and the clash and EN 81-20 checks (DESIGN-FLOW "Doors", DOORS.md).
    /// </summary>
    public static class DoorRules
    {
        const double Eps = 1e-7;

        /// <summary>The largest preferred door width that fits the car and the jambs; 0 when none fits (Manual doors).</summary>
        public static double SelectWidth(double sw, double carWidth, double jambWidth, EquipmentProfile p)
        {
            return p.PreferredDoorWidthsMm
                .OrderByDescending(v => v)
                .FirstOrDefault(v => v > 0 && v <= carWidth - 2 * p.MinimumCabinDoorReturnMm + 1e-7 && v + 2 * jambWidth <= sw + 1e-7);
        }

        static string L(double v)
        {
            return Math.Round(v, 1).ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The layout of an automatic door on the plan: the locked size when there is one, otherwise the largest standard size that
        /// fits (the smallest, with its problems, when none does). The plan must already hold the door's front zone.
        /// </summary>
        public static DoorLayout Select(DoorInput door, ElevatorParameters plan)
        {
            var sizes = door.Catalog.OpeningsMm.OrderByDescending(o => o).ToList();
            double? locked = door.Settings.OpeningMm;
            if (locked.HasValue)
                return Layout(door, plan, locked.Value, false);
            DoorLayout smallest = null;
            foreach (double o in sizes)
            {
                var l = Layout(door, plan, o, true);
                if (l.Fits)
                    return l;
                smallest = l;
            }
            return smallest;
        }

        /// <summary>The layout of a project's door on a plan; a Manual door gives a layout with its plan door width.</summary>
        public static DoorLayout Evaluate(ElevatorProject project, DoorCatalog catalog, ElevatorParameters plan)
        {
            var door = DoorInput.From(project, catalog);
            if (door == null)
                return new DoorLayout { Type = DoorTypes.Manual, OpeningMm = plan == null ? 0 : plan.DoorWidth };
            return Select(door, plan);
        }

        /// <summary>The layout of one clear-opening size.</summary>
        public static DoorLayout Layout(DoorInput door, ElevatorParameters plan, double openingMm, bool auto)
        {
            var cat = door.Catalog;
            var u = door.Settings;
            var t = cat.For(door.Type);
            bool side = door.Type == DoorTypes.Automatic2S;
            var l = new DoorLayout { Type = door.Type, OpeningMm = openingMm, AutoSelected = auto };
            l.CarZoneMm = u.CarZoneMm ?? t.CarZoneMm.Value;
            l.SillGapMm = u.SillGapMm ?? t.SillGapMm.Value;
            l.LandingZoneMm = u.LandingZoneMm ?? t.LandingZoneMm.Value;
            l.FrontZoneMm = l.CarZoneMm + l.SillGapMm + l.LandingZoneMm;
            l.CarToWallMm = l.LandingZoneMm + l.SillGapMm;
            l.LandingWallClearanceMm = u.LandingWallClearanceMm ?? t.LandingWallClearanceMm.Value;
            l.PanelThicknessMm = t.PanelThicknessMm ?? 0;
            l.PanelOverlapMm = t.PanelOverlapMm ?? 0;
            var row = t.Row(openingMm);
            if (row == null)
            {
                l.Problems.Add(
                    new DoorProblem
                    {
                        Code = "opening-size",
                        Message = L(openingMm) + " mm is not a standard opening ("
                            + String.Join(", ", cat.OpeningsMm.OrderBy(o => o).Select(L)) + " mm)"
                    });
                l.Fits = false;
                return l;
            }
            double sw = plan.SW, cw = plan.CarWidth, c = sw / 2;
            l.FixedReturnMm = u.FixedReturnMm ?? row.FixedReturnMm;
            l.StackReturnMm = side ? (u.StackReturnMm ?? row.StackReturnMm) : l.FixedReturnMm;
            l.LandingFixedJambMm = u.LandingFixedJambMm ?? row.LandingFixedJambMm;
            l.LandingStackPocketMm = u.LandingStackPocketMm ?? row.LandingStackPocketMm;
            if (!side)
                l.LandingFixedJambMm = l.LandingStackPocketMm;
            double off = side ? (u.OpeningOffsetMm ?? (l.StackReturnMm - l.FixedReturnMm) / 2) : 0;
            l.OpeningOffsetMm = off;
            bool stackRight = u.Handing == DoorTypes.Right || (u.Handing != DoorTypes.Left && cat.GovernorSide == DoorTypes.Left);
            l.StackingSide = stackRight ? DoorTypes.Right : DoorTypes.Left;
            int dir = stackRight ? 1 : -1;
            l.CentreShiftMm = -dir * off;
            double xo = c + l.CentreShiftMm;
            l.OpeningLeftX = xo - openingMm / 2;
            l.OpeningRightX = xo + openingMm / 2;
            l.PanelWidthMm = side
                ? (openingMm + (t.PanelEdgeOvertravelMm ?? 0) + l.PanelOverlapMm) / 2
                : (openingMm - l.PanelOverlapMm) / 2;
            double stackJamb = l.LandingStackPocketMm, fixedJamb = l.LandingFixedJambMm;
            l.LandingLeftX = l.OpeningLeftX - (stackRight || !side ? (side ? fixedJamb : stackJamb) : stackJamb);
            l.LandingRightX = l.OpeningRightX + (stackRight ? stackJamb : (side ? fixedJamb : stackJamb));
            l.ActualFixedReturnMm = side ? cw / 2 - off - openingMm / 2 : (cw - openingMm) / 2;
            l.ActualStackReturnMm = side ? cw / 2 + off - openingMm / 2 : (cw - openingMm) / 2;
            CheckFit(l, plan, side, cat, off);
            CheckEn(l, cat);
            CheckClashes(l, plan, cat, side, stackRight);
            l.Fits = l.Problems.Count == 0;
            return l;
        }

        static void CheckFit(DoorLayout l, ElevatorParameters plan, bool side, DoorCatalog cat, double off)
        {
            double o = l.OpeningMm, sw = plan.SW, cw = plan.CarWidth, wc = l.LandingWallClearanceMm;
            // The car must hold the opening and both returns.
            double needFixed = 2 * (l.FixedReturnMm + off + o / 2);
            double needStack = 2 * (l.StackReturnMm - off + o / 2);
            double needCar = side ? Math.Max(needFixed, needStack) : o + 2 * l.FixedReturnMm;
            if (needCar > cw + Eps)
            {
                double needShaft = sw + (needCar - cw);
                l.Problems.Add(
                    new DoorProblem
                    {
                        Code = "car-width",
                        Message = "needs car width " + L(needCar) + " mm (now " + L(cw) + " mm), shaft width " + L(needShaft) + " mm",
                        NeededCarWidthMm = needCar,
                        NeededShaftWidthMm = needShaft
                    });
            }
            // The landing frame and the parked panels stay inside the shaft with the wall clearance.
            double stackReach = Math.Max(l.LandingStackPocketMm, l.PanelWidthMm);
            double needLanding = side
                ? o + 2 * Math.Max(stackReach + wc - off, l.LandingFixedJambMm + wc + off)
                : o + 2 * (stackReach + wc);
            if (needLanding > sw + Eps)
            {
                l.Problems.Add(
                    new DoorProblem
                    {
                        Code = "landing-width",
                        Message = "needs shaft width " + L(needLanding) + " mm (now " + L(sw) + " mm)",
                        NeededCarWidthMm = needLanding - (sw - cw),
                        NeededShaftWidthMm = needLanding
                    });
            }
            double? nc = null, ns = null;
            foreach (var p in l.Problems)
            {
                if (p.NeededCarWidthMm.HasValue)
                    nc = nc.HasValue ? Math.Max(nc.Value, p.NeededCarWidthMm.Value) : p.NeededCarWidthMm;
                if (p.NeededShaftWidthMm.HasValue)
                    ns = ns.HasValue ? Math.Max(ns.Value, p.NeededShaftWidthMm.Value) : p.NeededShaftWidthMm;
            }
            l.NeededCarWidthMm = nc;
            l.NeededShaftWidthMm = ns;
        }

        static void CheckEn(DoorLayout l, DoorCatalog cat)
        {
            if (l.SillGapMm > cat.MaxSillGapMm.Value + Eps)
            {
                l.Problems.Add(
                    new DoorProblem
                    {
                        Code = "sill-gap",
                        Message = "sill gap " + L(l.SillGapMm) + " mm exceeds " + L(cat.MaxSillGapMm.Value) + " mm (EN 81-20 5.3.6.2.2.1)"
                    });
            }
            if (l.CarToWallMm > cat.MaxCarToWallMm.Value + Eps)
            {
                l.Problems.Add(
                    new DoorProblem
                    {
                        Code = "car-to-wall",
                        Message = "car door to front wall " + L(l.CarToWallMm) + " mm exceeds " + L(cat.MaxCarToWallMm.Value)
                            + " mm (EN 81-20 5.2.5.2.1)"
                    });
            }
        }

        sealed class Box
        {
            public string Name;
            public double X0, X1, Y0, Y1;

            public bool Hits(Box b)
            {
                return Math.Min(X1, b.X1) - Math.Max(X0, b.X0) > 1e-6 && Math.Min(Y1, b.Y1) - Math.Max(Y0, b.Y0) > 1e-6;
            }
        }

        static Box B(string name, double x0, double x1, double y0, double y1)
        {
            return new Box { Name = name, X0 = Math.Min(x0, x1), X1 = Math.Max(x0, x1), Y0 = Math.Min(y0, y1), Y1 = Math.Max(y0, y1) };
        }

        // Clash checks in plan: the landing frame, and the car door with its parked panels, against guide rails with their brackets,
        // the counterweight rails and frame, and the speed governor.
        static void CheckClashes(DoorLayout l, ElevatorParameters plan, DoorCatalog cat, bool side, bool stackRight)
        {
            var e = cat.Envelopes;
            double sw = plan.SW, c = sw / 2, rw = e.RailWidthMm.Value / 2;
            var doors = new List<Box>();
            doors.Add(B("landing door frame", l.LandingLeftX, l.LandingRightX, 0, l.LandingZoneMm));
            double carY0 = l.LandingZoneMm + l.SillGapMm, carY1 = carY0 + l.CarZoneMm;
            double pw = l.PanelWidthMm;
            double carX0 = side ? (stackRight ? l.OpeningLeftX : l.OpeningLeftX - pw) : l.OpeningLeftX - pw;
            double carX1 = side ? (stackRight ? l.OpeningRightX + pw : l.OpeningRightX) : l.OpeningRightX + pw;
            doors.Add(B("car door panels", carX0, carX1, carY0, carY1));
            var obstacles = new List<Box>();
            obstacles.Add(B("car guide rail", 0, c - plan.CarDbg / 2, plan.CarRow - rw, plan.CarRow + rw));
            obstacles.Add(B("car guide rail", c + plan.CarDbg / 2, sw, plan.CarRow - rw, plan.CarRow + rw));
            obstacles.Add(B("counterweight guide rail", 0, c - plan.CwDbg / 2, plan.CwRow - rw, plan.CwRow + rw));
            obstacles.Add(B("counterweight guide rail", c + plan.CwDbg / 2, sw, plan.CwRow - rw, plan.CwRow + rw));
            obstacles.Add(B("counterweight", c - plan.CwWidth / 2, c + plan.CwWidth / 2, plan.CwFront, plan.CwRear));
            double gy0 = plan.CarRow + e.GovernorFromGuideAxisMm.Value, gy1 = gy0 + e.GovernorLengthMm.Value;
            double gx0 = cat.GovernorSide == DoorTypes.Left
                ? e.GovernorGapToWallMm.Value
                : sw - e.GovernorGapToWallMm.Value - e.GovernorWidthMm.Value;
            obstacles.Add(B("speed governor", gx0, gx0 + e.GovernorWidthMm.Value, gy0, gy1));
            var seen = new HashSet<string>();
            foreach (var d in doors)
            {
                foreach (var o in obstacles)
                {
                    string key = d.Name + "|" + o.Name;
                    if (d.Hits(o) && seen.Add(key))
                        l.Problems.Add(new DoorProblem { Code = "clash", Message = d.Name + " clashes with the " + o.Name });
                }
            }
        }
    }
}
