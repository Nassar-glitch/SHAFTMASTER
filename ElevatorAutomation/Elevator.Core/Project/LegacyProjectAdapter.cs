using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Doors;
using Elevator.Core.Geometry;

namespace Elevator.Core.Project
{
    public static class LegacyProjectAdapter
    {
        public static ElevatorProject FromLegacy(ProjectInput old, SectionAAProject aa, ElevatorProject retained = null)
        {
            if (old == null || old.SchemaVersion != 1)
                throw new ArgumentException("Unsupported legacy Project schema.");
            if (aa != null && aa.SchemaVersion != 1)
                throw new ArgumentException("Unsupported legacy A-A schema.");
            var p = retained == null ? new ElevatorProject() : ProjectJson.Clone(retained);
            p.Info.Name = old.ProjectName;
            p.Elevator.LegacyFamily = old.ElevatorType;
            p.Elevator.TemplateId = old.TemplateId;
            if (retained == null)
                p.Elevator.SystemId = old.ElevatorType == ElevatorType.ExistingPlan || old.ElevatorType == ElevatorType.Gearbox
                    ? "traction-geared"
                    : "legacy-" + old.ElevatorType;
            p.Elevator.CapacityKg = old.Capacity;
            p.Elevator.SpeedMetresPerSecond = old.Speed;
            p.Shaft.WidthMm = old.SW;
            p.Shaft.DepthMm = old.SD;
            p.Shaft.PitDepthMm = old.Pit;
            p.Shaft.OverheadMm = old.Overhead;
            p.Doors.Type = old.DoorType;
            p.Doors.EntranceConfiguration = old.EntranceConfiguration;
            p.Counterweight.Position = old.CounterweightPosition;
            p.Machine.Type = old.MachineType;
            p.Machine.TractionSheavePitchDiameterMm = old.TractionSheavePitchDiameterMm;
            p.Suspension.Type = old.SuspensionType;
            p.Suspension.RopeDiameterMm = old.SuspensionRopeDiameterMm;
            p.Suspension.RopeCount = old.NumberOfSuspensionRopes;
            p.Rails.RequestedRailType = old.RailType;
            p.Drawing.PlanTitle = old.PlanTitle;
            p.MachineRoom.LeftExtensionMm = old.MachineRoomLeftExtension;
            p.MachineRoom.RightExtensionMm = old.MachineRoomRightExtension;
            p.MachineRoom.FrontExtensionMm = old.MachineRoomFrontExtension;
            p.MachineRoom.RearExtensionMm = old.MachineRoomRearExtension;
            p.MachineRoom.MinimumDeflectorSlabClearanceMm = old.MinimumDeflectorSlabClearanceMm;
            p.Extensions = old.Extensions == null ? new Dictionary<string, object>() : new Dictionary<string, object>(old.Extensions);
            // Legacy summary values were placeholders, not an authoritative floor stack. Preserve them
            // as migration metadata only until actual SectionAA floor data exists.
            if (aa == null && p.Building.Floors.Count == 0)
            {
                p.Extensions["legacy.stops"] = old.Stops;
                p.Extensions["legacy.travel"] = old.Travel;
                p.Extensions["legacy.floorHeights"] = old.FloorHeights;
            }
            if (aa != null)
            {
                var normal = aa.Floors.Where(f => !f.IsRoof).ToArray();
                if (normal.Length == 0 || Math.Abs(normal[0].FflZ) > 1e-8)
                    throw new ArgumentException("Legacy lowest floor must have FFL zero.");
                var previous = p.Building.Floors.ToDictionary(f => f.FloorId);
                p.Building.Floors = normal.Select((f, i) => new FloorDefinition
                {
                    FloorId = f.FloorId,
                    Name = f.Name,
                    IsServed = f.Served,
                    HeightToNextMm = i + 1 < normal.Length ? (double?)(normal[i + 1].FflZ - f.FflZ) : null,
                    LandingDoor = previous.ContainsKey(f.FloorId) ? previous[f.FloorId].LandingDoor : new LandingDoorConfiguration()
                }).ToList();
                p.Extensions["legacy.roofFloorId"] = aa.Floors.Last().FloorId;
                p.Extensions["legacy.roofName"] = aa.Floors.Last().Name;
                p.Shaft.PitDepthMm = aa.Pit;
                p.Shaft.OverheadMm = aa.OH;
                p.Car.DisplayFloorId = aa.DisplayFloorId;
                p.Counterweight.ReferenceCarElevationMm = aa.ReferenceCarZ;
                p.Counterweight.ReferenceBottomElevationMm = aa.ReferenceCWBottomZ;
                p.Counterweight.CalibrationStatus = aa.CWCalibrationStatus;
                p.Rails.BracketMaximumSpacingMm = aa.BracketMaximum;
                p.Rails.BracketSource = aa.BracketSource;
                p.Suspension.RopingRatio = 1;
                p.Extensions.Remove("legacy.stops");
                p.Extensions.Remove("legacy.travel");
                p.Extensions.Remove("legacy.floorHeights");
            }
            return p;
        }

