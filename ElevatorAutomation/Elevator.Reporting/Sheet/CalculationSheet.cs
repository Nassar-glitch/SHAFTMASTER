using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Elevator.Core.Application;
using Elevator.Core.Calculations;
using Elevator.Core.Doors;
using Elevator.Core.Geometry;
using Elevator.Core.GuideRails;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;
using Elevator.Core.SafetyGear;
using Elevator.Reporting.Docx;
using Elevator.Reporting.Figures;

namespace Elevator.Reporting.Sheet
{
    /// <summary>
    /// Every result is recalculated from the current canonical project at export and checked against its input fingerprint. Previously
    /// displayed UI numbers are never reused. The document contains only available calculations; unavailable ones are omitted and reported
    /// to the user before saving. Source-note traceability lives in docs/calculations/CAR-GUIDE-FORCES.md and docs/calculations/CALCULATION-SHEET-EXPORT.md.
    /// </summary>
    public sealed partial class CalculationSheet
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        /// <summary>The standards line printed on the sheet.</summary>
        public const string DesignBasis = "Design basis: EN 81-20:2014 and EN 81-50:2014.";

        public ElevatorProject Project { get; private set; }
        public CalculationSheetInputs Inputs { get; private set; }
        public string Mode { get; private set; }
        public ChassisCalculationResult Chassis { get; private set; }
        public bool ChassisAvailable { get; private set; }
        public SafetyPitCalculationResult SafetyPit { get; private set; }
        public CarGuideForceResult CarGuideForces { get; private set; }
        public bool CarGuideForcesAvailable { get; private set; }
        public List<OmittedCalculation> Omitted { get; private set; }

        public bool HasContent
        {
            get
            {
                return ChassisAvailable || Loads.Any(Available) || CarGuideForcesAvailable || RopeTractionAvailable;
            }
        }

        public bool RopeTensionsAvailable
        {
            get
            {
                return ChassisAvailable && Chassis.CarBundleTensionN.HasValue && Chassis.CounterweightBundleTensionN.HasValue;
            }
        }

        CalculationSheet()
        {
            Omitted = new List<OmittedCalculation>();
        }

        /// <summary>Builds the calculation sheet of a project: runs the calculations and collects what can be shown.</summary>
        public static CalculationSheet Build(ElevatorProject project, DesignContext context, CalculationSheetInputs inputs)
        {
            if (project == null)
                throw new ArgumentException("No project is open.");
            var s = new CalculationSheet { Project = project, Inputs = inputs ?? new CalculationSheetInputs() };
            s.Mode = CalculationModes.Resolve(project.Calculations);
            // P1–P4: active workflow only.
            BuildChassis(project, context, s);
            // P5–P7: independent of P1–P4 availability.
            BuildSafetyPit(project, s);
            // FF1/FF2: independent workflow.
            BuildCarGuideForces(project, context, s);
            BuildPlan(project, context, s);
            var profiles = new ElevatorDesignResult();
            s.CarRail = ProportionalRailSection.Parse(profiles.CarRailProfile);
            s.CounterweightRail = ProportionalRailSection.Parse(profiles.CounterweightRailProfile);
            RecordOmissions(s);
            s.BuildRopeTraction(context);
            // A Doors Offset outside its range is listed with the other NOT OK results before export.
            if (s.DoorOffset != null && s.DoorOffset.Warning != null && s.NotOk != null)
                s.NotOk.Add(s.DoorOffset.Warning);
            return s;
        }

        private static void BuildChassis(ElevatorProject project, DesignContext context, CalculationSheet s)
        {
            if (s.Inputs.ChassisDraftErrors.Count > 0)
            {
                s.Chassis = new ChassisCalculationResult { Mode = s.Mode, Status = "INVALID_INPUT" };
                foreach (var e in s.Inputs.ChassisDraftErrors)
                    s.Chassis.Issues.Add(new CalculationIssue(e.Key, e.Value));
            }
            else
            {
                var workspace = new CalculationWorkspace();
                s.Chassis = workspace.Calculate(project, context);
                s.ChassisAvailable = s.Chassis.Reactions != null && workspace.IsCurrent(project, context);
                if (s.Chassis.Reactions != null && !s.ChassisAvailable)
                {
                    s.Chassis.Issues.Add(
                        new CalculationIssue("Currency", "Result could not be verified current against the project inputs."));
                    s.Chassis.Reactions = null;
                }
            }
        }

        private static void BuildSafetyPit(ElevatorProject project, CalculationSheet s)
        {
            s.SafetyPit = new SafetyPitCalculationEngine().Calculate(project);
            foreach (var error in s.Inputs.SafetyPitDraftErrors)
            {
                bool cw = error.Key == "Calculations.CounterweightMassKg"
                    || error.Key == "Calculations.CounterweightSideAdditionalSuspendedMassKg"
                    || error.Key.EndsWith("CounterweightEventAdditionalMassPercent");
                foreach (var load in cw ? new[] { s.SafetyPit.P7 } : new[] { s.SafetyPit.P5, s.SafetyPit.P6 })
                {
                    load.Status = "INVALID_INPUT";
                    load.GroupLoadN = null;
                    load.PerComponentLoadN = null;
                    load.ParticipatingMassKg = null;
                    load.Details.Clear();
                    load.Issues.RemoveAll(i => i.Field == error.Key);
                    load.Issues.Add(new CalculationIssue(error.Key, error.Value));
                }
            }
            if (!SafetyPitCalculationEngine.IsCurrent(s.SafetyPit, project))
            {
                foreach (var load in s.Loads)
                {
                    load.Status = "UNVERIFIED";
                    load.GroupLoadN = null;
                    load.PerComponentLoadN = null;
                    load.Issues.Add(new CalculationIssue("Currency", "Result could not be verified current."));
                }
            }
        }

        private static void BuildCarGuideForces(ElevatorProject project, DesignContext context, CalculationSheet s)
        {
            var geometry = new ElevatorDesignEngine().CalculateCarGuideForceGeometry(project, context);
            s.CarGuideForces = new CarGuideForceCalculationEngine().Calculate(project, geometry, context);
            if (s.Inputs.CarGuideForceDraftError != null)
            {
                var r = s.CarGuideForces;
                r.Status = "INVALID_INPUT";
                r.FF1BaseN = null;
                r.FF1DesignN = null;
                r.FF2BaseN = null;
                r.FF2DesignN = null;
                r.Details.Clear();
                const string field = "Calculations.CarGuideForces.GuideShoeSpacingMm";
                r.Issues.RemoveAll(i => i.Field == field);
                r.Issues.Add(new CalculationIssue(field, s.Inputs.CarGuideForceDraftError));
            }
            s.CarGuideForcesAvailable = s.CarGuideForces.FF1BaseN.HasValue
                && s.CarGuideForces.FF2BaseN.HasValue
                && CarGuideForceCalculationEngine.IsCurrent(s.CarGuideForces, project, context);
            if (s.CarGuideForces.FF1BaseN.HasValue && !s.CarGuideForcesAvailable)
            {
                var r = s.CarGuideForces;
                r.FF1BaseN = null;
                r.FF1DesignN = null;
                r.FF2BaseN = null;
                r.FF2DesignN = null;
                r.Issues.Add(new CalculationIssue("Currency", "Result could not be verified current."));
            }
        }

