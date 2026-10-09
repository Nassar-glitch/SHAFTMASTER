using System;
using System.Linq;
using System.Collections.Generic;
using Elevator.Core.Calculations;
namespace Elevator.Core.Project
{
    // DESIGN-FLOW stage 2 ("Flow foundation"): every remaining project input is a materialized preset field. While the
    // project follows presets (FollowsPresets = true), each unlocked field holds its preset, which may follow an upstream
    // driver (Q, P, v). Editing a field locks it; Reset unlocks it. Older projects (FollowsPresets null) are left unchanged
    // until the user chooses "Fill blanks from presets". No calculation equation is involved here.
    public static class ProjectPresets
    {
        public const string RopeCountKey="Suspension.RopeCount",RopeDiameterKey="Suspension.RopeDiameterMm",SheaveKey="Machine.TractionSheavePitchDiameterMm";
        public const string Progressive="Progressive",Instantaneous="Instantaneous",EnergyAccumulation="EnergyAccumulation",EnergyDissipation="EnergyDissipation",EqualSharing="EqualSharing";
        public static readonly string[] Pages={"GENERAL","SHAFT","FLOORS","MACHINE","SUSPENSION","RAILS","CALCULATIONS"};

        static ChassisCalculationInputs Calc(ElevatorProject p){if(p.Calculations==null)p.Calculations=new ChassisCalculationInputs();return p.Calculations;}
        static SafetyPitInputs Pit(ElevatorProject p){var c=Calc(p);if(c.SafetyPit==null)c.SafetyPit=new SafetyPitInputs();return c.SafetyPit;}
        static CarGuideForceInputs Guides(ElevatorProject p){var c=Calc(p);if(c.CarGuideForces==null)c.CarGuideForces=new CarGuideForceInputs();return c.CarGuideForces;}
        static ChassisCalculationInputs ReadCalc(ElevatorProject p){return p==null?null:p.Calculations;}
        static double? D(object v){return v==null?(double?)null:Convert.ToDouble(v,System.Globalization.CultureInfo.InvariantCulture);}
        static int? I(object v){return v==null?(int?)null:Convert.ToInt32(v,System.Globalization.CultureInfo.InvariantCulture);}
        static bool? B(object v){return v==null?(bool?)null:Convert.ToBoolean(v);}
        static double? Q(PresetContext c){return c.Project.Elevator==null?null:c.Project.Elevator.CapacityKg;}
        static double? V(PresetContext c){return c.Project.Elevator==null?null:c.Project.Elevator.SpeedMetresPerSecond;}
        static bool Advanced(PresetContext c){return CalculationModes.Resolve(c.Project.Calculations)=="Advanced";}
        static PresetField F(string page,string key,string label,Func<PresetContext,object> preset,Func<ElevatorProject,object> get,Action<ElevatorProject,object> set)
        {return new PresetField{Page=page,Key=key,Label=label,Materialized=true,Preset=preset,Stored=get,Store=set};}

