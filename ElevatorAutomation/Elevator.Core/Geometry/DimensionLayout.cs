using System;
using System.Collections.Generic;
using System.Globalization;

namespace Elevator.Core.Geometry
{
    /// <summary>Where the text of one dimension of a chain sits relative to its interval.</summary>
    public enum DimensionTextSlot
    {
        /// <summary>On the dimension line, centred between the extension lines (the text fits).</summary>
        Centred,
        /// <summary>On the dimension line, outside the chain, before its first extension line.</summary>
        BeforeChain,
        /// <summary>On the dimension line, outside the chain, after its last extension line.</summary>
        AfterChain,
        /// <summary>Centred along the interval but moved away from the dimension line, beside the chain.</summary>
        Beside
    }

    /// <summary>The placement of the text of one dimension of a chain.</summary>
    public sealed class DimensionTextPlacement
    {
        /// <summary>Slot of the text.</summary>
        public DimensionTextSlot Slot;

        /// <summary>Distance of the text centre from the centre of its interval, along the dimension line, mm.</summary>
        public double AlongMm;

        /// <summary>
        /// For a text beside the chain: distance of the text centre from the dimension line, perpendicular to it, away from the
        /// extension lines (the usual text centre is half a text height plus a gap from the line, so this is one text lane further);
        /// zero for the other slots, mm.
        /// </summary>
        public double LateralMm;

        /// <summary>
        /// Distance by which this dimension's own line is moved from the chain line, towards the geometry (shorter extension lines), mm.
        /// Used when the first or last dimension of a chain is stepped out of the chain so that the small text of its neighbour
        /// can stand outside the chain (the user's 2C correction): zero for a dimension that stays on the chain line.
        /// </summary>
        public double LineOffsetMm;

        /// <summary>Estimated text length along the dimension line, mm.</summary>
        public double LengthMm;

        /// <summary>Estimated text height (the text box across the dimension line), mm.</summary>
        public double HeightMm;
    }

    /// <summary>
    /// Arrangement rules of the plan dimensions (DESIGN-FLOW principle 8, "Drawing quality"): chained dimensions on one dimension line,
    /// successive rows and columns at an equal spacing, and a text that does not fit between its extension lines placed outside the
    /// chain or beside it, never on top of a neighbouring text, tick or line. Pure geometry (millimetres), no drawing API.
    /// </summary>
    public static class DimensionLayout
    {
        /// <summary>Average character width as a fraction of the text height (conservative for capitals and digits).</summary>
        public const double CharWidthFactor = 0.7;

        /// <summary>Clearance between a text and its neighbours as a fraction of the text height.</summary>
        public const double GapFactor = 0.25;

        /// <summary>Spacing of two dimension columns / rows created by the automatic door drawing, mm.</summary>
        public const double ColumnPitchMm = 110;

        /// <summary>Step of a dimension stepped out of a chain, as a multiple of the text height (a text lane plus clearance).</summary>
        public const double StepFactor = 1.5;

        /// <summary>The estimated length of a dimension text along its dimension line.</summary>
        public static double TextLength(string text, double textHeight)
        {
            return (text ?? "").Length * CharWidthFactor * textHeight;
        }

        /// <summary>The displayed text of a dimension: the template with "&lt;&gt;" replaced by the measurement, or the measurement.</summary>
        public static string DisplayText(string template, double measurement)
        {
            string value = Math.Round(measurement, 2).ToString("0.##", CultureInfo.InvariantCulture);
            if (String.IsNullOrEmpty(template))
                return value;
            return template.Replace("<>", value);
        }

        /// <summary>True when the text fits between the extension lines of an interval with its clearance on both sides.</summary>
        public static bool Fits(double interval, double textLength, double textHeight)
        {
            return textLength + 2 * GapFactor * textHeight <= interval + 1e-9;
        }

        /// <summary>The row one step nearer to the geometry than <paramref name="nearRow"/>, at the spacing of the two given rows.</summary>
        public static double RowBefore(double nearRow, double farRow)
        {
            return nearRow + (nearRow - farRow);
        }

        /// <summary>Rows at an equal spacing: first, first + step, ...</summary>
        public static double[] EqualRows(double first, double step, int count)
        {
            var rows = new double[count];
            for (int i = 0; i < count; i++)
                rows[i] = first + step * i;
            return rows;
        }

