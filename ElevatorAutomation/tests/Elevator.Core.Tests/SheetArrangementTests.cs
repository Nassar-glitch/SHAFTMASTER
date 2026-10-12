using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Elevator.Core.Layouts;
using Xunit;

namespace Elevator.Core.Tests
{
    // Cloud C2: the arrangement of the A0 geared sheet (DESIGN-FLOW "Sheet arrangement") over a grid of projects: 2 to 20 stops, shafts
    // 1100 to 2600 x 1300 to 2800, three machine-room sizes. The model extents are those of the generated views with their dimensions
    // (walls 250 mm, a dimension band of 600 / 750 mm each side); the details are the template's reference windows
    // (Templates/Layout_Geared_A0.details.json).
    public class SheetArrangementTests
    {
        public const double Wall = 250, PlanDims = 600, SectionDims = 750, Pit = 1300, Overhead = 4200, RoomHeight = 3000, Slab = 300;

        // Machine rooms: left, right, front, rear extensions beyond the shaft (mm).
        public static readonly double[][] Rooms = { new double[] { 0, 0, 0, 0 }, new double[] { 500, 500, 0, 1000 }, new double[] { 1000, 0, 500, 1500 } };

        // The template's detail windows (mm) and scales: fish plate, rails plan, rail brackets (1:10), door elevation and section (1:20).
        public static readonly double[][] Details = { new double[] { 1067, 1662, 10 }, new double[] { 2094, 808, 10 }, new double[] { 968, 912, 10 }, new double[] { 3822, 4108, 20 } };

        static SheetViewRequest View(SheetViewKind kind, string name, double w, double h, double x = 0, double y = 0)
        {
            return new SheetViewRequest { Kind = kind, Name = name, ModelExtents = new SheetRect(x, y, w, h) };
        }

        public static List<SheetViewRequest> Project(int stops, double sw, double sd, double[] room, double floor = 3000, bool details = true)
        {
            double travel = (stops - 1) * floor;
            var v = new List<SheetViewRequest>
            {
                View(SheetViewKind.ShaftPlan, "Shaft plan", sw + 2 * Wall + 2 * PlanDims, sd + 2 * Wall + 2 * PlanDims, 0, 0),
                View(SheetViewKind.MachineRoomPlan, "Machine-room plan", sw + room[0] + room[1] + 2 * Wall + 2 * PlanDims,
                    sd + room[2] + room[3] + 2 * Wall + 2 * PlanDims, 0, 20000),
                View(SheetViewKind.SectionAA, "Section A-A", sd + 2 * Wall + 2 * SectionDims,
                    Pit + travel + Overhead + Slab + RoomHeight + Slab + 2 * SectionDims, 10000, -Pit),
                View(SheetViewKind.SectionBB, "Section B-B", sw + room[0] + room[1] + 2 * Wall + 2 * SectionDims,
                    2000 + Slab + RoomHeight + Slab + 2 * SectionDims, 20000, travel)
            };
            if (details)
            {
                for (int i = 0; i < Details.Length; i++)
                {
                    v.Add(View(SheetViewKind.Detail, "Detail " + (i + 1), Details[i][0], Details[i][1], 1000000 + 5000 * i, 0));
                }
            }
            return v;
        }

        public static IEnumerable<object[]> Grid()
        {
            foreach (int stops in new[] { 2, 3, 4, 6, 8, 10, 12, 15, 18, 20 })
                foreach (double sw in new[] { 1100.0, 1600, 2100, 2600 })
                    foreach (double sd in new[] { 1300.0, 1800, 2300, 2800 })
                        for (int r = 0; r < Rooms.Length; r++)
                            yield return new object[] { stops, sw, sd, r };
        }

        // ---------- checks ----------
        // The target fill ratio (viewport area / drawing area) of the Planner. A project below it must be saturated: every main view
        // is at the largest standard scale that fits with the others unchanged (Saturated below), so no view could take the space.
        // Over this grid 6 of 480 projects are below 0.60 (min 0.574): a narrow (1100) deep (2300 / 2800) shaft without machine-room
        // extensions and 6 to 20 stops. A-A is held by the height (1:75 / 1:100), B-B is at 1:10, the plans at 1:10 and 1:20 (both
        // at 1:10 overflow the height), and the four details are placed at the largest scale that fits. The floor catches a real fault.
        public const double TargetFill = 0.60;
        public const double FloorFill = 0.55;