        // Order matters: drivers (Q, v, P) come before the fields that follow them.
        public static readonly PresetField[] Fields=
        {
            F("GENERAL","Elevator.CapacityKg","Capacity (kg)",c=>c.Library.NewProject.CapacityKg,p=>p.Elevator.CapacityKg,(p,v)=>p.Elevator.CapacityKg=D(v)),
            F("GENERAL","Elevator.SpeedMetresPerSecond","Speed (m/s)",c=>c.Library.NewProject.SpeedMps,p=>p.Elevator.SpeedMetresPerSecond,(p,v)=>p.Elevator.SpeedMetresPerSecond=D(v)),
            F("SHAFT","Shaft.WidthMm","Shaft width · SW (mm)",c=>c.Library.NewProject.ShaftWidthMm,p=>p.Shaft.WidthMm,(p,v)=>p.Shaft.WidthMm=D(v)??0),
            F("SHAFT","Shaft.DepthMm","Shaft depth · SD (mm)",c=>c.Library.NewProject.ShaftDepthMm,p=>p.Shaft.DepthMm,(p,v)=>p.Shaft.DepthMm=D(v)??0),
            F("SHAFT","Shaft.PitDepthMm","Pit depth (mm)",c=>c.Library.NewProject.PitDepthMm,p=>p.Shaft.PitDepthMm,(p,v)=>p.Shaft.PitDepthMm=D(v)),
            F("SHAFT","Shaft.OverheadMm","Overhead (mm)",c=>c.Library.NewProject.OverheadMm,p=>p.Shaft.OverheadMm,(p,v)=>p.Shaft.OverheadMm=D(v)),
            F("MACHINE",SheaveKey,"Traction sheave pitch diameter (mm)",c=>c.Library.TractionSheavePitchDiameterMm,p=>p.Machine.TractionSheavePitchDiameterMm,(p,v)=>p.Machine.TractionSheavePitchDiameterMm=D(v)),
            F("SUSPENSION",RopeDiameterKey,"Rope diameter (mm)",c=>{var r=c.Library.RopesFor(Q(c));return r==null?(double?)null:r.DiameterMm;},p=>p.Suspension.RopeDiameterMm,(p,v)=>p.Suspension.RopeDiameterMm=D(v)),
            F("SUSPENSION",RopeCountKey,"Number of ropes",c=>{var r=c.Library.RopesFor(Q(c));return r==null?(int?)null:r.RopeCount;},p=>p.Suspension.RopeCount,(p,v)=>p.Suspension.RopeCount=I(v)),
            F("RAILS","Rails.BracketMaximumSpacingMm","Maximum bracket spacing (mm)",c=>c.Library.BracketMaximumSpacingMm,p=>p.Rails.BracketMaximumSpacingMm,(p,v)=>p.Rails.BracketMaximumSpacingMm=D(v)),
            F("CALCULATIONS","Calculations.CarMassKg","Empty car mass P (kg)",c=>{var m=c.Library.MassesFor(Q(c));return m==null?(double?)null:m.CarMassKg;},p=>ReadCalc(p)==null?null:p.Calculations.CarMassKg,(p,v)=>Calc(p).CarMassKg=D(v)),
            F("CALCULATIONS","Calculations.MachineMassKg","Machine mass (kg)",c=>{var m=c.Library.MassesFor(Q(c));return m==null?(double?)null:m.MachineMassKg;},p=>ReadCalc(p)==null?null:p.Calculations.MachineMassKg,(p,v)=>Calc(p).MachineMassKg=D(v)),
            F("CALCULATIONS","Calculations.CounterweightMassKg","Counterweight mass (kg)",c=>CounterweightPreset(c),p=>ReadCalc(p)==null?null:p.Calculations.CounterweightMassKg,(p,v)=>Calc(p).CounterweightMassKg=D(v)),
            F("CALCULATIONS","Calculations.CarSideAdditionalSuspendedMassKg","Car-side additional mass (kg)",c=>Advanced(c)?c.Library.Advanced.AdditionalMassKg:null,p=>ReadCalc(p)==null?null:p.Calculations.CarSideAdditionalSuspendedMassKg,(p,v)=>Calc(p).CarSideAdditionalSuspendedMassKg=D(v)),
            F("CALCULATIONS","Calculations.CounterweightSideAdditionalSuspendedMassKg","CW-side additional mass (kg)",c=>Advanced(c)?c.Library.Advanced.AdditionalMassKg:null,p=>ReadCalc(p)==null?null:p.Calculations.CounterweightSideAdditionalSuspendedMassKg,(p,v)=>Calc(p).CounterweightSideAdditionalSuspendedMassKg=D(v)),
            F("CALCULATIONS","Calculations.IncludeChassisSelfWeight","Chassis self-weight",c=>false,p=>ReadCalc(p)==null?null:p.Calculations.IncludeChassisSelfWeight,(p,v)=>Calc(p).IncludeChassisSelfWeight=B(v)),
            F("CALCULATIONS","Calculations.IncludeHangerAssemblyWeight","Hanger assembly weight",c=>false,p=>ReadCalc(p)==null?null:p.Calculations.IncludeHangerAssemblyWeight,(p,v)=>Calc(p).IncludeHangerAssemblyWeight=B(v)),
            F("CALCULATIONS","Calculations.ChassisWeightSourceOrExclusionReason","Chassis weight basis",c=>c.Library.Advanced.ExcludedWeightBasis,p=>ReadCalc(p)==null?null:p.Calculations.ChassisWeightSourceOrExclusionReason,(p,v)=>Calc(p).ChassisWeightSourceOrExclusionReason=v as string),
            F("CALCULATIONS","Calculations.HangerWeightSourceOrExclusionReason","Hanger weight basis",c=>c.Library.Advanced.ExcludedWeightBasis,p=>ReadCalc(p)==null?null:p.Calculations.HangerWeightSourceOrExclusionReason,(p,v)=>Calc(p).HangerWeightSourceOrExclusionReason=v as string),
            F("CALCULATIONS","Calculations.LoadCase","Load case",c=>c.Library.Advanced.LoadCase,p=>ReadCalc(p)==null?null:p.Calculations.LoadCase,(p,v)=>Calc(p).LoadCase=v as string),
            F("CALCULATIONS","Calculations.MachineCgOffsetXMm","Machine CG ΔX (mm)",c=>c.Library.Advanced.MachineCgOffsetXMm,p=>ReadCalc(p)==null?null:p.Calculations.MachineCgOffsetXMm,(p,v)=>Calc(p).MachineCgOffsetXMm=D(v)),
            F("CALCULATIONS","Calculations.MachineCgOffsetYMm","Machine CG ΔY (mm)",c=>c.Library.Advanced.MachineCgOffsetYMm,p=>ReadCalc(p)==null?null:p.Calculations.MachineCgOffsetYMm,(p,v)=>Calc(p).MachineCgOffsetYMm=D(v)),
            F("CALCULATIONS","Calculations.MachineCgOffsetZMm","Machine CG ΔZ (mm)",c=>c.Library.Advanced.MachineCgOffsetZMm,p=>ReadCalc(p)==null?null:p.Calculations.MachineCgOffsetZMm,(p,v)=>Calc(p).MachineCgOffsetZMm=D(v)),
            F("CALCULATIONS","Calculations.MachineCgSource","Machine CG basis",c=>c.Library.Advanced.MachineCgBasis,p=>ReadCalc(p)==null?null:p.Calculations.MachineCgSource,(p,v)=>Calc(p).MachineCgSource=v as string),
            F("CALCULATIONS","Calculations.TransferModel","Transfer model",c=>c.Library.Advanced.TransferModel,p=>ReadCalc(p)==null?null:p.Calculations.TransferModel,(p,v)=>Calc(p).TransferModel=v as string),
            F("CALCULATIONS","Calculations.TransferAssumptionSource","Transfer basis",c=>c.Library.Advanced.TransferBasis,p=>ReadCalc(p)==null?null:p.Calculations.TransferAssumptionSource,(p,v)=>Calc(p).TransferAssumptionSource=v as string),
            F("CALCULATIONS","Calculations.HangerAttachmentOffsetXMm","Hanger attachment ΔX (mm)",c=>c.Library.Advanced.HangerAttachmentOffsetXMm,p=>ReadCalc(p)==null?null:p.Calculations.HangerAttachmentOffsetXMm,(p,v)=>Calc(p).HangerAttachmentOffsetXMm=D(v)),
            F("CALCULATIONS","Calculations.HangerAttachmentOffsetYMm","Hanger attachment ΔY (mm)",c=>c.Library.Advanced.HangerAttachmentOffsetYMm,p=>ReadCalc(p)==null?null:p.Calculations.HangerAttachmentOffsetYMm,(p,v)=>Calc(p).HangerAttachmentOffsetYMm=D(v)),
            F("CALCULATIONS","Calculations.HangerAttachmentSource","Hanger attachment basis",c=>c.Library.Advanced.HangerAttachmentBasis,p=>ReadCalc(p)==null?null:p.Calculations.HangerAttachmentSource,(p,v)=>Calc(p).HangerAttachmentSource=v as string),
            F("CALCULATIONS","Calculations.CarGuideForces.GuideShoeSpacingMm","Guide-shoe spacing h (mm)",c=>c.Library.GuideShoeSpacingMm,p=>ReadCalc(p)==null||p.Calculations.CarGuideForces==null?null:p.Calculations.CarGuideForces.GuideShoeSpacingMm,(p,v)=>Guides(p).GuideShoeSpacingMm=D(v)),
            F("CALCULATIONS","Calculations.SafetyPit.SafetyGearType","Car safety gear",c=>{var v=V(c);return v.HasValue?(v.Value>c.Library.SafetyGear.ProgressiveAboveSpeedMps.Value?Progressive:Instantaneous):null;},p=>PitValue(p,s=>s.SafetyGearType),(p,v)=>Pit(p).SafetyGearType=v as string),
            F("CALCULATIONS","Calculations.SafetyPit.BufferType","Buffer type",c=>{var v=V(c);return v.HasValue?(v.Value<=c.Library.Buffers.EnergyAccumulationUpToSpeedMps.Value+1e-9?EnergyAccumulation:EnergyDissipation):null;},p=>PitValue(p,s=>s.BufferType),(p,v)=>Pit(p).BufferType=v as string),
            F("CALCULATIONS","Calculations.SafetyPit.CarGuideCount","Car guide rails",c=>c.Library.SafetyGear.CarGuideCount,p=>PitValue(p,s=>s.CarGuideCount),(p,v)=>Pit(p).CarGuideCount=I(v)),
            F("CALCULATIONS","Calculations.SafetyPit.CarBufferCount","Car buffers",c=>c.Library.Buffers.CarCount,p=>PitValue(p,s=>s.CarBufferCount),(p,v)=>Pit(p).CarBufferCount=I(v)),
            F("CALCULATIONS","Calculations.SafetyPit.CounterweightBufferCount","Counterweight buffers",c=>c.Library.Buffers.CounterweightCount,p=>PitValue(p,s=>s.CounterweightBufferCount),(p,v)=>Pit(p).CounterweightBufferCount=I(v)),
            F("CALCULATIONS","Calculations.SafetyPit.CarBufferSharing","Car buffer sharing",c=>c.Library.Buffers.Sharing,p=>PitValue(p,s=>s.CarBufferSharing),(p,v)=>Pit(p).CarBufferSharing=v as string),
            F("CALCULATIONS","Calculations.SafetyPit.CounterweightBufferSharing","CW buffer sharing",c=>c.Library.Buffers.Sharing,p=>PitValue(p,s=>s.CounterweightBufferSharing),(p,v)=>Pit(p).CounterweightBufferSharing=v as string),
        };
        // CW = P + q·Q with the selected q (balance factor step) while the CW is unlocked, else the library q.
        static object CounterweightPreset(PresetContext c)
        {
            var calc=ReadCalc(c.Project);var rt=calc==null?null:calc.RopeTraction;double? p=calc==null?null:calc.CarMassKg,q=Q(c);
            if(rt!=null&&rt.SelectedBalanceFactor.HasValue)return p.HasValue&&q.HasValue?p.Value+rt.SelectedBalanceFactor.Value*q.Value:(double?)null;
            return c.Library.CounterweightFor(p,q);
        }
        static object PitValue(ElevatorProject p,Func<SafetyPitInputs,object> get){var c=ReadCalc(p);return c==null||c.SafetyPit==null?null:get(c.SafetyPit);}
        public static PresetField Find(string key){return Fields.FirstOrDefault(f=>f.Key==key)??PresetFields.Find(key);}
        public static bool Follows(ElevatorProject p){return p!=null&&p.FollowsPresets==true;}

