using System;
using System.Linq;
using System.Collections.Generic;
using Elevator.Core.Project;
namespace Elevator.Core.Calculations
{
 public static class CalculationModes
 {
  public static string Resolve(ChassisCalculationInputs c)
  {
   if(c==null)return "Basic";if(c.Mode!=null)return c.Mode;
   // Any pre-mode calculation data belongs to the pre-existing Advanced workflow.
   return typeof(ChassisCalculationInputs).GetFields().Any(f=>f.Name!="Mode"&&f.Name!="SafetyPit"&&f.Name!="CarGuideForces"&&f.GetValue(c)!=null)?"Advanced":"Basic";
  }
 }
 public sealed class SimplifiedDistances
 {
  public LocalPoint LoadReference;
  public double FrontRowY,RearRowY,LongitudinalSpanMm,LoadFromFrontMm,LoadToRearMm;
  public double FrontTransverseSpanMm,RearTransverseSpanMm;
  public double FrontLoadFromLeftMm,FrontLoadToRightMm,RearLoadFromLeftMm,RearLoadToRightMm;
  public double FrontLoadN,RearLoadN;
 }
 public static class SimplifiedVerticalSolver
 {
  public const string Model="TwoStageVerticalMachineDatum-v1";
  static void Require(bool ok,string message){if(!ok)throw new ArgumentException(message);}
  public static ChassisCalculationResult Solve(ChassisCalculationGeometry g,double force)
  {
   var r=new ChassisCalculationResult{Mode="Basic",ModelVersion=Model};
   try{
    Require(g!=null&&g.Units=="mm"&&ChassisReactionSolver.Finite(force)&&force>0,"Finite positive vertical load and mm geometry required.");
    Require(g.Bearings!=null&&g.Bearings.Count==4&&g.Bearings.Select(b=>b.Id).Distinct().Count()==4,"Four unique bearing contact regions required.");
    Func<string,BearingSeat> seat=id=>g.Bearings.SingleOrDefault(b=>b.Id==id);
    var p1=seat("P1");var p2=seat("P2");var p3=seat("P3");var p4=seat("P4");Require(p1!=null&&p2!=null&&p3!=null&&p4!=null,"Required orientation: P1 right/rear, P2 right/front, P3 left/rear, P4 left/front.");
    foreach(var b in g.Bearings){Require(ChassisReactionSolver.Finite(b.Centre),"Nonfinite bearing: "+b.Id);var contact=ChassisGeometry.ContactCentroid(b.ContactBoundary);Require(Math.Abs(contact.X-b.Centre.X)<.02&&Math.Abs(contact.Y-b.Centre.Y)<.02&&Math.Abs(contact.Z-b.Centre.Z)<.02,"Bearing centre must be the contact-area centroid: "+b.Id);Require(Math.Abs(b.Centre.Z-g.BearingPlaneZMm)<.02,"Noncoplanar bearing elevations.");}
    // Numerical equality only: this recipe cannot silently replace skew rows by averages.
    Require(Math.Abs(p2.Centre.Y-p4.Centre.Y)<1e-7&&Math.Abs(p1.Centre.Y-p3.Centre.Y)<1e-7,"Incompatible transverse bearing rows; Basic requires aligned front P4/P2 and rear P3/P1 rows.");
    var point=g.TractionCentre;Require(ChassisReactionSolver.Finite(point),"Native traction datum unavailable.");
    var d=new SimplifiedDistances{LoadReference=point,FrontRowY=p4.Centre.Y,RearRowY=p3.Centre.Y,FrontTransverseSpanMm=p2.Centre.X-p4.Centre.X,RearTransverseSpanMm=p1.Centre.X-p3.Centre.X};
    d.LongitudinalSpanMm=d.RearRowY-d.FrontRowY;Require(d.LongitudinalSpanMm>0&&d.FrontTransverseSpanMm>0&&d.RearTransverseSpanMm>0,"Invalid P orientation or nonpositive bearing spans.");
    d.LoadFromFrontMm=point.Y-d.FrontRowY;d.LoadToRearMm=d.RearRowY-point.Y;
    d.FrontLoadFromLeftMm=point.X-p4.Centre.X;d.FrontLoadToRightMm=p2.Centre.X-point.X;d.RearLoadFromLeftMm=point.X-p3.Centre.X;d.RearLoadToRightMm=p1.Centre.X-point.X;
    d.FrontLoadN=force*d.LoadToRearMm/d.LongitudinalSpanMm;d.RearLoadN=force*d.LoadFromFrontMm/d.LongitudinalSpanMm;
    r.SimplifiedGeometry=d;r.TotalVerticalLoadN=force;
    r.Reactions=new List<BearingReaction>{new BearingReaction{Id="P1",PositionMm=p1.Centre,UpwardN=d.RearLoadN*d.RearLoadFromLeftMm/d.RearTransverseSpanMm},new BearingReaction{Id="P2",PositionMm=p2.Centre,UpwardN=d.FrontLoadN*d.FrontLoadFromLeftMm/d.FrontTransverseSpanMm},new BearingReaction{Id="P3",PositionMm=p3.Centre,UpwardN=d.RearLoadN*d.RearLoadToRightMm/d.RearTransverseSpanMm},new BearingReaction{Id="P4",PositionMm=p4.Centre,UpwardN=d.FrontLoadN*d.FrontLoadToRightMm/d.FrontTransverseSpanMm}};
    r.Loads.Add(new LoadNode{Id="SimplifiedVerticalResultant",PositionMm=point,ForceN=new LocalPoint(0,0,-force),AssumedLocation=true,Source="Native traction-sheave centre used as the user-accepted assumed machine load reference, not measured CG."});
    r.VerticalResidualN=r.Reactions.Sum(v=>v.UpwardN)-force;r.MomentXResidualNmm=r.Reactions.Sum(v=>v.PositionMm.Y*v.UpwardN)-force*point.Y;r.MomentYResidualNmm=force*point.X-r.Reactions.Sum(v=>v.PositionMm.X*v.UpwardN);
    Require(r.Reactions.All(v=>ChassisReactionSolver.Finite(v.UpwardN))&&Math.Abs(r.VerticalResidualN.Value)<force*1e-8&&Math.Abs(r.MomentXResidualNmm.Value)<force*Math.Max(1,d.LongitudinalSpanMm)*1e-8&&Math.Abs(r.MomentYResidualNmm.Value)<force*Math.Max(1,d.FrontTransverseSpanMm)*1e-8,"Numerical overflow or force/moment equilibrium failure.");
    r.Status=r.Reactions.Any(v=>v.UpwardN< -force*1e-9)?"UPLIFT_REVIEW_REQUIRED":"CALCULATED_UNDER_ASSUMPTIONS";
    if(r.Status=="UPLIFT_REVIEW_REQUIRED")r.Issues.Add(new CalculationIssue("Bearings","Negative reactions retained; compression-only bearing seats require uplift/restraint review."));
    r.Assumptions.Add("Simplified static vertical distribution at the assumed machine load reference.");r.Assumptions.Add("Total vertical load applied once at native traction-sheave centre. Not a measured machine CG. No separate rope/sheave actions or assembly CGs added.");r.Assumptions.Add("Declared two-stage sharing: front/rear shares by longitudinal lever arms, then front P4/P2 and rear P3/P1 by actual transverse lever arms at the same X station. This additional sharing assumption makes four reactions determinate; it is not a stiffness or structural verification.");
   }catch(ArgumentException e){r.Status="INVALID_GEOMETRY_OR_LOAD";r.Reactions=null;r.SimplifiedGeometry=null;r.Issues.Add(new CalculationIssue("Basic.Geometry",e.Message));}
   return r;
  }
 }
 public sealed class SimplifiedCalculationEngine
 {
  static string Fingerprint(ElevatorProject p,ChassisCalculationGeometry g){var c=p.Calculations;return CalculationFingerprint.Hash(new{Version=SimplifiedVerticalSolver.Model,Mode=CalculationModes.Resolve(c),p.Elevator.CapacityKg,c.CarMassKg,c.CounterweightMassKg,c.MachineMassKg,c.CarSideAdditionalSuspendedMassKg,c.CounterweightSideAdditionalSuspendedMassKg,c.IncludeChassisSelfWeight,ChassisMass=c.IncludeChassisSelfWeight==true?c.ChassisMassKg:null,c.IncludeHangerAssemblyWeight,HangerMass=c.IncludeHangerAssemblyWeight==true?c.HangerAssemblyMassKg:null,RopingRatio=p.Suspension.RopingRatio,p.Suspension.RopeCount,Geometry=g});}
  public static bool IsCurrent(ChassisCalculationResult r,ElevatorProject p,ChassisCalculationGeometry g,DesignContext ctx){return p!=null&&p.Calculations!=null&&r!=null&&r.Mode=="Basic"&&r.ModelVersion==SimplifiedVerticalSolver.Model&&CalculationModes.Resolve(p.Calculations)=="Basic"&&r.Reactions!=null&&ChassisGeometryProvenance.Validate(p,g,ctx).IsValid&&r.InputFingerprint==Fingerprint(p,g);}
  public ChassisCalculationResult Calculate(ElevatorProject p,ChassisCalculationGeometry g,DesignContext ctx)
  {
   var r=new ChassisCalculationResult{Mode="Basic",ModelVersion=SimplifiedVerticalSolver.Model};var valid=ChassisGeometryProvenance.Validate(p,g,ctx);if(!valid.IsValid){r.Status=valid.Status;r.Issues.AddRange(valid.Issues);return r;}
   if(p.Calculations==null){r.Issues.Add(new CalculationIssue("Calculations","Component masses are not provided."));return r;}var c=p.Calculations;
   if(CalculationModes.Resolve(c)!="Basic"){r.Status="INVALID_INPUT";r.Issues.Add(new CalculationIssue("Calculations.Mode","Select Basic explicitly; populated legacy Advanced projects are not converted automatically."));return r;}
   bool invalid=false;var masses=new Dictionary<string,double>();
   Action<string,string,double?,bool> add=(field,label,value,required)=>{if(!value.HasValue){if(required)r.Issues.Add(new CalculationIssue(field,"NOT PROVIDED; mass in kg required."));else masses[label+" (not included)"]=0;return;}if(!ChassisReactionSolver.Finite(value.Value)||(required?value.Value<=0:value.Value<0)){invalid=true;r.Issues.Add(new CalculationIssue(field,"Finite "+(required?"positive":"nonnegative")+" mass required."));return;}masses[label]=value.Value;};
   add("Elevator.CapacityKg","Rated capacity",p.Elevator.CapacityKg,true);add("Calculations.CarMassKg","Empty car",c.CarMassKg,true);add("Calculations.CounterweightMassKg","Counterweight",c.CounterweightMassKg,true);add("Calculations.MachineMassKg","Machine",c.MachineMassKg,true);
   add("Calculations.CarSideAdditionalSuspendedMassKg","Effective car-side additions / compensation",c.CarSideAdditionalSuspendedMassKg,false);add("Calculations.CounterweightSideAdditionalSuspendedMassKg","Effective CW-side additions / compensation",c.CounterweightSideAdditionalSuspendedMassKg,false);
   if(c.IncludeChassisSelfWeight==true)add("Calculations.ChassisMassKg","Chassis assembly",c.ChassisMassKg,true);else masses["Chassis assembly (not included)"]=0;
   if(c.IncludeHangerAssemblyWeight==true)add("Calculations.HangerAssemblyMassKg","Hanger assembly",c.HangerAssemblyMassKg,true);else masses["Hanger assembly (not included)"]=0;
   if(!p.Suspension.RopingRatio.HasValue||p.Suspension.RopingRatio.Value!=1){invalid=true;r.Issues.Add(new CalculationIssue("Suspension.RopingRatio","Only the supported explicit static 1:1 case is implemented."));}
   if(r.Issues.Count>0){r.Status=invalid?"INVALID_INPUT":"MISSING_INPUT";return r;}
   double mass=masses.Values.Sum();r=SimplifiedVerticalSolver.Solve(g,mass*ChassisCalculationEngine.GravityMetresPerSecondSquared);r.TotalMassKg=mass;r.MassCompositionKg=masses;r.InputFingerprint=Fingerprint(p,g);
   if(r.Reactions!=null){r.CarBundleTensionN=(p.Elevator.CapacityKg.Value+c.CarMassKg.Value+(c.CarSideAdditionalSuspendedMassKg??0))*9.81;r.CounterweightBundleTensionN=(c.CounterweightMassKg.Value+(c.CounterweightSideAdditionalSuspendedMassKg??0))*9.81;if(p.Suspension.RopeCount.HasValue&&p.Suspension.RopeCount.Value>0){r.CarPerRopeTensionN=r.CarBundleTensionN/p.Suspension.RopeCount.Value;r.CounterweightPerRopeTensionN=r.CounterweightBundleTensionN/p.Suspension.RopeCount.Value;}r.Assumptions.Add("Optional blank side additions are not included; supplied effective masses are counted once, including compensation assigned to that side. Optional chassis/hanger mass is counted only when included; no automatic member weights without full properties/lengths. Rope tensions are separate informational results and are not added to the resultant again.");}
   return r;
  }
 }
}
