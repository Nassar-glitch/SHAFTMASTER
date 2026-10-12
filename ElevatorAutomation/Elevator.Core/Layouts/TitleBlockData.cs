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
        /// <summary>"AS SHOWN": every view carries its own scale under it (DESIGN-FLOW "Sheet arrangement").</summary>
        public string Scale = "";
        /// <summary>The project number: Report.DocumentNumber.</summary>
        public string ProjectNumber = "";
        /// <summary>The type of work: the title-block preset "أعمال مصاعد", editable per project.</summary>
        public string TypeOfWork = "";
        /// <summary>The phase: optional, typed per project.</summary>
        public string Phase = "";
        /// <summary>The drawing number on the sheet: "1".</summary>
        public string SheetNo = "";
        /// <summary>The number of entrances: one per served floor, so the number of stops.</summary>
        public string Entrances = "";
        /// <summary>The floors with an entrance: "(G-1-2-3)".</summary>
        public string EntranceNames = "";
        /// <summary>The issue date, dd/MM/yyyy ("12/10/2026").</summary>
        public string Date = "";
        public string PreparedBy = "";
        public string CheckedBy = "";
        public string ApprovedBy = "";
        /// <summary>The revision: "00" when blank, NN for a number ("01"), otherwise the stored text.</summary>
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

        /// <summary>The scale cell text.</summary>
        public const string AsShown = "AS SHOWN";

        /// <summary>The drawing number of the single geared sheet.</summary>
        public const string FirstSheet = "1";

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
            t.Scale = AsShown;
            t.SheetNo = FirstSheet;
            if (date.HasValue)
                t.Date = date.Value.ToString("dd/MM/yyyy", Inv);
            if (project == null)
                return t;
            t.ProjectNumber = doc.DocumentNumber;
            t.ProjectName = Clean(project.Info == null ? null : project.Info.Name);
            t.Client = doc.Client;
            t.PreparedBy = doc.PreparedBy;
            t.CheckedBy = doc.CheckedBy;
            t.ApprovedBy = doc.ApprovedBy;
            t.Revision = project.Report == null ? DrawingRevision.Display(null) : DrawingRevision.Display(project.Report.Revision);
            if (project.Elevator != null)
            {
                t.RatedSpeed = Speed(project.Elevator.SpeedMetresPerSecond);
                t.RatedLoad = RatedLoadText(project.Elevator.CapacityKg, library == null ? null : library.PersonsFor(project.Elevator.CapacityKg));
            }
            t.Travel = TravelText(project);
            t.Stops = StopsText(project);
            t.Floors = FloorsText(project);
            // One entrance per served floor (geared, one-sided): the entrances are the stops.
            t.Entrances = t.Stops;
            t.EntranceNames = t.Floors;
            if (library != null)
            {
                t.Control = Clean(TitleBlockFields.Effective(TitleBlockFields.Control, project, library));
                t.Supply = Clean(TitleBlockFields.Effective(TitleBlockFields.Supply, project, library));
                t.TypeOfWork = Clean(TitleBlockFields.Effective(TitleBlockFields.TypeOfWork, project, library));
            }
            if (project != null)
            {
                t.Phase = Clean(TitleBlockFields.Effective(TitleBlockFields.Phase, project, library));
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

        /// <summary>The attribute tags of the block SM_TITLEBLOCK in Templates/Layout_Geared_A0.dxf, in the order of <see cref="Attributes"/>.</summary>
        public static readonly string[] Tags =
        {
            "P1", "P2", "P3", "P4", "P5", "P6", "P7", "LOAD", "SPEED", "TRAVEL", "STOPS", "FLOORS", "ENTRANCES", "ENTRANCE_NAMES", "CONTROL",
            "SUPPLY", "COMPANY_AR", "COMPANY", "REV_ROW1_NO", "REV_ROW1_DATE", "CLIENT", "PROJECT", "DOC_NO", "DRAWING_NAME", "TYPE_OF_WORK",
            "SCALE", "PREPARED", "CHECKED", "APPROVED", "DATE", "PHASE", "SHEET_NO"
        };

        /// <summary>
        /// The value of every attribute of the title block, with its script and text style: Arabic values go to the xarab style
        /// encoded (<see cref="XarabText"/>), other values to the cent style as they are. The company name fills COMPANY_AR when it is
        /// typed in Arabic and COMPANY otherwise; the first revision row holds the revision and its date.
        /// </summary>
        public List<TitleBlockAttribute> Attributes()
        {
            bool arabicCompany = XarabText.Detect(CompanyName) == TextScript.Arabic;
            var values = new[]
            {
                P1, P2, P3, P4, P5, P6, P7, RatedLoad, RatedSpeed, Travel, Stops, Floors, Entrances, EntranceNames, Control, Supply,
                arabicCompany ? CompanyName : "", arabicCompany ? "" : CompanyName, Revision, Revision.Length > 0 ? Date : "", Client,
                ProjectName, ProjectNumber, DrawingTitle, TypeOfWork, Scale, PreparedBy, CheckedBy, ApprovedBy, Date, Phase, SheetNo
            };
            var list = new List<TitleBlockAttribute>(Tags.Length);
            for (int i = 0; i < Tags.Length; i++)
                list.Add(TitleBlockAttribute.Of(Tags[i], values[i] ?? ""));
            return list;
        }
    }

    /// <summary>One attribute of the title block: the display value, its script, the template style and the text written into the drawing.</summary>
    public sealed class TitleBlockAttribute
    {
        public string Tag;
        /// <summary>The value as the user reads it (Unicode).</summary>
        public string Value;
        public TextScript Script;
        /// <summary>The template text style: <see cref="XarabText.ArabicStyle"/> or <see cref="XarabText.LatinStyle"/>.</summary>
        public string Style;
        /// <summary>The text written into the attribute: the xarab codes for Arabic, the value itself otherwise.</summary>
        public string DrawingText;

        /// <summary>The attribute of a tag and a value.</summary>
        public static TitleBlockAttribute Of(string tag, string value)
        {
            var script = XarabText.Detect(value);
            return new TitleBlockAttribute
            {
                Tag = tag,
                Value = value,
                Script = script,
                Style = script == TextScript.Arabic ? XarabText.ArabicStyle : XarabText.LatinStyle,
                DrawingText = XarabText.ForDrawing(value)
            };
        }
    }
}
