using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Elevator.Core.Project;

namespace Elevator.Solution.Bridge
{
    public sealed class StepExportResult
    {
        public string OutputPath;
        public string SourceFolder;
        public string LayoutStatus;
        public string WrapAngleStatus;
        public string CodeDiameterRatioStatus;
        public double ElapsedSeconds;
        public StepVerification Verification;
    }
}
