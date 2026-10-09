using System;
using Xunit;
using System.IO;
using System.Linq;
using Elevator.Core.Application;
using Elevator.Core.MachineRoom;
using Elevator.Core.GuideRails;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.Core.SafetyGear;
using Elevator.Solution.Bridge;
using Elevator.Reporting.Sheet;
using Elevator.Reporting.DesignNotes;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;

namespace Elevator.Core.Tests
{
    // Independent numeric fixtures, never project defaults. Live tests use geometry
    // issued by the shared design engine rather than stamping arbitrary dimensions.
    public class CarGuideForceTests
    {
        static int assertions;
        static void Check(bool condition,string name){if(!condition)throw new Exception(name);assertions++;Suite.Count("ff-core");}
        static void Near(double? value,double expected,string name){Check(value.HasValue&&Math.Abs(value.Value-expected)<=Math.Max(1,Math.Abs(expected))*1e-9,name);}
        static bool Available(CarGuideForceResult r){return r!=null&&r.Status=="CALCULATED_UNDER_NOTE_ASSUMPTIONS"&&r.FF1BaseN.HasValue&&r.FF1DesignN.HasValue&&r.FF2BaseN.HasValue&&r.FF2DesignN.HasValue;}
        static void Unavailable(CarGuideForceResult r,string status,string name)
        {Check(r!=null&&r.Status==status&&!r.FF1BaseN.HasValue&&!r.FF1DesignN.HasValue&&!r.FF2BaseN.HasValue&&!r.FF2DesignN.HasValue,name);}
        static bool HasField(CarGuideForceResult r,string field){return r.Issues.Any(i=>(i.Field??"").Contains(field));}
        static void Reject(Action action,string name){bool threw=false;try{action();}catch(ArgumentException){threw=true;}Check(threw,name);}
        static CarGuideForceGeometry Geometry(ElevatorProject p,DesignContext ctx){return new ElevatorDesignEngine().CalculateCarGuideForceGeometry(p,ctx);}
        static CarGuideForceResult Calc(ElevatorProject p,CarGuideForceGeometry g,DesignContext ctx){return new CarGuideForceCalculationEngine().Calculate(p,g,ctx);}
        static ElevatorProject Live(string bin)
        {
            var p=ProjectJson.Deserialize(File.ReadAllText(Path.Combine(bin,"reviewed-example.elevator.json")));
            p.Info.Name="SYNTHETIC FF VERIFICATION — NOT PROJECT DEFAULTS";
            p.Elevator.CapacityKg=480;p.Shaft.WidthMm=1500;p.Shaft.DepthMm=1600;
            p.Calculations.CarGuideForces=new CarGuideForceInputs{GuideShoeSpacingMm=1200};
            return p;
        }
        [Fact]
        public void Car_guide_forces_FF1_FF2()
        {
            int before = assertions;
            Run(new[] { TestData.Bin, TestData.Folder("ff-core"), TestData.Root });
            Assert.Equal(131, assertions - before);
        }
        static void Run(string[] args)
        {
            string bin=args[0],output=args[1],root=args[2];Directory.CreateDirectory(output);
            var ctx=Wire.Read<DesignContext>(Path.Combine(bin,"reviewed-context.json"));
            ctx.ChassisEvidence=Wire.Read<ChassisGeometryEvidence>(Path.Combine(bin,"reviewed-chassis-evidence.json"));
            var fresh=new ElevatorProject();Check(fresh.Calculations.CarGuideForces==null,"No fabricated guide-shoe spacing or note inputs in new project");
            fresh.Calculations.CarGuideForces=new CarGuideForceInputs();Check(!fresh.Calculations.CarGuideForces.GuideShoeSpacingMm.HasValue,"New h explicitly missing");
            Check(typeof(CarGuideForceInputs).GetFields().Length==1,"Only genuinely missing h is added; no duplicate capacity or cabin-dimension inputs");
            Check(CalculationModes.Resolve(fresh.Calculations)=="Basic","FF-only legacy data does not force Advanced");
            fresh.Calculations.MachineCgSource="Previously entered assumption";Check(CalculationModes.Resolve(fresh.Calculations)=="Advanced","Existing populated legacy Advanced remains Advanced");

            var note=CarGuideForceCalculationEngine.EvaluateNoteRule(480,1100,2100,1200);
            Check(note.Length==4,"Independent scalar note evaluator returns two base/design pairs");
            Near(note[0],1100,"Note FF1 base 1100 N");Near(note[1],2200,"Note FF1 design 2200 N");
            Near(note[2],1050,"Note FF2 base 1050 N");Near(note[3],2100,"Note FF2 design 2100 N");
            Near(note[1],2*note[0],"One design multiplier FF1");Near(note[3],2*note[2],"One design multiplier FF2");
            Check(Math.Abs(note[0]-480*9.81*1100/(4*1200))>1,"Adopted 10 N/kg is distinct from physical g 9.81");
            var metres=CarGuideForceCalculationEngine.EvaluateNoteRule(480,1.1,2.1,1.2);
            for(int i=0;i<4;i++)Near(metres[i],note[i],"Consistent metre/mm ratios "+i);
            foreach(double invalid in new[]{0.0,-1.0,Double.NaN,Double.PositiveInfinity,Double.NegativeInfinity})
            {
                Reject(()=>CarGuideForceCalculationEngine.EvaluateNoteRule(invalid,1100,2100,1200),"Scalar invalid Q "+invalid);
                Reject(()=>CarGuideForceCalculationEngine.EvaluateNoteRule(480,invalid,2100,1200),"Scalar invalid internal width "+invalid);
                Reject(()=>CarGuideForceCalculationEngine.EvaluateNoteRule(480,1100,invalid,1200),"Scalar invalid internal depth "+invalid);
                Reject(()=>CarGuideForceCalculationEngine.EvaluateNoteRule(480,1100,2100,invalid),"Scalar invalid vertical shoe spacing "+invalid);
            }
            Reject(()=>CarGuideForceCalculationEngine.EvaluateNoteRule(Double.MaxValue,1100,2100,1200),"Scalar force overflow never yields infinity");

            var p=Live(bin);var g=Geometry(p,ctx);var r=Calc(p,g,ctx);
            Check(Available(r),"Live source-bound force calculation");Check(CarGuideForceCalculationEngine.IsGeometryCurrent(g,p,ctx),"Fresh geometry provenance");Check(CarGuideForceCalculationEngine.IsCurrent(r,p,ctx),"Fresh FF result currency");
            var plan=new ElevatorDesignEngine().Calculate(p,ctx,DrawingViews.Plan);Check(plan.Plan!=null&&plan.CanGenerate,"Shared plan geometry available");
            Near(g.InternalWidthMm,plan.Plan.CarWidth,"Authoritative shared internal width, not a second sizing recipe");Near(g.InternalDepthMm,plan.Plan.CarDepth,"Authoritative shared internal depth");
            Check(g.InternalWidthMm!=p.Shaft.WidthMm&&g.InternalDepthMm!=p.Shaft.DepthMm&&g.InternalWidthMm!=plan.Plan.DoorWidth,"Internal dimensions are distinct from shaft and door dimensions");
            var scalar=CarGuideForceCalculationEngine.EvaluateNoteRule(480,plan.Plan.CarWidth,plan.Plan.CarDepth,1200);
            Near(r.FF1BaseN,scalar[0],"Live FF1 uses calculated internal width");Near(r.FF2BaseN,scalar[2],"Live FF2 uses calculated internal depth");
            Near(r.CapacityKg,480,"Result reuses canonical rated capacity");Near(r.GuideShoeSpacingMm,1200,"Vertical h remains explicit, never rail spacing");
            // Audit 2026-10-07: approved interpretation is reported; stale "unresolved" wording is gone; equations unchanged.
            string reported=String.Join("\n",r.Details)+"\n"+CarGuideForceCalculationEngine.Definitions;
            Check(r.Details.Contains(CarGuideForceCalculationEngine.ApprovedInterpretation)&&reported.Contains("FF1 = Fy")&&reported.Contains("FF2 = Fx")&&reported.Contains("per guide rail at one guide-shoe level")&&reported.Contains("centre-to-centre"),"Result reports the user-approved FF axis/per-rail/h interpretation");
            Check(!reported.Contains("does not establish their project X/Y")&&!reported.Contains("require confirmation")&&!reported.Contains("unconfirmed"),"No stale unresolved-interpretation wording in reported FF text");
            Check(CarGuideForceCalculationEngine.RuleVersion=="GearboxNoteCarGuideForces-v1"&&CarGuideForceCalculationEngine.AdoptedCoefficientNPerKg==10&&CarGuideForceCalculationEngine.DesignMultiplier==2,"Interpretation text changes no rule version, coefficient or factor");
            Near(r.FF1DesignN,2*r.FF1BaseN.Value,"Live FF1 design = base × 2 exactly once (no rail/shoe division or group sum)");Near(r.FF2DesignN,2*r.FF2BaseN.Value,"Live FF2 design = base × 2 exactly once (no rail/shoe division or group sum)");

            // Geometry provenance survives unrelated edits, but cannot be reused after a sizing change.
            p.Calculations.CarMassKg=Double.NaN;p.Calculations.CounterweightMassKg=-1;p.Calculations.MachineMassKg=Double.PositiveInfinity;
            p.Calculations.MachineCgOffsetXMm=Double.NaN;p.Calculations.HangerAttachmentOffsetYMm=Double.NaN;
            p.Calculations.SafetyPit=new SafetyPitInputs{CarGuideCount=0,CarBufferCount=-1,CarEventAdditionalMassPercent=Double.NaN};
            p.Calculations.Mode="Advanced";p.Suspension.RopeCount=-1;p.Suspension.RopeDiameterMm=Double.NaN;
            Check(Available(Calc(p,g,ctx)),"Unrelated malformed/nonfinite canonical masses, CG, pit and rope inputs cannot block FF");
            Check(CarGuideForceCalculationEngine.IsCurrent(r,p,ctx),"Unrelated engine/mode inputs do not stale FF numbers");
            p.Elevator.CapacityKg=600;Check(!CarGuideForceCalculationEngine.IsCurrent(r,p,ctx),"Capacity edit invalidates FF results");
            Check(CarGuideForceCalculationEngine.IsGeometryCurrent(g,p,ctx),"Capacity-only edit reuses valid shape geometry");Near(Calc(p,g,ctx).FF1BaseN,scalar[0]*600/480,"Capacity-only result updates against source-bound shape");
            p.Elevator.CapacityKg=480;p.Calculations.CarGuideForces.GuideShoeSpacingMm=600;Check(!CarGuideForceCalculationEngine.IsCurrent(r,p,ctx),"h edit invalidates FF results");Near(Calc(p,g,ctx).FF1BaseN,2*scalar[0],"Halving h doubles FF1 once");Near(Calc(p,g,ctx).FF2BaseN,2*scalar[2],"Halving h doubles FF2");
            p.Calculations.CarGuideForces.GuideShoeSpacingMm=1200;p.Shaft.WidthMm=1700;Check(!CarGuideForceCalculationEngine.IsGeometryCurrent(g,p,ctx),"Retained geometry rejected after SW edit");Unavailable(Calc(p,g,ctx),"STALE_GEOMETRY","No forces computed from old SW geometry");
            var changed=Calc(p,Geometry(p,ctx),ctx);Check(Available(changed),"Regenerated SW geometry calculates");Check(changed.FF1BaseN>r.FF1BaseN,"Larger internal width changes FF1 automatically");Near(changed.FF2BaseN,r.FF2BaseN.Value,"SW-only edit preserves FF2 depth");
            p=Live(bin);g=Geometry(p,ctx);r=Calc(p,g,ctx);p.Shaft.DepthMm=1800;Unavailable(Calc(p,g,ctx),"STALE_GEOMETRY","Retained geometry rejected after SD edit");changed=Calc(p,Geometry(p,ctx),ctx);Check(changed.FF2BaseN>r.FF2BaseN,"Larger depth changes FF2 automatically");Near(changed.FF1BaseN,r.FF1BaseN.Value,"SD-only edit preserves FF1 width");
            p=Live(bin);g=Geometry(p,ctx);ctx.Equipment.CarWidthDeductionMm+=20;Unavailable(Calc(p,g,ctx),"STALE_GEOMETRY","Retained geometry rejected after equipment rule change");changed=Calc(p,Geometry(p,ctx),ctx);Check(changed.FF1BaseN<r.FF1BaseN,"Changed shared sizing rule used by FF");ctx.Equipment.CarWidthDeductionMm-=20;
            p=Live(bin);g=Geometry(p,ctx);ctx.CounterweightDepthMm+=10;Check(!CarGuideForceCalculationEngine.IsGeometryCurrent(g,p,ctx),"Context change invalidates source-bound shape");ctx.CounterweightDepthMm-=10;

            p=Live(bin);g=Geometry(p,ctx);p.Calculations.CarGuideForces=null;r=Calc(p,g,ctx);Unavailable(r,"MISSING_INPUT","Older/missing FF group has no zero forces");Check(HasField(r,"GuideShoeSpacingMm"),"Missing h has field-specific message");
            p.Calculations.CarGuideForces=new CarGuideForceInputs();Unavailable(Calc(p,g,ctx),"MISSING_INPUT","Intentionally blank h is missing");
            foreach(double invalid in new[]{0.0,-1.0,Double.NaN,Double.PositiveInfinity,Double.NegativeInfinity})
            {p.Calculations.CarGuideForces.GuideShoeSpacingMm=invalid;r=Calc(p,g,ctx);Unavailable(r,"INVALID_INPUT","Invalid canonical h "+invalid);Check(HasField(r,"GuideShoeSpacingMm"),"Invalid h field message "+invalid);}
            p.Calculations.CarGuideForces.GuideShoeSpacingMm=1200;p.Elevator.CapacityKg=null;r=Calc(p,g,ctx);Unavailable(r,"MISSING_INPUT","Missing Q unavailable");Check(HasField(r,"CapacityKg"),"Missing Q field message");
            foreach(double invalid in new[]{0.0,-1.0,Double.NaN,Double.PositiveInfinity})
            {p.Elevator.CapacityKg=invalid;r=Calc(p,g,ctx);Unavailable(r,"INVALID_INPUT","Invalid capacity "+invalid);Check(HasField(r,"CapacityKg"),"Invalid Q field message "+invalid);}
            foreach(double invalid in new[]{0.0,-1.0,Double.NaN,Double.PositiveInfinity})
            {p=Live(bin);g=Geometry(p,ctx);p.Shaft.WidthMm=invalid;r=Calc(p,g,ctx);Unavailable(r,"INVALID_INPUT","Malformed/invalid current geometry width "+invalid);Check(HasField(r,"Shaft"),"Current geometry error field-specific");}
            p=Live(bin);p.Shaft.WidthMm=100;g=Geometry(p,ctx);Check(g.Issues.Count>0,"Impossible cabin geometry carries explicit issues");Check(!Available(Calc(p,g,ctx)),"Impossible cabin geometry has unavailable forces");
            p=Live(bin);g=Geometry(p,ctx);p.Elevator.TemplateId="unregistered-template";Unavailable(Calc(p,g,ctx),"INVALID_INPUT","Unregistered template cannot reuse geometry proof");
            p=Live(bin);g=Geometry(p,ctx);p.Standards.RuleSetId="unregistered-rule";Unavailable(Calc(p,g,ctx),"INVALID_INPUT","Unregistered geometry rule cannot reuse geometry proof");
            p=Live(bin);g=Geometry(p,ctx);double originalFront=ctx.Equipment.CarFrontAllowanceMm;ctx.Equipment.CarFrontAllowanceMm=Double.NaN;Unavailable(Calc(p,g,ctx),"INVALID_INPUT","Nonfinite current sizing context is invalid independently");ctx.Equipment.CarFrontAllowanceMm=originalFront;
            p=Live(bin);g=Geometry(p,ctx);Check(!Available(Calc(p,g,null)),"Missing reviewed geometry context makes forces unavailable");
            p=Live(bin);Unavailable(Calc(p,null,ctx),"MISSING_INPUT","Missing source geometry has no forces");
            p=Live(bin);g=Geometry(p,ctx);p.Calculations.CarGuideForces.GuideShoeSpacingMm=Double.Epsilon;Unavailable(Calc(p,g,ctx),"INVALID_INPUT","Positive h causing numeric overflow yields unavailable forces");

            p=Live(bin);p.Calculations.Mode="Basic";var session=new ProjectSession(p);string file=Path.Combine(output,"SYNTHETIC-FF.elevator.json");session.Save(file);session.Open(file);p=session.Project;
            Near(p.Calculations.CarGuideForces.GuideShoeSpacingMm,1200,"h survives save/reopen");Check(p.Calculations.Mode=="Basic","Basic selection survives FF save/reopen");Check(Available(Calc(p,Geometry(p,ctx),ctx)),"Reopened project regenerates valid source-bound geometry");
            string oldJson=File.ReadAllText(Path.Combine(bin,"reviewed-example.elevator.json"));var old=ProjectJson.Deserialize(oldJson);Check(old.Calculations.CarGuideForces==null,"Older project opens with missing h rather than an invented spacing");Check(CalculationModes.Resolve(old.Calculations)=="Basic","Older empty legacy project remains Basic");

            // FF configuration cannot alter either established chassis engine or pit loads.
            var basic=Live(bin);basic.Calculations=new ChassisCalculationInputs{Mode="Basic",CarMassKg=500,CounterweightMassKg=740,MachineMassKg=300};var bw=new CalculationWorkspace();var br=bw.Calculate(basic,ctx);Check(br.Reactions!=null,"Existing Basic baseline available");basic.Calculations.CarGuideForces=new CarGuideForceInputs{GuideShoeSpacingMm=1200};Check(bw.IsCurrent(basic,ctx),"FF-only edit preserves Basic fingerprint");var br2=bw.Calculate(basic,ctx);foreach(var bearing in br.Reactions)Near(br2.Reactions.Single(v=>v.Id==bearing.Id).UpwardN,bearing.UpwardN,"Basic P1-P4 preserved "+bearing.Id);
            var advanced=ProjectJson.Deserialize(File.ReadAllText(Path.Combine(root,"tests/fixtures/SYNTHETIC.elevator.json")));var aw=new CalculationWorkspace();var ar=aw.Calculate(advanced,ctx);Check(ar.Reactions!=null,"Existing Advanced baseline available");advanced.Calculations.CarGuideForces=new CarGuideForceInputs{GuideShoeSpacingMm=1200};Check(aw.IsCurrent(advanced,ctx),"FF-only edit preserves Advanced fingerprint");var ar2=aw.Calculate(advanced,ctx);foreach(var bearing in ar.Reactions)Near(ar2.Reactions.Single(v=>v.Id==bearing.Id).UpwardN,bearing.UpwardN,"Advanced P1-P4 preserved "+bearing.Id);
            basic.Calculations.SafetyPit=new SafetyPitInputs{SafetyGearType="Progressive",CarGuideCount=2,CarBufferCount=1,CounterweightBufferCount=1};var pit=new SafetyPitCalculationEngine().Calculate(basic);basic.Calculations.CarGuideForces.GuideShoeSpacingMm=-1;Check(SafetyPitCalculationEngine.IsCurrent(pit,basic),"Invalid FF h cannot stale unrelated safety/pit loads");Check(new ElevatorDesignEngine().Calculate(basic,ctx).CanGenerate,"Invalid FF input does not block native drawing workflow");
            Wire.Write(Path.Combine(output,"ff-core-tests.json"),new{Status="PASS",Assertions=assertions,Method="Independent note fixture; mm/m ratio; shared source-bound dimensions; stale provenance; canonical validation; independent Basic/Advanced/P5-P7; save/reopen",FixtureOnly=true});
            Console.WriteLine("PASS: "+assertions+" car guide force assertions.");
        }
    }
}