        /// <summary>
        /// Places the texts of a chain of contiguous dimensions on one dimension line. <paramref name="boundaries"/> are the n + 1
        /// ascending extension line positions along the line, <paramref name="texts"/> the n displayed texts. A text that fits stays
        /// centred; one that does not goes outside the chain on the line (first and last interval, past any blocked range such as a
        /// car wall), or, for an inner interval, beside the chain on a second lane. <paramref name="blocked"/> are ranges
        /// {from, to} along the line that texts must not cover.
        /// </summary>
        public static DimensionTextPlacement[] PlaceChainTexts(double[] boundaries, string[] texts, double textHeight, double arrowSize, IList<double[]> blocked)
        {
            int n = texts.Length;
            if (boundaries.Length != n + 1)
                throw new ArgumentException("A chain of n dimensions has n + 1 extension line positions.");
            double gap = GapFactor * textHeight;
            var fits = new bool[n];
            for (int i = 0; i < n; i++)
                fits[i] = Fits(boundaries[i + 1] - boundaries[i], TextLength(texts[i], textHeight), textHeight);
            var result = new DimensionTextPlacement[n];
            for (int i = 0; i < n; i++)
                result[i] = new DimensionTextPlacement { Slot = DimensionTextSlot.Centred, LengthMm = TextLength(texts[i], textHeight), HeightMm = textHeight };
            // First of the chain (or last) stepped out when its neighbour is the only inner interval that is too small: the small
            // text then stands outside the shortened chain, on the line, beside the stepped-out dimension.
            int first = 0, last = n - 1;
            if (n >= 3)
            {
                int small = -1, count = 0;
                for (int i = 1; i < n - 1; i++)
                {
                    if (!fits[i])
                    {
                        small = i;
                        count++;
                    }
                }
                if (count == 1 && small == 1 && fits[0])
                {
                    result[0].LineOffsetMm = StepFactor * textHeight;
                    first = 1;
                }
                else if (count == 1 && small == n - 2 && fits[n - 1])
                {
                    result[n - 1].LineOffsetMm = StepFactor * textHeight;
                    last = n - 2;
                }
            }
            double clear = arrowSize * 0.5 + gap;
            bool beforeTaken = false, afterTaken = false;
            for (int i = first; i <= last; i++)
            {
                var p = result[i];
                if (fits[i])
                    continue;
                double centre = (boundaries[i] + boundaries[i + 1]) / 2;
                if (i == first && !beforeTaken)
                {
                    double c = boundaries[first] - clear - p.LengthMm / 2;
                    c = ClearOf(c, p.LengthMm, gap, -1, blocked);
                    p.Slot = DimensionTextSlot.BeforeChain;
                    p.AlongMm = c - centre;
                    beforeTaken = true;
                }
                else if (i == last && !afterTaken)
                {
                    double c = boundaries[last + 1] + clear + p.LengthMm / 2;
                    c = ClearOf(c, p.LengthMm, gap, +1, blocked);
                    p.Slot = DimensionTextSlot.AfterChain;
                    p.AlongMm = c - centre;
                    afterTaken = true;
                }
                else
                {
                    p.Slot = DimensionTextSlot.Beside;
                    p.LateralMm = 1.5 * textHeight + gap;
                }
            }
            return result;
        }

        // Moves a text centre along the line, in the given direction, until it covers none of the blocked ranges.
        static double ClearOf(double centre, double length, double gap, int direction, IList<double[]> blocked)
        {
            if (blocked == null)
                return centre;
            for (int pass = 0; pass <= blocked.Count; pass++)
            {
                bool moved = false;
                foreach (var range in blocked)
                {
                    double lo = Math.Min(range[0], range[1]), hi = Math.Max(range[0], range[1]);
                    if (centre + length / 2 + gap > lo && centre - length / 2 - gap < hi)
                    {
                        centre = direction < 0 ? lo - gap - length / 2 : hi + gap + length / 2;
                        moved = true;
                    }
                }
                if (!moved)
                    break;
            }
            return centre;
        }

        /// <summary>
        /// True when the text boxes of a chain overlap one another or a blocked range. A box is the text length along the line by the
        /// text height across it; a text on the second lane is separated from the first lane by its lateral offset.
        /// </summary>
        public static bool TextsOverlap(double[] boundaries, DimensionTextPlacement[] placement, IList<double[]> blocked)
        {
            int n = placement.Length;
            var from = new double[n];
            var to = new double[n];
            var lane = new double[n];
            for (int i = 0; i < n; i++)
            {
                double centre = (boundaries[i] + boundaries[i + 1]) / 2 + placement[i].AlongMm;
                from[i] = centre - placement[i].LengthMm / 2;
                to[i] = centre + placement[i].LengthMm / 2;
                // Distance of the text centre from the dimension line: a text on the line sits half its height plus a gap above it.
                lane[i] = (placement[i].LateralMm > 0 ? placement[i].LateralMm : placement[i].HeightMm / 2 + GapFactor * placement[i].HeightMm) - placement[i].LineOffsetMm;
                if (blocked != null)
                {
                    foreach (var range in blocked)
                    {
                        // A blocked range is a line across the lanes, so it blocks a text on any lane.
                        if (to[i] > Math.Min(range[0], range[1]) + 1e-9 && from[i] < Math.Max(range[0], range[1]) - 1e-9)
                            return true;
                    }
                }
            }
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    bool alongOverlap = to[i] > from[j] + 1e-9 && to[j] > from[i] + 1e-9;
                    bool laneOverlap = Math.Abs(lane[i] - lane[j]) < Math.Max(placement[i].HeightMm, placement[j].HeightMm);
                    if (alongOverlap && laneOverlap)
                        return true;
                }
            }
            return false;
        }
    }
}