        // An older project is converted on its first unlock: every materialized field is locked first (keeping its value),
        // then the requested keys are unlocked.
        public static void Unlock(ElevatorProject p,IEnumerable<string> keys)
        {
            if(p.UserEditedFields==null)p.UserEditedFields=new List<string>();
            if(p.FollowsPresets!=true){p.FollowsPresets=true;foreach(var f in Fields)if(!p.UserEditedFields.Contains(f.Key))p.UserEditedFields.Add(f.Key);}
            foreach(var k in keys)p.UserEditedFields.Remove(k);
            if(p.UserEditedFields.Count==0)p.UserEditedFields=null;
        }
        public static void Lock(ElevatorProject p,string key)
        {
            if(p.UserEditedFields==null)p.UserEditedFields=new List<string>();
            if(!p.UserEditedFields.Contains(key))p.UserEditedFields.Add(key);
        }

        // New project from the presets: survey example shaft, stops, capacity and speed; everything else follows.
        public static ElevatorProject CreateNew(PresetLibrary lib,DesignContext ctx,CounterweightConfiguration counterweight,string bracketSource)
        {
            var p=new ElevatorProject{FollowsPresets=true};
            if(counterweight!=null)p.Counterweight=counterweight;
            if(bracketSource!=null)p.Rails.BracketSource=bracketSource;
            p.Suspension.RopingRatio=1;p.Calculations.Mode="Basic";
            if(lib!=null)NewFloors(p,lib);
            else{p.Building.Floors.Add(new FloorDefinition{Name="G"});p.Building.Floors.Add(new FloorDefinition{Name="1"});}
            p.Car.DisplayFloorId=p.Building.Floors[0].FloorId;
            Apply(p,lib,ctx);
            return p;
        }
        public static string FloorName(int index){return index==0?"G":index.ToString(System.Globalization.CultureInfo.InvariantCulture);}
        static void NewFloors(ElevatorProject p,PresetLibrary lib)
        {
            int stops=lib.NewProject.Stops.Value;
            for(int i=0;i<stops;i++)p.Building.Floors.Add(new FloorDefinition{Name=FloorName(i),HeightToNextMm=i<stops-1?lib.NewProject.FloorHeightMm:null});
        }
        public static double? NewFloorHeight(PresetLibrary lib){return lib==null||lib.NewProject==null?null:lib.NewProject.FloorHeightMm;}

