using System;
using System.Collections.Generic;
using Elevator.Core.Calculations;
using Elevator.Core.MachineRoom;
using Elevator.Core.Project;

namespace Elevator.Core.GuideRails
{
    public sealed class CarGuideForceGeometry
    {
        public double? InternalWidthMm
        {
            get; private set;
        }

        public double? InternalDepthMm
        {
            get; private set;
        }

        public string Status
        {
            get; private set;
        }

        public List<CalculationIssue> Issues
        {
            get; private set;
        }

        internal readonly string SourceInputHash;
        internal readonly string SourceContextHash;

        internal CarGuideForceGeometry(ElevatorProject p, DesignContext c, Func<ElevatorParameters> calculate)
        {
            Issues = CarGuideForceCalculationEngine.GeometryInputIssues(p, c);
            Status = Issues.Count == 0 ? "SOURCE_BOUND" : "INVALID_INPUT";
            if (Issues.Count > 0)
                return;
            // Capture provenance BEFORE invoking the shared plan recipe, never stamp old
            // raw geometry with current inputs. Geometry cannot be supplied/deserialized
            // through the live calculation API as an authoritative dimensions object.
            SourceInputHash = CarGuideForceCalculationEngine.GeometryInputHash(p);
            SourceContextHash = CarGuideForceCalculationEngine.GeometryContextHash(c);
            try
            {
                var plan = calculate();
                if (plan == null || !Positive(plan.CarWidth) || !Positive(plan.CarDepth))
                    throw new ArgumentException("Shared internal cabin dimensions must be finite and positive.");
                if (SourceInputHash == null
                    || SourceContextHash == null
                    || SourceInputHash != CarGuideForceCalculationEngine.GeometryInputHash(p)
                    || SourceContextHash != CarGuideForceCalculationEngine.GeometryContextHash(c))
                    throw new InvalidOperationException("Geometry sources changed during generation; regenerate.");
                InternalWidthMm = plan.CarWidth;
                InternalDepthMm = plan.CarDepth;
            }
            catch (ArgumentException e) { Status = "INVALID_INPUT"; Issues.Add(new CalculationIssue("Cabin.Geometry", e.Message)); }
            catch (InvalidOperationException e)
            {
                Status =
                "STALE_GEOMETRY";
                Issues.Add(new CalculationIssue("Cabin.Geometry", e.Message));
            }
        }

        static bool Positive(double v)
        {
            return ChassisReactionSolver.Finite(v) && v > 0;
        }
    }
}
