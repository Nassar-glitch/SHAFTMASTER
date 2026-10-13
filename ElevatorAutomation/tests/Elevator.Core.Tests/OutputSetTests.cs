using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Layouts;
using Xunit;

namespace Elevator.Core.Tests
{
    // L3: the decision about existing output files (DWG, PDF, project file under one base name) as a pure function.
    public class OutputSetTests
    {
        static string Base(int r)
        {
            return DrawingFileName.BaseName("NILE LIFTS", ElevatorSystemType.GB, CounterweightPositionCode.RCW, 1600, 1800, "Villa bx12", r);
        }

        static Func<string, bool> Existing(params string[] names)
        {
            var set = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            return n => set.Contains(n);
        }

        [Fact]
        public void A_free_set_has_no_conflict_and_the_three_files_share_one_base_name()
        {
            var plan = OutputSet.Plan(0, Base, Existing());
            Assert.False(plan.Conflict);
            Assert.Equal("NILE LIFTS-GB-RCW-W1600D1800-Villa bx12-MODEL00", plan.Names.BaseName);
            Assert.Equal(plan.Names.BaseName + ".dwg", plan.Names.Dwg);
            Assert.Equal(plan.Names.BaseName + ".pdf", plan.Names.Pdf);
            Assert.Equal(plan.Names.BaseName + ".elevator.json", plan.Names.Project);
            Assert.Null(plan.NextRevision);
            var r = OutputSet.Resolve(plan, OutputChoice.Overwrite);
            Assert.Equal(0, r.Item1);
        }

        [Fact]
        public void Any_one_existing_file_is_a_conflict_and_offers_the_next_free_revision()
        {
            foreach (string which in new[] { ".dwg", ".pdf", ".elevator.json" })
            {
                var plan = OutputSet.Plan(0, Base, Existing(Base(0) + which));
                Assert.True(plan.Conflict, which);
                Assert.Equal(new[] { Base(0) + which }, plan.Existing.ToArray());
                Assert.Equal(1, plan.NextRevision);
                Assert.EndsWith("MODEL01.dwg", plan.NextNames.Dwg);
            }
        }

        [Fact]
        public void The_next_revision_skips_revisions_that_already_have_a_file()
        {
            var plan = OutputSet.Plan(0, Base, Existing(Base(0) + ".dwg", Base(1) + ".pdf", Base(2) + ".elevator.json"));
            Assert.Equal(3, plan.NextRevision);
            var chosen = OutputSet.Resolve(plan, OutputChoice.NextRevision);
            Assert.Equal(3, chosen.Item1);
            Assert.EndsWith("-MODEL03.pdf", chosen.Item2.Pdf);
        }

        [Fact]
        public void Overwrite_keeps_the_names_cancel_writes_nothing_and_revision_99_has_no_next()
        {
            var plan = OutputSet.Plan(5, Base, Existing(Base(5) + ".dwg"));
            Assert.Equal(5, OutputSet.Resolve(plan, OutputChoice.Overwrite).Item1);
            Assert.Null(OutputSet.Resolve(plan, OutputChoice.Cancel));
            var last = OutputSet.Plan(99, Base, Existing(Base(99) + ".dwg"));
            Assert.True(last.Conflict);
            Assert.Null(last.NextRevision);
            Assert.Null(OutputSet.Resolve(last, OutputChoice.NextRevision));
            Assert.Throws<ArgumentOutOfRangeException>(() => OutputSet.Plan(100, Base, Existing()));
        }
    }
}
