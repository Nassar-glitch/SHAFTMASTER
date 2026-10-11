using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Xml;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Elevator.Core.Application;
using Elevator.Core.Presets;
using Elevator.Core.MachineRoom;
using Elevator.Core.GuideRails;
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

// Tier-1 golden-master tests (ARCHITECTURE-PLAN P0). For every fixture in tests/golden/fixtures the current behaviour is
// captured as text files and compared byte for byte with tests/golden/approved: calculation results and the automatic rope
// selection, fingerprints, the project JSON after load -> save, the calculation-sheet and Design Notes text and every figure
// SVG. Plus the project JSON of "new project from presets". No AutoCAD and no Word. Normalisations: docs/TESTING.md.
// Modes: compare (default), approve (initial baseline only: the approved folder must be empty), fixtures (writes the
// fixture set once into an empty folder).
public static class GoldenTests
{
    // Fixed report clock: the date printed in the calculation sheet and the Design Notes.
    public static readonly DateTime Clock = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Local);
    static string root, bin, fixtures, approved, run, mode;
    static DesignContext ctx; static PresetLibrary lib;
    static int compared; static readonly List<string> mismatches = new List<string>(); static readonly HashSet<string> produced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

    public sealed class GoldenOptions
    {
        public string Description, CompanyName, LogoFile, CarGuideForceDraftError;
        public Dictionary<string, string> ChassisDraftErrors, SafetyPitDraftErrors;
    }

    [STAThread] // WPF text measurement for the figures
    public static int Main(string[] args)
    {
        root = Path.GetFullPath(args[0]); bin = Path.GetFullPath(args[1]); fixtures = Path.GetFullPath(args[2]); approved = Path.GetFullPath(args[3]); run = Path.GetFullPath(args[4]); mode = args.Length > 5 ? args[5] : "compare";
        int exit = 1;
        try
        {
            ReportClock.Now = () => Clock;
            FigureRender.Rasterizer = new WpfFigureRasterizer(); // WPF text measurement and PNG, as in the application
            ctx = Wire.Read<DesignContext>(Path.Combine(bin, "reviewed-context.json")); ctx.ChassisEvidence = Wire.Read<ChassisGeometryEvidence>(Path.Combine(bin, "reviewed-chassis-evidence.json"));
            lib = PresetLibrary.Load(Path.Combine(bin, "presets.json"));
            Directory.CreateDirectory(run);
            if (mode == "fixtures") { MakeFixtures(); Console.WriteLine("Fixtures written: " + Directory.GetFiles(fixtures, "*.elevator.json").Length); return 0; }
            if (mode == "door-fixtures") { MakeDoorFixtures(); Console.WriteLine("Door fixtures written (existing files are kept)."); return 0; }
            if (mode == "approve" && Directory.Exists(approved) && Directory.EnumerateFileSystemEntries(approved).Any()) throw new InvalidOperationException("Approve creates the initial baseline only; the approved folder is not empty: " + approved);
            var names = Directory.GetFiles(fixtures, "*.elevator.json").Select(f => Path.GetFileName(f)).OrderBy(f => f, StringComparer.Ordinal).ToList();
            if (names.Count == 0) throw new InvalidOperationException("No fixtures in " + fixtures);
            foreach (var name in names) Capture(name.Substring(0, name.Length - ".elevator.json".Length));
            NewProjectFromPresets();
            if (mode == "compare" && Directory.Exists(approved))
                foreach (var file in Directory.GetFiles(approved, "*", SearchOption.AllDirectories))
                {
                    string rel = file.Substring(approved.Length + 1).Replace('\\', '/');
                    if (!produced.Contains(rel)) mismatches.Add(rel + ": approved file was not produced");
                }
            exit = mismatches.Count == 0 ? 0 : 1;
            Wire.Write(Path.Combine(run, "golden-results.json"), new { Status = exit == 0 ? "PASS" : "FAIL", Mode = mode, Fixtures = names.Count, Files = compared, Mismatches = mismatches, Clock = Clock.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) });
            Console.WriteLine((exit == 0 ? "PASS: " : "FAIL: ") + compared + " golden files" + (mode == "approve" ? " approved" : " compared") + " (" + names.Count + " fixtures + new project)" + (mismatches.Count > 0 ? "; " + mismatches.Count + " mismatches, see " + Path.Combine(run, "mismatch") : "") + ".");
            foreach (var m in mismatches.Take(20)) Console.WriteLine("  MISMATCH " + m);
        }
        catch (Exception e) { Wire.Write(Path.Combine(run, "golden-error.json"), new { Error = e.ToString() }); Console.Error.WriteLine(e); exit = 1; }
        return exit;
    }

    // ---------- capture ----------
    static void Capture(string name)
    {
        string fixturePath = Path.Combine(fixtures, name + ".elevator.json");
        var options = File.Exists(Path.Combine(fixtures, name + ".options.json")) ? new JavaScriptSerializer().Deserialize<GoldenOptions>(File.ReadAllText(Path.Combine(fixtures, name + ".options.json"))) : new GoldenOptions();
        // Project JSON after load -> save through the application's ProjectSession: byte-exact values, member order normalised (JsonMembers.Sort).
        var session = new ProjectSession(null); session.Open(fixturePath);
        string saved = Path.Combine(run, "work", name + ".saved.elevator.json"); Directory.CreateDirectory(Path.GetDirectoryName(saved));
        session.Save(saved);
        Emit(name + "/roundtrip.elevator.json", Utf8.GetBytes(JsonMembers.Sort(File.ReadAllText(saved, Utf8))));
        var p = session.Project;
        // A project that follows the presets gets them applied (as the application does), including the rope selection.
        object selection = null;
        if (p.FollowsPresets == true) selection = Try(() => { var o = ProjectPresets.Apply(p, lib, ctx); if (o != null) o.Milliseconds = 0; /* run time of the search: volatile */ return o; });
        var workspace = new CalculationWorkspace();
        object chassis = Try(() => workspace.Calculate(p, ctx));
        object pit = Try(() => new SafetyPitCalculationEngine().Calculate(p));
        object ff = Try(() => new CarGuideForceCalculationEngine().Calculate(p, new ElevatorDesignEngine().CalculateCarGuideForceGeometry(p, ctx), ctx));
        object rope = Try(() => new RopeTractionCalculationEngine().Calculate(p, ctx, lib));
        var rt = p.Calculations == null ? null : p.Calculations.RopeTraction;
        var ropeResult = rope as RopeTractionResult;
        var values = new
        {
            Q = p.Elevator == null ? null : p.Elevator.CapacityKg,
            SW = p.Shaft == null ? (double?)null : p.Shaft.WidthMm, SD = p.Shaft == null ? (double?)null : p.Shaft.DepthMm,
            n = p.Suspension == null ? null : p.Suspension.RopeCount, d = p.Suspension == null ? null : p.Suspension.RopeDiameterMm,
            Dt = p.Machine == null ? null : p.Machine.TractionSheavePitchDiameterMm,
            Beta = ropeResult == null || ropeResult.Inputs == null ? null : ropeResult.Inputs.UndercutAngleDeg,
            SelectedBeta = rt == null ? null : rt.SelectedUndercutAngleDeg,
            BalanceFactor = rt != null && rt.SelectedBalanceFactor.HasValue ? rt.SelectedBalanceFactor : (p.FollowsPresets == true ? lib.CounterweightBalanceFactor : null),
            SelectedBalanceFactor = rt == null ? null : rt.SelectedBalanceFactor,
            P = p.Calculations == null ? null : p.Calculations.CarMassKg,
            CW = p.Calculations == null ? null : p.Calculations.CounterweightMassKg,
            Machine = p.Calculations == null ? null : p.Calculations.MachineMassKg,
            Locked = p.UserEditedFields == null ? null : p.UserEditedFields.OrderBy(k => k, StringComparer.Ordinal).ToList()
        };
        EmitJson(name + "/results.json", new { Fixture = name, options.Description, p.FollowsPresets, Mode = p.Calculations == null ? null : CalculationModes.Resolve(p.Calculations), Selection = selection, Values = values, Chassis = chassis, SafetyPit = pit, CarGuideForces = ff, RopeTraction = rope });
        if (p.Doors != null && Elevator.Core.Doors.DoorTypes.IsAutomatic(p.Doors.Type))
            EmitJson(name + "/doors.json", Try(() => { var plan = new ElevatorDesignEngine().Calculate(p, ctx, DrawingViews.Plan).Plan; return new { Plan = plan, Layout = Elevator.Core.Doors.DoorRules.Evaluate(p, ctx.Doors, plan) }; }));
        var cr = chassis as ChassisCalculationResult; var pr = pit as SafetyPitCalculationResult; var fr = ff as CarGuideForceResult;
        EmitJson(name + "/fingerprints.json", new
        {
            Chassis = cr == null ? null : cr.InputFingerprint, ChassisCurrent = cr == null ? (bool?)null : workspace.IsCurrent(p, ctx),
            SafetyPit = pr == null ? null : pr.InputFingerprint, SafetyPitCurrent = pr == null ? (bool?)null : SafetyPitCalculationEngine.IsCurrent(pr, p),
            CarGuideForces = fr == null ? null : fr.InputFingerprint, CarGuideForcesCurrent = fr == null ? (bool?)null : CarGuideForceCalculationEngine.IsCurrent(fr, p, ctx),
            RopeTraction = ropeResult == null ? null : ropeResult.InputFingerprint, RopeTractionCurrent = ropeResult == null ? (bool?)null : RopeTractionCalculationEngine.IsCurrent(ropeResult, p, ctx, lib),
            PresetLibrary = lib.Hash()
        });
        // Calculation sheet: document / header / footer text and every figure SVG.
        var inputs = Inputs(options);
        try
        {
            var sheet = CalculationSheet.Build(p, ctx, inputs);
            EmitJson(name + "/sheet-omitted.json", new { sheet.HasContent, Omitted = sheet.Omitted });
            if (sheet.HasContent)
            {
                byte[] docx; using (var m = new MemoryStream()) { sheet.Render().Save(m); docx = m.ToArray(); }
                Emit(name + "/sheet.txt", Utf8.GetBytes(DocxText(docx)));
                EmitJson(name + "/sheet-figure-errors.json", sheet.FigureErrors);
                int i = 0;
                foreach (var f in sheet.Figures) { i++; Emit(name + "/figures/" + i.ToString("00", CultureInfo.InvariantCulture) + "-" + Slug(f.Title) + ".svg", Utf8.GetBytes(FigureRender.Svg(f))); }
            }
        }
        catch (Exception e) { Emit(name + "/sheet.txt", Utf8.GetBytes("EXCEPTION " + e.GetType().FullName + ": " + e.Message + "\n")); }
        // Design Notes.
        try
        {
            var notes = DesignNotes.Build(p, ctx, Inputs(options));
            byte[] docx; using (var m = new MemoryStream()) { notes.Render().Save(m); docx = m.ToArray(); }
            Emit(name + "/design-notes.txt", Utf8.GetBytes(DocxText(docx)));
        }
        catch (Exception e) { Emit(name + "/design-notes.txt", Utf8.GetBytes("EXCEPTION " + e.GetType().FullName + ": " + e.Message + "\n")); }
    }
    static CalculationSheetInputs Inputs(GoldenOptions o)
    {
        var i = new CalculationSheetInputs { Presets = lib, CompanyName = o.CompanyName, CarGuideForceDraftError = o.CarGuideForceDraftError };
        if (o.LogoFile != null) i.Logo = DocxImage.FromFile(Path.Combine(root, o.LogoFile));
        if (o.ChassisDraftErrors != null) foreach (var e in o.ChassisDraftErrors) i.ChassisDraftErrors[e.Key] = e.Value;
        if (o.SafetyPitDraftErrors != null) foreach (var e in o.SafetyPitDraftErrors) i.SafetyPitDraftErrors[e.Key] = e.Value;
        return i;
    }
    // "New project from presets" exactly as the application creates it; the random project and floor GUIDs are the only normalisation.
    static void NewProjectFromPresets()
    {
        var p = NewProject();
        string saved = Path.Combine(run, "work", "new-project.saved.elevator.json");
        new ProjectSession(p).Save(saved);
        Emit("new-project-from-presets/new-project.elevator.json", Utf8.GetBytes(JsonMembers.Sort(NormaliseGuids(File.ReadAllText(saved, Utf8)))));
    }
    static string NormaliseGuids(string json)
    {
        var map = new Dictionary<string, string>();
        return Regex.Replace(json, "\"[0-9a-f]{32}\"", m => { string v; if (!map.TryGetValue(m.Value, out v)) { v = "\"GUID-" + (map.Count + 1) + "\""; map[m.Value] = v; } return v; });
    }
    static object Try(Func<object> f)
    {
        try { return f(); } catch (Exception e) { return "EXCEPTION " + e.GetType().FullName + ": " + e.Message; }
    }

    // ---------- DOCX text (document, header, footer): one line per paragraph, w:br as a line break ----------
    static string DocxText(byte[] docx)
    {
        var b = new StringBuilder();
        using (var m = new MemoryStream(docx))
        using (var package = Package.Open(m, FileMode.Open, FileAccess.Read))
        {
            foreach (var part in package.GetParts().Where(x => x.Uri.OriginalString.StartsWith("/word/document") || x.Uri.OriginalString.StartsWith("/word/header") || x.Uri.OriginalString.StartsWith("/word/footer")).OrderBy(x => x.Uri.OriginalString == "/word/document.xml" ? 0 : 1).ThenBy(x => x.Uri.OriginalString, StringComparer.Ordinal))
            {
                var xml = new XmlDocument(); using (var s = part.GetStream()) xml.Load(s);
                var ns = new XmlNamespaceManager(xml.NameTable); ns.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
                b.Append("=== ").Append(part.Uri.OriginalString).Append('\n');
                foreach (XmlNode p in xml.SelectNodes("//w:p", ns))
                {
                    foreach (XmlNode t in p.SelectNodes(".//w:t|.//w:br|.//w:tab|.//w:instrText", ns))
                        b.Append(t.LocalName == "br" ? "\n" : t.LocalName == "tab" ? "\t" : t.LocalName == "instrText" ? "{" + t.InnerText.Trim() + "}" : t.InnerText);
                    b.Append('\n');
                }
            }
        }
        return b.ToString();
    }
    static string Slug(string title)
    {
        var s = Regex.Replace((title ?? "figure").ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return s.Length > 50 ? s.Substring(0, 50).Trim('-') : s.Length == 0 ? "figure" : s;
    }

    // ---------- output, comparison and diff ----------
    static void EmitJson(string rel, object value) { Emit(rel, Utf8.GetBytes(Pretty(value) + "\n")); }
    static void Emit(string rel, byte[] content)
    {
        compared++; produced.Add(rel);
        string captured = Path.Combine(run, "captured", rel.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(captured)); File.WriteAllBytes(captured, content);
        string target = Path.Combine(approved, rel.Replace('/', Path.DirectorySeparatorChar));
        if (mode == "approve") { Directory.CreateDirectory(Path.GetDirectoryName(target)); File.WriteAllBytes(target, content); return; }
        // approve-new: files that have no approved copy yet are approved; an existing approved file is compared as usual.
        if (mode == "approve-new" && !File.Exists(target)) { Directory.CreateDirectory(Path.GetDirectoryName(target)); File.WriteAllBytes(target, content); return; }
        if (!File.Exists(target)) { mismatches.Add(rel + ": no approved file"); WriteMismatch(rel, content, null); return; }
        var expected = File.ReadAllBytes(target);
        if (!expected.SequenceEqual(content)) { mismatches.Add(rel + ": differs"); WriteMismatch(rel, content, expected); }
    }
    static void WriteMismatch(string rel, byte[] received, byte[] expected)
    {
        string basePath = Path.Combine(run, "mismatch", rel.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(basePath));
        File.WriteAllBytes(basePath + ".received", received);
        File.WriteAllText(basePath + ".diff.txt", expected == null ? "No approved file.\n" : Diff(Utf8.GetString(expected), Utf8.GetString(received)), Utf8);
    }
    // Readable diff: the changed region (first to last differing line) with three lines of context, approved (-) and received (+).
    static string Diff(string a, string b)
    {
        var x = a.Split('\n'); var y = b.Split('\n');
        int start = 0; while (start < x.Length && start < y.Length && x[start] == y[start]) start++;
        int endX = x.Length - 1, endY = y.Length - 1; while (endX >= start && endY >= start && x[endX] == y[endY]) { endX--; endY--; }
        var o = new StringBuilder(); o.Append("Approved lines " + (start + 1) + "-" + (endX + 1) + " (of " + x.Length + ") replaced by received lines " + (start + 1) + "-" + (endY + 1) + " (of " + y.Length + ").\n");
        for (int i = Math.Max(0, start - 3); i < start; i++) o.Append("  ").Append(x[i]).Append('\n');
        for (int i = start; i <= endX && i < start + 200; i++) o.Append("- ").Append(x[i]).Append('\n');
        for (int i = start; i <= endY && i < start + 200; i++) o.Append("+ ").Append(y[i]).Append('\n');
        for (int i = endX + 1; i < Math.Min(x.Length, endX + 4); i++) o.Append("  ").Append(x[i]).Append('\n');
        return o.ToString();
    }
    // Canonical, indented JSON: keys sorted (ordinal), so the serializer's field order does not matter.
    static string Pretty(object value)
    {
        var js = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 1000 };
        var b = new StringBuilder(); Write(js.DeserializeObject(js.Serialize(value)), js, b, 0); return b.ToString();
    }
    static void Write(object v, JavaScriptSerializer js, StringBuilder b, int depth)
    {
        string pad = new string(' ', 2 * (depth + 1)), end = new string(' ', 2 * depth);
        var map = v as IDictionary<string, object>;
        if (map != null)
        {
            if (map.Count == 0) { b.Append("{}"); return; }
            b.Append("{\n"); bool first = true;
            foreach (var k in map.Keys.OrderBy(k => k, StringComparer.Ordinal)) { if (!first) b.Append(",\n"); first = false; b.Append(pad).Append(js.Serialize(k)).Append(": "); Write(map[k], js, b, depth + 1); }
            b.Append('\n').Append(end).Append('}'); return;
        }
        var list = v as System.Collections.IEnumerable;
        if (list != null && !(v is string))
        {
            var items = list.Cast<object>().ToList();
            if (items.Count == 0) { b.Append("[]"); return; }
            b.Append("[\n");
            for (int i = 0; i < items.Count; i++) { if (i > 0) b.Append(",\n"); b.Append(pad); Write(items[i], js, b, depth + 1); }
            b.Append('\n').Append(end).Append(']'); return;
        }
        b.Append(js.Serialize(v));
    }

    // ---------- fixture set (written once by "-MakeFixtures"; reuses the existing test fixtures) ----------
    static ElevatorProject Seed() { return ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json"))); }
    static ElevatorProject NewProject() { var seed = Seed(); return ProjectPresets.CreateNew(lib, ctx, seed.Counterweight, seed.Rails.BracketSource); }
    static ElevatorProject NewWithQ(double q) { var p = NewProject(); ProjectPresets.Lock(p, "Elevator.CapacityKg"); p.Elevator.CapacityKg = q; return p; }
    // CalculationSheetTests.BasicComplete / AdvancedComplete.
    static ElevatorProject Example(string name)
    {
        var p = Seed(); p.Info.Name = name;
        foreach (var f in p.Building.Floors.Take(p.Building.Floors.Count - 1)) if (!f.HeightToNextMm.HasValue) f.HeightToNextMm = 3000;
        return p;
    }
    static ElevatorProject BasicComplete(string name)
    {
        var p = Example(name); p.Elevator.CapacityKg = 480; p.Shaft.WidthMm = 1500; p.Shaft.DepthMm = 2500; p.Machine.TractionSheavePitchDiameterMm = 520; p.Elevator.SpeedMetresPerSecond = 1.0;
        p.Calculations = new ChassisCalculationInputs { Mode = "Basic", CarMassKg = 500, CounterweightMassKg = 740, MachineMassKg = 300 };
        p.Calculations.SafetyPit = new SafetyPitInputs { SafetyGearType = "Progressive", CarGuideCount = 2, CarBufferCount = 1, CounterweightBufferCount = 2, CounterweightBufferSharing = "EqualSharing" };
        p.Calculations.CarGuideForces = new CarGuideForceInputs { GuideShoeSpacingMm = 1200 };
        return p;
    }
    static ElevatorProject AdvancedComplete(string name)
    {
        var p = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(root, "tests/fixtures/SYNTHETIC.elevator.json"))); p.Info.Name = name;
        var c = p.Calculations; c.MachineCgSource = "Machine supplier drawing MR-01"; c.TransferAssumptionSource = "Chassis drawing CH-02"; c.HangerAttachmentSource = "Hanger detail HG-03";
        c.ChassisWeightSourceOrExclusionReason = "Chassis weight carried separately"; c.HangerWeightSourceOrExclusionReason = "Hanger weight carried separately";
        c.SafetyPit = new SafetyPitInputs { SafetyGearType = "Progressive", CarGuideCount = 2, CarBufferCount = 1, CounterweightBufferCount = 1 };
        c.CarGuideForces = new CarGuideForceInputs { GuideShoeSpacingMm = 1200 };
        return p;
    }
    static void Save(string name, ElevatorProject p, GoldenOptions o)
    {
        new ProjectSession(p).Save(Path.Combine(fixtures, name + ".elevator.json"));
        if (o != null) File.WriteAllText(Path.Combine(fixtures, name + ".options.json"), Pretty(o) + "\n", Utf8);
    }
    static GoldenOptions Describe(string text) { return new GoldenOptions { Description = text }; }
    // The door fixtures (D1): written beside the existing set, never over an existing file.
    static void MakeDoorFixtures()
    {
        Directory.CreateDirectory(fixtures);
        Action<string, ElevatorProject, string> add = (name, p, text) =>
        {
            if (File.Exists(Path.Combine(fixtures, name + ".elevator.json"))) return;
            Save(name, p, Describe(text));
        };
        Func<string, string, ElevatorProject> door = (name, type) =>
        {
            var p = BasicComplete(name); p.Shaft.WidthMm = 1900; p.Shaft.DepthMm = 2800; Elevator.Core.Doors.DoorFlow.SetType(p, type); return p;
        };
        var s2 = door("Golden D1 - 2S auto", Elevator.Core.Doors.DoorTypes.Automatic2S);
        add("doors-2s-auto", s2, "Automatic 2S, opening selected by the rules, shaft 1900 x 2800 (D1).");
        var c2 = door("Golden D2 - 2C auto", Elevator.Core.Doors.DoorTypes.Automatic2C);
        add("doors-2c-auto", c2, "Automatic 2C, opening selected by the rules, shaft 1900 x 2800 (D1).");
        var shifted = door("Golden D5 - 2S shifted", Elevator.Core.Doors.DoorTypes.Automatic2S); shifted.Shaft.WidthMm = 1750;
        add("doors-2s-shifted", shifted, "Automatic 2S on the 1750 mm shaft: the 1000 mm opening is selected and shifted to the car side wall (fixed return 0) (D1).");
        // D2 (door drawings): the three native cases, F1 shaft plan figures with the automatic door.
        var d2s = door("Golden D6 - 2S 800 on 1720", Elevator.Core.Doors.DoorTypes.Automatic2S); d2s.Shaft.WidthMm = 1720;
        Elevator.Core.Doors.DoorFlow.SetNumber(d2s, Elevator.Core.Doors.DoorSettings.OpeningKey, 800);
        add("doors-2s-800-1720", d2s, "Automatic 2S, 800 mm locked on a 1720 x 2800 shaft: preferred layout (D2 figure F1).");
        var d2c = door("Golden D7 - 2C 800 on 1720", Elevator.Core.Doors.DoorTypes.Automatic2C); d2c.Shaft.WidthMm = 1720;
        Elevator.Core.Doors.DoorFlow.SetNumber(d2c, Elevator.Core.Doors.DoorSettings.OpeningKey, 800);
        add("doors-2c-800-1720", d2c, "Automatic 2C, 800 mm locked on a 1720 x 2800 shaft (D2 figure F1).");
        var d7 = door("Golden D8 - 2S 700 on 1300", Elevator.Core.Doors.DoorTypes.Automatic2S); d7.Shaft.WidthMm = 1300;
        Elevator.Core.Doors.DoorFlow.SetNumber(d7, Elevator.Core.Doors.DoorSettings.OpeningKey, 700);
        add("doors-2s-700-1300", d7, "Automatic 2S, 700 mm locked on a 1300 x 2800 shaft: opening shifted to the car side wall (D2 figure F1).");
        var left = door("Golden D3 - 2S left 1000", Elevator.Core.Doors.DoorTypes.Automatic2S); left.Shaft.WidthMm = 2100;
        Elevator.Core.Doors.DoorFlow.SetNumber(left, Elevator.Core.Doors.DoorSettings.OpeningKey, 1000); Elevator.Core.Doors.DoorFlow.SetHanding(left, Elevator.Core.Doors.DoorTypes.Left);
        Elevator.Core.Doors.DoorFlow.SetNumber(left, Elevator.Core.Doors.DoorSettings.SillGapKey, 30);
        add("doors-2s-left-1000", left, "Automatic 2S, 1000 mm and left handing and sill gap 30 locked, shaft 2100 x 2800 (D1).");
        var big = door("Golden D4 - 2C 1200 does not fit", Elevator.Core.Doors.DoorTypes.Automatic2C);
        Elevator.Core.Doors.DoorFlow.SetNumber(big, Elevator.Core.Doors.DoorSettings.OpeningKey, 1200);
        add("doors-2c-1200-no-fit", big, "Automatic 2C with 1200 mm locked on a 1900 mm shaft: does not fit; Design Notes give the needed widths (D1).");
        // D4 (Doors Offset): the new keys Doors.OffsetMm / Doors.OffsetSide, a value outside the range, and the key of earlier files.
        var o2c = door("Golden D9 - 2C offset 60 Right", Elevator.Core.Doors.DoorTypes.Automatic2C);
        Elevator.Core.Doors.DoorFlow.SetNumber(o2c, Elevator.Core.Doors.DoorSettings.OpeningKey, 800);
        Elevator.Core.Doors.DoorFlow.SetOffset(o2c, 60, Elevator.Core.Doors.DoorTypes.Right);
        add("doors-2c-offset-60-right", o2c, "Automatic 2C, 800 mm locked on a 1900 x 2800 shaft, Doors Offset 60 mm Right (D4).");
        var o2s = door("Golden D10 - 2S offset 40 Left", Elevator.Core.Doors.DoorTypes.Automatic2S); o2s.Shaft.WidthMm = 1720;
        Elevator.Core.Doors.DoorFlow.SetNumber(o2s, Elevator.Core.Doors.DoorSettings.OpeningKey, 800);
        Elevator.Core.Doors.DoorFlow.SetOffset(o2s, 40, Elevator.Core.Doors.DoorTypes.Left);
        add("doors-2s-offset-40-left", o2s, "Automatic 2S, 800 mm locked on a 1720 x 2800 shaft, Doors Offset 40 mm Left (D4).");
        var oor = door("Golden D11 - 2S offset out of range", Elevator.Core.Doors.DoorTypes.Automatic2S); oor.Shaft.WidthMm = 1720;
        Elevator.Core.Doors.DoorFlow.SetNumber(oor, Elevator.Core.Doors.DoorSettings.OpeningKey, 800);
        Elevator.Core.Doors.DoorFlow.SetOffset(oor, 300, Elevator.Core.Doors.DoorTypes.Left);
        add("doors-2s-offset-out-of-range", oor, "Automatic 2S, 800 mm locked on a 1720 x 2800 shaft, Doors Offset 300 mm Left: outside the range, kept; Design Notes give the limit (D4).");
        var old = door("Golden D12 - 2S old opening offset", Elevator.Core.Doors.DoorTypes.Automatic2S); old.Shaft.WidthMm = 1720;
        Elevator.Core.Doors.DoorFlow.SetNumber(old, Elevator.Core.Doors.DoorSettings.OpeningKey, 800);
        {
            var doorsExt = (Dictionary<string, object>)old.Extensions["doors"];
            doorsExt["Doors.OpeningOffsetMm"] = 40.0;
            old.UserEditedFields.Add("Doors.OpeningOffsetMm");
        }
        add("doors-2s-old-opening-offset", old, "Automatic 2S as an earlier version saved it: the locked key Doors.OpeningOffsetMm 40 mm towards the fixed side (D4 conversion).");
    }

    static void MakeFixtures()
    {
        if (Directory.Exists(fixtures) && Directory.EnumerateFileSystemEntries(fixtures).Any()) throw new InvalidOperationException("Fixture folder is not empty: " + fixtures);
        Directory.CreateDirectory(fixtures);
        // Default new project (Q 630) as the application creates it.
        Save("new-project-q630", NewProject(), Describe("Default new project from presets (Q 630), as File > New creates it."));
        // Q sweep (as in output/presets-heavy-20261009): default survey shaft, and the Table 6 shafts of that run.
        var table6 = new Dictionary<double, double[]> { { 320, new double[] { 1300, 1400 } }, { 480, new double[] { 1300, 1800 } }, { 630, new double[] { 1300, 2200 } }, { 800, new double[] { 1500, 2200 } }, { 1000, new double[] { 1900, 2000 } }, { 1275, new double[] { 2200, 2000 } }, { 1600, new double[] { 2200, 2200 } } };
        foreach (var q in new double[] { 320, 480, 630, 800, 1000, 1275, 1600 })
        {
            Save("sweep-survey-q" + q.ToString("0000", CultureInfo.InvariantCulture), NewWithQ(q), Describe("Q sweep: new project, Q " + q + " locked, default survey shaft 1500 x 1800."));
            var t = NewWithQ(q); ProjectPresets.Lock(t, "Shaft.WidthMm"); ProjectPresets.Lock(t, "Shaft.DepthMm"); t.Shaft.WidthMm = table6[q][0]; t.Shaft.DepthMm = table6[q][1];
            Save("sweep-table6-q" + q.ToString("0000", CultureInfo.InvariantCulture), t, Describe("Q sweep: new project, Q " + q + " locked, Table 6 shaft " + table6[q][0] + " x " + table6[q][1] + " locked."));
        }
        // Locked versus unlocked rope / groove values (PresetsTests fixtures A-E, H, I); the unlocked counterparts are the new project and the sweep.
        var a = NewWithQ(320); PresetFields.MinimumBreakingLoad.Edit(a, 20.0); Save("locked-mbl-q320", a, Describe("Q 320 with the rope MBL locked at 20 kN (fixture A)."));
        var b = NewProject(); ProjectPresets.Find(ProjectPresets.RopeCountKey).Edit(b, 7); Save("locked-rope-count-7", b, Describe("New project with the rope count locked at 7 (fixture B)."));
        var c = NewWithQ(320); PresetFields.MinimumBreakingLoad.Edit(c, 20.0); ProjectPresets.Find(ProjectPresets.RopeCountKey).Edit(c, 4); ProjectPresets.Find(ProjectPresets.RopeDiameterKey).Edit(c, 10.0); Save("locked-rope-count-and-diameter", c, Describe("Q 320, MBL 20 kN, rope count 4 and diameter 10 mm locked (fixture C)."));
        var d = NewProject(); PresetFields.UndercutAngle.Edit(d, 95.0); Save("locked-beta-95", d, Describe("New project with the undercut angle locked at 95 degrees (fixture D)."));
        var e = NewProject(); PresetFields.GrooveType.Edit(e, GrooveTypes.UndercutU); Save("locked-groove-type", e, Describe("New project with the groove type locked (fixture E)."));
        var h = NewWithQ(1000); ProjectPresets.Find("Calculations.CounterweightMassKg").Edit(h, 1600.0); Save("locked-counterweight-q1000", h, Describe("Q 1000 with the counterweight locked at 1600 kg (fixture H)."));
        var i = NewWithQ(1600); ProjectPresets.Find(ProjectPresets.SheaveKey).Edit(i, 520.0); Save("locked-sheave-q1600", i, Describe("Q 1600 with the traction sheave locked at 520 mm (fixture I)."));
        // Calculation-sheet fixtures (CalculationSheetTests 01-08, 14, 15, 20, 21, 24).
        Save("basic-complete", BasicComplete("Golden A - Basic complete"), Describe("Basic complete: W 1100, D 2100, Q 480, h 1200 (sheet test 01; FF 2200 / 2100 N, P5 9800, P6 39200, P7 29600 / 14800 N)."));
        Save("advanced-complete", AdvancedComplete("Golden B - Advanced complete"), Describe("Advanced complete (sheet test 02)."));
        var mh = BasicComplete("Golden C - missing h"); mh.Calculations.CarGuideForces = new CarGuideForceInputs(); Save("missing-h", mh, Describe("Basic complete without h (sheet test 03)."));
        var mm = BasicComplete("Golden D - missing masses and pit"); mm.Calculations.CarMassKg = null; mm.Calculations.SafetyPit = null; Save("missing-masses-and-pit", mm, Describe("Car mass and pit choices missing (sheet test 04)."));
        var mt = BasicComplete("Golden E - malformed text"); mt.Calculations.CarMassKg = null; mt.Calculations.CarGuideForces.GuideShoeSpacingMm = null;
        var mo = Describe("Malformed UI text for the car mass and h (sheet test 05): draft errors passed to the sheet.");
        mo.CarGuideForceDraftError = "Vertical upper-to-lower car guide-shoe spacing h (mm): invalid text; enter a finite positive number.";
        mo.ChassisDraftErrors = new Dictionary<string, string> { { "Calculations.CarMassKg", "Empty car mass: finite number required." } };
        mo.SafetyPitDraftErrors = new Dictionary<string, string> { { "Calculations.CarMassKg", "Empty car mass: finite number required. Malformed text is not an omitted mass." } };
        Save("malformed-text", mt, mo);
        var mv = BasicComplete("Golden F - malformed values"); mv.Elevator.CapacityKg = -480; mv.Calculations.CarGuideForces.GuideShoeSpacingMm = 0; mv.Calculations.SafetyPit.CarGuideCount = 0; mv.Suspension.RopeCount = 0;
        Save("malformed-values", mv, Describe("Out-of-range values in the project file: negative Q, h 0, no car guide rails, 0 ropes."));
        var nr = BasicComplete("Golden G - no rope count"); nr.Suspension.RopeCount = null; Save("missing-rope-count", nr, Describe("No rope count (sheet test 07)."));
        var ac = AdvancedComplete("Golden H - Advanced missing CG basis"); ac.Calculations.MachineCgSource = null; Save("advanced-missing-cg", ac, Describe("Advanced without the machine CG basis (sheet test 08)."));
        var r1 = BasicComplete("Golden M - ropes and traction"); r1.Shaft.DepthMm = 2100; r1.Suspension.RopeDiameterMm = 10; r1.Suspension.RopeCount = 4; Save("rope-traction-ok", r1, Describe("Rope / traction complete (sheet test 14)."));
        var r2 = BasicComplete("Golden N - rope NOT OK"); r2.Shaft.DepthMm = 1600; Save("rope-traction-not-ok", r2, Describe("Rope data NOT OK on the 250 mm deflector (sheet test 15)."));
        var tb = BasicComplete("Golden T1"); tb.Report = new ProjectReportDetails { ClientName = "Delta Developments", DocumentNumber = "NL-CS-001", Revision = "A", PreparedBy = "A. Engineer", CheckedBy = "B. Checker", ApprovedBy = "C. Approver" };
        var to = Describe("Title block complete: report fields, company name and logo (sheet test 20)."); to.CompanyName = "Nile Lifts Engineering"; to.LogoFile = "Elevator.Solution.Desktop/Assets/ShaftMasterLogo.png";
        Save("titleblock-filled", tb, to);
        var tbb = BasicComplete("Golden T2"); tbb.Report = new ProjectReportDetails { ClientName = "  ", DocumentNumber = "", Revision = null, PreparedBy = " ", CheckedBy = null, ApprovedBy = "" };
        var tbo = Describe("Title block blank, including whitespace-only text (sheet test 21)."); tbo.CompanyName = "  ";
        Save("titleblock-blank", tbb, tbo);
        // Older projects: the reviewed v01 example as shipped, and a project file without the Report group (sheet test 24).
        File.Copy(Path.Combine(root, "examples", "ElevatorProject.example.json"), Path.Combine(fixtures, "older-v01-example.elevator.json"));
        File.WriteAllText(Path.Combine(fixtures, "older-v01-example.options.json"), Pretty(Describe("Reviewed v01-format example project, file as shipped (examples/ElevatorProject.example.json).")) + "\n", Utf8);
        var js = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
        var raw = js.DeserializeObject(ProjectJson.Serialize(BasicComplete("Golden T5"))) as Dictionary<string, object>; ((Dictionary<string, object>)raw["project"]).Remove("Report");
        File.WriteAllText(Path.Combine(fixtures, "older-no-report-group.elevator.json"), js.Serialize(raw), Utf8);
        File.WriteAllText(Path.Combine(fixtures, "older-no-report-group.options.json"), Pretty(Describe("Older project file without the Report group (sheet test 24).")) + "\n", Utf8);
    }
}
