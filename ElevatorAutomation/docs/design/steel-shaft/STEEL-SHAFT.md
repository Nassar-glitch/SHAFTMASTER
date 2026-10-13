# Steel shaft structure (UPN): reading of the client drawing and proposed model

Stage S1 (analysis only, no product code). Source: `UPN STEEL SHAFT.dxf` (the user's drawing, shaft only, no lift; AC1032, units mm,
10 MB). A copy for the cloud is in `source/` (the original at the repository root was not modified). All renderings in this folder were
made from that file with ezdxf + matplotlib (black on white, lineweights flattened, so hatches and splines look denser than in
AutoCAD). Dimensions below are the drawing's own dimension values; geometry that was measured from entities is marked "measured".

Scope (user, 2026-10-13): the program draws its **own four views and A0 sheet** with the steel tower; the reference DXF is only the source of
the steel look and rules. Sections 1 to 4 read the reference; **section 5 is what changes in each existing view and the sheet**; 6 the
model; 7 questions; 8 inconsistencies in the drawing.

## 1. What the file holds

Model space has three title blocks (two projects) and one extra section, about 15 000 entities. The sheets are not paper-space layouts
(`Layout1`, `Layout2` are empty); each is a frame drawn in model space, 45 000 x 32 000 model units.

| # | Sheet / view | Where (model x, y) | Version | Picture |
|---|---|---|---|---|
| A | Plan "SHAFT and CAR 1:10" only, UPN10, loose (left of the sheets) | x -23 400..-3 000, y 36 000..52 000 | UPN10 | `upn10-plan-model.png` |
| B | Sheet "STRUCTRAL LAYOUT", REEM RESIDENCE / VILLA 376: views at D and E 1:10, SIDE - SEPARATORY BEAM, base section, plan, bolt detail, GENERAL NOTES, title block | x 0..45 000, y 34 500..66 500 | **UPN10** (shaft 2000 x 950) | `upn10-sheet1.png` |
| C | Sheet, same project: SEC. C-C, B-B, A-A 1:50 (block `U10U10 SEC`, scaled 1.40625) | x 0..45 000, y 0..34 000 | **UPN10** (2000 x 950, height 16050 on the sheet) | `upn10-sheet2.png` |
| D | One loose section C-C 1:50 with double-cross (X) bracing, labels "U14 STEEL BEAM / COLUMN", "L50X50X5mm." | x 51 000..57 000, y 36 000..55 000 | **UPN 140, older** (shaft 2000 wide, travel 9667.97, 3 floors 3222.66, OH 3523.44, pit 988.28, total 14179.69) | `u14-strip-old.png` |
| E | Sheet "STRUCTRAL LAYOUT", OTIK ELEVATORS / VILLA 34 (block `upn st`, scaled x5, inserted at 61 753, 65 901): D and E details, separatory beam, base section, plan, C-C, A-A, B-B, bolt detail, REMARKS, title block | x 62 000..107 000, y 33 000..66 500 | **UPN 140, latest** (shaft 1500 x 2000, 4 stops, travel 11250, pit 1150, OH 4100) | `u14-sheet.png` |

Zooms of sheet E: `u14-plan.png` (plan), `u14-plan-corner-TL.png`, `u14-plan-corner-BL.png` (corner column at about x 80),
`u14-details-top.png` (D, E, separatory beam, base section), `u14-sections.png` (C-C, A-A, B-B), `u14-bolt-detail.png`,
`u14-notes-title.png` (remarks and title block).

Sheet E is the one to follow. Sheets B and C are the same project at UPN10 (UPN 100 columns and beams, no bracing). D is the study that
has the X bracing the user wants for this client.

Title-block data of sheet E (attributes inside the block, as text): SHAFT `2000X1500 - HEIGHT 16500 MM.`, TRAVEL `11250 MM.`, STOPS `4`,
ENTRANCES `4`, CONTROL `SIMPLEX COLLECTIVE DOWN`, CURRENT `3 PHASE 380 V. 50 Hz.`; company `OTIK ELEVATORS`, project `VILLA 34`.
Sheet B/C: SHAFT `2000X950`, STOPS `4 [B-G-1-2]`, ENTRANCES `4 [B-G-1-2]`, REEM RESIDENCE / VILLA 376.

## 2. Plan (sheet E, "SHAFT and CAR 1:10")

All numbers are true millimetres (the block is drawn 1:1 and scaled x5 in model space).

- **Shaft:** `SHAFT WIDTH 1500` (horizontal, X) and `SHAFT DEPTH 2000` (vertical on the sheet, Y). Width 1500 is measured between the
  flange tips of two facing columns (measured, block units: 1221 to 2721). Depth 2000 is measured
  between the column axes (plate centres; 1989 measured).
