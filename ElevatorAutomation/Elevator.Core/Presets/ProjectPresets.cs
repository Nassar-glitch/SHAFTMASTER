using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Calculations;
using Elevator.Core.Car;
using Elevator.Core.Counterweight;
using Elevator.Core.GuideRails;
using Elevator.Core.Project;
using Elevator.Core.SafetyGear;
using Elevator.Core.Suspension;

namespace Elevator.Core.Presets
{
    /// <summary>
    /// DESIGN-FLOW stage 2 ("Flow foundation"): every remaining project input is a materialized preset field. While the project follows
    /// presets (FollowsPresets = true), each unlocked field holds its preset, which may follow an upstream driver (Q, P, v). Editing a
    /// field locks it; Reset unlocks it. Older projects (FollowsPresets null) are left unchanged until the user chooses "Fill blanks from
    /// presets". No calculation equation is involved here.
    /// </summary>
    public static class ProjectPresets
    {
        /// <summary>Key of the rope count field.</summary>
        public const string RopeCountKey = "Suspension.RopeCount";
        /// <summary>Key of the rope diameter field.</summary>
        public const string RopeDiameterKey = "Suspension.RopeDiameterMm";
        /// <summary>Key of the machine-on-rails field.</summary>
        public const string MachineOnRailsKey = "Rails.MachineOnRails";
        /// <summary>Key of the car rail profile field.</summary>
        public const string CarProfileKey = "Rails.CarProfile";
        /// <summary>Key of the car rail bracket spacing field.</summary>
        public const string CarSpacingKey = "Rails.CarSpacingMm";
        /// <summary>Key of the counterweight rail profile field.</summary>
        public const string CwProfileKey = "Rails.CwProfile";
        /// <summary>Key of the counterweight rail bracket spacing field.</summary>
        public const string CwSpacingKey = "Rails.CwSpacingMm";
        /// <summary>Key of the traction sheave field.</summary>
        public const string SheaveKey = "Machine.TractionSheavePitchDiameterMm";
        /// <summary>Safety-gear type: progressive.</summary>
        public const string Progressive = "Progressive";
        /// <summary>Safety-gear type: instantaneous.</summary>
        public const string Instantaneous = "Instantaneous";
        /// <summary>Buffer type: energy accumulation.</summary>
        public const string EnergyAccumulation = "EnergyAccumulation";
        /// <summary>Buffer type: energy dissipation.</summary>
        public const string EnergyDissipation = "EnergyDissipation";
        /// <summary>Buffer sharing: equal.</summary>
        public const string EqualSharing = "EqualSharing";
        public static readonly string[] Pages = { "GENERAL", "SHAFT", "FLOORS", "MACHINE", "SUSPENSION", "RAILS", "CALCULATIONS" };

        static ChassisCalculationInputs Calc(ElevatorProject p)
        {
            if (p.Calculations == null)
                p.Calculations = new ChassisCalculationInputs();
            return p.Calculations;
        }

        static SafetyPitInputs Pit(ElevatorProject p)
        {
            var c = Calc(p);
            if (c.SafetyPit == null)
                c.SafetyPit = new SafetyPitInputs();
            return c.SafetyPit;
        }

        static CarGuideForceInputs Guides(ElevatorProject p)
        {
            var c = Calc(p);
            if (c.CarGuideForces == null)
                c.CarGuideForces = new CarGuideForceInputs();
            return c.CarGuideForces;
        }

        static ChassisCalculationInputs ReadCalc(ElevatorProject p)
        {
            return p == null ? null : p.Calculations;
        }

