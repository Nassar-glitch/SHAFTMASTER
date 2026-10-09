# Calculation sheet figures (Phase A F1–F6, Phase B F7–F9)

Internal document. The figures are generated in the desktop/report layer and add no equations; the calculation engines are unchanged.

- Code: `Elevator.Solution.Desktop/FigureModel.cs` (model and standard), `FigureRender.cs` (SVG and 300 dpi PNG renderers), `CalculationFigures.cs` (F1–F6), `DocxWriter.cs` (SVG embedding and figure frame).
- Each figure is built from the same plan geometry and results that the sheet prints. It appears only when its section appears, and it is never drawn with missing or placeholder values.

## Drawing standard (fixed)

| Item | Rule |
|---|---|
| Background / colours | White. Geometry black/dark grey. Loads and reaction values in one accent colour, red `#D0021B`, labelled in kN |
| Line weights | Outlines 0.5 mm; secondary 0.35 mm; thin/centre/hidden 0.25 mm; dimensions, hatch, leaders 0.18–0.25 mm |
| Line types | Centre lines dash-dot (6 / 1.2 / 0.6 / 1.2 mm); hidden lines dashed (2.5 / 1.2 mm) |
| Hatch | ANSI31: 45°, 1.5 mm pitch (2 mm for the F4 load areas), clipped to the outline |
| Dimensions | Extension lines with a 0.8 mm gap and 1.5 mm overshoot; filled closed arrows (2.5 × 0.9 mm); text above the line, or beyond the arrows when it does not fit |
| Text | 2.5 mm (ISO cap height) for labels, 3.5 mm for titles. Upright; only titles bold |
| Font | ISOCPEUR if installed, else Arial Narrow, else Arial (`FigureFonts`) |
| Placement | No overlapping text (placement tries alternative positions; tested). Dimension chains sit outside the geometry |
| Size / scale | Figure width 165 mm inside the page frame; one uniform scale per view |
| Embedding | SVG, with a PNG fallback at 300 dpi (Word 2016+ `svgBlip`). SVG user unit = 0.1 mm, because Word spaces glyphs badly at tiny font sizes in user units |
| Hyphens | Figure text never contains U+002D between letters: Word's SVG renderer adds space around it and ISOCPEUR has no U+2011. `Figure.Text` turns a word hyphen into a space ("guide shoe", "car rail line"); labels are phrased to need none. Trailing full stops and semicolons are avoided in notes for the same reason. Tested |
| Frame | Thin bordered frame with a title strip: "Figure n — title" \| "Dimensions in mm" \| "NTS" (schematic figures only). Numbered in order of appearance |

## FF1 / FF2 direction convention (F4, F5)

Forces are drawn on the rail, from the shoe. The rated load acts on half of the floor; its resultant is offset from the car centre by ex = ±W/4 (FF1) or ey = ±D/4 (FF2). The guide-shoe couple on the car balances the load moment about the car centre.

- **FF1, load towards +X (right).** Seen from the entrance the load moment is clockwise, so the shoe couple on the car is counter-clockwise. At the upper shoe level the rail on the loaded (+X) side is pushed outward (+X). At the lower level the opposite (left) rail is pushed outward (−X).
- **FF2, load towards +Y (rear).** At the upper level both rails are pushed towards the rear (+Y); at the lower level both are pushed towards the front.

The builders take the sign of each offset (`GuideForcePlan(…, ff1Side, ff2Side)`, `GuideForceElevation(…, ff1Side, ff2Side)`) and derive every arrow from it; the sheet uses +1 / +1. F4, F5a and F5b use the same signs. F5a shows the offset load as "Q at W/4" on the loaded side.

Test (`MomentBalance`, sheet core) for both fixtures and all four sign combinations, read from the drawn arrows. The drawn rail forces act on the car reversed. With z = ±h/2 at the upper/lower level, it checks that Σ z·dir·FF − Q·10·e = 0:

- F4 FF1;
- F4 FF2 (the lower level is reversed, as the note states);
- F5a FF1, which must also have the same load side as F4;
- F5b FF2, with two rails.

It also confirms that reversing one arrow breaks the balance. With the base values (FF1 = Q·10·W/(4h), FF2 = Q·10·D/(8h)) the balance is exact.

## Fixed vs project-driven

