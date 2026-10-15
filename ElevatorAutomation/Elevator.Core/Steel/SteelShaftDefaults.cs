using Elevator.Core.Serialization;

namespace Elevator.Core.Steel
{
    /// <summary>
    /// The shipped steel-shaft presets, used when an older config/presets.json has no SteelShaft group (the same values as the shipped
    /// file; a test compares them).
    /// </summary>
    public static class SteelShaftDefaults
    {
        const string Json = @"{
    ""Source"": ""UPN: DIN 1026-1:2000 / EN 10279:2000, Kocaer Steel UPN datasheet (kocaersteel.com/uploads/2025/02/channels-upn.pdf) and STAD technical data of sections UPN (stad.fr); angles: EN 10056-1, Engineering Toolbox / Murray Steel equal-angle tables. Rows with a Check text still need the producer value of that column."",
    ""Construction"": ""Concrete"", ""System"": ""UPN"",
    ""ColumnProfile"": ""UPN 140"", ""BeamProfile"": ""UPN 100"", ""BracingProfile"": ""L50x50x5"", ""Bracing"": ""DoubleCross"",
    ""BasePlateWidthMm"": 200, ""BasePlateDepthMm"": 200, ""BasePlateThicknessMm"": 15,
    ""BoltCount"": 4, ""BoltDiameterMm"": 20, ""BoltPitchMm"": 120,
    ""GussetLengthMm"": 400, ""GussetWidthMm"": 250, ""GussetThicknessMm"": 6,
    ""SteelGrade"": ""St 37"",
    ""SlabEdgeDepthMm"": 250, ""SlabEdgeSideMm"": 850,
    ""HousingHeightMm"": 1950, ""HousingOverhangMm"": 160, ""HousingWallMm"": 50, ""HousingRoofMm"": 50,
    ""Base"": { ""MarginMm"": 200, ""ThicknessMm"": 250, ""BarDiameterMm"": 8, ""BarSpacingMm"": 120, ""CoverMm"": 50 },
    ""Channels"": [
      { ""Name"": ""UPN 80"", ""HeightMm"": 80, ""WidthMm"": 45, ""WebMm"": 6, ""FlangeMm"": 8, ""RootRadiusMm"": 8, ""AreaCm2"": 11.0, ""MassKgPerM"": 8.64, ""IxCm4"": 106, ""WxCm3"": 26.5, ""IyCm4"": 19.4, ""WyCm3"": 6.36, ""EccentricityMm"": 14.5, ""Check"": ""e"" },
      { ""Name"": ""UPN 100"", ""HeightMm"": 100, ""WidthMm"": 50, ""WebMm"": 6, ""FlangeMm"": 8.5, ""RootRadiusMm"": 8.5, ""AreaCm2"": 13.5, ""MassKgPerM"": 10.6, ""IxCm4"": 206, ""WxCm3"": 41.2, ""IyCm4"": 29.3, ""WyCm3"": 8.49, ""EccentricityMm"": 15.5, ""Check"": ""e"" },
      { ""Name"": ""UPN 120"", ""HeightMm"": 120, ""WidthMm"": 55, ""WebMm"": 7, ""FlangeMm"": 9, ""RootRadiusMm"": 9, ""AreaCm2"": 17.0, ""MassKgPerM"": 13.4, ""IxCm4"": 364, ""WxCm3"": 60.7, ""IyCm4"": 43.2, ""WyCm3"": 11.1, ""EccentricityMm"": 16.0, ""Check"": ""e"" },
      { ""Name"": ""UPN 140"", ""HeightMm"": 140, ""WidthMm"": 60, ""WebMm"": 7, ""FlangeMm"": 10, ""RootRadiusMm"": 10, ""AreaCm2"": 20.4, ""MassKgPerM"": 16.0, ""IxCm4"": 605, ""WxCm3"": 86.4, ""IyCm4"": 62.7, ""WyCm3"": 14.8, ""EccentricityMm"": 17.5 },
      { ""Name"": ""UPN 160"", ""HeightMm"": 160, ""WidthMm"": 65, ""WebMm"": 7.5, ""FlangeMm"": 10.5, ""RootRadiusMm"": 10.5, ""AreaCm2"": 24.0, ""MassKgPerM"": 18.8, ""IxCm4"": 925, ""WxCm3"": 116, ""IyCm4"": 85.3, ""WyCm3"": 18.3, ""EccentricityMm"": 18.4, ""Check"": ""e"" },
      { ""Name"": ""UPN 180"", ""HeightMm"": 180, ""WidthMm"": 70, ""WebMm"": 8, ""FlangeMm"": 11, ""RootRadiusMm"": 11, ""AreaCm2"": 28.0, ""MassKgPerM"": 22.0, ""IxCm4"": 1350, ""WxCm3"": 150, ""IyCm4"": 114, ""WyCm3"": 22.4, ""EccentricityMm"": 19.2, ""Check"": ""e"" },
      { ""Name"": ""UPN 200"", ""HeightMm"": 200, ""WidthMm"": 75, ""WebMm"": 8.5, ""FlangeMm"": 11.5, ""RootRadiusMm"": 11.5, ""AreaCm2"": 32.2, ""MassKgPerM"": 25.3, ""IxCm4"": 1910, ""WxCm3"": 191, ""IyCm4"": 148, ""WyCm3"": 27.0, ""EccentricityMm"": 20.1, ""Check"": ""e"" }
    ],
    ""Angles"": [
      { ""Name"": ""L40x40x4"", ""LegMm"": 40, ""ThicknessMm"": 4, ""RootRadiusMm"": 6, ""ToeRadiusMm"": 3, ""AreaCm2"": 3.08, ""MassKgPerM"": 2.42, ""IxCm4"": 4.47, ""EccentricityMm"": 11.2, ""Check"": ""e, radii"" },
      { ""Name"": ""L50x50x5"", ""LegMm"": 50, ""ThicknessMm"": 5, ""RootRadiusMm"": 7, ""ToeRadiusMm"": 3.5, ""AreaCm2"": 4.80, ""MassKgPerM"": 3.77, ""IxCm4"": 11.0, ""EccentricityMm"": 14.0 },
      { ""Name"": ""L60x60x6"", ""LegMm"": 60, ""ThicknessMm"": 6, ""RootRadiusMm"": 8, ""ToeRadiusMm"": 4, ""AreaCm2"": 6.91, ""MassKgPerM"": 5.42, ""IxCm4"": 22.8, ""EccentricityMm"": 16.9, ""Check"": ""radii"" }
    ]
  }";

        /// <summary>The shipped presets, created once; callers read them and do not change them (use Create for a copy).</summary>
        public static SteelShaftPreset Shared
        {
            get
            {
                lock (gate)
                {
                    if (shared == null)
                        shared = Create();
                    return shared;
                }
            }
        }

        static readonly object gate = new object();
        static SteelShaftPreset shared;

        /// <summary>A new copy of the shipped presets.</summary>
        public static SteelShaftPreset Create()
        {
            return CoreJson.CreateWithDefaultLimit().Deserialize<SteelShaftPreset>(Json);
        }
    }
}