        static double? D(object v)
        {
            return v == null ? (double?)null : Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        static int? I(object v)
        {
            return v == null ? (int?)null : Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        static bool? B(object v)
        {
            return v == null ? (bool?)null : Convert.ToBoolean(v);
        }

        static double? Q(PresetContext c)
        {
            return c.Project.Elevator == null ? null : c.Project.Elevator.CapacityKg;
        }

        static double? V(PresetContext c)
        {
            return c.Project.Elevator == null ? null : c.Project.Elevator.SpeedMetresPerSecond;
        }

        static bool Advanced(PresetContext c)
        {
            return CalculationModes.Resolve(c.Project.Calculations) == "Advanced";
        }

        static PresetField F(
            string page,
            string key,
            string label,
            Func<PresetContext, object> preset,
            Func<ElevatorProject, object> get,
            Action<ElevatorProject, object> set)
        {
            return new PresetField
            {
                Page = page,
                Key = key,
                Label = label,
                Materialized = true,
                Preset = preset,
                Stored = get,
                Store = set
            };
        }

        // Order matters: drivers (Q, v, P) come before the fields that follow them.
        public static readonly PresetField[] Fields =
        {
            F(
                "GENERAL",
                "Elevator.CapacityKg",
                "Capacity (kg)",
                c=>c.Library.NewProject.CapacityKg,
                p=>p.Elevator.CapacityKg,
                (p,v)=>p.Elevator.CapacityKg=D(v)),
            F(
                "GENERAL",
                "Elevator.SpeedMetresPerSecond",
                "Speed (m/s)",
                c=>c.Library.NewProject.SpeedMps,
                p=>p.Elevator.SpeedMetresPerSecond,
                (p,v)=>p.Elevator.SpeedMetresPerSecond=D(v)),
            F(
                "SHAFT",
                "Shaft.WidthMm",
                "Shaft width · SW (mm)",
                c=>c.Library.NewProject.ShaftWidthMm,
                p=>p.Shaft.WidthMm,
                (p,v)=>p.Shaft.WidthMm=D(v)??0),
            F(
                "SHAFT",
                "Shaft.DepthMm",
                "Shaft depth · SD (mm)",
                c=>c.Library.NewProject.ShaftDepthMm,
                p=>p.Shaft.DepthMm,
                (p,v)=>p.Shaft.DepthMm=D(v)??0),
            F(
                "SHAFT",
                "Shaft.PitDepthMm",
                "Pit depth (mm)",
                c=>c.Library.NewProject.PitDepthMm,
                p=>p.Shaft.PitDepthMm,
                (p,v)=>p.Shaft.PitDepthMm=D(v)),
            F(
                "SHAFT",
                "Shaft.OverheadMm",
                "Overhead (mm)",
                c=>c.Library.NewProject.OverheadMm,
                p=>p.Shaft.OverheadMm,
                (p,v)=>p.Shaft.OverheadMm=D(v)),
            F(
                "MACHINE",
                SheaveKey,
                "Traction sheave pitch diameter (mm)",
                c=>c.Library.TractionSheavePitchDiameterMm,
                p=>p.Machine.TractionSheavePitchDiameterMm,
                (p,v)=>p.Machine.TractionSheavePitchDiameterMm=D(v)),
            F(
                "SUSPENSION",
                RopeDiameterKey,
                "Rope diameter (mm)",
                c=>{
var r=c.Library.RopesFor(Q(c));
return r==null?(double?)null:r.DiameterMm;
},
                p=>p.Suspension.RopeDiameterMm,
                (p,v)=>p.Suspension.RopeDiameterMm=D(v)),
            F(
                "SUSPENSION",
                RopeCountKey,
                "Number of ropes",
                c=>{
var r=c.Library.RopesFor(Q(c));
return r==null?(int?)null:r.RopeCount;
},
                p=>p.Suspension.RopeCount,
                (p,v)=>p.Suspension.RopeCount=I(v)),
            F(
                "RAILS",
                "Rails.BracketMaximumSpacingMm",
                "Maximum bracket spacing (mm)",
                c=>BracketSpacing(c),
                p=>p.Rails.BracketMaximumSpacingMm,
                (p,v)=>p.Rails.BracketMaximumSpacingMm=D(v)),
            F(
                "CALCULATIONS",
                "Calculations.CarMassKg",
                "Empty car mass P (kg)",
                c=>{
var m=c.Library.MassesFor(Q(c));
return m==null?(double?)null:m.CarMassKg;
},
                p=>ReadCalc(p)==null?null:p.Calculations.CarMassKg,
                (p,v)=>Calc(p).CarMassKg=D(v)),
            F(
                "CALCULATIONS",
                "Calculations.MachineMassKg",
                "Machine mass (kg)",
                c=>{
var m=c.Library.MassesFor(Q(c));
return m==null?(double?)null:m.MachineMassKg;
},
                p=>ReadCalc(p)==null?null:p.Calculations.MachineMassKg,
                (p,v)=>Calc(p).MachineMassKg=D(v)),
            F(
                "CALCULATIONS",
                "Calculations.CounterweightMassKg",
                "Counterweight mass (kg)",
                c=>CounterweightPreset(c),
                p=>ReadCalc(p)==null?null:p.Calculations.CounterweightMassKg,
                (p,v)=>Calc(p).CounterweightMassKg=D(v)),
            F(
                "CALCULATIONS",
                "Calculations.CarSideAdditionalSuspendedMassKg",
                "Car-side additional mass (kg)",
                c=>Advanced(c)?c.Library.Advanced.AdditionalMassKg:null,
                p=>ReadCalc(p)==null?null:p.Calculations.CarSideAdditionalSuspendedMassKg,
                (p,v)=>Calc(p).CarSideAdditionalSuspendedMassKg=D(v)),
            F(
                "CALCULATIONS",
                "Calculations.CounterweightSideAdditionalSuspendedMassKg",
                "CW-side additional mass (kg)",
                c=>Advanced(c)?c.Library.Advanced.AdditionalMassKg:null,
                p=>ReadCalc(p)==null?null:p.Calculations.CounterweightSideAdditionalSuspendedMassKg,
                (p,v)=>Calc(p).CounterweightSideAdditionalSuspendedMassKg=D(v)),
            F(
                "CALCULATIONS",
                "Calculations.IncludeChassisSelfWeight",
                "Chassis self-weight",
                c=>false,
                p=>ReadCalc(p)==null?null:p.Calculations.IncludeChassisSelfWeight,
                (p,v)=>Calc(p).IncludeChassisSelfWeight=B(v)),
            F(
                "CALCULATIONS",
                "Calculations.IncludeHangerAssemblyWeight",
                "Hanger assembly weight",
                c=>false,
                p=>ReadCalc(p)==null?null:p.Calculations.IncludeHangerAssemblyWeight,
                (p,v)=>Calc(p).IncludeHangerAssemblyWeight=B(v)),
            F(
                "CALCULATIONS",
                "Calculations.ChassisWeightSourceOrExclusionReason",
                "Chassis weight basis",
                c=>c.Library.Advanced.ExcludedWeightBasis,
                p=>ReadCalc(p)==null?null:p.Calculations.ChassisWeightSourceOrExclusionReason,
                (p,v)=>Calc(p).ChassisWeightSourceOrExclusionReason=v as string),
            F(
                "CALCULATIONS",
                "Calculations.HangerWeightSourceOrExclusionReason",
                "Hanger weight basis",
                c=>c.Library.Advanced.ExcludedWeightBasis,
                p=>ReadCalc(p)==null?null:p.Calculations.HangerWeightSourceOrExclusionReason,
                (p,v)=>Calc(p).HangerWeightSourceOrExclusionReason=v as string),
            F(
                "CALCULATIONS",
                "Calculations.LoadCase",
                "Load case",
                c=>c.Library.Advanced.LoadCase,
                p=>ReadCalc(p)==null?null:p.Calculations.LoadCase,
                (p,v)=>Calc(p).LoadCase=v as string),
            F(
                "CALCULATIONS",
                "Calculations.MachineCgOffsetXMm",
                "Machine CG ΔX (mm)",
                c=>c.Library.Advanced.MachineCgOffsetXMm,
                p=>ReadCalc(p)==null?null:p.Calculations.MachineCgOffsetXMm,
                (p,v)=>Calc(p).MachineCgOffsetXMm=D(v)),
            F(
                "CALCULATIONS",
                "Calculations.MachineCgOffsetYMm",
                "Machine CG ΔY (mm)",
                c=>c.Library.Advanced.MachineCgOffsetYMm,
                p=>ReadCalc(p)==null?null:p.Calculations.MachineCgOffsetYMm,
                (p,v)=>Calc(p).MachineCgOffsetYMm=D(v)),
            F(
                "CALCULATIONS",
                "Calculations.MachineCgOffsetZMm",
                "Machine CG ΔZ (mm)",
                c=>c.Library.Advanced.MachineCgOffsetZMm,
                p=>ReadCalc(p)==null?null:p.Calculations.MachineCgOffsetZMm,
                (p,v)=>Calc(p).MachineCgOffsetZMm=D(v)),
            F(
                "CALCULATIONS",
                "Calculations.MachineCgSource",
                "Machine CG basis",
                c=>c.Library.Advanced.MachineCgBasis,
                p=>ReadCalc(p)==null?null:p.Calculations.MachineCgSource,
                (p,v)=>Calc(p).MachineCgSource=v as string),
            F(
                "CALCULATIONS",
                "Calculations.TransferModel",
                "Transfer model",
                c=>c.Library.Advanced.TransferModel,
                p=>ReadCalc(p)==null?null:p.Calculations.TransferModel,
                (p,v)=>Calc(p).TransferModel=v as string),
            F(
                "CALCULATIONS",
                "Calculations.TransferAssumptionSource",
                "Transfer basis",
                c=>c.Library.Advanced.TransferBasis,
                p=>ReadCalc(p)==null?null:p.Calculations.TransferAssumptionSource,
                (p,v)=>Calc(p).TransferAssumptionSource=v as string),
            F(
                "CALCULATIONS",
                "Calculations.HangerAttachmentOffsetXMm",
                "Hanger attachment ΔX (mm)",
                c=>c.Library.Advanced.HangerAttachmentOffsetXMm,
                p=>ReadCalc(p)==null?null:p.Calculations.HangerAttachmentOffsetXMm,
                (p,v)=>Calc(p).HangerAttachmentOffsetXMm=D(v)),
            F(
                "CALCULATIONS",
                "Calculations.HangerAttachmentOffsetYMm",
                "Hanger attachment ΔY (mm)",
                c=>c.Library.Advanced.HangerAttachmentOffsetYMm,
                p=>ReadCalc(p)==null?null:p.Calculations.HangerAttachmentOffsetYMm,
                (p,v)=>Calc(p).HangerAttachmentOffsetYMm=D(v)),
            F(
                "CALCULATIONS",
                "Calculations.HangerAttachmentSource",
                "Hanger attachment basis",
                c=>c.Library.Advanced.HangerAttachmentBasis,
                p=>ReadCalc(p)==null?null:p.Calculations.HangerAttachmentSource,
                (p,v)=>Calc(p).HangerAttachmentSource=v as string),
            F(
                "CALCULATIONS",
                "Calculations.CarGuideForces.GuideShoeSpacingMm",
                "Guide-shoe spacing h (mm)",
                c=>c.Library.GuideShoeSpacingMm,
                p=>ReadCalc(p)==null||p.Calculations.CarGuideForces==null?null:p.Calculations.CarGuideForces.GuideShoeSpacingMm,
                (p,v)=>Guides(p).GuideShoeSpacingMm=D(v)),
            F("CALCULATIONS","Calculations.SafetyPit.SafetyGearType","Car safety gear",c=>{var v=V(c);return v.HasValue
                ?(v.Value>c.Library.SafetyGear.ProgressiveAboveSpeedMps.Value?Progressive:Instantaneous)
                :null;},p=>PitValue(p,s=>s.SafetyGearType),(p,v)=>Pit(p).SafetyGearType=v as string),
            F("CALCULATIONS","Calculations.SafetyPit.BufferType","Buffer type",c=>{var v=V(c);return v.HasValue
                ?(v.Value<=c.Library.Buffers.EnergyAccumulationUpToSpeedMps.Value+1e-9?EnergyAccumulation:EnergyDissipation)
                :null;},p=>PitValue(p,s=>s.BufferType),(p,v)=>Pit(p).BufferType=v as string),
            F(
                "CALCULATIONS",
                "Calculations.SafetyPit.CarGuideCount",
                "Car guide rails",
                c=>c.Library.SafetyGear.CarGuideCount,
                p=>PitValue(p,s=>s.CarGuideCount),
                (p,v)=>Pit(p).CarGuideCount=I(v)),
            F(
                "CALCULATIONS",
                "Calculations.SafetyPit.CarBufferCount",
                "Car buffers",
                c=>c.Library.Buffers.CarCount,
                p=>PitValue(p,s=>s.CarBufferCount),
                (p,v)=>Pit(p).CarBufferCount=I(v)),
            F(
                "CALCULATIONS",
                "Calculations.SafetyPit.CounterweightBufferCount",
                "Counterweight buffers",
                c=>c.Library.Buffers.CounterweightCount,
                p=>PitValue(p,s=>s.CounterweightBufferCount),
                (p,v)=>Pit(p).CounterweightBufferCount=I(v)),
            F(
                "CALCULATIONS",
                "Calculations.SafetyPit.CarBufferSharing",
                "Car buffer sharing",
                c=>c.Library.Buffers.Sharing,
                p=>PitValue(p,s=>s.CarBufferSharing),
                (p,v)=>Pit(p).CarBufferSharing=v as string),
            F(
                "CALCULATIONS",
                "Calculations.SafetyPit.CounterweightBufferSharing",
                "CW buffer sharing",
                c=>c.Library.Buffers.Sharing,
                p=>PitValue(p,s=>s.CounterweightBufferSharing),
                (p,v)=>Pit(p).CounterweightBufferSharing=v as string),
            // Guide rails (DESIGN-FLOW "Guide rails"): in the order of the rails page, each recommended from the earlier ones.
            F(
                "RAILS",
                MachineOnRailsKey,
                "Machine supported on the guide rails",
                c=>c.Library.GuideRailsOrDefault().MachineOnRails,
                p=>p.Rails.MachineOnRails,
                (p,v)=>p.Rails.MachineOnRails=B(v)),
            F(
                "RAILS",
                CarProfileKey,
                "Car rail profile",
                c=>RailRecommendation(c,CarProfileKey),
                p=>p.Rails.CarProfile,
                (p,v)=>p.Rails.CarProfile=v as string),
            F(
                "RAILS",
                CarSpacingKey,
                "Car rail bracket spacing (mm)",
                c=>RailRecommendation(c,CarSpacingKey),
                p=>p.Rails.CarSpacingMm,
                (p,v)=>p.Rails.CarSpacingMm=D(v)),
            F(
                "RAILS",
                CwProfileKey,
                "Counterweight rail profile",
                c=>RailRecommendation(c,CwProfileKey),
                p=>p.Rails.CwProfile,
                (p,v)=>p.Rails.CwProfile=v as string),
            F(
                "RAILS",
                CwSpacingKey,
                "Counterweight rail bracket spacing (mm)",
                c=>Fields.First(f=>f.Key==CarSpacingKey).Effective(c), // the counterweight spacing follows the car spacing until typed
                p=>p.Rails.CwSpacingMm,
                (p,v)=>p.Rails.CwSpacingMm=D(v)),
            // Advanced guide-rail values: the library value, or the catalog value of the chosen profile, until edited.
            F(
                "RAILS",
                "Rails.MachineRailCount",
                "Number of rails carrying the machine",
                c=>c.Library.GuideRailsOrDefault().MachineRailCount,
                p=>p.Rails.MachineRailCount,
                (p,v)=>p.Rails.MachineRailCount=I(v)),
            F(
                "RAILS",
                "Rails.ClipPushThroughN",
                "Push-through force of the clips of one bracket (N)",
                c=>c.Library.GuideRailsOrDefault().ClipPushThroughN,
                p=>p.Rails.ClipPushThroughN,
                (p,v)=>p.Rails.ClipPushThroughN=D(v)),
            F(
                "RAILS",
                "Rails.CwShoeSpacingMm",
                "Counterweight guide-shoe spacing (mm)",
                c=>c.Library.GuideRailsOrDefault().CwShoeSpacingMm,
                p=>p.Rails.CwShoeSpacingMm,
                (p,v)=>p.Rails.CwShoeSpacingMm=D(v)),
            F(
                "RAILS",
                "Rails.StructureDeflectionMm",
                "Structure deflection at the brackets (mm)",
                c=>c.Library.GuideRailsOrDefault().StructureDeflectionXMm,
                p=>p.Rails.StructureDeflectionMm,
                (p,v)=>p.Rails.StructureDeflectionMm=D(v)),
            F(
                "RAILS",
                "Rails.CarShoeType",
                "Car guide-shoe type",
                c=>CatalogOf(c,CarProfileKey,r=>r.ShoeType),
                p=>p.Rails.CarShoeType,
                (p,v)=>p.Rails.CarShoeType=v as string),
            F(
                "RAILS",
                "Rails.CarShoeLiningMm",
                "Car guide-shoe lining length (mm)",
                c=>CatalogOf(c,CarProfileKey,r=>(object)r.ShoeLiningLengthMm),
                p=>p.Rails.CarShoeLiningMm,
                (p,v)=>p.Rails.CarShoeLiningMm=D(v)),
            F(
                "RAILS",
                "Rails.CwShoeType",
                "Counterweight guide-shoe type",
                c=>CatalogOf(c,CwProfileKey,r=>r.ShoeType),
                p=>p.Rails.CwShoeType,
                (p,v)=>p.Rails.CwShoeType=v as string),
            F(
                "RAILS",
                "Rails.CwShoeLiningMm",
                "Counterweight guide-shoe lining length (mm)",
                c=>CatalogOf(c,CwProfileKey,r=>(object)r.ShoeLiningLengthMm),
                p=>p.Rails.CwShoeLiningMm,
                (p,v)=>p.Rails.CwShoeLiningMm=D(v)),
            F(
                "RAILS",
                "Rails.A5Band",
                "Rail steel elongation A5",
                c=>CatalogOf(c,CarProfileKey,r=>r.A5Band),
                p=>p.Rails.A5Band,
                (p,v)=>p.Rails.A5Band=v as string),
        };

        // The maximum bracket spacing of the drawings follows the effective car rail spacing; the library value when that is not known.
        static double? BracketSpacing(PresetContext c)
        {
            var v = Fields.First(f=>f.Key==CarSpacingKey).Effective(c);
            return v == null ? c.Library.BracketMaximumSpacingMm : D(v);
        }

        // A catalog value of the profile in effect on a line (the preset of the advanced shoe and A5 values); null without a design context.
        static object CatalogOf(PresetContext c, string profileKey, Func<RailProfile, object> value)
        {
            var name = Fields.First(f=>f.Key==profileKey).Effective(c) as string;
            var profile = name == null ? null : c.Library.GuideRailsOrDefault().Profile(name);
            return profile == null ? null : value(profile);
        }

        // The guide-rail recommendation of a field. Without a design context (a caller that cannot give one) the stored value is kept.
        static object RailRecommendation(PresetContext c, string key)
        {
            if (key == CarSpacingKey)
            {
                // A steel shaft: the bracket spacing is derived from the beam levels (the largest gap), not recommended.
                var derived = Steel.SteelStructure.DerivedSpacing(c);
                if (derived.HasValue)
                    return derived.Value;
            }
            if (c.Design == null)
            {
                return Fields.First(f=>f.Key==key).Stored(c.Project);
            }
            return GuideRailRecommendation.ForField(c, key);
        }

        // CW = P + q·Q with the selected q (balance factor step) while the CW is unlocked, else the library q.
        static object CounterweightPreset(PresetContext c)
        {
            var calc = ReadCalc(c.Project);
            var rt = calc == null ? null : calc.RopeTraction;
            double? p = calc == null ? null : calc.CarMassKg, q = Q(c);
            if (rt != null && rt.SelectedBalanceFactor.HasValue)
                return p.HasValue && q.HasValue ? p.Value + rt.SelectedBalanceFactor.Value * q.Value : (double?)null;
            return c.Library.CounterweightFor(p, q);
        }

        static object PitValue(ElevatorProject p, Func<SafetyPitInputs, object> get)
        {
            var c = ReadCalc(p);
            return c == null || c.SafetyPit == null ? null : get(c.SafetyPit);
        }

        /// <summary>The preset field with a key, or null.</summary>
        public static PresetField Find(string key)
        {
            return Fields.FirstOrDefault(f => f.Key == key) ?? PresetFields.Find(key) ?? Layouts.TitleBlockFields.Find(key) ?? Steel.SteelFields.Find(key);
        }

        /// <summary>True when the project follows the presets.</summary>
        public static bool Follows(ElevatorProject p)
        {
            return p != null && p.FollowsPresets == true;
        }

        /// <summary>
        /// An older project is converted on its first unlock: every materialized field is locked first (keeping its value), then the
        /// requested keys are unlocked.
        /// </summary>
        public static void Unlock(ElevatorProject p, IEnumerable<string> keys)
        {
            if (p.UserEditedFields == null)
                p.UserEditedFields = new List<string>();
            if (p.FollowsPresets != true)
            {
                p.FollowsPresets = true;
                foreach (var f in Fields)
                {
                    if (!p.UserEditedFields.Contains(f.Key))
                        p.UserEditedFields.Add(f.Key);
                }
            }
            foreach (var k in keys)
                p.UserEditedFields.Remove(k);
            if (p.UserEditedFields.Count == 0)
                p.UserEditedFields = null;
        }

        /// <summary>Locks a field at its current value.</summary>
        public static void Lock(ElevatorProject p, string key)
        {
            if (p.UserEditedFields == null)
                p.UserEditedFields = new List<string>();
            if (!p.UserEditedFields.Contains(key))
                p.UserEditedFields.Add(key);
        }

        /// <summary>New project from the presets: survey example shaft, stops, capacity and speed; everything else follows.</summary>
        public static ElevatorProject CreateNew(
            PresetLibrary lib,
            DesignContext ctx,
            CounterweightConfiguration counterweight,
            string bracketSource)
        {
            var p = new ElevatorProject { FollowsPresets = true };
            if (counterweight != null)
                p.Counterweight = counterweight;
            if (bracketSource != null)
                p.Rails.BracketSource = bracketSource;
            p.Suspension.RopingRatio = 1;
            p.Calculations.Mode = "Basic";
            if (lib != null)
                NewFloors(p, lib);
            else
            {
                p.Building.Floors.Add(new FloorDefinition { Name = "G" });
                p.Building.Floors.Add(new FloorDefinition { Name = "1" });
            }
            p.Car.DisplayFloorId = p.Building.Floors[0].FloorId;
            Apply(p, lib, ctx);
            return p;
        }

        /// <summary>The name of the floor with an index.</summary>
        public static string FloorName(int index)
        {
            return index == 0 ? "G" : index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        static void NewFloors(ElevatorProject p, PresetLibrary lib)
        {
            int stops = lib.NewProject.Stops.Value;
            for (int i = 0; i < stops; i++)
            {
                p.Building.Floors.Add(
                                    new FloorDefinition
                                    {
                                        Name = FloorName(i),
                                        HeightToNextMm = i < stops - 1 ? lib.NewProject.FloorHeightMm : null
                                    });
            }
        }

        /// <summary>The height of a new floor from the library.</summary>
        public static double? NewFloorHeight(PresetLibrary lib)
        {
            return lib == null || lib.NewProject == null ? null : lib.NewProject.FloorHeightMm;
        }

        /// <summary>Writes every unlocked materialized field's preset into the project, then sizes the ropes.</summary>
        public static RopeSizing Apply(ElevatorProject p, PresetLibrary lib, DesignContext ctx)
        {
            if (p == null || lib == null || !Follows(p))
                return null;
            var c = new PresetContext { Project = p, Library = lib, Design = ctx };
            foreach (var f in Fields)
            {
                if (!f.IsEdited(p))
                    f.Store(p, f.Preset(c));
            }
            return SizeRopes(p, lib, ctx);
        }

        /// <summary>True when a page (null: every page) has blank preset fields.</summary>
        public static bool AnyBlank(ElevatorProject p, string page)
        {
            if (p == null)
                return false;
            if ((page == null || page == "FLOORS") && FloorsBlank(p))
                return true;
            return Fields.Any(f => (page == null || f.Page == page) && BlankForFill(p, f));
        }

        // Blank Advanced-only values do not count on a Basic project (Basic does not use them).
        static bool BlankForFill(ElevatorProject p, PresetField f)
        {
            return f.IsBlank(p) && (f.Page != "CALCULATIONS" || Relevant(p, f.Key));
        }

        static bool Relevant(ElevatorProject p, string key)
        {
            if (CalculationModes.Resolve(p.Calculations) == "Advanced")
                return true;
            return new[]
            {
                "Calculations.CarMassKg",
                "Calculations.MachineMassKg",
                "Calculations.CounterweightMassKg",
                "Calculations.IncludeChassisSelfWeight",
                "Calculations.IncludeHangerAssemblyWeight",
                "Calculations.CarGuideForces.GuideShoeSpacingMm"
            }.Contains(key) || key.StartsWith("Calculations.SafetyPit.");
        }

        static bool FloorsBlank(ElevatorProject p)
        {
            var floors = p.Building == null ? null : p.Building.Floors;
            return floors == null || floors.Count < 2 || floors.Take(floors.Count - 1).Any(f => f != null && !f.HeightToNextMm.HasValue);
        }

        /// <summary>"Fill blanks from presets" (one page, or every page when page is null). Non-blank values are kept and locked.</summary>
        public static RopeSizing FillBlanks(ElevatorProject p, PresetLibrary lib, DesignContext ctx, string page)
        {
            if (p == null || lib == null)
                return null;
            if (p.Calculations == null)
                p.Calculations = new ChassisCalculationInputs();
            if (p.Calculations.Mode == null)
                p.Calculations.Mode = CalculationModes.Resolve(p.Calculations);
            var blanks = Fields.Where(f => (page == null || f.Page == page) && BlankForFill(p, f)).Select(f => f.Key).ToList();
            bool wasOld = !Follows(p);
            Unlock(p, blanks);
            if (wasOld)
                p.FollowsPresets = true;
            if (page == null || page == "FLOORS")
            {
                if (p.Building == null)
                    p.Building = new BuildingConfiguration();
                if (p.Building.Floors == null)
                    p.Building.Floors = new List<FloorDefinition>();
                var floors = p.Building.Floors;
                if (floors.Count == 0)
                    NewFloors(p, lib);
                else if (floors.Count == 1)
                {
                    floors[0].HeightToNextMm = lib.NewProject.FloorHeightMm;
                    floors.Add(new FloorDefinition { Name = UniqueName(floors) });
                }
                else
                {
                    for (int i = 0; i < floors.Count - 1; i++)
                    {
                        if (floors[i] != null && !floors[i].HeightToNextMm.HasValue)
                            floors[i].HeightToNextMm = lib.NewProject.FloorHeightMm;
                    }
                }
                if (p.Car == null)
                    p.Car = new CarConfiguration();
                if (p.Car.DisplayFloorId == null || !floors.Any(f => f.FloorId == p.Car.DisplayFloorId))
                    p.Car.DisplayFloorId = floors[0].FloorId;
            }
            return Apply(p, lib, ctx);
        }

        static string UniqueName(List<FloorDefinition> floors)
        {
            for (int i = 1; ; i++)
            {
                var n = FloorName(i);
                if (!floors.Any(f => f.Name == n))
                    return n;
            }
        }

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
        // Selections already searched, by canonical project fingerprint; an entry is used only with the same context object.
        // Bounded and locked: the application, the golden fixtures and the parallel tests share it.
        const int CacheLimit = 256;
        static readonly object cacheGate = new object();
        static readonly Dictionary<string, KeyValuePair<DesignContext, RopeSizing>> cache = new Dictionary<string, KeyValuePair<DesignContext, RopeSizing>>();
        public static readonly string[] RopeChecks = { "rope-diameter", "rope-count", "dd-sheave", "safety-factor" };

        /// <summary>
        /// Selects the rope count and diameter, groove angle, balance factor and sheave that pass the rope and traction checks, for the
        /// fields that are not locked.
        /// </summary>
        public static RopeSizing SizeRopes(ElevatorProject p, PresetLibrary lib, DesignContext ctx)
        {
            var countField = Find(RopeCountKey);
            var diameterField = Find(RopeDiameterKey);
            var sheaveField = Find(SheaveKey);
            var betaField = PresetFields.UndercutAngle;
            var cwField = Find(CounterweightKey);
            bool countFree = !countField.IsEdited(p), diameterFree = !diameterField.IsEdited(p), sheaveFree = !sheaveField.IsEdited(p);
            bool betaFree = !betaField.IsEdited(p)
                && !PresetFields.GrooveType.IsEdited(p)
                && GrooveTypes.NeedsUndercut(lib.Groove.Type)
                && lib.UndercutAngleStepsDeg != null
                && lib.UndercutAngleStepsDeg.Count > 0;
            bool qFree = !cwField.IsEdited(p) && lib.BalanceFactorSteps != null && lib.BalanceFactorSteps.Count > 0;
            ResetSelections(p, lib, qFree);
            var outcome = StartOutcome(p, lib);
            if (ctx == null
                || !(countFree || diameterFree || betaFree || sheaveFree || qFree)
                || !p.Suspension.RopeCount.HasValue
                || !p.Suspension.RopeDiameterMm.HasValue)
            {
                outcome.Status = "NotApplicable";
                return Finish(p, lib, outcome, betaFree, qFree);
            }
            // Canonical key (sorted JSON keys): the serializer's field order is not stable between calls.
            string key = CalculationFingerprint.Hash(new
            {
                Project = p,
                Library = lib.Hash(),
                countFree,
                diameterFree,
                sheaveFree,
                betaFree,
                qFree
            });
            RopeSizing cached = null;
            lock (cacheGate)
            {
                KeyValuePair<DesignContext, RopeSizing> entry;
                if (key != null && cache.TryGetValue(key, out entry) && Object.ReferenceEquals(ctx, entry.Key))
                    cached = entry.Value.Copy();
            }
            if (cached != null)
                return Finish(p, lib, cached, betaFree, qFree);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int n0 = p.Suspension.RopeCount.Value;
            double? s0 = p.Machine.TractionSheavePitchDiameterMm;
            var effectiveBeta = betaField.Effective(new PresetContext { Project = p, Library = lib });
            double? b0 =
                effectiveBeta == null ? (double?)null : Convert.ToDouble(effectiveBeta, System.Globalization.CultureInfo.InvariantCulture);
            var search = new RopeSearch
            {
                Project = p,
                Library = lib,
                Context = ctx,
                Outcome = outcome,
                BetaFree = betaFree,
                QFree = qFree,
                DiameterFree = diameterFree,
                D0 = p.Suspension.RopeDiameterMm.Value,
                S0 = s0,
                B0 = b0
            };
            var sheaves = sheaveFree && s0.HasValue
                ? new[] { s0.Value }.Concat(lib.TractionSheavePitchSteps.Where(v => v > s0.Value + 1e-9)).ToList()
                : new List<double> { s0 ?? 0 };
            var betas = betaFree ? lib.UndercutAngleStepsDeg.Where(v => !b0.HasValue || v >= b0.Value - 1e-9).ToList() : new List<double>();
            if (betas.Count == 0)
                betas.Add(b0 ?? 0);
            var qs = qFree ? new[] { lib.CounterweightBalanceFactor.Value }.Concat(lib.BalanceFactorSteps.Where(
                v => Math.Abs(v - lib.CounterweightBalanceFactor.Value) > 1e-12)).ToList() : new List<double> { Double.NaN };
            var counts = countFree ? Enumerable.Range(4, Math.Max(1, lib.MaximumRopeCount.Value - 3)).ToList() : new List<int> { n0 };
            // Reference trial: the ladder values as they stand.
            var reference = search.Engine.Calculate(p, ctx, lib);
            outcome.Trials++;
            if (!reference.HasResults || !reference.Checks.Any(k => k.Id == "safety-factor"))
            {
                outcome.Status = "NotEvaluable";
            }
            else
            {
                if (!Passes(reference))
                    outcome.LimitingCheck = FirstFailure(reference);
                if (!SearchSheaves(search, sheaves, qs, betas, counts))
                {
                    outcome.Status = "NoPass";
                    if (search.Failures.Count > 0)
                        outcome.LimitingCheck = search.Failures.OrderByDescending(x => x.Value).First().Key;
                }
            }
            outcome.Milliseconds = clock.ElapsedMilliseconds;
            lock (cacheGate)
            {
                if (cache.Count >= CacheLimit)
                    cache.Clear();
                if (key != null)
                    cache[key] = new KeyValuePair<DesignContext, RopeSizing>(ctx, outcome.Copy());
            }
            return Finish(p, lib, outcome, betaFree, qFree);
        }

        // The search always starts from the presets: previous β / q selections are cleared first (a locked value keeps none).
        static void ResetSelections(ElevatorProject p, PresetLibrary lib, bool qFree)
        {
            if (p.Calculations != null && p.Calculations.RopeTraction != null)
            {
                p.Calculations.RopeTraction.SelectedUndercutAngleDeg = null;
                p.Calculations.RopeTraction.SelectedBalanceFactor = null;
            }
            if (qFree)
                SetBalance(p, lib, lib.CounterweightBalanceFactor);
            // An emptied group is the same as no group (keeps the cache key stable).
            if (p.Calculations != null
                && p.Calculations.RopeTraction != null
                && typeof(RopeTractionInputs).GetFields().All(f => f.GetValue(p.Calculations.RopeTraction) == null))
                p.Calculations.RopeTraction = null;
        }

        static RopeSizing StartOutcome(ElevatorProject p, PresetLibrary lib)
        {
            return new RopeSizing
            {
                StartCount = p.Suspension.RopeCount,
                StartDiameterMm = p.Suspension.RopeDiameterMm,
                StartSheaveMm = p.Machine.TractionSheavePitchDiameterMm,
                StartUndercutAngleDeg = lib.Groove.UndercutAngleDeg,
                StartBalanceFactor = lib.CounterweightBalanceFactor
            };
        }

        enum TrialStatus
        {
            Continue,
            Selected,
            NextSheave
        }

        // The fixed inputs and the running state of one rope search.
        sealed class RopeSearch
        {
            public ElevatorProject Project;
            public PresetLibrary Library;
            public DesignContext Context;
            public RopeSizing Outcome;
            public bool BetaFree;
            public bool QFree;
            public bool DiameterFree;
            public double D0;
            public double? S0;
            public double? B0;
            public readonly RopeTractionCalculationEngine Engine = new RopeTractionCalculationEngine();
            public readonly Dictionary<string, int> Failures = new Dictionary<string, int>();

            // The shared geometry depends on the sheave and on the rope diameter (validation) only, not on n, β or q: computed
            // once per (Dt, d) and reused for every trial of that pair.
            public readonly Dictionary<string, RopeTractionGeometry> Geometries = new Dictionary<string, RopeTractionGeometry>();

            // Diameters for a sheave: from the ladder diameter downwards (largest d ≤ ladder d first), then larger ones only
            // if nothing passes; all with d ≤ Dt / 40.
            public List<double> DiametersFor(double dt)
            {
                if (!DiameterFree)
                    return new List<double> { D0 };
                double limit = (dt > 0 ? dt : Double.MaxValue) / 40;
                var table = Library.Ropes.Table.Select(r => r.DiameterMm).Where(v => v <= limit + 1e-9).ToList();
                return table.Where(v => v <= D0 + 1e-9)
                    .OrderByDescending(v => v)
                    .Concat(table.Where(v => v > D0 + 1e-9).OrderBy(v => v))
                    .ToList();
            }
        }

        // True when a combination passed (the outcome then holds the selection).
        static bool SearchSheaves(RopeSearch s, List<double> sheaves, List<double> qs, List<double> betas, List<int> counts)
        {
            foreach (var dt in sheaves)
            {
                if (SearchSheave(s, dt, qs, betas, counts) == TrialStatus.Selected)
                    return true;
            }
            return false;
        }

        static TrialStatus SearchSheave(RopeSearch s, double dt, List<double> qs, List<double> betas, List<int> counts)
        {
            foreach (var q in qs)
            {
                var status = SearchBetas(s, dt, q, betas, counts);
                if (status != TrialStatus.Continue)
                    return status;
            }
            return TrialStatus.Continue;
        }

        static TrialStatus SearchBetas(RopeSearch s, double dt, double q, List<double> betas, List<int> counts)
        {
            // Within one sheave and q: after the first β only the combinations that failed on traction alone are tried
            // again: a larger β raises Nequiv(t) (EN 81-50 Table 2) and so the required Sf, and leaves D/d unchanged.
            List<Tuple<double, int>> tractionOnly = null;
            foreach (var beta in betas)
            {
                bool tractionFailed = false;
                bool firstBeta = tractionOnly == null;
                var next = new List<Tuple<double, int>>();
                var combos = firstBeta
                    ? s.DiametersFor(dt).SelectMany(dd => counts.Select(nn => Tuple.Create(dd, nn))).ToList()
                    : tractionOnly;
                double skipDiameter = Double.NaN;
                foreach (var combo in combos)
                {
                    if (combo.Item1 == skipDiameter)
                        continue;
                    var status = RunTrial(s, dt, q, beta, combo, next, ref tractionFailed, ref skipDiameter);
                    if (status != TrialStatus.Continue)
                        return status;
                }
                tractionOnly = next;
                // β is stepped only while a traction case is NOT OK; it cannot help the rope checks.
                if (!tractionFailed || tractionOnly.Count == 0)
                    break;
            }
            return TrialStatus.Continue;
        }

        static TrialStatus RunTrial(
            RopeSearch s,
            double dt,
            double q,
            double beta,
            Tuple<double, int> combo,
            List<Tuple<double, int>> next,
            ref bool tractionFailed,
            ref double skipDiameter)
        {
            double d = combo.Item1;
            int n = combo.Item2;
            var trial = ProjectJson.Clone(s.Project);
            trial.Suspension.RopeCount = n;
            trial.Suspension.RopeDiameterMm = d;
            if (s.S0.HasValue)
                trial.Machine.TractionSheavePitchDiameterMm = dt;
            if (s.BetaFree)
                SelectBeta(trial, beta);
            if (s.QFree)
                SetBalance(trial, s.Library, q);
            string gk = dt.ToString("R") + "|" + d.ToString("R");
            RopeTractionGeometry geo;
            if (!s.Geometries.TryGetValue(gk, out geo))
            {
                geo = RopeTractionCalculationEngine.Geometry(trial, s.Context);
                s.Geometries[gk] = geo;
            }
            var r = s.Engine.Calculate(trial, s.Context, s.Library, geo);
            s.Outcome.Trials++;
            if (Passes(r))
            {
                s.Outcome.Status = "Selected";
                s.Outcome.RopeCount = n;
                s.Outcome.RopeDiameterMm = d;
                s.Outcome.SheaveMm = s.S0.HasValue ? dt : (double?)null;
                s.Outcome.UndercutAngleDeg = s.BetaFree ? beta : s.B0;
                s.Outcome.BalanceFactor = s.QFree ? q : (double?)null;
                return TrialStatus.Selected;
            }
            // A sheave the layout cannot use (invalid geometry or input) gives no result for any rope: next sheave.
            if (r.Omissions.Any(o => o.Kind == "INVALID_GEOMETRY" || o.Kind == "INVALID_INPUT"))
                return TrialStatus.NextSheave;
            bool dOnly = r.Checks.Any(k => (k.Id == "rope-diameter" || k.Id == "dd-sheave") && k.Ok == false);
            // Limiting check of the search: what fails in the candidates that meet the minimum diameter and sheave D/d.
            if (!dOnly)
            {
                foreach (var f in Failures(r))
                {
                    int c;
                    s.Failures.TryGetValue(f, out c);
                    s.Failures[f] = c + 1;
                }
            }
            if (r.Traction.Any(t => !t.Ok))
            {
                tractionFailed = true;
                if (!r.Checks.Any(k => k.Ok == false))
                    next.Add(combo);
            }
            // Checks that depend on d only (minimum diameter, sheave D/d) fail for every count: next d.
            if (dOnly)
                skipDiameter = d;
            return TrialStatus.Continue;
        }

        /// <summary>Key of the counterweight mass field.</summary>
        public const string CounterweightKey = "Calculations.CounterweightMassKg";

        static void SelectBeta(ElevatorProject p, double? beta)
        {
            if (p.Calculations == null)
                p.Calculations = new ChassisCalculationInputs();
            if (p.Calculations.RopeTraction == null)
            {
                if (!beta.HasValue)
                    return;
                p.Calculations.RopeTraction = new RopeTractionInputs();
            }
            p.Calculations.RopeTraction.SelectedUndercutAngleDeg = beta;
        }

        // q selection (unlocked CW only): CW = P + q·Q; the library q needs no stored selection.
        static void SetBalance(ElevatorProject p, PresetLibrary lib, double? q)
        {
            if (p.Calculations == null)
                p.Calculations = new ChassisCalculationInputs();
            bool preset = !q.HasValue
                || (lib.CounterweightBalanceFactor.HasValue && Math.Abs(q.Value - lib.CounterweightBalanceFactor.Value) < 1e-12);
            if (p.Calculations.RopeTraction == null && !preset)
                p.Calculations.RopeTraction = new RopeTractionInputs();
            if (p.Calculations.RopeTraction != null)
                p.Calculations.RopeTraction.SelectedBalanceFactor = preset ? (double?)null : q;
            double? qq = preset ? lib.CounterweightBalanceFactor : q;
            p.Calculations.CounterweightMassKg = p.Calculations.CarMassKg.HasValue && p.Elevator.CapacityKg.HasValue && qq.HasValue
                ? p.Calculations.CarMassKg.Value + qq.Value * p.Elevator.CapacityKg.Value
                : (double?)null;
        }

        static RopeSizing Finish(ElevatorProject p, PresetLibrary lib, RopeSizing o, bool betaFree, bool qFree)
        {
            if (o.Status == "Selected" || o.Status == "Passed")
            {
                o.LimitingCheck = null;
                p.Suspension.RopeCount = o.RopeCount;
                p.Suspension.RopeDiameterMm = o.RopeDiameterMm;
                if (o.SheaveMm.HasValue)
                    p.Machine.TractionSheavePitchDiameterMm = o.SheaveMm;
                // The preset β / q need no stored selection; a stepped value is kept as the selection.
                if (betaFree)
                {
                    SelectBeta(p, o.UndercutAngleDeg.HasValue
                                            && o.StartUndercutAngleDeg.HasValue
                                            && Math.Abs(o.UndercutAngleDeg.Value - o.StartUndercutAngleDeg.Value) > 1e-9
                                                ? o.UndercutAngleDeg
                                                : null);
                }
                if (qFree)
                    SetBalance(p, lib, o.BalanceFactor);
            }
            else
            {
                if (betaFree)
                    SelectBeta(p, null);
                if (qFree)
                    SetBalance(p, lib, null);
                o.RopeCount = p.Suspension.RopeCount;
                o.RopeDiameterMm = p.Suspension.RopeDiameterMm;
                o.SheaveMm = p.Machine.TractionSheavePitchDiameterMm;
                o.UndercutAngleDeg = o.StartUndercutAngleDeg;
                o.BalanceFactor = qFree ? o.StartBalanceFactor : null;
            }
            return o;
        }

        /// <summary>
        /// A combination passes only with complete results: every rope check present and OK, every traction case OK, no omission.
        /// </summary>
        public static bool Passes(RopeTractionResult r)
        {
            return r != null
                && r.Omissions.Count == 0
                && r.Checks.Any(k => k.Id == "safety-factor")
                && r.Checks.Any(k => k.Id == "dd-sheave")
                && r.Traction.Count >= 2
                && !r.Checks.Any(k => k.Ok == false) && !r.Traction.Any(t => !t.Ok);
        }

        static IEnumerable<string> Failures(RopeTractionResult r)
        {
            foreach (var k in r.Checks.Where(x => x.Ok == false))
                yield return k.Title;
            foreach (var t in r.Traction.Where(x => !x.Ok))
                yield return "Traction, " + t.Title.Substring(0, 1).ToLowerInvariant() + t.Title.Substring(1);
        }

        static string FirstFailure(RopeTractionResult r)
        {
            var c = r.Checks.FirstOrDefault(k => k.Ok == false);
            if (c != null)
                return c.Title;
            var t = r.Traction.FirstOrDefault(x => !x.Ok);
            if (t != null)
                return "Traction, " + t.Title.Substring(0, 1).ToLowerInvariant() + t.Title.Substring(1);
            var o = r.Omissions.FirstOrDefault();
            return o == null ? null : o.Item;
        }

        /// <summary>Outcome for the Design Notes (no project change).</summary>
        public static RopeSizing Outcome(ElevatorProject p, PresetLibrary lib, DesignContext ctx)
        {
            if (p == null || lib == null || !Follows(p))
                return null;
            var copy = ProjectJson.Clone(p);
            var c = new PresetContext { Project = copy, Library = lib, Design = ctx };
            foreach (var f in Fields)
            {
                if (!f.IsEdited(copy))
                    f.Store(copy, f.Preset(c));
            }
            return SizeRopes(copy, lib, ctx);
        }
    }
}