In every figure, shapes, symbols, style and layout are fixed. Dimensions, positions, counts, rail positions, h, W/D and all force values come from the project.

### F1 — Shaft plan (Input data section)

| Element | Fixed or project-driven | Data source |
|---|---|---|
| Shaft outline SW × SD, dimensions SW, SD | Project | `ElevatorParameters.SW`, `.SD` (from `ElevatorProject.Shaft.WidthMm/DepthMm`) |
| Wall band and its hatch | Fixed, symbolic, not dimensioned | `CalculationFigures.Wall` = 200 mm |
| Internal car outline, dimensions W, D | Project | `ElevatorParameters.X("carLeft"/"carRight")`, `.CarFront`, `.CarRear`, `.CarWidth`, `.CarDepth` |
| Counterweight outline | Project | `X("cwLeft"/"cwRight")`, `.CwFront`, `.CwRear` (depth = `DesignContext.CounterweightDepthMm`) |
| Car dimensions labelled "W …" and "D …" | Project | `.CarWidth`, `.CarDepth` |
| Dimensions car DBG and CW DBG | Project | `.CarDbg` (between the car rail blade tips), `.CwDbg` (between the CW rail blade tips) |
| Entrance: opening in the front wall with jamb lines, label "ENTRANCE" | Project | `.DoorWidth`, `X("doorLeft"/"doorRight")`. If the plan has no door opening, the wall stays closed and the note "Entrance at the bottom" is added |
| Car rails: position | Project | `X("railLeft"/"railRight")` = centre ∓ `CarDbg`/2, at `.CarRow`. `CarDbg = SW − 2·EquipmentProfile.CarRailSideAllowanceMm` |
| Counterweight rails: position | Project | `X("cwRailLeft"/"cwRailRight")` at `.CwRow` |
| Rail symbols: filled T profile (foot and blade), blade towards car/CW | Fixed shape; true scale from the designation, enlarged about the blade tip only when smaller than `CalculationFigures.MinRailSymbolMm` = 3 mm on paper (`RailSymbolFactor`) | `ElevatorDesignResult.CarRailProfile`, `.CounterweightRailProfile` → `IRailSection` |
| Leader labels "car rail T70/65/9" and "CW rail T50/50/5" | Fixed text; designations from the project | `CarRailProfile`, `CounterweightRailProfile` |
| Car-rail line | Position from the project; line type fixed | `.CarRow` |
| X/Y axis glyph, labels | Fixed | — |

The plan comes from `DesignRules.Calculate(SW, SD, context.Equipment, context.CounterweightDepthMm)`, the same recipe the FF geometry uses. F1 and F4 are skipped if the plan's W/D differ from the FF result's W/D.

### F2 — P1–P4 chassis plan (P1–P4 section)

| Element | Fixed or project-driven | Data source |
|---|---|---|
| Bearing seats P1–P4 | Project | `ChassisCalculationResult.Reactions[].PositionMm` |
| Reaction labels (kN) | Project | `Reactions[].UpwardN` |
| Load point W, value | Project (Basic) | `SimplifiedDistances.LoadReference` (traction-sheave centre), `TotalVerticalLoadN` |
| Dimensions L, a, b, s(f), s(r) | Project (Basic) | `SimplifiedDistances.LongitudinalSpanMm`, `LoadFromFrontMm`, `LoadToRearMm`, `FrontTransverseSpanMm`, `RearTransverseSpanMm` |
| Advanced: load points and values; dimensions L, s(f), s(r) | Project | `Loads[].PositionMm`, `Loads[].ForceN.Z`; seat coordinates |
| Chassis outline joining the seats, seat and load symbols, axes | Fixed (schematic outline) | — |

### F3 — P1–P4 free body (Basic only; NTS)

| Element | Fixed or project-driven | Data source |
|---|---|---|
| W and its position | Project | `TotalVerticalLoadN`, `SimplifiedDistances.LoadReference.Y` |
| Front/rear row reactions | Project | `FrontLoadN` (P2 + P4), `RearLoadN` (P1 + P3) |
| Dimensions a, b, L (horizontal to scale) | Project | `SimplifiedDistances` |
| Beam, supports, vertical layout | Fixed, schematic | — |

Advanced has no single resultant, so F3 is not drawn.

### F4 — FF load cases, plan (FF section)

