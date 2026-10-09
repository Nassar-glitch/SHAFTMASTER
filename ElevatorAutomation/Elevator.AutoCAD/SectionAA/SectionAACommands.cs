using System;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD.Common;
using Elevator.AutoCAD.SectionBB;
using Elevator.AutoCAD.Vertical;
using Elevator.Core.Geometry;
using Elevator.Core.Shaft;

namespace Elevator.AutoCAD.SectionAA
{
    /// <summary>The AutoCAD section A-A commands.</summary>
    public sealed class SectionAACommands
    {
        static void Check(PromptStatus s)
        {
            if (s != PromptStatus.OK && s != PromptStatus.None)
                throw new OperationCanceledException("Cancelled; drawing retained.");
        }

        static double Num(Editor e, string q, double? v)
        {
            var r = e.GetDouble(new PromptDoubleOptions("\n" + q + ": ")
            {
                AllowNone = v.HasValue,
                AllowNegative = false,
                AllowZero = false,
                UseDefaultValue = v.HasValue,
                DefaultValue = v ?? 0
            });
            Check(r.Status);
            return r.Status == PromptStatus.OK ? r.Value : v.Value;
        }

        static string Text(Editor e, string q, string v)
        {
            var r = e.GetString(new PromptStringOptions("\n" + q + (v == null ? "" : " <" + v + ">") + ": ") { AllowSpaces = true });
            Check(r.Status);
            return String.IsNullOrWhiteSpace(r.StringResult) ? v : r.StringResult.Trim();
        }

        static string Choice(Editor e, string q, string[] a, string v)
        {
            var o = new PromptKeywordOptions("\n" + q + ": ") { AllowNone = true };
            foreach (var s in a)
                o.Keywords.Add(s);
            o.Keywords.Default = v;
            var r = e.GetKeywords(o);
            Check(r.Status);
            return r.Status == PromptStatus.None ? v : r.StringResult;
        }

        static void Car(Editor e, SectionAAProject d)
        {
            var available = d.Floors.Where(f => f.Served).ToArray();
            var old = available.FirstOrDefault(f => f.FloorId == d.DisplayFloorId)
                ?? available.FirstOrDefault(f => f.Name == "G") ?? available.First();
            var name = Text(e, "Displayed car landing [" + String.Join(" / ", available.Select(f => f.Name)) + "]", old.Name);
            var f1 = available.SingleOrDefault(f => f.Name == name);
            if (f1 == null)
                throw new ArgumentException("Choose a served floor.");
            d.DisplayFloorId = f1.FloorId;
        }

        /// <summary>Prompts for the A-A data.</summary>
        public static SectionAAProject Edit(Document doc, SectionAAProject current)
        {
            var d = current == null ? new SectionAAProject() : Elevator.Core
                .Serialization
                .CoreJson
                .CreateWithDefaultLimit()
                .Deserialize<SectionAAProject>(SectionAADrawing.Json(current));
            var e = doc.Editor;
            string part = current == null
                ? "Floors"
                : Choice(e, "Edit A-A", new[] { "Floors", "Insert", "Remove", "Rename", "NoStop", "PitOH", "Car", "Brackets" }, "PitOH");
            if (part == "Floors")
            {
                EditFloorStack(d, e);
            }
            if (part == "Insert")
            {
                InsertFloor(d, e);
            }
            if (part == "Remove")
            {
                RemoveFloor(d, e);
            }
            if (part == "Rename")
            {
                RenameFloor(d, e);
            }
            if (part == "Floors" || part == "NoStop")
            {
                EditNoStopFloors(d, e);
            }
            if (current == null || part == "PitOH")
            {
                d.Pit = Num(e, "Pit depth mm", d.Pit);
                d.OH = Num(e, "Overhead mm", d.OH);
            }
            if (current == null || part == "Brackets")
            {
                string s = Choice(
                    e,
                    "Maximum bracket station spacing (project input, calculation pending)",
                    new[] { "3000", "2000", "1500", "1000", "Custom" },
                    "1500");
                d.BracketMaximum = s == "Custom" ? Num(e, "Custom maximum mm", null) : Double.Parse(s);
            }
            if (current == null || new[] { "Floors", "Insert", "Remove", "NoStop", "Car" }.Contains(part))
                Car(e, d);
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                SectionSnapshot bb;
                var s = VerticalExperimentDrawing.ReadSource(doc.Database, tr, out bb);
                SectionAACalculator.Calculate(d, s.Section.Reference.SourceSlabThickness);
            }
            return d;
        }

        private static void EditFloorStack(SectionAAProject d, Editor e)
        {
            var rr = e.GetInteger(new PromptIntegerOptions(
                                "\nNumber of normal physical floors (R added automatically): ")
            { LowerLimit = 2, UpperLimit = 99, AllowNone = false });
            Check(rr.Status);
            int n = rr.Value;
            string naming = Choice(e, "Naming convention", new[] { "BG", "Numeric" }, "BG");
            string first = Text(e, "First level", naming == "BG" ? "G" : "0");
            string mode = Choice(e, "FFL intervals", new[] { "Common", "Individual" }, "Common");
            double[] h = new double[n - 1];
            if (mode == "Common")
            {
                double height = Num(e, "Common floor height mm", null);
                for (int i = 0; i < h.Length; i++)
                    h[i] = height;
            }
            else
            {
                for (int i = 0; i < h.Length; i++)
                    h[i] = Num(e, "Interval " + (i + 1) + " height mm", null);
            }
            d.Floors = FloorStackGenerator.Create(n + 1, naming, first, h, d.Floors);
            foreach (var f in d.Floors)
                f.Served = !f.IsRoof;
        }

