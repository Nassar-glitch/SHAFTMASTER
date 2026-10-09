using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elevator.Core.Calculations;
using Elevator.Solution.Desktop.Common;
namespace Elevator.Solution.Desktop.Pages
{
 // Ropes and traction: checks, traction cases, omissions and equations of the live result.
 sealed class RopeTractionPanel:ViewPart
 {
  static readonly CultureInfo PresetInv=CultureInfo.InvariantCulture;
  public RopeTractionPanel(MainWindow shell):base(shell){}
  public StackPanel Results;
  public void ResetView(){Results=null;}
  public void Section()
  {
   Form.CalculationGroup("Ropes and traction",()=>{
    Results=new StackPanel{Margin=new Thickness(0,12,0,0)};Content.Children.Add(Results);Render();
   },true);
  }
  public void Render()
  {
   if(Results==null)return;Results.Children.Clear();var parent=Content;Content=Results;
   try{
    var r=Live.RopeTraction;
    if(r==null||!RopeTractionCalculationEngine.IsCurrent(r,Session.Project,Context,Presets.Library))return;
    Func<bool?,Brush> colour=ok=>ok==false?(Brush)Brushes.DarkRed:Ink;
    foreach(var c in r.Checks)Content.Children.Add(new TextBlock{Text=c.Title+" · "+(c.Ok==null?c.Required:c.Actual+" (required "+c.Required+") · "+(c.Ok.Value?"OK":"NOT OK")),Foreground=colour(c.Ok),FontWeight=c.Ok==false?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)});
    foreach(var t in r.Traction)Content.Children.Add(new TextBlock{Text="Traction, "+(t.Title.Substring(0,1).ToLowerInvariant()+t.Title.Substring(1))+" · T1/T2 "+t.Ratio.ToString("0.###",PresetInv)+" "+t.Condition+" e^(fα) "+t.EFAlpha.ToString("0.###",PresetInv)+" · "+(t.Ok?"OK":"NOT OK"),Foreground=colour(t.Ok),FontWeight=t.Ok?FontWeights.Normal:FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)});
    foreach(var o in r.Omissions)Content.Children.Add(new TextBlock{Text=o.Item+" · "+DisplayText.MessageText(o.Reason),Foreground=Brushes.SlateGray,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,4)});
    Form.CalculationGroup("Equations",()=>{
     if(r.TmaxN.HasValue)Note("Tmax = ((P + Q)/n + H·w)·gn = "+r.TmaxN.Value.ToString("0.##",PresetInv)+" N · Sf = MBL/Tmax = "+r.SfActual.Value.ToString("0.##",PresetInv)+"\nNequiv = "+r.Nequiv.Value.ToString("0.####",PresetInv)+" (Nequiv(t) "+r.NequivT.Value.ToString("0.##",PresetInv)+(r.Geometry.DeflectorPresent?" + Kp "+r.Kp.Value.ToString("0.####",PresetInv)+" × 1":"")+") · Sf(EN 81-50) = "+r.Sf8150.Value.ToString("0.##",PresetInv)+" · Sf required = "+r.SfRequired.Value.ToString("0.##",PresetInv));
     foreach(var c in r.Checks)Note(c.Title+": "+c.Equation+" · "+c.Substitution);
     foreach(var t in r.Traction){Note(t.Title+": "+t.MuEquation+" · "+t.FEquation+" = "+t.F.ToString("0.####",PresetInv)+" · α = "+t.AlphaDeg.ToString("0.##",PresetInv)+"° · e^(fα) = "+t.EFAlpha.ToString("0.###",PresetInv));foreach(var q in t.Positions)Note(q.Name+"\n"+q.T1Equation+"\n"+q.T2Equation);}
    },false);
   }finally{Content=parent;}
  }
 }
}