- **Columns: four, one at each corner**, each a `[14` (UPN 140) standing up, drawn as a section with a solid web line and two flanges.
  At the left corners the web is on the outside (x minimum) and both flanges point **into the shaft along X**; at the right corners it
  is mirrored (web at x maximum, flanges pointing to -X). So the four channels open towards each other across the width. Measured
  section about 64 x 126 drawn units with outlines (a UPN 140 is 60 x 140; the plan section looks smaller than the labels, see 7).
- **Base plate under each column: 200 x 200 x 15 mm ("BASE PL 200X200X15MM.")** with **4 bolts Ø20** ("4- BOLTS 20mm.", holes drawn
  Ø28), square pattern about 120 x 120 mm. The plate is centred about 27 mm outside the flange tip, so the plate edge sits
  about 100 mm outside the clear steel face. Dimensions shown: plate size `200` (X, twice), gap between plates `1350`
  (X, so the plate-to-plate overall is 1750), `200` and gap `1790` (Y; overall 2190).
- **Beams** form a closed rectangle at each level: two beams along X (front and back faces, 1500 wide faces) and two along Y (side
  faces, 2000). In plan they show as long double lines at the inside of the corner plates: the Y beams run from column to column on
  the web line, the X beams run between the two columns of a face at the top and bottom of the plan, ending on the flange of the
  column (the beam channel is an inch inside the plate). Beam section is also `[14`.
- **Entrance side:** the bottom of the plan (cut lines B-B), 1500 wide. Evidence: section B-B (3) shows the door frame verticals and
  no bracing; the concrete base is drawn continuous (2600 wide) so the opening is not shown in the plan; the landing doors are not
  drawn at all in this "shaft only" file.
- **Drawn:** column sections as solid (hatch of many fine lines), base plates and bolt circles, beams as double lines, two centre lines
  (dash-dot, `CL` layer), the base slab outline 2350 x ... (the concrete block), section arrows A-A (top, left and right), B-B (bottom),
  C-C (centre line), no hatch on the plates.
- **No door, no car, no rails** in the plan (the title says "SHAFT and CAR" but only the steel is drawn).

Plan rules (relative to the clear width W and the depth D, plate P = 200):

- the four columns stand at the corners, web outside, flanges into the shaft along the width axis, channels facing each other;
- the plate is centred on the column and extends P/2 each side of its axis, so the steel footprint is about (W + 2 x 127) x (D + P);
- a beam ring of four channels at every level, front and back (W) between the columns, sides (D) between the columns;
- the entrance is the front face (bottom of the plan), no bracing there.

## 3. Elevation (sheet E sections, 1:50)

Pit, floors and overhead are as in the lift: total 16500 = pit 1150 + travel 11250 (3 x 3750) + OH 4100.

- **Views:** C-C looks at a 2000 side face (door stack arrows 2, 1, G, B point at the right-hand 1500 face edge: the entrance is on the
  1500 faces; floor levels B, G, 1, 2 are drawn with the landing sill lines). A-A is the 1500 back face seen from inside. B-B is the
  1500 front (entrance) face, outside.
- **Beam levels (A-A dimension chain, bottom to top): 1150 | 9 x 1600 | 500** = 16050 on the chain (the title says 16500, see 7).
  The first beam is at pit-top level (1150 = pit); then every 1600 mm; a last 500 above the top beam to the roof beam. The beams do **not**
  fall on the floors (floors at 1150, 4900, 8650, 12400; beams at 1150, 2750, 4350, 5950 ...). That is why the user now wants a beam
  at every floor level.
- **Top and bottom:** a closed pyramid cap (a roof hip drawn as two lines above the top beam, same on all three views), column feet on
  the 200 x 200 x 15 plates on a plain/reinforced concrete block (the two hatches "خرسانة مسلحة" and "خرسانة عادية": a reinforced
  concrete slab on top of a plain concrete block).
- **Bracing, UPN 140 version (sheet E):**
  - single diagonal L50x50x5 per panel, alternating direction every panel (a continuous zigzag up the face, joints alternately at
    the left and the right column);
  - on the two 2000 faces (C-C) and on the 1500 back face (A-A);
  - **none on the entrance face** (B-B shows only beams and the door-frame verticals);
  - the last (bottom) panel is shorter (1150) and also braced, the top 500 panel is not;
  - connection: the angle ends on the beam-column joint plate (view at E): bolted to a `PL 400x250x6` gusset with Ø14 bolts, angles
    drawn at about 45-50 degrees to the vertical.
- **Bracing, older UPN 140 version (sheet D):** full double cross (X) in every panel on the 2000 face, panels of 1375 mm, beams every 1375,
  angles L50X50X5mm.; the beams are `U14 STEEL BEAM`, the columns `U14 STEEL COLUMN`.
