# Steel shaft structure (UPN): reading of the client drawing and proposed model

Stage S1 (analysis only, no product code). Source: `UPN STEEL SHAFT.dxf` (the user's drawing, shaft only, no lift; AC1032, units mm,
10 MB). A copy for the cloud is in `source/` (the original at the repository root was not modified). All renderings in this folder were
made from that file with ezdxf + matplotlib (black on white, lineweights flattened, so hatches and splines look denser than in
AutoCAD). Dimensions below are the drawing's own dimension values; geometry that was measured from entities is marked "measured".

Contents: 1 sheets and views, 2 plan rules, 3 elevation rules, 4 details and notes, 5 proposed model, 6 questions, 7 inconsistencies in the drawing.

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

## 5. Proposed model

### 5.1 Inputs and presets (Core)

- **Shaft construction** (shaft page): Concrete (default) | Steel structure. `Steel` group in the project:
  - Construction system: `UPN` (first one; the field exists so other systems can come later).
  - **Column profile:** list from the UPN catalogue (preset UPN 140), also placed at the 4 corners; **Beam profile:** list (preset UPN 100,
    the user's rule; the drawing itself uses U14 for both); **Bracing profile:** list from the L catalogue (preset L50x50x5).
  - **Bracing scheme:** None | Single diagonal (zigzag) | Double cross (X); per face: front, back, left, right; presets: UPN10
    none, current client double cross on the side and back faces, entrance face none (see question 1).
  - **Beam levels:** preset rule "a beam at every floor level (the doors are fixed to the steel), the space between floors divided
    equally, preset 2 spaces per floor (3250 -> 1625), editable". Also the pit-top level and the top beam. Editable list of levels
    (each locks when edited, Reset gives the rule back); like every other input it starts from the preset.
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

### 5.2 Drawings (what each view shows)

- **Plan:** column sections (channel with its web outwards, flanges into the shaft, as in section 2) in place of the concrete wall
  hatch, the base plates, bolts and the beam ring; the shaft dimensions W and D measured clear between the steel faces. Doors and
  car as today.
- **Section A-A:** columns as two lines, beams at every level, the bracing per face as above, base plates and the concrete base,
  with the bracket levels on the beams; floors and pit / OH unchanged.
- **A0 sheet:** the same views in the user's arrangement; a detail group for the column splice, the beam-column joint and the base
  (stage G, postponed).
- **STEP:** later, same model (columns, beams, angles as extrusions along their profile).

### 5.3 Core (cloud) and AutoCAD (local)

- **Core / Reporting (cloud, tier 1):** catalogues, input model and presets, beam-level rule, the derived bracket spacing,
  the plan / section geometry as data (column rectangles, beam lines, brace polylines, labels with the profile names), the
  steel member list (profile, length, count, mass: the base of a bill of materials and, later, the STEEL STRUCTURE calculation), the
  sheet and Design Notes text, xUnit tests.
- **AutoCAD (local):** drawing the channel section, the hatch, the beam / bracing linework in plan and A-A, dimension chains,
  layers (CL, S-PLATES, S-TEXT conventions of the client file), sheet assembly, PDF.

### 5.4 Catalogue (DIN 1026-1 / EN 10279 standard sections; to be verified by the cloud session against a manufacturer table)

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
Source: DIN 1026-1:2000 and EN 10279:2000 (hot-rolled steel channels), EN 10056-1 (equal angles); the values above are the standard
catalogue values from memory of the common producer tables (not fetched here: no web access in this analysis); cloud
session to confirm and cite the producer table. The drawing itself only draws the section outline and does not state any of these.

## 6. Questions for the user (only what the drawing does not settle)

1. **Braced faces:** sheet E braces the two 2000 faces and the 1500 back face, never the entrance face. For the target "double
   cross (X) bracing", is that the same face set (sides and back, not the entrance)? Is it a double cross in every panel, or one
   per floor?
2. **Beam at the door head:** beams at floor levels sit at the sill level. Do the automatic doors need a second beam at the
   header (door height above the floor, about 2100 to 2400 mm)? The drawing shows none.
3. **Pit and top levels:** is the first beam at the pit top (1150, as drawn) or at the pit floor plate? What are the top
   beam level and the cap (the roof pyramid): at the overhead height 4100 above the top floor, or at the machine-room slab?
4. **Columns on a 2000 face:** the plan shows 4 corner columns only. Do you want intermediate columns on faces wider than 2000
   (bigger shafts), and from what width?
5. **Machine room:** does the machine room (or the machine on a beam) sit on this steel, so the top beams carry the machine
   reactions (P1-P4 become steel loads), or is it a separate structure?
6. **Base:** is the plate 200 x 200 x 15 with 4 x Ø20 fixed for every shaft, or scaled with the column? Does the program draw the
   concrete base (2600 x 250, Ø8 @ 120) or leave it to the structural engineer?
7. **Profile of beams on the drawing:** the drawing says U14 for beams, your target says UPN 100 beams every 1500. Confirm that the
   beam profile preset is UPN 100 and the column UPN 140.
8. **Notes and remarks:** keep notes 2 to 5 (specification) as the preset text of the steel sheet and drop note 1 and "finished
   masonry"?

## 7. Inconsistencies in the drawing (not decisions; for information)

- Total shaft height is `16500` (title block and dimension) but the A-A dimension chain 1150 + 9 x 1600 + 500 adds to 16050 (and
  UPN10 sheet C: 700 + 9 x 1600 + 500 = 15600 against 16050). The C-C chain 1150 + 11250 + 4100 = 16500 is consistent.
- The plan section is drawn about 64 x 126 (outline) while the labels, details and notes say UPN 140 (60 x 140).
- `SHAFT WIDTH 1500 / DEPTH 2000` on sheet E, `2000 x 950 / 900` on sheet B / C (3 different depth values 950, 900 in text).
- The sections are called `C-C` twice in the file (strip D and sheet E) with different bracing.
- Some texts are in `S-TEXT` / `STRUCADTXT` layers with a style that has a missing font in this file: shown as boxes in the
  renderings (title block cells).
