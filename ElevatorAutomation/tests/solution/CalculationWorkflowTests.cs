using System;
using System.IO;
using System.Linq;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.Solution.Bridge;
using Elevator.Solution.Desktop;
public static class CalculationWorkflowTests
{
 static int count;static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;}
 public static int Main(string[] args)
 {
  try{
   string bin=args[0],output=args[1];Directory.CreateDirectory(output);
   var old=ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin,"reviewed-example.elevator.json")));
   var context=Wire.Read<DesignContext>(Path.Combine(bin,"reviewed-context.json"));context.ChassisEvidence=Wire.Read<ChassisGeometryEvidence>(Path.Combine(bin,"reviewed-chassis-evidence.json"));
   var w=new CalculationWorkspace();var session=new ProjectSession(ProjectJson.Clone(old));
   Check(typeof(ChassisCalculationInputs).GetFields().All(f=>f.GetValue(session.Project.Calculations)==null),"Old projects have no fabricated calculation values");
   Check(w.Calculate(session.Project,context).Reactions==null,"Old project no invented reactions");
   Check(new ElevatorDesignEngine().Calculate(old,context,DrawingViews.All).CanGenerate,"Missing calculations do not block all-four design");
   var p=ProjectJson.Clone(old);p.Info.Name="SYNTHETIC CALCULATION UI TEST — NOT DESIGN INPUTS";p.Shaft.WidthMm=1450;p.Shaft.DepthMm=1600;p.Elevator.CapacityKg=450;p.Suspension.RopeCount=6;p.Suspension.RopingRatio=1;
   p.Calculations=new ChassisCalculationInputs{LoadCase="StaticRatedLoad1To1",CarMassKg=500,CounterweightMassKg=800,MachineMassKg=300,CarSideAdditionalSuspendedMassKg=0,CounterweightSideAdditionalSuspendedMassKg=0,MachineCgOffsetXMm=180,MachineCgOffsetYMm=-90,MachineCgOffsetZMm=-140,MachineCgSource="SYNTHETIC test CG; not manufacturer evidence",LeftCarryingMemberId="Longitudinal.Middle",RightCarryingMemberId="Longitudinal.Right",TransferModel=ChassisReactionSolver.Model,TransferAssumptionSource="SYNTHETIC released two-stage test assumption; not structural approval",HangerAttachmentOffsetXMm=120,HangerAttachmentOffsetYMm=0,HangerAttachmentSource="SYNTHETIC hanger connection assumption",IncludeChassisSelfWeight=false,ChassisWeightSourceOrExclusionReason="Explicit exclusion for isolated SYNTHETIC test",IncludeHangerAssemblyWeight=false,HangerWeightSourceOrExclusionReason="Explicit exclusion for isolated SYNTHETIC test"};
   session=new ProjectSession(p);session.Save(Path.Combine(output,"SYNTHETIC.elevator.json"));session.Open(session.FilePath);
   Check(session.Project.Calculations.CarSideAdditionalSuspendedMassKg==0,"Explicit zero survives open");Check(session.Project.Calculations.IncludeChassisSelfWeight==false,"Exclusion survives open");Check(session.Project.Calculations.MachineCgSource==p.Calculations.MachineCgSource,"Assumption source survives open");
   session.Save(Path.Combine(output,"SYNTHETIC-save-as.elevator.json"));session.Open(session.FilePath);p=session.Project;
   var r=w.Calculate(p,context);Check(r.Status=="CALCULATED_UNDER_ASSUMPTIONS"&&r.Reactions.Count==4,"Synthetic source-bound result");Check(w.IsCurrent(p,context),"Context-aware current result");Check(r.CarBundleTensionN==9319.5&&r.CarPerRopeTensionN==1553.25,"Static 1:1 tension meaning");
   Check(r.Reactions.Single(v=>v.Id=="P1").PositionMm.Y>r.Reactions.Single(v=>v.Id=="P2").PositionMm.Y,"Preserved P1 rear / P2 front");
   var geometry=w.Geometry;int generated=w.GeometryGenerationCount;double previous=r.Reactions.Single(v=>v.Id=="P1").UpwardN;
   p.Calculations.MachineMassKg=400;Check(!w.IsCurrent(p,context),"Load edit invalidates current result");w.Invalidate();Check(w.Result==null,"Stale numbers cleared immediately");r=w.Calculate(p,context);Check(Object.ReferenceEquals(w.Geometry,geometry)&&w.GeometryGenerationCount==generated,"Load-only edit reuses valid geometry");Check(r.Reactions.Single(v=>v.Id=="P1").UpwardN!=previous,"Load edit recalculates reactions");
   p.Shaft.WidthMm+=100;Check(!w.IsCurrent(p,context),"SW edit stale");r=w.Calculate(p,context);Check(!Object.ReferenceEquals(geometry,w.Geometry)&&w.GeometryGenerationCount==generated+1&&w.Geometry.ShaftWidthMm==1550,"SW regenerates through design engine");
   generated=w.GeometryGenerationCount;context.RoomExtensions.Left+=10;Check(!w.IsCurrent(p,context),"Context edit stale");r=w.Calculate(p,context);Check(w.GeometryGenerationCount==generated+1&&w.IsCurrent(p,context),"Context regenerated");context.RoomExtensions.Left-=10;
   p.Machine.TractionSheavePitchDiameterMm=600;r=w.Calculate(p,context);Check(r.Status=="INVALID_INPUT"&&r.Issues.Any(v=>v.Field=="Machine.TractionSheavePitchDiameterMm")&&r.Reactions==null&&!w.IsCurrent(p,context),"Unsupported diameter rejected with authoritative field message in UI workflow");p.Machine.TractionSheavePitchDiameterMm=520;
   p.Calculations.CarMassKg=null;r=w.Calculate(p,context);Check(r.Reactions==null&&r.Issues.Any(v=>v.Field=="Calculations.CarMassKg"),"Missing field-specific message");p.Calculations.CarMassKg=-1;r=w.Calculate(p,context);Check(r.Status=="INVALID_INPUT"&&r.Reactions==null,"Invalid finite mass");p.Calculations.CarMassKg=500;
   p.Calculations.IncludeHangerAssemblyWeight=null;r=w.Calculate(p,context);Check(r.Reactions==null&&r.Issues.Any(v=>v.Field=="Calculations.IncludeHangerAssemblyWeight"),"Explicit optional-weight decision");p.Calculations.IncludeHangerAssemblyWeight=false;
   p.Calculations.MachineMassKg=5000;p.Calculations.MachineCgOffsetXMm=2000;r=w.Calculate(p,context);Check(r.Status=="UPLIFT_REVIEW_REQUIRED"&&r.Reactions.Any(v=>v.UpwardN<0),"Uplift retained and labelled");
   p.Calculations.LeftCarryingMemberId=null;r=w.Calculate(p,context);Check(r.Reactions==null,"No implicit member choice");
   var retained=LegacyProjectAdapter.FromLegacy(LegacyProjectAdapter.ToLegacy(session.Project),LegacyProjectAdapter.ToSectionAA(session.Project),session.Project);Check(Wire.Json(retained.Calculations)==Wire.Json(session.Project.Calculations),"Drawing adapter retains calculations");
   Wire.Write(Path.Combine(output,"workflow-tests.json"),new{Status="PASS",Assertions=count,Mode="Automated workflow / canonical model tests; not mouse-driven UI",GeometryGenerationCount=w.GeometryGenerationCount});Console.WriteLine("PASS: "+count+" calculation UI workflow assertions.");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}
