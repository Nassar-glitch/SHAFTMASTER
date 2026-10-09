#if NETFRAMEWORK
namespace Elevator.Core.Serialization
{
    /// <summary>.NET Framework: JavaScriptSerializer itself.</summary>
    public sealed class JavaScriptSerializerJson : IJsonSerializer
    {
        readonly System.Web.Script.Serialization.JavaScriptSerializer serializer;

        public JavaScriptSerializerJson(int? maxJsonLength)
        {
            serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            if (maxJsonLength.HasValue)
                serializer.MaxJsonLength = maxJsonLength.Value;
        }

        /// <summary>Writes an object as JSON with JavaScriptSerializer.</summary>
        public string Serialize(object value)
        {
            return serializer.Serialize(value);
        }

        /// <summary>Reads JSON into a typed object with JavaScriptSerializer.</summary>
        public T Deserialize<T>(string json)
        {
            return serializer.Deserialize<T>(json);
        }

        /// <summary>Reads JSON into dictionaries, lists and primitives with JavaScriptSerializer.</summary>
        public object DeserializeObject(string json)
        {
            return serializer.DeserializeObject(json);
        }
    }
}
#endif