        private static void BuildPlan(ElevatorProject project, DesignContext context, CalculationSheet s)
        {
            try
            {
                if (context != null
                    && context.Equipment != null
                    && project.Shaft != null
                    && Positive(project.Shaft.WidthMm)
                    && Positive(project.Shaft.DepthMm)
                    && Positive(context.CounterweightDepthMm))
                {
                    s.Plan = DesignRules.Calculate(
                                            project.Shaft.WidthMm,
                                            project.Shaft.DepthMm,
                                            context.Equipment,
                                            context.CounterweightDepthMm,
                                            Elevator.Core.Doors.DoorInput.From(project, context.Doors));
                }
            }
            catch (ArgumentException)
            {
                s.Plan = null;
            }
            if (s.Plan != null && s.CarGuideForcesAvailable && (Math.Abs(s.Plan.CarWidth - s.CarGuideForces.InternalWidthMm.Value) > 1e-6
                || Math.Abs(s.Plan.CarDepth - s.CarGuideForces.InternalDepthMm.Value) > 1e-6))
                s.Plan = null;
            // Automatic door: the layout behind the plan (type, size and handing appear in the input table). Manual: none.
            var door = DoorInput.From(project, context == null ? null : context.Doors);
            if (s.Plan != null && door != null)
            {
                s.Door = DoorRules.Select(door, s.Plan);
                s.DoorOffset = DoorOffset.From(door, s.Door, s.Plan);
            }
        }

        private static void RecordOmissions(CalculationSheet s)
        {
            if (!s.ChassisAvailable)
                s.Omit("P1–P4 machine-room bearing reactions and rope tensions (" + s.Mode + ")", s.Chassis.Issues);
            foreach (var load in s.Loads)
            {
                if (!Available(load))
                    s.Omit(LoadTitle(load), load.Issues);
            }
            if (!s.CarGuideForcesAvailable)
                s.Omit("FF1 / FF2 car guide-rail forces", s.CarGuideForces.Issues);
        }

        void Omit(string name, IEnumerable<CalculationIssue> issues)
        {
            var o = new OmittedCalculation { Name = name };
            foreach (var i in issues)
                o.Reasons.Add(FieldLabel(i.Field) + ": " + i.Message);
            if (o.Reasons.Count == 0)
                o.Reasons.Add("Required inputs are not available.");
            Omitted.Add(o);
        }

        static string FieldLabel(string field)
        {
            switch (field ?? "")
            {
                case "Elevator.CapacityKg":
                    return "Rated load";
                case "Shaft.WidthMm":
                    return "Shaft width";
                case "Shaft.DepthMm":
                    return "Shaft depth";
                case "Calculations.CarMassKg":
                    return "Car mass";
                case "Calculations.CounterweightMassKg":
                    return "Counterweight mass";
                case "Calculations.MachineMassKg":
                    return "Machine mass";
                case "Calculations.CarGuideForces.GuideShoeSpacingMm":
                    return "Guide-shoe spacing h";
                case "SafetyPit.SafetyGearType":
                    return "Safety-gear type";
                case "SafetyPit.CarGuideCount":
                    return "Car guide rails";
                case "SafetyPit.CarBufferCount":
                    return "Car buffers";
                case "SafetyPit.CounterweightBufferCount":
                    return "Counterweight buffers";
                case "Suspension.RopeCount":
                    return "Number of ropes";
                case "Suspension.RopingRatio":
                    return "Roping ratio";
                default:
                    return String.IsNullOrEmpty(field) ? "Input" : field;
            }
        }

        static bool Available(SafetyPitLoad load)
        {
            return load.PerComponentLoadN.HasValue && load.GroupLoadN.HasValue && load.ParticipatingMassKg.HasValue;
        }

        static string LoadTitle(SafetyPitLoad load)
        {
            return load.Id == "P5"
                ? "P5 safety-gear load per car guide rail"
                : load.Id == "P6" ? "P6 car-buffer reaction" : "P7 counterweight-buffer reaction";
        }

        public IEnumerable<SafetyPitLoad> Loads
        {
            get
            {
                return new[] { SafetyPit.P5, SafetyPit.P6, SafetyPit.P7 };
            }
        }

        /// <summary>The suggested file name for a project.</summary>
        public static string SuggestedFileName(string projectName)
        {
            string name = String.IsNullOrWhiteSpace(projectName) ? "Untitled project" : projectName.Trim();
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            name = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim(' ', '.');
            if (name.Length > 80)
                name = name.Substring(0, 80).Trim(' ', '.');
            if (name.Length == 0)
                name = "Untitled project";
            return name + " - Calculation Sheet.docx";
        }