        // The largest empty rectangle (no view, label or gap in it) may hold at most this share of the drawing area.
        public const double MaximumEmptyShare = 0.30;

        static SheetRect Footprint(SheetViewport v)
        {
            return new SheetRect(v.Paper.X, v.Paper.Y - SheetArrangement.Spacing, v.Paper.Width, v.Paper.Height + SheetArrangement.Spacing);
        }

        static SheetRect Grow(SheetRect r, double d)
        {
            return new SheetRect(r.X - d, r.Y - d, r.Width + 2 * d, r.Height + 2 * d);
        }

        // The largest empty rectangle of the area, on a 5 mm grid, with every footprint grown by half a gap.
        public static double LargestEmpty(SheetArrangementResult r)
        {
            const double cell = 5;
            var a = r.Area;
            int nx = (int)Math.Ceiling(a.Width / cell), ny = (int)Math.Ceiling(a.Height / cell);
            var used = new bool[ny, nx];
            foreach (var v in r.Viewports)
            {
                var f = Grow(Footprint(v), SheetArrangement.Spacing / 2);
                int x0 = Math.Max(0, (int)Math.Floor((f.X - a.X) / cell)), x1 = Math.Min(nx, (int)Math.Ceiling((f.Right - a.X) / cell));
                int y0 = Math.Max(0, (int)Math.Floor((f.Y - a.Y) / cell)), y1 = Math.Min(ny, (int)Math.Ceiling((f.Top - a.Y) / cell));
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                        used[y, x] = true;
            }
            // the margin all round is not empty space
            int m = (int)Math.Ceiling(SheetArrangement.Spacing / cell);
            var heights = new int[nx];
            int best = 0;
            for (int y = m; y < ny - m; y++)
            {
                for (int x = 0; x < nx; x++)
                    heights[x] = x < m || x >= nx - m || used[y, x] ? 0 : heights[x] + 1;
                var stack = new Stack<int>();
                for (int x = 0; x <= nx; x++)
                {
                    int h = x == nx ? 0 : heights[x];
                    while (stack.Count > 0 && heights[stack.Peek()] >= h)
                    {
                        int top = heights[stack.Pop()];
                        int left = stack.Count == 0 ? -1 : stack.Peek();
                        best = Math.Max(best, top * (x - left - 1));
                    }
                    stack.Push(x);
                }
            }
            return best * cell * cell / a.Area;
        }

