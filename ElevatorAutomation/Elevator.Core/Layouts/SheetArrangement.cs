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
        /// Details only: the scale denominator the detail is drawn at (the template's, Templates/Layout_Geared_A0.details.json); null:
        /// 1:10. A detail that does not fit takes the next smaller standard scale.
        /// </summary>
        public int? PreferredScale;
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
    /// machine-room plan above it (one scale for both plans), right of it the tall section A-A, top right the section B-B, and the detail
    /// views in the free space under and above those. Every view takes a standard scale; margins, gaps and the label band come from one
    /// spacing constant. All scale combinations are tried; the one with the largest views together (the sum of ln(1 / scale), details
    /// weighted less) is kept, and among equals the one that fills the sheet most. So a tall section takes the full height and a short one
    /// gets a larger scale or leaves its space to the details. Pure geometry: no AutoCAD.
    /// </summary>
    public static class SheetArrangement
    {
        /// <summary>The standard scales, largest first.</summary>
        public static readonly int[] StandardScales = { 10, 20, 25, 50, 75, 100, 150, 200 };

        /// <summary>The scale of a detail without a preferred scale.</summary>
        public const int DefaultDetailScale = 10;

        /// <summary>The weight of each detail view in the choice of scales; a main view weighs 1.</summary>
        public const double DetailWeight = 0.25;

        /// <summary>The spacing constant (mm): sheet margin, gap between views, and the scale-label band under each view.</summary>
        public const double Spacing = 10;

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
            return v.ModelExtents.Width / s;
        }

        static double H(SheetViewRequest v, int s)
        {
            return v.ModelExtents.Height / s;
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
                if (v != null && v.PreferredScale.HasValue && v.PreferredScale.Value <= 0)
                    throw new ArgumentException("A preferred scale must be positive.");
                if (v == null || v.ModelExtents == null || !(v.ModelExtents.Width > 0) || !(v.ModelExtents.Height > 0)
                    || Double.IsInfinity(v.ModelExtents.Width) || Double.IsInfinity(v.ModelExtents.Height))
                    throw new ArgumentException("Every view needs positive, finite model extents.");
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

            // Optional views take a dummy scale index so the loops stay the same. B-B shows the machine room of the machine-room plan:
            // it takes the plans' scale (its own only when there is no plan).
            int[] none = { 0 };
            bool plans = plan != null || room != null;
            Layout best = null;
            foreach (int s1 in plans ? StandardScales : none)
            {
                foreach (int s2 in aa != null ? StandardScales : none)
                {
                    foreach (int s3 in bb == null ? none : plans ? new[] { s1 } : StandardScales)
                    {
                        var l = Try(area, plan, room, aa, bb, details, s1, s2, s3);
                        if (l != null && (best == null || l.Better(best)))
                            best = l;
                    }
                }
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
            // Sum over the views of weight x ln(1 / scale): the views as large as possible together. A log measure, so one view is
            // never made much smaller to make another a little larger (plans at 1:150 to win one step on A-A).
            public double Readability;

            // The larger scales win; then the more filled sheet; equal layouts keep the first found (the larger scales come first).
            public bool Better(Layout o)
            {
                if (Math.Abs(Readability - o.Readability) > 1e-9)
                    return Readability > o.Readability;
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
            int s1,
            int s2,
            int s3)
        {
            double g = Spacing, label = Spacing;
            double innerW = area.Width - 2 * g, innerH = area.Height - 2 * g;
            // Column widths and heights (a view's footprint is the view plus its label band under it).
            double w1 = Math.Max(plan != null ? W(plan, s1) : 0, room != null ? W(room, s1) : 0);
            double h1 = (plan != null ? H(plan, s1) + label : 0) + (room != null ? H(room, s1) + label : 0) + (plan != null && room != null ? g : 0);
            double w2 = aa != null ? W(aa, s2) : 0, h2 = aa != null ? H(aa, s2) + label : 0;
            double w3 = bb != null ? W(bb, s3) : 0, h3 = bb != null ? H(bb, s3) + label : 0;
            if (h1 > innerH + 1e-9 || h2 > innerH + 1e-9 || h3 > innerH + 1e-9)
                return null;
            var widths = new List<double>();
            if (w1 > 0)
                widths.Add(w1);
            if (w2 > 0)
                widths.Add(w2);
            double used = widths.Sum() + g * Math.Max(0, widths.Count - 1);
            double third = innerW - used - (widths.Count > 0 ? g : 0); // the right column takes what is left
            if (bb != null && w3 > third + 1e-9)
                return null;
            if (third < 0)
                return null;

            var l = new Layout();
            var column = new List<int>(); // the column of each viewport: 1 plans, 2 A-A, 3 B-B and the details under it
            double x = area.X + g, bottom = area.Y + g, top = area.Top - g;
            var free = new List<Shelf>();
            if (w1 > 0)
            {
                double y = bottom;
                if (plan != null)
                {
                    l.Viewports.Add(Place(plan, s1, x + (w1 - W(plan, s1)) / 2, y + label));
                    column.Add(1);
                    y += H(plan, s1) + label + g;
                }
                if (room != null)
                {
                    l.Viewports.Add(Place(room, s1, x + (w1 - W(room, s1)) / 2, y + label));
                    column.Add(1);
                    y += H(room, s1) + label + g;
                }
                if (top - y > 0)
                    free.Add(Shelf.Of(new SheetRect(x, y, w1, top - y), 1));
                x += w1 + g;
            }
            if (w2 > 0)
            {
                l.Viewports.Add(Place(aa, s2, x, bottom + label));
                column.Add(2);
                double y = bottom + H(aa, s2) + label + g;
                if (top - y > 0)
                    free.Add(Shelf.Of(new SheetRect(x, y, w2, top - y), 2));
                x += w2 + g;
            }
            if (third > 0)
            {
                double y = top;
                if (bb != null)
                {
                    l.Viewports.Add(Place(bb, s3, x, top - H(bb, s3)));
                    column.Add(3);
                    y = top - h3 - g;
                }
                if (y - bottom > 0)
                    free.Insert(0, Shelf.Of(new SheetRect(x, bottom, third, y - bottom), 3)); // under B-B first
            }
            if (details.Count > 0 && !Pack(details, free, l.Viewports, column))
                return null;
            // Spread the columns: the width the right column does not use goes equally into the two gaps between the columns.
            if (third > 0)
            {
                double right = l.Viewports.Where((v, i) => column[i] == 3).Select(v => v.Paper.Right).DefaultIfEmpty(x).Max();
                double slack = area.Right - g - right;
                int gaps = (w1 > 0 ? 1 : 0) + (w2 > 0 ? 1 : 0);
                if (slack > 1e-9 && gaps > 0)
                {
                    for (int i = 0; i < l.Viewports.Count; i++)
                    {
                        double shift = column[i] == 3 ? slack : column[i] == 2 && w1 > 0 ? slack / gaps : 0;
                        Shift(l.Viewports[i], shift);
                    }
                }
                // B-B stays flush top right, also when the details under it are wider.
                var b = l.Viewports.FirstOrDefault(v => v.Kind == SheetViewKind.SectionBB);
                if (b != null)
                    Shift(b, area.Right - g - b.Paper.Right);
            }
            l.Filled = l.Viewports.Sum(v => v.Paper.Area);
            l.Readability = l.Viewports.Sum(v => (v.Kind == SheetViewKind.Detail ? DetailWeight : 1.0) * -Math.Log(v.ScaleDenominator));
            return l;
        }

        static void Shift(SheetViewport v, double dx)
        {
            if (dx == 0)
                return;
            v.Paper.X += dx;
            v.LabelX += dx;
        }

        // Shelf packing: the details, tallest first, in rows from the top of each free rectangle (the space under B-B first).
        static int Preferred(SheetViewRequest d)
        {
            int s = d.PreferredScale ?? DefaultDetailScale;
            return StandardScales.Contains(s) ? s : StandardScales.FirstOrDefault(x => x > s);
        }

        static bool Pack(List<SheetViewRequest> details, List<Shelf> shelves, List<SheetViewport> placed, List<int> column)
        {
            double g = Spacing, label = Spacing;
            var order = details.Select((d, i) => new { d, i }).OrderByDescending(o => H(o.d, Preferred(o.d))).ThenBy(o => o.i).Select(o => o.d).ToList();
            foreach (var d in order)
            {
                bool done = false;
                // The preferred detail scale, else the next smaller standard scales for this detail only: a detail never pulls the
                // main views down a scale.
                foreach (int ds in StandardScales.Where(x => x >= Preferred(d)))
                {
                    double w = W(d, ds), h = H(d, ds) + label;
                    foreach (var sh in shelves)
                    {
                        double x, y;
                        if (sh.Fit(w, h, g, out x, out y))
                        {
                            placed.Add(Place(d, ds, x, y + label));
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
