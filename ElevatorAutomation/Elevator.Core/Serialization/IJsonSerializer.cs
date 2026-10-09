namespace Elevator.Core.Serialization
{
    /// <summary>
    /// JSON used by Core: project files, the preset library and the calculation fingerprints (ARCHITECTURE-PLAN P2). The contract is the
    /// output of System.Web.Script.Serialization.JavaScriptSerializer, which the .NET Framework build keeps using. JavaScriptCompatibleJson
    /// reproduces it, but not proven byte-identical for every double (docs/TESTING.md), so the netstandard2.0 build has no default: a
    /// caller chooses the serializer with CoreJson.Use (today: the tests).
    /// </summary>
    public interface IJsonSerializer
    {
        string Serialize(object value);
        T Deserialize<T>(string json);

        // Objects as Dictionary<string,object>, arrays as lists, numbers as int, long, decimal or double.
        object DeserializeObject(string json);
    }
}
