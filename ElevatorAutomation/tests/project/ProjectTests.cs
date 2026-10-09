using System;
using System.IO;
using System.Linq;
using Elevator.Core;
using Elevator.Core.Project;
using Elevator.Core.SectionAA;
using Elevator.Core.Vertical;
public static class ProjectTests
{
    static int count;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);count++;}
    static void Reject(Action action,string name){try{action();}catch(ArgumentException){count++;return;}throw new Exception("Accepted: "+name);}
    public static void Main(string[] args)
    {
        var old=new ProjectInput{SW=1450,SD=1600,ProjectName="مشروع اختبار",Capacity=450,SuspensionRopeDiameterMm=8,NumberOfSuspensionRopes=6};
        var aa=new SectionAAProject{Floors=FloorStackGenerator.Create(6,"BG","G",new[]{3000.0,3200,2800,3500},null)};
        aa.DisplayFloorId=aa.Floors[0].FloorId;
        var p=LegacyProjectAdapter.FromLegacy(old,aa);p.Extensions["future"]=new System.Collections.Generic.Dictionary<string,object>{{"flag",true}};
        string json=ProjectJson.Serialize(p);var copy=ProjectJson.Deserialize(json);
        Check(ProjectJson.Serialize(copy)==json,"JSON roundtrip");Check(copy.Info.Name==old.ProjectName,"Unicode");
        Reject(()=>ProjectJson.Deserialize(json.Replace("\"schemaVersion\":1","\"schemaVersion\":2")),"future schema");
        Reject(()=>ProjectJson.Deserialize("{\"project\":{}}"),"missing version");
        Reject(()=>ProjectJson.Deserialize(json.Replace("\"schemaVersion\":1","\"schemaVersion\":1.1")),"fractional version");
        var elevations=ProjectFloors.Elevations(copy.Building);Check(elevations[4].ElevationMm==12500,"unequal cumulative elevations");
        string id=copy.Building.Floors[2].FloorId;ProjectFloors.Rename(copy.Building,id,"Lobby");Check(copy.Building.Floors[2].FloorId==id,"rename identity");
        copy.Building.Floors[2].IsServed=false;Check(!ProjectFloors.Elevations(copy.Building)[2].IsServed,"NO STOP");
        var inserted=new FloorDefinition{Name="Intermediate"};ProjectFloors.InsertBefore(copy.Building,id,inserted,2700);
        Check(ProjectFloors.Elevations(copy.Building).Single(f=>f.FloorId==id).ElevationMm==8900,"insertion cumulative shift");
        ProjectFloors.Remove(copy.Building,inserted.FloorId);Check(ProjectFloors.Elevations(copy.Building).Single(f=>f.FloorId==id).ElevationMm==6200,"removal inverse");
        var restored=LegacyProjectAdapter.ToSectionAA(p);var expected=SectionAACalculator.Calculate(aa,200);var actual=SectionAACalculator.Calculate(restored,200);
        Check(actual.Travel==expected.Travel&&actual.CWBottom==expected.CWBottom&&actual.R==expected.R,"A-A migration geometry");
        Check(actual.Floors.Select(f=>f.FloorId).SequenceEqual(expected.Floors.Select(f=>f.FloorId)),"all IDs including R preserved");
        var profile=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<EquipmentProfile>(File.ReadAllText(args[0]));
        foreach(var size in new[]{new[]{1450.0,1600},new[]{1650.0,1800},new[]{1850.0,2000}})
        {
            p.Shaft.WidthMm=size[0];p.Shaft.DepthMm=size[1];
            var result=new ElevatorDesignEngine().Calculate(p,new DesignContext{Equipment=profile,CounterweightDepthMm=70},DrawingViews.Plan);
            var legacy=DesignRules.Calculate(size[0],size[1],profile,70);
            Check(result.CanGenerate&&result.Plan.CarWidth==legacy.CarWidth&&result.Plan.CarDepth==legacy.CarDepth&&result.Plan.TZ==legacy.TZ&&result.Plan.CwDbg==legacy.CwDbg&&result.Plan.DoorWidth==legacy.DoorWidth,"SW/SD compatibility");
        }
        p.Shaft.WidthMm=Double.NaN;Check(ProjectValidator.Validate(p,DrawingViews.All).Any(i=>i.BlocksGeneration),"invalid SW validation");p.Shaft.WidthMm=1450;p.Shaft.DepthMm=1600;
        p.Elevator.SystemId="hydraulic";Check(ProjectValidator.Validate(p,DrawingViews.All).Any(i=>i.BlocksGeneration),"future system rejected");p.Elevator.SystemId="traction-geared";
        var incomplete=ProjectJson.Clone(p);incomplete.Building.Floors=null;Check(ProjectValidator.Validate(incomplete,DrawingViews.All).Any(i=>i.BlocksGeneration),"null floor collection returns validation");
        var migrated=LegacyProjectAdapter.FromLegacy(new ProjectInput{SW=1450,SD=1600,Stops=5,Travel=12000,FloorHeights=new[]{3000.0}},null);
        var back=LegacyProjectAdapter.ToLegacy(ProjectJson.Deserialize(ProjectJson.Serialize(migrated)));
        Check(back.Stops==5&&back.Travel==12000&&back.FloorHeights[0]==3000,"legacy placeholders preserved without fabricating floors");
        Check(migrated.Building.Floors.Count==0,"missing floor identity not invented");
        Check(!typeof(ElevatorProject).Assembly.GetReferencedAssemblies().Any(a=>a.Name.StartsWith("AcDb")||a.Name.StartsWith("AcMgd")||a.Name.Contains("Autodesk")),"Core has no Autodesk dependency");
        var placements=DrawingLayoutService.Arrange(new[]{new ViewBounds{View=DrawingViews.Plan,MaxX=100,MaxY=100},new ViewBounds{View=DrawingViews.MachineRoomPlan,MaxX=200,MaxY=150},new ViewBounds{View=DrawingViews.SectionAA,MaxX=120,MaxY=1000},new ViewBounds{View=DrawingViews.SectionBB,MaxX=100,MaxY=250}},50);
        Check(placements.Single(v=>v.View==DrawingViews.MachineRoomPlan).DeltaX==170,"layout measured left column width");
        Check(placements.Single(v=>v.View==DrawingViews.SectionAA).DeltaY==-1200,"layout measured row clearance");
        Check(new GenerationOptions().Views==DrawingViews.All,"default all four views");
        File.WriteAllText(args[1],ProjectJson.Serialize(p));
        Console.WriteLine("PASS: "+count+" unified project assertions.");
    }
}
