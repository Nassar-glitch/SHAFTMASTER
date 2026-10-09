using System;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.AutoCAD.Common;

namespace Elevator.AutoCAD.Plan
{
    public static class ComponentBinding
    {
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

        public static void Tag(Entity entity, string id, string role)
        {
            entity.UpgradeOpen();
            using (var data =
                new ResultBuffer(new TypedValue(1001, DrawingReader.RegApp), new TypedValue(1000, id), new TypedValue(1000, role)))
                entity.XData = data;
        }
    }
}
