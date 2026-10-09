using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Elevator.Reporting.Figures
{
    /// <summary>A figure of the calculation sheet: layers, entities and view; rendered to SVG or PNG.</summary>
    public sealed class Figure
    {
        public string Title;
        public string Id;
        public bool Schematic;
        public double WidthMm = 165;
        public double HeightMm;
        public FigureView View; // main model-to-paper transform (recorded in the SVG for verification)
        public readonly List<FigureEntity> Entities = new List<FigureEntity>();

        public IEnumerable<FigureText> Texts
        {
            get
            {
                return Entities.OfType<FigureText>();
            }
        }

        // Font chain per the drawing standard; measured by the WPF renderer.
        public static string FontFamily
        {
            get
            {
                return FigureFonts.Family;
            }
        }

        public static Func<string, double, double> Measure = (text, heightMm) => text.Length * heightMm * 0.62;

        /// <summary>Formats a number with a format string.</summary>
        public static string Num(double v, string format)
        {
            return v.ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>Formats newtons as kN.</summary>
        public static string Kn(double newtons)
        {
            return Num(newtons / 1000, "0.###") + " kN";
        }

        /// <summary>Formats a length in mm.</summary>
        public static string Mm(double mm)
        {
            return Num(mm, "0.#");
        }

        /// <summary>Adds a line.</summary>
        public FigurePolyline Line(FigurePoint a, FigurePoint b, FigureLayer layer, string tag = null)
        {
            var l = new FigurePolyline { Layer = layer, Tag = tag };
            l.Points.Add(a);
            l.Points.Add(b);
            Entities.Add(l);
            return l;
        }

        /// <summary>Adds a polyline or polygon.</summary>
        public FigurePolyline Poly(IEnumerable<FigurePoint> points, bool closed, FigureLayer layer, string tag = null, bool filled = false)
        {
            var l = new FigurePolyline { Layer = layer, Closed = closed, Filled = filled, Tag = tag };
            l.Points.AddRange(points);
            Entities.Add(l);
            return l;
        }

        /// <summary>Adds a rectangle.</summary>
        public FigurePolyline Rect(double x0, double y0, double x1, double y1, FigureLayer layer, string tag = null)
        {
            return Poly(
                new[] { new FigurePoint(x0, y0), new FigurePoint(x1, y0), new FigurePoint(x1, y1), new FigurePoint(x0, y1) },
                true,
                layer,
                tag);
        }

        /// <summary>Adds a circle.</summary>
        public FigureCircle Circle(FigurePoint c, double r, FigureLayer layer, bool filled = false, string tag = null)
        {
            var e = new FigureCircle { Centre = c, Radius = r, Layer = layer, Filled = filled, Tag = tag };
            Entities.Add(e);
            return e;
        }

        /// <summary>ANSI31: 45° lines, 1.5 mm pitch, clipped to the (simple) polygon.</summary>
        public void Hatch(IList<FigurePoint> polygon, double pitchMm = 1.5)
        {
            double c = Math.Cos(Math.PI / 4), s = Math.Sin(Math.PI / 4);
            Func<FigurePoint, FigurePoint> rot = p => new FigurePoint(p.X * c + p.Y * s, -p.X * s + p.Y * c);
            Func<FigurePoint, FigurePoint> back = p => new FigurePoint(p.X * c - p.Y * s, p.X * s + p.Y * c);
            var r = polygon.Select(rot).ToList();
            double min = r.Min(p => p.Y), max = r.Max(p => p.Y);
            for (double y = Math.Ceiling(min / pitchMm) * pitchMm; y < max; y += pitchMm)
            {
                var xs = new List<double>();
                for (int i = 0; i < r.Count; i++)
                {
                    var a = r[i];
                    var b = r[(i + 1) % r.Count];
                    if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y))
                        xs.Add(a.X + (y - a.Y) / (b.Y - a.Y) * (b.X - a.X));
                }
                xs.Sort();
                for (int i = 0; i + 1 < xs.Count; i += 2)
                    Line(back(new FigurePoint(xs[i], y)), back(new FigurePoint(xs[i + 1], y)), FigureLayer.Hatch);
            }
        }

        /// <summary>Filled closed arrowhead with its tip at 'tip', pointing along 'direction'.</summary>
        public void Arrowhead(FigurePoint tip, FigurePoint direction, FigureLayer layer, double lengthMm = 2.5, double halfWidthMm = 0.45)
        {
            var d = direction.Unit;
            var n = d.Normal;
            var baseCentre = tip - d * lengthMm;
            Poly(new[] { tip, baseCentre + n * halfWidthMm, baseCentre - n * halfWidthMm }, true, layer, null, true);
        }

        bool Overlaps(double x0, double y0, double x1, double y1, FigureText self)
        {
            foreach (var t in Texts)
            {
                if (t == self)
                    continue;
                double a0, b0, a1, b1;
                t.Box(out a0, out b0, out a1, out b1);
                if (x0 < a1 && a0 < x1 && y0 < b1 && b0 < y1)
                    return true;
            }
            return false;
        }

        /// <summary>Adds text at the first candidate position whose box does not overlap existing text.</summary>
        public FigureText Text(
            string content,
            IList<FigurePoint> candidates,
            string anchor = "start",
            FigureLayer layer = null,
            double heightMm = 2.5,
            bool vertical = false,
            string tag = null,
            string value = null,
            bool title = false)
        {
            // The Word SVG renderer adds space around U+002D between letters and ISOCPEUR has no U+2011, so word hyphens become spaces.
            content = System.Text.RegularExpressions.Regex.Replace(content ?? "", @"(?<=\p{L})-(?=\p{L})", " ");
            var t = new FigureText
            {
                Content = content,
                Anchor = anchor,
                Layer = layer ?? FigureLayer.Text,
                HeightMm = heightMm,
                Vertical = vertical,
                Tag = tag,
                Value = value,
                Title = title
            };
            t.WidthMm = Measure(content, heightMm);
            foreach (var c in candidates)
            {
                t.At = c;
                double x0, y0, x1, y1;
                t.Box(out x0, out y0, out x1, out y1);
                if (!Overlaps(x0, y0, x1, y1, t))
                {
                    Entities.Add(t);
                    return t;
                }
            }
            t.At = candidates[candidates.Count - 1];
            Entities.Add(t);
            return t; // reported by the overlap test if no candidate was free
        }

        /// <summary>Adds a text.</summary>
        public FigureText Text(
            string content,
            FigurePoint at,
            string anchor = "start",
            FigureLayer layer = null,
            double heightMm = 2.5,
            bool vertical = false,
            string tag = null,
            string value = null,
            bool title = false)
        {
            var candidates = new List<FigurePoint> { at };
            for (int i = 1; i <= 6; i++)
            {
                double step = i * (heightMm + 0.8);
                candidates.Add(vertical ? new FigurePoint(at.X - step, at.Y) : new FigurePoint(at.X, at.Y - step));
                candidates.Add(vertical ? new FigurePoint(at.X + step, at.Y) : new FigurePoint(at.X, at.Y + step));
            }
            return Text(content, candidates, anchor, layer, heightMm, vertical, tag, value, title);
        }

        /// <summary>
        /// Horizontal dimension between p1 and p2 with its line at yDim. Extension lines leave a 0.8 mm gap at the geometry and overshoot
        /// the dimension line by 1.5 mm; filled closed arrows; 2.5 mm text above the line.
        /// </summary>
        public void DimH(FigurePoint p1, FigurePoint p2, double yDim, string text, string tag = null, string value = null)
        {
            foreach (var p in new[] { p1, p2 })
            {
                double s = yDim > p.Y ? 1 : -1;
                Line(
                    new FigurePoint(p.X, p.Y + 0.8 * s),
                    new FigurePoint(p.X, yDim + 1.5 * s),
                    FigureLayer.Dimension,
                    tag == null ? null : tag + "-ext");
            }
            var a = new FigurePoint(p1.X, yDim);
            var b = new FigurePoint(p2.X, yDim);
            Line(a, b, FigureLayer.Dimension, tag).Value = value;
            Arrowhead(a, a - b, FigureLayer.Dimension);
            Arrowhead(b, b - a, FigureLayer.Dimension);
            // Text that does not fit between the arrows goes beyond the right-hand end.
            double room = Math.Abs(b.X - a.X) - 6;
            if (Measure(text, 2.5) <= room)
            {
                Text(
                                    text,
                                    new FigurePoint((a.X + b.X) / 2, yDim - 0.8),
                                    "middle",
                                    FigureLayer.Text,
                                    2.5,
                                    false,
                                    tag == null ? null : tag + "-text",
                                    value);
            }
            else
            {
                Text(
                                    text,
                                    new FigurePoint(Math.Max(a.X, b.X) + 1.5, yDim - 0.8),
                                    "start",
                                    FigureLayer.Text,
                                    2.5,
                                    false,
                                    tag == null ? null : tag + "-text",
                                    value);
            }
        }

        /// <summary>Vertical dimension between p1 and p2 with its line at xDim; text reads upwards on the left of the line.</summary>
        public void DimV(FigurePoint p1, FigurePoint p2, double xDim, string text, string tag = null, string value = null)
        {
            foreach (var p in new[] { p1, p2 })
            {
                double s = xDim > p.X ? 1 : -1;
                Line(
                    new FigurePoint(p.X + 0.8 * s, p.Y),
                    new FigurePoint(xDim + 1.5 * s, p.Y),
                    FigureLayer.Dimension,
                    tag == null ? null : tag + "-ext");
            }
            var a = new FigurePoint(xDim, p1.Y);
            var b = new FigurePoint(xDim, p2.Y);
            Line(a, b, FigureLayer.Dimension, tag).Value = value;
            Arrowhead(a, a - b, FigureLayer.Dimension);
            Arrowhead(b, b - a, FigureLayer.Dimension);
            double room = Math.Abs(b.Y - a.Y) - 6;
            if (Measure(text, 2.5) <= room)
            {
                Text(
                                    text,
                                    new FigurePoint(xDim - 0.8, (a.Y + b.Y) / 2),
                                    "middle",
                                    FigureLayer.Text,
                                    2.5,
                                    true,
                                    tag == null ? null : tag + "-text",
                                    value);
            }
            else
            {
                Text(
                                    text,
                                    new FigurePoint(xDim - 0.8, Math.Min(a.Y, b.Y) - 1.5),
                                    "start",
                                    FigureLayer.Text,
                                    2.5,
                                    true,
                                    tag == null ? null : tag + "-text",
                                    value);
            }
        }

        /// <summary>Load arrow from tail to tip in the accent colour, label near the tail.</summary>
        public void Load(
            FigurePoint tail,
            FigurePoint tip,
            string label,
            IList<FigurePoint> labelCandidates,
            string anchor,
            string tag,
            string value)
        {
            Line(tail, tip - (tip - tail).Unit * 2.0, FigureLayer.Load, tag).Value = value;
            Arrowhead(tip, tip - tail, FigureLayer.Load, 3.0, 0.6);
            Text(label, labelCandidates, anchor, FigureLayer.LoadText, 2.5, false, tag == null ? null : tag + "-text", value);
        }
    }
}
