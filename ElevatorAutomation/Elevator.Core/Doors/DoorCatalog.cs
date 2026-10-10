using System;
using System.Collections.Generic;
using System.Linq;

namespace Elevator.Core.Doors
{
    /// <summary>
    /// The door catalog (config/presets.json "Doors"): standard clear openings and the clearance presets of the automatic door types,
    /// taken from the reference drawings. Company practice changes in the file, not in code.
    /// </summary>
    public sealed class DoorCatalog
    {
        /// <summary>Standard clear openings, mm.</summary>
        public List<double> OpeningsMm;

        /// <summary>EN 81-20 5.3.6.2.2.1: largest horizontal gap between car and landing sill.</summary>
        public double? MaxSillGapMm;

        /// <summary>EN 81-20 5.2.5.2.1: largest distance from the shaft front wall to the car door.</summary>
        public double? MaxCarToWallMm;

        /// <summary>The side of the speed governor ("Left" in the reference drawings); 2S stacks on the opposite side.</summary>
        public string GovernorSide;

        public DoorTypePreset Automatic2S;
        public DoorTypePreset Automatic2C;
        public DoorEnvelopes Envelopes;

        /// <summary>The preset of an automatic type, or null.</summary>
        public DoorTypePreset For(string type)
        {
            switch (DoorTypes.Normalize(type))
            {
                case DoorTypes.Automatic2S:
                    return Automatic2S;
                case DoorTypes.Automatic2C:
                    return Automatic2C;
                default:
                    return null;
            }
        }

        static DoorTypePreset Build(bool sideOpening, IEnumerable<double> openings)
        {
            var t = new DoorTypePreset
            {
                LandingWallClearanceMm = 25,
                MinFixedReturnMm = 0,
                MinStackReturnMm = 0,
                LandingFixedJambMinMm = 0,
                Sizes = new List<DoorSizePreset>()
            };
            if (sideOpening)
            {
                t.CarZoneMm = 130;
                t.SillGapMm = 25;
                t.LandingZoneMm = 90;
                t.PanelThicknessMm = 40;
                t.PanelOverlapMm = 10;
                t.PanelEdgeOvertravelMm = 5;
            }
            else
            {
                t.CarZoneMm = 110;
                t.SillGapMm = 30;
                t.LandingZoneMm = 80;
                t.PanelThicknessMm = 39;
                t.PanelOverlapMm = 2;
                t.PanelEdgeOvertravelMm = 0;
            }
            foreach (double o in openings)
            {
                if (sideOpening)
                {
                    t.Sizes.Add(new DoorSizePreset
                    {
                        OpeningMm = o,
                        FixedReturnMm = 200,
                        StackReturnMm = o / 2 - 50,
                        LandingFixedJambMm = 30,
                        LandingStackPocketMm = o / 2 + 25
                    });
                }
                else
                {
                    t.Sizes.Add(new DoorSizePreset
                    {
                        OpeningMm = o,
                        FixedReturnMm = o / 4 + 75,
                        StackReturnMm = o / 4 + 75,
                        LandingFixedJambMm = o / 2 + 25,
                        LandingStackPocketMm = o / 2 + 25
                    });
                }
            }
            return t;
        }

        /// <summary>The catalog shipped in config/presets.json; used when a preset library has no "Doors" section.</summary>
        public static DoorCatalog Default()
        {
            var openings = new List<double> { 700, 800, 900, 1000, 1200 };
            return new DoorCatalog
            {
                OpeningsMm = openings,
                MaxSillGapMm = 35,
                MaxCarToWallMm = 150,
                GovernorSide = DoorTypes.Left,
                Automatic2S = Build(true, openings),
                Automatic2C = Build(false, openings),
                Envelopes = new DoorEnvelopes
                {
                    RailDepthMm = 70,
                    RailWidthMm = 65,
                    GovernorGapToWallMm = 79,
                    GovernorWidthMm = 26,
                    GovernorFromGuideAxisMm = 159,
                    GovernorLengthMm = 240
                }
            };
        }

        static bool Positive(double? v)
        {
            return v.HasValue && !Double.IsNaN(v.Value) && !Double.IsInfinity(v.Value) && v.Value > 0;
        }

        static bool NonNegative(double? v)
        {
            return v.HasValue && !Double.IsNaN(v.Value) && !Double.IsInfinity(v.Value) && v.Value >= 0;
        }

        /// <summary>The problems of the catalog; empty when it is valid.</summary>
        public List<string> Validate()
        {
            var p = new List<string>();
            if (OpeningsMm == null || OpeningsMm.Count == 0 || OpeningsMm.Any(o => Double.IsNaN(o) || o <= 0))
                p.Add("Doors.OpeningsMm needs positive sizes");
            else if (OpeningsMm.Distinct().Count() != OpeningsMm.Count)
                p.Add("Doors.OpeningsMm must be unique");
            if (!Positive(MaxSillGapMm))
                p.Add("Doors.MaxSillGapMm must be positive");
            if (!Positive(MaxCarToWallMm))
                p.Add("Doors.MaxCarToWallMm must be positive");
            if (GovernorSide != DoorTypes.Left && GovernorSide != DoorTypes.Right)
                p.Add("Doors.GovernorSide must be Left or Right");
            Validate(p, "Automatic2S", Automatic2S);
            Validate(p, "Automatic2C", Automatic2C);
            if (Envelopes == null
                || !Positive(Envelopes.RailDepthMm)
                || !Positive(Envelopes.RailWidthMm)
                || !NonNegative(Envelopes.GovernorGapToWallMm)
                || !Positive(Envelopes.GovernorWidthMm)
                || !NonNegative(Envelopes.GovernorFromGuideAxisMm)
                || !Positive(Envelopes.GovernorLengthMm))
                p.Add("Doors.Envelopes needs positive rail and governor dimensions");
            return p;
        }

        void Validate(List<string> p, string name, DoorTypePreset t)
        {
            string n = "Doors." + name;
            if (t == null)
            {
                p.Add(n + " is required");
                return;
            }
            if (!Positive(t.CarZoneMm) || !Positive(t.SillGapMm) || !Positive(t.LandingZoneMm))
                p.Add(n + " front-zone split needs positive CarZoneMm, SillGapMm and LandingZoneMm");
            if (!NonNegative(t.LandingWallClearanceMm))
                p.Add(n + ".LandingWallClearanceMm must not be negative");
            if (!NonNegative(t.MinFixedReturnMm) || !NonNegative(t.MinStackReturnMm) || !NonNegative(t.LandingFixedJambMinMm))
                p.Add(n + " minimum returns and landing jamb must not be negative");
            if (t.Sizes == null || t.Sizes.Count == 0)
            {
                p.Add(n + ".Sizes is required");
                return;
            }
            foreach (double o in OpeningsMm ?? new List<double>())
            {
                var row = t.Row(o);
                if (row == null)
                    p.Add(n + ".Sizes has no row for " + o.ToString(System.Globalization.CultureInfo.InvariantCulture));
                else if (!Positive(row.FixedReturnMm)
                    || !Positive(row.StackReturnMm)
                    || !NonNegative(row.LandingFixedJambMm)
                    || !Positive(row.LandingStackPocketMm))
                    p.Add(n + ".Sizes rows need positive returns and landing clearances");
            }
        }
    }
}
