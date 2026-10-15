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
        /// The model extents the view has when shown at a scale (null: <see cref="ModelExtents"/> at every scale): a reference detail's
        /// title is 8 mm on paper at any scale, so it takes more model space the larger the denominator.
        /// </summary>
        public Dictionary<int, SheetRect> ExtentsByScale;
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
        /// <summary>The largest scale denominator the view may take (0: none). A detail whose drawn title states a scale is fixed to it (Min = Max).</summary>
        public int MaxScaleDenominator;
        /// <summary>The scales a view may take (null: <see cref="SheetArrangement.StandardScales"/>): reference details may be enlarged to 1:5.</summary>
        public int[] AllowedScales;
        /// <summary>
        /// The role of a reference detail in the grid (the template's detail kind): "RailFixing" above the section A-A, and
        /// "DoorElevationSection", "RailsPlan", "RailBrackets" stacked in that order under the section B-B. Other details (null)
        /// go to the bands left free.
        /// </summary>
        public string Role;
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

        /// <summary>The scales of a steel sheet's main views: the standard ladder with 1:40 (the user's own steel sheet shows A-A at 1:40).</summary>
        public static readonly int[] SteelScales = { 10, 20, 25, 40, 50, 75, 100, 150, 200 };

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

        static SheetRect Extents(SheetViewRequest v, int s)
        {
            SheetRect r;
            return v.ExtentsByScale != null && v.ExtentsByScale.TryGetValue(s, out r) && r != null ? r : v.ModelExtents;
        }

        static double W(SheetViewRequest v, int s)
        {
            return (Extents(v, s).Width + 2 * v.MarginMm) / s;
        }

        static double H(SheetViewRequest v, int s)
        {
            return (Extents(v, s).Height + 2 * v.MarginMm) / s;
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
                ModelCenterX = Extents(v, s).X + Extents(v, s).Width / 2,
                ModelCenterY = Extents(v, s).Y + Extents(v, s).Height / 2,
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
        public static SheetArrangementResult Arrange(SheetRect area, IList<SheetViewRequest> views, int[] mainScales = null, int sectionAAMaxDenominator = 0)
        {
            Validate(area, views);
            int[] ladder = mainScales ?? StandardScales;
            var plan = views.FirstOrDefault(v => v.Kind == SheetViewKind.ShaftPlan);
            var room = views.FirstOrDefault(v => v.Kind == SheetViewKind.MachineRoomPlan);
            var aa = views.FirstOrDefault(v => v.Kind == SheetViewKind.SectionAA);
            var bb = views.FirstOrDefault(v => v.Kind == SheetViewKind.SectionBB);
            var details = views.Where(v => v.Kind == SheetViewKind.Detail).ToList();

            int[] none = { 0 }; // a missing view keeps the loops the same
            Layout best = null;
            // The column grid of the user's own sheet first; the free packing only when the grid holds no combination at all.
            // A-A is not taken smaller than 1:sectionAAMaxDenominator while a layout exists with it at that scale or larger (steel sheets:
            // the section is the view that shows the tower); the search is repeated without the limit when none does.
            for (int pass = 0; pass < 2 && best == null; pass++)
            {
            if (pass == 1 && sectionAAMaxDenominator <= 0)
                break;
            int cap = pass == 0 ? sectionAAMaxDenominator : 0;
            for (int mode = 0; mode < 2 && best == null; mode++)
                foreach (int sp in plan != null ? ladder : none)
                    foreach (int sm in room != null ? ladder : none)
                        foreach (int sa in aa != null ? ladder : none)
                            foreach (int sb in bb != null ? ladder : none)
                            {
                                if (cap > 0 && sa > cap)
                                    continue;
                                // Hierarchy: the shaft plan leads. No other main view takes a larger scale (a smaller denominator) than it.
                                if (plan != null && (sm != 0 && sm < sp || sa != 0 && sa < sp || sb != 0 && sb < sp))
                                    continue;
                                Layout l;
                                if (mode == 0)
                                {
                                    // A tall A-A takes the whole column: the detail above it (fish plate, side guide rail) goes down to
                                    // the details group under B-B, which is packed again. The main view never pays for a detail.
                                    l = null;
                                    if (aa != null && H(aa, sa) > TallSectionShare * (area.Height - 2 * Spacing)
                                        && details.Any(d => d.Role == FishRole))
                                        l = TryGrid(area, plan, room, aa, bb, details, sp, sm, sa, sb, true);
                                    if (l == null)
                                        l = TryGrid(area, plan, room, aa, bb, details, sp, sm, sa, sb, false);
                                }
                                else
                                    l = Try(area, plan, room, aa, bb, details, sp, sm, sa, sb);
                                if (l != null && (best == null || l.Better(best)))
                                    best = l;
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


        /// <summary>A detail shown larger than its template scale may take at most this share of the width of its column.</summary>
        public const double EnlargedWidthShare = 0.95;

        /// <summary>A-A is tall when its viewport takes more than this share of the column height.</summary>
        public const double TallSectionShare = 0.62;

        static readonly string[] StackRoles = { "DoorElevationSection", "RailsPlan", "RailBrackets" };
        const string FishRole = "RailFixing";

        // The column grid of the user's own sheet (L2c), as proportions of the drawing area:
        //   column 1  the machine-room plan above the shaft plan (space between);
        //   column 2  the section A-A at the bottom, the fish plate detail above it;
        //   column 3  the section B-B flush top right (reaching left over column 2 when it is wider than column 3), under it the door
        //             detail, the guides and brackets plan and the rail-bracket elevation, top to bottom.
        // Every view takes the largest standard scale that fits its cell; the column widths follow the content (a tall A-A widens
        // column 2, a wide shaft widens column 1); the slack of the sheet is spread between the columns and the stacked details.
        // There are no scale labels under the viewports: a view's own title states its scale.
        static Layout TryGrid(
            SheetRect area,
            SheetViewRequest plan,
            SheetViewRequest room,
            SheetViewRequest aa,
            SheetViewRequest bb,
            List<SheetViewRequest> details,
            int sp,
            int sm,
            int sa,
            int sb,
            bool fishToGroup)
        {
            const double eps = 1e-9;
            double g = Spacing;
            double x0 = area.X + g, xr = area.Right - g, yb = area.Y + g, yt = area.Top - g;
            double innerH = yt - yb;
            double wPlan = plan != null ? W(plan, sp) : 0, hPlan = plan != null ? H(plan, sp) : 0;
            double wRoom = room != null ? W(room, sm) : 0, hRoom = room != null ? H(room, sm) : 0;
            double w1 = Math.Max(wPlan, wRoom);
            double h1 = hPlan + hRoom + (plan != null && room != null ? g : 0);
            double w2 = aa != null ? W(aa, sa) : 0, h2 = aa != null ? H(aa, sa) : 0;
            double w3 = bb != null ? W(bb, sb) : 0, h3 = bb != null ? H(bb, sb) : 0;
            if (h1 > innerH + eps || h2 > innerH + eps || h3 > innerH + eps)
                return null;

            double x2 = x0 + w1 + (w1 > 0 ? g : 0);          // column 2, tight to column 1
            double x3 = x2 + w2 + (w2 > 0 ? g : 0);          // column 3
            bool stacked = false;
            if (bb != null && w3 > xr - x3 + eps)
            {
                // B-B above A-A, reaching left over its column (never over the plans)
                if (aa == null || xr - w3 < x2 - eps || h2 + h3 + g > innerH + eps)
                    return null;
                stacked = true;
            }
            if (x3 > xr + eps)
                return null;

            var fish = aa != null ? details.Where(d => d.Role == FishRole).ToList() : new List<SheetViewRequest>();
            var stack = details.Where(d => Array.IndexOf(StackRoles, d.Role) >= 0)
                .Select((d, i) => new { d, i }).OrderBy(o => Array.IndexOf(StackRoles, o.d.Role)).ThenBy(o => o.i).Select(o => o.d).ToList();
            var pool = details.Where(d => !fish.Contains(d) && !stack.Contains(d)).ToList();
            if (fishToGroup)
            {
                // the whole details group is packed again under B-B (the stacked details give way to each other as well)
                pool.AddRange(fish);
                pool.AddRange(stack);
                fish = new List<SheetViewRequest>();
                stack = new List<SheetViewRequest>();
            }

            // column 3: the stacked details take the largest scales that fit together under B-B; when they do not fit the last one
            // (and then the next) goes to the bands left free
            double stackTop = bb != null ? yt - h3 - g : yt;
            double stackHeight = stackTop - yb;
            double availW = xr - x3;
            int[] stackScales = null;
            double ws = 0, hs = 0;
            int keep = stack.Count;
            for (; keep >= 0; keep--)
            {
                stackScales = ChooseStack(stack.Take(keep).ToList(), availW, stackHeight, g, out ws, out hs);
                if (stackScales != null)
                    break;
            }
            if (stackScales == null)
                return null;
            // The cells hold only when every detail fits its own: the stacked ones under B-B and the fish plate above A-A. Otherwise
            // all of them are packed together, in rows, into the band under B-B and the other bands left free.
            bool cells = keep == stack.Count;
            double aaTop = yb + h2;
            double bbLeft = xr - w3;
            if (!cells)
            {
                pool.AddRange(stack);
                pool.AddRange(fish);
                stack = new List<SheetViewRequest>();
                fish = new List<SheetViewRequest>();
                stackScales = new int[0];
                ws = hs = 0;
            }

            // the fish plate: centred in the free cell above A-A, left of B-B and of the stacked details
            int fishScale = 0;
            double fishX = 0, fishY = 0;
            // the stacked details stand under B-B: right-aligned with it, or centred under it when B-B is much wider
            double stackRight = xr;
            if (!stacked && bb != null && stack.Count > 0 && w3 > ws + 2 * g)
                stackRight = Math.Max(x3 + ws, bbLeft + w3 / 2 + ws / 2);
            double stackLeft = stackRight - ws;
            double fishRight = Math.Min(xr, Math.Min(bb != null ? bbLeft - g : xr, stack.Count > 0 ? stackLeft - g : xr));
            double fishCellBottom = aaTop + g;
            if (fish.Count > 0)
            {
                var d = fish[0];
                double cellW = fishRight - x2, cellH = yt - fishCellBottom;
                foreach (int s in (d.AllowedScales ?? StandardScales).OrderByDescending(v => -v))
                {
                    if (s < d.MinScaleDenominator || (d.MaxScaleDenominator > 0 && s > d.MaxScaleDenominator))
                        continue;
                    // the fish plate and side guide rail keep their template scale in their cell (the user's sheet did not enlarge them)
                    if (d.MaxScaleDenominator > 0 && s != d.MaxScaleDenominator)
                        continue;
                    double w = W(d, s), h = H(d, s);
                    if (w > cellW + eps || h > cellH + eps)
                        continue;
                    fishScale = s;
                    fishX = Math.Min(Math.Max(x2 + w2 / 2 - w / 2, x2), fishRight - w);
                    fishY = fishCellBottom + (cellH - h) / 2;
                    break;
                }
                if (fishScale == 0)
                {
                    // above A-A there is no room for it: the stacked details join the pool, the band under B-B takes them all
                    pool.Add(d);
                    pool.AddRange(stack);
                    fish = new List<SheetViewRequest>();
                    stack = new List<SheetViewRequest>();
                    stackScales = new int[0];
                    ws = hs = 0;
                }
            }

            // column 2 moves right by half of the slack between the columns (not when B-B reaches over it, nor when details wait)
            if (!stacked && w2 > 0 && pool.Count == 0)
            {
                double col3Left = xr - Math.Max(ws, bb == null ? 0 : w3);
                double slack = col3Left - g - (x2 + w2);
                if (slack > 0)
                {
                    double base2 = x2;
                    x2 += slack / 2;
                    if (fishScale != 0)
                    {
                        // the fish plate stays centred over A-A as far as the band beside B-B allows
                        double fw = W(fish[0], fishScale);
                        fishX = Math.Min(Math.Max(x2 + w2 / 2 - fw / 2, base2), fishRight - fw);
                    }
                }
            }

            var l = new Layout();
            var column = new List<int>();
            var free = new List<Shelf>();
            // column 1: the shaft plan at the bottom, the machine-room plan at the top (directly above it when details wait for space)
            if (plan != null)
            {
                l.Viewports.Add(Place(plan, sp, x0 + (w1 - wPlan) / 2, yb));
                column.Add(1);
            }
            if (room != null)
            {
                double ry = plan != null ? yb + hPlan + g : yb;
                if (pool.Count == 0 && plan != null)
                    ry = yt - hRoom;
                l.Viewports.Add(Place(room, sm, x0 + (w1 - wRoom) / 2, ry));
                column.Add(1);
                if (pool.Count > 0 && yt - (ry + hRoom + g) > 0)
                    free.Add(Shelf.Of(new SheetRect(x0, ry + hRoom + g, w1, yt - (ry + hRoom + g)), 1));
            }
            else if (plan != null && pool.Count > 0 && yt - (yb + hPlan + g) > 0)
                free.Add(Shelf.Of(new SheetRect(x0, yb + hPlan + g, w1, yt - (yb + hPlan + g)), 1));
            // column 2
            if (aa != null)
            {
                l.Viewports.Add(Place(aa, sa, x2, yb));
                column.Add(2);
            }
            // B-B
            if (bb != null)
            {
                l.Viewports.Add(Place(bb, sb, bbLeft, yt - h3));
                column.Add(3);
            }
            if (fishScale != 0)
            {
                l.Viewports.Add(Place(fish[0], fishScale, fishX, fishY));
                column.Add(2);
            }
            // column 3: the stacked details, right-aligned as one column, top to bottom under B-B (space between when nothing waits)
            double stackBottom = stackTop;
            if (stack.Count > 0)
            {
                double cx = stackRight - ws / 2;
                double gap = g;
                if (pool.Count == 0 && stack.Count > 1)
                    gap = g + Math.Max(0, stackHeight - hs) / (stack.Count - 1);
                double y = stackTop;
                for (int i = 0; i < stack.Count; i++)
                {
                    double w = W(stack[i], stackScales[i]), h = H(stack[i], stackScales[i]);
                    y -= h;
                    l.Viewports.Add(Place(stack[i], stackScales[i], cx - w / 2, y));
                    column.Add(3);
                    y -= gap;
                }
                stackBottom = y + gap - g;
            }
            if (pool.Count > 0)
            {
                // the bands left free: under the stacked details, and above A-A beside B-B
                double left3 = stack.Count > 0 ? stackLeft : x3;
                double right3 = stack.Count > 0 ? stackRight : xr;
                if (stackBottom - yb > 0 && right3 - left3 > 0)
                    free.Add(Shelf.Of(new SheetRect(left3, yb, right3 - left3, stackBottom - yb), 3));
                if (aa != null && fishScale == 0 && !fishToGroup)
                {
                    double right = Math.Min(xr, Math.Min(bb != null ? bbLeft - g : xr, stack.Count > 0 ? stackLeft - g : xr));
                    if (yt - fishCellBottom > 0 && right - x2 > 0)
                        free.Add(Shelf.Of(new SheetRect(x2, fishCellBottom, right - x2, yt - fishCellBottom), 2));
                }
                free = free.OrderByDescending(f => f.Box.Area).ThenBy(f => f.Box.X).ThenBy(f => f.Box.Y).ToList();
                if (!PackShrinking(pool, free, l.Viewports, column, 0))
                    return null;
            }
            l.Filled = l.Viewports.Sum(v => v.Paper.Area);
            // The main views decide first (the user's sheet keeps A-A at 1:50 although a narrower A-A would let a detail grow); the
            // details only break ties between equal main views.
            l.Score = l.Viewports.Sum(v => (v.Kind == SheetViewKind.Detail ? GridDetailWeight : 1.0) * Math.Sqrt(v.Paper.Area))
                - (plan != null && room != null && sp != sm ? PlanScalePenalty : 0);
            return l;
        }

        static int PriorityOf(string role)
        {
            switch (role)
            {
                case "RailBrackets": return 0;
                case "RailsPlan": return 1;
                default: return 2;
            }
        }

        // The scales of the stacked details: the largest that fit the width and the height together, enlarging in the priority above.
        // Null when even the smallest allowed scales do not fit.
        static int[] ChooseStack(List<SheetViewRequest> stack, double availW, double stackHeight, double g, out double ws, out double hs)
        {
            ws = 0;
            hs = 0;
            const double eps = 1e-9;
            if (stack.Count == 0)
                return new int[0];
            var options = stack.Select(d => (d.AllowedScales ?? StandardScales).OrderByDescending(v => -v)
                .Where(v => v >= d.MinScaleDenominator && (d.MaxScaleDenominator <= 0 || v <= d.MaxScaleDenominator)).ToArray()).ToList();
            if (options.Any(o => o.Length == 0))
                return null;
            // The enlargement priority of the user's own sheet: the rail-bracket elevation first (he enlarged it to 1:5), then the guides
            // and brackets plan, then the door; a detail gets the larger scale only when the ones before it have theirs.
            var order = Enumerable.Range(0, stack.Count).OrderBy(i => PriorityOf(stack[i].Role)).ThenBy(i => i).ToArray();
            int[] pick = new int[stack.Count];
            int[] bestKey = null;
            int[] bestPick = null;
            while (true)
            {
                double sumH = (stack.Count - 1) * g;
                bool ok = true;
                for (int i = 0; i < stack.Count && ok; i++)
                {
                    int s = options[i][pick[i]];
                    // an enlarged detail (a larger scale than the template's) keeps a margin to the neighbouring column
                    double limit = stack[i].MaxScaleDenominator > 0 && s < stack[i].MaxScaleDenominator ? EnlargedWidthShare * availW : availW;
                    if (W(stack[i], s) > limit + eps)
                        ok = false;
                    sumH += H(stack[i], s);
                }
                if (ok && sumH <= stackHeight + eps)
                {
                    var key = order.Select(i => options[i][pick[i]]).ToArray();
                    bool better = bestKey == null;
                    for (int k2 = 0; !better && k2 < key.Length; k2++)
                    {
                        if (key[k2] != bestKey[k2])
                        {
                            better = key[k2] < bestKey[k2];
                            break;
                        }
                    }
                    if (better)
                    {
                        bestKey = key;
                        bestPick = (int[])pick.Clone();
                    }
                }
                int k = 0;
                while (k < pick.Length && ++pick[k] >= options[k].Length)
                    pick[k++] = 0;
                if (k >= pick.Length)
                    break;
            }
            if (bestPick == null)
                return null;
            var scales = new int[stack.Count];
            for (int i = 0; i < stack.Count; i++)
            {
                scales[i] = options[i][bestPick[i]];
                ws = Math.Max(ws, W(stack[i], scales[i]));
                hs += H(stack[i], scales[i]);
            }
            hs += (stack.Count - 1) * g;
            return scales;
        }

        /// <summary>The weight of a detail against a main view in the grid search (the main views decide first).</summary>
        public const double GridDetailWeight = 0.001;

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
            // B-B does not fit beside A-A: it may sit above A-A and reach left over its column (never over the plans) when both
            // stack within the height (the user's own arrangement of the sheet).
            bool stacked = false;
            if (third < -1e-9)
                return null;
            if (bb != null && w3 > third + 1e-9)
            {
                double beside = innerW - w1 - (w1 > 0 ? g : 0);
                if (aa != null && w3 <= beside + 1e-9 && h2 + h3 + g <= innerH + 1e-9)
                    stacked = true;
                else
                    return null;
            }

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
                double ceiling = stacked ? top - h3 - g : top; // B-B above A-A takes the top of this column
                if (ceiling - y > 0)
                    free.Add(Shelf.Of(new SheetRect(x, y, w2, ceiling - y), 2));
                x += w2 + g;
            }
            if (third > 1e-9 || stacked)
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
            if (details.Count > 0 && !Pack(details, free, l.Viewports, column, label))
                return null;
            l.Filled = l.Viewports.Sum(v => v.Paper.Area);
            l.Score = l.Viewports.Sum(v => (v.Kind == SheetViewKind.Detail ? DetailWeight : 1.0) * Math.Sqrt(v.Paper.Area))
                - (plan != null && room != null && sp != sm ? PlanScalePenalty : 0);
            return l;
        }

        // Shelf packing: the details, the largest first, each at the largest standard scale that still fits, in rows from the top of
        // the free bands (the band under B-B first).
        // Packs the details; when they do not fit, the largest enlarged one goes one scale step smaller and the packing starts again
        // (the details give way to each other, never the main views).
        static bool PackShrinking(List<SheetViewRequest> details, List<Shelf> shelves, List<SheetViewport> placed, List<int> column, double label)
        {
            var floor = new Dictionary<SheetViewRequest, int>();
            for (int attempt = 0; attempt < 60; attempt++)
            {
                var copy = shelves.Select(sh => Shelf.Of(sh.Box, sh.Column)).ToList();
                var tryPlaced = new List<SheetViewport>();
                var tryColumn = new List<int>();
                if (Pack(details, copy, tryPlaced, tryColumn, label, floor))
                {
                    placed.AddRange(tryPlaced);
                    column.AddRange(tryColumn);
                    return true;
                }
                SheetViewRequest pick = null;
                int next = 0;
                double area = -1;
                foreach (var v in tryPlaced)
                {
                    var d = details.FirstOrDefault(x => (x.Name ?? x.Kind.ToString()) == v.Name);
                    if (d == null)
                        continue;
                    var larger = (d.AllowedScales ?? StandardScales).Where(x => x > v.ScaleDenominator && x >= d.MinScaleDenominator
                        && (d.MaxScaleDenominator <= 0 || x <= d.MaxScaleDenominator)).OrderBy(x => x).ToList();
                    if (larger.Count > 0 && v.Paper.Area > area)
                    {
                        area = v.Paper.Area;
                        pick = d;
                        next = larger[0];
                    }
                }
                if (pick == null)
                    return false;
                floor[pick] = next;
            }
            return false;
        }

        static bool Pack(List<SheetViewRequest> details, List<Shelf> shelves, List<SheetViewport> placed, List<int> column, double label, Dictionary<SheetViewRequest, int> floor = null)
        {
            double g = Spacing;
            var order = details.Select((d, i) => new { d, i })
                .OrderByDescending(o => W(o.d, 1) * H(o.d, 1)).ThenBy(o => o.i).Select(o => o.d).ToList();
            foreach (var d in order)
            {
                bool done = false;
                int minScale = d.MinScaleDenominator, raised;
                if (floor != null && floor.TryGetValue(d, out raised))
                    minScale = Math.Max(minScale, raised);
                foreach (int s in (d.AllowedScales ?? StandardScales).OrderByDescending(x => -x))
                {
                    if (s < minScale || (d.MaxScaleDenominator > 0 && s > d.MaxScaleDenominator))
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