| Element | Fixed or project-driven | Data source |
|---|---|---|
| Internal car W × D, dimension W | Project | `CarGuideForceResult.InternalWidthMm`, `.InternalDepthMm` |
| Rails and car-rail line relative to the car | Project | ±`CarDbg`/2 at `CarRow − (CarFront + CarRear)/2` |
| Guide-shoe symbols | Fixed size, at the rail tips | — |
| Level labels: FF1 "upper level" on the rail on the loaded side and "lower level" on the opposite rail, both arrows outward; FF2 "upper level" on both rails, towards the loaded side, with the note "forces reverse at the lower level" | Fixed convention (see above); load side from the offset sign | — |
| Notes: rated load on half the floor; "FF1 / FF2: force per guide rail at one guide shoe level" / "Directions reverse between the upper and lower shoe levels" | Fixed | — |
| Eccentric load area: half of the floor, hatched | Fixed rule (resultant at W/4 for FF1, D/4 for FF2) | Offsets from W and D |
| Dimensions W/4, D/4 | Project | W/4, D/4 |
| FF1 along the rail line and FF2 perpendicular, at each rail (kN) | Project | `FF1DesignN`, `FF2DesignN` |

D is dimensioned in F1 and listed in the FF inputs table, so F4 does not repeat it (that keeps the labels clear).

### F5 — Guide-shoe forces, elevation (NTS)

Two views separated by a vertical line:

- **a) Front elevation** (seen from the entrance): both car rails, the car frame, four shoes and the load "Q at W/4" on the loaded side. FF1 acts in this plane as a couple: the upper shoe pushes the rail on the loaded side outward, the lower shoe pushes the opposite rail outward. Labels "upper shoe level" and "lower shoe level".
- **b) Side view of one rail** (seen along the car rail line): the rail blade with its front and rear side faces. FF2 acts in this plane as a couple on the blade side faces: upper shoe towards the loaded side (rear, as in F4), lower shoe towards the front.

FF2 is never drawn in view a, where it would act perpendicular to the paper. Every force arrow ends on a rail face with its tail outside the rail; none points from a shoe away from a rail (tested).

| Element | Fixed or project-driven | Data source |
|---|---|---|
| Dimension h (shoe centre to shoe centre), in both views | Project | `CarGuideForceResult.GuideShoeSpacingMm` |
| FF1 values in view a, FF2 values in view b (kN) | Project | `FF1DesignN`, `FF2DesignN` |
| Rails, car frame, shoes, drawn spacing | Fixed, schematic | `CalculationFigures.F5UpperY/F5LowerY`, `F5LeftRailFace/F5RightRailFace`, `F5SideFrontFace/F5SideRearFace`, `F5ViewSplitX` (paper mm; also used by the tests) |

### F6 — Car guide-rail cross-section (NTS)

| Element | Fixed or project-driven | Data source |
|---|---|---|
| Designation | Project | `ElevatorDesignResult.CarRailProfile` ("T70/65/9") |
| b1, h1, k (the only dimensions) | Parsed from the designation | `ProportionalRailSection.Parse` → b1 = 70, h1 = 65, k = 9 |
| Other features: foot thickness, edge, root | Proportional and undimensioned | Foot 0.12·h1, foot edge 0.09·h1, root 0.8·k |
| x-x axis (through the area centroid), y-y axis | Computed from the outline | `IRailSection.Outline()` |
| FF1 (Fy) on the blade tip, FF2 (Fx) on the blade side (kN) | Project | `FF1DesignN`, `FF2DesignN` |
| Hatch, axis types, layout | Fixed | — |

No ISO 7465 detail dimensions are invented. The figure code uses only the `IRailSection` interface: designation, b1, h1, k, `Proportional` and `Outline()`. A future rail-profile table (designation → full dimensions) can implement it and replace `ProportionalRailSection` without changing `CalculationFigures`.

## Phase B (F7–F9)

All three are schematic (NTS). Numbers appear only as tagged project values, and the only dimension is the pit depth when it is set. Tests: every text with a digit in F7–F9 is a tagged value; no other `dim-` tag.

### F7 — Suspension, 1:1 roping (rope tensions section; needs the shared plan)

