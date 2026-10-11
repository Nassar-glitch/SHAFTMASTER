# Automatic doors (D1): extracted dimensions and generalised rules

Status: D1 (Core + UI, no drawing changes). Source: the user's reference drawings `Automatic 2S800 Geared Rear CW.dwg` and
`Automatic 2C800 Geared Rear CW.dwg` (geared, rear counterweight, 1:1), read from their DXF copies. Product behaviour is governed by
`DESIGN-FLOW.md` ("Doors"); this file records the numbers and the rules built from them (rules corrected 2026-10-11: the drawing values are preferred, the
minimums bind). Everything below is a preset in
`config/presets.json` (`Doors`), editable, with lock / follow / Reset.

## 1. Plan sketch (2S 800, reference drawing, mm)

X is measured from the shaft left wall, D from the shaft front wall (towards the rear). The governor is on the left, so the doors stack
on the right.

```
 shaft left wall X=0                                                       shaft right wall X=1720
 |  rail+bracket   car left X=185                          car right X=1535   rail+bracket |
 |   X 65..135     |                                                      |  X 1585..1655  |
 |                 |                    CAR  1350 x 1700                  |                |
 |  governor       |                                                      |                |
 |  X 79..105      |                                                      |                |
 D=245 ------------+--<fixed return 200>--+==== opening 800 ====+--<stack return 350>--+---   car interior front
 |                  car return 40 (D 205..245)                                         |
 D=205 ..........  car front-wall outer face
 D=200..115   car door panels: 2 x 40 + 5 gap, 5 clear of the return        |  CAR ZONE 130
 D=115 ------------------------------------------------------------------   outer car door face
 D=115..90   SILL GAP 25 (EN 81-20 5.3.6.2.2.1: <= 35)
 D= 90..5    landing door panels 2 x 40 + 5 gap                              |  LANDING ZONE 90
 D=  0 ------ shaft front wall

 landing frame:  X 355 |--jamb 30--| 385 ===== opening 800 ===== 1185 |-- stacking pocket 425 --| 1610     (1720 - 1610 = 110 to the wall)
 opening centre X=785 = shaft centre 860 - 75 (offset towards the fixed side)
 panels: A X 385..792.5, B X 782.5..1190 (width 407.5, overlap 10, 5 beyond the opening on the stacking side)
```

## 2. Extracted dimensions

### 2S 800 (shaft 1720 x 2800, car 1350 x 1700, DBG 1450, TZ 990)

| Item | Value | Source in the drawing |
|---|---|---|
| Front zone | 245 = 130 + 25 + 90 | dimensions 245, 130, 25, 90 |
| Car door zone (car interior front to outer car door face) | 130 | car return 40 + 5 + panel 40 + 5 + panel 40 |
| Sill gap | 25 | outer car panel D 115 to landing panel D 90 |
| Landing door zone | 90 | landing panels 5 + 40 + 5 + 40 to the front wall |
| Car return, fixed side (left) | 200 | dimension 200 (car left X 185 to opening X 385) |
| Car return, stacking side (right) | 350 = 800 / 2 - 50 | dimension 350 |
| Opening | 800 clear, height 2000 | "DOOR 800X2000" |
| Opening offset | 75 towards the fixed side | opening centre X 785, shaft centre X 860 |
| Wall to opening | 385 left, 535 right | dimensions 385, 535 |
| Landing jamb, fixed side | 30 (frame X 355 to opening X 385) | frame outline |
| Landing stacking pocket | 425 = 800 / 2 + 25 (opening edge X 1185 to frame X 1610) | frame outline |
| Panels | 2, width 407.5, thickness 40, overlap 10, 5 beyond the opening edge | panel outlines |
| Car rail + bracket | X 65..135, 65 wide along the depth (guide axis D 1160) | imported rail outline |
| Governor | X 79..105 (26 wide), starts 159 behind the guide axis, 240 long, left of the car | governor outline |
| Car depth | 1700; guide axis 785 behind the car front, 915 in front of the rear (dimensions) | |

### 2C 800 (same shaft and car)

