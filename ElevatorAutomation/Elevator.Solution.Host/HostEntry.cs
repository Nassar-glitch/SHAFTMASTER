using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Elevator.AutoCAD.Common;
using Elevator.AutoCAD.MachineRoom;
using Elevator.AutoCAD.Plan;
using Elevator.AutoCAD.SectionAA;
using Elevator.AutoCAD.SectionBB;
using Elevator.AutoCAD.Sheet;
using Elevator.Core.Layouts;
using Elevator.Core.Geometry;
using Elevator.Core.Presets;
using Elevator.Core.Project;
using Elevator.Solution.Bridge;

namespace Elevator.Solution.Host
{
    /// <summary>The AutoCAD Core Console entry point that runs a desktop request.</summary>
    public sealed class HostEntry
    {
        /// <summary>Command SOLUTIONHOST: reads the request folder, generates or verifies the drawing and writes the response.</summary>
        [CommandMethod("SOLUTIONHOST")]
        public void Execute()
        {
            var editor = Application.DocumentManager.MdiActiveDocument.Editor;
            string job = editor.GetString(
                new Autodesk.AutoCAD.EditorInput.PromptStringOptions("\nRequest folder: ") { AllowSpaces = true }).StringResult;
            string mode = editor.GetString("\nOperation: ").StringResult;
            if (String.IsNullOrWhiteSpace(job))
                throw new InvalidOperationException("This host requires an explicit desktop request.");
            var request = Wire.Read<GenerationRequest>(Path.Combine(job, "request.json"));
            var response = new GenerationResponse { RequestId = request.RequestId, InputHash = Wire.HashText(request.ProjectJson) };
            try
            {
                BridgeContract.Require(request.ProtocolVersion);
                var project = Wire.Validate(request);
                PrepareRoofMetadata(project, request.Options.Views);
                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                if (mode == "generate")
                {
                    GenerateOutput(job, request, response, project, doc, db);
                }
                else if (mode == "verify")
                {
                    VerifyOutput(job, request, response, project, db);
                }
                else
                    throw new ArgumentException("Unsupported host operation.");
                response.Success = true;
            }
            catch (System.Exception e)
            {
                response.Error = e.ToString();
                response.Success = false;
            }
            Wire.Write(Path.Combine(job, mode == "verify" ? "verified.json" : "response.json"), response);
        }

        private void GenerateOutput(
            string job,
            GenerationRequest request,
            GenerationResponse response,
            ElevatorProject project,
            Document doc,
            Database db)
        {
            var generated = new ProjectDrawingService(doc).GenerateProjectDrawings(
                                    project,
                                    new GenerationOptions { Views = request.Options.Views, ArrangeViews = request.Options.ArrangeViews });
            response.DesignJson = Wire.Json(generated.Design);
            response.Validation = generated.Design.Validation;
            response.LayoutStatus = generated.Design.LayoutStatus.ToString();
            response.WrapAngleStatus = generated.Design.WrapAngleStatus.ToString();
            response.CodeDiameterRatioStatus = generated.Design.CodeDiameterRatioStatus.ToString();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                response.Context = ReadContext(db, tr, project, request.Options.Views);
                if ((request.Options.Views & ~DrawingViews.Plan) != 0)
                    MachineRoomGeometryAudit.Run(MachineRoomDrawing.Read(db, tr), generated.Design.Plan);
                if ((request.Options.Views & (DrawingViews.SectionAA | DrawingViews.SectionBB)) != 0)
                {
                    MachineRoomSectionGeometryAudit.Run(
                                                    MachineRoomSectionDrawing.Read(db, tr),
                                                    generated.Design.Plan,
                                                    MachineRoomDrawing.Read(db, tr));
                }
                if ((request.Options.Views & DrawingViews.SectionAA) != 0)
                    SectionAADrawing.Audit(db, tr);
                // Dependency views have served their calculation purpose. Only the user's selections are published.
                foreach (var pair in Groups(db, tr).Where(g => (request.Options.Views & g.Key) == 0))
                {
                    foreach (var e in pair.Value)
                    {
                        e.UpgradeOpen();
                        e.Erase();
                    }
                }
                tr.Commit();
            }
            // Store the exact versioned snapshot too, so reopen proves there was no transport mutation.
            using (var tr = db.TransactionManager.StartTransaction())
            {
                ProjectDataStorage.Write(db, tr, "SolutionInputSnapshot", request.ProjectJson);
                tr.Commit();
            }
            doc.Editor.Regen();
            Inspect(db, request, response);
            // L2b: the A0 sheet is only added on request, after the views are final; it adds a layout and untagged reference details.
            if (request.Sheet != null)
            {
                string template = String.IsNullOrWhiteSpace(request.Sheet.TemplateFile)
                    ? Path.Combine(Configuration.Root, "Templates", SheetLayout.TemplateFileName)
                    : request.Sheet.TemplateFile;
                response.Sheet = SheetAssembler.Assemble(db, request.Sheet, response.Bounds, template);
            }
            response.OutputPath = Path.Combine(job, "generated.dwg");
            db.SaveAs(response.OutputPath, DwgVersion.AC1032);
        }

