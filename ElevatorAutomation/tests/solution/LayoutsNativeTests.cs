using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Elevator.Core.Application;
using Elevator.Core.Calculations;
using Elevator.Core.Company;
using Elevator.Core.Doors;
using Elevator.Core.Layouts;
using Elevator.Core.MachineRoom;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.SafetyGear;
using Elevator.Solution.Bridge;

// L2b tier 3 (layouts-native): the A0 geared sheet in generated drawings. Four projects (2 stops 1600 x 1800, 6 stops with Arabic names,
// 15 stops, one Automatic 2S door) are generated through the packaged GenerationClient and the AutoCAD 2023 Core Console host with a
// SheetRequest; the request, the response and the expected title-block values are written beside each DWG for
// tests/integration/verify_layouts.py, which reopens the DWGs (DXFOUT + ezdxf) and checks the layout, the viewports and the attributes.
// The first project is also generated WITHOUT a sheet: the four views, their entity counts and bounds must be the same.
public static class LayoutsNativeTests
{
    static string bin, output, root;
    static int count;
    static readonly List<string> names = new List<string>();
    static void Check(bool value, string name) { if (!value) throw new Exception(name); ++count; names.Add(name); }

    static ElevatorProject Project(PresetLibrary lib, DesignContext ctx, int stops, double width, double depth, string name, string door)
    {
        var seed = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json")));
        var p = ProjectPresets.CreateNew(lib, ctx, seed.Counterweight, seed.Rails.BracketSource);
        p.Info.Name = name;
        ProjectPresets.Lock(p, "Shaft.WidthMm");
        ProjectPresets.Lock(p, "Shaft.DepthMm");
        p.Shaft.WidthMm = width;
        p.Shaft.DepthMm = depth;
        var first = p.Building.Floors[0];
        var floors = new List<FloorDefinition> { first };
        for (int i = 1; i < stops; i++)
            floors.Add(new FloorDefinition { Name = i.ToString(), HeightToNextMm = 3000 });
        first.HeightToNextMm = 3000;
        floors[floors.Count - 1].HeightToNextMm = null;
        p.Building.Floors = floors;
        if (door != null)
        {
            DoorFlow.SetType(p, door);
            DoorFlow.SetNumber(p, DoorSettings.OpeningKey, 800);
        }
        return p;
    }

    static string MakeLogo(string path)
    {
        using (var bmp = new Bitmap(300, 150))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.FillEllipse(Brushes.SteelBlue, 10, 10, 130, 130);
            g.FillRectangle(Brushes.DarkOrange, 160, 30, 130, 90);
            using (var f = new Font("Arial", 22f, FontStyle.Bold))
                g.DrawString("LOGO", f, Brushes.White, 172, 52);
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        return path;
    }

