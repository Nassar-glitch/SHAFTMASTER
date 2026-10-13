using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elevator.Core.Project;

namespace Elevator.Core.Steel
{
    /// <summary>What a beam level is for (DESIGN-FLOW "Steel shaft structure", answer 1).</summary>
    public enum SteelLevelKind
    {
        /// <summary>A beam at a floor level (fixed: the doors are fixed to the structure).</summary>
        Floor,
        /// <summary>The header beam at the door construction height above a floor (fixed).</summary>
        DoorHead,
        /// <summary>A beam between a door head and the next floor beam, in steps of the beam spacing.</summary>
        Intermediate,
        /// <summary>A beam in the pit, from the base up to the lowest floor beam.</summary>
        Pit,
        /// <summary>A beam above the top-floor door head, up to the tower top (the last one is the machine platform).</summary>
        Top
    }

    /// <summary>One beam level. Elevations are measured from the lowest floor (the pit is negative), as ProjectFloors does.</summary>
    public sealed class SteelLevel
    {
        /// <summary>Identity of the level for edits: F:name, D:name, I:name:k, P:k, T:k, T:top, or C:n for a level the user added.</summary>
        public string Key;
        public SteelLevelKind Kind;
        public double ElevationMm;
        /// <summary>The floor the level belongs to (floor, door head, intermediate); null in the pit and at the top.</summary>
        public string FloorName;
        /// <summary>True for a level the user edited or added (locked); false for a level the rule gives.</summary>
        public bool Edited;

        public string Tag
        {
            get
            {
                switch (Kind)
                {
                    case SteelLevelKind.Floor: return "floor";
                    case SteelLevelKind.DoorHead: return "door head";
                    case SteelLevelKind.Intermediate: return "intermediate";
                    case SteelLevelKind.Pit: return "pit";
                    default: return "top";
                }
            }
        }

        public SteelLevel Clone()
        {
            return (SteelLevel)MemberwiseClone();
        }
    }

    /// <summary>A user edit of the level list: a moved level, a removed one, or an added one (key C:n). An edited level is locked.</summary>
    public sealed class SteelLevelEdit
    {
        public string Key;
        public double? ElevationMm;
        public bool Removed;
    }

    /// <summary>
    /// The beam-level rule of the steel shaft, per face (DESIGN-FLOW "Steel shaft structure", answer 1). s is the effective car rail
    /// bracket spacing of the project (the rails drive the beams). Side and back faces: a beam at every floor level; each gap between
    /// floor beams, the pit (base to the lowest floor) and the top (top floor to the tower top) is divided equally into the fewest parts
    /// none larger than s. Entrance face: a floor beam and a door-head beam at every floor; the floor-to-door-head gap is the opening and
    /// is not split; from the door head to the next floor beam (or the tower top) the gap is divided equally into the fewest parts none
    /// larger than s; the pit as on the other faces. The tower top carries the last beam (the machine platform).
    /// </summary>
    public static class SteelBeamLevels
    {
        public const double Epsilon = 1e-6;