        // Writes every unlocked materialized field's preset into the project, then sizes the ropes.
        public static RopeSizing Apply(ElevatorProject p,PresetLibrary lib,DesignContext ctx)
        {
            if(p==null||lib==null||!Follows(p))return null;
            var c=new PresetContext{Project=p,Library=lib};
            foreach(var f in Fields)if(!f.IsEdited(p))f.Store(p,f.Preset(c));
            return SizeRopes(p,lib,ctx);
        }
        public static bool AnyBlank(ElevatorProject p,string page)
        {
            if(p==null)return false;
            if((page==null||page=="FLOORS")&&FloorsBlank(p))return true;
            return Fields.Any(f=>(page==null||f.Page==page)&&BlankForFill(p,f));
        }
        // Blank Advanced-only values do not count on a Basic project (Basic does not use them).
        static bool BlankForFill(ElevatorProject p,PresetField f){return f.IsBlank(p)&&(f.Page!="CALCULATIONS"||Relevant(p,f.Key));}
        static bool Relevant(ElevatorProject p,string key)
        {
            if(CalculationModes.Resolve(p.Calculations)=="Advanced")return true;
            return new[]{"Calculations.CarMassKg","Calculations.MachineMassKg","Calculations.CounterweightMassKg","Calculations.IncludeChassisSelfWeight","Calculations.IncludeHangerAssemblyWeight","Calculations.CarGuideForces.GuideShoeSpacingMm"}.Contains(key)||key.StartsWith("Calculations.SafetyPit.");
        }
        static bool FloorsBlank(ElevatorProject p)
        {
            var floors=p.Building==null?null:p.Building.Floors;
            return floors==null||floors.Count<2||floors.Take(floors.Count-1).Any(f=>f!=null&&!f.HeightToNextMm.HasValue);
        }
        // "Fill blanks from presets" (one page, or every page when page is null). Non-blank values are kept and locked.
        public static RopeSizing FillBlanks(ElevatorProject p,PresetLibrary lib,DesignContext ctx,string page)
        {
            if(p==null||lib==null)return null;
            if(p.Calculations==null)p.Calculations=new ChassisCalculationInputs();
            if(p.Calculations.Mode==null)p.Calculations.Mode=CalculationModes.Resolve(p.Calculations);
            var blanks=Fields.Where(f=>(page==null||f.Page==page)&&BlankForFill(p,f)).Select(f=>f.Key).ToList();
            bool wasOld=!Follows(p);
            Unlock(p,blanks);
            if(wasOld)p.FollowsPresets=true;
            if(page==null||page=="FLOORS")
            {
                if(p.Building==null)p.Building=new BuildingConfiguration();
                if(p.Building.Floors==null)p.Building.Floors=new List<FloorDefinition>();
                var floors=p.Building.Floors;
                if(floors.Count==0)NewFloors(p,lib);
                else if(floors.Count==1){floors[0].HeightToNextMm=lib.NewProject.FloorHeightMm;floors.Add(new FloorDefinition{Name=UniqueName(floors)});}
                else for(int i=0;i<floors.Count-1;i++)if(floors[i]!=null&&!floors[i].HeightToNextMm.HasValue)floors[i].HeightToNextMm=lib.NewProject.FloorHeightMm;
                if(p.Car==null)p.Car=new CarConfiguration();
                if(p.Car.DisplayFloorId==null||!floors.Any(f=>f.FloorId==p.Car.DisplayFloorId))p.Car.DisplayFloorId=floors[0].FloorId;
            }
            return Apply(p,lib,ctx);
        }
        static string UniqueName(List<FloorDefinition> floors){for(int i=1;;i++){var n=FloorName(i);if(!floors.Any(f=>f.Name==n))return n;}}