| Item | Value | Source in the drawing |
|---|---|---|
| Front zone | 220 = 110 + 30 + 80 (the 2C drawing gives only 220; the split is read from the door boxes) | dimension 220; door boxes |
| Car door zone | 110 = car return 40 + car door box 70 (D 110..180) | polyline X 35..1686, D 110..180 |
| Sill gap | 30 (box edge D 110 to landing box edge D 80) | polylines |
| Landing door zone | 80 = 10 to the wall + landing box 70 (D 10..80) | polyline X 35..1686, D 10..80 |
| Car returns | 275 each side (car X 185 to opening X 460) | dimensions 275 |
| Opening | 800, centred (460 to each wall) | dimensions 460, 860 |
| Landing pocket per side | 425 = 800 / 2 + 25 (opening edge X 460 to frame X 35) | frame outline |
| Landing frame to the wall | 35 each side | polyline X 35.2..1685.9 |
| Panels | 2, one per side, width 399, thickness 39, gap 2 | panel outlines |

Both drawings fit their 1350 car exactly with the *preferred* returns (2S: 200 + 800 + 350; 2C: 800 + 2 x 275); these are preferred
values, not minimums (section 3). The 2C shaft 1720 is exactly the 800 landing minimum with a 35 mm frame to the wall (the minimum is 25).

## 3. Rules (700 / 800 / 900 / 1000 / 1200)

**Preferred versus minimum (Planner correction 2026-10-11).** The returns, the offset and the fixed-side jamb measured in the reference
drawings are *preferred* values. The binding values are the *minimums* (all presets, editable, Reset per field).

Let `o` be the clear opening, `g` the distance from the car side wall to the shaft wall (each side; 200 in the current plan recipe,
185 in the drawings).

| Preset | 2S | 2C |
|---|---|---|
| Front zone: car zone / sill gap / landing zone | 130 / 25 / 90 = 245 | 110 / 30 / 80 = 220 |
| Car return, fixed side, preferred | 200 | o / 4 + 75 (275 at 800), each side |
| Car return, stacking side, preferred | o / 2 - 50 | same as the fixed side |
| Opening offset towards the fixed side, preferred | (stack - fixed) / 2 = o / 4 - 125 (50, 75, 100, 125, 175) | 0 (centred) |
| Landing jamb, fixed side, preferred | 30 | o / 2 + 25 (pocket) |
| **Car return, fixed side, minimum** | **0** (opening edge flush with the car side wall) | **0**, each side |
| **Car return, stacking side, minimum** | **0** | same |
| **Landing jamb, fixed side, minimum** | **0** (opening edge flush with the shaft wall) | n/a |
| Landing stacking pocket | o / 2 + 25 | o / 2 + 25, each side |
| **Landing frame to the wall, minimum** (stacking side; 2C each side) | **25** | **25** |
| Panel width | (o + 5 + 10) / 2 | (o - 2) / 2 |

### 2S placement algorithm

Positions are distances `u` from the fixed-side shaft wall; the opening spans `[x, x + o]`.

1. **Preferred layout:** `x = SW / 2 - preferredOffset - o / 2`. This is the drawing (offset 75, fixed return 200 at 800 in 1350).
2. **Shift:** when the preferred layout does not fit, the opening moves towards the fixed side to
   `x = min(preferred, hi)`, where `hi = min(SW - 25 - (o/2 + 25) - o, SW - g - minStackReturn - o)`. The shift stops at
   `lo = max(g + minFixedReturn, minFixedJamb)`: fixed return 0 (edge flush with the car side wall), jamb shrunk to its minimum.
   The landing door moves with the opening; the fixed-side jamb is `min(preferred jamb, x)`. (The shift is computed in one step, not by
   stepping; the result equals stepping down to the first position that fits.) If the preferred position lies on the other side of
   `lo` (a car narrower than the preferred layout), the opening is placed at `lo`.
3. **Next smaller size:** when `lo > hi` the size does not fit; with a blank size the next smaller standard size is tried
   (largest first). The smallest size with its problems is the result when none fits.

Conditions after shifting: car width >= o + minimum returns; shaft width >= `lo + o + max(o/2 + 25 + 25, g + minStackReturn)`.
The parked panels may extend beyond the car side wall into the front zone; the clash checks (car guide rails and brackets, counterweight
rails and frame, speed governor) stay.

### 2C

Centred; the returns shrink from their preferred value to the minimum (0). Binding: car width >= o + 2 x minimum return, and the landing
`shaft width >= o + 2 x max(g + minimum return, o/2 + 25 + 25)`.

### Minimum shaft widths

| o | 2S, opening free to the shaft wall (g = 0) | 2S, plan recipe (g = 200) | 2C (g = 0 or 200) |
|---|---|---|---|
| 700 | 1100 | 1300 | 1500 |
| 800 | 1250 | 1450 | 1700 |
| 900 | 1400 | 1600 | 1900 |
| 1000 | 1550 | 1750 | 2100 |
| 1200 | 1850 | 2050 | 2500 |

