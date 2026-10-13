using System;
using System.Collections.Generic;
using System.Linq;
using Elevator.Core.Steel;
using Elevator.Reporting.Docx;

namespace Elevator.Reporting.Sheet
{
    /// <summary>
    /// "Shaft structure" input block of the calculation sheet (DESIGN-FLOW "Steel shaft structure"): the construction, the profiles and the
    /// beam levels of a steel shaft. Inputs only: no structural check (that is the later STEEL STRUCTURE module). A concrete shaft has no block.
    /// </summary>
    public sealed partial class CalculationSheet
    {
        /// <summary>The steel shaft of the project; null for a concrete shaft or when the structure cannot be built.</summary>
        public SteelStructure Steel { get; private set; }

        void BuildSteel()
        {
            if (Presets == null || !SteelFields.IsSteel(Project, Presets))
                return;
            var s = SteelStructure.Build(Project, Presets);
            if (s.Valid)
                Steel = s;
        }

        /// <summary>The rows of the block, label and value.</summary>
        public List<string[]> SteelRows()
        {
            var rows = new List<string[]>();
            if (Steel == null)
                return rows;
            var i = Steel.Inputs;
            rows.Add(new[] { "Shaft construction", "Steel structure (" + SteelShaftPreset.Upn + ")" });
            rows.Add(new[] { "Column profile", i.Column.Name });
            rows.Add(new[] { "Beam profile", i.Beam.Name });
            rows.Add(new[] { "Bracing profile", i.Brace.Name + ", double cross (X) in every panel" });
            rows.Add(new[] { "Beam spacing s", L(i.SpacingMm) + " mm" });
            rows.Add(new[]
            {
                "Beam levels",
                String.Join(", ", Steel.Levels.Select(l => L(l.ElevationMm))) + " mm (from the lowest floor level)"
            });
            if (Steel.DerivedBracketSpacingMm.HasValue)
                rows.Add(new[] { "Guide-rail bracket spacing (largest gap between beams)", L(Steel.DerivedBracketSpacingMm.Value) + " mm" });
            rows.Add(new[]
            {
                "Base plate",
                "PL " + L(i.PlateWidthMm) + "x" + L(i.PlateDepthMm) + "x" + L(i.PlateThicknessMm) + ", " + i.BoltCount + " bolts Ø" + L(i.BoltDiameterMm)
            });
            rows.Add(new[] { "Steel grade", i.SteelGrade });
            return rows;
        }

        void SteelBlock(DocxDocument d)
        {
            if (Steel == null)
                return;
            d.Paragraph("Shaft structure", "Heading2");
            d.Table(new[] { "Input", "Value" }, SteelRows(), TwoCol, new HashSet<int> { 0 });
        }
    }
}
