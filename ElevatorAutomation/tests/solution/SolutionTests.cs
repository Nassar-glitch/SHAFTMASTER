using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Elevator.Core.Project;
using Elevator.Solution.Bridge;
using Elevator.Solution.Desktop;
public static class SolutionTests
{
    static List<object> results=new List<object>();static string output;
    static void Check(bool x,string message){if(!x)throw new Exception(message);}
    static void Test(string name,Action action){try{action();results.Add(new{name,Status="PASS"});}catch(Exception e){results.Add(new{name,Status="FAIL",Error=e.ToString()});}Wire.Write(Path.Combine(output,"solution-tests.json"),results);}
    public static int Main(string[] args)
    {
        string root=args[0],bin=args.Length>2?args[2]:Path.Combine(root,"bin-solution-v01");output=Path.Combine(root,"output","solution-v01","test-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(output);
        var p=ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin,"reviewed-example.elevator.json")));
        Test("Desktop session JSON roundtrip and extension data",()=>{var s=new ProjectSession(ProjectJson.Clone(p));string path=Path.Combine(output,"roundtrip.elevator.json");s.Project.Info.Name="UI مشروع";s.Save(path);string json=File.ReadAllText(path);s.Open(path);Check(ProjectJson.Serialize(s.Project)==json,"Roundtrip mismatch");});
        Test("Floor edits stable identity unequal intervals NO STOP",()=>{var s=new ProjectSession(ProjectJson.Clone(p));string id=s.Project.Building.Floors[1].FloorId;var row=new FloorRow(s,s.Project.Building.Floors[1],()=>{});row.Name="Mezzanine";row.Height="3100";row.Served=false;Check(row.Floor.FloorId==id&&!row.Served,"ID or Served lost");s.MoveIntermediate(id,1);Check(s.Project.Building.Floors[2].FloorId==id,"Move lost identity");ProjectFloors.Elevations(s.Project.Building);});
        Test("Floor insert remove and protected datums",()=>{var b=ProjectJson.Clone(p).Building;var f=new FloorDefinition{Name="Added"};ProjectFloors.InsertBefore(b,b.Floors[1].FloorId,f,2700);Check(b.Floors[1].FloorId==f.FloorId,"Insert");ProjectFloors.Remove(b,f.FloorId);Check(b.Floors.Count==p.Building.Floors.Count,"Remove");bool failed=false;try{ProjectFloors.Remove(b,b.Floors[0].FloorId);}catch(ArgumentException){failed=true;}Check(failed,"Removed datum");});
        Test("Input validation and all-four defaults",()=>{var s=new ProjectSession(p);Check(s.Views==DrawingViews.All&&s.Arrange,"Default views");var bad=ProjectJson.Clone(p);bad.Shaft.WidthMm=-1;Check(ProjectValidator.Validate(bad,s.Views).Any(i=>i.BlocksGeneration),"Invalid SW passed");});
        Test("Calculated dimensions not authoritative project inputs",()=>{string json=ProjectJson.Serialize(p);foreach(string key in new[]{"CarWidth","CarDepth","CarDbg","CwDbg","TZ","CalculatedWrapAngle"})Check(!json.Contains("\""+key+"\""),key+" duplicated");});
        Test("Request exact Unicode snapshot transport",()=>{var r=new GenerationRequest{ProjectJson=ProjectJson.Serialize(p)};string path=Path.Combine(output,"transport.json");Wire.Write(path,r);var q=Wire.Read<GenerationRequest>(path);Check(q.ProjectJson==r.ProjectJson&&q.Options.Views==DrawingViews.All,"Transport changed");Wire.Validate(q);});
        Test("No Autodesk dependency in Desktop or Core",()=>{foreach(var assembly in new[]{typeof(ElevatorProject).Assembly,System.Reflection.Assembly.LoadFrom(Path.Combine(bin,File.Exists(Path.Combine(bin,"ShaftMaster.exe"))?"ShaftMaster.exe":"Elevator Solution.exe"))})Check(!assembly.GetReferencedAssemblies().Any(a=>a.Name.StartsWith("Ac")||a.Name.Contains("Autodesk")),assembly.FullName);});
        Test("Output confirmation refusal and atomic replacement",()=>{string target=Path.Combine(output,"consent.dwg"),staged=Path.Combine(output,"staged.dwg");File.WriteAllText(target,"original");File.WriteAllText(staged,"verified");bool denied=false;try{OutputConsent.Capture(target,false);}catch(IOException){denied=true;}Check(denied&&File.ReadAllText(target)=="original","Missing confirmation");var c=OutputConsent.Capture(target,true);c.Publish(staged);Check(File.ReadAllText(target)=="verified","Publish failed");});
        Test("Output changed after consent is preserved",()=>{string target=Path.Combine(output,"race.dwg"),staged=Path.Combine(output,"staged.dwg");File.WriteAllText(target,"first");var c=OutputConsent.Capture(target,true);File.WriteAllText(target,"other writer");bool denied=false;try{c.Publish(staged);}catch(IOException){denied=true;}Check(denied&&File.ReadAllText(target)=="other writer","Lost concurrent file");});
        if(!args.Contains("--unit-only"))
        {
            var client=new GenerationClient(root,bin,@"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe");
            Test("Disposable all-four generate fresh reopen publish",()=>{var path=Path.Combine(output,"AllFour.dwg");var r=client.Run(new GenerationRequest{ProjectJson=ProjectJson.Serialize(p)},OutputConsent.Capture(path,false),Console.WriteLine);Check(r.Success,r.Error);Check(r.ViewsGenerated.Length==4,"Missing views");Wire.Write(Path.Combine(bin,"reviewed-context.json"),r.Context);Wire.Write(Path.Combine(output,"all-four-result.json"),r);});
            foreach(DrawingViews view in new[]{DrawingViews.Plan,DrawingViews.MachineRoomPlan,DrawingViews.SectionAA,DrawingViews.SectionBB})
            {var selected=view;Test("Individual selection "+view,()=>{var path=Path.Combine(output,selected+".dwg");var r=client.Run(new GenerationRequest{ProjectJson=ProjectJson.Serialize(p),Options=new GenerationOptions{Views=selected,ArrangeViews=true}},OutputConsent.Capture(path,false),Console.WriteLine);Check(r.Success,r.Error);Check(r.ViewsGenerated.Length==1&&r.ViewsGenerated[0]==selected.ToString(),"Unexpected dependency view published");});}
            Test("Repeated disposable generation has no placement drift",()=>{var previous=Wire.Read<GenerationResponse>(Path.Combine(output,"all-four-result.json"));var path=Path.Combine(output,"AllFourRepeated.dwg");var r=client.Run(new GenerationRequest{ProjectJson=ProjectJson.Serialize(p)},OutputConsent.Capture(path,false),Console.WriteLine);Check(r.Success,r.Error);Check(Wire.Json(previous.Bounds)==Wire.Json(r.Bounds),"View placement drift");});
            Test("Native failure never replaces final output",()=>{var bad=ProjectJson.Clone(p);bad.Shaft.WidthMm=300;string path=Path.Combine(output,"failure.dwg");File.WriteAllText(path,"protected final");var r=client.Run(new GenerationRequest{ProjectJson=ProjectJson.Serialize(bad)},OutputConsent.Capture(path,true),Console.WriteLine);Check(!r.Success&&r.Error.Contains("configured cabin")&&File.ReadAllText(path)=="protected final"&&!File.Exists(Path.Combine(client.LastJobDirectory,"generated.dwg")),"Failed generation published or did not reach native geometry validation");});
        }
        Console.WriteLine(Wire.Json(results));return results.Any(r=>Wire.Json(r).Contains("\"FAIL\""))?1:0;
    }
}
