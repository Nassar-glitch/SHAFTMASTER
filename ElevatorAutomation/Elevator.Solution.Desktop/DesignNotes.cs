using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using Elevator.Core;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
namespace Elevator.Solution.Desktop
{
 // Design Notes (DESIGN-FLOW principle 7): short advisory notes only, written as an engineer's advice, one or two lines each,
 // with the clause in brackets. Warnings carry a recommendation computed from the project; recommendations list what is
 // needed to complete a check. Nothing is written about checks that pass. Same CAD page frame and title block as the sheet.
 public sealed class DesignNotes
 {
  static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
  public const string NoNotes="No design notes for this project.";
  public ElevatorProject Project {get;private set;}
  public CalculationSheetInputs Inputs {get;private set;}
  public RopeTractionResult RopeTraction {get;private set;}
  public List<string> Warnings {get;private set;}
  public List<string> Recommendations {get;private set;}
  // Quotations (DESIGN-FLOW 11a): the deflector D/d below 40, quoted once; not a warning.
  public List<string> Notes {get;private set;}
  public bool Empty {get{return Warnings.Count==0&&Recommendations.Count==0&&Notes.Count==0;}}
  static string L(double v){return v.ToString("0.##",Inv);}
  static string L1(double v){return v.ToString("0.#",Inv);}
  static string Lower1(string t){return String.IsNullOrEmpty(t)?t:t.Substring(0,1).ToLowerInvariant()+t.Substring(1);}
  static readonly Dictionary<string,string> Clause=new Dictionary<string,string>{
   {"Nominal rope diameter","EN 81-20 5.5.1.2"},{"Number of ropes","EN 81-20 5.5.1.3"},{"Traction sheave D/d","EN 81-20 5.5.2.1"},{"Deflector D/d","EN 81-20 5.5.2.1"},
   {"Rope safety factor","EN 81-20 5.5.2.2; EN 81-50 5.12"},{"Rope termination strength","EN 81-20 5.5.2.3"},{"Traction","EN 81-50 5.11"},
   {"Emergency braking","EN 81-50 5.11.2.2.2"},{"Car stalled (counterweight on buffers)","EN 81-50 5.11.2.2.3"},{"Safety factor and traction","EN 81-50 5.11; 5.12"}};
  static string ClauseOf(string item){string c;return Clause.TryGetValue(item,out c)?c:"EN 81-20 5.5; EN 81-50 5.11, 5.12";}
  public static DesignNotes Build(ElevatorProject project,DesignContext context,CalculationSheetInputs inputs)
  {
   if(project==null)throw new ArgumentException("No project is open.");
   var n=new DesignNotes{Project=project,Inputs=inputs??new CalculationSheetInputs(),Warnings=new List<string>(),Recommendations=new List<string>(),Notes=new List<string>()};
   var presets=n.Inputs.Presets??CalculationSheet.LoadPresets(null);
   var engine=new RopeTractionCalculationEngine();
   var r=n.RopeTraction=engine.Calculate(project,context,presets);
   var x=r.Inputs;
   // ---------- warnings: NOT OK results, each with a computed recommendation ----------
   foreach(var c in r.Checks.Where(c=>c.Ok==false))
   {
    if(c.Id=="rope-diameter")n.Warnings.Add("Rope diameter "+L(c.ActualValue.Value)+" mm is below the 8 mm minimum (EN 81-20 5.5.1.2). Use ropes of at least 8 mm.");
    else if(c.Id=="rope-count")n.Warnings.Add("Only "+c.Actual+" rope(s) (EN 81-20 5.5.1.3). Use at least 2 independent ropes.");
    else if(c.Id=="dd-sheave")n.Warnings.Add("Traction sheave D/d "+c.Actual+" is below 40 (EN 81-20 5.5.2.1). Use a traction sheave pitch diameter of at least "+L(40*x.RopeDiameterMm.Value)+" mm (40 × "+L(x.RopeDiameterMm.Value)+" mm).");
    else if(c.Id=="safety-factor")n.Warnings.Add("Rope safety factor "+c.Actual+" is below the required "+L(c.RequiredValue.Value)+" (EN 81-20 5.5.2.2; EN 81-50 5.12). "+SafetyFactorRemedy(project,context,presets,engine,x));
   }
   foreach(var t in r.Traction.Where(t=>!t.Ok))
   {
    var gov=t.Positions.First(pos=>Math.Abs(pos.Ratio-t.Ratio)<1e-12);double alpha=t.AlphaDeg*Math.PI/180,lnRatio=Math.Log(t.Ratio);
    double margin=Math.Abs(t.Ratio/t.EFAlpha-1)*100,fNeeded=lnRatio/alpha,alphaNeeded=lnRatio/t.F*180/Math.PI;
    string where="the "+Lower1(gov.Name);
    if(t.Condition=="≤")
     n.Warnings.Add(t.Title+": T1/T2 "+t.Ratio.ToString("0.###",Inv)+" exceeds e^(fα) "+t.EFAlpha.ToString("0.###",Inv)+" by "+L1(margin)+" % with "+where+" ("+t.Clause+"). "+(alphaNeeded<=180?"It needs a wrap angle of at least "+L1(alphaNeeded)+"° with this groove, or a groove giving f ≥ "+fNeeded.ToString("0.####",Inv)+".":"It needs a groove giving f ≥ "+fNeeded.ToString("0.####",Inv)+"; the wrap angle alone cannot reach it."));
    else
     n.Warnings.Add(t.Title+": T1/T2 "+t.Ratio.ToString("0.###",Inv)+" is below e^(fα) "+t.EFAlpha.ToString("0.###",Inv)+" by "+L1(margin)+" % ("+t.Clause+"), so the ropes would not slip. Use a groove giving f ≤ "+fNeeded.ToString("0.####",Inv)+", or limit traction by an electric safety device (EN 81-20 5.5.3 c) 2)).");
   }
   // Checks that could not be done because the layout is not valid: one note with the cause and the valid range.
   if(r.Omissions.Any(o=>o.Kind=="INVALID_GEOMETRY"))
   {
    double tz=r.Geometry.TzMm??0;
    n.Warnings.Add("Traction and the EN 81-50 safety factor were not calculated: TZ "+L(tz)+" mm is outside the deflector selection range 520–1100 mm. Keep TZ at 1100 mm or less.");
   }
   foreach(var o in r.Omissions.Where(o=>o.Kind=="INVALID_INPUT"))n.Warnings.Add(o.Item+" not calculated: "+o.Reason.TrimEnd('.')+" ("+ClauseOf(o.Item)+").");
   // EN 81-20 5.4.2.1, Table 6: car available area against the rated load (shared internal car dimensions).
   double? q=project.Elevator==null?null:project.Elevator.CapacityKg;
   if(r.Geometry.CarWidthMm.HasValue&&r.Geometry.CarDepthMm.HasValue&&q.HasValue&&q.Value>0)
   {
    double area=Math.Round(r.Geometry.CarWidthMm.Value*r.Geometry.CarDepthMm.Value/1e6,2);var max=CarAreaRules.MaximumAvailableAreaM2(q.Value);
    if(max.HasValue&&area>Math.Round(max.Value,2)+1e-9)
     n.Warnings.Add("Car available area "+area.ToString("0.00",Inv)+" m² exceeds the maximum "+max.Value.ToString("0.00",Inv)+" m² for "+L(q.Value)+" kg (EN 81-20 Table 6). Use a rated load of at least "+L(CarAreaRules.MinimumRatedLoadKg(area).Value)+" kg, or reduce the car to ≤ "+max.Value.ToString("0.00",Inv)+" m².");
   }
   else if(!q.HasValue||q.Value<=0)n.Recommendations.Add("Enter the rated load to check the car area (EN 81-20 Table 6).");
   // Rope / groove / balance / sheave selection (project presets): nothing passes on 1:1. One recommendation: the limiting check,
   // 2:1 roping, and the minimum rope MBL that would satisfy the safety factor at 7 ropes (same engine, 7 ropes of this diameter).
   var sizing=ProjectPresets.Outcome(project,presets,context);
   if(sizing!=null&&sizing.Status=="NoPass"&&presets.RopeLadder!=null&&sizing.RopeCount.HasValue&&sizing.RopeDiameterMm.HasValue)
   {
    var p7=ProjectJson.Clone(project);p7.Suspension.RopeCount=presets.MaximumRopeCount.Value;p7.Suspension.RopeDiameterMm=sizing.RopeDiameterMm;
    var r7=engine.Calculate(p7,context,presets);
    string mbl=r7.TmaxN.HasValue&&r7.SfRequired.HasValue?(Math.Ceiling(r7.SfRequired.Value*r7.TmaxN.Value/100.0)/10.0).ToString("0.0",Inv):null;
    n.Recommendations.Add("No 1:1 arrangement passes every rope and traction check; the limiting check is "+Lower1(sizing.LimitingCheck??"traction")+". 2:1 roping is recommended for this rated load"+(mbl==null?"":"; on 1:1, "+presets.MaximumRopeCount.Value+" ropes of "+L(sizing.RopeDiameterMm.Value)+" mm would need MBL ≥ "+mbl+" kN each")+" (EN 81-20 5.5; EN 81-50 5.11, 5.12).");
   }
   // Deflector smaller than 40·d (DESIGN-FLOW 11a): one quotation; Sf already uses the actual deflector (Kp).
   if(r.DpOverDr.HasValue&&r.DpOverDr.Value<40-1e-9&&r.Geometry.DeflectorPitchMm.HasValue&&x.RopeDiameterMm.HasValue)
    n.Notes.Add("Deflector pitch diameter "+L(r.Geometry.DeflectorPitchMm.Value)+" mm gives D/d = "+L(r.DpOverDr.Value)+" for "+L(x.RopeDiameterMm.Value)+" mm ropes; EN 81-20 5.5.2.1 requires D/d ≥ 40 for pulleys (≥ "+L(40*x.RopeDiameterMm.Value)+" mm). The smaller deflector is accounted for in the rope safety factor (EN 81-50 5.12).");
   // ---------- recommendations: what is needed to complete a check ----------
   foreach(var o in r.Omissions.Where(o=>o.Kind=="MISSING_INPUT"))
   {
    const string prefix="Not provided: ";
    if(o.Reason.StartsWith(prefix))n.Recommendations.Add("Enter the "+o.Reason.Substring(prefix.Length).TrimEnd('.')+" to check "+Lower1(o.Item)+" ("+ClauseOf(o.Item)+").");
    else n.Recommendations.Add(o.Item+" not calculated: "+o.Reason.TrimEnd('.')+".");
   }
   return n;
  }
  // Smallest rope count (up to 7, the project rope range) or next listed rope diameter that gives an OK safety factor,
  // computed with the same engine on a copy of the project. A new diameter takes the rope data of that diameter.
  static string SafetyFactorRemedy(ElevatorProject project,DesignContext context,PresetLibrary presets,RopeTractionCalculationEngine engine,RopeTractionEffectiveInputs x)
  {
   var options=new List<string>();
   Func<ElevatorProject,RopeCheck> sf=p=>engine.Calculate(p,context,presets).Checks.FirstOrDefault(c=>c.Id=="safety-factor");
   int current=x.RopeCount??0;
   for(int count=Math.Max(4,current+1);count<=7;count++)
   {
    var p=ProjectJson.Clone(project);p.Suspension.RopeCount=count;var c=sf(p);
    if(c!=null&&c.Ok==true){options.Add(count+" ropes (Sf "+c.Actual+")");break;}
   }
   if(presets!=null&&presets.Ropes!=null&&x.RopeDiameterMm.HasValue)
    foreach(var row in presets.Ropes.Table.Where(t=>t.DiameterMm>x.RopeDiameterMm.Value+1e-9).OrderBy(t=>t.DiameterMm))
    {
     var p=ProjectJson.Clone(project);p.Suspension.RopeDiameterMm=row.DiameterMm;PresetFields.MinimumBreakingLoad.Reset(p);PresetFields.RopeMass.Reset(p);
     var res=engine.Calculate(p,context,presets);var c=res.Checks.FirstOrDefault(k=>k.Id=="safety-factor");
     if(c!=null&&c.Ok==true&&res.Checks.Where(k=>k.Id.StartsWith("dd-")).All(k=>k.Ok==true)){options.Add(L(row.DiameterMm)+" mm ropes with "+L(row.MinimumBreakingLoadKn)+" kN MBL (Sf "+c.Actual+")");break;}
    }
   return options.Count==0?"No rope count up to 7 and no listed rope diameter reaches it; a stronger rope (higher MBL) is needed.":"Use "+String.Join(", or ",options)+".";
  }
  public DocxDocument Render()
  {
   var p=Project;var report=p.Report??new ProjectReportDetails();string date=Inputs.GeneratedLocal.ToString("yyyy-MM-dd",Inv);
   Func<string,string> clean=v=>String.IsNullOrWhiteSpace(v)?null:v.Trim();
   string name=clean(p.Info.Name),number=clean(report.DocumentNumber),revision=clean(report.Revision),client=clean(report.ClientName);
   var d=new DocxDocument{Title="Design Notes"+(name==null?"":" · "+name)};
   d.TitleBlock.Title="Design Notes";d.TitleBlock.Logo=Inputs.Logo;d.TitleBlock.Company=clean(Inputs.CompanyName);
   if(name!=null)d.TitleBlock.ProjectRows.Add(new KeyValuePair<string,string>("Project",name));
   if(client!=null)d.TitleBlock.ProjectRows.Add(new KeyValuePair<string,string>("Client",client));
   if(number!=null)d.TitleBlock.DocumentRows.Add(new KeyValuePair<string,string>("Doc. No.",number));
   if(revision!=null)d.TitleBlock.DocumentRows.Add(new KeyValuePair<string,string>("Rev.",revision));
   d.FooterLeft="Prepared with ShaftMaster · "+date;
   d.FooterRight=number==null&&revision==null?null:(number==null?"":"Doc. No. "+number)+(number!=null&&revision!=null?" · ":"")+(revision==null?"":"Rev "+revision);
   if(Empty){d.Paragraph(NoNotes);return d;}
   foreach(var group in new[]{new{Title="Warnings",Notes=Warnings},new{Title="Recommendations",Notes=Recommendations},new{Title="Notes",Notes=Notes}})
   {
    if(group.Notes.Count==0)continue;
    d.Heading(group.Title,1);
    foreach(var note in group.Notes)d.Paragraph("•  "+note);
   }
   return d;
  }
  public static string SuggestedFileName(string projectName)
  {
   var s=CalculationSheet.SuggestedFileName(projectName);
   return s.Replace(" - Calculation Sheet.docx"," - Design Notes.docx");
  }
  public void Save(string path)
  {
   if(String.IsNullOrWhiteSpace(path))throw new ArgumentException("Choose a file name for the design notes.");
   if(!String.Equals(Path.GetExtension(path),".docx",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Design notes are saved as .docx.");
   var dir=Path.GetDirectoryName(Path.GetFullPath(path));if(!Directory.Exists(dir))throw new DirectoryNotFoundException("Folder not found: "+dir);
   var doc=Render();string temp=Path.Combine(dir,"~"+Path.GetFileName(path)+".tmp");
   try{using(var fs=new FileStream(temp,FileMode.Create,FileAccess.ReadWrite))doc.Save(fs);if(File.Exists(path))File.Delete(path);File.Move(temp,path);}
   catch(IOException e){if(File.Exists(temp))File.Delete(temp);throw new IOException("The design notes could not be saved; close it if it is open in Word. "+e.Message,e);}
  }
 }
}