- **Bracing, UPN10 version (sheets B, C):** none; only beams every 1600 and, in B-B, vertical door-frame members in the door zones.
- **Level dimensions shown:** pit 1150 (or 700 UPN10), car travel 11250, three 3750 floor spacings, O.H. 4100, total shaft height,
  the width above each section (2000 / 1500 / 1500), the 1600 chain on A-A.
- **Labels:** `U14 STEEL BEAM`, `U14 STEEL COLUMN`, `L50X50X5mm.`, `U14` (leader on A-A), `E` and `D` detail callouts (circles on the
  2000 face at a beam-column joint and at a column splice), Arabic callouts `خرسانة مسلحة` / `خرسانة عادية` under the base.

## 4. Details and notes

- **View at D 1:10 (column splice):** two `[14` channels (140 wide, 45-50-45 gauge) lapped with 8 bolts in 4 rows over 400 mm
  (200 + 200), 70 / 140 stagger. Splice plate flush on the web.
- **View at E 1:10 (beam-column joint):** the beam `[14` runs into the column web; the joint plate `PL 400x250x6MM.` with Ø14 mm
  bolts (45-60-35 and 130-140-130 gauge, 25 / 40 edge), the two L50X50X5 diagonals bolted to the same plate with 55 / 65 / 80 / 100
  distances.
- **SIDE - SEPARATORY BEAM:** a `U CHANNEL 140` on a concrete base with an **M12 ANCHOR BOLT** (separatory beam = the beam of the
  lift's separator wall; shown on the UPN10 sheet as `UPN10`).
- **Base section:** a reinforced concrete slab 2600 (125 + 2350 + 125) x 250 thick, a 400 high cage of Ø8 @ 120 (Arabic text
  `م /١٢Ø٨`), reinforced concrete over plain concrete. The UPN10 sheet has 2900 (125 + 2650 + 125).
- **Bolt detail:** bolt Ø20 (ST-52), 550 long, 150 thread above, welded washer plate `60X60X10 MM`.
- **Anchor of columns:** the 4 x Ø20 holes of the base plate (bolt detail above).
- **Rail brackets:** not drawn anywhere in the file.
- **GENERAL NOTES / REMARKS text (sheet B, "REMARKS" on E):**
  1. ALL DIMENSIONS SHOULD BE CHECKED BY THE MAIN CONTRACTOR BEFORE FABRICATION AND ERICTION. (disclaimer-like: principle 6 keeps it out of
     generated drawings; also "ERICTION" is a typo.)
  2. ALL DIMENSIONS ARE IN MILLIMETERS UNLESS STATED OTHERWISE. (unit statement; ordinary drawing content)
  3. STRUCTURAL STEEL GRADE (37) SHALL BE USED. (specification: keep, as a preset text)
  4. THE DESIGN, FABRICATION AND ERECTION OF STRUCTURAL STEEL SHOULD BE IN ACCORDANCE WITH THE CODE OF PRACTICE "SPECIFICATION FOR THE
     DESIGN, FABRICATION AND ERECTION OF STRUCTURAL STEEL FOR BUILDING" LATEST EDITION. (specification; the code name is cut: the text
     has no code number, the printed name is a placeholder of the client template)
  5. FIELD CONNECTIONS FOR MAIN MEMBERS SHALL BE MADE WITH 20MM HIGH STRENGTH BOLTS, FRICTION TYPE DIN 6914 GRADE 8.8 UNLESS NOTED.
     (specification: keep)
  Title-block note: `ALL THE DIMENSION INDICATED ARE FOR FINISHED MASONRY` (a remark on how to read the dimensions: principle 6
  would keep it out of the generated drawing; ask).
  Principle 6 summary: note 1 and the "finished masonry" note are remarks; notes 2 to 5 are specification text. All of them are the
  client's template, not the program's own statements.
- **Legend in the title block:** CONCRETE, REINFORCED CONCRETE, BRICK hatch swatches.

## 5. How the four existing views and the sheet change

Goal (user, 2026-10-13): the same four main views (shaft plan, machine-room plan, A-A, B-B) and the same A0 sheet as today, drawn with the
steel tower instead of concrete walls. The reference file gives the steel look and its rules; its sheets are not reproduced. Settled and
not asked again: a beam at the door head when the door is fixed to the structure; the product is an external lift, so the machine room
(MR) sits on top of the steel; the bracing is drawn as in the reference side elevation (double cross per panel; the face-by-face choice
is the later STEEL module). Today's concrete views are in `../layouts/l2b/6-stops-1600x1800-arabic-sheet.png`.

**Common rule: the clear shaft stays the clear shaft.** Width W and depth D are the dimensions everything else is measured from
(car, counterweight, guides, DBG, TZ, entrance). With steel they are the clear size between the inner faces of the beams, which is the
flange-tip line of the columns in the reference. The steel lies outside that rectangle where the wall used to be, so no car,
counterweight or rail calculation changes; only what is drawn for the "wall" changes.

