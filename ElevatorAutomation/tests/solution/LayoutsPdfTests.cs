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

// L3 tier 3 (layouts-pdf): the output set of one Generate Drawings run through the packaged OutputSetWriter and the AutoCAD 2023 Core
// Console: DWG with the sheet, PDF of that sheet (plot engine, then the logo image put on the page), project file, one base name.
// Two projects: 6 stops with a logo and an Arabic company; Automatic 2S without a logo. tests/integration/verify_pdf.py reads the PDFs
// (PyMuPDF): one page of 1189 x 841 mm, the project name and "P1 =" in the text, the logo image object only when a logo is set.
public static class LayoutsPdfTests
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

    static void MakeLogo(string path)
    {
        using (var bmp = new Bitmap(300, 150))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.FillEllipse(Brushes.SteelBlue, 10, 10, 130, 130);
            g.FillRectangle(Brushes.DarkOrange, 160, 30, 130, 90);
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
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
            string logoFile = Path.Combine(output, "logo.png");
            MakeLogo(logoFile);
            var client = new GenerationClient(root, bin, @"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe");
            var cases = new[]
            {
                new { Id = "6-stops-arabic-logo", Stops = 6, W = 1600.0, D = 1800.0, Door = (string)null, Arabic = true, Logo = true },
                new { Id = "2S-4-stops-no-logo", Stops = 4, W = 1720.0, D = 2800.0, Door = DoorTypes.Automatic2S, Arabic = false, Logo = false }
            };
            var summary = new List<object>();
            foreach (var c in cases)
            {
                string name = c.Arabic ? "\u0641\u064a\u0644\u0627 \u0627\u0644\u062a\u062c\u0645\u0639 \u0627\u0644\u062e\u0627\u0645\u0633" : "Villa bx12 - 2S";
                var p = Project(lib, ctx, c.Stops, c.W, c.D, name, c.Door);
                p.Report = new ProjectReportDetails
                {
                    ClientName = c.Arabic ? "\u0627\u0644\u0633\u064a\u062f \u0623\u062d\u0645\u062f \u0645\u062d\u0645\u0648\u062f" : "Delta Developments",
                    DocumentNumber = "NL-" + c.Stops.ToString("00"),
                    Revision = "",
                    PreparedBy = "A. Engineer",
                    CheckedBy = "B. Checker",
                    ApprovedBy = "C. Approver"
                };
                DocumentData.SetPhase(p, "Tender");
                var company = new CompanySettings { CompanyName = c.Arabic ? "\u0634\u0631\u0643\u0629 \u0627\u0644\u0646\u064a\u0644 \u0644\u0644\u0645\u0635\u0627\u0639\u062f" : "Nile Lifts Engineering" };
                var design = new ElevatorDesignEngine().Calculate(p, ctx, DrawingViews.All);
                Check(design.CanGenerate && design.Plan != null, c.Id + ": the design allows generation");
                IList<BearingReaction> reactions = null;
                SafetyPitCalculationResult pit = null;
                try { reactions = new CalculationWorkspace().Calculate(p, ctx).Reactions; } catch (Exception) { }
                try { pit = new SafetyPitCalculationEngine().Calculate(p); } catch (Exception) { }
                var title = TitleBlockData.Build(p, lib, company, reactions, pit, new DateTime(2026, 10, 12));
                var sheet = new SheetRequest { Attributes = title.Attributes(), DoorType = p.Doors == null ? null : p.Doors.Type };
                if (c.Logo)
                {
                    sheet.LogoFile = logoFile;
                    sheet.LogoWidthPx = 300;
                    sheet.LogoHeightPx = 150;
                }
                string baseName = DrawingFileName.BaseName(p, company);
                var files = DrawingFileName.Files(baseName);
                string folder = Path.Combine(output, c.Id);
                var set = OutputSetWriter.Write(client, new OutputSetRequest
                {
                    Project = p,
                    Options = new GenerationOptions { Views = DrawingViews.All, ArrangeViews = true },
                    Sheet = sheet,
                    Folder = folder,
                    Names = files,
                    Overwrite = false,
                    Progress = Console.WriteLine
                });
                Check(set.Generation != null && set.Generation.Success, c.Id + ": generated" + (set.Generation != null && !set.Generation.Success ? " (" + set.Generation.Error + ")" : ""));
                Check(set.PdfError == null, c.Id + ": the PDF step has no error (" + set.PdfError + ")");
                Check(set.ProjectError == null, c.Id + ": the project file step has no error");
                Check(File.Exists(set.Dwg) && File.Exists(set.Pdf) && File.Exists(set.ProjectFile), c.Id + ": the three files exist");
                Check(Path.GetFileName(set.Dwg) == baseName + ".dwg" && Path.GetFileName(set.Pdf) == baseName + ".pdf"
                    && Path.GetFileName(set.ProjectFile) == baseName + ".elevator.json", c.Id + ": one base name " + baseName);
                Check(Directory.GetFiles(folder).Length == 3, c.Id + ": exactly the three files are in the output folder");
                var reopened = ProjectJson.Deserialize(File.ReadAllText(set.ProjectFile));
                Check(reopened.Info.Name == p.Info.Name, c.Id + ": the project file holds the project");
                Check(set.Generation.Sheet != null && set.Generation.Sheet.LogoPlaced == c.Logo, c.Id + ": the DWG has the logo image only when a logo is set");
                // a second run into the same folder must not replace silently
                bool refused = false;
                try
                {
                    OutputSetWriter.Write(client, new OutputSetRequest
                    {
                        Project = p,
                        Options = new GenerationOptions { Views = DrawingViews.All, ArrangeViews = true },
                        Sheet = sheet,
                        Folder = folder,
                        Names = files,
                        Overwrite = false
                    });
                }
                catch (IOException) { refused = true; }
                Check(refused, c.Id + ": existing files are never replaced silently");
                summary.Add(new
                {
                    c.Id,
                    Folder = folder,
                    Dwg = set.Dwg,
                    Pdf = set.Pdf,
                    ProjectFile = set.ProjectFile,
                    ProjectName = p.Info.Name,
                    // the project name as the drawing holds it (the Arabic name is written in the xarab key codes of the SHX font)
                    ProjectNameInDrawing = sheet.Attributes.Where(t => t.Tag == "PROJECT").Select(t => t.DrawingText ?? t.Value).FirstOrDefault(),
                    HasLogo = c.Logo,
                    LogoAreaX = set.Generation.Sheet.LogoAreaX,
                    LogoAreaY = set.Generation.Sheet.LogoAreaY,
                    LogoAreaWidth = set.Generation.Sheet.LogoAreaWidth,
                    LogoAreaHeight = set.Generation.Sheet.LogoAreaHeight
                });
            }
            Wire.Write(Path.Combine(output, "layouts-pdf-cases.json"), summary);
            exit = 0;
            Console.WriteLine("PASS: " + count + " layouts pdf assertions.");
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(output, "layouts-pdf-error.txt"), e.ToString()); Console.Error.WriteLine(e); }
        File.WriteAllText(Path.Combine(output, "layouts-pdf-tests.json"), Wire.Json(new { Status = exit == 0 ? "PASS" : "FAIL", Assertions = count, Checks = names }));
        return exit;
    }
}
