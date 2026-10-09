using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Elevator.Core;
using Elevator.Core.Calculations;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // Plan preview of the chassis geometry: shaft, members, contact perimeters and load points (local mm).
 static class CalculationPreview
 {
  public static FrameworkElement Create(ChassisCalculationGeometry g,bool basic,Brush ink,Brush accent)
  {
   var canvas=new Canvas{Width=760,Height=345,Background=new SolidColorBrush(Color.FromRgb(246,249,252))};
   double minX=Math.Min(0,g.Bearings.Min(b=>b.Centre.X)),maxX=Math.Max(g.ShaftWidthMm,g.Bearings.Max(b=>b.Centre.X));double minY=Math.Min(0,g.Bearings.Min(b=>b.Centre.Y)),maxY=Math.Max(g.ShaftDepthMm,g.Bearings.Max(b=>b.Centre.Y));
   double scale=Math.Min(500/(maxX-minX+150),255/(maxY-minY+150));Func<double,double> x=v=>110+(v-minX)*scale;Func<double,double> y=v=>295-(v-minY)*scale;
   Action<double,double,double,double,Brush,double> line=(a,b,c,d,brush,width)=>canvas.Children.Add(new Line{X1=x(a),Y1=y(b),X2=x(c),Y2=y(d),Stroke=brush,StrokeThickness=width});
   Action<double,double,string,Brush> label=(a,b,t,brush)=>{var text=new TextBlock{Text=t,Foreground=brush,FontSize=12};Canvas.SetLeft(text,a);Canvas.SetTop(text,b);canvas.Children.Add(text);};
   line(0,0,g.ShaftWidthMm,0,Brushes.SlateGray,1);line(0,0,0,g.ShaftDepthMm,Brushes.SlateGray,1);line(g.ShaftWidthMm,0,g.ShaftWidthMm,g.ShaftDepthMm,Brushes.SlateGray,1);line(0,g.ShaftDepthMm,g.ShaftWidthMm,g.ShaftDepthMm,Brushes.SlateGray,1);
   var p4=g.Bearings.Single(b=>b.Id=="P4").Centre;var p2=g.Bearings.Single(b=>b.Id=="P2").Centre;var p3=g.Bearings.Single(b=>b.Id=="P3").Centre;var p1=g.Bearings.Single(b=>b.Id=="P1").Centre;
   line(p4.X,p4.Y,p2.X,p2.Y,ink,6);line(p3.X,p3.Y,p1.X,p1.Y,ink,6);
   foreach(var m in g.LongitudinalMembers){line(m.Front.X,m.Front.Y,m.Rear.X,m.Rear.Y,accent,3);label(x(m.Rear.X)-4,y(m.Rear.Y)-18,m.Id.Replace("Longitudinal.","").Substring(0,1),ink);}
   foreach(var b in g.Bearings){var polygon=new Polygon{Stroke=Brushes.DarkOrange,StrokeThickness=2,Fill=Brushes.White,Points=new PointCollection(b.ContactBoundary.Select(v=>new Point(x(v.X),y(v.Y))))};canvas.Children.Add(polygon);label(x(b.Centre.X)+(b.Id=="P1"||b.Id=="P2"?14:-75),y(b.Centre.Y)-7,b.Id+" "+DisplayText.BearingLabel(b.Id).Replace(" / ","/"),ink);}
   Action<LocalPoint,string,Brush> node=(p,text,color)=>{var dot=new Ellipse{Width=9,Height=9,Fill=color};Canvas.SetLeft(dot,x(p.X)-4);Canvas.SetTop(dot,y(p.Y)-4);canvas.Children.Add(dot);label(x(p.X)+8,y(p.Y)-7,text,color);};
   node(g.TractionCentre,basic?"Machine load point":"Traction centre",ink);if(!basic)node(g.DeflectorCentre,"Deflector centre",ink);
   if(g.NativeMachineMountingContours!=null&&g.NativeMachineMountingContours.Length>0){var points=g.NativeMachineMountingContours;double a=points.Min(p=>p.X),b=points.Max(p=>p.X),lo=points.Min(p=>p.Y),hi=points.Max(p=>p.Y);var region=new Rectangle{Width=(b-a)*scale,Height=(hi-lo)*scale,Stroke=Brushes.SlateGray,StrokeThickness=1,StrokeDashArray=new DoubleCollection{4,3}};Canvas.SetLeft(region,x(a));Canvas.SetTop(region,y(hi));canvas.Children.Add(region);}
   label(505,30,"PLAN · local mm\n+X → right\n+Y ↑ rear\n+Z above slab\n\nBlue: upper longitudinal\nL = Left · M = Middle · R = Right\nNavy: lower transverse\nOrange: contact perimeters\nDashed: machine contour envelope\n(not confirmed feet / CG)",ink);
   label(110,318,"ENTRANCE · Y = 0    |    SW "+DisplayText.N(g.ShaftWidthMm)+"    SD "+DisplayText.N(g.ShaftDepthMm),ink);
   return new Viewbox{Child=canvas,Stretch=Stretch.Uniform,MaxHeight=345,HorizontalAlignment=HorizontalAlignment.Left};
  }
 }
}
