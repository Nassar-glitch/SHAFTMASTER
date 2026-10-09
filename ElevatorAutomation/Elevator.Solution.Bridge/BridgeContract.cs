using System;

namespace Elevator.Solution.Bridge
{
    /// <summary>The version of the request / response contracts exchanged with the AutoCAD host.</summary>
    public static class BridgeContract
    {
        /// <summary>Contract version written into every request and response; the host rejects any other value.</summary>
        public const int Version = 1;

        /// <summary>Throws ArgumentException when a request carries another contract version.</summary>
        public static void Require(int version)
        {
            if (version != Version)
                throw new ArgumentException("Unsupported/incomplete generation request.");
        }
    }
}
