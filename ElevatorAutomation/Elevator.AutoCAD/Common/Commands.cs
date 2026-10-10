using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Elevator.AutoCAD.MachineRoom;
using Elevator.AutoCAD.Plan;
using Elevator.AutoCAD.SectionBB;
using Elevator.Core.MachineRoom;
using Elevator.Core.Presets;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Common
{
    /// <summary>Runs an update command over a set of view updaters and records its outputs for the tests.</summary>
    public static class Commands
    {
        public static ElevatorParameters LastElevatorParameters;
        public static string LastOutput;
        public static string LastError;
        public static Dictionary<string, double> LastDimensions;

        /// <summary>Updates the given views of the open drawing, validates the result and saves a separate DWG and JSON.</summary>
        public static void Run(string command, IEnumerable<IViewUpdater> views)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            LastError = null;
            LastOutput = null;
            LastElevatorParameters = null;
            LastDimensions = null;
            try
            {
                if (!doc.Database.TileMode)
                    throw new InvalidOperationException("Switch to the Model tab before updating.");
                if (views.Any(v => v.ViewId == "MachineRoomSection"))
                    MachineRoomSectionDrawing.DetachSourceDimensionAssociations(doc);
                ProjectInput input;
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                    input = ProjectDataStorage.ReadProject(doc.Database, tr);
                if (input == null && !ElevatorProjectCommand.Edit(doc))
                    return;
                string dir = Path.Combine(Configuration.Root, "output");
                Directory.CreateDirectory(dir);
                ElevatorParameters design;
                ViewUpdateResult plan = null;
                EquipmentProfile profile;
                UpdateViews(views, doc, out input, out design, ref plan, out profile);
                ed.Regen();
                ValidateUpdated(doc, design, plan);
                string output = SaveOutput(doc, input, dir, design, plan);
                ReportCompletion(command, ed, input, design, plan, profile, output);
            }
            catch (System.Exception ex)
            {
                LastError = ex.Message;
                ed.WriteMessage("\n" + command + " stopped: " + ex.Message + "\n");
            }
        }

        private static void UpdateViews(
            IEnumerable<IViewUpdater> views,
            Document doc,
            out ProjectInput input,
            out ElevatorParameters design,
            ref ViewUpdateResult plan,
            out EquipmentProfile profile)
        {
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                input = ProjectDataStorage.ReadProject(doc.Database, tr);
                var family = ElevatorProjectCommand.Family(input);
                CommonRules.ValidateProject(input, family);
                var manifest = ComponentBinding.ReadManifest(doc.Database, tr);
                if (manifest.TemplateId != input.TemplateId)
                    throw new InvalidOperationException("Project data and template identifiers differ.");
                profile = Configuration.Read<EquipmentProfile>(family.ProfileFile);
                var snapshot = DrawingReader.Read(doc.Database, tr);
                design = DesignRules.Calculate(
                    input.SW,
                    input.SD,
                    profile,
                    snapshot.ElevatorParameters.CwDepth,
                    AutomaticDoorPlan.InputFor(doc.Database, tr));
                if (views.Any(v => v.ViewId == "MachineRoom"))
                {
                    var mr = MachineRoomDrawing.Read(doc.Database, tr);
                    design.MachineRoom = MachineRoomRules.Calculate(input, design, mr.Extensions);
                    if (views.Any(v => v.ViewId == "MachineRoomSection"))
                    {
                        MachineRoomDependencies.Prepare(doc.Database, tr, mr);
                        var section = MachineRoomSectionDrawing.Read(doc.Database, tr);
                        double front = (MachineRoomSectionGeometryAudit.SupportOffset(mr, "P2")
                            + MachineRoomSectionGeometryAudit.SupportOffset(mr, "P4")) / 2;
                        double rear = (MachineRoomSectionGeometryAudit.SupportOffset(mr, "P1")
                            + MachineRoomSectionGeometryAudit.SupportOffset(mr, "P3")) / 2;
                        design.MachineRoomSection = MachineRoomSectionRules.Calculate(
                            input,
                            design,
                            section.Manifest.Reference,
                            front,
                            rear,
                            mr.PitchProjection,
                            mr.Entities[MachineRoomDrawing.Pitch].Visible);
                    }
                }
                foreach (var view in views)
                {
                    var result = view.Update(doc.Database, tr, design);
                    if (view.ViewId == "Plan")
                        plan = result;
                }
                if (plan == null)
                    throw new InvalidOperationException("No plan updater registered.");
                tr.Commit();
            }
        }

        private static void ValidateUpdated(Document doc, ElevatorParameters design, ViewUpdateResult plan)
        {
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var updated = DrawingReader.Read(doc.Database, tr);
                DrawingUpdater.Validate(updated, design, plan.Dimensions);
                PlanAnnotations.Validate(updated, plan.Annotations);
                if (design.MachineRoom != null)
                    MachineRoomUpdater.Validate(MachineRoomDrawing.Read(doc.Database, tr), design);
                if (design.MachineRoomSection != null)
                {
                    MachineRoomSectionGeometryAudit.Run(
                                                MachineRoomSectionDrawing.Read(doc.Database, tr),
                                                design,
                                                MachineRoomDrawing.Read(doc.Database, tr));
                }
            }
        }

        private static string SaveOutput(Document doc, ProjectInput input, string dir, ElevatorParameters design, ViewUpdateResult plan)
        {
            string name = "Elevator_SW"
                                + input.SW.ToString("0.###", CultureInfo.InvariantCulture)
                                + "_SD"
                                + input.SD.ToString("0.###", CultureInfo.InvariantCulture)
                                + "_"
                                + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")
                                + "_"
                                + Guid.NewGuid().ToString("N").Substring(0, 6)
                                + ".dwg";
            string output = Path.Combine(dir, name);
            doc.Database.SaveAs(output, DwgVersion.AC1032);
            LastElevatorParameters = design;
            LastDimensions = plan.Dimensions;
            LastOutput = output;
            return output;
        }

        private static void ReportCompletion(
            string command,
            Editor ed,
            ProjectInput input,
            ElevatorParameters design,
            ViewUpdateResult plan,
            EquipmentProfile profile,
            string output)
        {
            if (design.MachineRoom != null)
            {
                ed.WriteMessage("\nMACHINE ROOM: "
                                        + design.MachineRoom.Deflector.SelectionStatus
                                        + ". LayoutStatus="
                                        + design.MachineRoom.LayoutStatus
                                        + "; WrapAngleStatus="
                                        + design.MachineRoom.Reeving.WrapAngleStatus
                                        + "; CodeDiameterRatioStatus="
                                        + design.MachineRoom.CodeDiameterRatioStatus
                                        + ". Engineering review required. See embedded MachineRoomValidation, "
                                        + "MachineRoomEngineeringRules and the output "
                                        + "JSON.\n");
            }
            Configuration.WriteJson(
                Path.ChangeExtension(output, ".json"),
                new
                {
                    command = command,
                    profile = profile.Version,
                    input = input,
                    calculated = design,
                    clearances = plan.Clearances,
                    dimensionChecks = plan.Dimensions,
                    annotations = plan.Annotations,
                    coverage = plan.Coverage,
                    output = output
                });
            if (plan.Coverage.UnboundEntities > 0)
            {
                ed.WriteMessage("\nPlan coverage: "
                                        + plan.Coverage.UnboundEntities
                                        + " unbound model entities preserved but NOT automated. Run ELEVATORPLANAUDIT to review.\n");
            }
            ed.WriteMessage("\n"
                + command
                + " complete. Car: "
                + design.CarWidth
                + " x "
                + design.CarDepth
                + "; Car DBG: "
                + design.CarDbg
                + "; CW DBG: "
                + design.CwDbg
                + "; Door: "
                + design.DoorWidth
                + "; TZ: "
                + design.TZ
                + " mm.\nSaved separate DWG: "
                + output
                + "\n");
        }
    }
}