        static List<string> Problems(List<SheetViewRequest> req, SheetArrangementResult r)
        {
            var p = new List<string>();
            var a = r.Area;
            double g = SheetArrangement.Spacing;
            if (r.NotPlaced.Count > 0)
                p.Add("not placed: " + string.Join(", ", r.NotPlaced));
            if (r.Viewports.Count != req.Count)
                p.Add("viewports " + r.Viewports.Count + " for " + req.Count + " views");
            var inner = new SheetRect(a.X + g, a.Y + g, a.Width - 2 * g, a.Height - 2 * g);
            foreach (var v in r.Viewports)
            {
                var q = req.Single(x => (x.Name ?? x.Kind.ToString()) == v.Name);
                if (!inner.Contains(Footprint(v)))
                    p.Add(v.Name + " outside the area (margin " + g + "): " + Footprint(v));
                var scales = SheetArrangement.StandardScales;
                if (!scales.Contains(v.ScaleDenominator))
                    p.Add(v.Name + " scale 1:" + v.ScaleDenominator);
                if (Math.Abs(v.Paper.Width - (q.ModelExtents.Width + 2 * q.MarginMm) / v.ScaleDenominator) > 1e-9
                    || Math.Abs(v.Paper.Height - (q.ModelExtents.Height + 2 * q.MarginMm) / v.ScaleDenominator) > 1e-9)
                    p.Add(v.Name + " viewport is not extents / scale");
                if (Math.Abs(v.ModelCenterX - (q.ModelExtents.X + q.ModelExtents.Width / 2)) > 1e-9
                    || Math.Abs(v.ModelCenterY - (q.ModelExtents.Y + q.ModelExtents.Height / 2)) > 1e-9)
                    p.Add(v.Name + " model centre");
                if (Math.Abs(v.LabelX - (v.Paper.X + v.Paper.Width / 2)) > 1e-9 || !(v.LabelY < v.Paper.Y && v.LabelY > v.Paper.Y - g))
                    p.Add(v.Name + " label not under the view");
                if (v.LabelText != "1:" + v.ScaleDenominator)
                    p.Add(v.Name + " label text " + v.LabelText);
            }
            for (int i = 0; i < r.Viewports.Count; i++)
                for (int j = i + 1; j < r.Viewports.Count; j++)
                    if (Grow(Footprint(r.Viewports[i]), g / 2 - 1e-6).Overlaps(Grow(Footprint(r.Viewports[j]), g / 2 - 1e-6)))
                        p.Add(r.Viewports[i].Name + " and " + r.Viewports[j].Name + " closer than the gap");
            Func<SheetViewKind, SheetViewport> k = kind => r.Viewports.FirstOrDefault(v => v.Kind == kind);
            SheetViewport plan = k(SheetViewKind.ShaftPlan), room = k(SheetViewKind.MachineRoomPlan), aa = k(SheetViewKind.SectionAA), bb = k(SheetViewKind.SectionBB);
            if (plan != null && room != null)
            {
                if (!(Footprint(room).Y >= plan.Paper.Top))
                    p.Add("the machine-room plan is not above the shaft plan");
                if (Math.Abs(Footprint(plan).Y - inner.Y) > 1e-6)
                    p.Add("the shaft plan is not at the bottom");
            }
            if (plan != null && aa != null && !(aa.Paper.X >= Math.Max(plan.Paper.Right, room != null ? room.Paper.Right : 0)))
                p.Add("A-A is not right of the plans");
            if (aa != null && bb != null && !(bb.Paper.X >= aa.Paper.Right))
                p.Add("B-B is not right of A-A");
            if (bb != null && (Math.Abs(bb.Paper.Top - inner.Top) > 1e-6 || Math.Abs(bb.Paper.Right - inner.Right) > 1e-6))
                p.Add("B-B is not top right");
            if (r.FillRatio < FloorFill || (r.FillRatio < TargetFill && !Saturated(req, r)))
                p.Add("fill " + r.FillRatio.ToString("0.000", CultureInfo.InvariantCulture));
            double empty = LargestEmpty(r);
            if (empty > MaximumEmptyShare)
                p.Add("empty area " + empty.ToString("0.000", CultureInfo.InvariantCulture));
            double fill = r.Viewports.Sum(v => v.Paper.Area) / a.Area;
            if (Math.Abs(fill - r.FillRatio) > 1e-12)
                p.Add("fill ratio is not viewport area / drawing area");
            return p;
        }

        // True when no main view can take the next larger standard scale with the other main views unchanged: the columns (plans
        // stacked, A-A, B-B) would exceed the drawing area's height or width.
        public static bool Saturated(List<SheetViewRequest> req, SheetArrangementResult r)
        {
            double g = SheetArrangement.Spacing, innerW = r.Area.Width - 2 * g, innerH = r.Area.Height - 2 * g;
            var kinds = new[] { SheetViewKind.ShaftPlan, SheetViewKind.MachineRoomPlan, SheetViewKind.SectionAA, SheetViewKind.SectionBB };
            var scale = kinds.ToDictionary(k => k, k => r.Viewports.Where(v => v.Kind == k).Select(v => v.ScaleDenominator).DefaultIfEmpty(0).First());
            Func<SheetViewKind, double> w = k => scale[k] == 0 ? 0 : req.Single(v => v.Kind == k).ModelExtents.Width / scale[k];
            Func<SheetViewKind, double> h = k => scale[k] == 0 ? 0 : req.Single(v => v.Kind == k).ModelExtents.Height / scale[k] + g;
            Func<bool> fits = () =>
                h(SheetViewKind.ShaftPlan) + h(SheetViewKind.MachineRoomPlan) + g <= innerH + 1e-9
                && h(SheetViewKind.SectionAA) <= innerH + 1e-9 && h(SheetViewKind.SectionBB) <= innerH + 1e-9
                && Math.Max(w(SheetViewKind.ShaftPlan), w(SheetViewKind.MachineRoomPlan)) + w(SheetViewKind.SectionAA) + w(SheetViewKind.SectionBB) + 2 * g <= innerW + 1e-9;
            foreach (var k in kinds)
            {
                int s = scale[k];
                int i = Array.IndexOf(SheetArrangement.StandardScales, s);
                if (s == 0 || i <= 0)
                    continue;
                scale[k] = SheetArrangement.StandardScales[i - 1];
                bool larger = fits();
                scale[k] = s;
                if (larger)
                    return false;
            }
            return true;
        }

