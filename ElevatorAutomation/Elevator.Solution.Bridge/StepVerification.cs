using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Elevator.Core.Project;

namespace Elevator.Solution.Bridge
{
    /// <summary>What the STEP worker verified after reopening the file.</summary>
    public sealed class StepVerification
    {
        public int SourceSolidCount;
        public int ReopenedSolidCount;
        public int FaultCount;
        public double MaxReopenedBoundsDifferenceMm;
        public string Units;
        public object[] Intersections;
    }
}
