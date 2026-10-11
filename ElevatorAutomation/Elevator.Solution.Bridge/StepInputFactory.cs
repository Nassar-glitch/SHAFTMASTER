using System;
using System.Linq;
using System.Collections.Generic;
using Elevator.Core.Doors;
using Elevator.Core.Project;

namespace Elevator.Solution.Bridge
{
    /// <summary>Builds the STEP model parameters from a project.</summary>
    public static class StepInputFactory
    {
        // Automatic doors (2S, 2C): the panels and the frame as simple boxes from the shared door layout; Manual: nothing is set.
        static void AutomaticDoor(StepModelParameters p, ElevatorProject project, DesignContext context, ElevatorParameters plan)
        {
            var l = DoorRules.Evaluate(project, context.Doors, plan);
            if (!DoorTypes.IsAutomatic(l.Type))
                return;
            double t = l.PanelThicknessMm, pw = l.PanelWidthMm, o = l.OpeningMm, sw = plan.SW;
            double lz = l.LandingZoneMm, cd = l.LandingZoneMm + l.SillGapMm, clear = p.DoorClearHeight, full = p.DoorConstructionHeight;
            p.DoorOpeningLeft = l.OpeningLeftX;
            p.DoorOpeningRight = l.OpeningRightX;
            var landing = new List<double>();
            var car = new List<double>();
            var frame = new List<double>();
            Action<List<double>, double, double, double, double, double, double> box =
                (list, x0, x1, d0, d1, z0, h) =>
                {
                    list.AddRange(new[] { Math.Min(x0, x1), d0, Math.Abs(x1 - x0), d1 - d0, z0, h });
                };
            if (l.Type == DoorTypes.Automatic2S)
            {
                bool right = l.StackingSide == DoorTypes.Right;
                Func<double, double> m = x => right ? x : sw - x;
                double uf = right ? l.OpeningLeftX : sw - l.OpeningRightX, us = uf + o;
                double ul = right ? l.LandingLeftX : sw - l.LandingRightX, ur = right ? l.LandingRightX : sw - l.LandingLeftX;
                double ot = Math.Max(0, 2 * pw - o - l.PanelOverlapMm);
                box(landing, m(uf), m(uf + pw), lz - t, lz, 0, clear);
                box(landing, m(us + ot - pw), m(us + ot), lz - 2 * t - 5, lz - t - 5, 0, clear);
                box(car, m(uf), m(uf + pw), cd, cd + t, 0, clear);
                box(car, m(us + ot - pw), m(us + ot), cd + t + 5, cd + 2 * t + 5, 0, clear);
                // Frame: fixed-side jamb, stacking pocket (beyond the panel over-travel), header over the opening.
                if (uf - ul > 0)
                    box(frame, m(ul), m(uf), 0, lz, 0, full);
                if (ur - (us + ot) > 0)
                    box(frame, m(us + ot), m(ur), 0, lz, 0, full);
                box(frame, m(uf), m(us), 0, lz, clear, full - clear);
            }
            else
            {
                double dl = lz - 5 - t, dc = cd + 12;
                box(landing, l.OpeningLeftX, l.OpeningLeftX + pw, dl, dl + t, 0, clear);
                box(landing, l.OpeningRightX - pw, l.OpeningRightX, dl, dl + t, 0, clear);
                box(car, l.OpeningLeftX, l.OpeningLeftX + pw, dc, dc + t, 0, clear);
                box(car, l.OpeningRightX - pw, l.OpeningRightX, dc, dc + t, 0, clear);
                box(frame, l.LandingLeftX, l.LandingRightX, lz - 70, lz, clear, full - clear);
            }
            p.LandingDoorPanels = landing.ToArray();
            p.CarDoorPanels = car.ToArray();
            p.LandingDoorFrame = frame.ToArray();
        }