        static string N(double v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>The rule for floors at given elevations (served floors, named "0", "1", ...), pure and without a project.</summary>
        public static List<SteelLevel> Rule(IList<double> floorElevationsMm, double doorHeightMm, double spacingMm, double baseMm, double topMm, bool entranceFace)
        {
            if (floorElevationsMm == null)
                throw new ArgumentException("Floors are required.");
            var floors = new List<FloorElevation>();
            for (int i = 0; i < floorElevationsMm.Count; i++)
            {
                floors.Add(new FloorElevation { FloorId = i.ToString(CultureInfo.InvariantCulture), Name = i.ToString(CultureInfo.InvariantCulture), ElevationMm = floorElevationsMm[i], IsServed = true });
            }
            return Rule(floors, doorHeightMm, spacingMm, baseMm, topMm, entranceFace);
        }

        /// <summary>The rule for the served floors of a building on one face (entranceFace: the face with the doors).</summary>
        public static List<SteelLevel> Rule(IList<FloorElevation> floors, double doorHeightMm, double spacingMm, double baseMm, double topMm, bool entranceFace)
        {
            var served = Served(floors);
            Check(served, doorHeightMm, spacingMm, baseMm, topMm);
            var levels = new List<SteelLevel>();
            int k = 0;
            foreach (double y in Divide(baseMm, served[0].ElevationMm, spacingMm))
                levels.Add(new SteelLevel { Key = "P:" + (++k).ToString(CultureInfo.InvariantCulture), Kind = SteelLevelKind.Pit, ElevationMm = y });
            for (int i = 0; i < served.Count; i++)
            {
                var f = served[i];
                bool last = i == served.Count - 1;
                double next = last ? topMm : served[i + 1].ElevationMm;
                levels.Add(new SteelLevel { Key = "F:" + f.Name, Kind = SteelLevelKind.Floor, ElevationMm = f.ElevationMm, FloorName = f.Name });
                double from = f.ElevationMm;
                double head = f.ElevationMm + doorHeightMm;
                if (entranceFace && head < next - Epsilon)
                {
                    levels.Add(new SteelLevel { Key = "D:" + f.Name, Kind = SteelLevelKind.DoorHead, ElevationMm = head, FloorName = f.Name });
                    from = head;
                }
                k = 0;
                foreach (double y in Divide(from, next, spacingMm))
                {
                    k++;
                    levels.Add(last
                        ? new SteelLevel { Key = "T:" + k.ToString(CultureInfo.InvariantCulture), Kind = SteelLevelKind.Top, ElevationMm = y }
                        : new SteelLevel { Key = "I:" + f.Name + ":" + k.ToString(CultureInfo.InvariantCulture), Kind = SteelLevelKind.Intermediate, ElevationMm = y, FloorName = f.Name });
                }
            }
            levels.Add(new SteelLevel { Key = "T:top", Kind = SteelLevelKind.Top, ElevationMm = topMm });
            return levels;
        }

        // The beams strictly between from and to when the gap is divided equally into the fewest parts none larger than s.
        static IEnumerable<double> Divide(double from, double to, double s)
        {
            double gap = to - from;
            int parts = (int)Math.Ceiling(gap / s - Epsilon);
            if (parts < 1)
                parts = 1;
            for (int j = 1; j < parts; j++)
                yield return from + gap * j / parts;
        }
        static List<FloorElevation> Served(IList<FloorElevation> floors)
        {
            if (floors == null)
                throw new ArgumentException("Floors are required.");
            return floors.Where(f => f != null && f.IsServed).OrderBy(f => f.ElevationMm).ToList();
        }

        static void Check(List<FloorElevation> served, double door, double s, double baseMm, double topMm)
        {
            if (served.Count < 1)
                throw new ArgumentException("At least one served floor is required.");
            if (!ProjectFloors.FinitePositive(door))
                throw new ArgumentException("The door construction height must be positive.");
            if (!ProjectFloors.FinitePositive(s))
                throw new ArgumentException("The beam spacing must be positive.");
            if (Double.IsNaN(baseMm) || Double.IsInfinity(baseMm) || baseMm >= served[0].ElevationMm - Epsilon)
                throw new ArgumentException("The base must be below the lowest floor.");
            if (Double.IsNaN(topMm) || Double.IsInfinity(topMm) || topMm <= served[served.Count - 1].ElevationMm + Epsilon)
                throw new ArgumentException("The tower top must be above the top floor.");
            for (int i = 1; i < served.Count; i++)
            {
                if (served[i].ElevationMm - served[i - 1].ElevationMm <= Epsilon)
                    throw new ArgumentException("Served floors must have increasing elevations.");
            }
            if ((topMm - baseMm) / s > 100000)
                throw new ArgumentException("The beam spacing gives more than 100000 beam levels.");
        }

        /// <summary>
        /// The rule the levels cannot meet: a door head that does not fit under the next floor beam (a gap at a door is by design, not an issue).
        /// Advisory text for the Design Notes; empty when the rule is met.
        /// </summary>
        public static List<string> Issues(IList<FloorElevation> floors, double doorHeightMm, double spacingMm, double baseMm, double topMm)
        {
            var issues = new List<string>();
            var served = Served(floors);
            Check(served, doorHeightMm, spacingMm, baseMm, topMm);
            for (int i = 0; i < served.Count; i++)
            {
                double next = i == served.Count - 1 ? topMm : served[i + 1].ElevationMm;
                if (served[i].ElevationMm + doorHeightMm >= next - Epsilon)
                {
                    issues.Add("At floor " + served[i].Name + " the door-head beam (" + N(doorHeightMm) + " mm above the floor) does not fit under the "
                        + (i == served.Count - 1 ? "tower top" : "next floor beam") + ", " + N(next - served[i].ElevationMm) + " mm above the floor.");
                }
            }
            return issues;
        }

        /// <summary>
        /// Applies the user's edits to the levels of the rule. A floor level is fixed by the building and never edited. An edit of an
        /// unknown key is ignored (the rule no longer has that level), except an added level (C:n). The result is ordered by elevation
        /// and every edited level is marked Edited.
        /// </summary>
        public static List<SteelLevel> Apply(IList<SteelLevel> rule, IList<SteelLevelEdit> edits, double baseMm, double topMm)
        {
            var result = rule.Select(l => l.Clone()).ToList();
            if (edits != null)
            {
                foreach (var e in edits)
                {
                    if (e == null || String.IsNullOrEmpty(e.Key))
                        continue;
                    var level = result.FirstOrDefault(l => l.Key == e.Key);
                    if (level == null)
                    {
                        if (e.Key.StartsWith("C:", StringComparison.Ordinal) && !e.Removed && e.ElevationMm.HasValue)
                            result.Add(new SteelLevel { Key = e.Key, Kind = SteelLevelKind.Intermediate, ElevationMm = e.ElevationMm.Value, Edited = true });
                        continue;
                    }
                    if (level.Kind == SteelLevelKind.Floor || e.Key == "T:top")
                        continue;
                    if (e.Removed)
                    {
                        result.Remove(level);
                        continue;
                    }
                    if (e.ElevationMm.HasValue)
                    {
                        level.ElevationMm = e.ElevationMm.Value;
                        level.Edited = true;
                    }
                }
            }
            result = result
                .Where(l => !Double.IsNaN(l.ElevationMm) && !Double.IsInfinity(l.ElevationMm) && l.ElevationMm > baseMm + Epsilon && l.ElevationMm <= topMm + Epsilon)
                .OrderBy(l => l.ElevationMm).ThenBy(l => l.Kind == SteelLevelKind.Floor ? 0 : 1)
                .ToList();
            // Two beams cannot share an elevation: the first (a fixed one sorts first) stays.
            for (int i = result.Count - 1; i > 0; i--)
            {
                if (result[i].ElevationMm - result[i - 1].ElevationMm < Epsilon)
                    result.RemoveAt(i);
            }
            return result;
        }

        /// <summary>The largest gap between consecutive levels from the lowest to the highest served floor: the rail bracket spacing of a steel shaft.</summary>
        public static double LargestGap(IList<SteelLevel> levels, double fromMm, double toMm)
        {
            double gap = 0, previous = Double.NaN;
            foreach (var l in levels.Where(x => x.ElevationMm >= fromMm - Epsilon && x.ElevationMm <= toMm + Epsilon).OrderBy(x => x.ElevationMm))
            {
                if (!Double.IsNaN(previous))
                    gap = Math.Max(gap, l.ElevationMm - previous);
                previous = l.ElevationMm;
            }
            return gap;
        }
    }
}
