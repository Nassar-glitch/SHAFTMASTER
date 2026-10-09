using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;

namespace Elevator.AutoCAD
{
    public sealed class LegacyMap
    {
        public string SemanticId { get; set; }
        public string LegacyKey { get; set; }
        public string Handle { get; set; }
    }

    public static class LegacyMigration
    {
        // Only this import boundary knows old handles. Normal reading/updating never uses them.
        public static void Ensure(Database db, Transaction tr)
        {
            if (ProjectDataStorage.Read<BindingFile>(db, tr, "PlanBindings") != null)
                return;
            var manifest = Configuration.Read<BindingFile>("template-bindings.json");
            var mapping = Configuration.Read<List<LegacyMap>>("legacy-migration.json");
            var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var handles = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            var tags = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId id in model)
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForRead);
                handles.Add(e.Handle.ToString(), e);
                using (var rb = e.GetXDataForApplication("ELEVATOR_PARAMETRIC"))
                {
                    if (rb == null || rb.AsArray().Length < 2)
                        continue;
                    string key = Convert.ToString(rb.AsArray()[1].Value);
                    if (tags.ContainsKey(key))
                        throw new InvalidOperationException("Duplicate legacy component: " + key);
                    tags.Add(key, e);
                }
            }
            bool tagged = mapping.All(m => tags.ContainsKey(m.LegacyKey));
            if (!tagged)
            {
                string hash;
                using (var sha = SHA256.Create())
                {
                    using (var stream = File.Open(db.Filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                }
                if (!String.Equals(hash, manifest.TemplateSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                                            "Unrecognized untagged drawing. Use Templates/ExistingPlan_Master.dwg. Automatic handle "
                                            + "migration is restricted to "
                                            + "the verified original template or a complete legacy tagged output.");
                }
            }
            var selected = new Dictionary<string, Entity>();
            foreach (var m in mapping)
            {
                Entity e;
                if (!(tagged ? tags : handles).TryGetValue(tagged ? m.LegacyKey : m.Handle, out e))
                    throw new InvalidOperationException("Legacy component missing: " + m.SemanticId);
                if (e.GetRXClass().DxfName != manifest.Entities.Single(b => b.Key == m.SemanticId).Type)
                    throw new InvalidOperationException("Legacy component type changed: " + m.SemanticId);
                selected.Add(m.SemanticId, e);
            }
            ComponentBinding.Register(db, tr);
            foreach (var b in manifest.Entities)
                ComponentBinding.Tag(selected[b.Key], b.Key, b.Role);
            ProjectDataStorage.Write(db, tr, "PlanBindings", manifest);
            DrawingReader.Read(db, tr); // Validate the original topology before committing migration.
        }
    }
}