        static string Signature(SheetArrangementResult r)
        {
            return string.Join(";", r.Viewports.Select(v => string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2:R}:{3:R}:{4:R}:{5:R}",
                v.Name, v.ScaleDenominator, v.Paper.X, v.Paper.Y, v.Paper.Width, v.Paper.Height))) + "|" + r.FillRatio.ToString("R", CultureInfo.InvariantCulture);
        }

        [Fact]
        public void Every_project_of_the_grid_gets_a_composed_sheet()
        {
            var failures = new List<string>();
            var report = new List<string>();
            double maxEmpty = 0;
            var fills = new List<double>();
            foreach (var c in Grid())
            {
                int stops = (int)c[0]; double sw = (double)c[1], sd = (double)c[2]; int room = (int)c[3];
                var req = Project(stops, sw, sd, Rooms[room]);
                var r = SheetArrangement.Arrange(SheetArrangement.A0DrawingArea(), req);
                string name = string.Format(CultureInfo.InvariantCulture, "{0} stops, shaft {1} x {2}, room {3}", stops, sw, sd, room);
                failures.AddRange(Problems(req, r).Select(x => name + ": " + x));
                // deterministic: the same input gives the same sheet
                if (Signature(SheetArrangement.Arrange(SheetArrangement.A0DrawingArea(), Project(stops, sw, sd, Rooms[room]))) != Signature(r))
                    failures.Add(name + ": not deterministic");
                double empty = LargestEmpty(r);
                fills.Add(r.FillRatio);
                maxEmpty = Math.Max(maxEmpty, empty);
                report.Add(string.Format(CultureInfo.InvariantCulture, "{0}; fill {1:0.000}; largest empty {2:0.000}; {3}", name, r.FillRatio, empty,
                    string.Join(", ", r.Viewports.Select(v => v.Name + " " + v.LabelText))));
            }
            var sorted = fills.OrderBy(f => f).ToList();
            Assert.True(sorted[(sorted.Count - 1) / 2] >= TargetFill, "median fill below the target");
            report.Insert(0, string.Format(CultureInfo.InvariantCulture, "{0} projects; fill min {1:0.000} median {2:0.000} max {3:0.000}; below {4:0.00}: {5}; largest empty share {6:0.000}",
                report.Count, sorted.First(), (sorted[(sorted.Count - 1) / 2] + sorted[sorted.Count / 2]) / 2, sorted.Last(), TargetFill, fills.Count(f => f < TargetFill), maxEmpty));
            File.WriteAllLines(Path.Combine(TestData.Folder("sheet-arrangement"), "grid.txt"), report);
            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
        }

        [Fact]
        public void A_tall_section_takes_the_height_and_a_short_one_does_not_leave_the_space_above_it_empty()
        {
            var area = SheetArrangement.A0DrawingArea();
            var tall = SheetArrangement.Arrange(area, Project(15, 1600, 1800, Rooms[1]));
            var aa = tall.Viewports.Single(v => v.Kind == SheetViewKind.SectionAA);
            Assert.True(aa.Paper.Height > 0.8 * area.Height, "15 stops: A-A " + aa.Paper);
            foreach (int stops in new[] { 2, 3 })
            {
                var r = SheetArrangement.Arrange(area, Project(stops, 1600, 1800, Rooms[1]));
                var s = r.Viewports.Single(v => v.Kind == SheetViewKind.SectionAA);
                // over A-A: a larger scale, or the details use the space
                double above = area.Top - SheetArrangement.Spacing - s.Paper.Top;
                bool used = r.Viewports.Any(v => v.Kind == SheetViewKind.Detail && v.Paper.X < s.Paper.Right && v.Paper.Right > s.Paper.X && v.Paper.Y >= s.Paper.Top);
                Assert.True(above < 0.25 * area.Height || used, stops + " stops: " + above + " mm empty above A-A");
                Assert.True(s.ScaleDenominator <= 25, stops + " stops: A-A at 1:" + s.ScaleDenominator);
            }
        }

        [Fact]
        public void Optional_views_may_be_missing()
        {
            var area = SheetArrangement.A0DrawingArea();
            var all = Project(6, 1600, 1800, Rooms[0]);
            foreach (var drop in new[] { SheetViewKind.MachineRoomPlan, SheetViewKind.SectionBB, SheetViewKind.Detail })
            {
                var req = all.Where(v => v.Kind != drop).ToList();
                var r = SheetArrangement.Arrange(area, req);
                Assert.Empty(r.NotPlaced);
                Assert.Equal(req.Count, r.Viewports.Count);
                var p = Problems(req, r).Where(x => !x.StartsWith("fill") && !x.StartsWith("empty")).ToList();
                Assert.True(p.Count == 0, string.Join("\n", p));
            }
            var single = SheetArrangement.Arrange(area, all.Where(v => v.Kind == SheetViewKind.ShaftPlan).ToList());
            Assert.Equal(10, single.Viewports.Single().ScaleDenominator);
        }

        [Fact]
        public void Views_that_cannot_fit_are_reported_and_bad_input_is_refused()
        {
            var area = SheetArrangement.A0DrawingArea();
            var huge = new List<SheetViewRequest> { View(SheetViewKind.SectionAA, "A-A", 5000, 400000) };
            var r = SheetArrangement.Arrange(area, huge);
            Assert.Equal(new[] { "A-A" }, r.NotPlaced);
            Assert.Empty(r.Viewports);
            Assert.Equal(0, r.FillRatio);
            Assert.Throws<ArgumentException>(() => SheetArrangement.Arrange(new SheetRect(0, 0, 0, 100), huge));
            Assert.Throws<ArgumentNullException>(() => SheetArrangement.Arrange(area, null));
            Assert.Throws<ArgumentException>(() => SheetArrangement.Arrange(area, new List<SheetViewRequest> { View(SheetViewKind.Detail, "d", 0, 10) }));
            Assert.Throws<ArgumentException>(() => SheetArrangement.Arrange(area, new List<SheetViewRequest> { View(SheetViewKind.ShaftPlan, "a", 10, 10), View(SheetViewKind.ShaftPlan, "b", 10, 10) }));
        }

        [Fact]
        public void The_drawing_area_is_the_templates()
        {
            var a = SheetArrangement.A0DrawingArea();
            // inside the A0 sheet, left of the title-block column (x from 1017.8 mm)
            Assert.True(a.X > 0 && a.Y > 0 && a.Right < 1017.8 && a.Top < 841);
            Assert.True(a.Width > 1000 && a.Height > 815);
        }

        // ---------- renders ----------
        public static string Svg(SheetArrangementResult r, string title)
        {
            var inv = CultureInfo.InvariantCulture;
            var b = new StringBuilder();
            double W = 1189, H = 841;
            Func<double, string> n = v => v.ToString("0.#", inv);
            Func<double, double> Y = y => H - y;
            b.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ").Append(n(W)).Append(' ').Append(n(H)).Append("\" width=\"1189\" height=\"841\" font-family=\"Arial, sans-serif\">\n");
            b.Append("<rect x=\"0\" y=\"0\" width=\"1189\" height=\"841\" fill=\"#ffffff\" stroke=\"#000\" stroke-width=\"1\"/>\n");
            var a = r.Area;
            b.Append("<rect x=\"").Append(n(a.X)).Append("\" y=\"").Append(n(Y(a.Top))).Append("\" width=\"").Append(n(a.Width)).Append("\" height=\"").Append(n(a.Height)).Append("\" fill=\"#f7f7f7\" stroke=\"#555\" stroke-width=\"1\"/>\n");
            b.Append("<rect x=\"1017.8\" y=\"").Append(n(Y(a.Top))).Append("\" width=\"157\" height=\"").Append(n(a.Height)).Append("\" fill=\"#e8e8e8\" stroke=\"#555\"/>\n");
            b.Append("<text x=\"1096\" y=\"420\" font-size=\"14\" text-anchor=\"middle\" fill=\"#666\">title block</text>\n");
            var colors = new Dictionary<SheetViewKind, string> { { SheetViewKind.ShaftPlan, "#9ecae1" }, { SheetViewKind.MachineRoomPlan, "#c6dbef" }, { SheetViewKind.SectionAA, "#a1d99b" }, { SheetViewKind.SectionBB, "#fdae6b" }, { SheetViewKind.Detail, "#dadaeb" } };
            foreach (var v in r.Viewports)
            {
                var p = v.Paper;
                b.Append("<rect x=\"").Append(n(p.X)).Append("\" y=\"").Append(n(Y(p.Top))).Append("\" width=\"").Append(n(p.Width)).Append("\" height=\"").Append(n(p.Height))
                    .Append("\" fill=\"").Append(colors[v.Kind]).Append("\" stroke=\"#333\" stroke-width=\"1\"/>\n");
                b.Append("<text x=\"").Append(n(p.X + p.Width / 2)).Append("\" y=\"").Append(n(Y(p.Y + p.Height / 2))).Append("\" font-size=\"12\" text-anchor=\"middle\">")
                    .Append(System.Security.SecurityElement.Escape(v.Name)).Append("</text>\n");
                b.Append("<text x=\"").Append(n(v.LabelX)).Append("\" y=\"").Append(n(Y(v.LabelY) + 3)).Append("\" font-size=\"9\" text-anchor=\"middle\">").Append(v.LabelText).Append("</text>\n");
            }
            b.Append("<text x=\"20\" y=\"834\" font-size=\"12\">").Append(System.Security.SecurityElement.Escape(title)).Append(" · fill ").Append(r.FillRatio.ToString("0.00", inv)).Append("</text>\n");
            b.Append("</svg>\n");
            return b.ToString();
        }

        [Fact]
        public void Renders_of_2_6_and_15_stops_and_a_wide_shaft()
        {
            string dir = TestData.Folder("sheet-arrangement");
            var cases = new[]
            {
                new { Stops = 2, Sw = 1600.0, Sd = 1800.0, File = "02-stops" },
                new { Stops = 6, Sw = 1600.0, Sd = 1800.0, File = "06-stops" },
                new { Stops = 15, Sw = 1600.0, Sd = 1800.0, File = "15-stops" },
                new { Stops = 4, Sw = 2600.0, Sd = 2800.0, File = "wide-2600x2800-04-stops" }
            };
            foreach (var c in cases)
            {
                var r = SheetArrangement.Arrange(SheetArrangement.A0DrawingArea(), Project(c.Stops, c.Sw, c.Sd, Rooms[1]));
                Assert.Empty(r.NotPlaced);
                string svg = Svg(r, string.Format(CultureInfo.InvariantCulture, "{0} stops, shaft {1} x {2}", c.Stops, c.Sw, c.Sd));
                File.WriteAllText(Path.Combine(dir, "sheet-arrangement-" + c.File + ".svg"), svg);
                Assert.Contains("1:", svg);
            }
        }

        [Fact]
        public void The_dimension_margin_grows_the_viewport_around_the_same_centre()
        {
            var area = SheetArrangement.A0DrawingArea();
            var bare = SheetArrangement.Arrange(area, new List<SheetViewRequest> { View(SheetViewKind.ShaftPlan, "P", 3000, 3000, 100, 200) }).Viewports.Single();
            var req = View(SheetViewKind.ShaftPlan, "P", 3000, 3000, 100, 200);
            req.MarginMm = 500;
            var grown = SheetArrangement.Arrange(area, new List<SheetViewRequest> { req }).Viewports.Single();
            Assert.Equal(1600, bare.ModelCenterX);
            Assert.Equal(grown.ModelCenterX, bare.ModelCenterX);
            Assert.Equal(grown.ModelCenterY, bare.ModelCenterY);
            Assert.Equal(4000.0 / grown.ScaleDenominator, grown.Paper.Width, 9);
            Assert.Equal(3000.0 / bare.ScaleDenominator, bare.Paper.Width, 9);
            req.MarginMm = -1;
            Assert.Throws<ArgumentException>(() => SheetArrangement.Arrange(area, new List<SheetViewRequest> { req }));
        }

        [Fact]
        public void The_two_plans_share_a_scale_when_it_costs_little()
        {
            // A small shaft: both plans fit at 1:10 together.
            var r = SheetArrangement.Arrange(SheetArrangement.A0DrawingArea(), Project(4, 1100, 1300, Rooms[0]));
            var plan = r.Viewports.Single(v => v.Kind == SheetViewKind.ShaftPlan);
            var room = r.Viewports.Single(v => v.Kind == SheetViewKind.MachineRoomPlan);
            Assert.Equal(plan.ScaleDenominator, room.ScaleDenominator);
            Assert.Equal(10, plan.ScaleDenominator);
        }
    }
}
