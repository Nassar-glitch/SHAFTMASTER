using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Elevator.Reporting.Figures;
namespace Elevator.Solution.Desktop.Common
{
 // WPF text measurement and 300 dpi PNG rendering for report figures (moved unchanged from FigureRender). Must run on an
 // STA thread. Registered with FigureRender.Rasterizer by the application.
 public sealed class WpfFigureRasterizer:IFigureRasterizer
 {
  static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
  const double CapRatio=FigureRender.CapRatio;
  public void Initialise()
  {
   FigureFonts.Resolve=()=>{var names=Fonts.SystemFontFamilies.Select(f=>f.Source).ToList();foreach(var n in new[]{"ISOCPEUR","Arial Narrow","Arial"})if(names.Any(x=>String.Equals(x,n,StringComparison.OrdinalIgnoreCase)))return n;return "Arial";};
   Figure.Measure=(text,heightMm)=>Formatted(text,heightMm,Brushes.Black).WidthIncludingTrailingWhitespace;
  }
  static FormattedText Formatted(string text,double heightMm,Brush brush)
  {
#pragma warning disable 618
   return new FormattedText(text??"",Inv,FlowDirection.LeftToRight,new Typeface(new FontFamily(Figure.FontFamily),FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),heightMm/CapRatio,brush);
#pragma warning restore 618
  }
  static string Dash(FigureLayer l){return FigureRender.Dash(l);}
  static Brush Brush(string hex){var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString("#"+hex));b.Freeze();return b;}
  static Pen PenFor(FigureLayer l)
  {
   var pen=new Pen(Brush(l.Colour),l.WeightMm){LineJoin=PenLineJoin.Round,StartLineCap=PenLineCap.Flat,EndLineCap=PenLineCap.Flat};
   var dash=Dash(l);if(dash!=null)pen.DashStyle=new DashStyle(dash.Split(' ').Select(x=>Double.Parse(x,Inv)/l.WeightMm),0); // WPF dashes are in pen widths
   pen.Freeze();return pen;
  }
  public byte[] Png(Figure f,double dpi)
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
 }
}
