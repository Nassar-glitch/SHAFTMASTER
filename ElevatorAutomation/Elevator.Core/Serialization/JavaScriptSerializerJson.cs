#if NETFRAMEWORK
namespace Elevator.Core.Serialization
{
    // .NET Framework: JavaScriptSerializer itself.
    public sealed class JavaScriptSerializerJson : IJsonSerializer
    {
        readonly System.Web.Script.Serialization.JavaScriptSerializer serializer;

        public JavaScriptSerializerJson(int? maxJsonLength)
        {
            serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            if (maxJsonLength.HasValue)
                serializer.MaxJsonLength = maxJsonLength.Value;
        }

        public string Serialize(object value)
        {
            return serializer.Serialize(value);
        }

        public T Deserialize<T>(string json)
        {
            return serializer.Deserialize<T>(json);
        }

        public object DeserializeObject(string json)
        {
            return serializer.DeserializeObject(json);
        }
    }
}
#endif
