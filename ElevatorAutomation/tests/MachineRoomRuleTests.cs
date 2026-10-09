using System;
using System.IO;
using System.Web.Script.Serialization;
using Elevator.Core.Project;
using Elevator.Core.Presets;
using Elevator.Core.MachineRoom;
public static class MachineRoomRuleTests
{
    static int count;
    static void Check(bool value,string name){count++;if(!value)throw new Exception(name);}
    public static int Main(string[] args)
    {
        foreach(var item in new[]{new[]{519.9,0.0},new[]{520.0,250.0},new[]{800.0,250.0},new[]{800.1,400.0},new[]{1100.0,400.0},new[]{1100.1,-1.0}})
        {
            var result=MachineRoomRules.SelectDeflector(item[0]);
            Check(result.IsRequired==(item[0]>=520),"sheave presence "+item[0]);
            Check(item[1]<=0?!result.PitchDiameterMm.HasValue:result.PitchDiameterMm==item[1],"sheave diameter "+item[0]);
            Check(result.RuleSource.StartsWith("Project Design Rule"),"source "+item[0]);
        }
        foreach(var item in new[]{new[]{699.9,100.0},new[]{700.0,150.0},new[]{849.9,150.0},new[]{850.0,200.0},new[]{1100.0,200.0},new[]{1100.1,-1.0}})
        {
            var result=MachineRoomRules.SelectGovernorOffset(item[0]);Check(item[1]<0?!result.OffsetFromCarGuideAxisMm.HasValue:result.OffsetFromCarGuideAxisMm==item[1],"governor boundary "+item[0]);
        }
        foreach(double value in new[]{Double.NaN,Double.PositiveInfinity,-1.0})
        {
            bool rejected=false;try{MachineRoomRules.SelectDeflector(value);}catch(ArgumentException){rejected=true;}Check(rejected,"invalid TZ");
        }
        var profile=new JavaScriptSerializer().Deserialize<EquipmentProfile>(File.ReadAllText(args[0]));
        var input=new ProjectInput{SW=1450,SD=1600};var design=DesignRules.Calculate(input.SW,input.SD,profile,100);
        var ext=new MachineRoomExtensions{Left=525,Right=525,Front=900,Rear=0};
        var mr=MachineRoomRules.Calculate(input,design,ext);
        Check(mr.Deflector.PitchDiameterMm==250&&mr.Governor.OffsetFromCarGuideAxisMm==150,"baseline shared selection");
        Check(mr.Rules.Exists(r=>r.RuleId=="MR-ROPE-001"&&r.Status=="FAIL"),"250 suspension compatibility conflict");
        Check(mr.Reeving.VerticalCenterOffsetMm==null&&mr.Reeving.CalculatedWrapAngleDeg==null,"vertical inputs not invented");
        Check(mr.AvailableDeflectorPitchDiametersMm.Length==2&&mr.AvailableDeflectorPitchDiametersMm[0]==250&&mr.AvailableDeflectorPitchDiametersMm[1]==400,"market sizes restricted to 250 and 400");
        Check(!mr.Reeving.AllowDeflectorDiameterSubstitution&&mr.Reeving.TargetTractionWrapAngleDeg==152,"fixed diameter future wrap target");
        Check(mr.LayoutStatus=="NOT YET EVALUATED"&&mr.Reeving.WrapAngleStatus=="NOT YET EVALUABLE"&&mr.CodeDiameterRatioStatus=="FAIL","independent initial validation statuses");
        Check(mr.Reeving.RopeEntryAngleDeg==null&&mr.Reeving.RopeExitAngleDeg==null&&mr.Reeving.RequiredChassisElevationAdjustmentMm==null,"future angles and chassis adjustment not invented");
        // Simulate a future solver result: even a successful wrap must not clear the code check.
        mr.LayoutStatus="PASS";mr.Reeving.WrapAngleStatus="PASS";mr.Reeving.CalculatedWrapAngleDeg=152;mr.Reeving.VerticalCenterOffsetMm=300;
        var independent=MachineRoomRuleRegistry.Evaluate(input,design,mr);
        Check(independent.Exists(r=>r.RuleId=="MR-WRAP-001"&&r.Status=="PASS")&&independent.Exists(r=>r.RuleId=="MR-ROPE-001"&&r.Status=="FAIL"),"wrap pass cannot resolve diameter conflict");
        Check(mr.Deflector.PitchDiameterMm==250&&mr.CodeDiameterRatioStatus=="FAIL","no automatic diameter substitution or code-status clearing");
        input.SD=1800;design=DesignRules.Calculate(input.SW,input.SD,profile,100);input.SuspensionRopeDiameterMm=10;
        mr=MachineRoomRules.Calculate(input,design,ext);Check(mr.Rules.Exists(r=>r.RuleId=="MR-ROPE-001"&&r.Status=="PASS - NUMERIC CHECK ONLY"),"400 / 10 ratio");
        input.SuspensionRopeDiameterMm=11;mr=MachineRoomRules.Calculate(input,design,ext);Check(mr.Rules.Exists(r=>r.RuleId=="MR-ROPE-001"&&r.Status=="FAIL"),"400 / 11 ratio");
        input.MachineRoomLeftExtension=600;bool invalid=false;try{MachineRoomRules.Calculate(input,design,ext);}catch(ArgumentException){invalid=true;}Check(invalid,"unsupported extension override");
        Console.WriteLine("PASS: "+count+" machine-room Core checks.");return 0;
    }
}