| Element | Fixed or project-driven | Data source |
|---|---|---|
| Side elevation: car (front, left) and counterweight (rear, right), machine-room slab with rope openings, traction sheave on its stand, hitches | Fixed, schematic | — |
| Car rope from the front pitch edge of the traction sheave | Fixed arrangement from the section geometry | `MachineRoomSectionRules`: `TractionX = CarDropX − 260` |
| Deflector sheave, drawn only when the project rule selects one; the CW rope drops from its rear pitch edge | Project | `MachineRoomRules.SelectDeflector(ElevatorParameters.TZ).IsRequired` (`DeflectorX = CwDropX + pitch/2`). Without a deflector, the CW rope drops from the traction sheave |
| T(car), T(cwt) arrows on the rope runs, all ropes (kN) | Project | `ChassisCalculationResult.CarBundleTensionN`, `.CounterweightBundleTensionN` |
| Per-rope values (kN), when the rope count is known | Project | `.CarPerRopeTensionN`, `.CounterweightPerRopeTensionN` |
| Label "n = <ropes> ropes, 1:1" | Project | `Suspension.RopeCount`, `Suspension.RopingRatio` |
| Label "pitch diameter …", only when set | Project | `Machine.TractionSheavePitchDiameterMm` |

F7 is left out when the shared plan (`DesignRules.Calculate`) is not available, because the deflector cannot be decided without TZ.

### F8 — Progressive safety gear (P5 subsection)

| Element | Fixed or project-driven | Data source |
|---|---|---|
| Front elevation of the lower car frame: stiles, safety plank, car floor line | Fixed, schematic | — |
| One rail, one safety-gear block and one downward P5 arrow per car guide rail | Count from the project | `SafetyPitLoad.ComponentCount` (= `SafetyPit.CarGuideCount`; only 2 is supported by the engine, otherwise P5 and F8 are omitted) |
| P5 value on each arrow, note "per car guide rail", rail-count label | Project | `P5.PerComponentLoadN`, `ComponentCount` |
| Plan detail: rail section with the jaws on both blade side faces | Rail shape from the designation (proportional); jaws schematic | `IRailSection` (`CarRailProfile`) |

### F9 — Pit buffer loads (after the P6/P7 subsections)

| Element | Fixed or project-driven | Data source |
|---|---|---|
| Pit section: hatched floor and walls, car and counterweight outlines | Fixed, schematic | — |
| Car buffers under the car, CW buffers under the CW | Counts from the project; positions schematic (no buffer position exists in the project geometry) | `P6.ComponentCount` (`CarBufferCount`), `P7.ComponentCount` (`CounterweightBufferCount`) |
| Downward arrow at each buffer base, onto the pit floor, with the per-buffer value | Project | `P6/P7.PerComponentLoadN` |
| Totals "P6 total … on n car buffers", "P7 total … on n CW buffers", per-buffer line | Project | `GroupLoadN`, `PerComponentLoadN`, `ComponentCount` |
| Pit-depth dimension from the landing line to the pit floor, only when set | Project | `Shaft.PitDepthMm` |

F9 shows only the groups that are available (P6 and/or P7) and is omitted when neither is.

## F10 — Traction sheave (rope / traction section; NTS)

| Element | Fixed or project-driven | Data source |
|---|---|---|
| Sheave rim, pitch circle, hub; groove-detail frame | Fixed, schematic | — |
| Rope wrap and the dimensioned wrap angle α | Project | `ReevingSolver.CalculatedWrapAngleDeg` through `MachineRoomSectionRules` with a deflector; 180° for direct roping |
| T1 (car side) and T2 (counterweight side) arrows on the rope runs; "to deflector" or "counterweight side" label | Fixed symbols; deflector presence from the project | `MachineRoomRules.SelectDeflector(TZ)` |
| Dt label | Project | `Machine.TractionSheavePitchDiameterMm` |
| Groove profile type | Project | Rope / traction groove type (semi-circular / undercut, V hardened, V unhardened) |
| β (undercut angle) and γ (groove angle), dimensioned | Project | Rope / traction inputs |

F10 appears when the section has a safety factor or a traction case. Tags `dim-alpha`, `groove-beta`, `groove-gamma`, `rope-t1`, `rope-t2` and `dt` are tested. See `ROPE-TRACTION-CALCULATIONS.md`.

## Scope notes

- The project supports 1:1 roping only (schema `RopingRatio` enum [1]). No 2:1 handling was added.
