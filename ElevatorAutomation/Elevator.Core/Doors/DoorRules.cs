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

        /// <summary>
        /// The layout of one clear-opening size. 2S: the preferred position is tried first; when it does not fit the opening is shifted
        /// towards the fixed side, down to the minimum fixed-side return (the opening edge flush with the car side wall) and the minimum
        /// fixed-side landing jamb. 2C: centred, the returns shrink to their minimum.
        /// </summary>
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
                            + String.Join(", ", cat.OpeningsMm.OrderBy(v => v).Select(L)) + " mm)"
                    });
                l.Fits = false;
                return l;
            }
            double sw = plan.SW, cw = plan.CarWidth, o = openingMm;
            // The car side wall stands g inside the shaft wall: the opening cannot go beyond it.
            double g = (sw - cw) / 2;
            l.CarSideGapMm = g;
            l.FixedReturnMm = u.FixedReturnMm ?? row.FixedReturnMm;
            l.StackReturnMm = side ? (u.StackReturnMm ?? row.StackReturnMm) : l.FixedReturnMm;
            l.FixedReturnMinMm = u.MinFixedReturnMm ?? t.MinFixedReturnMm ?? 0;
            l.StackReturnMinMm = side ? (u.MinStackReturnMm ?? t.MinStackReturnMm ?? 0) : l.FixedReturnMinMm;
            l.LandingStackPocketMm = u.LandingStackPocketMm ?? row.LandingStackPocketMm;
            double jambPreferred = side ? (u.LandingFixedJambMm ?? row.LandingFixedJambMm) : l.LandingStackPocketMm;
            l.LandingFixedJambMinMm = side ? (u.LandingFixedJambMinMm ?? t.LandingFixedJambMinMm ?? 0) : l.LandingStackPocketMm;
            bool stackRight = u.Handing == DoorTypes.Right || (u.Handing != DoorTypes.Left && cat.GovernorSide == DoorTypes.Left);
            l.StackingSide = stackRight ? DoorTypes.Right : DoorTypes.Left;
            // A centre opening has no stacking side: its plan X is the plan X of the drawing.
            bool mirrored = side && !stackRight;
            l.PanelWidthMm = side
                ? (o + (t.PanelEdgeOvertravelMm ?? 0) + l.PanelOverlapMm) / 2
                : (o - l.PanelOverlapMm) / 2;
            // Placement in u: distance from the fixed-side shaft wall (2C: from the left wall). The opening spans [x, x + o].
            double pocket = l.LandingStackPocketMm, wc = l.LandingWallClearanceMm;
            // The requested position: the Doors Offset when the user set one, the offset of an earlier file (2S), else the preset.
            double prefOffset = side ? (l.StackReturnMm - l.FixedReturnMm) / 2 : 0;
            if (u.OffsetMm.HasValue)
            {
                // Left / Right as seen in the plan drawing; towards the fixed side is towards the left when the stack is on the right.
                string sd = u.OffsetSide ?? (stackRight || !side ? DoorTypes.Left : DoorTypes.Right);
                bool towardsLeft = sd == DoorTypes.Left;
                prefOffset = towardsLeft == (stackRight || !side) ? u.OffsetMm.Value : 0.0 - u.OffsetMm.Value;
            }
            else if (side && u.OpeningOffsetMm.HasValue)
                prefOffset = u.OpeningOffsetMm.Value;
            l.PreferredOffsetMm = prefOffset;
            double lo, hi, x;
            double stackReach = Math.Max(pocket, l.PanelWidthMm);
            if (side)
            {
                // 1 requested layout; 2 shifted towards the fixed side while it does not fit, down to the fixed return minimum
                // (and the landing jamb minimum); the landing door moves with the opening. A requested position outside the limits
                // is placed at the nearest allowed one (the Doors Offset message reports it).
                lo = Math.Max(g + l.FixedReturnMinMm, l.LandingFixedJambMinMm);
                hi = Math.Min(sw - wc - stackReach - o, sw - g - l.StackReturnMinMm - o);
                double preferred = sw / 2 - prefOffset - o / 2;
                x = Math.Max(lo, Math.Min(preferred, hi));
                l.Shifted = x < preferred - Eps && hi >= lo - Eps;
            }
            else
            {
                // Centre opening: both returns at or above their minimum and both landing boxes inside the shaft with the wall
                // clearance. The requested position (centred by default) is placed at the nearest allowed one.
                lo = Math.Max(g + l.FixedReturnMinMm, stackReach + wc);
                hi = sw - o - lo;
                double preferred = sw / 2 - prefOffset - o / 2;
                x = hi >= lo - Eps ? Math.Max(lo, Math.Min(preferred, hi)) : (sw - o) / 2;
            }
            l.ActualFixedReturnMm = x - g;
            l.ActualStackReturnMm = sw - g - (x + o);
            l.OpeningOffsetMm = sw / 2 - (x + o / 2);
            double fixedJamb = Math.Min(jambPreferred, Math.Max(x, l.LandingFixedJambMinMm));
            l.LandingFixedJambMm = side ? fixedJamb : pocket;
            l.LandingFixedJambPreferredMm = jambPreferred;
            double uFixedOuter = x - (side ? fixedJamb : pocket), uStackOuter = x + o + pocket;
            double ux0 = x, ux1 = x + o;
            if (!mirrored)
            {
                l.OpeningLeftX = ux0;
                l.OpeningRightX = ux1;
                l.LandingLeftX = uFixedOuter;
                l.LandingRightX = uStackOuter;
            }
            else
            {
                l.OpeningLeftX = sw - ux1;
                l.OpeningRightX = sw - ux0;
                l.LandingLeftX = sw - uStackOuter;
                l.LandingRightX = sw - uFixedOuter;
            }
            l.CentreShiftMm = (l.OpeningLeftX + l.OpeningRightX) / 2 - sw / 2;
            CheckFit(l, plan, side, g, lo);
            CheckEn(l, cat);
            CheckClashes(l, plan, cat, side, stackRight);
            l.Fits = l.Problems.Count == 0;
            return l;
        }

        // The car holds the opening and the minimum returns; the landing frame and the parked pocket stay inside the shaft. The needed
        // widths are exact (the car side gap g is constant as the shaft grows).
        static void CheckFit(DoorLayout l, ElevatorParameters plan, bool side, double g, double lo)
        {
            double o = l.OpeningMm, sw = plan.SW, cw = plan.CarWidth, wc = l.LandingWallClearanceMm, pocket = l.LandingStackPocketMm;
            double needCar = o + l.FixedReturnMinMm + l.StackReturnMinMm;
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
            double stackReach = Math.Max(pocket, l.PanelWidthMm);
            double needLanding = side
                ? lo + o + Math.Max(stackReach + wc, g + l.StackReturnMinMm)
                : o + 2 * Math.Max(g + l.FixedReturnMinMm, stackReach + wc);
            if (needLanding > sw + Eps)
            {
                l.Problems.Add(
                    new DoorProblem
                    {
                        Code = "landing-width",
                        Message = "needs shaft width " + L(needLanding) + " mm (now " + L(sw) + " mm)",
                        NeededCarWidthMm = cw + (needLanding - sw),
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
