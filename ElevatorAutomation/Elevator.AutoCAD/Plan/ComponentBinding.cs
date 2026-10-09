using System;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.AutoCAD.Common;

namespace Elevator.AutoCAD.Plan
{
    /// <summary>Tags drawing entities with their key and role, and reads the manifest.</summary>
    public static class ComponentBinding
    {
        /// <summary>Reads the plan binding manifest from the drawing.</summary>
        public static BindingFile ReadManifest(Database db, Transaction tr)
        {
            var m = ProjectDataStorage.Read<BindingFile>(db, tr, "PlanBindings");
            if (m == null || m.SchemaVersion != 1 || m.Entities == null)
            {
                throw new InvalidOperationException(
                                    "Missing or unsupported semantic plan bindings. Use a prepared master or migrate the known legacy "
                                    + "plan.");
            }
            return m;
        }

        /// <summary>Registers the application name used for entity tags.</summary>
        public static void Register(Database db, Transaction tr)
        {
            var apps = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (apps.Has(DrawingReader.RegApp))
                return;
            apps.UpgradeOpen();
            var app = new RegAppTableRecord { Name = DrawingReader.RegApp };
            apps.Add(app);
            tr.AddNewlyCreatedDBObject(app, true);
        }

        /// <summary>Tags an entity with a key and a role.</summary>
        public static void Tag(Entity entity, string id, string role)
        {
            entity.UpgradeOpen();
            using (var data =
                new ResultBuffer(new TypedValue(1001, DrawingReader.RegApp), new TypedValue(1000, id), new TypedValue(1000, role)))
                entity.XData = data;
        }
    }
}
