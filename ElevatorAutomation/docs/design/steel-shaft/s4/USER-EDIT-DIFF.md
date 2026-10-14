# S4 - the user's own edit of a steel sheet: rules

Source: `output/solution-v01/NASSAR DEVELOPMENTS-GB-RCW-W1800D1400-ROMA LIFT-MODEL00.dwg` (saved 10:26, "I changed a lot"), against
the program output of the same run (`.bak`, 09:27). Project: steel construction, shaft 1800 x 1400, 5 stops, floors 3000, pit 1300,
overhead 4200, capacity 480 kg (`project.elevator.json`).

Files here (all copies; the originals were not touched):
`user-edit.dwg/.dxf` (his edit), `program-output.dwg/.dxf/.pdf` (the program's run), `current-output.dwg/.dxf` (same project, this
branch before S4), `new-output.dwg` (same project, this branch after S4); `plots/` has the sheets, the per-view zooms and the
side-by-side images (`side-sheet.png`, `side-tower-top.png`, `side-plan.png`, `side-aa-top.png`, `side-bb.png`: his edit | new output).

Method: DWG -> DXF (Core Console DXFOUT), entities matched by handle, tag (`ELEVATOR_AUTOMATION` xdata) and extents; layout
`SM_Geared_A0` viewports compared by scale and position. Counts in model space: 15 tagged entities deleted, 838 new (830 untagged),
1921 moved; layout: 7 gone, 5 new, 6 moved. Of the 830 new untagged entities ~790 are a pasted reference drawing at x = -60 000,
y = +28 000 (far from every view, layer 0: the UPN reference); 31 are the PDF import of two small dimension details (layer PDF_0); the
rest are the items below. They are ignored.

Legend: **matches** = the current branch already does it (before S4); **new** = implemented in S4; **question** = not
implemented, listed in SESSION_HANDOFF.md for the Planner.

## (a) The steel geometry

| # | Rule (relative to the geometry) | Status |
|---|---|---|
| a1 | The ring beams stand against the clear faces, outside the clear rectangle, one flange width (UPN 100: 50) thick; they run between the columns (front/back beam from the inner flange line of the left column to that of the right column; side beams between the front and back columns). | **new** (S3 fix round 2 had them on the column's outer face, 140 mm outside the clear depth; they are back against the clear faces) |
| a2 | The columns' outer faces are flush with the beams' outer faces on all four sides: the column box is `beam flange width` outside the clear rectangle and reaches (column flange width - beam width) = 10 mm into the clear width and (column depth - beam width) = 90 mm into the clear depth. The envelope of the tower is the clear rectangle + the beam flange width on every side; one flat outer face per side. In the edit all four corners moved by (+10, +90) mm inwards with their plates (200 x 200, centred on the column box) and bolts; the beams shortened to the new runs. | **new**; **conflicts with the Planner's "the clear W x D stays clear: steel outside it"** (S3 fix round 2). Applied because it is the user's explicit geometry; see question Q1 |
| a3 | The web is on the outer side, the flanges point into the shaft (left columns web at x min, right columns web at x max), as before. | matches |
| a4 | Base plate 200 x 200 x 15, 4 bolts 20 mm, centred on the column box; concrete base under the plates. | matches |
| a5 | Beam levels per face (floor beams, equal division <= car rail bracket spacing, door-head on the entrance face), X bracing with L50x50x5 in every panel on the three side faces, the entrance face free. In A-A the edit shows the same panels and beams. | matches |
| a6 | The gusset plates at the base level are not drawn (the user deleted the two lowest ones, SectionAA.Steel.067/068; the base plates take the connection). The gussets at the upper panel corners stay. | **new** |
| a7 | The top of the tower: ring of columns and beams, no outriggers, machine within the footprint. | matches |

## (b) The four views

| # | Rule | Status |
|---|---|---|
| b1 | Shaft plan: the steel columns are named at both left corners (two "UPN 140 STEEL COLUMN" with leaders), the beams on both side faces ("UPN 100 STEEL BEAM" left and right), "BASE PL ..." as before. | **new** (before: one column label, one beam label) |
| b2 | Tower-top plan: the two front columns are named, the leaders running down and outwards, and the beam is named on the right. | **new** |
| b3 | Control panel: the symbol of the concrete room's control panel is on the landing side, in front of the entrance face, right of the door as seen from the landing, turned a quarter so that the fan of its door faces the landing. (Before: right of the tower, at the side, unturned.) The label text in the edit is "CONTROL PANEL"; the Planner's text "CONTROLLER AT TOP FLOOR" is kept. | **new** (position and rotation); label text: question Q4 |
| b4 | B-B: the steel profile sections (two UPN channels) and the two profile labels are deleted; the column stubs with the break stay; the template's bearing blocks and the machine beams stay. | **new** |
| b5 | B-B title "SEC AT B-B 1:10" is visible. (The program output hid it: the title is tagged with the slab of the master. The user typed a new "SEC B-B 1:10".) | **new** (bug fix: the title is kept) |
| b6 | A-A: machine room walls, slabs, pit floor hidden, steel tower with the X bracing and the platform, same as before; the machine-room label and hook hidden. | matches |
| b7 | A-A: a thin box (walls and a 57 mm roof plate with 160 mm overhang) over the platform, 1948 mm high, as wide as the column envelope (1680 mm for D = 1400), and the machine group (92 + 64 entities) moved up by 960 mm into it. | **question Q3** |
| b8 | Plan and tower-top plan: a concrete strip of the building along the entrance face, 250 mm deep, 3500 mm wide for W = 1800 (= W + 2 x 850), directly outside the front face (drawn as a plain rectangle in both). | **question Q2** |
| b9 | Tower-top plan: a dimension "725" from the governor to the machine. | **question Q5** |
| b10 | Door width 800 instead of the program's 1000: the opening, the jamb dimensions (500 -> 400, 200 -> 300) and the D-D view figures (1100/800). It is the door input of this project, not a drawing rule. | not a rule |
| b11 | B-B vertical dimensions: "640" for the program's "620,52", "160 / 160 / 160" for "160 / 140 / 350"; the ConcreteHeight dimension (350) and the rope-fall box note are deleted. | **question Q6** |

## (c) The sheet

| # | Rule | Status |
|---|---|---|
| c1 | Arrangement: plan bottom-left, tower-top above it, the rail-bracket detail above A-A, A-A in the middle, B-B top right, door views and details at the right, the car/C.W. guide details bottom right. The program's arrangement is automatic; his is manual. | not a rule (the automatic arrangement is kept; with the B-B title visible the new sheet places the views in a similar order) |
| c2 | Scales: A-A 1:40 (program 1:50), B-B 1:10 (program 1:20), door view 1:20 (program 1:10), tower-top and plan 1:10, details 1:5. | not implemented: they follow from the manual arrangement (the free space he made); question Q7 |
| c3 | The door section "SEC. C-C 1:20" is kept next to "VIEW at D-D 1:20". The S3 fix round (Planner) leaves C-C out of steel sheets. | **question Q8** (contradicts the Planner's S3 item 3) |
| c4 | Title block: the company box holds the client's logo image ("ROMA ELEVATORS" with a crest); the owner and project boxes are empty; the title-block text and the revision date follow the project. | not a rule (client data) |
| c5 | Legend with the STEEL swatch, the masonry notes erased, the "CONCRETE / REINFORCED / BRICK" legend line. | matches |
| c6 | Titles: "TOWER TOP 1:10", "SHAFT AND CAR 1:10", "SEC AT A-A 1:40", detail titles with their scales. | matches (the scale numbers follow the viewports) |