2S: `1.5 o + 50 + g`; 2C: `2 o + 100`. The first column is the Planner's expected table (shaft width >= opening + stacking pocket +
25 + 0); it is exact only when the opening can reach the shaft wall. In a real shaft the car side wall stands `g` inside it
(rail, bracket and clearance), and the car opening and the landing opening are the same opening, so the second column applies: +200 with
the current recipe. `DoorRules` uses the real `g` of the plan; both columns are tested (`Minimum_shaft_width_2S_*`).

Selection and checks (all in `Elevator.Core/Doors/DoorRules`):

- **Blank size:** the largest standard size that passes every check after shifting; when none passes, the smallest, with its problems.
- **User size:** validated. Reasons given: not a standard size; the true minimum car width and shaft width ("does not fit: needs car width
  1350 mm, shaft width 1750 mm"); sill gap above 35 mm (EN 81-20 5.3.6.2.2.1); distance from the front wall to the car door above 150 mm
  (EN 81-20 5.2.5.2.1; landing zone + sill gap); clash.
- **Clash** (plan rectangles): the landing frame and the car door with its parked panels, against the car guide rails with brackets,
  the counterweight rails and frame, and the speed governor. Envelope sizes are the measured ones above (`Doors.Envelopes`).
- **Handing (2S):** stacking side opposite the governor (governor on the left in the catalog, so right); Left or Right can be locked.
  The mirror image about the shaft centre is exact.
- **The front zone sets the car depth:** `CarDepth = SD - frontZone - rear allowances` (Manual: the existing 125 mm front allowance,
  unchanged).

## 4. Findings and open decisions for the Planner

1. **The expected minimum table (1100 ... 1850) needs g = 0.** The car side wall is 200 mm (recipe; 185 in the drawings) inside the shaft
   wall, and the car door opening is the landing opening, so the opening edge can reach the car side wall but not the shaft wall.
   Real minimums with the current recipe are 1300 / 1450 / 1600 / 1750 / 2050 (2S). If a car may stand closer to the shaft wall on the
   fixed side, or the landing opening may differ from the car opening, that is an engineering decision for the Planner / user.
2. **The equipment profile gives a 1320 mm car in 1720** (150 mm rail allowance per side), the drawings 1350 (135). On the reference
   shaft 1720 x 2800 the rules select 900 (2S) and 800 (2C). The 800 numbers of the drawings are reproduced exactly with the drawing's
   car (tests `Reference_2S_800_reproduces_the_drawing`, `Reference_2C_800_reproduces_the_drawing`). Changing the 150 allowance would
   change the Manual recipe, which D1 does not touch.
3. **Car depth in the references is 1700 on SD 2800**; the recipe gives 2280 with the 2S front zone. The recipe is unchanged.
4. **2C preferred return generalisation** (o / 4 + 75, 275 at 800) is a proposal from one drawing; with minimum 0 it no longer binds.
5. **2C front-zone split (110 / 30 / 80)** is read from the door box outlines, not from dimensions.
6. **Rail, bracket and governor envelopes** are measured from the imported outlines (rail 70 x 65, governor 26 x 240). There is no rail
   profile library yet (DESIGN-FLOW stage 3); they are presets.
7. **Drawings:** the plan and sections still draw the Manual door. `DoorLayout` (opening X, landing X, panels, zones, `CentreShiftMm`)
   is available for the drawing stage (D2).

## 5. Drawings (D2)

The automatic door is drawn from the `DoorLayout` of section 3 in the native plan, in the calculation-sheet figure F1 and in the STEP
model. A Manual door is drawn exactly as before (no template entity is touched).

### Native plan (`Elevator.AutoCAD/Plan/AutomaticDoorPlan`)

- The template door geometry stays in the drawing, bound and updated as before, but is **hidden** (nothing is cloned or moved any more).
  Hidden for a 2S / 2C door, by tag (52 entities):
  - manual door: `Plan.Door.Opening`, `Plan.DoorLeft.Part001-004`, `Plan.DoorRight.Part001-007` (jamb blocks, handles, ticks, the yellow
    swing line), `Plan.DoorSpan.Part001-003` (landing sill box), `Plan.DoorLeader.Part001`;
  - car entrance notch: `Plan.Entrance.Part002-007`;
  - centre mark device of the manual landing door: `Plan.CenterFront.Part001-013` (the references have none; `Part014-019`, the section
    arrow, stays);
  - car front returns: `Plan.Cabin.Inner.Front`, `Plan.Cabin.Part002-004`;
  - door dimensions: `Plan.Dimension.Part006-013`;
  - wall pieces that carry the opening, redrawn with the opening where the door is: `Plan.Wall.Part001`, `Part004`, `Part005`, `Part006`
    and `Plan.Shaft.InnerOutline.EntranceDatum` (hidden but still the frame datum).
  A Manual project, or a later update of the same drawing with a Manual door, erases the new entities and shows the template geometry
  again (`AutomaticDoorPlan.Clear`).
- New entities are tagged `Plan.AutoDoor.NNN` (role `autoDoor`), so they are read, arranged and counted as plan entities, and not as
  unbound entities.
- Drawn from the layout (X from the shaft left wall, D from the shaft front wall; mirrored for handing Left). Style of the references:
  a panel is its outline plus three construction lines at 10 mm inside it ("double line"), a parked panel block is a rectangle with a
  second one 1 mm inside.

| Item | 2S | 2C | Layer |
|---|---|---|---|
| Wall with the opening | the template wall pieces with the two jamb edges at opening edge -/+ jamb width | same | Wall |
| Jamb blocks in the wall opening | double line blocks (outline and 3 mm inset), 135 wide, at the opening edges | same | 0 |
| Landing frame | fixed-side jamb (outline and 3 mm inset, D 0 to landing zone); pocket lines: top from the fixed panel end to the pocket edge, bottom, outer edge | boxes D (zone - 70) to zone and D (zone + gap) to +70 over the landing frame width | 0 |
| Landing and car panels (closed) | 2 x panel width, fixed-side panel next to the car, leading panel behind it, over-travel 5 beyond the opening, telescopic overlap 10; thickness 40, 5 between rows | 2 centre panels per door, 2 mm apart, thickness 39 | outline: Car (2S), 0 (2C); construction lines: 0 |
| Parked panels | 4 blocks 106 wide, 4 mm inside the pocket edge, in the four panel rows | 4 blocks 141 wide at the ends of both boxes | Car (2S), 0 (2C) |
| Car front returns | car side wall (outer face) to the opening edge, 40 deep, ending at the car front, with a centre line at each opening edge | same | step&guides, CL |
| Sill lines | landing sill D 0, car sill D (car front - 40), across the opening | same | DL |
| Dimensions (style Nassar2, texts as in the references) | returns and OPENING o above the car front; wall to opening, DOOR oX2000, the two halves and (offset only) the opening offset below the wall; front-zone split | same, without the offset | 0 |

- Front-zone dimensions: the total (template dimension, 245 / 220) keeps its column; landing door zone, sill gap and car door zone have
  their own columns 60, 120 and 180 mm towards the wall, outside the wall, so no text overlaps. The offset dimension (2S, e.g. 75) is
  the row nearest the wall, between the opening centre and the shaft centre.

Result for the three tier-3 cases: `output/doors-native/*-plan.png` (rendered from the generated DWG) against the reference renders
`2S.png` and `2C.png`; every entity is compared with the layout by `tests/integration/verify_doors.py` (45 checks). The comparison images
are in `docs/design/doors-d2/`.

### Sections

- **A-A:** the door is the template's native door line (frame plane at the wall, clear head 2000, full head 2160, audited per served
  floor): correct for 2S and 2C (door height 2000 = DOOR oX2000). The car front follows the front zone through the shared plan.
  Only the experimental vertical geometry (`VerticalSource`) used the manual positions for the car and landing door boxes; for an
  automatic door they are now the car door zone and the landing door zone.
