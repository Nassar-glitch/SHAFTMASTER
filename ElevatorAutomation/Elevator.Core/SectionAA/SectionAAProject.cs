using System;
using System.Linq;
using System.Collections.Generic;
using Elevator.Core.Vertical;

namespace Elevator.Core.SectionAA
{
    public sealed class SectionAAProject
    {
        public int SchemaVersion = 1;
        public List<VerticalFloor> Floors = new List<VerticalFloor>();
        public string DisplayFloorId;
        public double Pit = 1000;
        public double OH = 4200;
        public double BracketMaximum = 1500;
        public double ReferenceCarZ = 0;
        public double ReferenceCWBottomZ = 12347.128322572622;
        public string CWCalibrationStatus = "PROVISIONAL_SOURCE_REFERENCE_POSE";
        public string BracketSource = "PROJECT INPUT PENDING FUTURE CALCULATION SHEET";
    }

    public sealed class SectionAAGeometry
    {
        public double PitZ;
        public double Underside;
        public double R;
        public double Ceiling;
        public double CarZ;
        public double CWBottom;
        public double Travel;
        public BracketLayout Brackets;
        public List<VerticalFloor> Floors;
        public string LayoutStatus = "PASS";
        public string RailEndAdequacy = "NOT_YET_EVALUABLE";
    }

    public static class SectionAACalculator
    {
        public static SectionAAGeometry Calculate(SectionAAProject d, double slab)
        {
            if (d == null || d.SchemaVersion != 1 || d.Floors == null || d.Floors.Count < 3 || d.Floors.Count > 101)
                throw new ArgumentException("Provide 2..100 normal floors plus structural R.");
            if (new[] { d.Pit, d.OH, d.BracketMaximum, slab }.Any(v => !FloorStackGenerator.Finite(v) || v <= 0)
                || !FloorStackGenerator.Finite(d.ReferenceCarZ)
                || !FloorStackGenerator.Finite(d.ReferenceCWBottomZ))
                throw new ArgumentException("Invalid section parameters.");
            if (d.Floors.Any(
                f => String.IsNullOrWhiteSpace(f.FloorId) || String.IsNullOrWhiteSpace(f.Name) || !FloorStackGenerator.Finite(f.FflZ))
                || d.Floors.Select(f => f.FloorId).Distinct().Count() != d.Floors.Count
                || d.Floors.Select(f => f.Name).Distinct().Count() != d.Floors.Count
                || d.Floors.Count(f => f.IsRoof) != 1
                || !d.Floors.Last().IsRoof
                || d.Floors.Last().Served)
                throw new ArgumentException("Unique floor identities/names and one final unserved R required.");
            var n = d.Floors.Where(f => !f.IsRoof).ToArray();
            var served = n.Where(f => f.Served).ToArray();
            if (served.Length < 2 || Math.Abs(n[0].FflZ) > 1e-8)
                throw new ArgumentException("At least two served floors; lowest modeled FFL must be zero.");
            for (int i = 1; i < n.Length; i++)
                if (n[i].FflZ <= n[i - 1].FflZ || (n[i - 1].Served && n[i].FflZ - n[i - 1].FflZ < 2410))
                    throw new ArgumentException("Floor interval conflicts with 2160 opening plus 250 slab.");
            var car = served.SingleOrDefault(f => f.FloorId == d.DisplayFloorId);
            if (car == null)
                throw new ArgumentException("Displayed car FloorId must be served.");
            var g = new SectionAAGeometry
            {
                PitZ = -d.Pit,
                Underside = served.Last().FflZ + d.OH,
                CarZ = car.FflZ,
                Travel = served.Last().FflZ - served.First().FflZ
            };
            g.R = g.Underside + slab;
            g.Ceiling = g.R + 2200;
            g.CWBottom = d.ReferenceCWBottomZ - (g.CarZ - d.ReferenceCarZ);
            if (n.Last().FflZ + 250 >= g.Underside || served.Last().FflZ + 2160 > g.Underside)
                throw new ArgumentException("Top modeled floor/opening conflicts with OH slab.");
            if (g.CWBottom < g.PitZ || g.CWBottom + 3000 > g.Underside)
                g.LayoutStatus = "WARNING - PROVISIONAL CW POSE OUTSIDE SHAFT";
            g.Floors = d.Floors.Select(f => new VerticalFloor
            {
                FloorId = f.FloorId,
                Name = f.Name,
                Served = f.Served,
                IsRoof = f.IsRoof,
                FflZ = f.IsRoof ? g.R : f.FflZ
            }).ToList();
            g.Brackets = BracketLayoutSolver.Solve(g.PitZ, g.Underside, d.BracketMaximum, g.Floors, 250, 2160);
            return g;
        }
    }
}
