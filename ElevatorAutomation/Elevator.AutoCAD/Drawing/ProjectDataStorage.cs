using System;
using System.Linq;
using System.Web.Script.Serialization;
using Autodesk.AutoCAD.DatabaseServices;
using Elevator.Core;

namespace Elevator.AutoCAD
{
    // Native NOD Xrecords survive SAVEAS and reopening; no sidecar is required.
    public static class ProjectDataStorage
    {
        public const string DictionaryName = "ELEVATOR_AUTOMATION";
        public static T Read<T>(Database db, Transaction tr, string key) where T : class
        {
            var root = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!root.Contains(DictionaryName)) return null;
            var dict = (DBDictionary)tr.GetObject(root.GetAt(DictionaryName), OpenMode.ForRead);
            if (!dict.Contains(key)) return null;
            var record = (Xrecord)tr.GetObject(dict.GetAt(key), OpenMode.ForRead);
            using (var data = record.Data)
            {
                if (data == null) throw new InvalidOperationException("Empty elevator data: " + key);
                var text = String.Concat(data.AsArray().Select(v => Convert.ToString(v.Value)));
                return Serializer().Deserialize<T>(text);
            }
        }
        public static void Write(Database db, Transaction tr, string key, object value)
        {
            WriteRecord(db,tr,key,value);
            UnifiedProjectStorage.LegacyWritten(db,tr,key,value);
        }
        internal static void WriteRecord(Database db, Transaction tr, string key, object value)
        {
            var root = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            DBDictionary dict;
            if (!root.Contains(DictionaryName))
            {
                root.UpgradeOpen(); dict = new DBDictionary(); root.SetAt(DictionaryName, dict); tr.AddNewlyCreatedDBObject(dict, true);
            }
            else dict = (DBDictionary)tr.GetObject(root.GetAt(DictionaryName), OpenMode.ForRead);
            Xrecord record;
            if (!dict.Contains(key))
            {
                dict.UpgradeOpen(); record = new Xrecord(); dict.SetAt(key, record); tr.AddNewlyCreatedDBObject(record, true);
            }
            else record = (Xrecord)tr.GetObject(dict.GetAt(key), OpenMode.ForWrite);
            string json = Serializer().Serialize(value);
            using (var data = new ResultBuffer())
            {
                // Short chunks avoid DWG string-length limits, including Unicode project names.
                for (int i = 0; i < json.Length; i += 200)
                    data.Add(new TypedValue((int)DxfCode.Text, json.Substring(i, Math.Min(200, json.Length - i))));
                record.Data = data;
            }
        }
        private static Elevator.Core.Serialization.IJsonSerializer Serializer() { return Elevator.Core.Serialization.CoreJson.Create(); }
        public static ProjectInput ReadProject(Database db, Transaction tr)
        {
            var p = Read<ProjectInput>(db, tr, "Project");
            if (p != null && p.SchemaVersion != 1) throw new InvalidOperationException("Unsupported project schema; upgrade the plugin before editing this DWG.");
            return p;
        }
    }
}
