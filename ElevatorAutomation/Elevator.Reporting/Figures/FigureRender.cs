using System;
using System.Linq;
using System.Text;
using System.Globalization;
using Elevator.Reporting.Docx;

namespace Elevator.Reporting.Figures
{
    // PNG rendering and text measurement for figures, supplied by the application (WPF in ShaftMaster: WpfFigureRasterizer).
    public interface IFigureRasterizer
    {
        // Sets Figure.Measure and FigureFonts.Resolve; called once, before the first figure is built.
        void Initialise();
        byte[] Png(Figure f, double dpi);
    }

    // SVG (vector) renderer for Figure, and the Word image (PNG fallback from the rasterizer, with the SVG attached).
    // Text sizes are ISO cap heights; the em size is cap height / 0.72.
    public static class FigureRender
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public const double CapRatio = 0.72;

        // Set by the application before reports are built; without it figures keep the default measure and have no PNG.
        public static IFigureRasterizer Rasterizer;
        static bool initialised;

        public static void Initialise()
        {
            if (initialised)
                return;
            initialised = true;
            if (Rasterizer != null)
                Rasterizer.Initialise();
        }

        static string F(double v)
        {
            return v.ToString("0.###", Inv);
        }

        public static string Dash(FigureLayer l)
        {
            return l.LineType == FigureLineType.Centre ? "6 1.2 0.6 1.2" : l.LineType == FigureLineType.Hidden ? "2.5 1.2" : null;
        }

        // SVG user unit = 0.1 mm: Word's SVG renderer spaces glyphs badly at very small font sizes in user units.
        public const double SvgUnitsPerMm = 10;

        static string U(double mm)
        {
            return (mm * SvgUnitsPerMm).ToString("0.##", Inv);
        }

        public static string Svg(Figure f)
        {
            var b = new StringBuilder();
            b.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" width=\"")
                .Append(F(f.WidthMm))
                .Append("mm\" height=\"")
                .Append(F(f.HeightMm))
                .Append("mm\" viewBox=\"0 0 ")
                .Append(U(f.WidthMm))
                .Append(" ")
                .Append(U(f.HeightMm))
                .Append("\" data-figure=\"")
                .Append(f.Id)
                .Append("\"")
                .Append(f.View == null ? "" : " data-view=\""
                    + F(f.View.Scale)
                    + ","
                    + F(f.View.MinX)
                    + ","
                    + F(f.View.MaxY)
                    + ","
                    + F(f.View.OffsetX)
                    + ","
                    + F(f.View.OffsetY)
                    + "\"")
                .Append(">");
            b.Append("<rect x=\"0\" y=\"0\" width=\"")
                .Append(U(f.WidthMm))
                .Append("\" height=\"")
                .Append(U(f.HeightMm))
                .Append("\" fill=\"#FFFFFF\"/>");
            string font = DocxDocument.Escape(Figure.FontFamily + ", 'Arial Narrow', Arial, sans-serif");
            foreach (var e in f.Entities)
            {
                string tag = e.Tag == null ? "" : " data-tag=\"" + DocxDocument.Escape(e.Tag) + "\"";
                if (e.Value != null)
                    tag += " data-value=\"" + DocxDocument.Escape(e.Value) + "\"";
                var p = e as FigurePolyline;
                if (p != null)
                {
                    string pts = String.Join(" ", p.Points.Select(q => U(q.X) + "," + U(q.Y)));
                    if (p.Filled)
                    {
                        b.Append("<polygon")
                            .Append(tag)
                            .Append(" points=\"")
                            .Append(pts)
                            .Append("\" fill=\"#")
                            .Append(p.Layer.Colour)
                            .Append("\" stroke=\"none\"/>");
                        continue;
                    }
                    b.Append(p.Closed ? "<polygon" : "<polyline")
                        .Append(tag)
                        .Append(" points=\"")
                        .Append(pts)
                        .Append("\" fill=\"none\" stroke=\"#")
                        .Append(p.Layer.Colour)
                        .Append("\" stroke-width=\"")
                        .Append(U(p.Layer.WeightMm))
                        .Append("\" stroke-linejoin=\"round\" stroke-linecap=\"butt\"");
                    var dash = Dash(p.Layer);
                    if (dash != null)
                        b.Append(" stroke-dasharray=\"")
                            .Append(String.Join(" ", dash.Split(' ').Select(x => U(Double.Parse(x, Inv)))))
                            .Append("\"");
                    b.Append("/>");
                    continue;
                }
                var c = e as FigureCircle;
                if (c != null)
                {
                    b.Append("<circle")
                        .Append(tag)
                        .Append(" cx=\"")
                        .Append(U(c.Centre.X))
                        .Append("\" cy=\"")
                        .Append(U(c.Centre.Y))
                        .Append("\" r=\"")
                        .Append(U(c.Radius))
                        .Append("\"")
                        .Append(c.Filled
                            ? " fill=\"#" + c.Layer.Colour + "\" stroke=\"none\""
                            : " fill=\"#FFFFFF\" stroke=\"#" + c.Layer.Colour + "\" stroke-width=\"" + U(c.Layer.WeightMm) + "\"")
                        .Append("/>");
                    continue;
                }
                var t = e as FigureText;
                if (t != null)
                {
                    b.Append("<text")
                        .Append(tag)
                        .Append(" x=\"")
                        .Append(U(t.At.X))
                        .Append("\" y=\"")
                        .Append(U(t.At.Y))
                        .Append("\" font-family=\"")
                        .Append(font)
                        .Append("\" font-size=\"")
                        .Append(U(t.HeightMm / CapRatio))
                        .Append("\" text-anchor=\"")
                        .Append(t.Anchor)
                        .Append("\" fill=\"#")
                        .Append(t.Layer.Colour)
                        .Append("\"");
                    if (t.Title)
                        b.Append(" font-weight=\"bold\"");
                    if (t.Vertical)
                        b.Append(" transform=\"rotate(-90 ").Append(U(t.At.X)).Append(" ").Append(U(t.At.Y)).Append(")\"");
                    b.Append(">").Append(DocxDocument.Escape(t.Content)).Append("</text>");
                }
            }
            b.Append("</svg>");
            return b.ToString();
        }

        public static byte[] Png(Figure f, double dpi = 300)
        {
            if (Rasterizer == null)
                throw new InvalidOperationException("No figure rasterizer is set (FigureRender.Rasterizer).");
            return Rasterizer.Png(f, dpi);
        }

        // Word image: PNG fallback with the SVG attached (Word 2016+ svgBlip).
        public static DocxImage Image(Figure f)
        {
            Initialise();
            return DocxImage.WithSvg(DocxImage.FromBytes(Png(f)), Encoding.UTF8.GetBytes(Svg(f)));
        }
    }
}