        private static void InsertFloor(SectionAAProject d, Editor e)
        {
            string before = Text(e, "Insert before normal floor name", null);
            int i = d.Floors.FindIndex(f => f.Name == before && !f.IsRoof);
            if (i <= 0)
                throw new ArgumentException("Choose a floor above lowest modeled FFL.");
            double h = Num(e, "Added floor interval mm", null);
            string name = Text(e, "New unique floor name", null);
            double z = d.Floors[i].FflZ;
            for (int j = i; j < d.Floors.Count - 1; j++)
                d.Floors[j].FflZ += h;
            d.Floors.Insert(i, new VerticalFloor { FloorId = Guid.NewGuid().ToString("N"), Name = name, Served = true, FflZ = z });
        }

        private static void RemoveFloor(SectionAAProject d, Editor e)
        {
            string name = Text(e, "Remove normal floor name and its following interval", null);
            int i = d.Floors.FindIndex(f => f.Name == name && !f.IsRoof);
            if (i <= 0 || i >= d.Floors.Count - 2)
                throw new ArgumentException("Remove an intermediate physical floor.");
            double delta = d.Floors[i + 1].FflZ - d.Floors[i].FflZ;
            d.Floors.RemoveAt(i);
            for (int j = i; j < d.Floors.Count - 1; j++)
                d.Floors[j].FflZ -= delta;
        }

        private static void RenameFloor(SectionAAProject d, Editor e)
        {
            string name = Text(e, "Existing floor name", null);
            var f = d.Floors.Single(v => v.Name == name && !v.IsRoof);
            f.Name = Text(e, "New unique display name (FloorId retained)", null);
        }

        private static void EditNoStopFloors(SectionAAProject d, Editor e)
        {
            foreach (var f in d.Floors)
                f.Served = !f.IsRoof;
            if (Choice(e, "ARE THERE ANY NO STOP FLOORS?", new[] { "No", "Yes" }, "No") == "Yes")
            {
                string names = Text(e, "NO STOP names separated by spaces", null);
                foreach (var name in names.Split(
                    new[] { ' ', ',', ';' },
                    StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = d.Floors.Single(v => v.Name == name && !v.IsRoof);
                    f.Served = false;
                }
            }
        }

        /// <summary>Command entry: places the A-A section.</summary>
        [CommandMethod("ELEVATORSECTIONAA")]
        public void Install()
        {
            Run(true, false);
        }

        /// <summary>Command entry: updates the A-A section.</summary>
        [CommandMethod("ELEVATORSECTIONAAUPDATE")]
        public void Update()
        {
            Run(false, false);
        }

        /// <summary>Command entry: edits the A-A data.</summary>
        [CommandMethod("ELEVATORSECTIONAADATA")]
        public void Data()
        {
            Run(false, true);
        }

        static void Run(bool install, bool edit)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                SectionAAProject d;
                bool exists;
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    exists =
                SectionAADrawing.Find(doc.Database, tr).Count > 0;
                    d = ProjectDataStorage.Read<SectionAAProject>(
                doc.Database,
                tr,
                SectionAADrawing.DataKey);
                }
                if (install
                    && exists)
                    throw new InvalidOperationException("A-A already installed. Use ELEVATORSECTIONAAUPDATE.");
                if (!install
                && !exists)
                    throw new InvalidOperationException("Place A-A with ELEVATORSECTIONAA first.");
                if (d == null
                    || edit)
                    d = Edit(doc, d);
                Point3d? pt = null;
                if (install)
                {
                    var r = doc.Editor.GetPoint(
                    "\nPlace A-A entrance / lowest FFL datum in empty space: ");
                    Check(r.Status);
                    pt =
                        r.Value.TransformBy(doc.Editor.CurrentUserCoordinateSystem);
                    SectionAADrawing.ImportLibrary(
                doc.Database);
                }
                var a = SectionAADrawing.Rebuild(doc.Database, d, pt);
                doc.Editor.Regen();
                if (install)
                {
                    try
                    {
                        doc.Editor.Command(
                    "_.ZOOM",
                    "_E");
                    }
                    catch { /* zoom is a view convenience; geometry is already committed */ }
                }
                Directory.CreateDirectory(
                        SectionAADrawing.Output);
                Configuration.WriteJson(
                    Path.Combine(SectionAADrawing.Output, "last-command-audit.json"),
                    a);
                doc.Editor.WriteMessage("\nA-A regenerated: "
                        + SectionAADrawing.Json(a)
                        + "\nA-A insertion datum: "
                        + (pt.HasValue ? pt.Value.ToString() : "stored frame")
                        + "\nIf it is not visible, run ZOOM EXTENTS (Z E). Save the project DWG normally. UPDATEALL does not update "
                        + "A-A.");
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage(
                            "\nA-A: " + ex.Message);
            }
        }

        /// <summary>Command entry: audits the A-A section.</summary>
        [CommandMethod("ELEVATORSECTIONAAAUDIT")]
        public void Audit()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    doc.Editor.WriteMessage(
                                    "\nREAD-ONLY A-A AUDIT: " + SectionAADrawing.Json(
                                    SectionAADrawing.Audit(doc.Database, tr)));
                }
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage(
                    "\nA-A audit FAILED: " + ex.Message);
            }
        }
    }
}