        /// <summary>Calculates the design and builds the STEP model parameters.</summary>
        public static StepModelParameters Create(
            ElevatorProject project,
            DesignContext context,
            StepModelParameters reference,
            out ElevatorDesignResult design)
        {
            design = new ElevatorDesignEngine().Calculate(project, context, DrawingViews.All);
            if (!design.CanGenerate)
                throw new ArgumentException(String.Join("\n", design.Validation.Where(v => v.BlocksGeneration).Select(v => v.Message)));
            if (design.SectionAA == null || design.Plan == null || design.Plan.MachineRoomSection == null)
                throw new ArgumentException("The coordinated four-view design could not be calculated.");
            var p = Elevator.Core.Serialization.CoreJson.CreateWithDefaultLimit().Deserialize<StepModelParameters>(Wire.Json(reference));
            var plan = design.Plan;
            var section = plan.MachineRoomSection;
            var aa = design.SectionAA;
            var mr = plan.MachineRoom;
            if (!section.HasCalculatedPath)
            {
                throw new ArgumentException(
                                    "The preliminary 3D workflow requires the reviewed two-sheave arrangement. Direct rope routing or TZ "
                                    + "above 1100 mm "
                                    + "needs a reviewed 3D arrangement before export.");
            }
            p.SW = plan.SW;
            p.SD = plan.SD;
            p.CarWidth = plan.CarWidth;
            p.CarDepth = plan.CarDepth;
            p.CarFront = plan.CarFront;
            p.CarRow = plan.CarRow;
            p.CarDBG = plan.CarDbg;
            p.CwDBG = plan.CwDbg;
            p.CwWidth = plan.CwWidth;
            p.CwDepth = plan.CwDepth;
            p.CwRow = plan.CwRow;
            p.DoorWidth = plan.DoorWidth;
            AutomaticDoor(p, project, context, plan);
            p.Pit = -aa.PitZ;
            p.Travel = aa.Underside - project.Shaft.OverheadMm.Value;
            p.Overhead = project.Shaft.OverheadMm.Value;
            p.RoofSlab = aa.R - aa.Underside;
            p.RoomClearHeight = aa.Ceiling - aa.R;
            p.CarFloorZ = aa.CarZ;
            p.CwBottom = aa.CWBottom;
            p.CarInternalHeight = design.CarInternalHeightMm;
            p.CwHeight = design.CounterweightHeightMm;
            p.LandingSlab = design.LandingSlabThicknessMm;
            p.DoorClearHeight = design.DoorClearHeightMm;
            p.DoorConstructionHeight = design.DoorConstructionHeightMm;
            p.FloorElevations = aa.Floors.Where(f => !f.IsRoof).Select(f => f.FflZ).ToArray();
            p.ServedFloors = aa.Floors.Where(f => !f.IsRoof).Select(f => f.Served).ToArray();
            p.RoomLeft = mr.Extensions.Left;
            p.RoomRight = mr.Extensions.Right;
            p.RoomFront = mr.Extensions.Front;
            p.RoomRear = mr.Extensions.Rear;
            p.ConcreteHeight = section.ConcreteHeight;
            p.SteelBottom = section.SteelBottomZ;
            p.SteelDepth = section.Beam.NominalDepthMm;
            p.BeamDesignation = section.Beam.SectionDesignation;
            p.FrontSupportCenterY = -section.FrontSupportX;
            p.RearSupportCenterY = -section.RearSupportX;
            p.TractionY = -section.TractionX;
            p.TractionZAboveRoof = section.TractionZ;
            p.TractionDiameter = 520;
            p.DeflectorY = -section.DeflectorX;
            p.DeflectorZAboveRoof = section.DeflectorZ;
            p.DeflectorDiameter = section.DeflectorPitchMm;
            p.DeflectorVisible = section.DeflectorVisible;
            p.HangerBottomAboveDeflectorCenter = context.SectionReference.HangerBottomAboveDeflectorCenter;
            // Retain the reviewed preliminary component dimensions and local reference offsets.
            // Equipment translates with the car-guide datum; governor X stays on its shaft-left datum, as in the MR updater.
            double dx =
                (plan.SW - reference.SW) / 2, dy = plan.CarRow - reference.CarRow, dz = section.TractionZ - reference.TractionZAboveRoof;
            p.Beam1X = reference.Beam1X + dx;
            p.Beam2X = reference.Beam2X + dx;
            p.SupportLeftX = reference.SupportLeftX + dx;
            p.MachineBaseX = reference.MachineBaseX + dx;
            p.MachineBaseY = reference.MachineBaseY + dy;
            p.MachineBaseTop = reference.MachineBaseTop + dz;
            p.MachineGearboxX = reference.MachineGearboxX + dx;
            p.MachineMotorX = reference.MachineMotorX + dx;
            p.MachineMotorY = reference.MachineMotorY + dy;
            p.MachineMotorZ = reference.MachineMotorZ + dz;
            p.GovernorY = plan.CarRow + mr.Governor.OffsetFromCarGuideAxisMm.Value + p.GovernorDiameter / 2;
            if (p.FloorElevations.Any(z => z > aa.Underside))
                throw new ArgumentException("A floor is above the shaft roof datum.");
            return p;
        }
    }
}
