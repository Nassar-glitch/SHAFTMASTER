using System;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.AutoCAD.SectionAA;
using Elevator.AutoCAD.SectionBB;
using Elevator.Core.Geometry;
using Elevator.Core.Project;

namespace Elevator.AutoCAD.Common
{
    /// <summary>Stores and reads the project in the drawing.</summary>
    public static class UnifiedProjectStorage
    {
        /// <summary>Name of the stored project record.</summary>
        public const string Key = "ElevatorProject";

        /// <summary>Reads the project from the drawing.</summary>
        public static ElevatorProject Read(Database db, Transaction tr)
        {
            var doc = ProjectDataStorage.Read<ProjectDocument>(db, tr, Key);
            if (doc != null)
            {
                if (doc.schemaVersion != 1 || doc.project == null)
                    throw new InvalidOperationException("Unsupported unified project schema.");
                return doc.project;
            }
            var legacy = ProjectDataStorage.ReadProject(db, tr);
            return legacy == null
                ? null
                : LegacyProjectAdapter.FromLegacy(legacy, ProjectDataStorage.Read<SectionAAProject>(db, tr, SectionAADrawing.DataKey));
        }

        // Called inside the SAME DWG transaction as an existing command's legacy write.
        internal static void LegacyWritten(Database db, Transaction tr, string key, object value)
        {
            if (key != "Project" && key != SectionAADrawing.DataKey)
                return;
            var legacy = ProjectDataStorage.Read<ProjectInput>(db, tr, "Project");
            if (legacy == null || legacy.SchemaVersion != 1)
                return;
            var current = ProjectDataStorage.Read<ProjectDocument>(db, tr, Key);
            if (current != null && current.schemaVersion != 1)
                throw new InvalidOperationException("Unsupported unified project schema; legacy edit cancelled.");
            var merged = LegacyProjectAdapter.FromLegacy(
                legacy,
                ProjectDataStorage.Read<SectionAAProject>(db, tr, SectionAADrawing.DataKey),
                current == null ? null : current.project);
            ProjectDataStorage.WriteRecord(db, tr, Key, new ProjectDocument { project = merged });
        }

        /// <summary>Stores the project in the drawing.</summary>
        public static void Save(Database db, Transaction tr, ElevatorProject project)
        {
            // Detached snapshot prevents subsequent caller mutations from changing stored intent.
            var p = ProjectJson.Clone(project);
            ProjectDataStorage.WriteRecord(db, tr, "Project", LegacyProjectAdapter.ToLegacy(p));
            if (p.Building.Floors.Count > 0)
            {
                var aa = LegacyProjectAdapter.ToSectionAA(p);
                if (MachineRoomSectionDrawing.IsPresent(db, tr))
                {
                    aa.Floors = SectionAACalculator.Calculate(
                                            aa,
                                            MachineRoomSectionDrawing.Read(db, tr).Manifest.Reference.SourceSlabThickness).Floors;
                }
                ProjectDataStorage.WriteRecord(db, tr, SectionAADrawing.DataKey, aa);
            }
            ProjectDataStorage.WriteRecord(db, tr, Key, new ProjectDocument { project = p });
        }

        /// <summary>Writes the stored project as JSON.</summary>
        public static string ExportJson(Database db, Transaction tr)
        {
            return ProjectJson.Serialize(Read(db, tr));
        }
    }
}