        private void VerifyOutput(string job, GenerationRequest request, GenerationResponse response, ElevatorProject project, Database db)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                string exact = ProjectDataStorage.Read<string>(db, tr, "SolutionInputSnapshot");
                if (exact != request.ProjectJson)
                    throw new InvalidDataException("DWG input snapshot differs from desktop input.");
                var actual = UnifiedProjectStorage.Read(db, tr);
                File.WriteAllText(Path.Combine(job, "reopened-project.elevator.json"), ProjectJson.Serialize(actual));
                if (ProjectJson.Serialize(actual) != ProjectJson.Serialize(project))
                    throw new InvalidDataException("Unified project changed after save/reopen.");
                if (request.Options.Views == DrawingViews.All)
                    SectionAADrawing.Audit(db, tr);
            }
            Inspect(db, request, response);
        }

        /// <summary>Adds the roof identifiers that the A-A round trip stores, before generation and verification.</summary>
        public static void PrepareRoofMetadata(ElevatorProject project, DrawingViews views)
        {
            // A-A's legacy round-trip persists these identifiers. Materialize the same
            // adapter defaults before generation and verification, on the detached input.
            // Keep the original desktop snapshot and its hash exact; compare the DWG
            // against this deterministic prepared project without ignoring any fields.
            if ((views & DrawingViews.SectionAA) == 0)
                return;
            var roof = LegacyProjectAdapter.ToSectionAA(project).Floors.Last();
            if (!project.Extensions.ContainsKey("legacy.roofFloorId"))
                project.Extensions["legacy.roofFloorId"] = roof.FloorId;
            if (!project.Extensions.ContainsKey("legacy.roofName"))
                project.Extensions["legacy.roofName"] = roof.Name;
        }

        /// <summary>Reads the design context from the generated drawing.</summary>
        public static DesignContext ReadContext(Database db, Transaction tr, ElevatorProject project, DrawingViews views)
        {
            var plan = DrawingReader.Read(db, tr);
            var c = new DesignContext
            {
                Equipment = Configuration.Read<EquipmentProfile>(ElevatorProjectCommand.Family(
                LegacyProjectAdapter.ToLegacy(project)).ProfileFile),
                CounterweightDepthMm = plan.ElevatorParameters.CwDepth,
                Doors = Configuration.DoorCatalog()
            };
            if ((views & ~DrawingViews.Plan) != 0)
            {
                var mr = MachineRoomDrawing.Read(db, tr);
                c.RoomExtensions = mr.Extensions;
                if ((views & (DrawingViews.SectionAA | DrawingViews.SectionBB)) != 0)
                {
                    c.SectionReference = MachineRoomSectionDrawing.Read(db, tr).Manifest.Reference;
                    c.FrontSupportDepthMm = (MachineRoomSectionGeometryAudit.SupportOffset(mr, "P2")
                        + MachineRoomSectionGeometryAudit.SupportOffset(mr, "P4")) / 2;
                    c.RearSupportOffsetMm = (MachineRoomSectionGeometryAudit.SupportOffset(mr, "P1")
                        + MachineRoomSectionGeometryAudit.SupportOffset(mr, "P3")) / 2;
                    c.RetainedDeflectorPitchMm = mr.PitchProjection;
                    c.RetainedDeflectorVisible = mr.Entities[MachineRoomDrawing.Pitch].Visible;
                }
            }
            return c;
        }

        /// <summary>The model entities grouped by drawing view.</summary>
        public static Dictionary<DrawingViews, List<Entity>> Groups(Database db, Transaction tr)
        {
            var groups = new Dictionary<DrawingViews, List<Entity>>();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId id in (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead))
            {
                var entity = (Entity)tr.GetObject(id, OpenMode.ForRead);
                var tag = SectionAADrawing.Tag(entity);
                if (tag == null || tag.Length == 0)
                    continue;
                DrawingViews v;
                if (tag[0].StartsWith("Plan."))
                    v = DrawingViews.Plan;
                else if (tag[0].StartsWith("MachineRoom."))
                    v = DrawingViews.MachineRoomPlan;
                else if (tag[0].StartsWith("SectionAA."))
                    v = DrawingViews.SectionAA;
                else if (tag[0].StartsWith("MachineRoomSection."))
                    v = DrawingViews.SectionBB;
                else
                    continue;
                if (!groups.ContainsKey(v))
                    groups[v] = new List<Entity>();
                groups[v].Add(entity);
            }
            return groups;
        }

        /// <summary>Reads the generated drawing back and fills the response.</summary>
        public static void Inspect(Database db, GenerationRequest request, GenerationResponse response)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var groups = Groups(db, tr);
                var bounds = new List<ViewBounds>();
                foreach (DrawingViews view in new[]
                {
                    DrawingViews.Plan,
                    DrawingViews.MachineRoomPlan,
                    DrawingViews.SectionAA,
                    DrawingViews.SectionBB
                })
                {
                    bool selected = (request.Options.Views & view) != 0;
                    if (selected != groups.ContainsKey(view))
                        throw new InvalidDataException("Unexpected or missing view: " + view);
                    if (!selected)
                        continue;
                    var tags = new HashSet<string>();
                    Extents3d? ext = null;
                    foreach (var e in groups[view])
                    {
                        string key = SectionAADrawing.Tag(e)[0];
                        if (!tags.Add(key))
                            throw new InvalidDataException("Duplicate semantic component: " + key);
                        if (!e.Visible)
                            continue;
                        try
                        {
                            var box = e.GeometricExtents;
                            if (ext.HasValue)
                            {
                                var total = ext.Value;
                                total.AddExtents(box);
                                ext = total;
                            }
                            else
                                ext = box;
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception) { }
                    }
                    if (!ext.HasValue)
                        throw new InvalidDataException("View has no measurable geometry: " + view);
                    bounds.Add(new ViewBounds
                    {
                        View = view,
                        MinX = ext.Value.MinPoint.X,
                        MinY = ext.Value.MinPoint.Y,
                        MaxX = ext.Value.MaxPoint.X,
                        MaxY = ext.Value.MaxPoint.Y
                    });
                    response.EntityCounts[view.ToString()] = groups[view].Count;
                }
                for (int a = 0; a < bounds.Count; a++)
                {
                    for (int b = a + 1; b < bounds.Count; b++)
                    {
                        if (Math.Min(bounds[a].MaxX, bounds[b].MaxX) > Math.Max(bounds[a].MinX, bounds[b].MinX) + 0.02
                            && Math.Min(bounds[a].MaxY, bounds[b].MaxY) > Math.Max(bounds[a].MinY, bounds[b].MinY) + 0.02)
                            throw new InvalidDataException("Selected view extents overlap.");
                    }
                }
                response.Bounds = bounds;
                response.ViewsGenerated = groups.Keys.Select(v => v.ToString()).ToArray();
            }
        }
    }
}
