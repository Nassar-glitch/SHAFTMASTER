using System.Collections.Generic;
using Elevator.Core.Geometry;
using Xunit;

namespace Elevator.Core.Tests
{
    // DESIGN-FLOW principle 8 (drawing quality): the dimension arrangement rules used by the automatic door plan (D3).
    public class DimensionLayoutTests
    {
        const double H = 40, Arrow = 20;

        static readonly string[] None = new string[0];

        [Fact]
        public void TextLengthAndFit()
        {
            Assert.Equal(56, DimensionLayout.TextLength("20", H), 6);
            Assert.True(DimensionLayout.Fits(200, DimensionLayout.TextLength("200", H), H));
            Assert.False(DimensionLayout.Fits(25, DimensionLayout.TextLength("25", H), H));
            Assert.Equal("OPENING 700", DimensionLayout.DisplayText("OPENING <>", 700));
            Assert.Equal("245", DimensionLayout.DisplayText("", 245));
        }

        [Fact]
        public void RowsAreEquallySpaced()
        {
            // below the wall: halves row -232.61, door row -341.80 -> the offset row is one equal step nearer
            double near = DimensionLayout.RowBefore(-232.61, -341.80);
            Assert.Equal(-123.42, near, 2);
            Assert.Equal(-232.61 - near, -341.80 + 232.61, 6);
            var rows = DimensionLayout.EqualRows(100, 110, 3);
            Assert.Equal(new[] { 100.0, 210.0, 320.0 }, rows);
        }

        [Fact]
        public void TextsThatFitStayCentred()
        {
            var p = DimensionLayout.PlaceChainTexts(new[] { 0.0, 90, 220 }, new[] { "90", "130" }, H, Arrow, null);
            Assert.All(p, x => Assert.Equal(DimensionTextSlot.Centred, x.Slot));
            Assert.False(DimensionLayout.TextsOverlap(new[] { 0.0, 90, 220 }, p, null));
        }

        [Fact]
        public void InnerSmallTextGoesBesideTheChainOnASecondLane()
        {
            // front zone 90 | 25 | 130
            var b = new[] { 0.0, 90, 115, 245 };
            var p = DimensionLayout.PlaceChainTexts(b, new[] { "90", "25", "130" }, H, Arrow, null);
            Assert.Equal(DimensionTextSlot.Centred, p[0].Slot);
            Assert.Equal(DimensionTextSlot.Beside, p[1].Slot);
            Assert.Equal(DimensionTextSlot.Centred, p[2].Slot);
            // one full text lane further than the usual text position (half a height + gap from the line)
            Assert.True(p[1].LateralMm - (H / 2 + 0.25 * H) >= H - 1e-9);
            Assert.False(DimensionLayout.TextsOverlap(b, p, null));
        }

        [Fact]
        public void FirstSmallTextGoesOutsideTheChainAndPastTheCarWall()
        {
            // returns 20 | opening 700 | 200, car wall (outer face 175, inner face 200) blocks texts
            var b = new[] { 200.0, 220, 920, 1120 };
            var blocked = new List<double[]> { new[] { 175.0, 200 }, new[] { 1120.0, 1145 } };
            var p = DimensionLayout.PlaceChainTexts(b, new[] { "20", "OPENING 700", "200" }, H, Arrow, blocked);
            Assert.Equal(DimensionTextSlot.BeforeChain, p[0].Slot);
            double centre = 210 + p[0].AlongMm;
            Assert.True(centre + p[0].LengthMm / 2 <= 175 - 0.25 * H + 1e-9);
            Assert.False(DimensionLayout.TextsOverlap(b, p, blocked));
        }

        [Fact]
        public void LastSmallTextGoesAfterTheChain()
        {
            var b = new[] { 0.0, 600, 620 };
            var p = DimensionLayout.PlaceChainTexts(b, new[] { "600", "20" }, H, Arrow, null);
            Assert.Equal(DimensionTextSlot.Centred, p[0].Slot);
            Assert.Equal(DimensionTextSlot.AfterChain, p[1].Slot);
            Assert.True(p[1].AlongMm > 0);
            Assert.False(DimensionLayout.TextsOverlap(b, p, null));
        }

        [Fact]
        public void TwoSmallEndTextsDoNotCollide()
        {
            var b = new[] { 0.0, 25, 50, 700 };
            var p = DimensionLayout.PlaceChainTexts(b, new[] { "25", "25", "650" }, H, Arrow, null);
            Assert.Equal(DimensionTextSlot.BeforeChain, p[0].Slot);
            Assert.Equal(DimensionTextSlot.Beside, p[1].Slot);
            Assert.False(DimensionLayout.TextsOverlap(b, p, null));
        }

        [Fact]
        public void ChainNeedsOneMoreBoundaryThanTexts()
        {
            Assert.Throws<System.ArgumentException>(() => DimensionLayout.PlaceChainTexts(new[] { 0.0, 10 }, new[] { "a", "b" }, H, Arrow, null));
        }
    }
}
