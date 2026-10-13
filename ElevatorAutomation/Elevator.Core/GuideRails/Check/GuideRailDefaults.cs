using Elevator.Core.Serialization;

namespace Elevator.Core.GuideRails
{
    /// <summary>
    /// The shipped guide-rail presets, used when an older config/presets.json has no GuideRails group (the same values as the shipped
    /// file; a test compares them).
    /// </summary>
    public static class GuideRailDefaults
    {
        const string Json = @"{
    ""RailCount"": 2,
    ""ElasticModulusNmm2"": 210000,
    ""MaxSpacingMm"": 2000, ""SpacingStepMm"": 50, ""MinSpacingMm"": 500,
    ""MachineOnRails"": false,
    ""StructureDeflectionXMm"": 0, ""StructureDeflectionYMm"": 0,
    ""RunningImpactFactor"": 1.2, ""AuxiliaryImpactFactor"": 1.2,
    ""ProgressiveK1"": 2, ""InstantaneousK1"": 5,
    ""SillForceFactor"": 0.4,
    ""CwShoeSpacingMm"": 2000, ""CwEccentricityWidthPercent"": 5, ""CwEccentricityDepthPercent"": 10,
    ""ClipPushThroughN"": 1000, ""PushThroughMaxTravelM"": 40,
    ""CarDeflectionLimitMm"": 5, ""CwDeflectionLimitMm"": 10,
    ""MaxSlenderness"": 250, ""MinSlenderness"": 20,
    ""Profiles"": [
      { ""Name"": ""T50/50/5"", ""HeightMm"": 50, ""WidthMm"": 50, ""BladeMm"": 5, ""NeckMm"": 5, ""FootMm"": 5, ""AreaMm2"": 475, ""MassKgPerM"": 3.73,
        ""IxxMm4"": 112400, ""WxxMm3"": 3150, ""IxMm"": 15.4, ""IyyMm4"": 52500, ""WyyMm3"": 2100, ""IyMm"": 10.5,
        ""RmNmm2"": 370, ""A5Band"": ""Over12"", ""ShoeType"": ""Sliding"", ""ShoeLiningLengthMm"": 60, ""ShoeHalfWidthMm"": 19 },
      { ""Name"": ""T70/65/9"", ""HeightMm"": 65, ""WidthMm"": 70, ""BladeMm"": 9, ""NeckMm"": 6, ""FootMm"": 8, ""AreaMm2"": 951, ""MassKgPerM"": 7.47,
        ""IxxMm4"": 413000, ""WxxMm3"": 9240, ""IxMm"": 20.9, ""IyyMm4"": 186500, ""WyyMm3"": 5350, ""IyMm"": 14.0,
        ""RmNmm2"": 370, ""A5Band"": ""Over12"", ""ShoeType"": ""Sliding"", ""ShoeLiningLengthMm"": 135, ""ShoeHalfWidthMm"": 19 },
      { ""Name"": ""T89/62/16"", ""HeightMm"": 62, ""WidthMm"": 89, ""BladeMm"": 16, ""NeckMm"": 10, ""FootMm"": 11, ""AreaMm2"": 1570, ""MassKgPerM"": 12.3,
        ""IxxMm4"": 595200, ""WxxMm3"": 14250, ""IxMm"": 19.5, ""IyyMm4"": 524000, ""WyyMm3"": 11800, ""IyMm"": 18.3,
        ""RmNmm2"": 370, ""A5Band"": ""Over12"", ""ShoeType"": ""Sliding"", ""ShoeLiningLengthMm"": 140, ""ShoeHalfWidthMm"": 19 },
      { ""Name"": ""T90/75/16"", ""HeightMm"": 75, ""WidthMm"": 90, ""BladeMm"": 16, ""NeckMm"": 10, ""FootMm"": 10, ""AreaMm2"": 1730, ""MassKgPerM"": 13.55,
        ""IxxMm4"": 1020000, ""WxxMm3"": 20870, ""IxMm"": 24.3, ""IyyMm4"": 530000, ""WyyMm3"": 11800, ""IyMm"": 17.5,
        ""RmNmm2"": 370, ""A5Band"": ""Over12"", ""ShoeType"": ""Sliding"", ""ShoeLiningLengthMm"": 140, ""ShoeHalfWidthMm"": 19 }
    ]
  }";

        /// <summary>A new copy of the shipped presets.</summary>
        public static GuideRailPreset Create()
        {
            return CoreJson.CreateWithDefaultLimit().Deserialize<GuideRailPreset>(Json);
        }
    }
}
