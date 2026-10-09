using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace Elevator.AutoCAD
{
    public sealed class Binding
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public string Role { get; set; }
        public string Axis { get; set; }
        public string A { get; set; }
        public string B { get; set; }
        public string Band { get; set; }
        public string AnnotationTemplate { get; set; }
    }

    public sealed class BindingFile
    {
        public int SchemaVersion { get; set; }
        public string TemplateId { get; set; }
        public string TemplateSha256 { get; set; }
        public int ExpectedEntityCount { get; set; }
        public List<Binding> Entities { get; set; }
    }

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