        // ---------- rope, groove, balance and sheave selection ----------
        // Joint search over the unlocked levers, in order of preference (least change from the presets first):
        //   (a) the rope arrangement: n = 4 … MaximumRopeCount and d from the rope table with d ≤ Dt / 40 (DESIGN-FLOW 11a: the
        //       deflector D/d is not a constraint), starting at the ladder diameter: the largest d ≤ the ladder d first, then the
        //       smallest n; a larger d only if nothing passes;
        //   (b) the groove undercut β steps (UndercutAngleStepsDeg);
        //   (c) the balance factor q steps (BalanceFactorSteps): CW = P + q·Q while the CW is unlocked;
        //   (d) the traction sheave steps (TractionSheavePitchSteps).
        // The first combination whose checks are all OK (rope diameter and count, Sf, sheave D/d, every traction case) is taken.
        // Otherwise the ladder / preset values stay, the checks stay NOT OK and the outcome is NoPass with the limiting check named.
        // Locked values never change. No equation is involved: every trial is the rope / traction engine.
        static string cacheKey;static RopeSizing cacheValue;static DesignContext cacheContext;
        public static readonly string[] RopeChecks={"rope-diameter","rope-count","dd-sheave","safety-factor"};
        public static RopeSizing SizeRopes(ElevatorProject p,PresetLibrary lib,DesignContext ctx)
        {
            var countField=Find(RopeCountKey);var diameterField=Find(RopeDiameterKey);var sheaveField=Find(SheaveKey);var betaField=PresetFields.UndercutAngle;var cwField=Find(CounterweightKey);
            bool countFree=!countField.IsEdited(p),diameterFree=!diameterField.IsEdited(p),sheaveFree=!sheaveField.IsEdited(p);
            bool betaFree=!betaField.IsEdited(p)&&!PresetFields.GrooveType.IsEdited(p)&&GrooveTypes.NeedsUndercut(lib.Groove.Type)&&lib.UndercutAngleStepsDeg!=null&&lib.UndercutAngleStepsDeg.Count>0;
            bool qFree=!cwField.IsEdited(p)&&lib.BalanceFactorSteps!=null&&lib.BalanceFactorSteps.Count>0;
            // The search always starts from the presets: previous β / q selections are cleared first (a locked value keeps none).
            if(p.Calculations!=null&&p.Calculations.RopeTraction!=null){p.Calculations.RopeTraction.SelectedUndercutAngleDeg=null;p.Calculations.RopeTraction.SelectedBalanceFactor=null;}
            if(qFree)SetBalance(p,lib,lib.CounterweightBalanceFactor);
            // An emptied group is the same as no group (keeps the cache key stable).
            if(p.Calculations!=null&&p.Calculations.RopeTraction!=null&&typeof(RopeTractionInputs).GetFields().All(f=>f.GetValue(p.Calculations.RopeTraction)==null))p.Calculations.RopeTraction=null;
            var outcome=new RopeSizing{StartCount=p.Suspension.RopeCount,StartDiameterMm=p.Suspension.RopeDiameterMm,StartSheaveMm=p.Machine.TractionSheavePitchDiameterMm,StartUndercutAngleDeg=lib.Groove.UndercutAngleDeg,StartBalanceFactor=lib.CounterweightBalanceFactor};
            if(ctx==null||!(countFree||diameterFree||betaFree||sheaveFree||qFree)||!p.Suspension.RopeCount.HasValue||!p.Suspension.RopeDiameterMm.HasValue){outcome.Status="NotApplicable";return Finish(p,lib,outcome,betaFree,qFree);}
            // Canonical key (sorted JSON keys): the serializer's field order is not stable between calls.
            string key=CalculationFingerprint.Hash(new{Project=p,Library=lib.Hash(),countFree,diameterFree,sheaveFree,betaFree,qFree});
            if(key!=null&&key==cacheKey&&cacheValue!=null&&Object.ReferenceEquals(ctx,cacheContext))return Finish(p,lib,cacheValue.Copy(),betaFree,qFree);
            var engine=new RopeTractionCalculationEngine();var clock=System.Diagnostics.Stopwatch.StartNew();
            int n0=p.Suspension.RopeCount.Value;double d0=p.Suspension.RopeDiameterMm.Value;double? s0=p.Machine.TractionSheavePitchDiameterMm;
            var effectiveBeta=betaField.Effective(new PresetContext{Project=p,Library=lib});
            double? b0=effectiveBeta==null?(double?)null:Convert.ToDouble(effectiveBeta,System.Globalization.CultureInfo.InvariantCulture);
            var sheaves=sheaveFree&&s0.HasValue?new[]{s0.Value}.Concat(lib.TractionSheavePitchSteps.Where(v=>v>s0.Value+1e-9)).ToList():new List<double>{s0??0};
            var betas=betaFree?lib.UndercutAngleStepsDeg.Where(v=>!b0.HasValue||v>=b0.Value-1e-9).ToList():new List<double>();
            if(betas.Count==0)betas.Add(b0??0);
            var qs=qFree?new[]{lib.CounterweightBalanceFactor.Value}.Concat(lib.BalanceFactorSteps.Where(v=>Math.Abs(v-lib.CounterweightBalanceFactor.Value)>1e-12)).ToList():new List<double>{Double.NaN};
            var counts=countFree?Enumerable.Range(4,Math.Max(1,lib.MaximumRopeCount.Value-3)).ToList():new List<int>{n0};
            // Reference trial: the ladder values as they stand.
            var reference=engine.Calculate(p,ctx,lib);outcome.Trials++;
            if(!reference.HasResults||!reference.Checks.Any(k=>k.Id=="safety-factor")){outcome.Status="NotEvaluable";goto done;}
            if(!Passes(reference))outcome.LimitingCheck=FirstFailure(reference);
            var failures=new Dictionary<string,int>();
            // The shared geometry depends on the sheave and on the rope diameter (validation) only, not on n, β or q: computed
            // once per (Dt, d) and reused for every trial of that pair.
            var geometries=new Dictionary<string,RopeTractionGeometry>();
            // Diameters for a sheave: from the ladder diameter downwards (largest d ≤ ladder d first), then larger ones only
            // if nothing passes; all with d ≤ Dt / 40.
            Func<double,List<double>> diametersFor=dt=>{
                if(!diameterFree)return new List<double>{d0};
                double limit=(dt>0?dt:Double.MaxValue)/40;var table=lib.Ropes.Table.Select(r=>r.DiameterMm).Where(v=>v<=limit+1e-9).ToList();
                return table.Where(v=>v<=d0+1e-9).OrderByDescending(v=>v).Concat(table.Where(v=>v>d0+1e-9).OrderBy(v=>v)).ToList();};
            foreach(var dt in sheaves)
            {
                foreach(var q in qs)
                {
                    // Within one sheave and q: after the first β only the combinations that failed on traction alone are tried
                    // again: a larger β raises Nequiv(t) (EN 81-50 Table 2) and so the required Sf, and leaves D/d unchanged.
                    List<Tuple<double,int>> tractionOnly=null;
                    foreach(var beta in betas)
                    {
                        bool tractionFailed=false;bool firstBeta=tractionOnly==null;var next=new List<Tuple<double,int>>();
                        var combos=firstBeta?diametersFor(dt).SelectMany(dd=>counts.Select(nn=>Tuple.Create(dd,nn))).ToList():tractionOnly;
                        double skipDiameter=Double.NaN;
                        foreach(var combo in combos)
                        {
                            double d=combo.Item1;int n=combo.Item2;
                            if(d==skipDiameter)continue;
                            var trial=ProjectJson.Clone(p);trial.Suspension.RopeCount=n;trial.Suspension.RopeDiameterMm=d;if(s0.HasValue)trial.Machine.TractionSheavePitchDiameterMm=dt;
                            if(betaFree)SelectBeta(trial,beta);
                            if(qFree)SetBalance(trial,lib,q);
                            string gk=dt.ToString("R")+"|"+d.ToString("R");RopeTractionGeometry geo;
                            if(!geometries.TryGetValue(gk,out geo)){geo=RopeTractionCalculationEngine.Geometry(trial,ctx);geometries[gk]=geo;}
                            var r=engine.Calculate(trial,ctx,lib,geo);outcome.Trials++;
                            if(Passes(r)){outcome.Status="Selected";outcome.RopeCount=n;outcome.RopeDiameterMm=d;outcome.SheaveMm=s0.HasValue?dt:(double?)null;outcome.UndercutAngleDeg=betaFree?beta:b0;outcome.BalanceFactor=qFree?q:(double?)null;goto done;}
                            // A sheave the layout cannot use (invalid geometry or input) gives no result for any rope: next sheave.
                            if(r.Omissions.Any(o=>o.Kind=="INVALID_GEOMETRY"||o.Kind=="INVALID_INPUT"))goto nextSheave;
                            bool dOnly=r.Checks.Any(k=>(k.Id=="rope-diameter"||k.Id=="dd-sheave")&&k.Ok==false);
                            // Limiting check of the search: what fails in the candidates that meet the minimum diameter and sheave D/d.
                            if(!dOnly)foreach(var f in Failures(r)){int c;failures.TryGetValue(f,out c);failures[f]=c+1;}
                            if(r.Traction.Any(t=>!t.Ok)){tractionFailed=true;if(!r.Checks.Any(k=>k.Ok==false))next.Add(combo);}
                            // Checks that depend on d only (minimum diameter, sheave D/d) fail for every count: next d.
                            if(dOnly)skipDiameter=d;
                        }
                        tractionOnly=next;
                        // β is stepped only while a traction case is NOT OK; it cannot help the rope checks.
                        if(!tractionFailed||tractionOnly.Count==0)break;
                    }
                }
                nextSheave:;
            }
            outcome.Status="NoPass";
            if(failures.Count>0)outcome.LimitingCheck=failures.OrderByDescending(x=>x.Value).First().Key;
            done:
            outcome.Milliseconds=clock.ElapsedMilliseconds;
            cacheKey=key;cacheValue=outcome.Copy();cacheContext=ctx;
            return Finish(p,lib,outcome,betaFree,qFree);
        }
        public const string CounterweightKey="Calculations.CounterweightMassKg";
        static void SelectBeta(ElevatorProject p,double? beta)
        {
            if(p.Calculations==null)p.Calculations=new ChassisCalculationInputs();
            if(p.Calculations.RopeTraction==null){if(!beta.HasValue)return;p.Calculations.RopeTraction=new RopeTractionInputs();}
            p.Calculations.RopeTraction.SelectedUndercutAngleDeg=beta;
        }
        // q selection (unlocked CW only): CW = P + q·Q; the library q needs no stored selection.
        static void SetBalance(ElevatorProject p,PresetLibrary lib,double? q)
        {
            if(p.Calculations==null)p.Calculations=new ChassisCalculationInputs();
            bool preset=!q.HasValue||(lib.CounterweightBalanceFactor.HasValue&&Math.Abs(q.Value-lib.CounterweightBalanceFactor.Value)<1e-12);
            if(p.Calculations.RopeTraction==null&&!preset)p.Calculations.RopeTraction=new RopeTractionInputs();
            if(p.Calculations.RopeTraction!=null)p.Calculations.RopeTraction.SelectedBalanceFactor=preset?(double?)null:q;
            double? qq=preset?lib.CounterweightBalanceFactor:q;
            p.Calculations.CounterweightMassKg=p.Calculations.CarMassKg.HasValue&&p.Elevator.CapacityKg.HasValue&&qq.HasValue?p.Calculations.CarMassKg.Value+qq.Value*p.Elevator.CapacityKg.Value:(double?)null;
        }
        static RopeSizing Finish(ElevatorProject p,PresetLibrary lib,RopeSizing o,bool betaFree,bool qFree)
        {
            if(o.Status=="Selected"||o.Status=="Passed")
            {
                o.LimitingCheck=null;
                p.Suspension.RopeCount=o.RopeCount;p.Suspension.RopeDiameterMm=o.RopeDiameterMm;if(o.SheaveMm.HasValue)p.Machine.TractionSheavePitchDiameterMm=o.SheaveMm;
                // The preset β / q need no stored selection; a stepped value is kept as the selection.
                if(betaFree)SelectBeta(p,o.UndercutAngleDeg.HasValue&&o.StartUndercutAngleDeg.HasValue&&Math.Abs(o.UndercutAngleDeg.Value-o.StartUndercutAngleDeg.Value)>1e-9?o.UndercutAngleDeg:null);
                if(qFree)SetBalance(p,lib,o.BalanceFactor);
            }
            else
            {
                if(betaFree)SelectBeta(p,null);
                if(qFree)SetBalance(p,lib,null);
                o.RopeCount=p.Suspension.RopeCount;o.RopeDiameterMm=p.Suspension.RopeDiameterMm;o.SheaveMm=p.Machine.TractionSheavePitchDiameterMm;o.UndercutAngleDeg=o.StartUndercutAngleDeg;o.BalanceFactor=qFree?o.StartBalanceFactor:null;
            }
            return o;
        }
        // A combination passes only with complete results: every rope check present and OK, every traction case OK, no omission.
        public static bool Passes(RopeTractionResult r)
        {
            return r!=null&&r.Omissions.Count==0&&r.Checks.Any(k=>k.Id=="safety-factor")&&r.Checks.Any(k=>k.Id=="dd-sheave")&&r.Traction.Count>=2
                &&!r.Checks.Any(k=>k.Ok==false)&&!r.Traction.Any(t=>!t.Ok);
        }
        static IEnumerable<string> Failures(RopeTractionResult r)
        {
            foreach(var k in r.Checks.Where(x=>x.Ok==false))yield return k.Title;
            foreach(var t in r.Traction.Where(x=>!x.Ok))yield return "Traction, "+t.Title.Substring(0,1).ToLowerInvariant()+t.Title.Substring(1);
        }
        static string FirstFailure(RopeTractionResult r)
        {
            var c=r.Checks.FirstOrDefault(k=>k.Ok==false);if(c!=null)return c.Title;
            var t=r.Traction.FirstOrDefault(x=>!x.Ok);if(t!=null)return "Traction, "+t.Title.Substring(0,1).ToLowerInvariant()+t.Title.Substring(1);
            var o=r.Omissions.FirstOrDefault();return o==null?null:o.Item;
        }
        // Outcome for the Design Notes (no project change).
        public static RopeSizing Outcome(ElevatorProject p,PresetLibrary lib,DesignContext ctx)
        {
            if(p==null||lib==null||!Follows(p))return null;
            var copy=ProjectJson.Clone(p);
            var c=new PresetContext{Project=copy,Library=lib};
            foreach(var f in Fields)if(!f.IsEdited(copy))f.Store(copy,f.Preset(c));
            return SizeRopes(copy,lib,ctx);
        }
    }
    public sealed class RopeSizing
    {
        public string Status; // NotApplicable, NotEvaluable, Selected, NoPass
        public int? StartCount,RopeCount;public double? StartDiameterMm,RopeDiameterMm,StartSheaveMm,SheaveMm,StartUndercutAngleDeg,UndercutAngleDeg,StartBalanceFactor,BalanceFactor;public string LimitingCheck;public int Trials;public long Milliseconds;
        public RopeSizing Copy(){return (RopeSizing)MemberwiseClone();}
    }
}
