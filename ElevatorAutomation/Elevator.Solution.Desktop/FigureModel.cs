using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
namespace Elevator.Solution.Desktop
{
 // Engineering figure model in paper millimetres (origin top-left, y downwards), rendered to SVG and PNG.
 // Drawing standard: white background; geometry black/dark grey; loads in one accent colour (red);
 // ISO line weights; centre lines dash-dot, hidden lines dashed; ANSI31 hatch; 2.5 mm text, 3.5 mm titles.
 public enum FigureLineType{Continuous,Centre,Hidden}
 public sealed class FigureLayer
 {
  public string Name;public double WeightMm;public FigureLineType LineType;public string Colour;
  public static readonly FigureLayer Outline=new FigureLayer{Name="outline",WeightMm=0.5,Colour="000000"};
  public static readonly FigureLayer Secondary=new FigureLayer{Name="secondary",WeightMm=0.35,Colour="2B2B2B"};
  public static readonly FigureLayer Thin=new FigureLayer{Name="thin",WeightMm=0.25,Colour="2B2B2B"};
  public static readonly FigureLayer Dimension=new FigureLayer{Name="dimension",WeightMm=0.18,Colour="000000"};
  public static readonly FigureLayer Hatch=new FigureLayer{Name="hatch",WeightMm=0.18,Colour="4A4A4A"};
  public static readonly FigureLayer Leader=new FigureLayer{Name="leader",WeightMm=0.25,Colour="000000"};
  public static readonly FigureLayer Centre=new FigureLayer{Name="centre",WeightMm=0.25,LineType=FigureLineType.Centre,Colour="2B2B2B"};
  public static readonly FigureLayer Hidden=new FigureLayer{Name="hidden",WeightMm=0.25,LineType=FigureLineType.Hidden,Colour="2B2B2B"};
  public static readonly FigureLayer Load=new FigureLayer{Name="load",WeightMm=0.35,Colour="D0021B"};
  public static readonly FigureLayer Text=new FigureLayer{Name="text",WeightMm=0,Colour="000000"};
  public static readonly FigureLayer LoadText=new FigureLayer{Name="load-text",WeightMm=0,Colour="D0021B"};
 }
 public struct FigurePoint
 {
  public double X,Y;public FigurePoint(double x,double y){X=x;Y=y;}
  public static FigurePoint operator+(FigurePoint a,FigurePoint b){return new FigurePoint(a.X+b.X,a.Y+b.Y);}
  public static FigurePoint operator-(FigurePoint a,FigurePoint b){return new FigurePoint(a.X-b.X,a.Y-b.Y);}
  public static FigurePoint operator*(FigurePoint a,double s){return new FigurePoint(a.X*s,a.Y*s);}
  public double Length{get{return Math.Sqrt(X*X+Y*Y);}}
  public FigurePoint Unit{get{double l=Length;return l>0?new FigurePoint(X/l,Y/l):new FigurePoint(0,0);}}
  public FigurePoint Normal{get{return new FigurePoint(-Y,X);}}
 }
 public abstract class FigureEntity{public FigureLayer Layer;public string Tag;public string Value;}
 public sealed class FigurePolyline:FigureEntity{public List<FigurePoint> Points=new List<FigurePoint>();public bool Closed,Filled;}
 public sealed class FigureCircle:FigureEntity{public FigurePoint Centre;public double Radius;public bool Filled;}
 public sealed class FigureText:FigureEntity
 {
  public FigurePoint At;public string Content;public double HeightMm=2.5;public string Anchor="start";public bool Vertical,Title;
  public double WidthMm; // measured with the figure font
  // Axis-aligned box in paper mm; At is on the baseline.
  public void Box(out double x0,out double y0,out double x1,out double y1)
  {
   double ascent=HeightMm*1.05,descent=HeightMm*0.3;
   if(!Vertical){double s=Anchor=="middle"?At.X-WidthMm/2:Anchor=="end"?At.X-WidthMm:At.X;x0=s;x1=s+WidthMm;y0=At.Y-ascent;y1=At.Y+descent;}
   else{double s=Anchor=="middle"?At.Y+WidthMm/2:Anchor=="end"?At.Y+WidthMm:At.Y;y1=s;y0=s-WidthMm;x0=At.X-ascent;x1=At.X+descent;}
  }
 }
 public sealed class Figure
 {
  public string Title,Id;public bool Schematic;public double WidthMm=165,HeightMm;
  public FigureView View; // main model-to-paper transform (recorded in the SVG for verification)
  public readonly List<FigureEntity> Entities=new List<FigureEntity>();
  public IEnumerable<FigureText> Texts{get{return Entities.OfType<FigureText>();}}
  // Font chain per the drawing standard; measured by the WPF renderer.
  public static string FontFamily{get{return FigureFonts.Family;}}
  public static Func<string,double,double> Measure=(text,heightMm)=>text.Length*heightMm*0.62;
  public static string Num(double v,string format){return v.ToString(format,CultureInfo.InvariantCulture);}
  public static string Kn(double newtons){return Num(newtons/1000,"0.###")+" kN";}
  public static string Mm(double mm){return Num(mm,"0.#");}
  public FigurePolyline Line(FigurePoint a,FigurePoint b,FigureLayer layer,string tag=null){var l=new FigurePolyline{Layer=layer,Tag=tag};l.Points.Add(a);l.Points.Add(b);Entities.Add(l);return l;}
  public FigurePolyline Poly(IEnumerable<FigurePoint> points,bool closed,FigureLayer layer,string tag=null,bool filled=false){var l=new FigurePolyline{Layer=layer,Closed=closed,Filled=filled,Tag=tag};l.Points.AddRange(points);Entities.Add(l);return l;}
  public FigurePolyline Rect(double x0,double y0,double x1,double y1,FigureLayer layer,string tag=null){return Poly(new[]{new FigurePoint(x0,y0),new FigurePoint(x1,y0),new FigurePoint(x1,y1),new FigurePoint(x0,y1)},true,layer,tag);}
  public FigureCircle Circle(FigurePoint c,double r,FigureLayer layer,bool filled=false,string tag=null){var e=new FigureCircle{Centre=c,Radius=r,Layer=layer,Filled=filled,Tag=tag};Entities.Add(e);return e;}
  // ANSI31: 45° lines, 1.5 mm pitch, clipped to the (simple) polygon.
  public void Hatch(IList<FigurePoint> polygon,double pitchMm=1.5)
  {
   double c=Math.Cos(Math.PI/4),s=Math.Sin(Math.PI/4);
   Func<FigurePoint,FigurePoint> rot=p=>new FigurePoint(p.X*c+p.Y*s,-p.X*s+p.Y*c);Func<FigurePoint,FigurePoint> back=p=>new FigurePoint(p.X*c-p.Y*s,p.X*s+p.Y*c);
   var r=polygon.Select(rot).ToList();double min=r.Min(p=>p.Y),max=r.Max(p=>p.Y);
   for(double y=Math.Ceiling(min/pitchMm)*pitchMm;y<max;y+=pitchMm)
   {
    var xs=new List<double>();
    for(int i=0;i<r.Count;i++){var a=r[i];var b=r[(i+1)%r.Count];if((a.Y<=y&&b.Y>y)||(b.Y<=y&&a.Y>y))xs.Add(a.X+(y-a.Y)/(b.Y-a.Y)*(b.X-a.X));}
    xs.Sort();for(int i=0;i+1<xs.Count;i+=2)Line(back(new FigurePoint(xs[i],y)),back(new FigurePoint(xs[i+1],y)),FigureLayer.Hatch);
   }
  }
  // Filled closed arrowhead with its tip at 'tip', pointing along 'direction'.
  public void Arrowhead(FigurePoint tip,FigurePoint direction,FigureLayer layer,double lengthMm=2.5,double halfWidthMm=0.45)
  {var d=direction.Unit;var n=d.Normal;var baseCentre=tip-d*lengthMm;Poly(new[]{tip,baseCentre+n*halfWidthMm,baseCentre-n*halfWidthMm},true,layer,null,true);}
  bool Overlaps(double x0,double y0,double x1,double y1,FigureText self)
  {
   foreach(var t in Texts){if(t==self)continue;double a0,b0,a1,b1;t.Box(out a0,out b0,out a1,out b1);if(x0<a1&&a0<x1&&y0<b1&&b0<y1)return true;}
   return false;
  }
  // Adds text at the first candidate position whose box does not overlap existing text.
  public FigureText Text(string content,IList<FigurePoint> candidates,string anchor="start",FigureLayer layer=null,double heightMm=2.5,bool vertical=false,string tag=null,string value=null,bool title=false)
  {
   // The Word SVG renderer adds space around U+002D between letters and ISOCPEUR has no U+2011, so word hyphens become spaces.
   content=System.Text.RegularExpressions.Regex.Replace(content??"",@"(?<=\p{L})-(?=\p{L})"," ");
   var t=new FigureText{Content=content,Anchor=anchor,Layer=layer??FigureLayer.Text,HeightMm=heightMm,Vertical=vertical,Tag=tag,Value=value,Title=title};t.WidthMm=Measure(content,heightMm);
   foreach(var c in candidates){t.At=c;double x0,y0,x1,y1;t.Box(out x0,out y0,out x1,out y1);if(!Overlaps(x0,y0,x1,y1,t)){Entities.Add(t);return t;}}
   t.At=candidates[candidates.Count-1];Entities.Add(t);return t; // reported by the overlap test if no candidate was free
  }
  public FigureText Text(string content,FigurePoint at,string anchor="start",FigureLayer layer=null,double heightMm=2.5,bool vertical=false,string tag=null,string value=null,bool title=false)
  {
   var candidates=new List<FigurePoint>{at};
   for(int i=1;i<=6;i++){double step=i*(heightMm+0.8);candidates.Add(vertical?new FigurePoint(at.X-step,at.Y):new FigurePoint(at.X,at.Y-step));candidates.Add(vertical?new FigurePoint(at.X+step,at.Y):new FigurePoint(at.X,at.Y+step));}
   return Text(content,candidates,anchor,layer,heightMm,vertical,tag,value,title);
  }
  // Horizontal dimension between p1 and p2 with its line at yDim. Extension lines leave a 0.8 mm gap at the
  // geometry and overshoot the dimension line by 1.5 mm; filled closed arrows; 2.5 mm text above the line.
  public void DimH(FigurePoint p1,FigurePoint p2,double yDim,string text,string tag=null,string value=null)
  {
   foreach(var p in new[]{p1,p2}){double s=yDim>p.Y?1:-1;Line(new FigurePoint(p.X,p.Y+0.8*s),new FigurePoint(p.X,yDim+1.5*s),FigureLayer.Dimension,tag==null?null:tag+"-ext");}
   var a=new FigurePoint(p1.X,yDim);var b=new FigurePoint(p2.X,yDim);Line(a,b,FigureLayer.Dimension,tag).Value=value;Arrowhead(a,a-b,FigureLayer.Dimension);Arrowhead(b,b-a,FigureLayer.Dimension);
   // Text that does not fit between the arrows goes beyond the right-hand end.
   double room=Math.Abs(b.X-a.X)-6;
   if(Measure(text,2.5)<=room)Text(text,new FigurePoint((a.X+b.X)/2,yDim-0.8),"middle",FigureLayer.Text,2.5,false,tag==null?null:tag+"-text",value);
   else Text(text,new FigurePoint(Math.Max(a.X,b.X)+1.5,yDim-0.8),"start",FigureLayer.Text,2.5,false,tag==null?null:tag+"-text",value);
  }
  // Vertical dimension between p1 and p2 with its line at xDim; text reads upwards on the left of the line.
  public void DimV(FigurePoint p1,FigurePoint p2,double xDim,string text,string tag=null,string value=null)
  {
   foreach(var p in new[]{p1,p2}){double s=xDim>p.X?1:-1;Line(new FigurePoint(p.X+0.8*s,p.Y),new FigurePoint(xDim+1.5*s,p.Y),FigureLayer.Dimension,tag==null?null:tag+"-ext");}
   var a=new FigurePoint(xDim,p1.Y);var b=new FigurePoint(xDim,p2.Y);Line(a,b,FigureLayer.Dimension,tag).Value=value;Arrowhead(a,a-b,FigureLayer.Dimension);Arrowhead(b,b-a,FigureLayer.Dimension);
   double room=Math.Abs(b.Y-a.Y)-6;
   if(Measure(text,2.5)<=room)Text(text,new FigurePoint(xDim-0.8,(a.Y+b.Y)/2),"middle",FigureLayer.Text,2.5,true,tag==null?null:tag+"-text",value);
   else Text(text,new FigurePoint(xDim-0.8,Math.Min(a.Y,b.Y)-1.5),"start",FigureLayer.Text,2.5,true,tag==null?null:tag+"-text",value);
  }
  // Load arrow from tail to tip in the accent colour, label near the tail.
  public void Load(FigurePoint tail,FigurePoint tip,string label,IList<FigurePoint> labelCandidates,string anchor,string tag,string value)
  {
   Line(tail,tip-(tip-tail).Unit*2.0,FigureLayer.Load,tag).Value=value;Arrowhead(tip,tip-tail,FigureLayer.Load,3.0,0.6);
   Text(label,labelCandidates,anchor,FigureLayer.LoadText,2.5,false,tag==null?null:tag+"-text",value);
  }
 }
 public static class FigureFonts
 {
  static string family;
  public static Func<string> Resolve=()=>"Arial";
  public static string Family{get{return family??(family=Resolve());}}
 }
 // Model-to-paper transform: model mm, y up → paper mm, y down; uniform scale.
 public sealed class FigureView
 {
  public double Scale,MinX,MaxY,OffsetX,OffsetY;
  public FigureView(double minX,double minY,double maxX,double maxY,double left,double top,double widthMm,double heightMm,bool alignTop=false)
  {
   Scale=Math.Min(widthMm/(maxX-minX),heightMm/(maxY-minY));MinX=minX;MaxY=maxY;
   OffsetX=left+(widthMm-(maxX-minX)*Scale)/2;OffsetY=alignTop?top:top+(heightMm-(maxY-minY)*Scale)/2;
  }
  public FigurePoint P(double x,double y){return new FigurePoint(OffsetX+(x-MinX)*Scale,OffsetY+(MaxY-y)*Scale);}
 }
 // Rail cross-section geometry. Today derived proportionally from the designation (only b1, h1, k are known);
 // a future table (designation → full dimensions) can implement this interface without changing figure code.
 public interface IRailSection
 {
  string Designation{get;}
  double BaseWidthMm{get;}   // b1
  double HeightMm{get;}      // h1
  double BladeWidthMm{get;}  // k
  bool Proportional{get;}    // true: features other than b1, h1, k are drawn proportionally and not dimensioned
  IList<FigurePoint> Outline(); // closed outline in section mm: x across the foot (centre 0), y from foot underside upwards
 }
 public sealed class ProportionalRailSection:IRailSection
 {
  public string Designation{get;private set;}public double BaseWidthMm{get;private set;}public double HeightMm{get;private set;}public double BladeWidthMm{get;private set;}
  public bool Proportional{get{return true;}}
  // "T70/65/9" → b1 = 70, h1 = 65, k = 9. Returns null if the designation does not have that form.
  public static ProportionalRailSection Parse(string designation)
  {
   if(String.IsNullOrWhiteSpace(designation))return null;
   var m=System.Text.RegularExpressions.Regex.Match(designation.Trim(),@"^T\s*(\d+(?:\.\d+)?)\s*/\s*(\d+(?:\.\d+)?)\s*/\s*(\d+(?:\.\d+)?)$");
   if(!m.Success)return null;
   double b=Double.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture),h=Double.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture),k=Double.Parse(m.Groups[3].Value,CultureInfo.InvariantCulture);
   if(b<=0||h<=0||k<=0||k>=b||k>=h)return null;
   return new ProportionalRailSection{Designation=designation.Trim(),BaseWidthMm=b,HeightMm=h,BladeWidthMm=k};
  }
  // Proportional shape (undimensioned features): foot thickness 0.12 h1 tapering to 0.09 h1 at the edges, root width 1.6 k.
  public IList<FigurePoint> Outline()
  {
   double b=BaseWidthMm/2,h=HeightMm,k=BladeWidthMm/2,foot=0.12*h,edge=0.09*h,root=0.8*BladeWidthMm;
   return new List<FigurePoint>{new FigurePoint(-b,0),new FigurePoint(b,0),new FigurePoint(b,edge),new FigurePoint(root,foot),new FigurePoint(k,foot+0.6*root),new FigurePoint(k,h),new FigurePoint(-k,h),new FigurePoint(-k,foot+0.6*root),new FigurePoint(-root,foot),new FigurePoint(-b,edge)};
  }
 }
}
