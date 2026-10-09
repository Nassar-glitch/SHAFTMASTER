using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Elevator.Core;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.Solution.Bridge;

// DESIGN-FLOW stage 2: a new project built from the presets, with no user input, generates all four native views through
// the packaged AutoCAD host and survives the native save / reopen check.
public static class PresetsNativeTests
{
    static string bin, output, root;
    static int count;
    static readonly List<string> names = new List<string>();
    static void Check(bool value, string name) { if (!value) throw new Exception(name); ++count; names.Add(name); }
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
            var seed = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin, "reviewed-example.elevator.json")));
            // Same construction as ShaftMaster's New Project.
            var p = ProjectPresets.CreateNew(lib, ctx, seed.Counterweight, seed.Rails.BracketSource);
            Check(p.Elevator.CapacityKg == 630 && p.Shaft.WidthMm == 1500 && p.Shaft.DepthMm == 1800 && p.Building.Floors.Count == 5 && p.Suspension.RopeCount == 4 && p.Suspension.RopeDiameterMm == 11 && ProjectPresets.Passes(new RopeTractionCalculationEngine().Calculate(p, ctx, lib)), "New project from the presets (Q 630, 1500 × 1800, 5 stops, ropes 4 × 11 with every rope / traction check OK)");
            var design = new ElevatorDesignEngine().Calculate(p, ctx, DrawingViews.All);
            Check(design.CanGenerate, "Shared design allows generation of all four views");
            var request = new GenerationRequest { ProjectJson = ProjectJson.Serialize(p), Options = new GenerationOptions { Views = DrawingViews.All, ArrangeViews = true } };
            File.WriteAllText(Path.Combine(output, "new-project.elevator.json"), request.ProjectJson);
            var target = Path.Combine(output, "NewProject-AllFour.dwg");
            var client = new GenerationClient(root, bin, @"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe");
            var response = client.Run(request, OutputConsent.Capture(target, false), Console.WriteLine);
            Wire.Write(Path.Combine(output, "generation.json"), response);
            Check(response.Success && response.ViewsGenerated.Length == 4 && File.Exists(target), "All four native views generated from the new project with no input");
            var verified = Wire.Read<GenerationResponse>(Path.Combine(client.LastJobDirectory, "verified.json"));
            Check(verified.Success && verified.InputHash == Wire.HashText(request.ProjectJson), "Fresh native verification matches the exact input snapshot");
            Check(response.EntityCounts.OrderBy(k => k.Key).SequenceEqual(verified.EntityCounts.OrderBy(k => k.Key)), "Generated and reopened native component counts match");
            var reopened = ProjectJson.Deserialize(File.ReadAllText(Path.Combine(client.LastJobDirectory, "reopened-project.elevator.json")));
            Check(reopened.FollowsPresets == true && reopened.Elevator.CapacityKg == 630 && reopened.Calculations.CarMassKg == 700 && reopened.Calculations.CounterweightMassKg == 1015 && reopened.Suspension.RopeCount == 4 && reopened.Suspension.RopeDiameterMm == 11 && reopened.Calculations.RopeTraction != null && reopened.Calculations.RopeTraction.SelectedUndercutAngleDeg == 100 && reopened.Calculations.SafetyPit.BufferType == "EnergyAccumulation" && reopened.Calculations.CarGuideForces.GuideShoeSpacingMm == 2800, "The DWG project snapshot keeps the preset flag and values after native save / reopen");
            string evidence = Path.Combine(output, "job-evidence"); Directory.CreateDirectory(evidence);
            foreach (string name in new[] { "request.json", "project.elevator.json", "reopened-project.elevator.json", "response.json", "verified.json", "generate.log", "verify.log" })
                File.Copy(Path.Combine(client.LastJobDirectory, name), Path.Combine(evidence, name), false);
            exit = 0;
            Console.WriteLine("PASS: " + count + " presets native assertions.");
        }
        catch (Exception e) { Wire.Write(Path.Combine(output, "presets-native-error.json"), new { Error = e.ToString() }); Console.Error.WriteLine(e); }
        Wire.Write(Path.Combine(output, "presets-native-tests.json"), new { Status = exit == 0 ? "PASS" : "FAIL", Assertions = count, Checks = names, Method = "Packaged GenerationClient and AutoCAD 2023 Core Console host; new project built exactly as ShaftMaster's New Project." });
        return exit;
    }
}
