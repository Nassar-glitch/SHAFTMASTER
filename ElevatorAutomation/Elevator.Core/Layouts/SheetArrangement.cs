using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Elevator.Core.Layouts
{
    /// <summary>A view placed on the sheet.</summary>
    public enum SheetViewKind
    {
        /// <summary>The main shaft plan (left column, bottom).</summary>
        ShaftPlan,
        /// <summary>The machine-room plan (left column, above the shaft plan).</summary>
        MachineRoomPlan,
        /// <summary>The vertical section A-A with the floors (tall, right of the plans).</summary>
        SectionAA,
        /// <summary>The machine section B-B with the beams and the traction angle (top right).</summary>
        SectionBB,
        /// <summary>An auxiliary detail view (under the others, in the free space).</summary>
        Detail
    }

    /// <summary>An axis-aligned rectangle in millimetres (paper or model).</summary>
    public sealed class SheetRect
    {
        public double X, Y, Width, Height;

        public SheetRect()
        {
        }

        public SheetRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double Right
        {
            get { return X + Width; }
        }

        public double Top
        {
            get { return Y + Height; }
        }

        public double Area
        {
            get { return Width * Height; }
        }

        /// <summary>True when the two rectangles share an inner point (touching edges do not overlap).</summary>
        public bool Overlaps(SheetRect o)
        {
            const double tol = 1e-6;
            return X < o.Right - tol && o.X < Right - tol && Y < o.Top - tol && o.Y < Top - tol;
        }

        /// <summary>True when the other rectangle lies inside this one.</summary>
        public bool Contains(SheetRect o)
        {
            const double tol = 1e-6;
            return o.X >= X - tol && o.Y >= Y - tol && o.Right <= Right + tol && o.Top <= Top + tol;
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "({0:0.#}, {1:0.#}, {2:0.#} x {3:0.#})", X, Y, Width, Height);
        }
    }

    /// <summary>A view to arrange: its kind, a name and the model extents of everything it shows (geometry and dimensions), in mm.</summary>
    public sealed class SheetViewRequest
    {
        public SheetViewKind Kind;
        public string Name;
        public SheetRect ModelExtents;
        /// <summary>
        /// The dimension margin around the extents, in model mm (0 when the extents already hold the dimensions): the viewport shows
        /// the extents grown by it on every side.
        /// </summary>
        public double MarginMm;
        /// <summary>
        /// The smallest scale denominator the view may take (0: none). A reference detail keeps the scale its drawn texts state
        /// ("VIEW at D-D 1:20"), so it is never shown at a larger scale than the template's.
        /// </summary>
        public int MinScaleDenominator;
    }

    /// <summary>A view placed on the sheet: the viewport rectangle (paper mm), its standard scale and the scale label under it.</summary>
    public sealed class SheetViewport
    {
        public SheetViewKind Kind;
        public string Name;
        /// <summary>The viewport rectangle on paper (mm).</summary>
        public SheetRect Paper;
        /// <summary>The scale denominator: 1 : ScaleDenominator.</summary>
        public int ScaleDenominator;
        /// <summary>The model point shown at the viewport centre (the centre of the model extents).</summary>
        public double ModelCenterX, ModelCenterY;
        /// <summary>The scale label insertion point (middle centre), under the viewport.</summary>
        public double LabelX, LabelY;

        /// <summary>The scale label: "1:50".</summary>
        public string LabelText
        {
            get { return "1:" + ScaleDenominator.ToString(CultureInfo.InvariantCulture); }
        }
    }

    /// <summary>The arranged sheet.</summary>
    public sealed class SheetArrangementResult
    {
        public SheetRect Area;
        public List<SheetViewport> Viewports = new List<SheetViewport>();
        /// <summary>Viewport area / drawing area (0–1).</summary>
        public double FillRatio;
        /// <summary>The views that did not fit at the smallest standard scale (empty when every view is placed).</summary>
        public List<string> NotPlaced = new List<string>();
    }

    /// <summary>
    /// Arranges the views of the A0 geared sheet (DESIGN-FLOW "Sheet arrangement"): left column the shaft plan at the bottom and the
    /// machine-room plan above it, right of it the section A-A (full height when tall), top right the section B-B, and the detail views
    /// in the bands left free. Pure geometry: no AutoCAD.
    /// <para>
    /// Hierarchy rule (L2b): the shaft plan is the leading view and no other main view (machine-room plan, A-A, B-B) gets a larger
    /// scale than it; they use its scale or a smaller one. Without a shaft plan there is no such constraint. The details are free.
    /// </para>
    /// <para>
    /// The search is joint: every combination of the standard scales of the four main views gives the column widths and heights; the
    /// details are then packed into the free bands, each at the largest standard scale that still fits. The combination kept has the
    /// largest views together: the sum over the views of the square root of their area (their linear size; details count half), so one
    /// view is not shrunk to win a step on another, with a penalty when the two plans differ in scale (the same scale is preferred, not
    /// required), and the fuller sheet among equals. Views are spaced with one constant (sheet margin, gaps, label band) and each view
    /// keeps its dimension margin. The plans and A-A sit from the left, B-B flush top right; the details fill the bands left free
    /// (beside and under B-B, above A-A and above the machine-room plan), the largest band first.
    /// </para>
    /// </summary>
    public static class SheetArrangement
    {
        /// <summary>The standard scales, largest first.</summary>
        public static readonly int[] StandardScales = { 10, 20, 25, 50, 75, 100, 150, 200 };

        /// <summary>The weight of a detail view against a main view (1).</summary>
        public const double DetailWeight = 0.5;

        /// <summary>The spacing constant (mm): sheet margin, gap between views, and the scale-label band under each view.</summary>
        public const double Spacing = 10;

        /// <summary>
        /// The penalty (mm of linear view size) when the shaft plan and the machine-room plan take different scales: they differ only
        /// when that wins more than this.
        /// </summary>
        public const double PlanScalePenalty = 60;

        /// <summary>
        /// The drawing area of the A0 geared template in its paper millimetres (Templates/Layout_Geared_A0.dxf): the inner frame
        /// left of the title-block column.
        /// </summary>
        public static SheetRect A0DrawingArea()
        {
            return new SheetRect(14.9, 12.4, 1001.9, 816.4);
        }

        static double W(SheetViewRequest v, int s)
        {
            return (v.ModelExtents.Width + 2 * v.MarginMm) / s;
        }

        static double H(SheetViewRequest v, int s)
        {
            return (v.ModelExtents.Height + 2 * v.MarginMm) / s;
        }

        static SheetViewport Place(SheetViewRequest v, int s, double x, double y)
        {
            var r = new SheetRect(x, y, W(v, s), H(v, s));
            return new SheetViewport
            {
                Kind = v.Kind,
                Name = v.Name ?? v.Kind.ToString(),
                Paper = r,
                ScaleDenominator = s,
                ModelCenterX = v.ModelExtents.X + v.ModelExtents.Width / 2,
                ModelCenterY = v.ModelExtents.Y + v.ModelExtents.Height / 2,
                LabelX = r.X + r.Width / 2,
                LabelY = r.Y - Spacing / 2
            };
        }

        static void Validate(SheetRect area, IList<SheetViewRequest> views)
        {
            if (area == null || !(area.Width > 0) || !(area.Height > 0))
                throw new ArgumentException("The drawing area must have a positive size.");
            if (views == null)
                throw new ArgumentNullException("views");
            foreach (var v in views)
            {
                if (v == null || v.ModelExtents == null || !(v.ModelExtents.Width > 0) || !(v.ModelExtents.Height > 0)
                    || Double.IsInfinity(v.ModelExtents.Width) || Double.IsInfinity(v.ModelExtents.Height))
                    throw new ArgumentException("Every view needs positive, finite model extents.");
                if (Double.IsNaN(v.MarginMm) || Double.IsInfinity(v.MarginMm) || v.MarginMm < 0)
                    throw new ArgumentException("A dimension margin must be zero or positive.");
            }
            foreach (var k in new[] { SheetViewKind.ShaftPlan, SheetViewKind.MachineRoomPlan, SheetViewKind.SectionAA, SheetViewKind.SectionBB })
            {
                if (views.Count(v => v.Kind == k) > 1)
                    throw new ArgumentException("At most one " + k + " view.");
            }
        }

        /// <summary>Arranges the views in the drawing area.</summary>
        public static SheetArrangementResult Arrange(SheetRect area, IList<SheetViewRequest> views)
        {
            Validate(area, views);
            var plan = views.FirstOrDefault(v => v.Kind == SheetViewKind.ShaftPlan);
            var room = views.FirstOrDefault(v => v.Kind == SheetViewKind.MachineRoomPlan);
            var aa = views.FirstOrDefault(v => v.Kind == SheetViewKind.SectionAA);
            var bb = views.FirstOrDefault(v => v.Kind == SheetViewKind.SectionBB);
            var details = views.Where(v => v.Kind == SheetViewKind.Detail).ToList();

            int[] none = { 0 }; // a missing view keeps the loops the same
            Layout best = null;
            foreach (int sp in plan != null ? StandardScales : none)
                foreach (int sm in room != null ? StandardScales : none)
                    foreach (int sa in aa != null ? StandardScales : none)
                        foreach (int sb in bb != null ? StandardScales : none)
                        {
                            // Hierarchy: the shaft plan leads. No other main view takes a larger scale (a smaller denominator) than it.
                            if (plan != null && (sm != 0 && sm < sp || sa != 0 && sa < sp || sb != 0 && sb < sp))
                                continue;
                            var l = Try(area, plan, room, aa, bb, details, sp, sm, sa, sb);
                            if (l != null && (best == null || l.Better(best)))
                                best = l;
                        }
            var result = new SheetArrangementResult { Area = area };
            if (best == null)
            {
                result.NotPlaced.AddRange(views.Select(v => v.Name ?? v.Kind.ToString()));
                return result;
            }
            result.Viewports = best.Viewports;
            result.FillRatio = best.Viewports.Sum(v => v.Paper.Area) / area.Area;
            return result;
        }

        sealed class Layout
        {
            public List<SheetViewport> Viewports = new List<SheetViewport>();
            public double Filled;
            public double Score;

            // The larger score wins; then the fuller sheet; equal layouts keep the first found (the larger scales come first).
            public bool Better(Layout o)
            {
                if (Math.Abs(Score - o.Score) > 1e-9)
                    return Score > o.Score;
                return Filled > o.Filled + 1e-6;
            }
        }

        static Layout Try(
            SheetRect area,
            SheetViewRequest plan,
            SheetViewRequest room,
            SheetViewRequest aa,
            SheetViewRequest bb,
            List<SheetViewRequest> details,
            int sp,
            int sm,
            int sa,
            int sb)
        {
            double g = Spacing, label = Spacing;
            double innerW = area.Width - 2 * g, innerH = area.Height - 2 * g;
            // Column sizes; a view's footprint is the view plus its label band under it.
            double w1 = Math.Max(plan != null ? W(plan, sp) : 0, room != null ? W(room, sm) : 0);
            double h1 = (plan != null ? H(plan, sp) + label : 0) + (room != null ? H(room, sm) + label : 0) + (plan != null && room != null ? g : 0);
            double w2 = aa != null ? W(aa, sa) : 0, h2 = aa != null ? H(aa, sa) + label : 0;
            double w3 = bb != null ? W(bb, sb) : 0, h3 = bb != null ? H(bb, sb) + label : 0;
            if (h1 > innerH + 1e-9 || h2 > innerH + 1e-9 || h3 > innerH + 1e-9)
                return null;
            int columns = (w1 > 0 ? 1 : 0) + (w2 > 0 ? 1 : 0);
            double third = innerW - w1 - w2 - g * columns; // the right column takes what is left
            if (third < -1e-9 || (bb != null && w3 > third + 1e-9))
                return null;

            var l = new Layout();
            var column = new List<int>(); // per viewport: 1 plans, 2 A-A, 3 B-B and the details under it
            double x = area.X + g, bottom = area.Y + g, top = area.Top - g;
            var free = new List<Shelf>();
            if (w1 > 0)
            {
                double y = bottom;
                if (plan != null)
                {
                    l.Viewports.Add(Place(plan, sp, x + (w1 - W(plan, sp)) / 2, y + label));
                    column.Add(1);
                    y += H(plan, sp) + label + g;
                }
                if (room != null)
                {
                    l.Viewports.Add(Place(room, sm, x + (w1 - W(room, sm)) / 2, y + label));
                    column.Add(1);
                    y += H(room, sm) + label + g;
                }
                if (top - y > 0)
                    free.Add(Shelf.Of(new SheetRect(x, y, w1, top - y), 1));
                x += w1 + g;
            }
            if (w2 > 0)
            {
                l.Viewports.Add(Place(aa, sa, x, bottom + label));
                column.Add(2);
                double y = bottom + h2 + g;
                if (top - y > 0)
                    free.Add(Shelf.Of(new SheetRect(x, y, w2, top - y), 2));
                x += w2 + g;
            }
            if (third > 1e-9)
            {
                // B-B flush top right; the rest of the right part is free: the band beside B-B (full height above the band under it)
                // and the band under B-B across the whole right part.
                double right = area.Right - g, under = top;
                if (bb != null)
                {
                    l.Viewports.Add(Place(bb, sb, right - w3, top - H(bb, sb)));
                    column.Add(3);
                    under = top - h3 - g;
                    if (right - w3 - g - x > 0)
                        free.Add(Shelf.Of(new SheetRect(x, under + g, right - w3 - g - x, top - under - g), 3));
                }
                if (under - bottom > 0)
                    free.Add(Shelf.Of(new SheetRect(x, bottom, right - x, under - bottom), 3));
            }
            // the details go to the largest free band first
            free = free.OrderByDescending(f => f.Box.Area).ThenBy(f => f.Box.X).ThenBy(f => f.Box.Y).ToList();
            if (details.Count > 0 && !Pack(details, free, l.Viewports, column))
                return null;
            l.Filled = l.Viewports.Sum(v => v.Paper.Area);
            l.Score = l.Viewports.Sum(v => (v.Kind == SheetViewKind.Detail ? DetailWeight : 1.0) * Math.Sqrt(v.Paper.Area))
                - (plan != null && room != null && sp != sm ? PlanScalePenalty : 0);
            return l;
        }

        // Shelf packing: the details, the largest first, each at the largest standard scale that still fits, in rows from the top of
        // the free bands (the band under B-B first).
        static bool Pack(List<SheetViewRequest> details, List<Shelf> shelves, List<SheetViewport> placed, List<int> column)
        {
            double g = Spacing, label = Spacing;
            var order = details.Select((d, i) => new { d, i })
                .OrderByDescending(o => W(o.d, 1) * H(o.d, 1)).ThenBy(o => o.i).Select(o => o.d).ToList();
            foreach (var d in order)
            {
                bool done = false;
                foreach (int s in StandardScales)
                {
                    if (s < d.MinScaleDenominator)
                        continue;
                    double w = W(d, s), h = H(d, s) + label;
                    foreach (var sh in shelves)
                    {
                        double x, y;
                        if (sh.Fit(w, h, g, out x, out y))
                        {
                            placed.Add(Place(d, s, x, y + label));
                            column.Add(sh.Column);
                            done = true;
                            break;
                        }
                    }
                    if (done)
                        break;
                }
                if (!done)
                    return false;
            }
            return true;
        }

        sealed class Shelf
        {
            public SheetRect Box;
            public double RowTop, RowX, RowHeight;
            public int Column;

            public static Shelf Of(SheetRect box, int column)
            {
                return new Shelf { Box = box, RowTop = box.Top, RowX = box.X, RowHeight = 0, Column = column };
            }

            // Places a w x h footprint in the current row, or in a new row under it; the shelf changes only when it fits. Gives the
            // footprint's lower-left corner.
            public bool Fit(double w, double h, double g, out double x, out double y)
            {
                x = y = 0;
                if (w > Box.Width + 1e-9)
                    return false;
                double rowTop = RowTop, rowX = RowX, rowHeight = RowHeight;
                if (rowX + w > Box.Right + 1e-9)
                {
                    rowTop -= rowHeight + g;
                    rowX = Box.X;
                    rowHeight = 0;
                }
                if (rowTop - h < Box.Y - 1e-9)
                    return false;
                x = rowX;
                y = rowTop - h;
                RowTop = rowTop;
                RowX = rowX + w + g;
                RowHeight = Math.Max(rowHeight, h);
                return true;
            }
        }
    }
}
