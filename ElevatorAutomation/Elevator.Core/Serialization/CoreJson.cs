using System;
using System.Threading;

namespace Elevator.Core.Serialization
{
    /// <summary>Chooses and creates the JSON serializer used by Core.</summary>
    public static class CoreJson
    {
        // The serializer chosen with Use; null in the .NET Framework application, which uses JavaScriptSerializer.
        internal static Func<IJsonSerializer> Override;

        // A serializer chosen for the current call context only (parallel tests do not see each other's choice).
        static readonly AsyncLocal<Func<IJsonSerializer>> Scoped = new AsyncLocal<Func<IJsonSerializer>>();

        /// <summary>Uses the serializer factory in the current call context until the returned scope is disposed.</summary>
        internal static IDisposable UseScoped(Func<IJsonSerializer> factory)
        {
            var previous = Scoped.Value;
            Scoped.Value = factory;
            return new ScopeEnd(previous);
        }

        sealed class ScopeEnd : IDisposable
        {
            readonly Func<IJsonSerializer> previous;

            public ScopeEnd(Func<IJsonSerializer> previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                Scoped.Value = previous;
            }
        }

        /// <summary>Chooses the serializer factory, for runtimes without JavaScriptSerializer and for tests.</summary>
        public static void Use(Func<IJsonSerializer> factory)
        {
            Override = factory;
        }

        const string NoSerializer =
            "Elevator.Core has no JSON serializer on this runtime: call CoreJson.Use first (JavaScriptCompatibleJson is not proven "
            + "byte-identical to JavaScriptSerializer for every number; see docs/testing/TESTING.md).";

        /// <summary>Without a size limit (project files, fingerprints): JavaScriptSerializer with MaxJsonLength = Int32.MaxValue.</summary>
        public static IJsonSerializer Create()
        {
            var scoped = Scoped.Value;
            if (scoped != null)
                return scoped();
            if (Override != null)
                return Override();
#if NETFRAMEWORK
            return new JavaScriptSerializerJson(Int32.MaxValue);
#else
            throw new NotSupportedException(NoSerializer);
#endif
        }

        /// <summary>With the serializer's default size limit (the preset library), as before.</summary>
        public static IJsonSerializer CreateWithDefaultLimit()
        {
            var scoped = Scoped.Value;
            if (scoped != null)
                return scoped();
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