        public static ProjectInput ToLegacy(ElevatorProject p, bool includeFloorSummary = true)
        {
            var old = new ProjectInput
            {
                ElevatorType = p.Elevator.LegacyFamily,
                TemplateId = p.Elevator.TemplateId,
                SW = p.Shaft.WidthMm,
                SD = p.Shaft.DepthMm,
                ProjectName = p.Info.Name,
                Capacity = p.Elevator.CapacityKg,
                Speed = p.Elevator.SpeedMetresPerSecond,
                Pit = p.Shaft.PitDepthMm,
                Overhead = p.Shaft.OverheadMm,
                DoorType = p.Doors.Type,
                EntranceConfiguration = p.Doors.EntranceConfiguration,
                CounterweightPosition = p.Counterweight.Position,
                MachineType = p.Machine.Type,
                TractionSheavePitchDiameterMm = p.Machine.TractionSheavePitchDiameterMm,
                SuspensionType = p.Suspension.Type,
                SuspensionRopeDiameterMm = p.Suspension.RopeDiameterMm,
                NumberOfSuspensionRopes = p.Suspension.RopeCount,
                RailType = p.Rails.RequestedRailType,
                PlanTitle = p.Drawing.PlanTitle,
                MachineRoomLeftExtension = p.MachineRoom.LeftExtensionMm,
                MachineRoomRightExtension = p.MachineRoom.RightExtensionMm,
                MachineRoomFrontExtension = p.MachineRoom.FrontExtensionMm,
                MachineRoomRearExtension = p.MachineRoom.RearExtensionMm,
                MinimumDeflectorSlabClearanceMm = p.MachineRoom.MinimumDeflectorSlabClearanceMm,
                Extensions = new Dictionary<string, object>(p.Extensions)
            };
            if (includeFloorSummary && p.Building.Floors.Count > 0)
            {
                var floors = ProjectFloors.Elevations(p.Building);
                var served = floors.Where(f => f.IsServed).ToArray();
                old.Stops = served.Length;
                old.Travel = served.Length == 0 ? (double?)null : served.Last().ElevationMm - served.First().ElevationMm;
                old.FloorHeights = p.Building.Floors.Where(f => f.HeightToNextMm.HasValue).Select(f => f.HeightToNextMm.Value).ToArray();
            }
            else if (includeFloorSummary)
            {
                object v;
                if (p.Extensions.TryGetValue("legacy.stops", out v) && v != null)
                    old.Stops = Convert.ToInt32(v);
                if (p.Extensions.TryGetValue("legacy.travel", out v) && v != null)
                    old.Travel = Convert.ToDouble(v);
                if (p.Extensions.TryGetValue("legacy.floorHeights", out v) && v != null)
                    old.FloorHeights = ((System.Collections.IEnumerable)v).Cast<object>().Select(Convert.ToDouble).ToArray();
            }
            return old;
        }

        public static SectionAAProject ToSectionAA(ElevatorProject p)
        {
            if (!p.Shaft.PitDepthMm.HasValue
                || !p.Shaft.OverheadMm.HasValue
                || !p.Rails.BracketMaximumSpacingMm.HasValue
                || !p.Counterweight.ReferenceCarElevationMm.HasValue
                || !p.Counterweight.ReferenceBottomElevationMm.HasValue)
                throw new ArgumentException("A-A requires Pit, OH, bracket maximum and the explicit provisional/reference CW pose.");
            var d = new SectionAAProject
            {
                Pit = p.Shaft.PitDepthMm.Value,
                OH = p.Shaft.OverheadMm.Value,
                BracketMaximum = p.Rails.BracketMaximumSpacingMm.Value,
                DisplayFloorId = p.Car.DisplayFloorId,
                ReferenceCarZ = p.Counterweight.ReferenceCarElevationMm.Value,
                ReferenceCWBottomZ = p.Counterweight.ReferenceBottomElevationMm.Value,
                CWCalibrationStatus = p.Counterweight.CalibrationStatus,
                BracketSource = p.Rails.BracketSource
            };
            d.Floors = ProjectFloors.Elevations(p.Building)
                .Select(f => new VerticalFloor { FloorId = f.FloorId, Name = f.Name, Served = f.IsServed, FflZ = f.ElevationMm })
                .ToList();
            object roofId, roofName;
            d.Floors.Add(new VerticalFloor
            {
                FloorId =
                p.Extensions.TryGetValue("legacy.roofFloorId", out roofId) ? Convert.ToString(roofId) : "structural-roof",
                Name = p.Extensions.TryGetValue("legacy.roofName", out roofName) ? Convert.ToString(roofName) : "R",
                IsRoof = true,
                Served = false,
                FflZ = 0
            });
            return d;
        }
    }
}
