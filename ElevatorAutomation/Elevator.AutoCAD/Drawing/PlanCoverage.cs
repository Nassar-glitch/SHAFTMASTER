using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace Elevator.AutoCAD
{
    public sealed class PlanCoverageReport
    {
        public int ModelEntities;
        public int BoundEntities;
        public int UnboundEntities;
        public int Hatches;
        public int Dimensions;
        public int DrivenAnnotations;
        public int OtherViewEntities;
        public Dictionary<string, int> UnboundByType;
        public string[] StaticAnnotations;
    }

    public static class PlanCoverage
    {
        public static PlanCoverageReport Read(Database db, Transaction tr, DrawingSnapshot snapshot)
        {
            var bound = new HashSet<ObjectId>(snapshot.Entities.Values.Select(e => e.ObjectId));
            var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var unbound = new Dictionary<string, int>();
            int otherViewEntities = 0;
            var other = ProjectDataStorage.Read<MachineRoomManifest>(db, tr, MachineRoomDrawing.Record);
            var otherKeys = new HashSet<string>(other == null ? new string[0] : other.Entities.Select(b => b.Key).ToArray());
            var section = ProjectDataStorage.Read<SectionManifest>(db, tr, MachineRoomSectionDrawing.Record);
            if (section != null)
            {
                foreach (var b in section.Entities)
                    otherKeys.Add(b.Key);
            }
            foreach (ObjectId id in model)
            {
                if (!bound.Contains(id))
                {
                    var entity = (Entity)tr.GetObject(id, OpenMode.ForRead);
                    string type = entity.GetRXClass().DxfName;
                    using (var data = entity.GetXDataForApplication(DrawingReader.RegApp))
                    {
                        if (data != null && data.AsArray().Length > 1 && otherKeys.Contains(Convert.ToString(data.AsArray()[1].Value)))
                        {
                            otherViewEntities++;
                            continue;
                        }
                    }
                    if (!unbound.ContainsKey(type))
                        unbound[type] = 0;
                    unbound[type]++;
                }
            }
            return new PlanCoverageReport
            {
                ModelEntities = snapshot.ModelEntityCount,
                BoundEntities = bound.Count,
                UnboundEntities = unbound.Values.Sum(),
                UnboundByType = unbound,
                OtherViewEntities = otherViewEntities,
                Hatches = snapshot.Entities.Values.Count(e => e is Hatch),
                Dimensions = snapshot.Entities.Values.Count(e => e is Dimension),
                DrivenAnnotations = snapshot.Bindings.Values.Count(b => b.AnnotationTemplate != null),
                StaticAnnotations = snapshot.Entities
                    .Where(p => (p.Value is MText || p.Value is MLeader) && snapshot.Bindings[p.Key].AnnotationTemplate == null)
                    .Select(p => p.Key)
                    .ToArray()
            };
        }
    }
}
