# Guide-rail bracket sizes (stage G1)

The bracket presets of the RAILS page (Advanced) and the details VP5 / VP6 of the sheet come from this table. One row per rail blade
width (5 / 9 / 16 mm). Every value is an editable preset: it follows the rail profile until the user edits it.

## 1. What the template library shows

Measured in `Layouts_Sec/01_Geared_MR_1-1_LAYOUT.dxf` (the rail-combination sets 5-9-3, 5-9, 5-16, 9-16, 9-16-2.0, 16-16-2.0 and the
anchor-bolt sheets, LAYOUTS.md) and in the reduced template `Templates/Layout_Geared_A0.dxf` (VP5 `RAILS_PLAN_CW5_CAR9`, VP6
`RAIL_BRACKETS_*`). Screenshots of every measured set are in [sets/](sets/).

The library draws **one bracket family** for every rail. The dimensions it states:

| Item | Value | Where |
|---|---|---|
| Angle arm | L 50 x 40, length 270, two arms per bracket (the angle section is `270x50x40`) | anchor sheets [set-anchor-sheet-3m](sets/set-anchor-sheet-3m.png), [set-anchor-sheet-2m](sets/set-anchor-sheet-2m.png) |
| Connecting plate | 270 x 50 x 4 | the same sheets |
| Assembly bolts | 4 x M10 x 40 | the same sheets |
| Anchors | Ø12 x 150 into the concrete | the same sheets |
| Bracket spacing | 3000 (sheet "3 m") and 2000 (sheet "2 m"); note "BRACKETS ARE FIXED AT 3000mm / 2000mm" | the same sheets, [set-5-9](sets/set-5-9.png), [set-9-16](sets/set-9-16.png) |
| Clips | two clips per rail on each bracket, **two bolts per clip**, slotted plate | VP5 plan [template-RAILS_PLAN_CW5_CAR9](sets/template-RAILS_PLAN_CW5_CAR9.png) |
| Rail sections | T50/50/5 (blade 5), T70/65/9 (blade 9) with their blade, height and width dimensions; 16 mm rails: "Car Guide Rail 16mm", bracket at 1500 | VP5, [set-5-16](sets/set-5-16.png), [set-car-16-brackets-at-1500](sets/set-car-16-brackets-at-1500.png) |
| Steel shaft fixing | bracket bolted to the steel member | [set-steel-shaft-fixing](sets/set-steel-shaft-fixing.png) |

The set drawings are PDF-import outlines. They do not state a clip size or a rail-bolt size, and the plan details are schematic (a
dimension text such as "65" is a label, not a measured length). The values that follow the blade width are therefore **proposed**
here, from the catalogue rail foot (GUIDE-RAILS.md section 8: neck c = 5 / 6 / 10, foot depth f = 5 / 8 / 11); they are the
only part of the bracket that changes between rows.

## 2. The table

All sizes in mm.

| Blade (mm) | Rail section b1 x h1 | Angle arm | Plate | Assembly bolts | Clip L x W x t | Rail-to-bracket bolt | Anchor | Source of the row |
|---|---|---|---|---|---|---|---|---|
| **5** | 50 x 50 (T50/50/5) | L50x40x4, 270 | 270x50x4 | 4 x M10x40 | 40 x 25 x 5 | 2 x M8 per clip | Ø12 x 150 | arm, plate, bolts, anchor: library; clip and rail bolt: proposed from the foot c = 5, f = 5 |
| **9** | 70 x 65 (T70/65/9) | L50x40x4, 270 | 270x50x4 | 4 x M10x40 | 50 x 30 x 6 | 2 x M10 per clip | Ø12 x 150 | arm, plate, bolts, anchor, 2 bolts per clip: library (VP5 T70 set); clip and rail bolt: proposed from c = 6, f = 8 |
| **16** | 89 x 62 (T89/62/16), 90 x 75 (T90/75/16) | L50x40x4, 270 | 270x50x4 | 4 x M10x40 | 60 x 35 x 8 | 2 x M12 per clip | Ø12 x 150 | arm, plate, bolts, anchor: library; clip and rail bolt: proposed from c = 10, f = 10 / 11. The set "car 16 brackets at 1500" differs only in the spacing 1500, which is the project's spacing, so it adds no bracket geometry |

Each row is `GuideRails.Profiles[].Bracket` in `config/presets.json` (and the shipped defaults in `GuideRailDefaults.cs`).

## 3. Catalogue profile to row

| Catalogue profile (GUIDE-RAILS.md section 8) | Blade k | Row |
|---|---|---|
| T50/50/5 | 5 | 5 |
| T70/65/9 | 9 | 9 |
| T89/62/16 | 16 | 16 |
| T90/75/16 | 16 | 16 |

A profile that is added to the catalogue without a `Bracket` takes the row of its blade width, or the next heavier row when it has no
set of its own (`RailBracketPreset.ForBlade`: up to 5 mm the 5 row, up to 9 mm the 9 row, above that the 16 row).

## 4. Spacing

The bracket spacing is not part of the bracket preset. The car spacing is the RAILS value (recommended by the check, 3000 or 2000 in the
library sheets); the counterweight spacing follows the car spacing until the user types it. The detail dimensions the real value.

## 5. Questions for the Planner

1. The clip and rail-bolt sizes are proposed from the rail foot because the library does not dimension them. Confirm or give the
   sizes of the supplier's clips.
2. The library shows the same arm and plate for 5 and 16 mm rails. If a heavier 16 mm rail needs a stronger bracket, the arm and plate rows
   are editable per profile and only the preset values change.

## 6. How the details are drawn (G1)

Both details are generated at full size from the project; the template's static details VP5 and VP6 are hidden and only their titles
are kept (the scale is appended as before).

- **Plan (VP5, "CAR / C.W. GUIDES AND BRACKETS")**: the wall (concrete: hatched wall, the anchors shown; steel: the UPN beam, the bolts
  through its web), the two angles (leg A on the wall, leg B towards the rail), the plate across the ends of the legs, the rail section
  of the effective profile (flange, neck, head), the two clips with their bolts. One named block per bracket and per rail section
  (`SM_RAIL_BRACKET_PLAN_*`, `SM_RAIL_*`).
- **Elevation (VP6)**: the car rail and the counterweight rail, each with two brackets at the **real spacing** of the project (a
  DIMENSION between the bracket centres: 3000 for a 3000 spacing), the bracket length and the width chain; the clips, their bolts and
  the anchors (or the bolts through the beam of a steel shaft, with the beam behind the bracket). Block `SM_RAIL_BRACKET_ELEV_*`.
- Layers `S-RAIL`, `S-BRACKET`, `S-CLIP`, `S-ANCHOR`, `A-WALL`, `S-BEAM`, `A-DIMS`, `A-ANNO`; labels are MULTILEADERs placed with the
  same no-overlap rule as the steel drawing (PlaceLabel with obstacles), the dimension texts of a chain by `DimensionLayout`.
- The text height is 3 mm printed at the scale the detail takes on the sheet; the elevation may take 1:50 (a 3000 spacing is 3.6 m
  high), the plan stays at 1:10 or 1:5.
- The plate width is computed: the rail foot width plus the two clips (library: 270 x 50 x 4 for the 50 mm rail).
