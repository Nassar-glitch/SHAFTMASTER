using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD.Plan;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Common
{
    /// <summary>The AutoCAD command that edits the project data stored in the drawing.</summary>
    public sealed class ElevatorProjectCommand
    {
        /// <summary>Command entry.</summary>
        [CommandMethod("ELEVATORPROJECT", CommandFlags.Modal)]
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                Edit(doc);
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage("\nELEVATORPROJECT stopped: " + ex.Message + "\n");
            }
        }

        /// <summary>The registered elevator family for a project input.</summary>
        public static FamilyDefinition Family(ProjectInput input)
        {
            return Configuration.Read<FamilyDefinition[]>("families.json").SingleOrDefault(f => f.ElevatorType == input.ElevatorType);
        }

        /// <summary>Prompts for the project data and stores it in the drawing; false when cancelled.</summary>
        public static bool Edit(Document doc)
        {
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                LegacyMigration.Ensure(doc.Database, tr);
                var s = DrawingReader.Read(doc.Database, tr);
                var p = ProjectDataStorage.ReadProject(doc.Database, tr);
                if (p == null)
                {
                    string templateId = ComponentBinding.ReadManifest(doc.Database, tr).TemplateId;
                    var family = Configuration.Read<FamilyDefinition[]>("families.json").SingleOrDefault(f => f.TemplateId == templateId);
                    if (family == null)
                        throw new InvalidOperationException("Template ID is absent from the family catalog: " + templateId);
                    p = new ProjectInput
                    {
                        ElevatorType = family.ElevatorType,
                        TemplateId = templateId,
                        SW = s.ElevatorParameters.SW,
                        SD = s.ElevatorParameters.SD
                    };
                }
                var opt = new PromptKeywordOptions("\nElevator type");
                // AutoCAD treats underscores specially in keyword syntax; keep command keywords alphabetic.
                foreach (var name in Enum.GetNames(typeof(ElevatorType)))
                    opt.Keywords.Add(name.Replace("_", ""));
                opt.Keywords.Default = p.ElevatorType.ToString().Replace("_", "");
                opt.AllowNone = true;
                var type = doc.Editor.GetKeywords(opt);
                if (type.Status != PromptStatus.OK && type.Status != PromptStatus.None)
                    return false;
                if (type.Status == PromptStatus.OK)
                {
                    p.ElevatorType = (ElevatorType)Enum.Parse(typeof(ElevatorType), Enum.GetNames(typeof(ElevatorType))
                                            .Single(n =>
                                                String.Equals(n.Replace("_", ""), type.StringResult, StringComparison.OrdinalIgnoreCase)));
                }
                CommonRules.ValidateProject(p, Family(p));
                double sw, sd;
                if (!Ask(doc.Editor, "Shaft Width SW (mm)", p.SW, out sw) || !Ask(doc.Editor, "Shaft Depth SD (mm)", p.SD, out sd))
                    return false;
                p.SW = sw;
                p.SD = sd;
                var profile = Configuration.Read<EquipmentProfile>(Family(p).ProfileFile);
                var target = DesignRules.Calculate(
                    p.SW,
                    p.SD,
                    profile,
                    s.ElevatorParameters.CwDepth,
                    AutomaticDoorPlan.InputFor(doc.Database, tr));
                DrawingUpdater.Preflight(s, new PlanGeometryAdapter(s, target));
                ProjectDataStorage.Write(doc.Database, tr, "Project", p);
                tr.Commit();
                doc.Editor.WriteMessage("\nProject data stored in drawing. Run UPDATEALL to apply and save a separate output DWG.\n");
                return true;
            }
        }

        private static bool Ask(Editor ed, string label, double value, out double result)
        {
            var r = ed.GetDouble(new PromptDoubleOptions(
                "\n" + label)
            { AllowNegative = false, AllowZero = false, AllowNone = true, UseDefaultValue = true, DefaultValue = value });
            result = r.Value;
            return r.Status == PromptStatus.OK;
        }
    }
}