### 5.1 Shaft plan ("SHAFT and CAR 1:10", today concrete walls with the opening and the door jambs)

- The wall outline and hatch are replaced by the **four corner columns** (UPN 140 section, solid, web outside, flanges into the shaft,
  channels facing each other along the width) on their 200 x 200 x 15 base plates (plate outline under the column) with the 4 x Ø20
  bolt circles.
- The **beam ring at the cut level** (UPN 100 channel on the four faces, between the columns) is drawn as double lines on the clear
  rectangle. The plan is cut just above the entrance floor beam, so the door-head beam is not in it.
- **Entrance face:** the corner columns and the floor beam only; the door opening lies between the columns, where today's entrance wall
  jambs (the 300 mm blocks) are. The door frame is fixed to the beams.
- The bracing lies in the face planes and is not drawn in plan (it would only repeat the beam lines).
- Dimensions: W and D as today; add only the profile labels ("UPN 140", "UPN 100") as leader texts in the style of the existing
  guide-rail labels.

### 5.2 Machine-room plan ("MACHINE ROOM 1:10")

- The MR is bigger than the shaft (2650 over 1600 in the example, 525 each side): its floor is a **steel frame on top of the tower**:
  the top beam ring of the shaft plus **outrigger beams** carrying the overhang, with the machine beams and the P1 to P4 supports on
  them. In plan: the top beam grid (shaft beams, outriggers, machine beams), the support points P1-P4 and TZ, and the MR walls as
  today (their construction is question 2).
- The hatch of the concrete slab is replaced by the steel floor (a floor-plate outline, no hatch).

### 5.3 Section A-A (today a 1:50 elevation: the walls as vertical lines, floor levels, brackets, car and counterweight)

- The wall lines are replaced by the **tower elevation**: two columns (double lines), the base plates on the concrete base at the pit,
  the top frame under the MR.
- **A beam at every floor level**, **a beam at each door head** (the header, at the door height above the floor) and the **mid beams**
  that divide the gaps (preset "2 spaces per floor", editable): see 6.1 and question 1.
- **Bracing:** double cross (X) of L50x50x5 in every panel between consecutive beams, as in the reference side elevation, at the
  gusset plates of the beam-column joints; none on the entrance face.
- The **rail brackets sit on the beams**: the bracket marks are the beam levels (the `Rails.BracketMaximumSpacingMm` layout becomes the
  beam layout), so the A-A bracket chain is the same chain.
- Labels in the reference style: "UPN 100 STEEL BEAM", "UPN 140 STEEL COLUMN", "L50X50X5mm.".

### 5.4 Section B-B ("SEC AT B-B 1:10": the machine-room section with the traction machine, TZ and the slab hatch)

- The reinforced-concrete slab and its hatch become the **steel MR floor on the top frame**: the main beams, the machine beams under the
  machine and its support stand, the deflector beam, with the P1 to P4 arrows landing on the steel. TZ and the machine dimensions are
  unchanged.
- The concrete bearing blocks drawn at the two sides become the steel top beams on the columns.

### 5.5 Title block and sheet

- Same A0 geared sheet and the same arrangement rules (`SheetArrangement` unchanged); the views only have new content.
- Legend swatches CONCRETE / REINFORCED CONCRETE / BRICK: add a STEEL swatch; the concrete ones stay only where concrete is drawn (the
  base).
- The note `ALL THE DIMENSION INDICATED ARE FOR FINISHED MASONRY` does not apply to a steel shaft (and is a remark under principle 6):
  it is not generated for steel.
- Title-block data: add the construction text ("STEEL STRUCTURE UPN") and the profile names where a field exists; other fields unchanged.
- Detail group (right of the sheet): "Guide rail support bracket and anchor details" becomes the bracket-on-beam fixing; the steel
  details of the reference (column splice, beam-column joint, base plate and bolt) are new reference details (stage G, postponed).
- General notes: notes 2 to 5 of the reference as the steel notes preset (steel grade, standard, high-strength bolts).

## 6. Proposed model

### 6.1 Inputs and presets (Core)

