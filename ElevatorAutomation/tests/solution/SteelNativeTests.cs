using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elevator.Core.Application;
using Elevator.Core.Calculations;
using Elevator.Core.Company;
using Elevator.Core.Layouts;
using Elevator.Core.MachineRoom;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.SafetyGear;
using Elevator.Core.Steel;
using Elevator.Solution.Bridge;

// S3 tier 3 (steel-native): a steel shaft in the four generated views and on the A0 sheet. Two steel projects (the client's: shaft
// 1500 x 2000, 4 stops, floors 3750, pit 1150, overhead 4100; and the default steel project) and the same client project as concrete are
// generated through the packaged GenerationClient and the AutoCAD 2023 Core Console host with a SheetRequest. The expected tower
// (columns, beam levels per face, bracing panels) is written beside each DWG for tests/integration/verify_steel.py, which reopens the
// DWGs (DXFOUT + ezdxf) and checks the entities.
public static class SteelNativeTests
{
    static string bin, output, root;
    static int count;
    static readonly List<string> names = new List<string>();
    static void Check(bool value, string name) { if (!value) throw new Exception(name); ++count; names.Add(name); }

    static ElevatorProject Project(PresetLibrary lib, DesignContext ctx, string name, int stops, double floorHeight, double w, double d, double pit, double overhead, bool steel)
    {
        var seed = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json")));
        var p = ProjectPresets.CreateNew(lib, ctx, seed.Counterweight, seed.Rails.BracketSource);
        p.Info.Name = name;
        foreach (var k in new[] { "Shaft.WidthMm", "Shaft.DepthMm", "Shaft.PitDepthMm", "Shaft.OverheadMm" })
            ProjectPresets.Lock(p, k);
        p.Shaft.WidthMm = w;
        p.Shaft.DepthMm = d;
        p.Shaft.PitDepthMm = pit;
        p.Shaft.OverheadMm = overhead;
        var floors = new List<FloorDefinition>();
        for (int i = 0; i < stops; i++)
            floors.Add(new FloorDefinition { Name = i == 0 ? "G" : i.ToString(), HeightToNextMm = i < stops - 1 ? (double?)floorHeight : null });
        p.Building.Floors = floors;
        p.Car.DisplayFloorId = floors[0].FloorId;
        if (steel)
            SteelFields.Construction.Edit(p, SteelShaftPreset.SteelStructure);
        ProjectPresets.Apply(p, lib, ctx);
        return p;
    }

