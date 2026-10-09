using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Elevator.Solution.Desktop
{
 // SVG (vector) and 300 dpi PNG (fallback) renderers for Figure. Text sizes are ISO cap heights;
 // the em size is cap height / 0.72. Must run on an STA thread (WPF) for PNG and measurement.
 public static class FigureRender
 {
  static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
  const double CapRatio=0.72;
  static bool initialised;
  public static void Initialise()
  {
   if(initialised)return;initialised=true;
   FigureFonts.Resolve=()=>{var names=Fonts.SystemFontFamilies.Select(f=>f.Source).ToList();foreach(var n in new[]{"ISOCPEUR","Arial Narrow","Arial"})if(names.Any(x=>String.Equals(x,n,StringComparison.OrdinalIgnoreCase)))return n;return "Arial";};
   Figure.Measure=(text,heightMm)=>Formatted(text,heightMm,Brushes.Black).WidthIncludingTrailingWhitespace;
  }
  static FormattedText Formatted(string text,double heightMm,Brush brush)
  {
#pragma warning disable 618
   return new FormattedText(text??"",Inv,FlowDirection.LeftToRight,new Typeface(new FontFamily(Figure.FontFamily),FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),heightMm/CapRatio,brush);
#pragma warning restore 618
  }
  static string F(double v){return v.ToString("0.###",Inv);}
  static string Dash(FigureLayer l){return l.LineType==FigureLineType.Centre?"6 1.2 0.6 1.2":l.LineType==FigureLineType.Hidden?"2.5 1.2":null;}
  // SVG user unit = 0.1 mm: Word's SVG renderer spaces glyphs badly at very small font sizes in user units.
  public const double SvgUnitsPerMm=10;
  static string U(double mm){return (mm*SvgUnitsPerMm).ToString("0.##",Inv);}
  public static string Svg(Figure f)
  {
   var b=new StringBuilder();
   b.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" width=\"").Append(F(f.WidthMm)).Append("mm\" height=\"").Append(F(f.HeightMm)).Append("mm\" viewBox=\"0 0 ").Append(U(f.WidthMm)).Append(" ").Append(U(f.HeightMm)).Append("\" data-figure=\"").Append(f.Id).Append("\"").Append(f.View==null?"":" data-view=\""+F(f.View.Scale)+","+F(f.View.MinX)+","+F(f.View.MaxY)+","+F(f.View.OffsetX)+","+F(f.View.OffsetY)+"\"").Append(">");
   b.Append("<rect x=\"0\" y=\"0\" width=\"").Append(U(f.WidthMm)).Append("\" height=\"").Append(U(f.HeightMm)).Append("\" fill=\"#FFFFFF\"/>");
   string font=DocxDocument.Escape(Figure.FontFamily+", 'Arial Narrow', Arial, sans-serif");
   foreach(var e in f.Entities)
   {
    string tag=e.Tag==null?"":" data-tag=\""+DocxDocument.Escape(e.Tag)+"\"";if(e.Value!=null)tag+=" data-value=\""+DocxDocument.Escape(e.Value)+"\"";
    var p=e as FigurePolyline;
    if(p!=null)
    {
     string pts=String.Join(" ",p.Points.Select(q=>U(q.X)+","+U(q.Y)));
     if(p.Filled){b.Append("<polygon").Append(tag).Append(" points=\"").Append(pts).Append("\" fill=\"#").Append(p.Layer.Colour).Append("\" stroke=\"none\"/>");continue;}
     b.Append(p.Closed?"<polygon":"<polyline").Append(tag).Append(" points=\"").Append(pts).Append("\" fill=\"none\" stroke=\"#").Append(p.Layer.Colour).Append("\" stroke-width=\"").Append(U(p.Layer.WeightMm)).Append("\" stroke-linejoin=\"round\" stroke-linecap=\"butt\"");
     var dash=Dash(p.Layer);if(dash!=null)b.Append(" stroke-dasharray=\"").Append(String.Join(" ",dash.Split(' ').Select(x=>U(Double.Parse(x,Inv))))).Append("\"");b.Append("/>");continue;
    }
    var c=e as FigureCircle;
    if(c!=null){b.Append("<circle").Append(tag).Append(" cx=\"").Append(U(c.Centre.X)).Append("\" cy=\"").Append(U(c.Centre.Y)).Append("\" r=\"").Append(U(c.Radius)).Append("\"").Append(c.Filled?" fill=\"#"+c.Layer.Colour+"\" stroke=\"none\"":" fill=\"#FFFFFF\" stroke=\"#"+c.Layer.Colour+"\" stroke-width=\""+U(c.Layer.WeightMm)+"\"").Append("/>");continue;}
    var t=e as FigureText;
    if(t!=null)
    {
     b.Append("<text").Append(tag).Append(" x=\"").Append(U(t.At.X)).Append("\" y=\"").Append(U(t.At.Y)).Append("\" font-family=\"").Append(font).Append("\" font-size=\"").Append(U(t.HeightMm/CapRatio)).Append("\" text-anchor=\"").Append(t.Anchor).Append("\" fill=\"#").Append(t.Layer.Colour).Append("\"");
     if(t.Title)b.Append(" font-weight=\"bold\"");if(t.Vertical)b.Append(" transform=\"rotate(-90 ").Append(U(t.At.X)).Append(" ").Append(U(t.At.Y)).Append(")\"");
     b.Append(">").Append(DocxDocument.Escape(t.Content)).Append("</text>");
    }
   }
   b.Append("</svg>");return b.ToString();
  }
  static Brush Brush(string hex){var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString("#"+hex));b.Freeze();return b;}
  static Pen PenFor(FigureLayer l)
  {
   var pen=new Pen(Brush(l.Colour),l.WeightMm){LineJoin=PenLineJoin.Round,StartLineCap=PenLineCap.Flat,EndLineCap=PenLineCap.Flat};
   var dash=Dash(l);if(dash!=null)pen.DashStyle=new DashStyle(dash.Split(' ').Select(x=>Double.Parse(x,Inv)/l.WeightMm),0); // WPF dashes are in pen widths
   pen.Freeze();return pen;
  }
  public static byte[] Png(Figure f,double dpi=300)
  {
   var visual=new DrawingVisual();
   using(var dc=visual.RenderOpen())
   {
    double mm=96/25.4;dc.PushTransform(new ScaleTransform(mm,mm));
    dc.DrawRectangle(Brushes.White,null,new Rect(0,0,f.WidthMm,f.HeightMm));
    foreach(var e in f.Entities)
    {
     var p=e as FigurePolyline;
     if(p!=null)
     {
      var geometry=new StreamGeometry();using(var g=geometry.Open()){g.BeginFigure(new Point(p.Points[0].X,p.Points[0].Y),p.Filled,p.Closed||p.Filled);g.PolyLineTo(p.Points.Skip(1).Select(q=>new Point(q.X,q.Y)).ToList(),true,true);}geometry.Freeze();
      dc.DrawGeometry(p.Filled?Brush(p.Layer.Colour):null,p.Filled?null:PenFor(p.Layer),geometry);continue;
     }
     var c=e as FigureCircle;
     if(c!=null){dc.DrawEllipse(c.Filled?Brush(c.Layer.Colour):Brushes.White,c.Filled?null:PenFor(c.Layer),new Point(c.Centre.X,c.Centre.Y),c.Radius,c.Radius);continue;}
     var t=e as FigureText;
     if(t!=null)
     {
      var ft=Formatted(t.Content,t.HeightMm,Brush(t.Layer.Colour));if(t.Title)ft.SetFontWeight(FontWeights.Bold);
      double width=ft.WidthIncludingTrailingWhitespace,dx=t.Anchor=="middle"?-width/2:t.Anchor=="end"?-width:0;
      if(t.Vertical)dc.PushTransform(new RotateTransform(-90,t.At.X,t.At.Y));
      dc.DrawText(ft,new Point(t.At.X+dx,t.At.Y-ft.Baseline));
      if(t.Vertical)dc.Pop();
     }
    }
   }
   int w=(int)Math.Ceiling(f.WidthMm/25.4*dpi),h=(int)Math.Ceiling(f.HeightMm/25.4*dpi);
   var bitmap=new RenderTargetBitmap(w,h,dpi,dpi,PixelFormats.Pbgra32);bitmap.Render(visual);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
   using(var stream=new MemoryStream()){encoder.Save(stream);return stream.ToArray();}
  }
  // Word image: PNG fallback with the SVG attached (Word 2016+ svgBlip).
  public static DocxImage Image(Figure f){Initialise();return DocxImage.WithSvg(DocxImage.FromBytes(Png(f)),Encoding.UTF8.GetBytes(Svg(f)));}
 }
}
