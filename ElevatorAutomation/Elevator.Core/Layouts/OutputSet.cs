using System;
using System.Collections.Generic;
using System.Linq;

namespace Elevator.Core.Layouts
{
    /// <summary>What the user decides when files of the output set already exist.</summary>
    public enum OutputChoice
    {
        /// <summary>Replace the existing files (after the new ones are generated and verified).</summary>
        Overwrite,
        /// <summary>Raise the revision to the next free NN and write the set under that name.</summary>
        NextRevision,
        /// <summary>Write nothing.</summary>
        Cancel
    }

    /// <summary>The output set of one Generate Drawings run: DWG, PDF and project file under one base name, and what already exists.</summary>
    public sealed class OutputSetPlan
    {
        /// <summary>The revision the set is planned for.</summary>
        public int Revision;
        /// <summary>The three file names of that revision.</summary>
        public DrawingFileNames Names;
        /// <summary>The names of those files that already exist in the output folder (empty: nothing is replaced).</summary>
        public List<string> Existing = new List<string>();
        /// <summary>The next revision for which none of the three files exists (null: none up to 99).</summary>
        public int? NextRevision;
        /// <summary>The file names of <see cref="NextRevision"/>.</summary>
        public DrawingFileNames NextNames;

        /// <summary>True when any of the three files exists and the user has to decide.</summary>
        public bool Conflict
        {
            get { return Existing.Count > 0; }
        }
    }

    /// <summary>
    /// The decision about existing output files, as a pure function (nothing is replaced silently): the set is free, or the user chooses
    /// to overwrite it or to raise the revision to the next NN whose three files do not exist.
    /// </summary>
    public static class OutputSet
    {
        /// <summary>The highest revision a file name can carry (two digits).</summary>
        public const int MaximumRevision = 99;

        /// <summary>Plans the set of a revision. <paramref name="baseNameOf"/> gives the base name of a revision, <paramref name="exists"/> says whether a file name exists in the output folder.</summary>
        public static OutputSetPlan Plan(int revision, Func<int, string> baseNameOf, Func<string, bool> exists)
        {
            if (baseNameOf == null)
                throw new ArgumentNullException("baseNameOf");
            if (exists == null)
                throw new ArgumentNullException("exists");
            if (revision < 0 || revision > MaximumRevision)
                throw new ArgumentOutOfRangeException("revision", "The revision must be 0 to 99.");
            var plan = new OutputSetPlan { Revision = revision, Names = DrawingFileName.Files(baseNameOf(revision)) };
            foreach (string name in new[] { plan.Names.Dwg, plan.Names.Pdf, plan.Names.Project })
                if (exists(name))
                    plan.Existing.Add(name);
            if (plan.Conflict)
            {
                for (int r = revision + 1; r <= MaximumRevision; r++)
                {
                    var names = DrawingFileName.Files(baseNameOf(r));
                    if (!exists(names.Dwg) && !exists(names.Pdf) && !exists(names.Project))
                    {
                        plan.NextRevision = r;
                        plan.NextNames = names;
                        break;
                    }
                }
            }
            return plan;
        }

        /// <summary>
        /// The revision and file names to write for a choice: the planned ones when the set is free or for Overwrite, the next free
        /// revision for NextRevision. Cancel and an unavailable next revision give null (nothing is written).
        /// </summary>
        public static Tuple<int, DrawingFileNames> Resolve(OutputSetPlan plan, OutputChoice choice)
        {
            if (plan == null)
                throw new ArgumentNullException("plan");
            if (!plan.Conflict)
                return Tuple.Create(plan.Revision, plan.Names);
            switch (choice)
            {
                case OutputChoice.Overwrite:
                    return Tuple.Create(plan.Revision, plan.Names);
                case OutputChoice.NextRevision:
                    return plan.NextRevision.HasValue ? Tuple.Create(plan.NextRevision.Value, plan.NextNames) : null;
                default:
                    return null;
            }
        }
    }
}
