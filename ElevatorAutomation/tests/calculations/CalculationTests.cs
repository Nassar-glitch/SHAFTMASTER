using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Elevator.Core;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
public static class CalculationTests
{
    static int count;
    static JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue};
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;}
    static void Near(double actual,double expected,string name){Check(Math.Abs(actual-expected)<Math.Max(1,Math.Abs(expected))*1e-8,name+" ("+actual+" vs "+expected+")");}
    static BearingSeat Seat(string id,double x,double y){var p=new[]{new LocalPoint(x-20,y-20,0),new LocalPoint(x+20,y-20,0),new LocalPoint(x+20,y+20,0),new LocalPoint(x-20,y+20,0)};return new BearingSeat{Id=id,ContactBoundary=p,Centre=ChassisGeometry.ContactCentroid(p),Evidence="Independent synthetic contact polygon"};}
    static ChassisCalculationGeometry Geometry()
    {
        return new ChassisCalculationGeometry{Bearings=new List<BearingSeat>{Seat("P1",1000,2000),Seat("P2",1000,0),Seat("P3",0,2000),Seat("P4",0,0)},LongitudinalMembers=new List<ChassisMember>{new ChassisMember{Id="L",Front=new LocalPoint(200,0,100),Rear=new LocalPoint(200,2000,100)},new ChassisMember{Id="R",Front=new LocalPoint(800,0,100),Rear=new LocalPoint(800,2000,100)}}};
    }
    static LoadNode Force(string id,double x,double y,double downward){return new LoadNode{Id=id,PositionMm=new LocalPoint(x,y,0),ForceN=new LocalPoint(0,0,-downward)};}
    static double P(ChassisCalculationResult r,string id){return r.Reactions.Single(v=>v.Id==id).UpwardN;}
    static ElevatorProject Inputs()
    {
        var p=new ElevatorProject();p.Shaft.WidthMm=1450;p.Shaft.DepthMm=1600;p.Elevator.CapacityKg=450;p.Suspension.RopingRatio=1;p.Suspension.RopeCount=6;
        // Test fixture only. These values are NOT project defaults or structural approval.
        p.Calculations=new ChassisCalculationInputs{LoadCase="StaticRatedLoad1To1",CarMassKg=500,CounterweightMassKg=800,MachineMassKg=300,CarSideAdditionalSuspendedMassKg=0,CounterweightSideAdditionalSuspendedMassKg=0,MachineCgOffsetXMm=180,MachineCgOffsetYMm=-90,MachineCgOffsetZMm=-140,MachineCgSource="Synthetic test CG, engineering assumption",LeftCarryingMemberId="Longitudinal.Middle",RightCarryingMemberId="Longitudinal.Right",TransferModel=ChassisReactionSolver.Model,TransferAssumptionSource="Synthetic rigid spreader/released ends fixture; not approval for actual machine",HangerAttachmentOffsetXMm=120,HangerAttachmentOffsetYMm=0,HangerAttachmentSource="Synthetic test hanger node",IncludeChassisSelfWeight=false,ChassisWeightSourceOrExclusionReason="Explicitly excluded in isolated test only",IncludeHangerAssemblyWeight=false,HangerWeightSourceOrExclusionReason="Explicitly excluded in isolated test only"};return p;
    }
    static DesignContext Context(EquipmentProfile equipment,ChassisGeometryEvidence evidence,ElevatorParameters plan)
    {
        return new DesignContext{Equipment=equipment,CounterweightDepthMm=plan.CwDepth,RoomExtensions=plan.MachineRoom.Extensions,SectionReference=plan.MachineRoomSection.Reference,FrontSupportDepthMm=-plan.MachineRoomSection.FrontSupportX,RearSupportOffsetMm=-plan.MachineRoomSection.RearSupportX-plan.SD,RetainedDeflectorPitchMm=250,RetainedDeflectorVisible=true,ChassisEvidence=evidence};
    }
    static ChassisCalculationGeometry Generated(ElevatorProject p,DesignContext context)
    {
        var design=new ElevatorDesignEngine().Calculate(p,context,DrawingViews.MachineRoomPlan|DrawingViews.SectionBB);
        if(!design.CanGenerate)throw new Exception("Fixture generation blocked: "+String.Join(";",design.Validation.Where(v=>v.BlocksGeneration).Select(v=>v.Message)));
        return design.ChassisGeometry;
    }
    public static void Main(string[] args)
    {
        try{Run(args);}catch(Exception e){Console.Error.WriteLine(e.ToString());Environment.ExitCode=1;}
    }
    static void Run(string[] args)
    {
        var g=Geometry();var symmetric=ChassisReactionSolver.Solve(g,new[]{Force("central",500,1000,1000)},"L","R");
        Check(symmetric.Status=="CALCULATED_UNDER_ASSUMPTIONS","symmetric calculation available");foreach(string id in new[]{"P1","P2","P3","P4"})Near(P(symmetric,id),250,"symmetric "+id);
        var eccentric=ChassisReactionSolver.Solve(g,new[]{Force("eccentric",200,500,1000)},"L","R");
        // Independent hand statics: member L gets 1000; front=750, rear=250;
        // each transverse member shares 80% left /20% right.
        Near(P(eccentric,"P1"),50,"eccentric right rear");Near(P(eccentric,"P2"),150,"eccentric right front");Near(P(eccentric,"P3"),200,"eccentric left rear");Near(P(eccentric,"P4"),600,"eccentric left front");
        Near(eccentric.Transfers.Single(v=>v.MemberId=="L").FrontDownwardN,750,"first stage front end independent");Near(eccentric.Transfers.Single(v=>v.MemberId=="R").RearDownwardN,0,"load on one member no invented equal share");
        Near(eccentric.VerticalResidualN.Value,0,"vertical equilibrium");Near(eccentric.MomentXResidualNmm.Value,0,"Mx equilibrium");Near(eccentric.MomentYResidualNmm.Value,0,"My equilibrium");
        var horizontal=Force("horizontal",500,1000,1000);horizontal.PositionMm.Z=1000;horizontal.ForceN.X=100;
        var h=ChassisReactionSolver.Solve(g,new[]{horizontal},"L","R");Near(P(h,"P1"),300,"horizontal overturning rear right");Near(P(h,"P4"),200,"horizontal overturning front left");Near(h.UnverifiedLateralForceN.X,100,"lateral restraint remains explicit");
        var couple=Force("couple",500,1000,1000);couple.CoupleNmm.X=100000;
        var c=ChassisReactionSolver.Solve(g,new[]{couple},"L","R");Near(P(c,"P1"),225,"Mx couple rear");Near(P(c,"P2"),275,"Mx couple front");
        var combined=ChassisReactionSolver.Solve(g,new[]{Force("a",200,500,1000),Force("b",800,1500,2000)},"L","R");
        Near(P(combined,"P1"),1250,"separate eccentric loads rear right");Near(P(combined,"P4"),700,"separate eccentric loads front left");
        var uplift=ChassisReactionSolver.Solve(g,new[]{Force("overhang",1500,1000,1000)},"L","R");Check(uplift.Status=="UPLIFT_REVIEW_REQUIRED"&&P(uplift,"P4")<0,"uplift retained no clipping");
        var tilted=Geometry();tilted.LongitudinalMembers[0].Rear.X=250;tilted.LongitudinalMembers[1].Rear.X=900;
        var tilt=ChassisReactionSolver.Solve(tilted,new[]{Force("ecc",450,1250,2000)},"L","R");Near(tilt.VerticalResidualN.Value,0,"nonparallel model F");Near(tilt.MomentYResidualNmm.Value,0,"nonparallel model My");Near(tilt.MomentXResidualNmm.Value,0,"nonparallel model Mx");
        var invalid=Geometry();invalid.Bearings[0].Id="P4";Check(ChassisReactionSolver.Solve(invalid,new[]{Force("a",500,1000,1)},"L","R").Reactions==null,"duplicate P invalid");
        invalid=Geometry();invalid.Bearings[0].Centre.X+=2;Check(ChassisReactionSolver.Solve(invalid,new[]{Force("a",500,1000,1)},"L","R").Reactions==null,"bbox/incorrect centre rejected");
        invalid=Geometry();invalid.Units="m";Check(ChassisReactionSolver.Solve(invalid,new[]{Force("a",500,1000,1)},"L","R").Reactions==null,"units rejected");
        invalid=Geometry();invalid.LongitudinalMembers[1].Rear.X=200;Check(ChassisReactionSolver.Solve(invalid,new[]{Force("a",500,1000,1)},"L","R").Reactions==null,"zero member spacing rejected");
        var nan=Force("nan",500,1000,1);nan.PositionMm.X=Double.NaN;Check(ChassisReactionSolver.Solve(Geometry(),new[]{nan},"L","R").Reactions==null,"nonfinite load rejected");
        var triangle=new[]{new LocalPoint(0,0,0),new LocalPoint(90,0,0),new LocalPoint(0,60,0)};Near(ChassisGeometry.ContactCentroid(triangle).X,30,"area centroid differs from bounding box");

        Console.WriteLine("Independent reaction checks passed; reading native evidence.");
        var evidence=json.Deserialize<ChassisGeometryEvidence>(File.ReadAllText(args[0]));
        Console.WriteLine("Reading shared reference plan.");
        var plan=json.Deserialize<ElevatorParameters>(File.ReadAllText(args[1]));
        var equipment=json.Deserialize<EquipmentProfile>(File.ReadAllText(args[4]));
        var nativeContext=Context(equipment,evidence,plan);
        var native=Generated(Inputs(),nativeContext);File.WriteAllText(args[2],json.Serialize(native));
        Check(native.Issues.Count==0,"native geometry: "+String.Join(";",native.Issues.Select(v=>v.Message)));
        Near(native.Bearings.Single(v=>v.Id=="P4").Centre.X,445.1619836367608,"independent perimeter front-left X");Near(native.Bearings.Single(v=>v.Id=="P4").Centre.Y,-113.37700208333265,"independent perimeter front-left Y");
        Check(native.Bearings.Single(v=>v.Id=="P1").Centre.X>native.Bearings.Single(v=>v.Id=="P3").Centre.X&&native.Bearings.Single(v=>v.Id=="P1").Centre.Y>native.Bearings.Single(v=>v.Id=="P2").Centre.Y,"native P orientation");
        Near(native.TractionCentre.X,725,"explicit pitch-profile plane");Near(native.DeflectorCentre.X,725,"deflector pitch plane avoids asymmetric centermark bbox");
        Near(native.TractionCentre.Y,985,"traction station from drawing engine");Near(native.DeflectorCentre.Y,1350,"deflector station from drawing engine");
        var engine=new ChassisCalculationEngine();var p=Inputs();var result=engine.Calculate(p,native);
        Check(result.Reactions!=null,"native fixture calculates: "+String.Join(";",result.Issues.Select(v=>v.Message)));
        Check(native.Provenance!=null,"shared design engine records geometry source provenance");
        foreach(double diameter in new[]{600.0,400,0,-520,Double.NaN,Double.PositiveInfinity})
        {
            var unsupported=Inputs();unsupported.Machine.TractionSheavePitchDiameterMm=diameter;
            var rejected=engine.Calculate(unsupported,native);
            Check(rejected.Status=="INVALID_INPUT"&&rejected.Reactions==null&&rejected.Loads.Count==0,"independent unsupported/nonfinite diameter rejected: "+diameter);
            Check(rejected.Issues.Any(v=>v.Field=="Machine.TractionSheavePitchDiameterMm"),"diameter issue identifies authoritative field");
            Check(!ChassisCalculationEngine.IsCurrent(rejected,unsupported,native),"rejected diameter result cannot be current");
        }
        var explicitDiameter=Inputs();explicitDiameter.Machine.TractionSheavePitchDiameterMm=520;
        Check(engine.Calculate(explicitDiameter,native).Status=="STALE_GEOMETRY","changed supported input still requires generation provenance");
        var regenerated=Generated(explicitDiameter,nativeContext);var current=engine.Calculate(explicitDiameter,regenerated);
        Check(current.Reactions!=null&&ChassisCalculationEngine.IsCurrent(current,explicitDiameter,regenerated),"regeneration restores supported 520 mm current result");
        foreach(var pr in result.Reactions)Near(P(current,pr.Id),pr.UpwardN,"explicit 520 regeneration preserves model "+pr.Id);
        var sourceChanges=new Action<ElevatorProject>[]
        {
            v=>v.Shaft.WidthMm+=100,v=>v.Shaft.DepthMm+=100,v=>v.Elevator.CapacityKg=600,
            v=>v.MachineRoom.MinimumDeflectorSlabClearanceMm=400,v=>v.MachineRoom.FrontExtensionMm=1200,
            v=>v.Machine.Type="changed-machine",v=>v.Elevator.TemplateId="changed-template",
            v=>v.Elevator.LegacyFamily=ElevatorType.Gearbox,v=>v.Elevator.SystemId="changed-system",
            v=>v.Standards.RuleSetId="changed-rules",v=>v.Counterweight.Position="side",v=>v.Suspension.RopingRatio=2
        };
        foreach(var change in sourceChanges)
        {
            var edited=Inputs();change(edited);var rejected=engine.Calculate(edited,native);
            Check(rejected.Status=="STALE_GEOMETRY"&&rejected.Reactions==null&&rejected.Loads.Count==0,"retained geometry after authoritative input change unavailable");
            Check(!ChassisCalculationEngine.IsCurrent(rejected,edited,native),"stale calculation cannot be current");
            // Deliberately reproduce the former error: hash current inputs + retained geometry.
            var falselyRehashed=new ChassisCalculationResult{Status="CALCULATED_UNDER_ASSUMPTIONS",Reactions=result.Reactions,InputFingerprint=ChassisCalculationEngine.Fingerprint(edited,native)};
            Check(!ChassisCalculationEngine.IsCurrent(falselyRehashed,edited,native),"current-pair hash cannot bypass source-input provenance");
        }
        var contextChanges=new Action<DesignContext>[]
        {
            v=>v.Equipment.CarFrontAllowanceMm+=10,v=>v.CounterweightDepthMm+=2,
            v=>v.RoomExtensions.Front+=10,v=>v.FrontSupportDepthMm+=1,v=>v.RearSupportOffsetMm+=1,
            v=>v.RetainedDeflectorPitchMm=400,v=>v.RetainedDeflectorVisible=false,
            v=>v.SectionReference.SourceTractionZ+=10,v=>v.ChassisEvidence.Contours[0].Points[0].X+=1
        };
        foreach(var change in contextChanges)
        {
            var origin=json.Deserialize<DesignContext>(json.Serialize(nativeContext));
            var contextual=Generated(Inputs(),origin);var before=engine.Calculate(Inputs(),contextual);
            change(origin);var rejected=engine.Calculate(Inputs(),contextual);
            Check(rejected.Status=="STALE_GEOMETRY"&&rejected.Reactions==null,"originating context mutation invalidates retained geometry");
            Check(!ChassisCalculationEngine.IsCurrent(before,Inputs(),contextual),"origin context mutation makes existing result noncurrent");
            var replacement=json.Deserialize<DesignContext>(json.Serialize(nativeContext));change(replacement);
            Check(engine.Calculate(Inputs(),native,replacement).Status=="STALE_GEOMETRY","replacement context must match generation snapshot");
            Check(!ChassisCalculationEngine.IsCurrent(result,Inputs(),native,replacement),"replacement context invalidates IsCurrent");
        }
        var equivalentContext=json.Deserialize<DesignContext>(json.Serialize(nativeContext));
        Check(ChassisCalculationEngine.IsCurrent(result,Inputs(),native,equivalentContext),"equivalent detached context accepted by value");
        var raw=ChassisGeometry.Calculate(evidence,plan);var noSource=engine.Calculate(Inputs(),raw);
        Check(noSource.Status=="STALE_GEOMETRY"&&noSource.Reactions==null,"raw/older geometry without provenance fails closed");
        Check(!ChassisCalculationEngine.IsCurrent(noSource,Inputs(),raw),"unproven geometry cannot be current");
        var invalidWithOldReactions=new ChassisCalculationResult{Status="INVALID_INPUT",Reactions=result.Reactions,InputFingerprint=result.InputFingerprint};
        Check(!ChassisCalculationEngine.IsCurrent(invalidWithOldReactions,Inputs(),native),"invalid status with retained reactions cannot be current");
        Near(result.CarBundleTensionN.Value,950*9.81,"1:1 car bundle tension");Near(result.CarPerRopeTensionN.Value,950*9.81/6,"per rope units");
        Near(result.Reactions.Sum(v=>v.UpwardN),(950+800+300)*9.81,"no car/CW double counting");
        var ropes=result.Loads.Where(v=>v.Kind.StartsWith("rope")).ToArray();var fsum=ropes.Select(v=>v.ForceN).Aggregate(new LocalPoint(0,0,0),ChassisReactionSolver.Add);Near(fsum.Y,0,"traction and deflector horizontal forces cancel");Near(fsum.Z,-(950+800)*9.81,"ropes net carried weight");
        double mx=ropes.Sum(v=>ChassisReactionSolver.Add(ChassisReactionSolver.Cross(v.PositionMm,v.ForceN),v.CoupleNmm).X);
        Near(mx,-950*9.81*plan.CarRow-800*9.81*plan.CwRow,"rope moment equals two actual drop actions, not machine centre");
        var shifted=ProjectJson.Clone(p);shifted.Calculations.HangerAttachmentOffsetYMm=70;var shiftedResult=engine.Calculate(shifted,native);
        var originalHanger=result.Loads.Single(v=>v.Id=="DeflectorHangerAction");var movedHanger=shiftedResult.Loads.Single(v=>v.Id=="DeflectorHangerAction");
        var originalMoment=ChassisReactionSolver.Add(ChassisReactionSolver.Cross(originalHanger.PositionMm,originalHanger.ForceN),originalHanger.CoupleNmm);var movedMoment=ChassisReactionSolver.Add(ChassisReactionSolver.Cross(movedHanger.PositionMm,movedHanger.ForceN),movedHanger.CoupleNmm);
        Near(movedMoment.X,originalMoment.X,"hanger shift preserves Mx");Near(movedMoment.Y,originalMoment.Y,"hanger shift preserves My");Near(movedMoment.Z,originalMoment.Z,"hanger shift preserves Mz");
        Near(shiftedResult.VerticalResidualN.Value,0,"shifted attachment equilibrium");
        Check(ChassisCalculationEngine.IsCurrent(result,p,native),"fresh result fingerprint");p.Calculations.MachineCgOffsetYMm+=20;Check(!ChassisCalculationEngine.IsCurrent(result,p,native),"CG edit invalidates results");p=Inputs();p.Shaft.DepthMm+=100;Check(!ChassisCalculationEngine.IsCurrent(result,p,native),"shaft edit invalidates even if caller retains old geometry");
        Check(engine.Calculate(p,native).Status=="STALE_GEOMETRY"&&engine.Calculate(p,native).Reactions==null,"stale geometry cannot create new reactions");
        native.LongitudinalMembers[0].Front.X+=.5;Check(!ChassisCalculationEngine.IsCurrent(result,Inputs(),native),"native member edit invalidates results");
        Check(engine.Calculate(Inputs(),native).Status=="STALE_GEOMETRY","edited payload cannot be rehashed as current geometry");native=Generated(Inputs(),nativeContext);
        native.Section.Arrangement.Reeving.TractionSheavePitchDiameterMm=600;
        Check(engine.Calculate(Inputs(),native).Status=="STALE_GEOMETRY"&&engine.Calculate(Inputs(),native).Reactions==null,"edited sheave payload cannot bypass geometry binding");native=Generated(Inputs(),nativeContext);
        p=Inputs();p.Calculations.MachineMassKg=null;var missing=engine.Calculate(p,native);Check(missing.Reactions==null&&missing.Issues.Any(v=>v.Field=="Calculations.MachineMassKg"),"missing mass unavailable not zero");
        p=Inputs();p.Calculations.MachineCgOffsetXMm=null;Check(engine.Calculate(p,native).Reactions==null,"missing CG no machine-centre fallback");
        p=Inputs();p.Calculations.LeftCarryingMemberId=null;Check(engine.Calculate(p,native).Reactions==null,"missing load transfer no silent selection");
        p=Inputs();p.Suspension.RopingRatio=2;Check(engine.Calculate(p,native).Reactions==null,"live 2:1 rejected without changing drawings");
        p=Inputs();p.Calculations.CarSideAdditionalSuspendedMassKg=-1;Check(engine.Calculate(p,native).Reactions==null,"negative additional mass rejected");
        p=Inputs();p.Calculations.MachineMassKg=Double.NaN;Check(engine.Calculate(p,native).Reactions==null,"nonfinite calculation input cannot produce reactions");
        p=Inputs();p.Calculations.MachineMassKg=Double.PositiveInfinity;Check(engine.Calculate(p,native).Reactions==null,"infinite calculation input cannot produce reactions");
        p=Inputs();p.Calculations.IncludeChassisSelfWeight=true;Check(engine.Calculate(p,native).Reactions==null,"included self weight cannot use missing zero");
        p=Inputs();p.Calculations.IncludeChassisSelfWeight=true;p.Calculations.ChassisMassKg=100;p.Calculations.ChassisCgOffsetXMm=0;p.Calculations.ChassisCgOffsetYMm=0;p.Calculations.ChassisCgOffsetZMm=80;
        Near(engine.Calculate(p,native).Reactions.Sum(v=>v.UpwardN),result.Reactions.Sum(v=>v.UpwardN)+981,"explicit separate chassis weight");
        var changed=json.Deserialize<ElevatorParameters>(json.Serialize(plan));changed.SW+=200;changed.SD+=200;changed.CarRow+=100;changed.CwRow+=200;changed.MachineRoomSection.TractionX-=100;changed.MachineRoomSection.DeflectorX-=200;
        var grown=ChassisGeometry.Calculate(evidence,changed);Check(grown.Issues.Count==0,"changed shaft recipes resolve");
        foreach(var seat in native.Bearings){var q=grown.Bearings.Single(v=>v.Id==seat.Id);Near(q.Centre.X-seat.Centre.X,100,"SW-dependent centred contact "+seat.Id);Near(q.Centre.Y-seat.Centre.Y,seat.Id=="P1"||seat.Id=="P3"?200:0,"SD-dependent contact row "+seat.Id);}
        Near(grown.TractionCentre.Y-native.TractionCentre.Y,100,"equipment station follows shared section");
        var absent=evidence.Contours.Where(v=>!v.Id.StartsWith("MachineRoom.Support.P4.")).ToList();var removed=json.Deserialize<ChassisGeometryEvidence>(json.Serialize(evidence));removed.Contours=absent;Check(ChassisGeometry.Calculate(removed,plan).Issues.Count>0,"edited/missing contact evidence unavailable");

        // DOCX 2:1 fixture, independent of live reaction mapping and topology. Note-only IDs.
        double qKg=480,carKg=500,cwKg=740,compKg=600,machineKg=250,g0=9.81;
        var noteTensions=IdealStaticRopeTensions.Calculate(qKg+carKg+compKg,cwKg,2,4,g0);
        Near(noteTensions.CarPerRopeN,1937.475,"note 2:1 per-rope car example");Near(noteTensions.CounterweightPerRopeN,907.425,"note 2:1 per-rope CW example");
        double noteW=(qKg+carKg+cwKg+compKg+machineKg)*g0;
        Near(noteW,25211.7,"note independently stated W");double w1=noteW*950/1800,w2=noteW*850/1800;
        Near(w1*400/600,8870.783333333333,"NOTE P1 arithmetic only");Near(w2*400/600,7937.016666666666,"NOTE P2 arithmetic only");Near((w1+w2)*(400+200)/600,noteW,"note two-stage arithmetic force total");
        // Old files omit Calculations. Loading does not copy example inputs or block drawings.
        var old=ProjectJson.Deserialize(File.ReadAllText(args[3]));Check(old.Calculations!=null&&old.Calculations.MachineMassKg==null&&old.Calculations.TransferModel==null,"old input file has empty optional group");
        var oldLegacy=LegacyProjectAdapter.ToLegacy(old);var roundLegacy=LegacyProjectAdapter.ToLegacy(ProjectJson.Clone(old));Near(roundLegacy.SW,oldLegacy.SW,"old SW roundtrip");Near(roundLegacy.SD,oldLegacy.SD,"old SD roundtrip");
        var cp=ProjectJson.Clone(Inputs());Near(cp.Calculations.MachineCgOffsetXMm.Value,180,"canonical new inputs roundtrip");
        var migrated=LegacyProjectAdapter.FromLegacy(LegacyProjectAdapter.ToLegacy(old),LegacyProjectAdapter.ToSectionAA(old),cp);Near(migrated.Calculations.MachineMassKg.Value,300,"legacy drawing edit retains canonical calculation inputs");
        var incompleteSection=json.Deserialize<ElevatorParameters>(json.Serialize(plan));incompleteSection.MachineRoomSection.Reference=null;Check(ChassisGeometry.Calculate(evidence,incompleteSection).Issues.Count>0,"missing vertical reference returns unavailable geometry");
        var updated=Inputs();updated.Calculations.MachineCgOffsetXMm+=100;var cgMoved=engine.Calculate(updated,native);
        double oldRight=P(result,"P1")+P(result,"P2"),newRight=P(cgMoved,"P1")+P(cgMoved,"P2");
        Near(newRight-oldRight,300*9.81*100/(native.Bearings.Single(v=>v.Id=="P2").Centre.X-native.Bearings.Single(v=>v.Id=="P4").Centre.X),"CG eccentricity independently changes total right reaction");
        foreach(var size in new[]{new[]{1450.0,1600},new[]{1650.0,1800},new[]{1850.0,2000}})
        {
            var live=ProjectJson.Clone(old);live.Shaft.WidthMm=size[0];live.Shaft.DepthMm=size[1];live.Calculations=Inputs().Calculations;
            var context=Context(equipment,evidence,plan);
            var design=new ElevatorDesignEngine().Calculate(live,context,DrawingViews.All);
            Check(design.CanGenerate&&design.ChassisCalculation.Reactions!=null,"coordinated geometry/calculation case "+size[0]+"/"+size[1]);Near(design.ChassisCalculation.VerticalResidualN.Value,0,"coordinated force equilibrium");Near(design.ChassisGeometry.TractionCentre.Y,-design.Plan.MachineRoomSection.TractionX,"same drawn machine position");
        }
        File.WriteAllText(Path.ChangeExtension(args[2],"calculation.json"),json.Serialize(result));
        Console.WriteLine("PASS: "+count+" calculation assertions; native case results use SYNTHETIC test masses/CG/transfer selection, not project defaults.");
    }
}
