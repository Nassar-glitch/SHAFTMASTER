using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Elevator.Core.Suspension;
using Elevator.Core.Shaft;
using Elevator.Core.GuideRails;
using Elevator.Core.Geometry;
public sealed class VerticalCoreTests
{
    sealed class Evidence {public VerticalSource Source {get;set;}}
    static int checks;
    static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    static void Reject(Action action,string message){bool caught=false;try{action();}catch(ArgumentException){caught=true;}Check(caught,message);}
    static VerticalProjectData Data(int stops,int spacing,string convention,bool unequal,bool noStop)
    {
        int n=stops+(noStop?1:0);var d=new VerticalProjectData{RequestedBracketMaxSpacingMm=spacing,FixtureLabel="SYNTHETIC CORE REGRESSION ONLY"};
        d.Floors=FloorStackGenerator.Create(n+1,convention,convention=="BG"?"B":"-2",Enumerable.Range(0,n-1).Select(i=>unequal?2750.0+i*137:3000.0).ToArray(),null);
        if(noStop)d.Floors[1].Served=false;d.CarReferenceFloorId=(d.Floors.FirstOrDefault(f=>f.Name=="G"&&f.Served)??d.Floors.First(f=>f.Served)).FloorId;return d;
    }
    public static int Main(string[] args)
    {
        try
        {
            var json=new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue};var source=json.Deserialize<Evidence>(File.ReadAllText(args[0])).Source;int cases=0;
            foreach(int stops in new[]{2,3,5,8,12})foreach(int spacing in new[]{3000,2000,1500,1000})foreach(string naming in new[]{"BG","Numeric"})foreach(bool unequal in new[]{false,true})foreach(bool noStop in new[]{false,true})
            {
                var d=Data(stops,spacing,naming,unequal,noStop);var g=VerticalSectionCalculator.Calculate(d,source);cases++;
                Check(g.Floors.Count(f=>f.Served)==stops,"Served count");Check(g.Floors.Last().Name=="R"&&!g.Floors.Last().Served,"R separate from served stops");
                Check(g.Floors.First().FflZ==0&&g.PitFloorZ==-1000,"Lowest modeled datum and Pit");
                double last=d.Floors[d.Floors.Count-2].FflZ;Check(g.TravelMm==last,"True travel across NO STOP/equal/unequal intervals");
                Check(g.RoofZ==last+4200+source.Section.Reference.SourceSlabThickness,"Coordinated R datum");
                Check(g.Brackets.Elevations.First()==-1000&&g.Brackets.Elevations.Last()==last+4200,"Complete continuous rail/bracket extent");
                Check(g.Brackets.ConsecutiveSpacings.All(x=>x>0&&x<=spacing+1e-8),"Maximum spacing, not fixed pitch");
                Check(g.Entities.Count(e=>e.Id.StartsWith("VerticalExperiment.Door.")&&e.Id.EndsWith(".Opening"))==stops,"Doors only at served floors");
                Check(g.Entities.Count(e=>e.Id.StartsWith("VerticalExperiment.Floor.")&&e.Id.EndsWith(".Slab"))==d.Floors.Count-1,"All physical normal slabs retained");
                Check(g.Entities.Where(e=>e.Kind=="Dimension").All(e=>!e.Id.Contains("Car")&&!e.Id.Contains("Door")&&!e.Id.Contains("CW")),"No unrequested component height dimensions");
                Check(g.Entities.Select(e=>e.Id).Distinct().Count()==g.Entities.Count,"Unique semantic identities");
                Check(g.CWAbsolutePositionStatus=="PROVISIONAL_REFERENCE_INPUT_REQUIRED","Missing calibration never passes engineering reference");
                Check(g.CodeDiameterRatioStatus==source.CodeDiameterRatioStatus,"D/d status remains independent");
                Check(json.Serialize(g)==json.Serialize(VerticalSectionCalculator.Calculate(d,source)),"Deterministic identical inputs");
            }
            var baseline=Data(5,1500,"BG",false,false);var atGround=VerticalSectionCalculator.Calculate(baseline,source);
            baseline.CarReferenceFloorId=baseline.Floors[0].FloorId;var atBasement=VerticalSectionCalculator.Calculate(baseline,source);
            Check(Math.Abs((atGround.CwBottomZ-atBasement.CwBottomZ)+(atGround.CarFloorZ-atBasement.CarFloorZ))<1e-8,"Exact 1:1 opposite motion across basement/G");
            baseline.CWBottomAbovePitFloorAtHighestServedLandingMm=450;baseline.CarReferenceFloorId=baseline.Floors[4].FloorId;var atTop=VerticalSectionCalculator.Calculate(baseline,source);
            Check(atTop.CwBottomZ==-550&&atTop.CWAbsolutePositionStatus=="PROJECT_REFERENCE_SUPPLIED","Absolute CW calibration at highest served landing");
            baseline.Floors[0].Served=false;var noLowest=VerticalSectionCalculator.Calculate(baseline,source);Check(noLowest.PitFloorZ==-1000&&noLowest.LowestServedZ==3000&&noLowest.TravelMm==9000,"Lowest NO STOP datum differs from lowest served");
            baseline.Floors[4].Served=false;baseline.CarReferenceFloorId=baseline.Floors[3].FloorId;var topNoStop=VerticalSectionCalculator.Calculate(baseline,source);Check(topNoStop.HighestServedZ==9000&&topNoStop.RoofZ==9000+4200+source.Section.Reference.SourceSlabThickness,"Top NO STOP does not define OH datum");
            var old=FloorStackGenerator.Create(4,"Numeric","-1",new[]{3000.0,3000},null);var renamed=FloorStackGenerator.Create(4,"BG","B",new[]{3000.0,3000},old);Check(old.Select(f=>f.FloorId).SequenceEqual(renamed.Select(f=>f.FloorId)),"Stable internal IDs survive display convention edits");
            var numeric=FloorStackGenerator.Create(5,"Numeric","-2",new[]{3000.0,3000,3000},null);Check(String.Join(",",numeric.Select(f=>f.Name))=="-2,-1,0,1,R","Numeric zero is not renamed G");
            var noBasement=FloorStackGenerator.Create(4,"BG","G",new[]{3000.0,3000},null);Check(String.Join(",",noBasement.Select(f=>f.Name))=="G,1,2,R","No basement naming");
            Reject(()=>BracketLayoutSolver.Solve(-1000,5000,0,old,250,2160),"Spacing cannot silently default");
            Reject(()=>FloorStackGenerator.Create(4,"Numeric","-1",new[]{3000.0,-1},null),"Negative interval rejected");
            var invalid=Data(3,1500,"BG",false,false);invalid.SuspensionTopology="2:1";Reject(()=>VerticalSectionCalculator.Calculate(invalid,source),"Unsupported topology rejected");
            invalid=Data(3,1500,"BG",false,false);invalid.CarEnvelopeBottomBelowCarFFLMm=500;Reject(()=>VerticalSectionCalculator.Calculate(invalid,source),"Envelope cannot truncate internal height");
            invalid=Data(3,1500,"BG",false,false);invalid.Floors[0].FloorId=invalid.Floors[1].FloorId;Reject(()=>VerticalSectionCalculator.Calculate(invalid,source),"Duplicate floor identity rejected");
            invalid=Data(3,1500,"BG",false,false);invalid.Floors.Last().Served=true;Reject(()=>VerticalSectionCalculator.Calculate(invalid,source),"R is not silently a served floor");
            invalid=Data(3,1500,"BG",false,false);invalid.CWBottomAbovePitFloorAtHighestServedLandingMm=10000;Check(VerticalSectionCalculator.Calculate(invalid,source).LayoutStatus=="WARNING","Envelope conflict does not pass layout");
            invalid=Data(3,1500,"BG",false,false);invalid.Floors[1].FflZ=1800;Check(VerticalSectionCalculator.Calculate(invalid,source).LayoutStatus=="WARNING","Door/next slab clash flagged");
            File.WriteAllText(args[1],json.Serialize(new{Status="PASS",Cases=cases,Assertions=checks,Scope="Synthetic fixtures only; no structural/code certification"}));Console.WriteLine("PASS: "+checks+" vertical Core assertions across "+cases+" fixtures.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}

