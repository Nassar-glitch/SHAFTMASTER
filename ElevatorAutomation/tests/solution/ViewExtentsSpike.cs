using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Elevator.Core.Presets;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;
using Elevator.Solution.Bridge;

// Layout spike (L2b): generates the four-view DWG of three projects that differ in the number of stops (2, 6 and 15, one shaft size) through
// the packaged GenerationClient and the AutoCAD 2023 Core Console host, and writes the project and the design plan beside each DWG.
// tests/integration/view_extents.py then reads every view's model extents (dimensions and labels included) back from the DWGs.
public static class ViewExtentsSpike
{
    static ElevatorProject Project(PresetLibrary lib, DesignContext ctx, string bin, int stops, double width, double depth, string name)
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
        return p;
    }

    public static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]), bin = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(output);
        int exit = 1;
        try
        {
            var ctx = Wire.Read<DesignContext>(Path.Combine(bin, "reviewed-context.json"));
            ctx.ChassisEvidence = Wire.Read<ChassisGeometryEvidence>(Path.Combine(bin, "reviewed-chassis-evidence.json"));
            var lib = PresetLibrary.Load(Path.Combine(bin, "presets.json"));
            ctx.Doors = lib.DoorsOrDefault();
            var client = new GenerationClient(root, bin, @"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe");
            foreach (var c in new[] { new { Id = "2-stops-1600x1800", Stops = 2 }, new { Id = "6-stops-1600x1800", Stops = 6 }, new { Id = "15-stops-1600x1800", Stops = 15 } })
            {
                var p = Project(lib, ctx, bin, c.Stops, 1600, 1800, "Extents " + c.Id);
                var design = new ElevatorDesignEngine().Calculate(p, ctx, DrawingViews.All);
                if (!design.CanGenerate)
                    throw new Exception(c.Id + ": the design cannot generate");
                var request = new GenerationRequest { ProjectJson = ProjectJson.Serialize(p), Options = new GenerationOptions { Views = DrawingViews.All, ArrangeViews = true } };
                File.WriteAllText(Path.Combine(output, c.Id + ".elevator.json"), request.ProjectJson);
                var target = Path.Combine(output, c.Id + ".dwg");
                var response = client.Run(request, OutputConsent.Capture(target, false), Console.WriteLine);
                Wire.Write(Path.Combine(output, c.Id + "-generation.json"), response);
                if (!response.Success || !File.Exists(target))
                    throw new Exception(c.Id + ": generation failed");
                Wire.Write(Path.Combine(output, c.Id + "-expected.json"), new { c.Id, c.Stops, Plan = design.Plan, Views = response.ViewsGenerated });
                Console.WriteLine(c.Id + ": " + response.ViewsGenerated.Length + " views generated");
            }
            exit = 0;
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(output, "view-extents-error.txt"), e.ToString()); Console.Error.WriteLine(e); }
        return exit;
    }
}