- **Shaft construction** (shaft page): Concrete (default) | Steel structure. `Steel` group in the project:
  - Construction system: `UPN` (first one; the field exists so other systems can come later).
  - **Column profile:** list from the UPN catalogue (preset UPN 140), also placed at the 4 corners; **Beam profile:** list (preset UPN 100,
    the user's rule; the drawing itself uses U14 for both); **Bracing profile:** list from the L catalogue (preset L50x50x5).
  - **Bracing:** drawn as in the reference side elevation (double cross in every panel, L50x50x5); the face-by-face choice
    (single diagonal, double cross, which faces) is left to the later STEEL module.
  - **Beam levels:** preset rule "a beam at every floor level and one at each door head (the doors are fixed to the steel), the
    space between divided equally, preset 2 spaces per floor (3250 -> 1625), editable" (the exact header / mid rule is question 1).
    Also the pit-top level and the top beam. Editable list of levels (each locks when edited, Reset gives the rule back); like
    every other input it starts from the preset.
  - Base plate (preset 200 x 200 x 15), bolt count and diameter (4 x Ø20), washer plate (60 x 60 x 10), gusset plate (PL 400 x 250 x 6),
    steel grade text and the standard notes as editable presets.
- **Rail brackets follow the beam levels:** the bracket levels are the beam levels (the bracket sits on a beam), so
  `Rails.BracketMaximumSpacingMm` and the car / CW spacing become *derived* when the construction is steel: effective spacing =
  the beam spacing (largest gap). The guide-rail check keeps its meaning (the check is then run at that value); the recommendation
  for the beam levels is "2 spaces per floor", which gives spacings of 1500 to 1700 mm for 3000 to 3400 mm floors. The
  user's own rule (1500 mm everywhere, the same as the brackets) can be a preset: "spaces per floor" = floor height / 1500
  rounded up.
- A UPN catalogue and an L-angle catalogue in `config/presets.json` (group `SteelShaft`), like the guide-rail profile catalogue
  (cloud: Elevator.Core, xUnit).

### 6.2 Core (cloud) and AutoCAD (local)

- **Machine room on the steel:** the MR frame (top ring, outriggers, machine beams) is part of the same Steel group, with its own
  presets (profiles, overhang), and carries P1 to P4.
- **Core / Reporting (cloud, tier 1):** catalogues, input model and presets, beam-level rule, the derived bracket spacing,
  the plan / section geometry as data (column rectangles, beam lines, brace polylines, labels with the profile names), the
  steel member list (profile, length, count, mass: the base of a bill of materials and, later, the STEEL STRUCTURE calculation), the
  sheet and Design Notes text, xUnit tests.
- **AutoCAD (local):** drawing the channel section, the hatch, the beam / bracing linework in plan and A-A, dimension chains,
  layers (CL, S-PLATES, S-TEXT conventions of the client file), sheet assembly, PDF.

### 6.3 Catalogue (DIN 1026-1 / EN 10279 standard sections; to be verified by the cloud session against a manufacturer table)

Channel UPN (parallel-flange-less, inclined flange 8 %), dimensions in mm, A in cm², mass in kg/m, Ix and Wx about the strong axis:

| Section | h | b | tw | tf | r | A | mass | Ix cm4 | Wx cm3 |
|---|---|---|---|---|---|---|---|---|---|
| UPN 80 | 80 | 45 | 6.0 | 8.0 | 8.0 | 11.0 | 8.64 | 106 | 26.5 |
| UPN 100 | 100 | 50 | 6.0 | 8.5 | 8.5 | 13.5 | 10.6 | 206 | 41.2 |
| UPN 120 | 120 | 55 | 7.0 | 9.0 | 9.0 | 17.0 | 13.4 | 364 | 60.7 |
| UPN 140 | 140 | 60 | 7.0 | 10.0 | 10.0 | 20.4 | 16.0 | 605 | 86.4 |
| UPN 160 | 160 | 65 | 7.5 | 10.5 | 10.5 | 24.0 | 18.8 | 925 | 116 |
| UPN 180 | 180 | 70 | 8.0 | 11.0 | 11.0 | 28.0 | 22.0 | 1350 | 150 |
| UPN 200 | 200 | 75 | 8.5 | 11.5 | 11.5 | 32.2 | 25.3 | 1910 | 191 |

Equal angle: **L 50 x 50 x 5**: A 4.80 cm², mass 3.77 kg/m, r1 7 mm, r2 3.5 mm, Ix = Iy 11.0 cm4, centroid 14.0 mm from the back.
Source: DIN 1026-1:2000 and EN 10279:2000 (hot-rolled steel channels), EN 10056-1 (equal angles). The table above was written from memory in
stage S1; stage S2 checked it against the producer tables, see section 9.1. The drawing itself only draws the section outline and does not
state any of these.

## 7. Questions for the user (only what the drawing does not settle)

Settled by the user and removed from the earlier list: the door-head beam, the MR on the steel (external lift), the bracing as in the
reference side elevation.

1. **Door-head beam level and the mid beams.** The header sits at the door construction height above the floor (2160 in the project
   constants). The preset rule "2 spaces per floor" (3250 -> 1625) does not put a beam on 2160. Which rule: (a) the floor beam and the
   header are fixed, and each gap between fixed beams is divided equally so that no gap exceeds a target (preset 1500 mm); or (b) the
   equal division of the floor height, with the header added as an extra beam?
2. **Machine room walls and overhang.** The MR is larger than the shaft (525 each side in the example). Are the MR walls part of the
   steel (cladding on a steel frame), and does the program draw the outrigger beams that carry the overhang with a preset (profile,
   length), or only the top ring of the shaft?
3. **Clear size.** Confirm that W x D on the shaft page is the clear size between the inner faces of the beams, as in the reference
   (1500 between flange tips), with the steel outside it.
4. **Plan cut level.** The plan shows the beam ring of the entrance floor beam. Confirm, or ask for the plan at the door-head beam.
5. **Base.** Does the program draw the concrete base (2600 x 250 with Ø8 @ 120 in the reference) in A-A, or only the plates on a base
   line?

## 8. Inconsistencies in the drawing (not decisions; for information)

- Total shaft height is `16500` (title block and dimension) but the A-A dimension chain 1150 + 9 x 1600 + 500 adds to 16050 (and
  UPN10 sheet C: 700 + 9 x 1600 + 500 = 15600 against 16050). The C-C chain 1150 + 11250 + 4100 = 16500 is consistent.
- The plan section is drawn about 64 x 126 (outline) while the labels, details and notes say UPN 140 (60 x 140).
- `SHAFT WIDTH 1500 / DEPTH 2000` on sheet E, `2000 x 950 / 900` on sheet B / C (3 different depth values 950, 900 in text).
- The sections are called `C-C` twice in the file (strip D and sheet E) with different bracing.
- Some texts are in `S-TEXT` / `STRUCADTXT` layers with a style that has a missing font in this file: shown as boxes in the
  renderings (title block cells).

## 9. Stage S2: the portable model (cloud, 2026-10-13)

Code: `Elevator.Core/Steel/` (catalogue, presets, fields, beam-level rule, structure and geometry), `Elevator.Reporting/Sheet/CalculationSheetSteel.cs`
and the Design Notes line; tests `tests/Elevator.Core.Tests/SteelShaftTests.cs` and `tests/Elevator.Reporting.Tests/SteelShaftReportTests.cs`.
No AutoCAD, no UI. A concrete project does not change at all: the steel inputs live in the project extension `steel` (the same pattern as the
title-block fields), which a concrete project never has, and the library group `SteelShaft` is outside the library hash.

### 9.1 Catalogue and what was verified

`config/presets.json`, group `SteelShaft` (editable): channels UPN 80 to 200 (h, b, tw, tf, r1, A, mass, Ix, Wx, Iy, Wy, e) and equal angles
L40x40x4, L50x50x5, L60x60x6 (A, mass, Ix, e, r1, r2). Checked on the web on 2026-10-13 (the producer pages could be searched, not fetched;
ArcelorMittal's table was not reachable, so the producer tables below stand in for it):

| Values | Source |
|---|---|
| UPN 80 to 200: h, b, tw, tf, A, mass, Ix, Wx, Iy, Wy | Kocaer Steel, UPN datasheet: [channels-upn.pdf](https://kocaersteel.com/uploads/2025/02/channels-upn.pdf) (based on DIN 1026-1 and EN 10279); cross-checked with the STAD technical data sheet [stad-technical-data-of-sections-upn.pdf](https://www.stad.fr/downloads/pdf/stad-technical-data-of-sections-upn.pdf) (UPN 140: Ix 605, Iy 62.7, Wx 86.4, Wy 14.8) |
| UPN 80: 8.64 kg/m, 11.0 cm² | [Project Materials UPN table](https://blog.projectmaterials.com/quick-answers/steel/upn-channel-dimensions-weight/) (EN 10279) |
| UPN 140: r1 10, r2 5, e 17.5 mm | Kocaer Steel UPN 140 page and [beamdimensions.com UPN 140](https://beamdimensions.com/database/European/Steel/Standard_UPN_U_channels/UPN_140/) |
| L50x50x5: 3.77 kg/m, 4.80 cm², Ix 11.0, e 14.0 mm | [Roymech equal angles (EN 10056-1)](https://roymech.org/Useful_Tables/Sections/AngleA_dim_prop.html), [Engineering Toolbox, steel angles](https://mail.engineeringtoolbox.com/steel-angles-d_1324.html) |
| L40x40x4: 2.42 kg/m, 3.08 cm², Ix 4.47 | Engineering Toolbox |
| L60x60x6: 5.42 kg/m, 6.91 cm², Ix 22.8, e 16.9 mm | [Murray Steel Products, BS EN 10056-1](https://murraysteelproducts.com/offers/structural/equal-angles) |

**Still to verify against a producer table** (each row carries a `Check` text in the file so it is not lost; the text is never printed):
the centroid distance e of UPN 80, 100, 120, 160, 180, 200 (values are the usual table values from memory; only UPN 140 was confirmed), and the
radii r1 / r2 of L40x40x4 and L60x60x6 (and e of L40x40x4). The consistency tests (mass = 7850 · A, Wx = Ix / (h/2), Wy = Iy / (b - e)) pass
for every row.

### 9.2 Inputs (all preset-first, lock and follow)

Keys `Steel.*` (page SHAFT): `Construction` (Concrete default | Steel), `System` (UPN), `ColumnProfile` (UPN 140), `BeamProfile` (UPN 100),
`BracingProfile` (L50x50x5), `Bracing` (DoubleCross), `BeamSpacingMm` (1500), `BasePlateWidthMm / DepthMm / ThicknessMm` (200 / 200 / 15),
`BoltCount` (4; 6 and 8 are drawn too) and `BoltDiameterMm` (20), `GussetLengthMm / WidthMm / ThicknessMm` (400 / 250 / 6), `SteelGrade` ("St 37"),
and the concrete base `Base.MarginMm` (200), `Base.ThicknessMm` (250), `Base.BarDiameterMm` (8), `Base.BarCountAlongX / AlongY` (the preset follows the
block size at no more than 120 mm bar spacing with 50 mm cover, so the counts follow the shaft size until typed). Library-only values: the bolt
pitch (120) and the bar spacing and cover. An edited value lives in the project extension `steel` and locks (key in `UserEditedFields`); Reset
removes it. The fields are not in `ProjectPresets.Fields` (so "Fill blanks" and `Apply` never touch a concrete project) but `ProjectPresets.Find`
returns them, like the title-block fields.

### 9.3 Beam levels (DESIGN-FLOW answer 1)

`SteelBeamLevels.Rule(floors, doorHeight, s, base, top)`, pure. Elevations are from the lowest floor (pit negative). Fixed: every served floor and
every door head (floor + 2160, the shared door construction height). From each door head up in steps of s; the rest to the next fixed beam <= s
means that fixed beam is next (a rest of exactly s goes to the fixed beam; 10 mm more adds a beam). The pit steps from the base up to the lowest
floor (the base itself is the datum, not a beam); the top steps from the top-floor door head to the tower top, which carries the last beam
(the machine platform). Tags: floor, door head, intermediate, pit, top. Tower top = top floor + overhead; base = lowest floor - pit.
Examples (tested): 3000 -> 0, 2160, 3000; 6000 -> 0, 2160, 3660, 5160, 6000; 3250 -> 0, 2160, 3250.

Edits: `SteelFields.EditLevel / RemoveLevel / ResetLevel / ResetLevels` keep the edits by level key (F:name, D:name, I:floor:k, P:k, T:k, T:top,
C:n for an added level); an edited level is locked, a floor level and the tower top are never moved, an edit of a key the rule no longer has is
ignored until the rule has it again.

### 9.4 Derived rail bracket spacing

For a steel shaft the car spacing preset is the largest gap between consecutive beam levels from the lowest to the highest served floor
(`SteelStructure.DerivedBracketSpacingMm`), the counterweight spacing follows it, `Rails.BracketMaximumSpacingMm` follows the car spacing, and
`GuideRailCheckEngine` uses the derived value for both lines whatever a typed spacing says. With the preset s = 1500 and the 2160 door height
the largest gap is always the door gap, 2160 mm (floors of 3000 and of 6000 alike); a larger s or a removed beam makes it larger. A concrete
project keeps the recommendation (2000 in the example) unchanged.

### 9.5 Geometry as data

`SteelStructure` (built from the project and the library, `Valid` false with `Inputs.Problems` when something is missing): `Plan`, `Faces`, `Base`,
`TowerTop`, `Members`. Plan axes as the project: X along W, Y along D, origin at the front-left corner of the clear rectangle, entrance face at
Y = 0 (the front). **The steel lies outside the clear rectangle:**

- columns: the channel box is b (flange width, 60) along X and h (depth, 140) along Y, outside each corner; the web is on the outer side and the
  flange tips are on the clear line (left columns X -60 to 0, right columns W to W + 60, front Y -140 to 0, back D to D + 140), so W is the
  distance between flange tips; the outline is the parallel-flange channel (8 % slope and radii not drawn);
- base plates: P x P centred on the column box (as in the reference), so a plate reaches (200 - 60) / 2 = 70 mm inside the clear rectangle along
  the width at the pit floor (`Plan.PlateIntrusionMm`; the reference plates intrude 75 mm per side too); bolts on the 120 square about the plate centre;
- beam ring: four beams between the columns, flange width across the face (UPN 100: 50), outside the clear faces and touching the columns' edges;
- elevation per face (u along the face, z from the lowest floor): two columns base to tower top, a plate each, a beam band (beam depth, centred on
  the level) at every level, and on every face except the entrance face the double cross in every panel between consecutive levels (the panels start
  at the base), plus gusset points at the panel corners; the concrete block under the base (top at the base elevation, its thickness below);
- block: the plate footprint plus the margin on every side; bars along X (spread over Y) and along Y as plan lines; volume;
- tower top: the last beam ring at the tower top, the machine envelope = the clear rectangle (no outriggers).

### 9.6 Member list

Columns (4, base to tower top), beams (2 per level along each direction), braces grouped by length (two diagonals per panel, three braced faces),
base plates, gusset plates (two per panel corner level on each braced face), anchor bolts (bolts per plate x 4); profile, length, count, mass of one
and in total (plates at 7850 kg/m3). The brace length is the work-point length across the clear face.

### 9.7 Reports

The calculation sheet has a "Shaft structure" table under the input data, only for a steel shaft: construction, column / beam / bracing profile,
beam spacing, the beam levels, the derived bracket spacing, base plate, steel grade. No structural check. Design Notes: a recommendation line when a
gap larger than s cannot be split because of a door (the floor-to-door-head gap is the door height, 2160 > 1500: this is the case with the
presets, see the handoff), and when a floor is not higher than the door height (no door-head beam fits). Nothing for a concrete shaft.

## 9. Stage S3 (local): what is built

The Core model of S2 with the per-face beam rule (beams follow the car rail bracket spacing), the Shaft page, and the steel tower in the four
generated views and on the A0 sheet. Evidence (plotted from generated DWGs) is in `s3/`: `steel-client-*` (shaft 1500 x 2000, 4 stops,
floors 3750, pit 1150, overhead 4100) and `steel-default-*` (the default steel project): `-sheet.png`, `-plan.png`, `-tower-top-plan.png`,
`-aa-top.png`, `-aa-panel.png`, `-aa-base.png`, `-bb.png`, `-title-block.png`.

- **Beam levels per face** (`SteelBeamLevels`): s is the effective car rail bracket spacing of the project. Side and back faces: a beam at every
  floor; every gap between floor beams, the pit and the top is divided equally into the fewest parts not larger than s. Entrance face: a floor
  beam and a door-head beam (the opening between them is not split); from the door head to the next floor beam (or the tower top) the gap is
  divided equally into the fewest parts not larger than s. Levels are editable per face (lock, Reset). The rail check uses the largest gap of the
  side and back faces. There is no steel spacing preset.
- **Shaft page:** Shaft construction (Concrete / Steel structure); for steel the column, beam and bracing lists (UPN 80 to 200, L40 to L60),
  base plate (200 x 200 x 15) and bolts (4 x 20), and under Advanced the gusset plate, steel grade, concrete base and the beam levels of each face.
- **Plan:** the walls and their hatch are hidden (never edited: they stay bound) and the four columns are drawn as solid sections (web outside,
  flanges in) with the 200 x 200 x 15 base plates and the bolt circles, and the beam ring. Labels "UPN 140 STEEL COLUMN", "UPN 100 STEEL BEAM",
  "BASE PL ...". The door and the clearances are unchanged; the door frame is fixed to the beams (no wall opening is drawn).
- **Tower-top plan** (in place of the machine-room plan): the machine within the shaft footprint on the top ring of columns and beams, no outriggers;
  the room walls, ventilation, entrance notes, concrete beams and control panel are hidden; the note "CONTROLLER AND SWITCHES AT THE TOP FLOOR,
  RIGHT OF THE DOOR AS SEEN FROM THE LANDING" is drawn under the tower.
- **A-A:** the tower elevation seen on the left face: columns, the beams at the levels of that face, the beams of the entrance and back faces
  end-on, the double cross of L50x50x5 in every panel with its gusset plates (PL 400 x 250), the concrete base with its hatch, the platform on top
  and the labels; the walls, the slabs and the machine-room walls are hidden. The machine-room floor level follows the top beam (the platform
  replaces the slab: `SectionAAProject.SteelPlatformDropMm`), and the rail brackets sit on the beams (the bracket marks are the beam levels).
- **B-B:** the slab and the concrete bearing blocks are hidden; the front and back top beams (UPN channels cut across) stand under the ends of the
  machine beams, with the column stubs below them.
- **Sheet:** for a steel shaft the two masonry notes of the title block ("all dimensions are for finished masonry", English and Arabic) are left
  out and a STEEL entry is added to the legend. The template file is not edited (the assembler erases and adds in the generated layout).
- **Concrete projects** are untouched: every steel step reads the project first and does nothing for a concrete shaft (tests: `steel-native` has
  the same project as concrete; the native golden is unchanged).

Not done in S3 (list for a later stage): the generated steel details (splice, beam-column joint, base plate and bolt; stage G); the face-by-face
bracing choice and the structural check (the STEEL module); STEEL in STEP; the bracing of the entrance face is never drawn.