- **B-B and the plan recipe in the AutoCAD commands:** the section audit recalculated the plan with the Manual recipe and failed against the
  new car depth ("shared car reference verification failed: 1385 versus 1325 mm"). Every plan recalculation in `Elevator.AutoCAD` now
  takes the door of the project stored in the drawing (`AutomaticDoorPlan.InputFor`), and the door catalog comes from `config/presets.json`
  (`Configuration.DoorCatalog`), also in the host context.

### STEP (`Elevator.Solution.StepExport/BuildModel`)

The model contains doors (two landing panels per served floor, two cabin panels, front-wall piers and spandrels around the opening). For an
automatic door `StepModelParameters` carries the opening X range and the panels and frame as simple boxes (X, Y, width, depth, height
above the floor, height); the piers and spandrels follow the opening position, the manual panels are replaced by the panel boxes and, per
floor, the frame bodies (2S: fixed-side jamb, stacking pocket beyond the panel over-travel, header over the opening; 2C: header). A Manual
door gives empty parameters and the same bodies as before.

## 6. D3: frames, jamb posts, panel bands and arranged dimensions (the user's own correction)

The user generated the 2S 700 / 1320 case and corrected the plan by hand; his file is kept in `docs/design/doors-d3/` and
`docs/design/doors-d3/USER-EDIT-DIFF.md` states what he did as rules. They are implemented in `AutomaticDoorPlan` (principle 8 of
DESIGN-FLOW, "Drawing quality"), values and recipe unchanged:

