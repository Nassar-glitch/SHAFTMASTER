using System;
namespace Elevator.Core.Serialization
{
    // JSON used by Core: project files, the preset library and the calculation fingerprints (ARCHITECTURE-PLAN P2).
    // The contract is the output of System.Web.Script.Serialization.JavaScriptSerializer, which the .NET Framework build
    // keeps using. JavaScriptCompatibleJson reproduces it, but not proven byte-identical for every double (docs/TESTING.md),
    // so the netstandard2.0 build has no default: a caller chooses the serializer with CoreJson.Use (today: the tests).
    public interface IJsonSerializer
    {
        string Serialize(object value);
        T Deserialize<T>(string json);
        // Objects as Dictionary<string,object>, arrays as lists, numbers as int, long, decimal or double.
        object DeserializeObject(string json);
    }
    public static class CoreJson
    {
        // The serializer chosen with Use; null in the .NET Framework application, which uses JavaScriptSerializer.
        internal static Func<IJsonSerializer> Override;
        public static void Use(Func<IJsonSerializer> factory){Override=factory;}
        const string NoSerializer="Elevator.Core has no JSON serializer on this runtime: call CoreJson.Use first (JavaScriptCompatibleJson is not proven byte-identical to JavaScriptSerializer for every number; see docs/TESTING.md).";
        // Without a size limit (project files, fingerprints): JavaScriptSerializer with MaxJsonLength = Int32.MaxValue.
        public static IJsonSerializer Create()
        {
            if(Override!=null)return Override();
#if NETFRAMEWORK
            return new JavaScriptSerializerJson(Int32.MaxValue);
#else
            throw new NotSupportedException(NoSerializer);
#endif
        }
        // With the serializer's default size limit (the preset library), as before.
        public static IJsonSerializer CreateWithDefaultLimit()
        {
            if(Override!=null)return Override();
#if NETFRAMEWORK
            return new JavaScriptSerializerJson(null);
#else
            throw new NotSupportedException(NoSerializer);
#endif
        }
    }
#if NETFRAMEWORK
    // .NET Framework: JavaScriptSerializer itself.
    public sealed class JavaScriptSerializerJson:IJsonSerializer
    {
        readonly System.Web.Script.Serialization.JavaScriptSerializer serializer;
        public JavaScriptSerializerJson(int? maxJsonLength)
        {
            serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
            if(maxJsonLength.HasValue)serializer.MaxJsonLength=maxJsonLength.Value;
        }
        public string Serialize(object value){return serializer.Serialize(value);}
        public T Deserialize<T>(string json){return serializer.Deserialize<T>(json);}
        public object DeserializeObject(string json){return serializer.DeserializeObject(json);}
    }
#endif
}