    public static int Main(string[] args)
    {
        root = Path.GetFullPath(args[0]); bin = Path.GetFullPath(args[1]); output = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(output);
        int exit = 1;
        try
        {
            var ctx = Wire.Read<DesignContext>(Path.Combine(bin, "reviewed-context.json"));
            ctx.ChassisEvidence = Wire.Read<ChassisGeometryEvidence>(Path.Combine(bin, "reviewed-chassis-evidence.json"));
            var lib = PresetLibrary.Load(Path.Combine(bin, "presets.json"));
            ctx.Doors = lib.DoorsOrDefault();
            string logo = MakeLogo(Path.Combine(output, "test-logo.png"));
            var client = new GenerationClient(root, bin, @"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe");
            var cases = new[]
            {
                new { Id = "2-stops-1600x1800", Stops = 2, W = 1600.0, D = 1800.0, Door = (string)null, Arabic = false, Baseline = true, Long = false },
                new { Id = "6-stops-1600x1800-arabic", Stops = 6, W = 1600.0, D = 1800.0, Door = (string)null, Arabic = true, Baseline = false, Long = false },
                new { Id = "15-stops-1600x1800", Stops = 15, W = 1600.0, D = 1800.0, Door = (string)null, Arabic = false, Baseline = false, Long = false },
                new { Id = "2S-800-4-stops-1720x2800", Stops = 4, W = 1720.0, D = 2800.0, Door = DoorTypes.Automatic2S, Arabic = false, Baseline = false, Long = false },
                // the longest realistic values of every attribute with a box: names, numbers, 20 stop names (the box check)
                new { Id = "20-stops-long-values", Stops = 20, W = 1600.0, D = 1800.0, Door = (string)null, Arabic = false, Baseline = false, Long = true }
            };
            // LAYOUTS_CASES: a comma-separated list of case id parts, to run a subset (debugging)
            string only = Environment.GetEnvironmentVariable("LAYOUTS_CASES");
            foreach (var c in cases.Where(x => String.IsNullOrEmpty(only) || only.Split(',').Any(o => x.Id.Contains(o))))
            {
                string name = c.Long ? "Residential Villa bx12 - Fifth Settlement - New Cairo - Block 7 - Plot 18"
                    : c.Arabic ? "فيلا التجمع الخامس" : "Villa bx12 - " + c.Id;
                var p = Project(lib, ctx, c.Stops, c.W, c.D, name, c.Door);
                p.Report = new ProjectReportDetails
                {
                    ClientName = c.Long ? "السيد المهندس محمد عبد الرحمن فتحي السيد وشركاه للمقاولات العامة والتوريدات"
                        : c.Arabic ? "السيد أحمد محمود" : "Delta Developments",
                    DocumentNumber = c.Long ? "NL-2026-ELV-0014-REV-03" : "NL-" + c.Stops.ToString("00"),
                    Revision = "1",
                    PreparedBy = c.Long ? "Eng. Mohamed Abdelrahman Fathy" : c.Arabic ? "م. محمد علي" : "A. Engineer",
                    CheckedBy = c.Long ? "Eng. Mohamed Abdelrahman Fathy" : "B. Checker",
                    ApprovedBy = c.Long ? "Eng. Mohamed Abdelrahman Fathy" : "C. Approver"
                };
                DocumentData.SetPhase(p, c.Long ? "Execution drawings" : c.Arabic ? "المرحلة الأولى" : "Tender");
                var company = new CompanySettings
                {
                    CompanyName = c.Long ? "Elevators and Escalators Engineering and Trading Company Ltd."
                        : c.Arabic ? "شركة النيل للمصاعد" : "Nile Lifts Engineering"
                };
                var design = new ElevatorDesignEngine().Calculate(p, ctx, DrawingViews.All);
                Check(design.CanGenerate && design.Plan != null, c.Id + ": the design allows generation");
                IList<BearingReaction> reactions = null;
                SafetyPitCalculationResult pit = null;
                try { reactions = new CalculationWorkspace().Calculate(p, ctx).Reactions; } catch (Exception) { }
                try { pit = new SafetyPitCalculationEngine().Calculate(p); } catch (Exception) { }
                var title = TitleBlockData.Build(p, lib, company, reactions, pit, new DateTime(2026, 10, 12));
                var attributes = title.Attributes();
                var sheet = new SheetRequest { LogoFile = logo, LogoWidthPx = 300, LogoHeightPx = 150, DoorType = p.Doors == null ? null : p.Doors.Type, Attributes = attributes };
                var request = new GenerationRequest { ProjectJson = ProjectJson.Serialize(p), Options = new GenerationOptions { Views = DrawingViews.All, ArrangeViews = true }, Sheet = sheet };
                File.WriteAllText(Path.Combine(output, c.Id + ".elevator.json"), request.ProjectJson);
                var target = Path.Combine(output, c.Id + ".dwg");
                var response = client.Run(request, OutputConsent.Capture(target, false), Console.WriteLine);
                Wire.Write(Path.Combine(output, c.Id + "-generation.json"), response);
                Check(response.Success && File.Exists(target), c.Id + ": generated with the sheet" + (response.Success ? "" : " (" + response.Error + ")"));
                Check(response.ViewsGenerated.Length == 4, c.Id + ": four views");
                Check(response.Sheet != null && response.Sheet.LayoutName == SheetLayout.Name, c.Id + ": the host reports the sheet layout");
                Check(response.Sheet.NotPlaced.Count == 0 && response.Sheet.Viewports.Count == 8, c.Id + ": four views and four reference details placed");
                // one title per view: a viewport has a scale label only when its view has no title of its own that states the scale
                foreach (var v in response.Sheet.Viewports)
                    Check(v.HasLabel != (v.Titles.Count > 0), c.Id + ": " + v.Name + " has either its own title or a scale label, not both");
                foreach (var v in response.Sheet.Viewports.Where(x => x.Kind != "Detail"))
                    Check(v.Titles.Count > 0 && v.Titles.All(t => t.Contains(v.Label)), c.Id + ": the title of " + v.Name + " states " + v.Label + " (" + String.Join(" / ", v.Titles) + ")");
                // every attribute fits its box (the longest realistic values in the last project)
                foreach (var a in response.Sheet.Attributes)
                    Check(a.Fits && (a.AvailableWidthMm == 0 || a.WidthMm <= a.AvailableWidthMm + 0.05), c.Id + ": attribute " + a.Tag + " fits its box (" + a.WidthMm.ToString("0.0") + " of " + a.AvailableWidthMm.ToString("0.0") + " mm, width factor " + a.WidthFactor.ToString("0.00") + ", height " + a.HeightMm.ToString("0.00") + ", " + a.Lines + " line(s))");
                Check(response.Sheet.LogoPlaced, c.Id + ": the logo is placed (" + response.Sheet.LogoMessage + ")");
                var verified = Wire.Read<GenerationResponse>(Path.Combine(client.LastJobDirectory, "verified.json"));
                Check(verified.Success, c.Id + ": reopened and audited");
                Check(response.EntityCounts.OrderBy(k => k.Key).SequenceEqual(verified.EntityCounts.OrderBy(k => k.Key)), c.Id + ": generated and reopened view counts match");
                Wire.Write(Path.Combine(output, c.Id + "-expected.json"), new
                {
                    c.Id,
                    Layout = SheetLayout.Name,
                    Area = new { response.Sheet.DrawingAreaX, response.Sheet.DrawingAreaY, response.Sheet.DrawingAreaWidth, response.Sheet.DrawingAreaHeight },
                    Attributes = attributes.Select(a => new { a.Tag, a.Value, a.Style, a.DrawingText }).ToList(),
                    Viewports = response.Sheet.Viewports,
                    Fit = response.Sheet.Attributes,
                    response.EntityCounts,
                    response.Bounds,
                    Title = title
                });
                if (c.Baseline)
                {
                    var plain = new GenerationRequest { ProjectJson = request.ProjectJson, Options = request.Options };
                    var bare = client.Run(plain, OutputConsent.Capture(Path.Combine(output, c.Id + "-nosheet.dwg"), false), Console.WriteLine);
                    Check(bare.Success && bare.Sheet == null, c.Id + ": generated without a sheet");
                    Check(bare.EntityCounts.OrderBy(k => k.Key).SequenceEqual(response.EntityCounts.OrderBy(k => k.Key)), c.Id + ": the sheet does not change the view entity counts");
                    Check(bare.Bounds.Count == response.Bounds.Count && bare.Bounds.Zip(response.Bounds, (a, b) =>
                        a.View == b.View && Math.Abs(a.MinX - b.MinX) < 1e-6 && Math.Abs(a.MinY - b.MinY) < 1e-6 && Math.Abs(a.MaxX - b.MaxX) < 1e-6 && Math.Abs(a.MaxY - b.MaxY) < 1e-6).All(x => x),
                        c.Id + ": the sheet does not move the views");
                }
            }
            exit = 0;
            Console.WriteLine("PASS: " + count + " layouts native assertions.");
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(output, "layouts-native-error.txt"), e.ToString()); Console.Error.WriteLine(e); }
        File.WriteAllText(Path.Combine(output, "layouts-native-tests.json"), Wire.Json(new { Status = exit == 0 ? "PASS" : "FAIL", Assertions = count, Checks = names }));
        return exit;
    }
}
