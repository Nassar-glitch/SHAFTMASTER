using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Xml;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using Elevator.Core.Application;
using Elevator.Core.Presets;
using Elevator.Core.MachineRoom;
using Elevator.Core.GuideRails;
using Elevator.Core.Car;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.Core.Suspension;
using Elevator.Core.SafetyGear;
using Elevator.Solution.Desktop.Common;
using Elevator.Solution.Bridge;
using Elevator.Reporting.Sheet;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;

// Client-facing calculation sheet: content, omission of unavailable calculations, renumbering, removed
// internal/disclaimer text, unchanged numbers and save-path handling on the packaged engines.
// Projects here are test inputs only, never project defaults.
public static class CalculationSheetTests
{
    static int assertions;static readonly List<string> names=new List<string>();
    static string bin,output,root;static DesignContext ctx;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);assertions++;names.Add(name);}
    // Text removed from the client document (case-insensitive), checked in document, header, footer and docProps.
    public static readonly string[] Removed={
        "preliminary","engineering review","not certified","certified","certification","compliance","complies","statement of",
        "incomplete","not for issue","unavailable","complete the missing","requested calculations",
        "elevator_calculations_gearbox",".docx","sha-256","ecfd591133f5f97ed923f62a3a3eea60c2b97a67805e60bcb7131c3da7fb13df","paragraph","paraid","439730eb","5429655e","3897ff34",
        "gearboxnote","twostageverticalmachinedatum","releasedtwostagewithrigidspreader","-v1","engine record","result currency","fingerprint",
        "calculationsheettests","shaftmaster.exe","adopted","calculated under","user-approved","approved interpretation","interpretation","reference only","synthetic",
        "equal-sharing","equal sharing","not established","rule sources","note-derived","limitations","not included","not provided","invalid text",
        "not covered","excluded","no guide strength","verification","k2","status","used by","unsaved","project file","project identifier","application"};
    static ElevatorProject Example(string name)
    {
        var p=ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin,"reviewed-example.elevator.json")));p.Info.Name=name;
        foreach(var f in p.Building.Floors.Take(p.Building.Floors.Count-1))if(!f.HeightToNextMm.HasValue)f.HeightToNextMm=3000;
        return p;
    }
    static ElevatorProject BasicComplete(string name)
    {
        var p=Example(name);p.Elevator.CapacityKg=480;p.Shaft.WidthMm=1500;p.Shaft.DepthMm=2500;p.Machine.TractionSheavePitchDiameterMm=520;p.Elevator.SpeedMetresPerSecond=1.0;
        p.Calculations=new ChassisCalculationInputs{Mode="Basic",CarMassKg=500,CounterweightMassKg=740,MachineMassKg=300};
        p.Calculations.SafetyPit=new SafetyPitInputs{SafetyGearType="Progressive",CarGuideCount=2,CarBufferCount=1,CounterweightBufferCount=2,CounterweightBufferSharing="EqualSharing"};
        p.Calculations.CarGuideForces=new CarGuideForceInputs{GuideShoeSpacingMm=1200};
        return p;
    }
    static ElevatorProject AdvancedComplete(string name)
    {
        var p=ProjectJson.Deserialize(File.ReadAllText(Path.Combine(root,"tests/fixtures/SYNTHETIC.elevator.json")));p.Info.Name=name;
        var c=p.Calculations;c.MachineCgSource="Machine supplier drawing MR-01";c.TransferAssumptionSource="Chassis drawing CH-02";c.HangerAttachmentSource="Hanger detail HG-03";
        c.ChassisWeightSourceOrExclusionReason="Chassis weight carried separately";c.HangerWeightSourceOrExclusionReason="Hanger weight carried separately";
        c.SafetyPit=new SafetyPitInputs{SafetyGearType="Progressive",CarGuideCount=2,CarBufferCount=1,CounterweightBufferCount=1};
        c.CarGuideForces=new CarGuideForceInputs{GuideShoeSpacingMm=1200};
        return p;
    }
    // Document body/header/footer text (paragraphs on separate lines) and docProps text.
    static string Text(string path,out string props)
    {
        var b=new StringBuilder();var pr=new StringBuilder();
        using(var package=Package.Open(path,FileMode.Open,FileAccess.Read))
        {
            foreach(var part in package.GetParts().Where(x=>x.ContentType.EndsWith("+xml")||x.ContentType.EndsWith("/xml")))
            {
                var xml=new XmlDocument();using(var s=part.GetStream())xml.Load(s); // throws on malformed XML
                string uri=part.Uri.OriginalString;
                if(part.ContentType.Contains("core-properties")){pr.Append(xml.InnerText).Append('\n');continue;}
                if(!uri.StartsWith("/word/document")&&!uri.StartsWith("/word/header")&&!uri.StartsWith("/word/footer"))continue;
                var ns=new XmlNamespaceManager(xml.NameTable);ns.AddNamespace("w","http://schemas.openxmlformats.org/wordprocessingml/2006/main");
                foreach(XmlNode p in xml.SelectNodes("//w:p",ns)){foreach(XmlNode t in p.SelectNodes(".//w:t|.//w:br",ns))b.Append(t.LocalName=="br"?"\n":t.InnerText);b.Append('\n');}
            }
            Check(package.GetParts().Any(x=>x.Uri.OriginalString=="/word/document.xml")&&package.GetParts().Any(x=>x.Uri.OriginalString=="/word/styles.xml"),"Package has main document and styles parts: "+Path.GetFileName(path));
        }
        props=pr.ToString();return b.ToString();
    }
    static string Section(string text,string start,string end){int a=text.IndexOf(start,StringComparison.Ordinal);if(a<0)return "";int z=end==null?-1:text.IndexOf(end,a+start.Length,StringComparison.Ordinal);return z<0?text.Substring(a):text.Substring(a,z-a);}
    static void Clean(string text,string props,ElevatorProject p,string file)
    {
        string all=(text+"\n"+props).ToLowerInvariant();
        foreach(var r in Removed)Check(!all.Contains(r),"Removed text absent ("+r+"): "+file);
        Check(p.Info.ProjectId==null||!all.Contains(p.Info.ProjectId.ToLowerInvariant()),"Project GUID absent: "+file);
        // Supplied offsets (Δ rows) and coordinate/vector cells may legitimately be zero; any other zero would be a placeholder.
        Check(!text.Split('\n').Where(l=>!l.Contains("Δ")&&!l.StartsWith("(")).Any(l=>Regex.IsMatch(l,@"(?<![\d.,])0(\.0+)? (N|kN|kg|mm)\b")),"No zero placeholder value: "+file);
        Check(!text.Contains("NaN")&&!text.Contains("Infinity")&&!text.Contains("∞"),"No nonfinite numbers rendered: "+file);
        Check(text.Contains(CalculationSheet.DesignBasis),"Design-basis line present: "+file);
        Check(text.Contains("Elevator Calculation Sheet\n")&&text.Contains("Prepared with ShaftMaster · "),"Title and footer band: "+file);
        Layout(file);
        Check(text.Contains("Calculation\nKey result\n")&&text.Contains("Input\nValue\n"),"Summary is Calculation | Key result; inputs are Input | Value: "+file);
        Check(Regex.Matches(text,"FF1 acts along the car-rail line").Count<=1,"No duplicated FF description: "+file);
    }
    static XmlNamespaceManager Ns()
    {
        var ns=new XmlNamespaceManager(new NameTable());ns.AddNamespace("w","http://schemas.openxmlformats.org/wordprocessingml/2006/main");ns.AddNamespace("wp","http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing");
        ns.AddNamespace("a","http://schemas.openxmlformats.org/drawingml/2006/main");ns.AddNamespace("asvg","http://schemas.microsoft.com/office/drawing/2016/SVG/main");ns.AddNamespace("r","http://schemas.openxmlformats.org/officeDocument/2006/relationships");
        return ns;
    }
    static string Concat(XmlNode n,XmlNamespaceManager ns){return String.Concat(n.SelectNodes(".//w:t",ns).Cast<XmlNode>().Select(t=>t.InnerText));}
    // Page frame, title block header on every page (single default header, no first-page variant), Page X of Y fields, keep-with-next.
    static void Layout(string file)
    {
        var ns=Ns();
        using(var package=Package.Open(Path.Combine(output,file),FileMode.Open,FileAccess.Read))
        {
            var doc=Part(package,"/word/document.xml",ns);var hdr=Part(package,"/word/header1.xml",ns);var styles=Part(package,"/word/styles.xml",ns);
            var sect=doc.SelectSingleNode("//w:body/w:sectPr",ns);var borders=sect.SelectSingleNode("w:pgBorders",ns);
            Check(sect.SelectSingleNode("w:pgSz[@w:w='11906' and @w:h='16838']",ns)!=null,"A4 portrait: "+file);
            Check(borders!=null&&borders.Attributes["w:offsetFrom"].Value=="page"&&new[]{"top","left","bottom","right"}.All(side=>{var e=borders.SelectSingleNode("w:"+side,ns);return e!=null&&e.Attributes["w:val"].Value.Contains("SmallGap")&&e.Attributes["w:sz"].Value=="12"&&Int32.Parse(e.Attributes["w:space"].Value)>=26;}),"Page border frame on all sides ~10 mm from the edge: "+file);
            Check(sect.SelectNodes("w:headerReference",ns).Count==1&&sect.SelectSingleNode("w:headerReference[@w:type='default']",ns)!=null&&sect.SelectSingleNode("w:titlePg",ns)==null,"One default header on every page, page 1 included: "+file);
            Check(sect.SelectSingleNode("w:footerReference[@w:type='default']",ns)!=null,"Footer band on every page: "+file);
            var instr=hdr.SelectNodes("//w:instrText",ns).Cast<XmlNode>().Select(x=>x.InnerText.Trim()).ToList();
            Check(instr.Contains("PAGE")&&instr.Contains("NUMPAGES"),"Page X of Y uses PAGE / NUMPAGES fields: "+file);
            var grid=hdr.SelectSingleNode("/w:hdr/w:tbl/w:tblGrid",ns);
            Check(String.Join(",",grid.SelectNodes("w:gridCol",ns).Cast<XmlNode>().Select(x=>x.Attributes["w:w"].Value))=="1500,3000,3038,2100"&&hdr.SelectSingleNode("/w:hdr/w:tbl/w:tr/w:trPr/w:trHeight[@w:val='1020' and @w:hRule='exact']",ns)!=null,"Title block geometry fixed (columns 1500/3000/3038/2100, height exact): "+file);
            foreach(var id in new[]{"Heading1","Heading2","Heading3"})Check(styles.SelectSingleNode("//w:style[@w:styleId='"+id+"']/w:pPr/w:keepNext",ns)!=null,"Keep-with-next on "+id+": "+file);
            foreach(XmlNode tbl in doc.SelectNodes("//w:body/w:tbl[.//asvg:svgBlip]",ns))
            {
                var rows=tbl.SelectNodes("w:tr",ns);
                Check(rows.Count==2&&rows.Cast<XmlNode>().All(r=>r.SelectSingleNode("w:trPr/w:cantSplit",ns)!=null)&&rows[0].SelectSingleNode(".//w:p/w:pPr/w:keepNext",ns)!=null&&Concat(rows[1],ns).StartsWith("Figure "),"Figure kept with its caption strip, rows not split: "+file);
            }
        }
    }
    static List<string> Headings(string text){return Regex.Matches(text,@"(?m)^(\d+)  ([^\n]+)$").Cast<Match>().Select(m=>m.Groups[1].Value+" "+m.Groups[2].Value).ToList();}
    // Section order as rendered: the rope / traction section follows the rope tensions when available.
    static string[] Expect(CalculationSheet sheet,string[] baseOrder)
    {
        var order=new List<string>{Summary,Inputs};
        if(sheet.ChassisAvailable)order.Add(Reactions);if(sheet.RopeTensionsAvailable)order.Add(Ropes);if(sheet.RopeTractionAvailable)order.Add(RopeChecks);
        if(sheet.Loads.Any(l=>l.PerComponentLoadN.HasValue&&l.GroupLoadN.HasValue&&l.ParticipatingMassKg.HasValue))order.Add(Pit);if(sheet.CarGuideForcesAvailable)order.Add(Guide);
        if(baseOrder!=null)Check(order.Where(t=>t!=RopeChecks).SequenceEqual(baseOrder),"Section order unchanged apart from the rope / traction section: "+String.Join(", ",order));
        return order.ToArray();
    }
    // Omissions other than the rope / traction section (which depends on the shared deflector geometry of each fixture).
    static List<OmittedCalculation> BaseOmitted(CalculationSheet sheet){return sheet.Omitted.Where(o=>!o.Name.StartsWith(RopeChecks)).ToList();}
    static string H(CalculationSheet sheet,string title){var order=Expect(sheet,null);int i=Array.IndexOf(order,title);return (i+1)+"  "+title;}
    static void Numbered(string text,string[] titles,string file)
    {
        var h=Headings(text);
        Check(h.Count==titles.Length,"Section count "+titles.Length+": "+file+" → "+String.Join(" | ",h));
        for(int i=0;i<titles.Length;i++)Check(h[i]==(i+1)+" "+titles[i],"Section "+(i+1)+" is "+titles[i]+": "+file);
    }
    static CalculationSheet Export(ElevatorProject p,string file,CalculationSheetInputs inputs,out string text)
    {
        var sheet=CalculationSheet.Build(p,ctx,inputs??new CalculationSheetInputs());
        string path=Path.Combine(output,file);Check(!File.Exists(path),"Fresh output path: "+file);sheet.Save(path);
        Check(File.Exists(path)&&new FileInfo(path).Length>3000,"Sheet written: "+file);
        string props;text=Text(path,out props);Clean(text,props,p,file);
        return sheet;
    }
    static bool Near(double? a,double b){return a.HasValue&&Math.Abs(a.Value-b)<1e-6*Math.Max(1,Math.Abs(b));}
    const string Summary="Calculation summary",Inputs="Input data",Reactions="Machine-room bearing reactions P1–P4",Ropes="Suspension rope tensions",RopeChecks="Suspension rope and traction checks",Pit="Safety-gear and buffer loads P5–P7",Guide="Car guide-rail forces FF1 / FF2";
    [STAThread] // WPF figure rendering
    public static int Main(string[] args)
    {
        FigureRender.Rasterizer=new WpfFigureRasterizer(); // WPF text measurement and PNG, as in the application
        root=Path.GetFullPath(args[0]);bin=Path.GetFullPath(args[1]);output=Path.GetFullPath(args[2]);
        if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any()){Console.Error.WriteLine("Output folder is not empty: "+output);return 2;}
        Directory.CreateDirectory(output);
        int exit=1;
        try{Run();exit=0;}
        catch(Exception e){Wire.Write(Path.Combine(output,"calculation-sheet-error.json"),new{Error=e.ToString()});Console.Error.WriteLine(e);}
        finally{Wire.Write(Path.Combine(output,"calculation-sheet-tests.json"),new{Status=exit==0?"PASS":"FAIL",Assertions=assertions,Checks=names,Method="CalculationSheet.Build/Save on packaged engines; .docx opened with System.IO.Packaging and every XML part parsed; text from document/header/footer and docProps. Test inputs only."});}
        Console.WriteLine((exit==0?"PASS: ":"FAIL: ")+assertions+" calculation sheet assertions.");
        return exit;
    }
    static void Run()
    {
        ctx=Wire.Read<DesignContext>(Path.Combine(bin,"reviewed-context.json"));ctx.ChassisEvidence=Wire.Read<ChassisGeometryEvidence>(Path.Combine(bin,"reviewed-chassis-evidence.json"));
        string text;

        // 1. Basic complete; FF fixture through live shared geometry (W 1100, D 2100, Q 480, h 1200).
        var p=BasicComplete("Test Tower A — Lift 1");
        var sheet=Export(p,"01-basic-complete.docx",null,out text);
        FigureChecks(sheet,"01-basic-complete.docx",new[]{"F1","F2","F3","F7","F8","F9","F4","F5","F6"},"default");
        MomentBalance(sheet,"01");
        Numbered(text,Expect(sheet,new[]{Summary,Inputs,Reactions,Ropes,Pit,Guide}),"01");
        Check(sheet.HasContent&&BaseOmitted(sheet).Count==0&&sheet.Omitted.Count==1&&sheet.Omitted[0].Name==RopeChecks+" (part)"&&sheet.RopeTraction.Omissions.All(o=>o.Kind=="INVALID_GEOMETRY"),"Complete Basic: nothing omitted except the rope / traction checks of the invalid deflector layout (TZ 1200)");
        Check(Near(sheet.CarGuideForces.InternalWidthMm,1100)&&Near(sheet.CarGuideForces.InternalDepthMm,2100),"Live shared internal cabin 1100 × 2100 mm");
        Check(Near(sheet.CarGuideForces.FF1BaseN,1100)&&Near(sheet.CarGuideForces.FF1DesignN,2200)&&Near(sheet.CarGuideForces.FF2BaseN,1050)&&Near(sheet.CarGuideForces.FF2DesignN,2100),"FF fixture unchanged: FF1 1100/2200 N, FF2 1050/2100 N");
        string ff=Section(text,H(sheet,Guide),null);
        Check(ff.Contains("FF1 = Q × 10 N/kg × W / (4 h) = 480 × 10 × 1100 / (4 × 1200) = 1100 N")&&ff.Contains("FF1(design) = 2 × FF1 = 2 × 1100 = 2200 N"),"FF1 equation with substitution and ×2 design factor");
        Check(ff.Contains("FF2 = Q × 10 N/kg × D / (8 h) = 480 × 10 × 2100 / (8 × 1200) = 1050 N")&&ff.Contains("FF2(design) = 2 × FF2 = 2 × 1050 = 2100 N"),"FF2 equation with substitution and ×2 design factor");
        Check(ff.Contains("acts along the car-rail line, in the car-width direction")&&ff.Contains("acts perpendicular to the car-rail line, in the car-depth direction")&&ff.Contains("per guide rail at one guide-shoe level")&&ff.Contains("vertical centre-to-centre distance between the upper and lower car guide shoes"),"FF physical meaning stated plainly");
        var b=Section(text,H(sheet,Reactions),H(sheet,Ropes));
        Check(b.Contains("W = Σm × g = (480 + 500 + 740 + 300) kg × 9.81 m/s² = 19816.2 N")&&sheet.Chassis.Reactions.Count==4,"Basic resultant from included masses only");
        foreach(var r in sheet.Chassis.Reactions)Check(b.Contains(r.Id+" = F(")&&b.Contains(r.UpwardN.ToString("0.##",CultureInfo.InvariantCulture)),"Basic reaction equation and value: "+r.Id);
        Check(Math.Abs(sheet.Chassis.Reactions.Sum(r=>r.UpwardN)-2020*9.81)<1e-6,"Basic reactions sum to W (unchanged)");
        Check(!b.Contains("additional")&&!b.Contains("Chassis assembly")&&!b.Contains("Hanger"),"Mass table lists only included masses");
        var rope=Section(text,H(sheet,Ropes),H(sheet,Pit));
        Check(rope.Contains("T(car) = (Q + M(car)) × g / r = (480 + 500) kg × 9.81 m/s² / 1 = 9613.8 N")&&rope.Contains("Per rope (N)")&&rope.Contains("1602.3"),"Rope tensions with per-rope values");
        var pit=Section(text,H(sheet,Pit),H(sheet,Guide));
        Check(pit.Contains("m(car) = Q + M(car) = 480 + 500 = 980 kg")&&pit.Contains("P5 = m(car) × 10 N/kg = 980 kg × 10 N/kg = 9800 N per car guide rail"),"P5 per car guide rail unchanged (9800 N)");
        Check(pit.Contains("P6(total) = m(car) × max(40, 4 × 9.81) N/kg = 980 kg × 40 N/kg = 39200 N")&&pit.Contains("P6(per buffer) = P6(total) / n = 39200 / 1 = 39200 N"),"P6 total and per buffer unchanged (39200 N)");
        Check(pit.Contains("P7(total) = m(cwt) × max(40, 4 × 9.81) N/kg = 740 kg × 40 N/kg = 29600 N")&&pit.Contains("P7(per buffer) = P7(total) / n = 29600 / 2 = 14800 N"),"P7 total and per buffer unchanged (29600 / 14800 N)");
        Check(Section(text,H(sheet,Summary),H(sheet,Inputs)).Contains("P5 safety-gear load per car guide rail\n9.8 kN per car guide rail")&&Section(text,H(sheet,Summary),H(sheet,Inputs)).Contains("14.8 kN per buffer; 29.6 kN total"),"Summary key results");
        var inputs=Section(text,H(sheet,Inputs),H(sheet,Reactions));
        Check(inputs.Contains("Rated load Q\n480 kg")&&inputs.Contains("Guide-shoe spacing h\n1200 mm")&&!inputs.Contains("speed")&&!inputs.Contains("additional"),"Inputs list only supplied values");

        // 2. Advanced complete.
        p=AdvancedComplete("Test Tower B — Lift 2");
        sheet=Export(p,"02-advanced-complete.docx",null,out text);
        FigureChecks(sheet,"02-advanced-complete.docx",new[]{"F1","F2","F7","F8","F9","F4","F5","F6"},null);
        Numbered(text,Expect(sheet,new[]{Summary,Inputs,Reactions,Ropes,Pit,Guide}),"02");
        Check(sheet.Mode=="Advanced"&&sheet.ChassisAvailable&&BaseOmitted(sheet).Count==0,"Advanced complete");
        var a=Section(text,H(sheet,Reactions),H(sheet,Ropes));
        Check(a.Contains("static equilibrium")&&a.Contains("Machine weight")&&a.Contains("Rope forces at traction sheave")&&a.Contains("Rope forces at deflector hanger")&&!a.Contains("one vertical load at the traction-sheave centre"),"Advanced rope/member actions, distinct from Basic");
        Check(a.Contains("Machine supplier drawing MR-01")&&a.Contains("Middle longitudinal member / Right longitudinal member"),"Advanced chassis model data");
        foreach(var r in sheet.Chassis.Reactions)Check(a.Contains(r.UpwardN.ToString("0.##",CultureInfo.InvariantCulture)),"Advanced reaction rendered "+r.Id);
        Check(Section(text,H(sheet,Ropes),H(sheet,Pit)).Contains("/ 6 ="),"Advanced rope tensions per rope");
        Check(!a.Contains("separately"),"Excluded self-weights are not listed");

        // 3. Missing h: FF omitted entirely, others intact and renumbered.
        p=BasicComplete("Test Tower C — missing h");p.Calculations.CarGuideForces=new CarGuideForceInputs();
        sheet=Export(p,"03-missing-h.docx",null,out text);
        FigureChecks(sheet,"03-missing-h.docx",new[]{"F1","F2","F3","F7","F8","F9"},null);
        Numbered(text,Expect(sheet,new[]{Summary,Inputs,Reactions,Ropes,Pit}),"03");
        Check(!text.Contains("FF1")&&!text.Contains("FF2")&&!text.Contains("Guide-shoe spacing"),"Missing h: no FF content anywhere");
        Check(BaseOmitted(sheet).Count==1&&BaseOmitted(sheet)[0].Name.StartsWith("FF1 / FF2")&&BaseOmitted(sheet)[0].Reasons.Any(r=>r.StartsWith("Guide-shoe spacing h")),"Omission recorded for the application with the missing input");
        Check(sheet.ChassisAvailable&&Near(sheet.SafetyPit.P5.PerComponentLoadN,9800),"Missing h keeps P1–P7");

        // 4. Missing masses and pit choices: P1–P7 and rope tensions omitted, FF kept.
        p=BasicComplete("Test Tower D — FF only");p.Calculations.CarMassKg=null;p.Calculations.SafetyPit=null;
        sheet=Export(p,"04-missing-masses-and-pit.docx",null,out text);
        FigureChecks(sheet,"04-missing-masses-and-pit.docx",new[]{"F1","F4","F5","F6"},null);
        Numbered(text,Expect(sheet,new[]{Summary,Inputs,Guide}),"04");
        {var outside=sheet.RopeTractionAvailable?text.Replace(Section(text,H(sheet,RopeChecks),H(sheet,Guide)),"").Replace("Rope nominal diameter d","").Replace("Traction sheave pitch diameter Dt",""):text;Check(!outside.Contains("P1")&&!outside.Contains("P5")&&!outside.Contains("P7")&&!outside.Contains("T(car)")&&!outside.Contains("Car mass")&&!outside.Contains("Machine mass")&&!outside.Contains("Counterweight mass")&&!outside.Contains("ropes"),"Omitted sections leave no content or input rows");}
        Check(BaseOmitted(sheet).Count==4&&BaseOmitted(sheet).Any(o=>o.Reasons.Any(r=>r.StartsWith("Car mass")))&&BaseOmitted(sheet).Any(o=>o.Reasons.Any(r=>r.StartsWith("Safety-gear type"))),"Application omission list names missing masses and pit choices");
        Check(Section(text,H(sheet,Guide),null).Contains("= 2200 N"),"FF unchanged when P1–P7 omitted");

        // 5. Malformed UI text: dependent calculations omitted, never zero.
        p=BasicComplete("Test Tower E — malformed text");p.Calculations.CarMassKg=null;p.Calculations.CarGuideForces.GuideShoeSpacingMm=null;
        var drafts=new CalculationSheetInputs{CarGuideForceDraftError="Vertical upper-to-lower car guide-shoe spacing h (mm): invalid text; enter a finite positive number."};
        drafts.ChassisDraftErrors["Calculations.CarMassKg"]="Empty car mass: finite number required.";drafts.SafetyPitDraftErrors["Calculations.CarMassKg"]="Empty car mass: finite number required. Malformed text is not an omitted mass.";
        sheet=Export(p,"05-malformed-text.docx",drafts,out text);
        FigureChecks(sheet,"05-malformed-text.docx",new[]{"F1","F9"},null);
        Check(Tagged(Svgs(sheet)["F9"],"car-buffer-1")==null&&Tagged(Svgs(sheet)["F9"],"cw-buffer-1")!=null,"F9 with only P7 available shows the counterweight buffers only");
        Numbered(text,Expect(sheet,new[]{Summary,Inputs,Pit}),"05");
        Check(sheet.Chassis.Status=="INVALID_INPUT"&&sheet.SafetyPit.P5.Status=="INVALID_INPUT"&&sheet.SafetyPit.P6.Status=="INVALID_INPUT"&&sheet.CarGuideForces.Status=="INVALID_INPUT","Malformed car mass and h make dependent calculations unavailable");
        Check(text.Contains("P7(total)")&&!text.Contains("P5 =")&&!text.Contains("P6(total)")&&!text.Contains("FF1"),"Only P7 remains in the pit section");
        Check(!text.Contains("Machine mass")&&!text.Contains("Shaft width")&&text.Contains("Counterweight mass\n740 kg"),"Inputs limited to those used by the remaining calculation");
        Check(BaseOmitted(sheet).Count==4&&BaseOmitted(sheet).All(o=>o.Reasons.Count>0),"Malformed text listed for the application");

        // 6. Changed inputs: every export recalculates.
        p=BasicComplete("Test Tower F — changed inputs");
        var first=CalculationSheet.Build(p,ctx,new CalculationSheetInputs());p.Elevator.CapacityKg=600;p.Calculations.CarGuideForces.GuideShoeSpacingMm=1000;
        sheet=Export(p,"06-changed-inputs.docx",null,out text);
        Check(Near(first.CarGuideForces.FF1BaseN,1100)&&Near(sheet.CarGuideForces.FF1BaseN,1650)&&text.Contains("600 × 10 × 1100 / (4 × 1000) = 1650 N")&&!text.Contains("= 1100 N"),"Changed Q/h recalculated; stale values absent");
        Check(Near(sheet.SafetyPit.P5.PerComponentLoadN,11000),"P5 recalculated with changed load");

        // 7. Missing rope count: per-rope column omitted, bundle kept.
        p=BasicComplete("Test Tower G — no rope count");p.Suspension.RopeCount=null;
        sheet=Export(p,"07-no-rope-count.docx",null,out text);
        rope=Section(text,H(sheet,Ropes),H(sheet,Pit));
        Check(sheet.ChassisAvailable&&rope.Contains("All ropes (N)")&&!rope.Contains("Per rope")&&!rope.Contains("/ n")&&!text.Contains("Number of suspension ropes"),"No rope count: bundle only, no per-rope column");

        // 8. Advanced incomplete keeps other sections.
        p=AdvancedComplete("Test Tower H — Advanced missing CG basis");p.Calculations.MachineCgSource=null;
        sheet=Export(p,"08-advanced-missing-cg.docx",null,out text);
        Numbered(text,Expect(sheet,new[]{Summary,Inputs,Pit,Guide}),"08");
        Check(BaseOmitted(sheet).Count==1&&BaseOmitted(sheet)[0].Reasons.Any(r=>r.Contains("MachineCgSource")),"Advanced missing CG basis recorded for the application");

        // 9. Explicit zero and blank optional additions are not listed.
        p=BasicComplete("Test Tower I — zero additions");p.Calculations.CarSideAdditionalSuspendedMassKg=0;
        sheet=Export(p,"09-zero-additions.docx",null,out text);
        Check(!text.Contains("additional")&&text.Contains("(480 + 500 + 740 + 300) kg"),"Zero/blank optional masses absent from inputs and equations");

        // 10. Nothing available: export blocked.
        var session=new ProjectSession(null);session.Open(Path.Combine(bin,"reviewed-example.elevator.json"));
        {
            var s=CalculationSheet.Build(new ElevatorProject(),ctx,new CalculationSheetInputs());bool threw=false;string path=Path.Combine(output,"blocked.docx");
            try{s.Save(path);}catch(InvalidOperationException){threw=true;}
            Check(!s.HasContent&&threw&&!File.Exists(path)&&BaseOmitted(s).Count==5&&s.Omitted.Count==6,"Nothing available: export blocked, five calculations and the rope / traction checks omitted");
            // The reviewed example (no masses, no sheave diameter, no speed; 8 mm ropes on the 250 mm deflector): its only rope result was the
            // deflector D/d, which is a value only since DESIGN-FLOW 11a, so no section is available and nothing is NOT OK.
            s=CalculationSheet.Build(session.Project,ctx,new CalculationSheetInputs());
            Check(!s.HasContent&&!s.RopeTractionAvailable&&!s.ChassisAvailable&&!s.CarGuideForcesAvailable&&BaseOmitted(s).Count==5&&!s.RopeTraction.Checks.Any(c=>c.Id=="dd-deflector")&&!s.NotOk.Any(x=>x.StartsWith("Deflector D/d")),"Reviewed example: no deflector D/d check (DESIGN-FLOW 11a), so no rope / traction section and no NOT OK item; nothing to export");
        }
        Check(session.Project.Calculations==null||(session.Project.Calculations.CarGuideForces==null&&session.Project.Calculations.SafetyPit==null),"Export does not add input groups to the project");

        // 11. Save-path handling.
        Check(CalculationSheet.SuggestedFileName("Tower A: Lift 1/2*")=="Tower A_ Lift 1_2_ - Calculation Sheet.docx","Invalid filename characters replaced");
        Check(CalculationSheet.SuggestedFileName("  ")=="Untitled project - Calculation Sheet.docx"&&CalculationSheet.SuggestedFileName(null)=="Untitled project - Calculation Sheet.docx","Blank name uses Untitled project");
        Check(CalculationSheet.SuggestedFileName(new string('x',200)).Length<=80+" - Calculation Sheet.docx".Length,"Long names truncated");
        string target=Path.Combine(output,"11-save-path.docx");
        var good=CalculationSheet.Build(BasicComplete("Test Tower J — save path"),ctx,new CalculationSheetInputs());good.Save(target);long size=new FileInfo(target).Length;good.Save(target);
        Check(File.Exists(target)&&new FileInfo(target).Length==size,"Existing sheet replaced atomically");
        byte[] before=File.ReadAllBytes(target);bool locked=false;
        using(new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.None)){try{good.Save(target);}catch(IOException e){locked=e.Message.Contains("close it");}}
        Check(locked&&File.ReadAllBytes(target).SequenceEqual(before),"Locked target: clear error, original file unchanged");
        Check(!Directory.GetFiles(output,"~*.docx.tmp").Any(),"No temporary files left after failure");
        bool missing=false;try{good.Save(Path.Combine(output,"no-such-folder","x.docx"));}catch(DirectoryNotFoundException){missing=true;}Check(missing,"Missing folder rejected");
        bool extension=false;try{good.Save(Path.Combine(output,"x.doc"));}catch(ArgumentException){extension=true;}Check(extension&&!File.Exists(Path.Combine(output,"x.doc")),"Non-.docx extension rejected");
        bool blank=false;try{good.Save(" ");}catch(ArgumentException){blank=true;}Check(blank,"Blank path rejected");
        // Changed geometry: different SW, SD, h and capacity change the figures accordingly.
        p=BasicComplete("Test Tower K — changed geometry");p.Shaft.WidthMm=1650;p.Shaft.DepthMm=2600;p.Elevator.CapacityKg=630;p.Calculations.CarGuideForces.GuideShoeSpacingMm=1000;
        sheet=Export(p,"12-changed-geometry.docx",null,out text);
        FigureChecks(sheet,"12-changed-geometry.docx",new[]{"F1","F2","F3","F7","F8","F9","F4","F5","F6"},"changed");
        MomentBalance(sheet,"12");
        var svg1=Svgs(sheet);
        Check(Value(svg1["F1"],"dim-SW")=="1650"&&Value(svg1["F1"],"dim-SD")=="2600"&&Value(svg1["F5"],"dim-h")=="1000","Changed SW, SD and h appear in F1 and F5");
        Check(Value(svg1["F1"],"dim-W")==Figure.Mm(sheet.Plan.CarWidth)&&sheet.Plan.CarWidth>1100&&Value(svg1["F1"],"dim-D")==Figure.Mm(sheet.Plan.CarDepth)&&sheet.Plan.CarDepth>2100,"Changed internal W and D appear in F1 and F4");
        Check(Near(sheet.CarGuideForces.FF1DesignN,2*630*10*sheet.Plan.CarWidth/(4*1000.0))&&Value(svg1["F4"],"force-ff1-left")==Figure.Num(sheet.CarGuideForces.FF1DesignN.Value/1000,"0.###")&&Value(svg1["F4"],"force-ff1-left")!="2.2","Changed capacity and h change the FF values in F4");
        Check(Value(svg1["F2"],"reaction-P1")==Figure.Num(sheet.Chassis.Reactions.Single(r=>r.Id=="P1").UpwardN/1000,"0.###"),"Changed capacity changes the P1 value in F2");
        // Counts follow the inputs: 4 ropes, 2 car buffers, 3 CW buffers; 4 car guide rails is not a supported P5 arrangement, so P5 and F8 are omitted.
        p=BasicComplete("Test Tower L — changed counts");p.Suspension.RopeCount=4;var sp=p.Calculations.SafetyPit;sp.CarBufferCount=2;sp.CarBufferSharing="EqualSharing";sp.CounterweightBufferCount=3;sp.CarGuideCount=4;
        sheet=Export(p,"13-changed-counts.docx",null,out text);
        FigureChecks(sheet,"13-changed-counts.docx",new[]{"F1","F2","F3","F7","F9","F4","F5","F6"},null);
        var svg13=Svgs(sheet);
        Check(Tagged(svg13["F7"],"roping").InnerText=="n = 4 ropes, 1:1"&&Value(svg13["F7"],"tension-car-rope")==Figure.Num(sheet.Chassis.CarPerRopeTensionN.Value/1000,"0.###")&&Near(sheet.Chassis.CarPerRopeTensionN,9613.8/4),"F7 follows the rope count (4 ropes, per rope T(car)/4)");
        Check(Count(svg13["F9"],"car-buffer-")==2&&Count(svg13["F9"],"cw-buffer-")==3&&Count(svg13["F9"],"car-force-")==2&&Count(svg13["F9"],"cw-force-")==3,"F9 follows the buffer counts (2 car, 3 CW)");
        Check(Value(svg13["F9"],"car-force-1")=="19.6"&&Value(svg13["F9"],"car-total")=="39.2"&&Value(svg13["F9"],"cw-force-1")==Figure.Num(29600/3.0/1000,"0.###")&&Value(svg13["F9"],"cw-total")=="29.6","F9 per-buffer values follow the counts; totals unchanged");
        Check(sheet.SafetyPit.P5.Status=="UNSUPPORTED_CONFIGURATION"||!sheet.SafetyPit.P5.PerComponentLoadN.HasValue,"Four car guide rails: P5 not available, F8 omitted with its section");
        var three=new SafetyPitLoad{Id="P5",PerComponentLoadN=9800,GroupLoadN=29400,ParticipatingMassKg=980,ComponentCount=3};
        var f8=CalculationFigures.SafetyGear(three,sheet.CarRail);
        Check(f8.Entities.OfType<FigurePolyline>().Count(e=>e.Tag!=null&&Regex.IsMatch(e.Tag,@"^rail-\d+$"))==3&&f8.Entities.OfType<FigurePolyline>().Count(e=>e.Tag!=null&&e.Tag.StartsWith("force-p5-"))==3&&f8.Texts.Single(t=>t.Tag=="rail-count").Content=="3 car guide rails","F8 builder follows the rail count (3 rails, 3 arrows)");
        RopeTractionSheet();
        TitleBlock();
    }
    // Body text of a .docx (document part only; header and footer excluded), one paragraph per line.
    static string Body(string path)
    {
        var b=new StringBuilder();
        using(var package=Package.Open(path,FileMode.Open,FileAccess.Read))
        {
            var xml=new XmlDocument();using(var st=package.GetPart(new Uri("/word/document.xml",UriKind.Relative)).GetStream())xml.Load(st);
            var ns=new XmlNamespaceManager(xml.NameTable);ns.AddNamespace("w","http://schemas.openxmlformats.org/wordprocessingml/2006/main");
            foreach(XmlNode para in xml.SelectNodes("//w:body/w:p",ns)){foreach(XmlNode t in para.SelectNodes(".//w:t",ns))b.Append(t.InnerText);b.Append('\n');}
            Check(xml.SelectNodes("//w:body//w:tbl",ns).Count==0,"Design Notes body has no tables: "+path);
        }
        return b.ToString();
    }
    // Design Notes are short advisory notes: no tables of checks, no basis section, no notes about passed checks.
    static void DesignNotesClean(string path,string text,string label)
    {
        var body=Body(path);var lower=body.ToLowerInvariant();
        foreach(var bad in new[]{"not credited","basis","code checks","status","project value","requirement","checks not calculated","calculation sheet shows"})Check(!lower.Contains(bad),"Design Notes "+label+" without '"+bad+"'");
        Check(!body.Split('\n').Any(l=>l.Trim().EndsWith(" OK")||l.Trim()=="OK"),"Design Notes "+label+": no OK items");
        var notes=body.Split('\n').Where(l=>l.StartsWith("•")).ToList();
        Check(notes.Count>0&&notes.All(l=>l.Length<=260),"Design Notes "+label+": every note is one or two lines ("+notes.Count+" notes, longest "+(notes.Count==0?0:notes.Max(l=>l.Length))+" characters)");
    }
    // ---------- rope / traction section, F10 and Design Notes ----------
    static void RopeTractionSheet()
    {
        string text;
        // 14. Complete rope / traction: 1500 × 2100 → 400 mm deflector, α 152°; 4 × 10 mm ropes; presets (MBL 44 kN, U β 95° γ 30°).
        var p=BasicComplete("Test Tower M — ropes and traction");p.Shaft.DepthMm=2100;p.Suspension.RopeDiameterMm=10;p.Suspension.RopeCount=4;
        var sheet=Export(p,"14-rope-traction.docx",null,out text);
        FigureChecks(sheet,"14-rope-traction.docx",new[]{"F1","F2","F3","F7","F10","F8","F9","F4","F5","F6"},"ropes");
        var r=sheet.RopeTraction;
        Check(sheet.RopeTractionAvailable&&r.Traction.Count==3&&r.Checks.Any(c=>c.Id=="safety-factor")&&!r.Checks.Any(c=>c.Id=="dd-deflector")&&r.DpOverDr.HasValue,"Complete rope / traction fixture: all checks and three traction cases; deflector D/d is a value only");
        var rt=Section(text,H(sheet,RopeChecks),H(sheet,Pit));
        Check(rt.Contains("Tmax = ((P + Q) / n + H · w) · gn = ((500 + 480) / 4 + 16.7 × 0.34) × 9.81 = ")&&rt.Contains("Sf = MBL / Tmax = 44000 / ")&&rt.Contains("Kp = (Dt / Dp)^4 = (520 / 400)^4 = 2.8561")&&rt.Contains("Sf required = max(12; "),"Sf equations with substitution");
        Check(rt.Contains("Rope safety factor")&&rt.Contains("EN 81-20 5.5.2.2; EN 81-50 5.12")&&!rt.Contains("Deflector D/d")&&rt.Contains("Traction sheave D/d")&&rt.Contains("≥ 40")&&rt.Contains("Ftermination ≥ 0.8 × MBL = 0.8 × 44 = 35.2 kN"),"Rope checks table: check, clause, required, actual, result; termination requirement");
        Check(rt.Contains("Rope termination strength\nEN 81-20 5.5.2.3\nFtermination ≥ 0.8 × MBL\n0.8 × MBL = 35.2 kN\nMBL = 44 kN\nRequired strength"),"Termination row: required 0.8 × MBL = 35.2 kN, actual MBL 44 kN, result Required strength (no empty cells)");
        Check(rt.Contains("μ = 0.1 / (1 + v/10) = 0.1 / (1 + 1/10) = 0.0909")&&rt.Contains("f = μ · 4 (cos(γ/2) − sin(β/2)) / (π − β − γ − sin β + sin γ) = ")&&rt.Contains("e^(f·α) = e^(")&&rt.Contains("Empty car, highest landing, retarding upwards")&&rt.Contains("mDP · a"),"Traction equations: μ, f, e^(fα), T1/T2 per position including mDP");
        Check(rt.Contains("Reduced mass of the deflector sheave (m/2)\nmDP\n22.5 kg")&&rt.Contains("Angle of wrap on the traction sheave\nα\n152°")&&rt.Contains("Suspended rope length (travel + overhead)\nH\n16.7 m"),"Geometry table: mDP shown, α from the reeving path, H = travel + overhead");
        var braking=r.Traction.Single(t=>t.Id=="braking");
        Check(!braking.Ok&&rt.Contains("NOT OK")&&sheet.NotOk.Any(x=>x.StartsWith("Traction, emergency braking"))&&r.Traction.Single(t=>t.Id=="loading").Ok,"NOT OK braking result shown in the sheet and listed for the export dialog");
        var summary=Section(text,H(sheet,Summary),H(sheet,Inputs));
        Check(Regex.IsMatch(summary,@"Suspension rope and traction checks\nRope Sf \d+(\.\d+)? ≥ \d+(\.\d+)? OK; D/d OK; traction NOT OK"),"Summary row: Rope Sf ≥ required OK; D/d OK; traction NOT OK");
        var inputs=Section(text,H(sheet,Inputs),H(sheet,Reactions));
        Check(inputs.Contains("Rope designation\n8×19 Seale FC 1370/1770")&&inputs.Contains("Rope minimum breaking load MBL\n44 kN")&&inputs.Contains("Rope mass per metre w\n0.34 kg/m")&&inputs.Contains("Traction sheave groove\nSemi-circular / undercut")&&inputs.Contains("Undercut angle β\n95°")&&inputs.Contains("Groove angle γ\n30°")&&inputs.Contains("Travelling cable mass per metre\n0.5 kg/m")&&inputs.Contains("Emergency braking retardation a\n0.5 m/s²")&&inputs.Contains("Deflector sheave mass\n45 kg")&&inputs.Contains("Traction limiting method\nRopes slip"),"New rope / traction inputs listed in the inputs table");
        foreach(var w in new[]{"preset","assumed","review","not from catalog","Table 6","remark"})Check(!text.ToLowerInvariant().Contains(w.ToLowerInvariant()),"Calculation sheet stays clean of '"+w+"'");
        var f10=Svgs(sheet)["F10"];
        Check(Value(f10,"dim-alpha")=="152"&&Tagged(f10,"dim-alpha-text").InnerText=="α 152°"&&Value(f10,"groove-beta")=="95"&&Value(f10,"groove-gamma")=="30"&&Tagged(f10,"rope-t1")!=null&&Tagged(f10,"rope-t2")!=null&&Value(f10,"dt")=="520"&&Value(f10,"groove-type")==GrooveTypes.UndercutU,"F10: α 152° dimensioned from the geometry, β 95° / γ 30° from the inputs, T1 / T2 arrows, Dt 520");
        // 15. NOT OK rope data: 8 mm ropes on the 250 mm deflector (1500 × 1600 → TZ 750).
        p=BasicComplete("Test Tower N — rope NOT OK");p.Shaft.DepthMm=1600;
        sheet=Export(p,"15-rope-not-ok.docx",null,out text);
        Check(!sheet.NotOk.Any(n=>n.StartsWith("Deflector D/d"))&&!Section(text,H(sheet,RopeChecks),H(sheet,Pit)).Contains("Deflector D/d")&&Section(text,H(sheet,RopeChecks),H(sheet,Pit)).Contains("Deflector sheave pitch diameter")&&Section(text,H(sheet,RopeChecks),H(sheet,Pit)).Contains("NOT OK"),"No deflector D/d row or NOT OK item in the sheet; the deflector pitch diameter is shown as a value (DESIGN-FLOW 11a)");
        Check(Section(text,H(sheet,Summary),H(sheet,Inputs)).Contains("D/d OK"),"Summary row: the sheave D/d is OK; the deflector D/d is not a check (DESIGN-FLOW 11a)");
        Check(Section(text,H(sheet,RopeChecks),H(sheet,Pit)).Contains("16.7 × 0.218) × 9.81")&&Section(text,H(sheet,Inputs),H(sheet,Reactions)).Contains("Rope mass per metre w\n0.218 kg/m"),"Rope mass shown with its full value (0.218 kg/m) in the substitution and inputs");
        // 16. Design Notes (short advisory notes) for the 480 kg / 1100 × 2100 fixture: Table 6 with the minimum rated load,
        // and the invalid deflector layout (TZ 1200) with its valid range.
        p=BasicComplete("Test Tower O — design notes");
        var notes=DesignNotes.Build(p,ctx,new CalculationSheetInputs());
        Check(notes.Warnings.Contains("Car available area 2.31 m² exceeds the maximum 1.36 m² for 480 kg (EN 81-20 Table 6). Use a rated load of at least 955 kg, or reduce the car to ≤ 1.36 m²."),"Design Notes: Table 6 warning with the minimum rated load 955 kg for 1100 × 2100 (Table 6 interpolation)");
        Check(CarAreaRules.MinimumRatedLoadKg(2.31)==955&&CarAreaRules.MaximumAvailableAreaM2(955).Value>=2.31-1e-9&&CarAreaRules.MaximumAvailableAreaM2(954).Value<2.31,"Table 6 inverse: 955 kg is the smallest rated load allowing 2.31 m²");
        Check(notes.Warnings.Any(w=>w.Contains("TZ 1200 mm is outside the deflector selection range 520–1100 mm")&&w.Contains("Keep TZ at 1100 mm or less")),"Design Notes: invalid layout stated once with the cause and the valid TZ range");
        Check(notes.Warnings.Count==2&&notes.Recommendations.Count==0,"Design Notes hold only what needs saying (2 warnings, no recommendations)");
        string notesPath=Path.Combine(output,"16-design-notes.docx");Check(!File.Exists(notesPath),"Fresh design notes path");notes.Save(notesPath);
        string props;var nt=Text(notesPath,out props);
        DesignNotesClean(notesPath,nt,"16");
        Check(nt.Contains("Warnings\n")&&!nt.Contains("Recommendations"),"Only the headings that have notes are shown");
        // 17. Complete rope fixture: emergency braking NOT OK with its e^(fα) margin and the wrap angle / friction it needs; Table 6 735 kg.
        p=BasicComplete("Test Tower P — design notes ropes");p.Shaft.DepthMm=2100;p.Suspension.RopeDiameterMm=10;p.Suspension.RopeCount=4;
        notes=DesignNotes.Build(p,ctx,new CalculationSheetInputs());
        Check(notes.Warnings.Any(w=>w.StartsWith("Emergency braking: T1/T2 1.681 exceeds e^(fα) 1.609 by 4.5 % with the empty car, highest landing, retarding upwards (EN 81-50 5.11.2.2.2).")&&w.Contains("wrap angle of at least")&&w.Contains("or a groove giving f ≥ ")),"Design Notes: failing traction case with its e^(fα) margin and the computed remedy");
        Check(notes.Warnings.Any(w=>w.Contains("1.87 m²")&&w.Contains("Use a rated load of at least 735 kg")),"Design Notes: Table 6 for 1100 × 1700 (1.87 m²) recommends at least 735 kg");
        Check(!notes.Warnings.Any(w=>w.StartsWith("Rope safety factor")||w.Contains("D/d")),"No note about checks that pass (Sf, D/d)");
        notesPath=Path.Combine(output,"17-design-notes-ropes.docx");notes.Save(notesPath);DesignNotesClean(notesPath,Text(notesPath,out props),"17");
        // 18. Failing rope safety factor (4 × 8 mm, MBL locked at 20 kN): rope upsize computed with the same engine.
        p=BasicComplete("Test Tower Q — rope upsize");p.Shaft.DepthMm=2100;p.Suspension.RopeCount=4;PresetFields.MinimumBreakingLoad.Edit(p,20.0);
        notes=DesignNotes.Build(p,ctx,new CalculationSheetInputs());
        var engine=new RopeTractionCalculationEngine();var presets=CalculationSheet.LoadPresets(bin);
        int expected=0;for(int c=5;c<=7&&expected==0;c++){var q=ProjectJson.Clone(p);q.Suspension.RopeCount=c;var k=engine.Calculate(q,ctx,presets).Checks.First(z=>z.Id=="safety-factor");if(k.Ok==true)expected=c;}
        var d10=ProjectJson.Clone(p);d10.Suspension.RopeDiameterMm=10;PresetFields.MinimumBreakingLoad.Reset(d10);var sf10=engine.Calculate(d10,ctx,presets).Checks.First(z=>z.Id=="safety-factor");
        var sfNote=notes.Warnings.FirstOrDefault(w=>w.StartsWith("Rope safety factor "));
        Check(expected>0&&sf10.Ok==true&&sfNote!=null&&sfNote.Contains("Use "+expected+" ropes (Sf ")&&sfNote.Contains("or 10 mm ropes with 44 kN MBL (Sf "+sf10.Actual+")"),"Design Notes: Sf NOT OK recommends the smallest rope count ("+expected+") and the next rope diameter (10 mm, Sf "+sf10.Actual+") from the same engine");
        notesPath=Path.Combine(output,"18-design-notes-rope-upsize.docx");notes.Save(notesPath);DesignNotesClean(notesPath,Text(notesPath,out props),"18");
        // 19. Clean project (direct roping, 1100 × 700 car, all checks OK): a single line.
        p=BasicComplete("Test Tower R — clean design notes");p.Shaft.DepthMm=1100;p.Suspension.RopeDiameterMm=10;p.Suspension.RopeCount=4;
        notes=DesignNotes.Build(p,ctx,new CalculationSheetInputs());
        Check(notes.Empty&&notes.RopeTraction.Traction.Count==3&&notes.RopeTraction.Traction.All(t=>t.Ok)&&notes.RopeTraction.Checks.All(c=>c.Ok!=false),"Clean project: all checks OK, no design notes");
        notesPath=Path.Combine(output,"19-design-notes-clean.docx");notes.Save(notesPath);nt=Text(notesPath,out props);
        Check(Body(notesPath).Trim()==DesignNotes.NoNotes,"Clean project exports a single line: No design notes for this project.");
    }
    // ---------- figures ----------
    static Dictionary<string,XmlDocument> Svgs(CalculationSheet sheet)
    {
        var map=new Dictionary<string,XmlDocument>();
        foreach(var f in sheet.Figures){var x=new XmlDocument();x.LoadXml(FigureRender.Svg(f));map[f.Id]=x;}
        return map;
    }
    static int Count(XmlDocument svg,string prefix){return svg.SelectNodes("//*[@data-tag]").Cast<XmlElement>().Count(e=>Regex.IsMatch(e.GetAttribute("data-tag"),"^"+Regex.Escape(prefix)+@"\d+$"));}
    // Moment balance from the drawn arrows, about the car centre: load moment + shoe couple = 0 (forces on the car are opposite to the
    // drawn rail forces). M = Σ z·dir·F − Q·10·e, with z = ±h/2 (upper / lower level) and e the drawn load offset. Run for both signs of
    // each load offset; a reversed arrow makes the residual 2·F·h or more.
    static void MomentBalance(CalculationSheet sheet,string file)
    {
        var ff=sheet.CarGuideForces;double Q=ff.CapacityKg.Value,h=ff.GuideShoeSpacingMm.Value,W=ff.InternalWidthMm.Value,D=ff.InternalDepthMm.Value,F1=ff.FF1BaseN.Value,F2=ff.FF2BaseN.Value;
        Func<double,bool> zero=m=>Math.Abs(m)<1e-6*Q*10*W;
        foreach(int s1 in new[]{1,-1})foreach(int s2 in new[]{1,-1})
        {
            string c="(load "+(s1>0?"+X":"−X")+", "+(s2>0?"+Y":"−Y")+") "+file;
            var f4=CalculationFigures.GuideForcePlan(sheet.Plan,ff,sheet.CarRail,s1,s2);var f5=CalculationFigures.GuideForceElevation(ff,s1,s2);
            Func<Figure,string,FigurePolyline> line=(f,t)=>f.Entities.OfType<FigurePolyline>().Single(e=>e.Tag==t);
            Func<Figure,string,FigurePoint> centre=(f,t)=>{var e=f.Entities.Single(q=>q.Tag==t);var circle=e as FigureCircle;if(circle!=null)return circle.Centre;var pts=((FigurePolyline)e).Points;return new FigurePoint(pts.Average(q=>q.X),pts.Average(q=>q.Y));};
            Func<Figure,string,string> text=(f,t)=>f.Texts.Single(q=>q.Tag==t).Content;
            // F4 FF1 (elevation plane x–z): offset from the hatched-half resultant, levels from the labels.
            double ex=Math.Sign(centre(f4,"resultant-ff1").X-centre(f4,"car-ff1").X)*W/4,m1=-Q*10*ex;
            foreach(var side in new[]{"left","right"}){var l=line(f4,"force-ff1-"+side);double dir=Math.Sign(l.Points[1].X-l.Points[0].X),z=text(f4,"level-ff1-"+side)=="upper level"?h/2:-h/2;m1+=z*dir*F1;}
            Check(zero(m1),"F4 FF1 moment balance "+c+": residual "+Figure.Num(m1,"0.###")+" N·mm");
            // F4 FF2 (plane y–z): both rails at the upper level, reversed at the lower level (stated by the reversal note).
            double ey=Math.Sign(centre(f4,"car-ff2").Y-centre(f4,"resultant-ff2").Y)*D/4,m2=-Q*10*ey;bool reverse=f4.Texts.Any(t=>t.Tag=="ff2-reverse-note");
            foreach(var side in new[]{"left","right"}){var l=line(f4,"force-ff2-"+side);double dir=-Math.Sign(l.Points[1].Y-l.Points[0].Y),z=text(f4,"level-ff2-"+side)=="upper level"?h/2:-h/2;m2+=z*dir*F2;if(reverse)m2+=(-z)*(-dir)*F2;}
            Check(zero(m2),"F4 FF2 moment balance "+c+": residual "+Figure.Num(m2,"0.###")+" N·mm");
            // F5a FF1: offset from the drawn load arrow, levels from the drawn heights.
            double cx=(line(f5,"rail-left").Points.Max(q=>q.X)+line(f5,"rail-right").Points.Min(q=>q.X))/2,ex5=Math.Sign(line(f5,"load-q-ff1").Points[0].X-cx)*W/4,m3=-Q*10*ex5;
            foreach(var t in new[]{"force-ff1-upper","force-ff1-lower"}){var l=line(f5,t);double dir=Math.Sign(l.Points[1].X-l.Points[0].X),z=Math.Abs(l.Points[0].Y-CalculationFigures.F5UpperY)<1e-6?h/2:-h/2;m3+=z*dir*F1;}
            Check(zero(m3)&&Math.Sign(ex5)==Math.Sign(ex),"F5a FF1 moment balance, same load side as F4 "+c+": residual "+Figure.Num(m3,"0.###")+" N·mm");
            // F5b FF2: one rail drawn; both car rails carry the same couple. Offset side from F4.
            double m4=-Q*10*ey;
            foreach(var t in new[]{"force-ff2-upper","force-ff2-lower"}){var l=line(f5,t);double dir=Math.Sign(l.Points[1].X-l.Points[0].X),z=Math.Abs(l.Points[0].Y-CalculationFigures.F5UpperY)<1e-6?h/2:-h/2;m4+=2*z*dir*F2;}
            Check(zero(m4),"F5b FF2 moment balance (two rails) "+c+": residual "+Figure.Num(m4,"0.###")+" N·mm");
            // The check is sensitive: reversing one drawn FF1 arrow breaks the balance.
            Check(!zero(m1-2*(h/2)*F1)&&!zero(m3-2*(h/2)*F1),"Moment check detects a reversed FF1 arrow "+c);
        }
    }
    static XmlElement Tagged(XmlDocument svg,string tag){return svg.SelectSingleNode("//*[@data-tag='"+tag+"']") as XmlElement;}
    static string Value(XmlDocument svg,string tag){var e=Tagged(svg,tag);return e==null?null:e.GetAttribute("data-value");}
    static List<FigurePoint> SvgPoints(XmlElement e)
    {
        double k=FigureRender.SvgUnitsPerMm;
        return e.GetAttribute("points").Split(' ').Select(t=>t.Split(',')).Select(a=>new FigurePoint(Double.Parse(a[0],CultureInfo.InvariantCulture)/k,Double.Parse(a[1],CultureInfo.InvariantCulture)/k)).ToList();
    }
    static void FigureChecks(CalculationSheet sheet,string file,string[] ids,string preview)
    {
        // F10 (traction sheave) belongs to the rope / traction section, placed before the P5–P7 and FF figures.
        var rt=sheet.RopeTraction;
        if(sheet.RopeTractionAvailable&&rt.Inputs.GrooveType!=null&&(rt.Traction.Count>0||rt.TmaxN.HasValue)&&!ids.Contains("F10"))
        {var l=ids.ToList();int at=l.FindIndex(i=>i=="F8"||i=="F9"||i=="F4"||i=="F5"||i=="F6");if(at<0)at=l.Count;l.Insert(at,"F10");ids=l.ToArray();}
        Check(sheet.FigureErrors.Count==0,"No figure builder errors: "+file+(sheet.FigureErrors.Count>0?" "+String.Join("; ",sheet.FigureErrors):""));
        Check(sheet.Figures.Select(f=>f.Id).SequenceEqual(ids),"Figures present with their sections "+String.Join(",",sheet.Figures.Select(f=>f.Id))+": "+file);
        var ns=Ns();
        using(var package=Package.Open(Path.Combine(output,file),FileMode.Open,FileAccess.Read))
        {
            var media=package.GetParts().Where(x=>x.Uri.OriginalString.StartsWith("/word/media/")).ToList();
            Check(media.Count(x=>x.ContentType=="image/svg+xml")==ids.Length&&media.Count(x=>x.ContentType=="image/png")>=ids.Length,"Each figure embedded as SVG with a PNG fallback: "+file);
            var doc=Part(package,"/word/document.xml",ns);
            var captions=doc.SelectNodes("//w:body/w:tbl[.//asvg:svgBlip]/w:tr[2]/w:tc[1]",ns).Cast<XmlNode>().Select(c=>Concat(c,ns)).ToList();
            Check(captions.SequenceEqual(sheet.Figures.Select((f,i)=>"Figure "+(i+1)+" — "+f.Title)),"Figures numbered in order of appearance: "+file);
            var strips=doc.SelectNodes("//w:body/w:tbl[.//asvg:svgBlip]/w:tr[2]",ns).Cast<XmlNode>().Select(r=>Concat(r,ns)).ToList();
            for(int i=0;i<strips.Count;i++)Check(strips[i].Contains("Dimensions in mm")&&(strips[i].EndsWith("NTS")==sheet.Figures[i].Schematic),"Title strip units and NTS (schematic only): "+sheet.Figures[i].Id+" "+file);
        }
        foreach(var f in sheet.Figures)
        {
            var texts=f.Texts.ToList();
            for(int i=0;i<texts.Count;i++)
            {
                double a0,b0,a1,b1;texts[i].Box(out a0,out b0,out a1,out b1);
                Check(a0>=-0.01&&b0>=-0.01&&a1<=f.WidthMm+0.01&&b1<=f.HeightMm+0.01,"Text inside the figure frame '"+texts[i].Content+"': "+f.Id+" "+file);
                for(int j=i+1;j<texts.Count;j++){double c0,d0,c1,d1;texts[j].Box(out c0,out d0,out c1,out d1);Check(!(a0<c1&&c0<a1&&b0<d1&&d0<b1),"No overlapping text '"+texts[i].Content+"' / '"+texts[j].Content+"': "+f.Id+" "+file);}
            }
            var pts=f.Entities.OfType<FigurePolyline>().SelectMany(e=>e.Points).Concat(f.Entities.OfType<FigureCircle>().Select(c=>c.Centre)).ToList();
            Check(pts.All(q=>q.X>=-0.01&&q.Y>=-0.01&&q.X<=f.WidthMm+0.01&&q.Y<=f.HeightMm+0.01),"Geometry inside the figure frame: "+f.Id+" "+file);
            Check(f.WidthMm<=165.01,"Figure width ≤ 165 mm: "+f.Id);
            Check(f.Texts.All(t=>t.HeightMm==2.5||(t.Title&&t.HeightMm==3.5)),"Text 2.5 mm, titles 3.5 mm: "+f.Id+" "+file);
            Check(f.Texts.All(t=>!Regex.IsMatch(t.Content,@"\p{L}-\p{L}")),"No U+002D hyphen between letters in figure text: "+f.Id+" "+file);
            if(preview!=null){Directory.CreateDirectory(Path.Combine(output,"previews"));File.WriteAllBytes(Path.Combine(output,"previews",preview+"-"+f.Id+".png"),FigureRender.Png(f));File.WriteAllText(Path.Combine(output,"previews",preview+"-"+f.Id+".svg"),FigureRender.Svg(f));}
        }
        var svg=Svgs(sheet);var ff=sheet.CarGuideForces;
        if(svg.ContainsKey("F1"))
        {
            var pl=sheet.Plan;var x=svg["F1"];
            Check(Value(x,"dim-SW")==Figure.Mm(pl.SW)&&Value(x,"dim-SD")==Figure.Mm(pl.SD)&&Value(x,"dim-W")==Figure.Mm(pl.CarWidth)&&Value(x,"dim-D")==Figure.Mm(pl.CarDepth),"F1 dimensions SW, SD, W, D match the plan geometry: "+file);
            var view=sheet.Figures.Single(f=>f.Id=="F1").View;
            Check(Math.Abs(SvgPoints(Tagged(x,"car-rail-left")).Max(q=>q.X)-view.P(pl.X("railLeft"),pl.CarRow).X)<0.01&&Math.Abs(SvgPoints(Tagged(x,"car-rail-right")).Min(q=>q.X)-view.P(pl.X("railRight"),pl.CarRow).X)<0.01,"F1 car rail blade tips at railLeft / railRight: "+file);
            Check(Math.Abs(SvgPoints(Tagged(x,"car-rail-line"))[0].Y-view.P(0,pl.CarRow).Y)<0.01&&Tagged(x,"cw-rail-left")!=null&&Tagged(x,"counterweight")!=null,"F1 car rail line at CarRow; counterweight and its rails drawn: "+file);
            Check(new[]{"car-rail-left","car-rail-right","cw-rail-left","cw-rail-right"}.All(t=>SvgPoints(Tagged(x,t)).Count==10),"F1 car and CW rails drawn as T-profile symbols: "+file);
            Check(Math.Abs(SvgPoints(Tagged(x,"cw-rail-left")).Max(q=>q.X)-view.P(pl.X("cwRailLeft"),pl.CwRow).X)<0.01&&Math.Abs(SvgPoints(Tagged(x,"cw-rail-right")).Min(q=>q.X)-view.P(pl.X("cwRailRight"),pl.CwRow).X)<0.01,"F1 CW rail blade tips at cwRailLeft / cwRailRight, facing the counterweight: "+file);
            Check(Value(x,"dim-DBG")==Figure.Mm(pl.CarDbg)&&Value(x,"dim-CWDBG")==Figure.Mm(pl.CwDbg)&&Tagged(x,"dim-DBG-text").InnerText=="car DBG "+Figure.Mm(pl.CarDbg)&&Tagged(x,"dim-CWDBG-text").InnerText=="CW DBG "+Figure.Mm(pl.CwDbg),"F1 car DBG = CarDbg and CW DBG = CwDbg: "+file);
            Check(Tagged(x,"dim-W-text").InnerText=="W "+Figure.Mm(pl.CarWidth)&&Tagged(x,"dim-D-text").InnerText=="D "+Figure.Mm(pl.CarDepth),"F1 car dimensions labelled W and D: "+file);
            Check(Tagged(x,"car-rail-label").InnerText=="car rail "+sheet.CarRail.Designation&&Tagged(x,"cw-rail-label").InnerText=="CW rail "+sheet.CounterweightRail.Designation&&Tagged(x,"car-rail-label-leader")!=null&&Tagged(x,"cw-rail-label-leader")!=null,"F1 leader labels car rail / CW rail designations: "+file);
            foreach(var rr in new[]{new{Tag="car-rail-right",Rail=sheet.CarRail},new{Tag="cw-rail-right",Rail=sheet.CounterweightRail}})
            {
                var q=SvgPoints(Tagged(x,rr.Tag));double ext=Math.Max(q.Max(a=>a.X)-q.Min(a=>a.X),q.Max(a=>a.Y)-q.Min(a=>a.Y)),trueSize=Math.Max(rr.Rail.BaseWidthMm,rr.Rail.HeightMm)*view.Scale;
                Check(ext>=CalculationFigures.MinRailSymbolMm-0.01&&Math.Abs(ext-Math.Max(trueSize,CalculationFigures.MinRailSymbolMm))<0.01,"F1 "+rr.Tag+" at true scale ("+Figure.Num(trueSize,"0.00")+" mm) with a 3 mm minimum: "+file);
            }
            Check(Tagged(x,"entrance")!=null&&Value(x,"entrance")==Figure.Mm(pl.DoorWidth)&&Value(x,"door-jamb-left")==Figure.Mm(pl.X("doorLeft"))&&Value(x,"door-jamb-right")==Figure.Mm(pl.X("doorRight")),"F1 entrance opening from the plan door width: "+file);
        }
        if(svg.ContainsKey("F2"))foreach(var r in sheet.Chassis.Reactions)Check(Value(svg["F2"],"reaction-"+r.Id)==Figure.Num(r.UpwardN/1000,"0.###")&&Tagged(svg["F2"],"reaction-"+r.Id).InnerText==r.Id+" "+Figure.Kn(r.UpwardN),"F2 label "+r.Id+" = "+Figure.Kn(r.UpwardN)+": "+file);
        if(svg.ContainsKey("F2")&&sheet.Mode=="Basic"){var g=sheet.Chassis.SimplifiedGeometry;Check(Value(svg["F2"],"dim-L")==Figure.Mm(g.LongitudinalSpanMm)&&Value(svg["F2"],"dim-a")==Figure.Mm(g.LoadFromFrontMm)&&Value(svg["F2"],"dim-b")==Figure.Mm(g.LoadToRearMm)&&Value(svg["F2"],"dim-sf")==Figure.Mm(g.FrontTransverseSpanMm)&&Value(svg["F2"],"dim-sr")==Figure.Mm(g.RearTransverseSpanMm),"F2 dimensions L, a, b, s(f), s(r): "+file);}
        if(svg.ContainsKey("F3")){var g=sheet.Chassis.SimplifiedGeometry;Check(Value(svg["F3"],"reaction-front")==Figure.Num(g.FrontLoadN/1000,"0.###")&&Value(svg["F3"],"reaction-rear")==Figure.Num(g.RearLoadN/1000,"0.###")&&Value(svg["F3"],"load-W")==Figure.Num(sheet.Chassis.TotalVerticalLoadN.Value/1000,"0.###"),"F3 W and row reactions: "+file);}
        if(svg.ContainsKey("F4"))
        {
            var x=svg["F4"];string f1=Figure.Num(ff.FF1DesignN.Value/1000,"0.###"),f2=Figure.Num(ff.FF2DesignN.Value/1000,"0.###");
            Check(new[]{"force-ff1-left","force-ff1-right"}.All(t=>Value(x,t)==f1)&&new[]{"force-ff2-left","force-ff2-right"}.All(t=>Value(x,t)==f2),"F4 FF1 / FF2 values at each rail ("+f1+" / "+f2+" kN): "+file);
            Check(Tagged(x,"level-ff1-right").InnerText=="upper level"&&Tagged(x,"level-ff1-left").InnerText=="lower level"&&Tagged(x,"level-ff2-left").InnerText=="upper level"&&Tagged(x,"level-ff2-right").InnerText=="upper level"&&Tagged(x,"ff2-reverse-note")!=null,"F4 level labels: FF1 upper / lower on opposite rails, FF2 both upper with reversal note: "+file);
            Check(Tagged(x,"ff-note").InnerText=="FF1 / FF2: force per guide rail at one guide shoe level"&&Tagged(x,"ff-note-2").InnerText=="Directions reverse between the upper and lower shoe levels","F4 note text: "+file);
            Check(Value(x,"dim-W")==Figure.Mm(ff.InternalWidthMm.Value)&&Tagged(x,"car-ff1").GetAttribute("data-value")==Figure.Mm(ff.InternalWidthMm.Value)+","+Figure.Mm(ff.InternalDepthMm.Value)&&Value(x,"dim-W4")==Figure.Mm(ff.InternalWidthMm.Value/4)&&Value(x,"dim-D4")==Figure.Mm(ff.InternalDepthMm.Value/4),"F4 W, D and resultant offsets W/4, D/4: "+file);
        }
        if(svg.ContainsKey("F5"))
        {
            var x=svg["F5"];var f5=sheet.Figures.Single(q=>q.Id=="F5");
            Check(Value(x,"dim-h")==Figure.Mm(ff.GuideShoeSpacingMm.Value)&&Value(x,"dim-h-side")==Figure.Mm(ff.GuideShoeSpacingMm.Value),"F5 h dimensioned in both views: "+file);
            Check(Value(x,"force-ff1-upper")==Figure.Num(ff.FF1DesignN.Value/1000,"0.###")&&Value(x,"force-ff1-lower")==Figure.Num(ff.FF1DesignN.Value/1000,"0.###")&&Value(x,"force-ff2-upper")==Figure.Num(ff.FF2DesignN.Value/1000,"0.###")&&Value(x,"force-ff2-lower")==Figure.Num(ff.FF2DesignN.Value/1000,"0.###"),"F5 FF1 / FF2 values: "+file);
            Check(f5.Texts.Any(t=>t.Content=="a) Front elevation")&&f5.Texts.Any(t=>t.Content=="b) Side view of one rail")&&f5.Texts.Any(t=>t.Content=="upper shoe level")&&f5.Texts.Any(t=>t.Content=="lower shoe level"),"F5 has two views and level labels: "+file);
            Func<string,FigurePolyline> line=t=>f5.Entities.OfType<FigurePolyline>().Single(e=>e.Tag==t);
            Func<string,FigurePoint> tipOf=t=>{var l=line(t);var a=l.Points[0];var b=l.Points[1];return b+(b-a).Unit*2.0;};
            Func<string,FigurePoint> dirOf=t=>{var l=line(t);return (l.Points[1]-l.Points[0]).Unit;};
            double leftFace=line("rail-left").Points.Max(q=>q.X),rightFace=line("rail-right").Points.Min(q=>q.X),frontFace=line("rail-side").Points.Min(q=>q.X),rearFace=line("rail-side").Points.Max(q=>q.X),split=CalculationFigures.F5ViewSplitX;
            var u1=tipOf("force-ff1-upper");var l1=tipOf("force-ff1-lower");
            Check(Math.Abs(u1.X-rightFace)<0.05&&Math.Abs(u1.Y-CalculationFigures.F5UpperY)<0.05&&dirOf("force-ff1-upper").X>0.99,"F5 FF1 upper level: on the loaded-side (right) rail, pointing outward: "+file);
            Check(Math.Abs(l1.X-leftFace)<0.05&&Math.Abs(l1.Y-CalculationFigures.F5LowerY)<0.05&&dirOf("force-ff1-lower").X<-0.99,"F5 FF1 lower level: on the opposite (left) rail, pointing outward: "+file);
            Check(line("force-ff1-upper").Points.All(q=>q.X<split)&&line("force-ff1-lower").Points.All(q=>q.X<split)&&line("force-ff2-upper").Points.All(q=>q.X>split)&&line("force-ff2-lower").Points.All(q=>q.X>split),"F5 FF1 arrows in the front elevation only, FF2 arrows in the side view only: "+file);
            var u2=tipOf("force-ff2-upper");var l2=tipOf("force-ff2-lower");
            Check(Math.Abs(u2.X-frontFace)<0.05&&dirOf("force-ff2-upper").X>0.99&&Math.Abs(l2.X-rearFace)<0.05&&dirOf("force-ff2-lower").X<-0.99&&Math.Abs(u2.Y-CalculationFigures.F5UpperY)<0.05&&Math.Abs(l2.Y-CalculationFigures.F5LowerY)<0.05,"F5 FF2 couple on blade side faces: upper towards the rear, lower towards the front: "+file);
            // No arrow points from a shoe away from a rail: every force ends on a rail face with its tail outside that rail.
            foreach(var t in new[]{"force-ff1-upper","force-ff1-lower","force-ff2-upper","force-ff2-lower"})
            {
                var tip=tipOf(t);var tail=line(t).Points[0];
                bool onLeft=Math.Abs(tip.X-leftFace)<0.05&&tail.X>leftFace,onRight=Math.Abs(tip.X-rightFace)<0.05&&tail.X<rightFace,onFront=Math.Abs(tip.X-frontFace)<0.05&&tail.X<frontFace,onRear=Math.Abs(tip.X-rearFace)<0.05&&tail.X>rearFace;
                Check(onLeft||onRight||onFront||onRear,"F5 "+t+" pushes onto a rail face (never away from a rail): "+file);
            }
        }
        if(svg.ContainsKey("F6"))Check(Value(svg["F6"],"dim-b1")=="70"&&Value(svg["F6"],"dim-h1")=="65"&&Value(svg["F6"],"dim-k-text")=="9"&&Value(svg["F6"],"designation")=="T70/65/9"&&Value(svg["F6"],"force-fy")==Figure.Num(ff.FF1DesignN.Value/1000,"0.###")&&Value(svg["F6"],"force-fx")==Figure.Num(ff.FF2DesignN.Value/1000,"0.###"),"F6 b1, h1, k and Fy / Fx values: "+file);
        var proj=sheet.Project;
        if(svg.ContainsKey("F7"))
        {
            var x=svg["F7"];var c=sheet.Chassis;int? n=proj.Suspension.RopeCount;
            Check(Value(x,"tension-car")==Figure.Num(c.CarBundleTensionN.Value/1000,"0.###")&&Tagged(x,"tension-car-text").InnerText=="T(car) "+Figure.Kn(c.CarBundleTensionN.Value)&&Value(x,"tension-cwt")==Figure.Num(c.CounterweightBundleTensionN.Value/1000,"0.###")&&Tagged(x,"tension-cwt-text").InnerText=="T(cwt) "+Figure.Kn(c.CounterweightBundleTensionN.Value),"F7 T(car) / T(cwt) labels match the rope tensions: "+file);
            if(n.HasValue&&n.Value>0)Check(Value(x,"tension-car-rope")==Figure.Num(c.CarPerRopeTensionN.Value/1000,"0.###")&&Value(x,"tension-cwt-rope")==Figure.Num(c.CounterweightPerRopeTensionN.Value/1000,"0.###")&&Tagged(x,"roping").InnerText=="n = "+n.Value+" ropes, "+Figure.Num(proj.Suspension.RopingRatio.Value,"0.##")+":1","F7 per-rope values and roping label from the project: "+file);
            bool deflector=MachineRoomRules.SelectDeflector(sheet.Plan.TZ).IsRequired;
            Check((Tagged(x,"deflector-sheave")!=null)==deflector&&Tagged(x,"traction-sheave")!=null&&Tagged(x,"car-hitch")!=null&&Tagged(x,"cw-hitch")!=null,"F7 deflector drawn only when the project rule selects one (TZ "+Figure.Mm(sheet.Plan.TZ)+"): "+file);
            double? pitch=proj.Machine==null?null:proj.Machine.TractionSheavePitchDiameterMm;
            Check(pitch.HasValue?Value(x,"sheave-pitch")==Figure.Mm(pitch.Value):Tagged(x,"sheave-pitch")==null,"F7 sheave pitch diameter label only when set: "+file);
        }
        if(svg.ContainsKey("F8"))
        {
            var x=svg["F8"];var l=sheet.SafetyPit.P5;int n=l.ComponentCount.Value;string v=Figure.Num(l.PerComponentLoadN.Value/1000,"0.###");
            Check(Enumerable.Range(1,n).All(i=>Tagged(x,"rail-"+i)!=null&&Tagged(x,"safety-gear-"+i)!=null&&Value(x,"force-p5-"+i)==v&&Tagged(x,"force-p5-"+i+"-text").InnerText=="P5 "+Figure.Kn(l.PerComponentLoadN.Value))&&Tagged(x,"rail-"+(n+1))==null,"F8 one rail, safety gear and P5 arrow per car guide rail ("+n+" × "+v+" kN): "+file);
            Check(Tagged(x,"p5-per-rail").InnerText=="per car guide rail"&&Value(x,"rail-count")==n.ToString(CultureInfo.InvariantCulture)&&Tagged(x,"jaw-left")!=null&&Tagged(x,"jaw-right")!=null,"F8 per-rail note, rail count and jaw plan detail: "+file);
            var f8=sheet.Figures.Single(q=>q.Id=="F8");
            foreach(var a in f8.Entities.OfType<FigurePolyline>().Where(e=>e.Tag!=null&&e.Tag.StartsWith("force-p5-")))Check(a.Points[1].Y>a.Points[0].Y&&Math.Abs(a.Points[1].X-a.Points[0].X)<1e-9,"F8 "+a.Tag+" points vertically down: "+file);
        }
        if(svg.ContainsKey("F9"))
        {
            var x=svg["F9"];
            foreach(var g in new[]{new{Load=sheet.SafetyPit.P6,Tag="car"},new{Load=sheet.SafetyPit.P7,Tag="cw"}})
            {
                if(!g.Load.PerComponentLoadN.HasValue){Check(Tagged(x,g.Tag+"-buffer-1")==null,"F9 no "+g.Tag+" buffers when unavailable: "+file);continue;}
                int n=g.Load.ComponentCount.Value;string per=Figure.Num(g.Load.PerComponentLoadN.Value/1000,"0.###");
                Check(Count(x,g.Tag+"-buffer-")==n&&Count(x,g.Tag+"-force-")==n&&Enumerable.Range(1,n).All(i=>Value(x,g.Tag+"-force-"+i)==per)&&Value(x,g.Tag+"-total")==Figure.Num(g.Load.GroupLoadN.Value/1000,"0.###")&&Tagged(x,g.Tag+"-total").InnerText.StartsWith(g.Load.Id+" total "+Figure.Kn(g.Load.GroupLoadN.Value)),"F9 "+g.Load.Id+": "+n+" buffers, "+per+" kN each, total labelled: "+file);
                var f9=sheet.Figures.Single(q=>q.Id=="F9");
                foreach(var a in f9.Entities.OfType<FigurePolyline>().Where(e=>e.Tag!=null&&e.Tag.StartsWith(g.Tag+"-force-")))
                {
                    var body=f9.Entities.OfType<FigurePolyline>().Single(e=>e.Tag==g.Tag+"-buffer-"+a.Tag.Substring(a.Tag.LastIndexOf('-')+1));double axis=body.Points.Average(q=>q.X);
                    Check(a.Points[1].Y>a.Points[0].Y&&Math.Abs(a.Points[1].Y+2.0-CalculationFigures.F9Floor)<1e-6&&Math.Abs(a.Points[0].X-axis)<1e-6&&Math.Abs(a.Points[1].X-axis)<1e-6,"F9 "+a.Tag+" on its buffer centre line, down onto the pit floor at the buffer base: "+file);
                    Check(f9.Texts.Single(t=>t.Tag==a.Tag+"-text").Content==g.Load.Id+" "+Figure.Kn(g.Load.PerComponentLoadN.Value),"F9 "+a.Tag+" labelled "+g.Load.Id+" per buffer: "+file);
                }
            }
            double? pit=proj.Shaft==null?null:proj.Shaft.PitDepthMm;
            Check(pit.HasValue&&pit.Value>0?Value(x,"dim-pit")==Figure.Mm(pit.Value):Tagged(x,"dim-pit")==null,"F9 pit depth dimensioned only when Shaft.PitDepthMm is set: "+file);
        }
        // No numeric dimension without a project data source: in F7–F9 every text with a digit is a tagged value label,
        // and the only dimension is the pit depth.
        foreach(var f in sheet.Figures.Where(q=>q.Id=="F7"||q.Id=="F8"||q.Id=="F9"))
        {
            Check(f.Texts.Where(t=>Regex.IsMatch(t.Content,@"\d")).All(t=>t.Tag!=null&&t.Value!=null),"Every number in "+f.Id+" is a tagged project value: "+file);
            Check(f.Texts.Where(t=>t.Tag!=null&&t.Tag.StartsWith("dim-")).All(t=>t.Tag=="dim-pit-text"),"Only sourced dimensions in "+f.Id+" (pit depth): "+file);
        }
        if(preview=="default")Check(Value(svg["F7"],"tension-car")=="9.614"&&Value(svg["F7"],"tension-cwt")=="7.259"&&Value(svg["F7"],"tension-car-rope")=="1.602"&&Value(svg["F7"],"tension-cwt-rope")=="1.21"&&Value(svg["F8"],"force-p5-1")=="9.8"&&Value(svg["F8"],"force-p5-2")=="9.8"&&Value(svg["F9"],"car-force-1")=="39.2"&&Value(svg["F9"],"car-total")=="39.2"&&Value(svg["F9"],"cw-force-1")=="14.8"&&Value(svg["F9"],"cw-force-2")=="14.8"&&Value(svg["F9"],"cw-total")=="29.6","Default fixture Phase B values: T(car) 9.614, T(cwt) 7.259, per rope 1.602 / 1.21, P5 9.8 per rail, P6 39.2, P7 14.8 per buffer / 29.6 total");
        if(preview=="default")Check(new[]{"F4"}.All(id=>Value(svg[id],"force-ff1-left")=="2.2"&&Value(svg[id],"force-ff2-left")=="2.1")&&Value(svg["F5"],"dim-h")=="1200"&&Value(svg["F1"],"dim-W")=="1100"&&Value(svg["F1"],"dim-D")=="2100","Default fixture figures: FF 2.2 / 2.1 kN, h 1200, W 1100, D 2100");
    }
    // ---------- optional title block / approval block (2026-10-07) ----------
    static readonly string[] Placeholders={"N/A","n/a","TBD","NOT PROVIDED","Not provided","—","UNAVAILABLE","None"};
    static string MakeImage(string file,int w,int h,ImageFormat format)
    {
        string path=Path.Combine(output,file);
        using(var bitmap=new Bitmap(w,h))using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.White);g.FillRectangle(Brushes.SteelBlue,0,0,w/3,h);g.DrawString("LOGO",new Font("Arial",h/4f),Brushes.Navy,w/3f,h/4f);bitmap.Save(path,format);}
        return path;
    }
    static XmlDocument Part(Package package,string uri,XmlNamespaceManager ns)
    {var xml=new XmlDocument();using(var st=package.GetPart(new Uri(uri,UriKind.Relative)).GetStream())xml.Load(st);return xml;}
    static List<List<string>> TableWith(XmlDocument doc,XmlNamespaceManager ns,string marker)
    {
        foreach(XmlNode tbl in doc.SelectNodes("//w:tbl",ns))
        {
            var rows=tbl.SelectNodes("w:tr",ns).Cast<XmlNode>().Select(tr=>tr.SelectNodes("w:tc",ns).Cast<XmlNode>().Select(tc=>String.Concat(tc.SelectNodes(".//w:t",ns).Cast<XmlNode>().Select(t=>t.InnerText))).ToList()).ToList();
            if(rows.Any(r=>r.Count>0&&r[0]==marker))return rows;
        }
        return null;
    }
    // Checks the title block, approval block, running header/footer and image parts of one sheet.
    static void TitleCase(string label,ElevatorProject p,CalculationSheetInputs inputs,string[] expectedLabels,string company,string[] projectLabels,string[] docLabels,string footerRight,string[] names,string imageExt)
    {
        string file=label+".docx";string text;var sheet=Export(p,file,inputs,out text);
        Check(Near(sheet.CarGuideForces.FF1DesignN,2200)&&Near(sheet.CarGuideForces.FF2DesignN,2100)&&Near(sheet.SafetyPit.P5.PerComponentLoadN,9800)&&Near(sheet.SafetyPit.P6.GroupLoadN,39200)&&Near(sheet.SafetyPit.P7.GroupLoadN,29600)&&Near(sheet.SafetyPit.P7.PerComponentLoadN,14800),"Numbers unchanged (FF 2200/2100, P5 9800, P6 39200, P7 29600/14800 N): "+label);
        Check(text.Contains("= 2200 N")&&text.Contains("= 2100 N")&&text.Contains("9800 N per car guide rail")&&text.Contains("= 29600 N")&&text.Contains("= 14800 N"),"Numbers unchanged in document text: "+label);
        var ns=Ns();
        using(var package=Package.Open(Path.Combine(output,file),FileMode.Open,FileAccess.Read))
        {
            var doc=Part(package,"/word/document.xml",ns);var hdr=Part(package,"/word/header1.xml",ns);var ftr=Part(package,"/word/footer1.xml",ns);
            var outer=hdr.SelectSingleNode("/w:hdr/w:tbl",ns);var cells=outer.SelectNodes("w:tr/w:tc",ns).Cast<XmlNode>().ToList();
            bool logo=imageExt!=null;
            Check(cells.Count==(logo?4:3)&&(logo||cells[0].SelectSingleNode("w:tcPr/w:gridSpan[@w:val='2']",ns)!=null),"Title block: logo cell "+(logo?"present":"merged into the title cell")+": "+label);
            var titleCell=cells[logo?1:0];var titleText=titleCell.SelectNodes("w:p",ns).Cast<XmlNode>().Select(x=>Concat(x,ns)).Where(x=>x.Length>0).ToList();
            Check(titleText.SequenceEqual(company==null?new[]{"Elevator Calculation Sheet"}:new[]{company,"Elevator Calculation Sheet"}),"Title cell '"+String.Join(" / ",titleText)+"': "+label);
            Func<XmlNode,List<string>> labels=cell=>cell.SelectNodes("w:tbl/w:tr",ns).Cast<XmlNode>().Select(r=>Concat(r.SelectSingleNode("w:tc",ns),ns)).ToList();
            Check(labels(cells[cells.Count-2]).SequenceEqual(projectLabels),"Project/client rows "+String.Join(",",labels(cells[cells.Count-2]))+": "+label);
            Check(labels(cells[cells.Count-1]).SequenceEqual(docLabels.Concat(new[]{"Page"})),"Document rows "+String.Join(",",labels(cells[cells.Count-1]))+": "+label);
            Check(cells[cells.Count-2].SelectNodes("w:tbl/w:tr",ns).Cast<XmlNode>().All(r=>Concat(r.SelectNodes("w:tc",ns)[1],ns).Trim().Length>0),"Every shown header row has a value: "+label);
            Check(hdr.SelectNodes("//wp:inline",ns).Count==(logo?1:0),"Header logo "+(logo?"present":"absent")+": "+label);
            var ftrCells=ftr.SelectNodes("/w:ftr/w:tbl/w:tr/w:tc",ns).Cast<XmlNode>().Select(c=>Concat(c,ns)).ToList();
            Check(ftrCells.Count==2&&Regex.IsMatch(ftrCells[0],@"^Prepared with ShaftMaster · \d{4}-\d{2}-\d{2}$")&&ftrCells[1]==(footerRight??""),"Footer band '"+String.Join(" | ",ftrCells)+"': "+label);
            var project=TableWith(doc,ns,"Date");
            Check(project!=null&&project.Select(r=>r[0]).SequenceEqual(expectedLabels),"Project table rows "+(project==null?"missing":String.Join(", ",project.Select(r=>r[0])))+": "+label);
            Check(project.All(r=>r.Count==2&&r[1].Trim().Length>0),"Every shown project row has a value: "+label);
            var approval=TableWith(doc,ns,"Prepared by");
            Check(approval!=null&&approval.Count==4&&approval[0].Skip(1).SequenceEqual(new[]{"Name","Signature","Date"})&&approval.Skip(1).Select(r=>r[0]).SequenceEqual(new[]{"Prepared by","Checked by","Approved by"}),"Approval block present: "+label);
            for(int i=0;i<3;i++)Check(approval[i+1].Count==4&&approval[i+1][1]==names[i]&&approval[i+1][2]==""&&approval[i+1][3]=="","Approval row "+approval[i+1][0]+": name '"+names[i]+"', empty signature and date: "+label);
            string headerText=Concat(hdr.DocumentElement,ns),footerText=String.Join(" ",ftrCells),titleArea=Section(text,"Elevator Calculation Sheet\n","1  Calculation summary");
            foreach(var ph in Placeholders)Check(!headerText.Contains(ph)&&!footerText.Contains(ph)&&!String.Join("|",project.SelectMany(r=>r)).Contains(ph)&&!String.Join("|",approval.SelectMany(r=>r)).Contains(ph),"No placeholder '"+ph+"' in header, footer, project or approval tables: "+label);
            foreach(var ph in new[]{"N/A","n/a","TBD","NOT PROVIDED"})Check(!text.Contains(ph),"No placeholder '"+ph+"' anywhere: "+label);
            // The logo is in the header only; body images are the figures (PNG + SVG).
            var headerTargets=package.GetPart(new Uri("/word/header1.xml",UriKind.Relative)).GetRelationships().Where(r=>r.RelationshipType.EndsWith("/image")).Select(r=>r.TargetUri.OriginalString).ToList();
            var bodyTargets=package.GetPart(new Uri("/word/document.xml",UriKind.Relative)).GetRelationships().Where(r=>r.RelationshipType.EndsWith("/image")).Select(r=>r.TargetUri.OriginalString).ToList();
            Check(headerTargets.Count==(logo?1:0)&&!bodyTargets.Intersect(headerTargets).Any(),"Logo in the header only, not repeated in the body: "+label);
            Check(doc.SelectNodes("//w:body/w:p//wp:inline",ns).Count==0,"No free-standing body image (logo band removed): "+label);
            if(logo)
            {
                var logoPart=package.GetPart(new Uri("/word/"+headerTargets[0],UriKind.Relative));
                Check(logoPart.ContentType==(imageExt=="png"?"image/png":"image/jpeg"),"Logo part is "+imageExt+": "+label);
                var extent=hdr.SelectSingleNode("//wp:inline/wp:extent",ns);double ratio=(double)inputs.Logo.PixelWidth/inputs.Logo.PixelHeight;
                Check(Math.Abs(Double.Parse(extent.Attributes["cx"].Value)/Double.Parse(extent.Attributes["cy"].Value)-ratio)<0.01*ratio,"Logo aspect ratio kept: "+label);
            }
        }
    }
    static void TitleBlock()
    {
        string png=MakeImage("logo.png",300,100,ImageFormat.Png),jpg=MakeImage("logo.jpg",200,160,ImageFormat.Jpeg);
        var pngImage=DocxImage.FromFile(png);var jpgImage=DocxImage.FromFile(jpg);
        Check(pngImage.PixelWidth==300&&pngImage.PixelHeight==100&&jpgImage.PixelWidth==200&&jpgImage.PixelHeight==160,"PNG/JPEG pixel sizes read from file headers");
        bool rejected=false;try{DocxImage.FromBytes(new byte[]{1,2,3,4,5,6,7,8,9,10});}catch(ArgumentException){rejected=true;}Check(rejected,"Non-PNG/JPEG data rejected");
        double w,h;pngImage.Fit(50,18,out w,out h);Check(Math.Abs(w-50)<1e-9&&Math.Abs(h-50.0/3)<1e-9,"Fit keeps aspect ratio within the box");

        // A) Complete fields, PNG logo and company name.
        var p=BasicComplete("Test Tower T1");p.Report=new ProjectReportDetails{ClientName="Delta Developments",DocumentNumber="NL-CS-001",Revision="A",PreparedBy="A. Engineer",CheckedBy="B. Checker",ApprovedBy="C. Approver"};
        TitleCase("20-titleblock-complete",p,new CalculationSheetInputs{CompanyName="Nile Lifts Engineering",Logo=pngImage},new[]{"Project","Client","Document No.","Revision","Date","Elevator system","Rated load"},"Nile Lifts Engineering",new[]{"Project","Client"},new[]{"Doc. No.","Rev."},"Doc. No. NL-CS-001 · Rev A",new[]{"A. Engineer","B. Checker","C. Approver"},"png");
        // B) All blank (including whitespace-only text).
        p=BasicComplete("Test Tower T2");p.Report=new ProjectReportDetails{ClientName="  ",DocumentNumber="",Revision=null,PreparedBy=" ",CheckedBy=null,ApprovedBy=""};
        TitleCase("21-titleblock-all-blank",p,new CalculationSheetInputs{CompanyName="  "},new[]{"Project","Date","Elevator system","Rated load"},null,new[]{"Project"},new string[0],null,new[]{"","",""},null);
        // C) Logo only (JPEG).
        p=BasicComplete("Test Tower T3");
        TitleCase("22-titleblock-logo-only",p,new CalculationSheetInputs{Logo=jpgImage},new[]{"Project","Date","Elevator system","Rated load"},null,new[]{"Project"},new string[0],null,new[]{"","",""},"jpg");
        // D) Company name only.
        p=BasicComplete("Test Tower T4");
        TitleCase("23-titleblock-company-only",p,new CalculationSheetInputs{CompanyName="Nile Lifts Engineering"},new[]{"Project","Date","Elevator system","Rated load"},"Nile Lifts Engineering",new[]{"Project"},new string[0],null,new[]{"","",""},null);
        // E) Older project file without the Report group.
        string json=ProjectJson.Serialize(BasicComplete("Test Tower T5"));
        var raw=new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(json) as Dictionary<string,object>;((Dictionary<string,object>)raw["project"]).Remove("Report");
        string olderJson=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(raw);Check(!olderJson.Contains("\"Report\""),"Older project JSON has no Report group");
        var older=ProjectJson.Deserialize(olderJson);Check(older.Report==null,"Older project opens with Report blank");
        TitleCase("24-titleblock-older-project",older,new CalculationSheetInputs(),new[]{"Project","Date","Elevator system","Rated load"},null,new[]{"Project"},new string[0],null,new[]{"","",""},null);

        // Report edits never make results stale, and never change drawing geometry or STEP parameters.
        p=BasicComplete("Test Tower T6");
        var ws=new CalculationWorkspace();ws.Calculate(p,ctx);var pit=new SafetyPitCalculationEngine().Calculate(p);
        var g=new ElevatorDesignEngine().CalculateCarGuideForceGeometry(p,ctx);var ff=new CarGuideForceCalculationEngine().Calculate(p,g,ctx);
        ElevatorDesignResult design;var stepRef=Wire.Read<StepModelParameters>(Path.Combine(bin,"preliminary-3d-reference.json"));
        var adv=AdvancedComplete("Test Tower T7");
        string stepBefore=Wire.Json(StepInputFactory.Create(adv,ctx,stepRef,out design));string planBefore=Wire.Json(design.Plan);var aws=new CalculationWorkspace();aws.Calculate(adv,ctx);
        Check(ws.IsCurrent(p,ctx)&&SafetyPitCalculationEngine.IsCurrent(pit,p)&&CarGuideForceCalculationEngine.IsCurrent(ff,p,ctx)&&aws.IsCurrent(adv,ctx),"Baseline results current");
        foreach(var project in new[]{p,adv})project.Report=new ProjectReportDetails{ClientName="Changed client",DocumentNumber="X-9",Revision="Rev 7",PreparedBy="P",CheckedBy="C",ApprovedBy="A"};
        Check(ws.IsCurrent(p,ctx),"Report edit keeps Basic P1–P4 current");Check(aws.IsCurrent(adv,ctx),"Report edit keeps Advanced P1–P4 current");
        Check(SafetyPitCalculationEngine.IsCurrent(pit,p),"Report edit keeps P5–P7 current");Check(CarGuideForceCalculationEngine.IsCurrent(ff,p,ctx),"Report edit keeps FF current");
        Check(Wire.Json(StepInputFactory.Create(adv,ctx,stepRef,out design))==stepBefore&&Wire.Json(design.Plan)==planBefore,"Report edit leaves STEP parameters and drawing plan geometry unchanged");

        // Save / reopen round trip.
        var session=new ProjectSession(p);string saved=Path.Combine(output,"25-report-roundtrip.elevator.json");session.Save(saved);session.Open(saved);var r=session.Project.Report;
        Check(r!=null&&r.ClientName=="Changed client"&&r.DocumentNumber=="X-9"&&r.Revision=="Rev 7"&&r.PreparedBy=="P"&&r.CheckedBy=="C"&&r.ApprovedBy=="A","Report fields survive save/reopen");
        var schema=File.ReadAllText(Path.Combine(root,"config/ElevatorProject.schema.json"));
        Check(schema.Contains("\"Report\":{\"anyOf\"")&&schema.Contains("\"const\":1"),"Schema: Report optional (anyOf with null), schemaVersion const 1");

        // Company settings live only in the settings folder (redirected for tests), never in the project.
        CompanyReportSettings.SettingsDirectory=Path.Combine(output,"settings");
        var cs=CompanyReportSettings.Load();Check(cs.CompanyName==null&&cs.LogoFile==null&&cs.Logo()==null,"No settings: blank company and logo");
        cs.CompanyName="Nile Lifts Engineering";cs.SetLogo(png);cs.Save();string first=cs.LogoPath;
        var loaded=CompanyReportSettings.Load();Check(loaded.CompanyName=="Nile Lifts Engineering"&&loaded.Logo()!=null&&loaded.Logo().PixelWidth==300&&Path.GetDirectoryName(loaded.LogoPath)==CompanyReportSettings.SettingsDirectory,"Settings saved; logo copied into the settings folder");
        loaded.SetLogo(jpg);loaded.Save();Check(!File.Exists(first)&&CompanyReportSettings.Load().Logo().Extension=="jpg","Replacing the logo removes the old copy");
        loaded.RemoveLogo();loaded.Save();Check(CompanyReportSettings.Load().LogoFile==null&&Directory.GetFiles(CompanyReportSettings.SettingsDirectory,"company-logo-*").Length==0,"Remove deletes the stored logo");
        bool bad=false;try{loaded.SetLogo(Path.Combine(output,"25-report-roundtrip.elevator.json"));}catch(ArgumentException){bad=true;}Check(bad&&CompanyReportSettings.Load().LogoFile==null,"Non-image logo rejected");
        Check(!ProjectJson.Serialize(p).Contains("Nile Lifts"),"Company settings are not stored in the project file");
    }
}