* **Frames:** the landing door frame (d 0 .. landing zone) and the car door frame (d car zone start .. car front - 40) are one rectangle
  each from the outer edge of the fixed-side jamb to the end of the stacking pocket (2C: across both stacking sides, centred), so the
  panels sit inside a frame and the two frames are aligned. The loose pocket lines are replaced by the frame.
* **Jamb posts (2S):** fixed-side post in both frames, outline plus a 3 mm inset line closed at the opening edge.
* **Panel bands:** each panel row continues from the closed end of its panel to the end of the pocket (2C: to the ends of the frame),
  outline and 1 mm inset line open at the closed end.
* **Dimensions** (`Elevator.Core.Geometry.DimensionLayout`, pure and unit tested): chains on one dimension line (returns / opening; wall /
  DOOR / wall; halves; landing zone / sill gap / car door zone; zone total / CAR DEPTH one column pitch of 110 mm further out); the rows
  below the wall at the template spacing (the opening offset one pitch nearer); extension lines from the frame corners, the sill line and
  the car front return; a text that does not fit its interval is placed outside the chain (beyond a car wall that blocks it) or beside
  the chain on a second lane, so no dimension text overlaps another. CAR DEPTH and the zone total of the template are hidden and redrawn
  in these places (`Plan.Dimension.Part020`, `Part031` join the hidden list of section 5).

Tier 3 (`doors-native`): the user's project is regenerated and compared with his DWG (line work identical, every dimension present with the
same interval and text, dimension lines within 15 mm); `verify_doors.py` checks the frame / post / band geometry of 2S right and left,
2S shifted, 2C 800 and 2C 900, that every panel is inside its frame, the one-line chains, the equal rows and the text boxes.
Images: `docs/design/doors-d3/`.

D3b (the user's 2C correction, `USER-EDIT-DIFF.md` section C): the 2C landing box keeps the reference depth 70 (the car frame runs from the
sill gap to the car front return); the inset line of a band is 1 mm short of the right-hand end only; and a first (or last) dimension of a
chain is stepped out one text lane nearer the geometry when the second (last but one) interval is too small for its text, whose text then
stands outside the shortened chain (applies to the 2S 90 | 25 | 130 and the 2C 80 | 30 | 110 front-zone chains). Tier 3 compares both
user corrections with the regenerated drawings (`verify_user_edit.py`).

## 8. Doors Offset (D4)

- **Field:** "Doors Offset" on the door page for 2S and 2C (hidden for Manual): the distance in mm from the shaft centre line to the opening centre line,
  and Left / Right as seen in the plan drawing (standing at the landing, facing the door). Distance and side lock together; Reset returns the preset.
- **Preset:** 2S the preferred offset from the returns, converted to Left / Right through the handing (75 at 800 towards the fixed side, the stack on the
  right gives 75 Left), with the automatic shift in narrow shafts; 2C 0 (centred).
- **Range (`DoorOffset`, Core):** 2S: fixed-side return at or above its minimum, fixed-side landing jamb at or above its minimum, stacking-side landing
  frame (pocket or panel reach) at least the wall clearance from the wall and the stacking return at or above its minimum. 2C: both landing door boxes
  at least the wall clearance from their walls and both car returns at or above their minimum. Example: 2C 800 on a 1720 shaft allows 10 Left to 10 Right only (boxes 425 + 25 clearance each side);
  an automatic size that takes the whole width (2C 900 on 1900) leaves no room to move.
- **Out of range:** the typed value is kept (field message "out of range: max N Left"), the drawings and STEP use the nearest allowed position, Design Notes
  give one line with the reason and limit, the export warning lists it with the other NOT OK results, the calculation sheet input table shows the typed value.
- **Files:** keys `Doors.OffsetMm` (distance, never negative) and `Doors.OffsetSide` (Left / Right), locked together. The key `Doors.OpeningOffsetMm` of
  earlier files (towards the fixed side, 2S only) is still read with the same geometric result; it is replaced when the user sets a Doors Offset.
