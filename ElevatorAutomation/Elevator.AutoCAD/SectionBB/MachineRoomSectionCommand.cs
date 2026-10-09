using System;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD.Common;
using Elevator.AutoCAD.MachineRoom;
using Elevator.AutoCAD.Plan;
using Elevator.Core.MachineRoom;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Core.Suspension;

namespace Elevator.AutoCAD.SectionBB
{
    /// <summary>The AutoCAD section B-B commands.</summary>
    public sealed class MachineRoomSectionCommand
    {
        /// <summary>Prompts for the B-B data.</summary>
        public static bool EditData(Document doc)
        {
            ProjectInput input;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
                input = ProjectDataStorage.ReadProject(doc.Database, tr);
            if (input == null)
                throw new InvalidOperationException("Run ELEVATORPROJECT first.");
            var capacity = doc.Editor.GetDouble(new PromptDoubleOptions("\nProject capacity (kg): ")
            {
                AllowNegative = false,
                AllowZero = false,
                AllowNone = input.Capacity.HasValue,
                UseDefaultValue = input.Capacity.HasValue,
                DefaultValue = input.Capacity ?? 0
            });
            if (capacity.Status != PromptStatus.OK && capacity.Status != PromptStatus.None)
                return false;
            if (capacity.Status == PromptStatus.OK)
                input.Capacity = capacity.Value;
            var diameter = doc.Editor.GetDouble(new PromptDoubleOptions(
                "\nSuspension rope diameter [6.5/8/10/11/12] (Enter retains unknown/current): ")
            {
                AllowNone = true,
                AllowZero = false,
                AllowNegative = false
            });
            if (diameter.Status != PromptStatus.OK && diameter.Status != PromptStatus.None)
                return false;
            if (diameter.Status == PromptStatus.OK)
                input.SuspensionRopeDiameterMm = diameter.Value;
            var count = doc.Editor.GetInteger(new PromptIntegerOptions("\nNumber of suspension ropes: ")
            {
                LowerLimit = 4,
                UpperLimit = 7,
                AllowNone = true,
                UseDefaultValue = true,
                DefaultValue = input.NumberOfSuspensionRopes ?? 6
            });
            if (count.Status != PromptStatus.OK && count.Status != PromptStatus.None)
                return false;
            input.NumberOfSuspensionRopes = count.Status == PromptStatus.OK ? count.Value : (input.NumberOfSuspensionRopes ?? 6);
            var clearance = doc.Editor.GetDouble(new PromptDoubleOptions(
                "\nSpecified minimum deflector-to-slab clearance (Enter retains unknown/current): ")
            {
                AllowNone = true,
                AllowNegative = false,
                AllowZero = true
            });
            if (clearance.Status != PromptStatus.OK && clearance.Status != PromptStatus.None)
                return false;
            if (clearance.Status == PromptStatus.OK)
                input.MinimumDeflectorSlabClearanceMm = clearance.Value;
            SuspensionProjectRules.Validate(input.SuspensionRopeDiameterMm, input.NumberOfSuspensionRopes);
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                ProjectDataStorage.Write(doc.Database, tr, "Project", input);
                tr.Commit();
            }
            return true;
        }

        /// <summary>Command entry: edits the B-B data.</summary>
        [CommandMethod("ELEVATORREEVINGDATA", CommandFlags.Modal)]
        public void Data()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                EditData(doc);
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage("\nReeving data stopped: " + ex.Message);
            }
        }

        /// <summary>Command entry: places the B-B section.</summary>
        [CommandMethod("ELEVATORMRSECTION", CommandFlags.Modal)]
        public void Place()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                ShaftFrame frame;
                bool needsData;
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    if (MachineRoomSectionDrawing.IsPresent(doc.Database, tr))
                        throw new InvalidOperationException("Section already installed; use UPDATEALL.");
                    if (!MachineRoomDrawing.IsPresent(doc.Database, tr))
                        throw new InvalidOperationException("Place Machine Room first with ELEVATORMACHINEROOM.");
                    frame = DrawingReader.Read(doc.Database, tr).Frame;
                    var p = ProjectDataStorage.ReadProject(doc.Database, tr);
                    needsData = p == null || !p.Capacity.HasValue || !p.NumberOfSuspensionRopes.HasValue;
                }
                if (needsData && !EditData(doc))
                    return;
                var point = doc.Editor.GetPoint("\nPlace the B-B front-shaft/slab datum in an empty drawing area: ");
                if (point.Status != PromptStatus.OK)
                    return;
                MachineRoomSectionDrawing.Import(
                    doc.Database,
                    Path.Combine(Configuration.Root, "Templates", "MachineRoomSection_Master.dwg"),
                    point.Value.TransformBy(doc.Editor.CurrentUserCoordinateSystem),
                    frame.XAxis);
                doc.Editor.Regen();
                doc.Editor.WriteMessage("\nB-B installed with semantic dependencies. Run UPDATEALL.\n");
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage("\nSection placement stopped: " + ex.Message + "\n");
            }
        }

        /// <summary>Command entry: audits the B-B section.</summary>
        [CommandMethod("ELEVATORMRSECTIONAUDIT", CommandFlags.Modal)]
        public void Audit()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var input = ProjectDataStorage.ReadProject(doc.Database, tr);
                    var plan = DrawingReader.Read(doc.Database, tr);
                    var mr = MachineRoomDrawing.Read(doc.Database, tr);
                    var section = MachineRoomSectionDrawing.Read(doc.Database, tr);
                    var design = DesignRules.Calculate(
                        input.SW,
                        input.SD,
                        Configuration.Read<EquipmentProfile>(ElevatorProjectCommand.Family(input).ProfileFile),
                        plan.ElevatorParameters.CwDepth);
                    design.MachineRoom = MachineRoomRules.Calculate(input, design, mr.Extensions);
                    design.MachineRoomSection = MachineRoomSectionRules.Calculate(
                        input,
                        design,
                        section.Manifest.Reference,
                        (MachineRoomSectionGeometryAudit.SupportOffset(mr, "P2") + MachineRoomSectionGeometryAudit.SupportOffset(mr, "P4"))
                            / 2,
                        (MachineRoomSectionGeometryAudit.SupportOffset(mr, "P1") + MachineRoomSectionGeometryAudit.SupportOffset(mr, "P3"))
                            / 2,
                        mr.PitchProjection,
                        mr.Entities[MachineRoomDrawing.Pitch].Visible);
                    var result = MachineRoomSectionGeometryAudit.Run(section, design, mr);
                    doc.Editor.WriteMessage("\nSection audit: "
                        + result.Status
                        + "; "
                        + result.NativePointsChecked
                        + " native points checked. "
                        + String.Join(" ", result.GeometricObservations)
                        + "\n");
                }
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage("\nSection audit FAILED: " + ex.Message + "\n");
            }
        }
    }
}
