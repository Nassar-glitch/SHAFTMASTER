using System;
using Elevator.Core;
public static class ReevingTests
{
    static int count;static void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
    static void Near(double a,double b,string message){Check(Math.Abs(a-b)<1e-7,message+": "+a+" / "+b);}
    static double Distance(LocalPoint a,LocalPoint b){return Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));}
    public static int Main()
    {
        var selector=new PreliminaryMachineBeamSelector();
        foreach(double capacity in new[]{479.9,480,800,800.1,1500,1500.1})foreach(double span in new[]{2199.9,2200,2200.1})
        {
            var b=selector.Select(capacity,span);double expected=capacity<480?(span<2200?140:160):(capacity<=800?160:(capacity<=1500?200:240));
            Near(b.NominalDepthMm,expected,"UPN boundary");Check(b.StructuralStatus==(capacity<480&&span>=2200?"CALCULATION REQUIRED":"PRELIMINARY"),"No invented structural pass");
        }
        foreach(double diameter in new[]{6.5,8,10,11,12})foreach(int ropes in new[]{4,5,6,7}){SuspensionProjectRules.Validate(diameter,ropes);Check(true,"Supported rope data");}
        foreach(double diameter in new[]{6,9,Double.NaN}){bool rejected=false;try{SuspensionProjectRules.Validate(diameter,6);}catch(ArgumentException){rejected=true;}Check(rejected,"Unsupported rope diameter");}
        var reference=ReevingSolver.Calculate(new LocalPoint(1575.7524613692763,1988.134178577678,0),new LocalPoint(1200.6132967400004,1348.3411850716222,0),520,250);
        Near(Distance(reference.TractionSheaveCenter,reference.TractionTangentPoint),260,"Reference traction tangency radius");
        Near(Distance(reference.DeflectorSheaveCenter,reference.DeflectorTangentPoint),125,"Reference deflector tangency radius");
        Console.WriteLine("Reference circles calculated wrap: "+reference.CalculatedWrapAngleDeg+" degrees (annotation is independently ~160.2169).");
        foreach(double wrap in new[]{151.9,152,160})
        {
            double angle=(180-wrap)*Math.PI/180,h=415,v=(h*Math.Cos(angle)-135)/Math.Sin(angle);
            var solution=ReevingSolver.Calculate(new LocalPoint(0,v,0),new LocalPoint(-h,0,0),520,250);
            Near(solution.CalculatedWrapAngleDeg.Value,wrap,"Exact wrap");Check(solution.WrapAngleStatus==(wrap>=152?"PASS":"FAIL"),"Independent wrap criterion");
            var radius=new LocalPoint(solution.TractionTangentPoint.X-solution.TractionSheaveCenter.X,solution.TractionTangentPoint.Y-solution.TractionSheaveCenter.Y,0);
            Near(radius.X*solution.RopeExitDirection.X+radius.Y*solution.RopeExitDirection.Y,0,"Tangent orthogonality");
        }
        foreach(double desired in new[]{349.9,350,650,999.9,1000,1000.1})
        {
            double angle=28*Math.PI/180,above=180+140+335.368112947966;
            double vertical=desired+above-125;
            double h=(135+vertical*Math.Sin(angle))/Math.Cos(angle);
            var input=new VerticalArrangementInput{CarDropX=0,CwDropX=-(h+385),TractionDiameterMm=520,DeflectorDiameterMm=250,MachineCenterAboveSteelMm=335.368112947966,FixedLevelingDepthMm=180,SteelDepthMm=140,StackedSteelLevels=1,AdjustableDeflectorHangerConfirmed=true};
            var result=ReevingSolver.SolveVertical(input);Near(result.RequiredConcreteBeamHeightMm.Value,desired,"Required concrete height");
            if(desired>1000){Check(result.Status=="REEVING REDESIGN REQUIRED"&&!result.ConcreteBeamHeightMm.HasValue,"Upper bound not exceeded");}
            else {Near(result.ConcreteBeamHeightMm.Value,Math.Max(350,desired),"Minimum practical arrangement without rounding");Check(result.Reeving.WrapAngleStatus=="PASS","Solved wrap passes");Check(result.DeflectorSlabClearanceStatus=="NOT YET EVALUABLE"&&result.DeflectorToSlabClearanceMm>=-1e-7,"Measured slab separation without invented minimum");}
        }
        Check(ReevingSolver.Calculate(new LocalPoint(0,0,0),new LocalPoint(10,10,0),520,250).GeometryStatus.StartsWith("INFEASIBLE"),"Intersecting sheaves rejected");
        var adjustable=new VerticalArrangementInput{CarDropX=0,CwDropX=-1000,TractionDiameterMm=520,DeflectorDiameterMm=400,MachineCenterAboveSteelMm=335,FixedLevelingDepthMm=180,SteelDepthMm=140,StackedSteelLevels=1,AdjustableDeflectorHangerConfirmed=true};
        var unspecified=ReevingSolver.SolveVertical(adjustable);adjustable.MinimumDeflectorSlabClearanceMm=300;var specified=ReevingSolver.SolveVertical(adjustable);
        Near(unspecified.RequiredVerticalCenterSeparationMm.Value,specified.RequiredVerticalCenterSeparationMm.Value,"Raising supports preserves reeving separation");
        Near(unspecified.RequiredHangerLengthMm.Value,specified.RequiredHangerLengthMm.Value,"Required hanger remains tied to separation");
        Check(specified.ConcreteSupportHeightMm>unspecified.ConcreteSupportHeightMm,"Supplied slab clearance raises concrete when needed");
        Near(specified.DeflectorToSlabClearanceMm.Value,300,"Actual supplied slab clearance");
        Check(unspecified.DeflectorSlabClearanceStatus=="NOT YET EVALUABLE"&&specified.DeflectorSlabClearanceStatus.StartsWith("PASS"),"Undefined clearance is not passed");
        Check(specified.HangerMechanicalStatus=="MECHANICAL REVIEW REQUIRED","No mechanical adequacy claim");
        Check(ReevingSolver.SolveVertical(new VerticalArrangementInput()).Status=="SUPPORT DEPENDENCY CONFIRMATION REQUIRED","No invented support dependency");
        foreach(double diameter in new[]{6.5,8.0})
        {
            var mr=MachineRoomRules.Calculate(new ProjectInput{SuspensionRopeDiameterMm=diameter,NumberOfSuspensionRopes=6},new ElevatorParameters{SW=1450,SD=1139.8,TZ=519.9},new MachineRoomExtensions());
            Check(mr.Rules.Exists(r=>r.RuleId=="MR-ROPE-MIN-001"&&r.Status==(diameter<8?"FAIL":"PASS - NUMERIC CHECK ONLY")),"Rope minimum checked even with absent deflector");
        }
        Console.WriteLine("PASS: "+count+" exact reeving and preliminary beam checks.");return 0;
    }
}
