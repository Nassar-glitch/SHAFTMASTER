using System;using System.IO;using System.Linq;using System.Xml.Linq;using System.Windows;using System.Windows.Media;using System.Windows.Media.Imaging;
// Rasterizes the simplified vector symbol; never modifies the approved full logo.
public static class BrandIconBuilder
{
 [STAThread]public static void Main(string[] args)
 {
  var svg=XDocument.Load(Path.Combine(args[0],"ShaftMasterMark.svg"));var drawing=new DrawingGroup();
  foreach(var path in svg.Descendants().Where(e=>e.Name.LocalName=="path"))
  {
   var group=path.Parent;string fill=(string)path.Attribute("fill")??(string)group.Attribute("fill");string stroke=(string)path.Attribute("stroke")??(string)group.Attribute("stroke");string width=(string)path.Attribute("stroke-width")??(string)group.Attribute("stroke-width")??"0";
   Brush brush=fill==null||fill=="none"?null:(Brush)new BrushConverter().ConvertFromString(fill);Pen pen=stroke==null?null:new Pen((Brush)new BrushConverter().ConvertFromString(stroke),Double.Parse(width,System.Globalization.CultureInfo.InvariantCulture));
   drawing.Children.Add(new GeometryDrawing(brush,pen,Geometry.Parse((string)path.Attribute("d"))));
  }
  var sizes=new[]{16,24,32,48,64,128,256};var images=new byte[sizes.Length][];
  for(int i=0;i<sizes.Length;i++)
  {
   int size=sizes[i];double scale=(size-2.0)/180;var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.PushTransform(new TranslateTransform((size-128*scale)/2,1));dc.PushTransform(new ScaleTransform(scale,scale));dc.DrawDrawing(drawing);dc.Pop();dc.Pop();}
   var bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=new MemoryStream()){png.Save(stream);images[i]=stream.ToArray();}
  }
  File.WriteAllBytes(Path.Combine(args[0],"ShaftMasterMark.png"),images.Last());
  using(var file=new BinaryWriter(File.Create(Path.Combine(args[0],"ShaftMaster.ico"))))
  {
   file.Write((ushort)0);file.Write((ushort)1);file.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
   for(int i=0;i<sizes.Length;i++){file.Write((byte)(sizes[i]==256?0:sizes[i]));file.Write((byte)(sizes[i]==256?0:sizes[i]));file.Write((ushort)0);file.Write((ushort)1);file.Write((ushort)32);file.Write(images[i].Length);file.Write(offset);offset+=images[i].Length;}
   foreach(var bytes in images)file.Write(bytes);
  }
 }
}
