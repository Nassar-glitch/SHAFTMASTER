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

## 6. How the rails are fixed in each arrangement (inventory, G1 round 3)

Read from the main plan of the generated drawings (the template plan, `docs/design/details/sets/set-inv-plan-*.png`), the plan
dimensions of `DrawingUpdater` (Part003 CW DBG, Part016 shaft width, Part017 rear wall to the CW row, Part024 car DBG) and the library
sheet `Layouts_Sec/01_Geared_MR_1-1_LAYOUT.dxf` (screenshots in `sets/`).

| Arrangement | Car rails | CW rails | Source |
|---|---|---|---|
| **RCW** (the only plan geometry the program has) | at the side walls: a bracket block from the wall to the rail back (gap (SW - DBG) / 2 - h1: 85 for T70 at DBG 1200 in SW 1500) | at the rear: the template draws the CW bracket blocks on the **rear wall** (125 from the rear wall to the CW row, 75 / 100 / 75 across the rail band), 230 from the side wall to the DBG point | [set-inv-plan-concrete-client](sets/set-inv-plan-concrete-client.png) |
| **RCW, steel** | the same, the bracket on the side UPN beam | the same, on the rear UPN beam; with the user's numbers 380 side, 125 rear, 75 / 100 / 75 | [set-inv-plan-steel-user](sets/set-inv-plan-steel-user.png) |
| **SCWL / SCWR** | at the side walls | not in the program: `DrawingFileName` returns RCW for every project and the plan geometry places the counterweight at the rear (DesignRules). No template plan for it in the library file | `Elevator.Core/Layouts/DrawingFileName.cs` |
| **Cross arm in the library** | | the sheet "GUIDE RAILS AND BRACKETS FIXATION AT THE SEPARATION BEAM": the rails fixed with brackets on a steel beam **UPE 120** that reaches from the shaft wall, fixed with an end plate "100 x 100 x 100 - 10 mm" and two anchor bolts M12 | [set-inv-separation-beam](sets/set-inv-separation-beam.png) |

The library's rail plan details (sets of 5-9-3 ... 16-16-2.0) show only the rail with its bracket, the rail section sizes (50 / 65 / 70 /
75 / 90 and the blade 5 / 9 / 16) and no wall: they are the bracket, not the arrangement.

**Conflict reported:** the template plan draws the CW bracket blocks on the rear wall for every RCW project, also when the rail back is
only 180 from the side wall (SW 1500). The program's rule (below) gives a wall bracket from the side wall for that case. The block is a
template entity (it is not drawn by the program for a concrete shaft), so the plan is left as it is and the difference is reported here.
Only the steel plan (the program's own tower) can follow the rule; the plan blocks of the template do not move with the fixing type.

### The rule (implemented)

1. **Numbers from the plan:** the gap from the rail back to the side surface = (SW - DBG) / 2 - h1 of the profile (the plan dimension to
   the DBG point less the rail height); for the CW rails the arm to the rear surface = the plan dimension rear wall to the CW row less half
   the rail width. A wall, a slab edge or a UPN beam is the surface (steel: the UPN beam).
2. **Fixing type** (RAILS page, Advanced, per rail group): Auto / Wall bracket / Cross arm; **reach** 300 mm (preset, editable).
   Auto = wall bracket when the side gap is within the reach, otherwise a cross arm from the rear surface; a chosen value is kept.
3. **Wall bracket:** the arm of the bracket is the gap (the preset leg 40 is the least); VP5 dimensions the gap.
4. **Cross arm:** section **UPE 120** (h 120, b 60, web 5, flange 8: from the library separation-beam detail), end plate 100 x 100 x 10
   with 2 anchors M12 (steel: bolts through the beam), all editable presets (`GuideRails.Fixing`); the length is the plan number (100 for
   the CW rails at 125 from the rear wall). The bracket stands on a head plate at the arm end.
5. **Details:** VP5 and VP6 draw the fixing of each rail group (car and CW can differ): wall or beam, arm and plates, bracket, rail.

Cases in the tests: `RailBracketTests` (Auto for the RCW default, CW far from the side walls giving a cross arm, chosen values, steel
surfaces, no rear surface, lock / follow of the fixing presets); drawings `concrete-client` (wall brackets), `concrete-wide` and
`steel-wide` (SW 1800: cross arm for the CW rails), the user's steel project (SW 1800, DBG 1040: cross arm).

## 7. How the details are drawn (G1)

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
- The text height is 2.5 mm printed at the scale the detail takes on the sheet. The elevation draws the two brackets close
  together with a break of the rail between them; its spacing DIMENSION carries the real value (2000 / 3000) as text. Plan 1:5 or 1:10,
  elevation 1:10 or 1:20.
- The plate width is computed: the rail foot width plus the two clips (library: 270 x 50 x 4 for the 50 mm rail).
