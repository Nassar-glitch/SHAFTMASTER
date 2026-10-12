using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Application;
using Elevator.Core.Calculations;
using Elevator.Core.Company;
using Elevator.Core.MachineRoom;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.SafetyGear;

namespace Elevator.Core.Layouts
{
    /// <summary>
    /// The display-ready texts of the drawing title block (DESIGN-FLOW "Layouts and PDF"), built from the project, the preset library,
    /// the company settings and the calculation results. Company, client, prepared / checked / approved and revision come from the one
    /// <see cref="DocumentData"/> set shared with the other documents. UI-free. A field whose source is missing is "" (never a placeholder or a remark, principle 6).
    /// </summary>
    public sealed class TitleBlockData
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public string CompanyName = "";
        public string ProjectName = "";
        public string Client = "";
        /// <summary>No source yet: always "".</summary>
        public string DrawingTitle = "";
        /// <summary>No source yet: always "".</summary>
        public string Scale = "";
        /// <summary>The issue date, dd/MM/yyyy ("12/10/2026").</summary>
        public string Date = "";
        public string PreparedBy = "";
        public string CheckedBy = "";
        public string ApprovedBy = "";
        /// <summary>The revision as on every document: "" when blank, NN for a number ("01"), otherwise the stored text.</summary>
        public string Revision = "";
        /// <summary>"1.00 M/SEC."</summary>
        public string RatedSpeed = "";
        /// <summary>"450 KG. - 6 PASSENGERS."</summary>
        public string RatedLoad = "";
        /// <summary>"9.28 METRES.": lowest to highest served floor.</summary>
        public string Travel = "";
        /// <summary>The number of served floors: "4".</summary>
        public string Stops = "";
        /// <summary>The served floor names: "(G-1-2-3)".</summary>
        public string Floors = "";
        public string Control = "";
        public string Supply = "";
        /// <summary>Loads in kg only, "P1 = 545 KG.": P1–P4, the machine-room bearing reactions.</summary>
        public string P1 = "", P2 = "", P3 = "", P4 = "";
        /// <summary>"P5 = … KG.": the safety-gear load on ONE car guide rail.</summary>
        public string P5 = "";
        /// <summary>"P6 = … KG.": the load of ONE car buffer.</summary>
        public string P6 = "";
        /// <summary>"P7 = … KG.": the load of ONE counterweight buffer.</summary>
        public string P7 = "";

        static string Clean(string v)
        {
            return String.IsNullOrWhiteSpace(v) ? "" : v.Trim();
        }

        static bool Finite(double? v)
        {
            return v.HasValue && !Double.IsNaN(v.Value) && !Double.IsInfinity(v.Value);
        }

        /// <summary>A load in kg only: "P1 = 545 KG.", kg by DESIGN-FLOW principle 10 (N / 9.81 rounded up to 5 kg).</summary>
        public static string Load(string id, double? newtons)
        {
            if (!Finite(newtons))
                return "";
            return id + " = " + LoadUnits.KgText(newtons.Value) + " KG.";
        }

        /// <summary>The rated speed: "1.00 M/SEC.".</summary>
        public static string Speed(double? metresPerSecond)
        {
            return Finite(metresPerSecond) && metresPerSecond.Value > 0 ? metresPerSecond.Value.ToString("0.00", Inv) + " M/SEC." : "";
        }

        /// <summary>The rated load and persons: "450 KG. - 6 PASSENGERS.".</summary>
        public static string RatedLoadText(double? capacityKg, int? persons)
        {
            if (!Finite(capacityKg) || capacityKg.Value <= 0 || !persons.HasValue)
                return "";
            return capacityKg.Value.ToString("0.##", Inv) + " KG. - " + persons.Value.ToString(Inv) + (persons.Value == 1 ? " PASSENGER." : " PASSENGERS.");
        }