        /// <summary>Write atomically: a failed export never truncates an existing file.</summary>
        public void Save(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Choose a file name for the calculation sheet.");
            if (!HasContent)
                throw new InvalidOperationException("No calculation is available to export.");
            path = Path.GetFullPath(path);
            if (!String.Equals(Path.GetExtension(path), ".docx", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The calculation sheet must be saved as a .docx file.");
            string folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException("The selected folder does not exist: " + folder);
            string temporary = Path.Combine(folder, "~" + Guid.NewGuid().ToString("N") + ".docx.tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite))
                    Render().Save(stream);
                try
                {
                    if (File.Exists(path))
                        File.Replace(temporary, path, null);
                    else
                        File.Move(temporary, path);
                }
                catch (IOException e)
                {
                    throw new IOException(
                    "Could not write " + path + ". If the document is open in Word, close it and export again. (" + e.Message + ")",
                    e);
                }
                catch (UnauthorizedAccessException e)
                {
                    throw new IOException(
                    "Access denied writing " + path + ". Choose another folder. (" + e.Message + ")",
                    e);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        // ---------- formatting (only ever applied to available, finite values) ----------
        static string L(double v)
        {
            return v.ToString("0.##", Inv);
        }

        static string N(double? v)
        {
            return L(v.Value);
        }

        static string KN(double? v)
        {
            return (v.Value / 1000).ToString("0.###", Inv);
        }

        static string KG(double? v)
        {
            return LoadUnits.KgText(v.Value);
        }

        // A force in N with its kg value beside it: "12345 N (1260 kg)" (DESIGN-FLOW principle 10).
        static string NK(double? v)
        {
            return v.HasValue ? N(v) + " N (" + KG(v) + " kg)" : N(v) + " N";
        }

        static string LK(double v)
        {
            return L(v) + " N (" + KG(v) + " kg)";
        }

        static string S(double v)
        {
            return v < 0 ? "(" + L(v) + ")" : L(v);
        }

        static string Pt(LocalPoint p)
        {
            return "(" + L(p.X) + ", " + L(p.Y) + ", " + L(p.Z) + ")";
        }

        static bool Positive(double? v)
        {
            return v.HasValue && !Double.IsNaN(v.Value) && !Double.IsInfinity(v.Value) && v.Value > 0;
        }

        // Figures actually placed in the last Render, numbered in order; builder failures are recorded (and tested) instead of breaking the
        // export.
        public List<Figure> Figures { get; private set; }
        public List<string> FigureErrors { get; private set; }
        public ElevatorParameters Plan { get; private set; }

        /// <summary>The layout of an automatic car door (null for a Manual door).</summary>
        public DoorLayout Door { get; private set; }

        /// <summary>The Doors Offset of the automatic door (range and state), or null for a Manual door.</summary>
        public DoorOffset DoorOffset { get; private set; }
        public IRailSection CarRail { get; private set; }
        public IRailSection CounterweightRail { get; private set; }

        int figureNumber;

        void AddFigure(DocxDocument d, Func<Figure> build)
        {
            Figure f;
            try
            {
                f = build();
            }
            catch (Exception e)
            {
                FigureErrors.Add(e.Message);
                return;
            }
            if (f == null)
                return;
            figureNumber++;
            Figures.Add(f);
            d.FigureFrame(
                FigureRender.Image(f),
                f.WidthMm,
                f.HeightMm,
                "Figure " + figureNumber + " — " + f.Title,
                "Dimensions in mm",
                f.Schematic ? "NTS" : null);
        }

        static string Clean(string v)
        {
            return String.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }

        static bool Finite(double? v)
        {
            return v.HasValue && !Double.IsNaN(v.Value) && !Double.IsInfinity(v.Value);
        }

        bool Draft(string path)
        {
            return Inputs.ChassisDraftErrors.ContainsKey(path)
                || Inputs.SafetyPitDraftErrors.ContainsKey(path)
                || (path == "Calculations.CarGuideForces.GuideShoeSpacingMm" && Inputs.CarGuideForceDraftError != null);
        }

        // A supplied, valid, nonzero value; anything else is not listed.
        bool Has(string path, double? v)
        {
            return Positive(v) && !Draft(path);
        }

        static string Bearing(string id)
        {
            return id == "P1" ? "right / rear" : id == "P2" ? "right / front" : id == "P3" ? "left / rear" : "left / front";
        }

        static string System(string id)
        {
            return id == "traction-geared" ? "Geared traction elevator" : String.IsNullOrWhiteSpace(id) ? "Traction elevator" : id;
        }

        static string LoadName(string id)
        {
            switch (id)
            {
                case "MachineWeight":
                    return "Machine weight";
                case "TractionRopeAction":
                    return "Rope forces at traction sheave";
                case "DeflectorHangerAction":
                    return "Rope forces at deflector hanger";
                case "ChassisWeight":
                    return "Chassis weight";
                case "HangerAssemblyWeight":
                    return "Hanger assembly weight";
                default:
                    return id;
            }
        }

        static string Member(string id)
        {
            return String.IsNullOrEmpty(id) ? "" : id.StartsWith("Longitudinal.") ? id.Substring(13) + " longitudinal member" : id;
        }

        static readonly int[] TwoCol = { 4200, 5438 };

        static KeyValuePair<string, string> Bold(string t)
        {
            return new KeyValuePair<string, string>("<w:b/>", t);
        }

        static KeyValuePair<string, string> Plain(string t)
        {
            return new KeyValuePair<string, string>(null, t);
        }

        static List<string> NullFills(int count, bool shadeLast)
        {
            var f = Enumerable.Repeat<string>(null, count).ToList();
            if (shadeLast && count > 0)
                f[count - 1] = "EEF3F8";
            return f;
        }

        // ---------- document ----------
        /// <summary>Renders the sheet as a document.</summary>
        public DocxDocument Render()
        {
            if (!HasContent)
                throw new InvalidOperationException("No calculation is available to export.");
            var p = Project;
            var report = p.Report ?? new ProjectReportDetails();
            string date = Inputs.GeneratedLocal.ToString("yyyy-MM-dd", Inv);
            string name = Clean(p.Info.Name), company = Clean(Inputs.CompanyName), client =
                Clean(report.ClientName), number = Clean(report.DocumentNumber), revision = Clean(report.Revision);
            // Blank optional fields are left out entirely; no placeholder is ever printed.
            var d = new DocxDocument { Title = "Elevator Calculation Sheet" + (name == null ? "" : " · " + name) };
            // CAD-style title block on every page; blank rows are dropped inside their cells.
            d.TitleBlock.Logo = Inputs.Logo;
            d.TitleBlock.Company = company;
            if (name != null)
                d.TitleBlock.ProjectRows.Add(new KeyValuePair<string, string>("Project", name));
            if (client != null)
                d.TitleBlock.ProjectRows.Add(new KeyValuePair<string, string>("Client", client));
            if (number != null)
                d.TitleBlock.DocumentRows.Add(new KeyValuePair<string, string>("Doc. No.", number));
            if (revision != null)
                d.TitleBlock.DocumentRows.Add(new KeyValuePair<string, string>("Rev.", revision));
            d.FooterLeft = "Prepared with ShaftMaster · " + date;
            d.FooterRight = number == null && revision == null ? null : (number == null ? "" : "Doc. No. " + number)
                + (number != null && revision != null ? " · " : "")
                + (revision == null ? "" : "Rev " + revision);
            figureNumber = 0;
            Figures = new List<Figure>();
            FigureErrors = new List<string>();
            FigureRender.Initialise();
            d.Paragraph("Elevator Calculation Sheet", "Title");
            var block = new List<string[]>();
            if (name != null)
                block.Add(new[] { "Project", name });
            if (client != null)
                block.Add(new[] { "Client", client });
            if (number != null)
                block.Add(new[] { "Document No.", number });
            if (revision != null)
                block.Add(new[] { "Revision", revision });
            block.Add(new[] { "Date", date });
            block.Add(new[] { "Elevator system", System(p.Elevator.SystemId) });
            if (Has("Elevator.CapacityKg", p.Elevator.CapacityKg))
                block.Add(new[] { "Rated load", L(p.Elevator.CapacityKg.Value) + " kg" });
            d.Table(null, block, TwoCol, new HashSet<int> { 0 });
            d.Paragraph(DesignBasis, null, "<w:b/>");
            // Approval block: always present; names when given, signature and date cells left empty for hand signing.
            d.Table(new[] { "", "Name", "Signature", "Date" }, new List<string[]>{
    new[]{"Prepared by",Clean(report.PreparedBy)??"","",""},
        new[]{"Checked by",Clean(report.CheckedBy)??"","",""},
        new[]{"Approved by",Clean(report.ApprovedBy)??"","",""}},
             new[] { 1900, 3538, 2600, 1600 }, new HashSet<int> { 0 }, null, 624);
            int n = 1;
            Summary(d, n++);
            ProjectInputs(d, n++);
            if (ChassisAvailable)
            {
                if (Mode == "Basic")
                    BasicChassis(d, n++);
                else
                    AdvancedChassis(d, n++);
            }
            if (RopeTensionsAvailable)
                RopeTensions(d, n++);
            if (RopeTractionAvailable)
                RopeTractionSection(d, n++);
            if (Loads.Any(Available))
                SafetyPitSection(d, n++);
            if (CarGuideForcesAvailable)
                ForcesSection(d, n++);
            return d;
        }

        void Summary(DocxDocument d, int n)
        {
            d.Heading(n + "  Calculation summary", 1);
            var rows = new List<string[]>();
            if (ChassisAvailable)
            {
                rows.Add(new[]
                {
                    "P1–P4 machine-room bearing reactions",
                    String.Join("; ", Chassis.Reactions.OrderBy(b => b.Id).Select(b => b.Id + " " + KN(b.UpwardN) + " kN (" + KG(b.UpwardN) + " kg)"))
                });
            }
            if (RopeTensionsAvailable)
            {
                rows.Add(new[]
                {
                    "Suspension rope tensions",
                    "Car side "
                        + KN(Chassis.CarBundleTensionN)
                        + " kN (" + KG(Chassis.CarBundleTensionN) + " kg); counterweight side "
                        + KN(Chassis.CounterweightBundleTensionN)
                        + " kN (" + KG(Chassis.CounterweightBundleTensionN) + " kg)"
                });
            }
            if (RopeTractionAvailable)
                rows.Add(new[] { RopeTractionSectionTitle, RopeSummary() });
            foreach (var load in Loads.Where(Available))
            {
                rows.Add(new[]
                {
                    LoadTitle(load),
                    load.Id == "P5"
                        ? KN(load.PerComponentLoadN) + " kN (" + KG(load.PerComponentLoadN) + " kg) per car guide rail"
                        : KN(load.PerComponentLoadN) + " kN (" + KG(load.PerComponentLoadN) + " kg) per buffer; " + KN(load.GroupLoadN) + " kN (" + KG(load.GroupLoadN) + " kg) total"
                });
            }
            var f = CarGuideForces;
            if (CarGuideForcesAvailable)
            {
                rows.Add(new[]
                {
                    "FF1 / FF2 car guide-rail forces",
                    "FF1 " + KN(f.FF1DesignN) + " kN (" + KG(f.FF1DesignN) + " kg); FF2 " + KN(f.FF2DesignN) + " kN (" + KG(f.FF2DesignN) + " kg) (design, per guide rail)"
                });
            }
            d.Table(new[] { "Calculation", "Key result" }, rows, new[] { 3900, 5738 }, new HashSet<int> { 0 });
        }

        void ProjectInputs(DocxDocument d, int n)
        {
            var p = Project;
            var c = p.Calculations ?? new ChassisCalculationInputs();
            d.Heading(n + "  Input data", 1);
            var rows = new List<string[]>();
            Action<string, string, double?, string> add =
                (label, path, v, unit) =>
                {
                    if (Has(path, v))
                        rows.Add(new[] { label, L(v.Value) + " " + unit });
                };
            // Only inputs used by the calculations that appear in this document.
            bool chassis = ChassisAvailable, carEvent =
                Available(SafetyPit.P5) || Available(SafetyPit.P6), cwEvent = Available(SafetyPit.P7), ff = CarGuideForcesAvailable;
            if (chassis || carEvent || ff)
                add("Rated load Q", "Elevator.CapacityKg", p.Elevator.CapacityKg, "kg");
            if (p.Shaft != null && (chassis || ff))
            {
                add("Shaft width", "Shaft.WidthMm", p.Shaft.WidthMm, "mm");
                add("Shaft depth", "Shaft.DepthMm", p.Shaft.DepthMm, "mm");
            }
            if (Door != null && (chassis || ff))
                DoorInputRows(rows);
            if (chassis && Positive(p.Suspension.RopingRatio))
                rows.Add(new[] { "Roping", p.Suspension.RopingRatio.Value.ToString(Inv) + ":1" });
            if (chassis && p.Suspension.RopeCount.HasValue && p.Suspension.RopeCount.Value > 0)
                rows.Add(new[] { "Number of suspension ropes n", p.Suspension.RopeCount.Value.ToString(Inv) });
            if (chassis || carEvent)
                add("Car mass", "Calculations.CarMassKg", c.CarMassKg, "kg");
            if (chassis || cwEvent)
                add("Counterweight mass", "Calculations.CounterweightMassKg", c.CounterweightMassKg, "kg");
            if (chassis)
                add("Machine mass", "Calculations.MachineMassKg", c.MachineMassKg, "kg");
            if (chassis || carEvent)
                add("Car-side additional mass", "Calculations.CarSideAdditionalSuspendedMassKg", c.CarSideAdditionalSuspendedMassKg, "kg");
            if (chassis || cwEvent)
            {
                add(
                                    "Counterweight-side additional mass",
                                    "Calculations.CounterweightSideAdditionalSuspendedMassKg",
                                    c.CounterweightSideAdditionalSuspendedMassKg,
                                    "kg");
            }
            if (chassis && c.IncludeChassisSelfWeight == true)
                add("Chassis assembly mass", "Calculations.ChassisMassKg", c.ChassisMassKg, "kg");
            if (chassis && c.IncludeHangerAssemblyWeight == true)
                add("Hanger assembly mass", "Calculations.HangerAssemblyMassKg", c.HangerAssemblyMassKg, "kg");
            if (ff && c.CarGuideForces != null)
                add("Guide-shoe spacing h", "Calculations.CarGuideForces.GuideShoeSpacingMm", c.CarGuideForces.GuideShoeSpacingMm, "mm");
            if (RopeTractionAvailable)
                RopeTractionInputRows(rows);
            d.Table(new[] { "Input", "Value" }, rows, TwoCol, new HashSet<int> { 0 });
            if (Plan != null && CarRail != null)
                AddFigure(d, () => CalculationFigures.ShaftPlan(Plan, CarRail, CounterweightRail, Door));
        }

        // Automatic car door: type, clear opening and (side opening) the stacking side. Manual doors add no row.
        void DoorInputRows(List<string[]> rows)
        {
            rows.Add(new[] { "Car door", DoorTypes.Display(Door.Type) });
            rows.Add(new[] { "Door clear opening", L(Door.OpeningMm) + " mm" });
            if (Door.Type == DoorTypes.Automatic2S)
                rows.Add(new[] { "Door stacking side", Door.StackingSide });
            // The offset appears only when the user has set it; the preset position is in the figure.
            if (DoorOffset != null && DoorOffset.Locked)
            {
                double shift = DoorOffset.RequestedShiftMm;
                rows.Add(new[] { "Doors Offset", L(Math.Abs(shift)) + " mm" + (Math.Abs(shift) < 0.05 ? "" : " " + DoorOffset.SideOf(shift, DoorTypes.Left)) });
            }
        }

        void BasicChassis(DocxDocument d, int n)
        {
            var r = Chassis;
            var g = r.SimplifiedGeometry;
            d.Heading(n + "  Machine-room bearing reactions P1–P4", 1);
            d.Paragraph(
                "P1 to P4 are the vertical reactions at the four bearing seats of the machine chassis in the machine room. The total "
                + "weight W of the suspended masses and the machine acts as one vertical load at the traction-sheave centre. W is shared "
                + "between the front and rear bearing rows by their lever arms, and within each row between the left and right bearings by "
                + "their lever arms.");
            d.Paragraph("Masses", "Heading2");
            AddMassTable(d, r);
            d.Paragraph("Load position and bearing positions", "Heading2");
            d.Paragraph(
                "Machine-room axes in mm: +X to the right, +Y from the entrance towards the rear, +Z above the machine-room slab.",
                "Small",
                null,
                true);
            AddPositionTable(d, r, g);
            AddBasicEquations(d, r, g);
            Reactions(d);
            // Text first, then the figures: a figure moves to the next page only when it does not fit.
            AddFigure(d, () => CalculationFigures.ChassisPlan(r, true));
            AddFigure(d, () => CalculationFigures.ChassisElevation(r));
        }

        private void AddMassTable(DocxDocument d, ChassisCalculationResult r)
        {
            var masses = r.MassCompositionKg.Where(m => !m.Key.EndsWith("(not included)") && m.Value > 0).ToList();
            var rows = masses.Select(m => new[] { MassName(m.Key), L(m.Value) + " kg" }).ToList();
            rows.Add(new[] { "Total Σm", L(r.TotalMassKg.Value) + " kg" });
            d.Table(new[] { "Mass", "Value" }, rows, TwoCol, null, NullFills(rows.Count, true));
            d.Equations(new[]
            {
                "W = Σm × g = ("
                    + String.Join(" + ", masses.Select(m => L(m.Value)))
                    + ") kg × 9.81 m/s² = "
                    + NK(r.TotalVerticalLoadN)
            });
        }

        private void AddPositionTable(DocxDocument d, ChassisCalculationResult r, SimplifiedDistances g)
        {
            var pos = new List<string[]> { new[]
            {
                "Load W",
                "Traction-sheave centre",
                L(g.LoadReference.X),
                L(g.LoadReference.Y),
                L(g.LoadReference.Z)
            } };
            foreach (var b in r.Reactions.OrderBy(b => b.Id))
                pos.Add(new[] { b.Id, Bearing(b.Id), L(b.PositionMm.X), L(b.PositionMm.Y), L(b.PositionMm.Z) });
            d.Table(
                new[] { "Point", "Location", "X (mm)", "Y (mm)", "Z (mm)" },
                pos,
                new[] { 1500, 3638, 1500, 1500, 1500 },
                new HashSet<int> { 0 });
        }

        private void AddBasicEquations(DocxDocument d, ChassisCalculationResult r, SimplifiedDistances g)
        {
            var x = r.Reactions.ToDictionary(b => b.Id, b => b.PositionMm);
            d.Paragraph("Equations", "Heading2");
            d.Equations(new[]
            {
                "L = y(rear row) − y(front row) = " + S(g.RearRowY) + " − " + S(g.FrontRowY) + " = " + L(g.LongitudinalSpanMm) + " mm",
                "a = y(W) − y(front row) = " + L(g.LoadFromFrontMm) + " mm      b = y(rear row) − y(W) = " + L(g.LoadToRearMm) + " mm",
                "F(front) = W × b / L = "
                    + N(r.TotalVerticalLoadN)
                    + " × "
                    + S(g.LoadToRearMm)
                    + " / "
                    + S(g.LongitudinalSpanMm)
                    + " = "
                    + LK(g.FrontLoadN),
                "F(rear) = W × a / L = "
                    + N(r.TotalVerticalLoadN)
                    + " × "
                    + S(g.LoadFromFrontMm)
                    + " / "
                    + S(g.LongitudinalSpanMm)
                    + " = "
                    + LK(g.RearLoadN)
            });
            d.Equations(new[]
            {
                "s(f) = x(P2) − x(P4) = " + S(x["P2"].X) + " − " + S(x["P4"].X) + " = " + L(g.FrontTransverseSpanMm) + " mm",
                "P2 = F(front) × (x(W) − x(P4)) / s(f) = "
                    + S(g.FrontLoadN)
                    + " × "
                    + S(g.FrontLoadFromLeftMm)
                    + " / "
                    + S(g.FrontTransverseSpanMm)
                    + " = "
                    + NK(R("P2")),
                "P4 = F(front) × (x(P2) − x(W)) / s(f) = "
                    + S(g.FrontLoadN)
                    + " × "
                    + S(g.FrontLoadToRightMm)
                    + " / "
                    + S(g.FrontTransverseSpanMm)
                    + " = "
                    + NK(R("P4"))
            });
            d.Equations(new[]
            {
                "s(r) = x(P1) − x(P3) = " + S(x["P1"].X) + " − " + S(x["P3"].X) + " = " + L(g.RearTransverseSpanMm) + " mm",
                "P1 = F(rear) × (x(W) − x(P3)) / s(r) = "
                    + S(g.RearLoadN)
                    + " × "
                    + S(g.RearLoadFromLeftMm)
                    + " / "
                    + S(g.RearTransverseSpanMm)
                    + " = "
                    + NK(R("P1")),
                "P3 = F(rear) × (x(P1) − x(W)) / s(r) = "
                    + S(g.RearLoadN)
                    + " × "
                    + S(g.RearLoadToRightMm)
                    + " / "
                    + S(g.RearTransverseSpanMm)
                    + " = "
                    + NK(R("P3"))
            });
        }

        static string MassName(string key)
        {
            if (key.StartsWith("Rated capacity"))
                return "Rated load Q";
            if (key.StartsWith("Empty car"))
                return "Car mass";
            if (key.StartsWith("Counterweight"))
                return "Counterweight mass";
            if (key.StartsWith("Machine"))
                return "Machine mass";
            if (key.StartsWith("Effective car-side"))
                return "Car-side additional mass";
            if (key.StartsWith("Effective CW-side"))
                return "Counterweight-side additional mass";
            if (key.StartsWith("Chassis"))
                return "Chassis assembly mass";
            if (key.StartsWith("Hanger"))
                return "Hanger assembly mass";
            return key;
        }

        double? R(string id)
        {
            return Chassis.Reactions.Single(v => v.Id == id).UpwardN;
        }

        void Reactions(DocxDocument d)
        {
            var r = Chassis;
            d.Paragraph("Results", "Heading2");
            var rows = r.Reactions.OrderBy(b => b.Id).Select(b => new[] { b.Id, Bearing(b.Id), N(b.UpwardN), KN(b.UpwardN), KG(b.UpwardN) }).ToList();
            double sum = r.Reactions.Sum(b => b.UpwardN);
            rows.Add(new[] { "ΣP", "", L(sum), KN(sum), KG(sum) });
            d.Table(
                new[] { "Bearing", "Location", "Reaction (N)", "Reaction (kN)", "Reaction (kg)" },
                rows,
                new[] { 1100, 2538, 2000, 2000, 2000 },
                new HashSet<int> { 0, 2 },
                NullFills(rows.Count, true));
            d.Paragraph("Positive values are upward (compressive) bearing reactions." + (r.Reactions.Any(b => b.UpwardN < 0)
                ? " A negative value indicates uplift at that bearing, which must be restrained."
                : ""), "Small");
        }

        void AdvancedChassis(DocxDocument d, int n)
        {
            var c = Project.Calculations;
            var r = Chassis;
            d.Heading(n + "  Machine-room bearing reactions P1–P4", 1);
            d.Paragraph(
                "P1 to P4 are the vertical reactions at the four bearing seats of the machine chassis in the machine room. They are "
                + "obtained from static equilibrium of the chassis under the static rated-load case with 1:1 suspension: the machine "
                + "weight at its centre of gravity, the rope forces at the traction sheave reduced to the traction pitch centre, and the "
                + "rope forces at the deflector transferred to the hanger attachment, each with its couple. The loads pass through the "
                + "selected upper longitudinal members, simply supported on the transverse members, to the bearing seats. Rope tensions "
                + "use g = 9.81 m/s² with equal tension in each rope.");
            d.Paragraph("Chassis model data", "Heading2");
            AddChassisModelTable(d, c);
            d.Paragraph("Loads on the chassis", "Heading2");
            d.Paragraph(
                "Positions in mm, forces in N and couples in N·mm on the machine-room axes (+X right, +Y entrance → rear, +Z above slab).",
                "Small",
                null,
                true);
            d.Table(
                new[] { "Load", "Position (mm)", "Force Fx, Fy, Fz (N)", "Fz (kg)", "Couple Mx, My, Mz (N·mm)" },
                r.Loads.Select(l => new[] { LoadName(l.Id), Pt(l.PositionMm), Pt(l.ForceN), LoadUnits.KgText(l.ForceN.Z), Pt(l.CoupleNmm) }).ToList(),
                new[] { 1938, 1800, 2000, 1000, 2900 },
                new HashSet<int> { 0 });
            if (r.Transfers.Count > 0)
            {
                d.Paragraph("Member transfers", "Heading2");
                d.Table(new[] { "Load", "Member", "Front (N)", "Front (kg)", "Rear (N)", "Rear (kg)" }, r.Transfers
                    .Select(t => new[] { LoadName(t.LoadId), Member(t.MemberId), N(t.FrontDownwardN), KG(t.FrontDownwardN), N(t.RearDownwardN), KG(t.RearDownwardN) })
                    .ToList(), new[] { 2238, 2000, 1350, 1350, 1350, 1350 }, new HashSet<int> { 0 });
                d.Paragraph("Positive member transfers act downwards on the supporting members; negative values act upwards.", "Small");
            }
            d.Paragraph("Bearing positions", "Heading2");
            d.Table(new[] { "Bearing", "Location", "X (mm)", "Y (mm)", "Z (mm)" }, r.Reactions
                .OrderBy(b => b.Id)
                .Select(b => new[] { b.Id, Bearing(b.Id), L(b.PositionMm.X), L(b.PositionMm.Y), L(b.PositionMm.Z) })
                .ToList(), new[] { 1500, 3638, 1500, 1500, 1500 }, new HashSet<int> { 0 });
            AddFigure(d, () => CalculationFigures.ChassisPlan(r, false));
            Reactions(d);
        }

        private void AddChassisModelTable(DocxDocument d, ChassisCalculationInputs c)
        {
            var rows = new List<string[]>();
            if (Finite(c.MachineCgOffsetXMm) && Finite(c.MachineCgOffsetYMm) && Finite(c.MachineCgOffsetZMm))
            {
                rows.Add(new[]
                {
                    "Machine centre of gravity from traction-sheave centre",
                    "ΔX "
                        + L(c.MachineCgOffsetXMm.Value)
                        + " mm · ΔY "
                        + L(c.MachineCgOffsetYMm.Value)
                        + " mm · ΔZ "
                        + L(c.MachineCgOffsetZMm.Value)
                        + " mm"
                });
            }
            if (!String.IsNullOrWhiteSpace(c.MachineCgSource))
                rows.Add(new[] { "Machine centre of gravity basis", c.MachineCgSource.Trim() });
            if (!String.IsNullOrWhiteSpace(c.LeftCarryingMemberId))
            {
                rows.Add(
                                    new[]
                                    {
                                        "Carrying members (left / right)",
                                        Member(c.LeftCarryingMemberId) + " / " + Member(c.RightCarryingMemberId)
                                    });
            }
            rows.Add(new[]
            {
                "Load transfer",
                "Upper members simply supported on front and rear transverse members, rigid spreader to the bearing seats"
            });
            if (!String.IsNullOrWhiteSpace(c.TransferAssumptionSource))
                rows.Add(new[] { "Load transfer basis", c.TransferAssumptionSource.Trim() });
            if (Finite(c.HangerAttachmentOffsetXMm) && Finite(c.HangerAttachmentOffsetYMm))
            {
                rows.Add(new[]
                {
                    "Hanger attachment from deflector centre",
                    "ΔX " + L(c.HangerAttachmentOffsetXMm.Value) + " mm · ΔY " + L(c.HangerAttachmentOffsetYMm.Value) + " mm"
                });
            }
            if (!String.IsNullOrWhiteSpace(c.HangerAttachmentSource))
                rows.Add(new[] { "Hanger attachment basis", c.HangerAttachmentSource.Trim() });
            if (c.IncludeChassisSelfWeight == true && Positive(c.ChassisMassKg))
                rows.Add(new[] { "Chassis assembly mass", L(c.ChassisMassKg.Value) + " kg" });
            if (c.IncludeHangerAssemblyWeight == true && Positive(c.HangerAssemblyMassKg))
                rows.Add(new[] { "Hanger assembly mass", L(c.HangerAssemblyMassKg.Value) + " kg" });
            d.Table(new[] { "Item", "Value" }, rows, TwoCol, new HashSet<int> { 0 });
        }

        void RopeTensions(DocxDocument d, int n)
        {
            var p = Project;
            var c = p.Calculations;
            var r = Chassis;
            d.Heading(n + "  Suspension rope tensions", 1);
            d.Paragraph(
                "Static rope tensions on the car side and the counterweight side under rated load, with r the roping ratio and n the "
                + "number of ropes.",
                null,
                null,
                true);
            var car = new List<double> { p.Elevator.CapacityKg.Value, c.CarMassKg.Value };
            if (Positive(c.CarSideAdditionalSuspendedMassKg))
                car.Add(c.CarSideAdditionalSuspendedMassKg.Value);
            var cw = new List<double> { c.CounterweightMassKg.Value };
            if (Positive(c.CounterweightSideAdditionalSuspendedMassKg))
                cw.Add(c.CounterweightSideAdditionalSuspendedMassKg.Value);
            string carTerms = Positive(c.CarSideAdditionalSuspendedMassKg) ? "(Q + M(car) + M(car add.))" : "(Q + M(car))", cwTerms =
                Positive(c.CounterweightSideAdditionalSuspendedMassKg) ? "(M(cwt) + M(cwt add.))" : "M(cwt)";
            Func<List<double>, string> sum = v => v.Count > 1 ? "(" + String.Join(" + ", v.Select(L)) + ")" : L(v[0]);
            var lines = new List<string>{
    "T(car) = "+carTerms+" × g / r = "+sum(car)+" kg × 9.81 m/s² / 1 = "+NK(r.CarBundleTensionN),
    "T(cwt) = "+cwTerms+" × g / r = "+sum(cw)+" kg × 9.81 m/s² / 1 = "+NK(r.CounterweightBundleTensionN)};
            bool perRope = r.CarPerRopeTensionN.HasValue
                && r.CounterweightPerRopeTensionN.HasValue
                && p.Suspension.RopeCount.HasValue
                && p.Suspension.RopeCount.Value > 0;
            if (perRope)
            {
                int count = p.Suspension.RopeCount.Value;
                lines.Add("T(car) / n = "
                    + N(r.CarBundleTensionN)
                    + " / "
                    + count
                    + " = "
                    + NK(r.CarPerRopeTensionN)
                    + "      T(cwt) / n = "
                    + N(r.CounterweightBundleTensionN)
                    + " / "
                    + count
                    + " = "
                    + NK(r.CounterweightPerRopeTensionN));
            }
            d.Equations(true, lines);
            if (perRope)
            {
                d.Table(new[] { "Rope tension", "All ropes (N)", "All ropes (kN)", "All ropes (kg)", "Per rope (N)", "Per rope (kg)" }, new List<string[]>{
    new[]{"Car side",N(r.CarBundleTensionN),KN(r.CarBundleTensionN),KG(r.CarBundleTensionN),N(r.CarPerRopeTensionN),KG(r.CarPerRopeTensionN)},
    new[]{"Counterweight side",N(r.CounterweightBundleTensionN),KN(r.CounterweightBundleTensionN),KG(r.CounterweightBundleTensionN),N(r.CounterweightPerRopeTensionN),KG(r.CounterweightPerRopeTensionN)}},
                        new[] { 2038, 1600, 1500, 1500, 1500, 1500 },
                        new HashSet<int> { 0 });
            }
            else
            {
                d.Table(new[] { "Rope tension", "All ropes (N)", "All ropes (kN)", "All ropes (kg)" }, new List<string[]>{
    new[]{"Car side",N(r.CarBundleTensionN),KN(r.CarBundleTensionN),KG(r.CarBundleTensionN)},
    new[]{"Counterweight side",N(r.CounterweightBundleTensionN),KN(r.CounterweightBundleTensionN),KG(r.CounterweightBundleTensionN)}},
                        new[] { 2638, 2333, 2333, 2334 },
                        new HashSet<int> { 0 });
            }
            // F7 needs the shared plan geometry (TZ selects the deflector); without it the figure is left out.
            if (Plan != null)
            {
                AddFigure(d, () => CalculationFigures.Suspension(
                                    r,
                                    p.Suspension.RopeCount,
                                    p.Suspension.RopingRatio,
                                    p.Machine == null ? null : p.Machine.TractionSheavePitchDiameterMm,
                                    MachineRoomRules.SelectDeflector(Plan.TZ).IsRequired));
            }
        }

        // Event-mass terms rebuilt from the inputs; used only when they reproduce the engine's mass exactly.
        List<KeyValuePair<string, double>> EventTerms(SafetyPitLoad load)
        {
            var c = Project.Calculations;
            var s = c.SafetyPit ?? new SafetyPitInputs();
            var terms = new List<KeyValuePair<string, double>>();
            bool car = load.Id != "P7";
            double? extra = car ? c.CarSideAdditionalSuspendedMassKg : c.CounterweightSideAdditionalSuspendedMassKg;
            string allocation = car ? s.CarAdditionalMassAllocation : s.CounterweightAdditionalMassAllocation;
            double? percent = car ? s.CarEventAdditionalMassPercent : s.CounterweightEventAdditionalMassPercent;
            if (car)
            {
                terms.Add(new KeyValuePair<string, double>("Q", Project.Elevator.CapacityKg.Value));
                terms.Add(new KeyValuePair<string, double>("M(car)", c.CarMassKg.Value));
            }
            else
                terms.Add(new KeyValuePair<string, double>("M(cwt)", c.CounterweightMassKg.Value));
            if (Positive(extra))
            {
                double share = allocation == "AllParticipates"
                    ? 1
                    : allocation == "FractionParticipates" && percent.HasValue ? percent.Value / 100 : 0;
                if (share > 0)
                {
                    terms.Add(new KeyValuePair<string, double>(share == 1
                                            ? (car ? "M(car add.)" : "M(cwt add.)")
                                            : L(share * 100) + " % × " + (car ? "M(car add.)" : "M(cwt add.)"), extra.Value * share));
                }
            }
            return Math.Abs(terms.Sum(t => t.Value) - load.ParticipatingMassKg.Value) < 1e-6 ? terms : null;
        }

        void SafetyPitSection(DocxDocument d, int n)
        {
            var s = Project.Calculations.SafetyPit;
            d.Heading(n + "  Safety-gear and buffer loads P5–P7", 1);
            AddSafetyPitItems(d, s);
            foreach (var load in Loads.Where(Available))
            {
                AddLoadSection(d, load);
            }
            if (Available(SafetyPit.P6) || Available(SafetyPit.P7))
            {
                AddFigure(d, () => CalculationFigures.PitBuffers(
                                    Available(SafetyPit.P6) ? SafetyPit.P6 : null,
                                    Available(SafetyPit.P7) ? SafetyPit.P7 : null,
                                    Project.Shaft == null ? null : Project.Shaft.PitDepthMm));
            }
        }

        private void AddSafetyPitItems(DocxDocument d, SafetyPitInputs s)
        {
            var rows = new List<string[]>();
            if (Available(SafetyPit.P5))
            {
                rows.Add(new[] { "Car safety gear", "Progressive" });
                rows.Add(new[] { "Car guide rails", s.CarGuideCount.Value.ToString(Inv) });
            }
            if (Available(SafetyPit.P6))
                rows.Add(new[] { "Car buffers", s.CarBufferCount.Value.ToString(Inv) });
            if (Available(SafetyPit.P7))
                rows.Add(new[] { "Counterweight buffers", s.CounterweightBufferCount.Value.ToString(Inv) });
            d.Table(new[] { "Item", "Value" }, rows, TwoCol, new HashSet<int> { 0 });
        }

        private void AddLoadSection(DocxDocument d, SafetyPitLoad load)
        {
            bool p5 = load.Id == "P5", p6 = load.Id == "P6";
            double m = load.ParticipatingMassKg.Value;
            var terms = EventTerms(load);
            string massSymbol = load.Id == "P7" ? "m(cwt)" : "m(car)";
            d.Paragraph(load.Id + " — " + (p5
                ? "Safety-gear load per car guide rail"
                : p6 ? "Car-buffer reaction" : "Counterweight-buffer reaction"), "Heading2");
            d.Paragraph(p5
                ? "P5 is the vertical load transmitted to each car guide rail when the progressive safety gear stops the car with "
                    + "rated load."
             : p6
                 ? "P6 is the vertical reaction on the pit floor under the car buffers when the car with rated load strikes the "
                     + "buffers. The total acts on all car buffers together; the per-buffer value acts on each buffer."
             : "P7 is the vertical reaction on the pit floor under the counterweight buffers when the counterweight strikes the "
                 + "buffers. The total acts on all counterweight buffers together; the per-buffer value acts on each buffer.",
                 null,
                 null,
                 true);
            AddLoadEquations(d, load, p5, m, terms, massSymbol);
            AddLoadTable(d, load, p5, m, massSymbol);
            if (p5 && CarRail != null)
                AddFigure(d, () => CalculationFigures.SafetyGear(load, CarRail));
        }

        private void AddLoadEquations(
            DocxDocument d,
            SafetyPitLoad load,
            bool p5,
            double m,
            List<KeyValuePair<string, double>> terms,
            string massSymbol)
        {
            var lines = new List<string>();
            if (terms != null && terms.Count > 1)
            {
                lines.Add(massSymbol
                                        + " = "
                                        + String.Join(" + ", terms.Select(t => t.Key))
                                        + " = "
                                        + String.Join(" + ", terms.Select(t => L(t.Value)))
                                        + " = "
                                        + L(m)
                                        + " kg");
            }
            else
                lines.Add(massSymbol + " = " + L(m) + " kg");
            if (p5)
            {
                lines.Add("P5 = "
                                        + massSymbol
                                        + " × 10 N/kg = "
                                        + L(m)
                                        + " kg × 10 N/kg = "
                                        + NK(load.PerComponentLoadN)
                                        + " per car guide rail");
            }
            else
            {
                lines.Add(load.Id
                    + "(total) = "
                    + massSymbol
                    + " × max(40, 4 × 9.81) N/kg = "
                    + L(m)
                    + " kg × "
                    + L(SafetyPitCalculationEngine.BufferCoefficientNPerKg)
                    + " N/kg = "
                    + NK(load.GroupLoadN));
                lines.Add(load.Id
                    + "(per buffer) = "
                    + load.Id
                    + "(total) / n = "
                    + N(load.GroupLoadN)
                    + " / "
                    + load.ComponentCount
                    + " = "
                    + NK(load.PerComponentLoadN));
            }
            d.Equations(true, lines);
        }

        private void AddLoadTable(DocxDocument d, SafetyPitLoad load, bool p5, double m, string massSymbol)
        {
            var table = new List<string[]> { new[] { "Event mass " + massSymbol, L(m) + " kg" } };
            if (p5)
                table.Add(new[] { "P5 per car guide rail", N(load.PerComponentLoadN) + " N (" + KN(load.PerComponentLoadN) + " kN; " + KG(load.PerComponentLoadN) + " kg)" });
            else
            {
                table.Add(new[] { "Number of buffers n", load.ComponentCount.Value.ToString(Inv) });
                table.Add(new[] { load.Id + " per buffer", N(load.PerComponentLoadN) + " N (" + KN(load.PerComponentLoadN) + " kN; " + KG(load.PerComponentLoadN) + " kg)" });
                table.Add(new[] { load.Id + " total", N(load.GroupLoadN) + " N (" + KN(load.GroupLoadN) + " kN; " + KG(load.GroupLoadN) + " kg)" });
            }
            d.Table(new[] { "Quantity", "Value" }, table, TwoCol, new HashSet<int> { 0, 1 });
        }

        void ForcesSection(DocxDocument d, int n)
        {
            var f = CarGuideForces;
            d.Heading(n + "  Car guide-rail forces FF1 / FF2", 1);
            d.Mixed(new[]
            {
                Plain("FF1 and FF2 are the horizontal forces on the car guide rails from rated load distributed irregularly in the car. "),
                Bold("FF1"),
                Plain(" acts along the car-rail line, in the car-width direction. "),
                Bold("FF2"),
                Plain(" acts perpendicular to the car-rail line, in the car-depth direction. Both are forces per guide rail at one "
                    + "guide-shoe level.")
            });
            d.Mixed(new[]
            {
                Bold("h"),
                Plain(
                    " is the vertical centre-to-centre distance between the upper and lower car guide shoes. W and D are the internal car "
                    + "width and depth.")
            },
                    null,
                    true);
            string q =
                L(f.CapacityKg.Value), w = L(f.InternalWidthMm.Value), dd = L(f.InternalDepthMm.Value), h = L(f.GuideShoeSpacingMm.Value);
            d.Table(new[] { "Input", "Value" }, new List<string[]>{
    new[]{"Rated load Q",q+" kg"},
        new[]{"Internal car width W",w+" mm"},
        new[]{"Internal car depth D",dd+" mm"},
        new[]{"Guide-shoe spacing h",h+" mm"}}, TwoCol, new HashSet<int> { 0 });
            d.Paragraph("Equations", "Heading2");
            d.Equations(true, new[]
            {
                "FF1 = Q × 10 N/kg × W / (4 h) = " + q + " × 10 × " + w + " / (4 × " + h + ") = " + NK(f.FF1BaseN),
                "FF1(design) = 2 × FF1 = 2 × " + N(f.FF1BaseN) + " = " + NK(f.FF1DesignN)
            });
            d.Equations(true, new[]
            {
                "FF2 = Q × 10 N/kg × D / (8 h) = " + q + " × 10 × " + dd + " / (8 × " + h + ") = " + NK(f.FF2BaseN),
                "FF2(design) = 2 × FF2 = 2 × " + N(f.FF2BaseN) + " = " + NK(f.FF2DesignN)
            });
            d.Paragraph("Results", "Heading2");
            d.Table(new[] { "Force", "Direction", "FF (N)", "FF design (N)", "FF design (kN)", "FF design (kg)" }, new List<string[]>{
    new[]{"FF1","Along the car-rail line (car width)",N(f.FF1BaseN),N(f.FF1DesignN),KN(f.FF1DesignN),KG(f.FF1DesignN)},
    new[]{"FF2","Perpendicular to the car-rail line (car depth)",N(f.FF2BaseN),N(f.FF2DesignN),KN(f.FF2DesignN),KG(f.FF2DesignN)}},
        new[] { 700, 3638, 1100, 1400, 1400, 1400 },
        new HashSet<int> { 0, 3 });
            d.Paragraph("Values per guide rail at one guide-shoe level.", "Small");
            // Text first, then the figures: a figure moves to the next page only when it does not fit.
            if (Plan != null && CarRail != null)
                AddFigure(d, () => CalculationFigures.GuideForcePlan(Plan, f, CarRail));
            AddFigure(d, () => CalculationFigures.GuideForceElevation(f));
            if (CarRail != null)
                AddFigure(d, () => CalculationFigures.RailSection(CarRail, f));
        }
    }
}
