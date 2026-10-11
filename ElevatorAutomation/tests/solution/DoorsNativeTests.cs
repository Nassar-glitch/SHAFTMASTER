using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Elevator.Core.Doors;
using Elevator.Core.Presets;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;
using Elevator.Solution.Bridge;

// D2 / D3: automatic doors (2S, 2C) in the native drawings. Four-view DWGs are generated and reopened (the packaged host verifies the
// save / reopen); the expected door layout of every case is written beside the DWG for tests/integration/verify_doors.py, which reads
// the door geometry back from the DWG and compares it with the DoorLayout numbers.
public static class DoorsNativeTests
{
    static string bin, output, root;
    static int count;
    static readonly List<string> names = new List<string>();
    static void Check(bool value, string name) { if (!value) throw new Exception(name); ++count; names.Add(name); }

    static ElevatorProject Project(PresetLibrary lib, DesignContext ctx, string type, double width, double depth, double opening, string name, string handing)
    {
        var seed = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json")));
        var p = ProjectPresets.CreateNew(lib, ctx, seed.Counterweight, seed.Rails.BracketSource);
        p.Info.Name = name;
        ProjectPresets.Lock(p, "Shaft.WidthMm");
        ProjectPresets.Lock(p, "Shaft.DepthMm");
        p.Shaft.WidthMm = width;
        p.Shaft.DepthMm = depth;
        DoorFlow.SetType(p, type);
        DoorFlow.SetNumber(p, DoorSettings.OpeningKey, opening);
        if (handing != null)
            DoorFlow.SetHanding(p, handing);
        return p;
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
            // D3: the project the user generated and then corrected by hand in AutoCAD (2S 700 on a 1320 x 1800 shaft), stored from
            // the DWG; and the mirrored (left-handed) 2S, to check that every frame rule holds for both handings.
            const string UserProject = "docs/design/doors-d3/user-edit-2S-700-1320.elevator.json";
            var cases = new[]
            {
                new { Id = "2S-800-1720", Type = DoorTypes.Automatic2S, Width = 1720.0, Depth = 2800.0, Opening = 800.0, Shifted = false, Handing = (string)null, Fixture = (string)null },
                new { Id = "2C-800-1720", Type = DoorTypes.Automatic2C, Width = 1720.0, Depth = 2800.0, Opening = 800.0, Shifted = false, Handing = (string)null, Fixture = (string)null },
                new { Id = "2S-700-1300", Type = DoorTypes.Automatic2S, Width = 1300.0, Depth = 2800.0, Opening = 700.0, Shifted = true, Handing = (string)null, Fixture = (string)null },
                new { Id = "2S-700-1320", Type = DoorTypes.Automatic2S, Width = 1320.0, Depth = 1800.0, Opening = 700.0, Shifted = true, Handing = (string)null, Fixture = UserProject },
                new { Id = "2S-800-1720-left", Type = DoorTypes.Automatic2S, Width = 1720.0, Depth = 2800.0, Opening = 800.0, Shifted = false, Handing = DoorTypes.Left, Fixture = (string)null },
                new { Id = "2C-900-1900", Type = DoorTypes.Automatic2C, Width = 1900.0, Depth = 2800.0, Opening = 900.0, Shifted = false, Handing = (string)null, Fixture = (string)null }
            };
            var client = new GenerationClient(root, bin, @"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe");
            foreach (var c in cases)
            {
                var p = c.Fixture != null
                    ? ProjectJson.Deserialize(File.ReadAllText(Path.Combine(root, c.Fixture.Replace('/', Path.DirectorySeparatorChar))))
                    : Project(lib, ctx, c.Type, c.Width, c.Depth, c.Opening, "Doors " + c.Id, c.Handing);
                var design = new ElevatorDesignEngine().Calculate(p, ctx, DrawingViews.All);
                Check(design.CanGenerate && design.Plan != null, c.Id + ": shared design allows generation");
                var layout = DoorRules.Evaluate(p, ctx.Doors, design.Plan);
                Check(layout.Fits && layout.Shifted == c.Shifted && layout.OpeningMm == c.Opening, c.Id + ": door fits (shifted " + c.Shifted + ")");
                var request = new GenerationRequest { ProjectJson = ProjectJson.Serialize(p), Options = new GenerationOptions { Views = DrawingViews.All, ArrangeViews = true } };
                File.WriteAllText(Path.Combine(output, c.Id + ".elevator.json"), request.ProjectJson);
                var target = Path.Combine(output, c.Id + ".dwg");
                var response = client.Run(request, OutputConsent.Capture(target, false), Console.WriteLine);
                Wire.Write(Path.Combine(output, c.Id + "-generation.json"), response);
                Check(response.Success && response.ViewsGenerated.Length == 4 && File.Exists(target), c.Id + ": four native views generated");
                var verified = Wire.Read<GenerationResponse>(Path.Combine(client.LastJobDirectory, "verified.json"));
                Check(verified.Success && verified.InputHash == Wire.HashText(request.ProjectJson), c.Id + ": fresh native verification matches the input");
                Check(response.EntityCounts.OrderBy(k => k.Key).SequenceEqual(verified.EntityCounts.OrderBy(k => k.Key)), c.Id + ": generated and reopened component counts match");
                Wire.Write(Path.Combine(output, c.Id + "-expected.json"), new { c.Id, c.Type, Layout = layout, Plan = design.Plan, Counts = response.EntityCounts });
            }
            exit = 0;
            Console.WriteLine("PASS: " + count + " doors native assertions.");
        }
        catch (Exception e) { Wire.Write(Path.Combine(output, "doors-native-error.json"), new { Error = e.ToString() }); Console.Error.WriteLine(e); }
        Wire.Write(Path.Combine(output, "doors-native-tests.json"), new { Status = exit == 0 ? "PASS" : "FAIL", Assertions = count, Checks = names, Method = "Packaged GenerationClient and AutoCAD 2023 Core Console host; the door geometry is compared by verify_doors.py." });
        return exit;
    }
}
