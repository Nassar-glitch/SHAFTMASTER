using System;

namespace Elevator.Core.Serialization
{
    public static class CoreJson
    {
        // The serializer chosen with Use; null in the .NET Framework application, which uses JavaScriptSerializer.
        internal static Func<IJsonSerializer> Override;

        public static void Use(Func<IJsonSerializer> factory)
        {
            Override = factory;
        }

        const string NoSerializer =
            "Elevator.Core has no JSON serializer on this runtime: call CoreJson.Use first (JavaScriptCompatibleJson is not proven "
            + "byte-identical to JavaScriptSerializer for every number; see docs/TESTING.md).";

        // Without a size limit (project files, fingerprints): JavaScriptSerializer with MaxJsonLength = Int32.MaxValue.
        public static IJsonSerializer Create()
        {
            if (Override != null)
                return Override();
#if NETFRAMEWORK
            return new JavaScriptSerializerJson(Int32.MaxValue);
#else
            throw new NotSupportedException(NoSerializer);
#endif
        }

        // With the serializer's default size limit (the preset library), as before.
        public static IJsonSerializer CreateWithDefaultLimit()
        {
            if (Override != null)
                return Override();
#if NETFRAMEWORK
            return new JavaScriptSerializerJson(null);
#else
            throw new NotSupportedException(NoSerializer);
#endif
        }
    }
}
