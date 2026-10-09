using System;
using System.Linq;
using System.Collections.Generic;
namespace Elevator.Core.Project
{
    public sealed class FloorElevation
    {
        public string FloorId;
        public string Name;
        public double ElevationMm; public bool IsServed;
    }
    public static class ProjectFloors
    {
        public static bool FinitePositive(double v)
        {
            return !Double.IsNaN(v) && !Double.IsInfinity(v) && v > 0;
        }
        public static List<FloorElevation> Elevations(BuildingConfiguration building)
        {
            if (building == null || building.Floors == null)
                throw new ArgumentException("Floors are missing.");
            var result = new List<FloorElevation>();
            double z = 0;
            for (int i = 0; i < building.Floors.Count; i++)
            {
                var f = building.Floors[i];
                if (f == null || String.IsNullOrWhiteSpace(f.FloorId) || String.IsNullOrWhiteSpace(f.Name))
                    throw new ArgumentException("Floor identity and name are required.");
                result.Add(new FloorElevation { FloorId = f.FloorId, Name = f.Name, IsServed = f.IsServed, ElevationMm = z });
                if (i < building.Floors.Count - 1)
                {
                    if (!f.HeightToNextMm.HasValue || !FinitePositive(f.HeightToNextMm.Value))
                        throw new ArgumentException("Each intermediate floor requires a positive height to next floor.");
                    z += f.HeightToNextMm.Value;
                }
                else if (f.HeightToNextMm.HasValue)
                    throw new ArgumentException("Last normal floor height must be null; R derives from OH and slab thickness.");
            }
            if (result.Select(f => f.FloorId).Distinct().Count() != result.Count
                || result.Select(f => f.Name).Distinct().Count() != result.Count)
                throw new ArgumentException("Floor IDs and names must be unique.");
            return result;
        }
        public static void Rename(BuildingConfiguration b, string id, string name)
        {
            if (String.IsNullOrWhiteSpace(name) || b.Floors.Any(f => f.FloorId != id && f.Name == name))
                throw new ArgumentException("Unique floor name required.");
            b.Floors.Single(f => f.FloorId == id).Name = name;
        }
        public static void InsertBefore(BuildingConfiguration b, string beforeId, FloorDefinition floor, double height)
        {
            int index = b.Floors.FindIndex(f => f.FloorId == beforeId);
            if (index <= 0
                || floor == null
                || !FinitePositive(height)
                || String.IsNullOrWhiteSpace(floor.Name)
                || String.IsNullOrWhiteSpace(floor.FloorId)
                || b.Floors.Any(f => f.FloorId == floor.FloorId || f.Name == floor.Name))
                throw new ArgumentException("Insert a unique floor above lowest datum with a positive interval.");
            floor.HeightToNextMm = height;
            b.Floors.Insert(index, floor);
        }
        public static void Remove(BuildingConfiguration b, string id)
        {
            int index = b.Floors.FindIndex(f => f.FloorId == id);
            if (index <= 0 || index >= b.Floors.Count - 1 || b.Floors.Count <= 2)
                throw new ArgumentException("Remove an intermediate floor; retain two normal floors.");
            b.Floors.RemoveAt(index);
        }
    }
}