        /// <summary>The served floors of a project, lowest first; null when the floor heights are incomplete.</summary>
        static List<FloorElevation> Served(ElevatorProject p)
        {
            try
            {
                return ProjectFloors.Elevations(p.Building).Where(f => f.IsServed).ToList();
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>The travel from the lowest to the highest served floor: "9.28 METRES."; "" with fewer than two served floors.</summary>
        public static string TravelText(ElevatorProject p)
        {
            var served = p == null ? null : Served(p);
            if (served == null || served.Count < 2)
                return "";
            double m = (served.Last().ElevationMm - served.First().ElevationMm) / 1000;
            return m.ToString("0.00", Inv) + " METRES.";
        }

        static List<string> ServedNames(ElevatorProject p)
        {
            if (p == null || p.Building == null || p.Building.Floors == null)
                return new List<string>();
            return p.Building.Floors.Where(f => f != null && f.IsServed).Select(f => Clean(f.Name)).ToList();
        }

        /// <summary>The number of served floors: "4"; "" when none.</summary>
        public static string StopsText(ElevatorProject p)
        {
            int n = ServedNames(p).Count;
            return n > 0 ? n.ToString(Inv) : "";
        }

        /// <summary>The served floor names: "(G-1-2-3)"; "" when none or a name is blank.</summary>
        public static string FloorsText(ElevatorProject p)
        {
            var names = ServedNames(p);
            return names.Count == 0 || names.Any(n => n.Length == 0) ? "" : "(" + String.Join("-", names) + ")";
        }

        // The calculated load of ONE component of a P5–P7 result (one car guide rail for P5, one buffer for P6 / P7); null when the
        // result is not available.
        static double? OneComponentLoadN(SafetyPitLoad load)
        {
            bool available = load != null && load.PerComponentLoadN.HasValue && load.GroupLoadN.HasValue && load.ParticipatingMassKg.HasValue;
            return available ? load.PerComponentLoadN : null;
        }

        /// <summary>
        /// Builds the title block. reactions: the current P1–P4 bearing reactions (null: unavailable); safetyPit: the current P5–P7 result
        /// (null: unavailable; a result that no longer matches the project is not used); company: the company settings (null: no company
        /// name); date: the issue date (null: none).
        /// </summary>
        public static TitleBlockData Build(
            ElevatorProject project,
            PresetLibrary library,
            CompanySettings company,
            IList<BearingReaction> reactions,
            SafetyPitCalculationResult safetyPit,
            DateTime? date)
        {
            var t = new TitleBlockData();
            var doc = DocumentData.Resolve(project, company);
            t.CompanyName = Clean(doc.CompanyName);
            if (date.HasValue)
                t.Date = date.Value.ToString("dd/MM/yyyy", Inv);
            if (project == null)
                return t;
            t.ProjectName = Clean(project.Info == null ? null : project.Info.Name);
            t.Client = doc.Client;
            t.PreparedBy = doc.PreparedBy;
            t.CheckedBy = doc.CheckedBy;
            t.ApprovedBy = doc.ApprovedBy;
            t.Revision = doc.Revision;
            if (project.Elevator != null)
            {
                t.RatedSpeed = Speed(project.Elevator.SpeedMetresPerSecond);
                t.RatedLoad = RatedLoadText(project.Elevator.CapacityKg, library == null ? null : library.PersonsFor(project.Elevator.CapacityKg));
            }
            t.Travel = TravelText(project);
            t.Stops = StopsText(project);
            t.Floors = FloorsText(project);
            if (library != null)
            {
                t.Control = Clean(TitleBlockFields.Effective(TitleBlockFields.Control, project, library));
                t.Supply = Clean(TitleBlockFields.Effective(TitleBlockFields.Supply, project, library));
            }
            if (reactions != null)
            {
                Func<string, string> r = id =>
                {
                    var b = reactions.FirstOrDefault(x => x != null && x.Id == id);
                    return b == null ? "" : Load(id, b.UpwardN);
                };
                t.P1 = r("P1");
                t.P2 = r("P2");
                t.P3 = r("P3");
                t.P4 = r("P4");
            }
            if (safetyPit != null && SafetyPitCalculationEngine.IsCurrent(safetyPit, project))
            {
                SafetyPitLoad carGuideRail = safetyPit.P5, carBuffer = safetyPit.P6, counterweightBuffer = safetyPit.P7;
                t.P5 = Load("P5", OneComponentLoadN(carGuideRail));
                t.P6 = Load("P6", OneComponentLoadN(carBuffer));
                t.P7 = Load("P7", OneComponentLoadN(counterweightBuffer));
            }
            return t;
        }

        /// <summary>Builds the title block from the live results; a result that no longer matches the project is not used.</summary>
        public static TitleBlockData Build(
            ElevatorProject project,
            PresetLibrary library,
            CompanySettings company,
            LiveCalculation live,
            DateTime? date)
        {
            IList<BearingReaction> reactions = null;
            SafetyPitCalculationResult pit = null;
            if (live != null && project != null)
            {
                var chassis = live.Chassis == null ? null : live.Chassis.Result;
                if (chassis != null && chassis.Reactions != null && !live.ChassisStale(project))
                    reactions = chassis.Reactions;
                if (live.SafetyPit != null && !live.SafetyPitStale(project))
                    pit = live.SafetyPit;
            }
            return Build(project, library, company, reactions, pit, date);
        }
    }
}
