using System;
using System.Globalization;
using System.Linq;
using Elevator.Core.Project;

namespace Elevator.Core.Presets
{
    public static class PresetFields
    {
        static Elevator.Core.Suspension.RopeTractionInputs Group(ElevatorProject p)
        {
            if (p.Calculations == null)
                p.Calculations = new Elevator.Core.Calculations.ChassisCalculationInputs();
            if (p.Calculations.RopeTraction == null)
                p.Calculations.RopeTraction = new Elevator.Core.Suspension.RopeTractionInputs();
            return p.Calculations.RopeTraction;
        }

        static Elevator.Core.Suspension.RopeTractionInputs Read(ElevatorProject p)
        {
            return p == null || p.Calculations == null ? null : p.Calculations.RopeTraction;
        }

        static PresetField Number(
            string name,
            string label,
            Func<PresetContext, double?> preset,
            Func<Elevator.Core.Suspension.RopeTractionInputs, double?> get,
            Action<Elevator.Core.Suspension.RopeTractionInputs, double?> set)
        {
            return new PresetField
            {
                Key = "Calculations.RopeTraction." + name,
                Label = label,
                Preset = c => preset(c),
                Stored = p =>
                {
                    var g = Read(p);
                    return g == null ? null : get(g);
                },
                Store = (p, v) => set(Group(p), v == null ? (double?)null : Convert.ToDouble(v, CultureInfo.InvariantCulture))
            };
        }

        static PresetField Text(
            string name,
            string label,
            Func<PresetContext, string> preset,
            Func<Elevator.Core.Suspension.RopeTractionInputs, string> get,
            Action<Elevator.Core.Suspension.RopeTractionInputs, string> set)
        {
            return new PresetField
            {
                Key = "Calculations.RopeTraction." + name,
                Label = label,
                Preset = c => preset(c),
                Stored = p =>
                {
                    var g = Read(p);
                    return g == null ? null : get(g);
                },
                Store = (p, v) => set(Group(p), v as string)
            };
        }

        static double? RopeDiameter(PresetContext c)
        {
            return c.Project == null || c.Project.Suspension == null ? null : c.Project.Suspension.RopeDiameterMm;
        }

        public static readonly PresetField RopeDesignation = Text(
            "RopeDesignation",
            "Rope designation",
            c => c.Library.Ropes.Designation,
            g => g.RopeDesignation,
            (g, v) => g.RopeDesignation = v);

        public static readonly PresetField MinimumBreakingLoad = Number(
            "MinimumBreakingLoadKn",
            "Rope minimum breaking load MBL per rope (kN)",
            c =>
            {
                var r = c.Library.Rope(RopeDiameter(c));
                return r == null ? (double?)null : r.MinimumBreakingLoadKn;
            },
            g => g.MinimumBreakingLoadKn,
            (g, v) => g.MinimumBreakingLoadKn = v);

        public static readonly PresetField RopeMass = Number(
            "RopeMassKgPerM",
            "Rope mass per metre (kg/m)",
            c =>
            {
                var r = c.Library.Rope(RopeDiameter(c));
                return r == null ? (double?)null : r.MassKgPerM;
            },
            g => g.RopeMassKgPerM,
            (g, v) => g.RopeMassKgPerM = v);

        public static readonly PresetField GrooveType =
            Text("GrooveType", "Traction sheave groove", c => c.Library.Groove.Type, g => g.GrooveType, (g, v) => g.GrooveType = v);

        public static readonly PresetField UndercutAngle = Number(
            "UndercutAngleDeg",
            "Undercut angle β (°)",
            c =>
            {
                var g = Read(c.Project);
                return g != null && g.SelectedUndercutAngleDeg.HasValue
                ? g.SelectedUndercutAngleDeg
                : c.Library.Groove.UndercutAngleDeg;
            },
            g => g.UndercutAngleDeg,
            (g, v) => g.UndercutAngleDeg = v);

        public static readonly PresetField GrooveAngle = Number(
            "GrooveAngleDeg",
            "Groove angle γ (°)",
            c => c.Library.Groove.GrooveAngleDeg,
            g => g.GrooveAngleDeg,
            (g, v) => g.GrooveAngleDeg = v);

        public static readonly PresetField TravellingCable = Number(
            "TravellingCableKgPerM",
            "Travelling cable mass per metre, all cables (kg/m)",
            c => c.Library.TravellingCableKgPerM,
            g => g.TravellingCableKgPerM,
            (g, v) => g.TravellingCableKgPerM = v);

        public static readonly PresetField BrakingRetardation = Number(
            "BrakingRetardationMps2",
            "Emergency braking retardation a (m/s²)",
            c => c.Library.BrakingRetardationMps2,
            g => g.BrakingRetardationMps2,
            (g, v) => g.BrakingRetardationMps2 = v);

        public static readonly PresetField DeflectorMass = Number(
            "DeflectorMassKg",
            "Deflector sheave mass (kg)",
            c => c.Library.DeflectorMassFor(c.DeflectorPitchMm),
            g => g.DeflectorMassKg,
            (g, v) => g.DeflectorMassKg = v);

        public static readonly PresetField TractionLimitingMethod = Text(
            "TractionLimitingMethod",
            "Traction limiting method",
            c => c.Library.TractionLimitingMethod,
            g => g.TractionLimitingMethod,
            (g, v) => g.TractionLimitingMethod = v);

        public static readonly PresetField[] RopeTraction = { RopeDesignation,
            MinimumBreakingLoad,
            RopeMass,
            GrooveType,
            UndercutAngle,
            GrooveAngle,
            TravellingCable,
            BrakingRetardation,
            DeflectorMass,
            TractionLimitingMethod };

        public static PresetField Find(string key)
        {
            return RopeTraction.FirstOrDefault(f => f.Key == key);
        }
    }
}
