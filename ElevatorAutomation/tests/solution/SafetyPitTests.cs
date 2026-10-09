using System;
using System.IO;
using System.Linq;
using Elevator.Core.Project;
using Elevator.Core.Calculations;
using Elevator.Solution.Bridge;
using Elevator.Solution.Desktop;

// Synthetic fixtures only. These values are never inserted into project defaults.
public static class SafetyPitTests
{
 // Source root (config/ and Templates/): the parent of the binary folder, or an ancestor for artifacts/bin.
 static string Root(string bin){var parent=Directory.GetParent(bin.TrimEnd(Path.DirectorySeparatorChar));for(var d=parent;d!=null;d=d.Parent)if(Directory.Exists(Path.Combine(d.FullName,"config"))&&Directory.Exists(Path.Combine(d.FullName,"Templates")))return d.FullName;return parent.FullName;}
    static int assertions;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);assertions++;}
    static void Near(double? actual,double expected,string name){Check(actual.HasValue&&Math.Abs(actual.Value-expected)<Math.Max(1,Math.Abs(expected))*1e-9,name);}
    static bool Available(SafetyPitLoad load){return load!=null&&load.Status=="CALCULATED_UNDER_NOTE_ASSUMPTIONS"&&load.GroupLoadN.HasValue&&load.PerComponentLoadN.HasValue;}
    static void Unavailable(SafetyPitLoad load,string status,string name){Check(load!=null&&load.Status==status&&!load.GroupLoadN.HasValue&&!load.PerComponentLoadN.HasValue,name);}
    static SafetyPitInputs Inputs(){return new SafetyPitInputs{SafetyGearType="Progressive",CarGuideCount=2,CarBufferCount=1,CounterweightBufferCount=1,CarBufferSharing="Single",CounterweightBufferSharing="Single",CarAdditionalMassAllocation="AllParticipates"};}
    static ElevatorProject NoteFixture()
    {
        var p=new ElevatorProject();p.Info.Name="SYNTHETIC NOTE VERIFICATION — NOT PROJECT DEFAULTS";p.Elevator.CapacityKg=480;
        p.Calculations.CarMassKg=500;p.Calculations.CounterweightMassKg=740;p.Calculations.CarSideAdditionalSuspendedMassKg=600;p.Calculations.SafetyPit=Inputs();
        p.Suspension.RopingRatio=2; // Note fixture is independent of the live 1:1 drawing workflow.
        return p;
    }
    static SafetyPitCalculationResult Calc(ElevatorProject p){return new SafetyPitCalculationEngine().Calculate(p);}
    public static int Main(string[] args){try{Run(args);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
    static void Run(string[] args)
    {
        string bin=args[0],outDir=args[1],automationRoot=args.Length>2?args[2]:Root(bin);Directory.CreateDirectory(outDir);
        var fresh=new ElevatorProject();Check(fresh.Calculations.SafetyPit==null,"No invented safety/pit defaults");
        fresh.Calculations.SafetyPit=new SafetyPitInputs();Check(CalculationModes.Resolve(fresh.Calculations)=="Basic","Safety-only data preserves empty legacy Basic resolution");
        foreach(var f in typeof(SafetyPitInputs).GetFields())Check(f.GetValue(fresh.Calculations.SafetyPit)==null,"Missing default "+f.Name);
        fresh.Calculations.MachineCgSource="Existing engineering assumption";Check(CalculationModes.Resolve(fresh.Calculations)=="Advanced","Existing legacy Advanced data remains Advanced");

        var p=NoteFixture();var r=Calc(p);
        Check(Available(r.P5)&&Available(r.P6)&&Available(r.P7),"Independent note fixture calculable without machine/CG/drawing geometry");
        Near(r.P5.ParticipatingMassKg,1580,"P5 capacity + car + car-borne compensation");Near(r.P6.ParticipatingMassKg,1580,"P6 same note event mass");Near(r.P7.ParticipatingMassKg,740,"P7 counterweight event mass");
        Near(r.P5.PerComponentLoadN,15800,"Note P5 15800 N PER guide, no second count division");Near(r.P5.GroupLoadN,31600,"P5 group is two per-guide loads");Check(r.P5.ComponentCount==2,"P5 explicit two guides");
        Near(r.P6.GroupLoadN,63200,"Note P6 63200 N group");Near(r.P6.PerComponentLoadN,63200,"Note P6 one buffer");Near(r.P7.GroupLoadN,29600,"Note P7 29600 N group");Near(r.P7.PerComponentLoadN,29600,"Note P7 one buffer");
        Check(Math.Abs(r.P5.PerComponentLoadN.Value-1580*9.81)>100,"Rounded adopted 10 N/kg remains distinct from g=9.81");
        Near(r.P6.GroupLoadN,1580*Math.Max(40,4*9.81),"Buffer adopted 40 N/kg versus factor 4 x g");
        Check(r.P5.Units=="N"&&r.P6.Units=="N"&&r.P7.Units=="N","Explicit force units N");Check(r.RuleBasis.Contains("Elevator_Calculations_GEARBOX_Code_Corrected_Dual_Units.docx")&&r.RuleBasis.Contains("not independent"),"Recorded note basis is distinct from independent certification");
        Check(SafetyPitCalculationEngine.IsCurrent(r,p),"Fresh safety/pit fingerprint");
        p.Calculations.MachineMassKg=Double.NaN;p.Calculations.IncludeChassisSelfWeight=true;p.Calculations.ChassisMassKg=700;p.Calculations.IncludeHangerAssemblyWeight=true;p.Calculations.HangerAssemblyMassKg=800;
        p.Calculations.MachineCgOffsetXMm=Double.NaN;p.Calculations.HangerAttachmentOffsetXMm=Double.NaN;
        var excluded=Calc(p);Near(excluded.P5.PerComponentLoadN,15800,"Machine/chassis/hanger excluded from P5");Near(excluded.P6.GroupLoadN,63200,"Machine/chassis/hanger excluded from P6");Near(excluded.P7.GroupLoadN,29600,"Machine/chassis/hanger excluded from P7");
        Check(SafetyPitCalculationEngine.IsCurrent(r,p),"Unrelated chassis-only inputs do not stale safety/pit values");
        p.Suspension.RopingRatio=1;p.Suspension.RopeCount=-5;p.Suspension.RopeDiameterMm=Double.NaN;Near(Calc(p).P6.GroupLoadN,63200,"Rope count/diameter and 1:1 versus note 2:1 do not change event load");
        p.Shaft.WidthMm=1800;p.Shaft.DepthMm=2100;Check(SafetyPitCalculationEngine.IsCurrent(r,p),"Unrelated shaft changes do not alter explicitly quantified event loads");

        p=NoteFixture();p.Calculations.CarSideAdditionalSuspendedMassKg=null;p.Calculations.SafetyPit.CarAdditionalMassAllocation=null;r=Calc(p);Near(r.P5.PerComponentLoadN,9800,"Absent optional additional mass introduces no invented mass");Near(r.P6.GroupLoadN,39200,"Absent optional car addition");
        p.Calculations.CarSideAdditionalSuspendedMassKg=0;r=Calc(p);Near(r.P5.PerComponentLoadN,9800,"Deliberate zero optional mass needs no allocation");
        p=NoteFixture();p.Calculations.SafetyPit.CarAdditionalMassAllocation="NoneParticipates";r=Calc(p);Near(r.P5.PerComponentLoadN,9800,"Explicit car additional exclusion");Near(r.P6.GroupLoadN,39200,"Explicit event exclusion does not mutate suspended mass");Near(p.Calculations.CarSideAdditionalSuspendedMassKg,600,"Shared mass preserved after exclusion");
        p.Calculations.SafetyPit.CarAdditionalMassAllocation="FractionParticipates";p.Calculations.SafetyPit.CarEventAdditionalMassPercent=25;r=Calc(p);Near(r.P5.ParticipatingMassKg,1130,"25 percent car effective mass reused");Near(r.P5.PerComponentLoadN,11300,"P5 percentage allocation");Near(r.P6.GroupLoadN,45200,"P6 percentage allocation");
        p.Calculations.SafetyPit.CarEventAdditionalMassPercent=0;Near(Calc(p).P6.GroupLoadN,39200,"Explicit zero percent allocation");p.Calculations.SafetyPit.CarEventAdditionalMassPercent=100;Near(Calc(p).P6.GroupLoadN,63200,"100 percent allocation");
        p.Calculations.SafetyPit.CarAdditionalMassAllocation="AllParticipates";p.Calculations.SafetyPit.CarEventAdditionalMassPercent=Double.NaN;r=Calc(p);Near(r.P6.GroupLoadN,63200,"Inactive fractional field cannot block all-participates choice");Check(SafetyPitCalculationEngine.IsCurrent(r,p),"Inactive malformed percentage cannot invalidate available event results");
        p.Calculations.CounterweightSideAdditionalSuspendedMassKg=200;p.Calculations.SafetyPit.CounterweightAdditionalMassAllocation="FractionParticipates";p.Calculations.SafetyPit.CounterweightEventAdditionalMassPercent=25;r=Calc(p);Near(r.P7.ParticipatingMassKg,790,"Counterweight percentage allocation");Near(r.P7.GroupLoadN,31600,"Counterweight additional mass counted once");Near(r.P6.GroupLoadN,63200,"CW additional mass never crosses to car event");

        p=NoteFixture();p.Calculations.SafetyPit.CarBufferCount=2;p.Calculations.SafetyPit.CarBufferSharing="EqualSharing";p.Calculations.SafetyPit.CounterweightBufferCount=4;p.Calculations.SafetyPit.CounterweightBufferSharing="EqualSharing";r=Calc(p);Near(r.P6.GroupLoadN,63200,"Two-buffer total group unchanged");Near(r.P6.PerComponentLoadN,31600,"Two-buffer explicit equal-sharing per component");Check(r.P6.ComponentCount==2,"Car physical buffer count explicit");Near(r.P7.GroupLoadN,29600,"Four-buffer CW group unchanged");Near(r.P7.PerComponentLoadN,7400,"Four-buffer explicit equal-sharing per component");
        p.Calculations.SafetyPit.CarBufferSharing="Single";Unavailable(Calc(p).P6,"UNSUPPORTED_CONFIGURATION","Multiple buffers cannot use single-buffer model");
        p.Calculations.SafetyPit.CarBufferSharing=null;Unavailable(Calc(p).P6,"MISSING_INPUT","No automatic equal sharing for multiple buffers");
        p=NoteFixture();p.Calculations.SafetyPit.CarBufferCount=2;p.Calculations.SafetyPit.CarBufferSharing="MeasuredUnequalSharing";Unavailable(Calc(p).P6,"UNSUPPORTED_CONFIGURATION","Unsupported unequal-sharing recipe unavailable");p.Calculations.SafetyPit.CarBufferCount=1;Check(Available(Calc(p).P6),"Inactive multiple-buffer sharing cannot block a single buffer");
        p=NoteFixture();p.Calculations.SafetyPit.CarBufferSharing=null;p.Calculations.SafetyPit.CounterweightBufferSharing=null;Check(Available(Calc(p).P6)&&Available(Calc(p).P7),"One physical buffer needs no redundant sharing decision");

        p=NoteFixture();p.Calculations.SafetyPit.SafetyGearType="Instantaneous";r=Calc(p);Unavailable(r.P5,"UNSUPPORTED_CONFIGURATION","Unsupported safety gear type");Check(Available(r.P6)&&Available(r.P7),"Unsupported safety gear does not block independent buffers");
        p.Calculations.SafetyPit.SafetyGearType="Progressive";p.Calculations.SafetyPit.CarGuideCount=3;r=Calc(p);Unavailable(r.P5,"UNSUPPORTED_CONFIGURATION","Three car guides not silently extrapolated");Check(Available(r.P6)&&Available(r.P7),"Guide recipe independent of buffer recipe");
        p.Calculations.SafetyPit.CarGuideCount=null;Unavailable(Calc(p).P5,"MISSING_INPUT","Car guide count not derived by counting CAD lines");p.Calculations.SafetyPit.CarGuideCount=0;Unavailable(Calc(p).P5,"INVALID_INPUT","Zero guides invalid");
        p=NoteFixture();p.Calculations.SafetyPit.CarBufferCount=null;Unavailable(Calc(p).P6,"MISSING_INPUT","Car buffer physical quantity unresolved");p.Calculations.SafetyPit.CarBufferCount=0;Unavailable(Calc(p).P6,"INVALID_INPUT","Zero car buffers invalid");p.Calculations.SafetyPit.CounterweightBufferCount=-2;Unavailable(Calc(p).P7,"INVALID_INPUT","Negative CW buffer count invalid");

        p=NoteFixture();p.Calculations.SafetyPit=null;r=Calc(p);Unavailable(r.P5,"MISSING_INPUT","Absent new optional group gives unavailable P5");Unavailable(r.P6,"MISSING_INPUT","Absent new optional group gives unavailable P6");Unavailable(r.P7,"MISSING_INPUT","Absent new optional group gives unavailable P7");
        p=NoteFixture();p.Elevator.CapacityKg=null;r=Calc(p);Unavailable(r.P5,"MISSING_INPUT","Missing capacity makes P5 unavailable");Unavailable(r.P6,"MISSING_INPUT","Missing capacity makes P6 unavailable");Check(Available(r.P7),"Missing car capacity leaves CW buffer independent");
        p=NoteFixture();p.Calculations.CarMassKg=0;r=Calc(p);Unavailable(r.P5,"INVALID_INPUT","Zero required empty-car mass invalid");Unavailable(r.P6,"INVALID_INPUT","Zero required car mass buffer invalid");Check(Available(r.P7),"Invalid car mass does not block CW buffer");
        p=NoteFixture();p.Calculations.CounterweightMassKg=null;r=Calc(p);Unavailable(r.P7,"MISSING_INPUT","Missing CW mass unavailable, no zero reaction");Check(Available(r.P5)&&Available(r.P6),"Missing CW mass leaves car results independent");
        p=NoteFixture();p.Calculations.CarSideAdditionalSuspendedMassKg=-1;r=Calc(p);Unavailable(r.P5,"INVALID_INPUT","Negative effective additional mass invalid");Unavailable(r.P6,"INVALID_INPUT","Negative optional car mass never deducted");
        p=NoteFixture();p.Calculations.SafetyPit.CarAdditionalMassAllocation=null;r=Calc(p);Unavailable(r.P5,"MISSING_INPUT","Positive effective suspended mass needs explicit event allocation");Unavailable(r.P6,"MISSING_INPUT","No silent compensation allocation in live projects");
        p.Calculations.SafetyPit.CarAdditionalMassAllocation="FractionParticipates";p.Calculations.SafetyPit.CarEventAdditionalMassPercent=null;Unavailable(Calc(p).P6,"MISSING_INPUT","Missing event percentage unavailable");
        foreach(double value in new[]{-1.0,101.0,Double.NaN,Double.PositiveInfinity}){p.Calculations.SafetyPit.CarEventAdditionalMassPercent=value;Unavailable(Calc(p).P5,"INVALID_INPUT","Invalid event percentage "+value);}
        p=NoteFixture();p.Calculations.SafetyPit.CarAdditionalMassAllocation="UnknownAllocation";Unavailable(Calc(p).P6,"INVALID_INPUT","Unknown allocation not silently adopted");
        foreach(double value in new[]{Double.NaN,Double.PositiveInfinity,-5.0}){p=NoteFixture();p.Calculations.CounterweightMassKg=value;Unavailable(Calc(p).P7,"INVALID_INPUT","Nonfinite/negative CW mass "+value);}
        p=NoteFixture();p.Elevator.CapacityKg=Double.MaxValue;p.Calculations.CarMassKg=Double.MaxValue;Unavailable(Calc(p).P6,"INVALID_INPUT","Overflow cannot yield an infinite load");

        p=NoteFixture();r=Calc(p);p.Calculations.CarMassKg=501;Check(!SafetyPitCalculationEngine.IsCurrent(r,p),"Changed shared car mass stales event result");p.Calculations.CarMassKg=500;p.Calculations.SafetyPit.CarGuideCount=3;Check(!SafetyPitCalculationEngine.IsCurrent(r,p),"Changed safety physical quantity stales event result");p.Calculations.SafetyPit.CarGuideCount=2;p.Calculations.SafetyPit.CarAdditionalMassAllocation="NoneParticipates";Check(!SafetyPitCalculationEngine.IsCurrent(r,p),"Changed event allocation stales prior result");
        p=NoteFixture();p.Calculations.SafetyPit.CarAdditionalMassAllocation="FractionParticipates";p.Calculations.SafetyPit.CarEventAdditionalMassPercent=35;p.Calculations.SafetyPit.CarBufferCount=2;p.Calculations.SafetyPit.CarBufferSharing="EqualSharing";p.Calculations.Mode="Basic";
        string file=Path.Combine(outDir,"SYNTHETIC-SafetyPit.elevator.json");var session=new ProjectSession(p);session.Save(file);session.Open(file);var reopened=session.Project;r=Calc(reopened);Check(reopened.Calculations.Mode=="Basic"&&reopened.Calculations.SafetyPit.CarBufferSharing=="EqualSharing"&&reopened.Calculations.SafetyPit.CarEventAdditionalMassPercent==35,"Safety choices save/reopen with existing calculation mode");Near(r.P5.PerComponentLoadN,11900,"Reopened event allocation");Near(r.P6.PerComponentLoadN,23800,"Reopened group/per-buffer meaning");
        string legacy=Path.Combine(bin,"reviewed-example.elevator.json");var old=ProjectJson.Deserialize(File.ReadAllText(legacy));Check(old.Calculations.SafetyPit==null,"Older project opens with missing safety inputs");Check(CalculationModes.Resolve(old.Calculations)=="Basic","Old empty project keeps Basic mode resolution");

        // Real source-bound drawing geometry proves the separate input group does not change chassis results.
        var ctx=Wire.Read<DesignContext>(Path.Combine(bin,"reviewed-context.json"));ctx.ChassisEvidence=Wire.Read<ChassisGeometryEvidence>(Path.Combine(bin,"reviewed-chassis-evidence.json"));
        var basic=ProjectJson.Deserialize(File.ReadAllText(legacy));basic.Calculations=new ChassisCalculationInputs{Mode="Basic",CarMassKg=500,CounterweightMassKg=800,MachineMassKg=300};basic.Elevator.CapacityKg=450;basic.Shaft.WidthMm=1450;basic.Shaft.DepthMm=1600;basic.Suspension.RopingRatio=1;
        var bw=new CalculationWorkspace();var br=bw.Calculate(basic,ctx);Check(br.Reactions!=null,"Basic baseline source-bound reactions available without pit inputs");basic.Calculations.SafetyPit=Inputs();basic.Calculations.SafetyPit.CarGuideCount=3;Check(bw.IsCurrent(basic,ctx),"Safety-only edit preserves Basic result currency");var br2=bw.Calculate(basic,ctx);foreach(var bearing in br.Reactions)Near(br2.Reactions.Single(v=>v.Id==bearing.Id).UpwardN,bearing.UpwardN,"Basic reaction unchanged "+bearing.Id);
        string advancedFixture=Path.Combine(automationRoot,"tests/fixtures/SYNTHETIC.elevator.json");var advanced=ProjectJson.Deserialize(File.ReadAllText(advancedFixture));var aw=new CalculationWorkspace();var ar=aw.Calculate(advanced,ctx);Check(ar.Reactions!=null,"Existing Advanced baseline available");advanced.Calculations.SafetyPit=Inputs();advanced.Calculations.SafetyPit.CarBufferCount=0;Check(aw.IsCurrent(advanced,ctx),"Safety-only edit preserves Advanced result currency");var ar2=aw.Calculate(advanced,ctx);foreach(var bearing in ar.Reactions)Near(ar2.Reactions.Single(v=>v.Id==bearing.Id).UpwardN,bearing.UpwardN,"Advanced reaction unchanged "+bearing.Id);
        var drawing=new ElevatorDesignEngine().Calculate(basic,ctx);Check(drawing.CanGenerate,"Missing/unsupported pit inputs do not block established drawings");
        Wire.Write(Path.Combine(outDir,"safety-pit-tests.json"),new{Status="PASS",Assertions=assertions,Method="Independent note equations; canonical API; source-bound Basic/Advanced preservation; save/reopen",NoteFixtureOnly=true});
        Console.WriteLine("PASS: "+assertions+" safety gear / pit load assertions.");
    }
}
