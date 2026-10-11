using Elevator.Core.Calculations;
using Elevator.Core.MachineRoom;
using Elevator.Core.SafetyGear;
using Elevator.Core.Suspension;
using Xunit;

namespace Elevator.Core.Tests
{
    // DESIGN-FLOW principle 10: kg = N / g rounded UP to the next multiple of 5 kg; an exact multiple stays; display only.
    public class LoadUnitsTests
    {
        [Fact]
        public void The_gravity_constant_is_the_calculations_constant()
        {
            Assert.Equal(ChassisCalculationEngine.GravityMetresPerSecondSquared, LoadUnits.GravityMps2);
            Assert.Equal(SafetyPitCalculationEngine.GravityMetresPerSecondSquared, LoadUnits.GravityMps2);
            Assert.Equal(RopeTractionCalculationEngine.Gn, LoadUnits.GravityMps2);
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(1.0, 5.0)]
        [InlineData(49.05, 5.0)]     // 5 kg exactly
        [InlineData(49.06, 10.0)]
        [InlineData(9810.0, 1000.0)] // 1000 kg exactly
        [InlineData(11772.0, 1200.0)] // 1200 kg exactly
        [InlineData(11781.0, 1205.0)] // 1200.9 kg -> 1205
        [InlineData(11782.0, 1205.0)] // 1201.0 kg -> 1205
        [InlineData(11781.81, 1205.0)] // 1201 kg exactly -> next multiple
        [InlineData(2200.0, 225.0)]  // FF1 2200 N = 224.3 kg
        [InlineData(2100.0, 215.0)]  // FF2 2100 N = 214.07 kg
        [InlineData(9800.0, 1000.0)] // P5 9800 N = 998.98 kg
        [InlineData(39200.0, 4000.0)] // 3995.9 kg
        [InlineData(-11781.81, -1205.0)]
        [InlineData(-11772.0, -1200.0)]
        public void Rounds_up_to_the_next_multiple_of_5_kg(double newtons, double kg)
        {
            Assert.Equal(kg, LoadUnits.Kg(newtons));
        }

        [Fact]
        public void An_exact_multiple_with_floating_noise_does_not_jump()
        {
            for (int kg = 5; kg <= 20000; kg += 5)
            {
                double n = kg * LoadUnits.GravityMps2;
                Assert.Equal((double)kg, LoadUnits.Kg(n));
                Assert.Equal((double)kg, LoadUnits.Kg(n * (1 + 1e-13)));
                Assert.Equal((double)kg + 5, LoadUnits.Kg(n + 0.01));
            }
        }

        [Fact]
        public void Text_forms()
        {
            Assert.Equal("1205", LoadUnits.KgText(11781.81));
            Assert.Equal("1205 kg", LoadUnits.KgWithUnit(11781.81));
            Assert.Equal("11.782 kN (1205 kg)", LoadUnits.Beside("11.782 kN", 11782));
            Assert.Equal("0", LoadUnits.KgText(0));
            Assert.Equal("0", LoadUnits.KgText(-0.0));
        }
    }
}