    static GenerationRequest Request(ElevatorProject p, PresetLibrary lib, DesignContext ctx, bool steel)
    {
        var company = new CompanySettings { CompanyName = "Nile Lifts Engineering" };
        IList<BearingReaction> reactions = null;
        SafetyPitCalculationResult pit = null;
        try { reactions = new CalculationWorkspace().Calculate(p, ctx).Reactions; } catch (Exception) { }
        try { pit = new SafetyPitCalculationEngine().Calculate(p); } catch (Exception) { }
        var title = TitleBlockData.Build(p, lib, company, reactions, pit, new DateTime(2026, 10, 13));
        var sheet = new SheetRequest { DoorType = p.Doors == null ? null : p.Doors.Type, Attributes = title.Attributes(), SteelStructure = steel };
        return new GenerationRequest { ProjectJson = ProjectJson.Serialize(p), Options = new GenerationOptions { Views = DrawingViews.All, ArrangeViews = true }, Sheet = sheet };
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
            var client = new GenerationClient(root, bin, @"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe");
            var cases = new[]
            {
                new { Id = "steel-client", Steel = true, Stops = 4, Floor = 3750.0, W = 1500.0, D = 2000.0, Pit = 1150.0, OH = 4100.0, File = (string)null },
                new { Id = "steel-default", Steel = true, Stops = 5, Floor = 3000.0, W = 1600.0, D = 1800.0, Pit = 1300.0, OH = 4200.0, File = (string)null },
                new { Id = "concrete-client", Steel = false, Stops = 4, Floor = 3750.0, W = 1500.0, D = 2000.0, Pit = 1150.0, OH = 4100.0, File = (string)null }
            }.ToList();
            // a project file of a user (S4): STEEL_USER_PROJECT=<file> generates it as the case "user-project"
            string userFile = Environment.GetEnvironmentVariable("STEEL_USER_PROJECT");
            if (!String.IsNullOrEmpty(userFile))
                cases.Add(new { Id = "user-project", Steel = true, Stops = 0, Floor = 0.0, W = 0.0, D = 0.0, Pit = 0.0, OH = 0.0, File = userFile });
            string only = Environment.GetEnvironmentVariable("STEEL_CASES");
            foreach (var c in cases.Where(x => String.IsNullOrEmpty(only) || only.Split(',').Any(o => x.Id.Contains(o))))
            {
                var p = c.File != null ? ProjectJson.Deserialize(File.ReadAllText(c.File)) : Project(lib, ctx, "Villa 34 - " + c.Id, c.Stops, c.Floor, c.W, c.D, c.Pit, c.OH, c.Steel);
                Check(SteelFields.IsSteel(p, lib) == c.Steel, c.Id + ": the construction is " + (c.Steel ? "steel" : "concrete"));
                var design = new ElevatorDesignEngine().Calculate(p, ctx, DrawingViews.All);
                Check(design.CanGenerate && design.Plan != null, c.Id + ": the design allows generation");
                var request = Request(p, lib, ctx, c.Steel);
                File.WriteAllText(Path.Combine(output, c.Id + ".elevator.json"), request.ProjectJson);
                var target = Path.Combine(output, c.Id + ".dwg");
                var response = client.Run(request, OutputConsent.Capture(target, false), Console.WriteLine);
                Wire.Write(Path.Combine(output, c.Id + "-generation.json"), response);
                Check(response.Success && File.Exists(target), c.Id + ": generated" + (response.Success ? "" : " (" + response.Error + ")"));
                Check(response.ViewsGenerated.Length == 4, c.Id + ": four views");
                Check(response.Sheet != null && response.Sheet.NotPlaced.Count == 0 && response.Sheet.Viewports.Count == 8, c.Id + ": the sheet has the four views and four details");
                var verified = Wire.Read<GenerationResponse>(Path.Combine(client.LastJobDirectory, "verified.json"));
                Check(verified.Success, c.Id + ": reopened and audited");
                Check(response.EntityCounts.OrderBy(k => k.Key).SequenceEqual(verified.EntityCounts.OrderBy(k => k.Key)), c.Id + ": generated and reopened view counts match");
                object tower = null;
                if (c.Steel)
                {
                    var s = SteelStructure.Build(p, lib);
                    Check(s.Valid, c.Id + ": the steel structure is valid");
                    // the rails drive the beams: s is the car rail bracket spacing, no gap is larger than it on the side and back faces
                    double sp = s.Inputs.SpacingMm;
                    foreach (var face in SteelStructure.BracketFaces)
                    {
                        var levels = s.Levels[face].Select(l => l.ElevationMm).ToList();
                        for (int i = 1; i < levels.Count; i++)
                            Check(levels[i] - levels[i - 1] <= sp + 1e-6 && levels[i] - levels[i - 1] > 1, c.Id + ": " + face + " gap " + (levels[i] - levels[i - 1]).ToString("0.#") + " within " + sp);
                    }
                    var left = s.Faces.First(f => f.Name == "Left");
                    tower = new
                    {
                        s.Inputs.SpacingMm,
                        Width = s.Inputs.WidthMm,
                        Depth = s.Inputs.DepthMm,
                        BaseMm = s.BaseMm,
                        TopMm = s.TopMm,
                        BoltCount = s.Inputs.BoltCount,
                        Column = s.Inputs.Column.Name,
                        Beam = s.Inputs.Beam.Name,
                        Brace = s.Inputs.Brace.Name,
                        Levels = s.Levels.ToDictionary(k => k.Key, k => k.Value.Select(l => l.ElevationMm).ToList()),
                        LeftBraces = left.Braces.Count,
                        LeftGussets = left.Gussets.Count(g => g.Y > s.BaseMm + 1e-6),
                        LeftBeams = left.Beams.Count,
                        FrontBeams = s.Faces.First(f => f.Name == "Front").Beams.Count,
                        BackBeams = s.Faces.First(f => f.Name == "Back").Beams.Count,
                        Plates = left.Plates.Count
                    };
                }
                Wire.Write(Path.Combine(output, c.Id + "-expected.json"), new
                {
                    c.Id,
                    c.Steel,
                    Tower = tower,
                    Viewports = response.Sheet == null ? null : response.Sheet.Viewports,
                    response.EntityCounts
                });
            }
            exit = 0;
            Console.WriteLine("PASS: " + count + " steel native assertions.");
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(output, "steel-native-error.txt"), e.ToString()); Console.Error.WriteLine(e); }
        File.WriteAllText(Path.Combine(output, "steel-native-tests.json"), Wire.Json(new { Status = exit == 0 ? "PASS" : "FAIL", Assertions = count, Checks = names }));
        return exit;
    }
}
