# Landing door detail (stage G2): the user's master doors

Status: implemented on `feature/details-g2` after the Planner's review (answers in section 6).

## 1. Sources

The originals in the workspace root stay untouched. The copies are in `g2/source/`:

| File | Content |
|---|---|
| `Automaitc700-Glass&solid.dxf` | Automatic 2-panel (centre opening) door at 700, two variants: **glass panel** (left), **solid** (right) |
| `Swing-Glass&Window.dxf` | Swing (manual) door, two variants: **glass panel** (left), **small vision window** (right) |

Each door has two views: `VIEW at D-D 1:20` (elevation) and `SEC. C-C 1:20` (section).

Renders:
- [g2/automatic-source.png](g2/automatic-source.png)
- [g2/swing-source.png](g2/swing-source.png)

## 2. What is fake (PDF import)

Everything is on layer 0. There are no blocks except `_ArchTick`, and every entity appears **twice** (duplicate copies).

| File | Polyline segments, 2–3 points | Hatches | Texts | DIMENSIONs |
|---|---|---|---|---|
| Automatic | 3366 (continuous) | 14 | 4 MTEXT (`700`, `700`, `700`, `1000`) | none |
| Swing | 2946 (continuous) | 1 | none | 4 |

Notes on the two files:
- **Automatic texts:** the remaining wording, such as `STR.`, `ENTRANCE HEIGHT` and `INDICATOR`, is glyph strokes.
- **Automatic hatches:** SOLID (arrow heads, sill), GOST_GLASS (glass), ANSI31 (small parts).
- **Swing:** in addition there are 64 LINEs, 8 CIRCLEs (handle) and 1 GOST_GLASS hatch.

| Fake item | Where | Rebuilt as |
|---|---|---|
| Dimensions (lines, ticks, glyph or MTEXT numbers) | `STR. 1100` / `1100`, clear `700`, section `200` / `160`, `2000 ENTRANCE HEIGHT`, `60` | real DIMENSIONs on A-DIMS, placed by DimensionLayout, values from the project |
| Dashed outline (short segments, about 6–18 mm) | structural opening around the frame; wall edges | one polyline, linetype DASHED (HIDDEN for the section), LTSCALE per the sheet scale |
| Centre lines (segments) | the vertical axis through the door and the C-C cut line | LINE with linetype CENTER; the C-C mark is a polyline with arrows and two MTEXTs "C" |
| Text strokes | `STR.`, `ENTRANCE HEIGHT`, `INDICATOR`, `FINISH FLOOR LEVEL`, `VIEW at D-D 1:20`, `SEC. C-C 1:20`, `C` | MTEXT or MLEADER on A-ANNO, same wording; titles show the real scale |
| Hatches | glass, wall / slab, sill, arrow heads | HATCH: GOST_GLASS for glass; wall and slab outline only (the solid-plot issue of G1); arrow heads SOLID |

**The labels do not match the geometry.** The drawing is 1:1 in mm, but:
- The clear opening is drawn **800** wide (x 399–1199, the extension lines), yet labelled `700`.
- In the automatic glass view, `STR.` is drawn **1100** wide (x 249–1349) but labelled `1000`.

So the program takes the shapes and the fixed offsets from the drawing, and every value from the project. No number is copied from the labels.

## 3. Measured geometry (mm, 1:1, floor = 0)

### Common frame (all 4 doors)

| Part | Size | Rule |
|---|---|---|
| Clear opening | 800 wide × 2000 high | **stretches**: width = project OPENING, height = entrance height |
| Frame (jamb) | 120 each side (outer frame 1039 = 800 + 2 × 120) | **fixed** |
| Frame header | 170 above the opening | **fixed** |
| Inner jamb line | 50 inside the frame edge (swing) | **fixed** |
| Structural opening (dashed) | 150 beyond the clear opening each side (1100 = 800 + 2 × 150, 30 beyond the frame), top 50 above the frame | **width follows**: opening + 2 × margin. The margin is a lock / follow preset; the default is taken from the drawing, see question Q1 |
| Section | wall above (header 200 automatic / 160 swing), frame, panel, sill 60, slab with the sill recess | header and sill are **fixed** presets; the panel height **stretches** with the entrance height |

### Automatic door (2 panels, centre opening)

| Part | Size | Rule |
|---|---|---|
| Panels | 2 × (opening / 2), joint on the centre line | **stretch** (width / 2, height) |
| Glass (glass variant) | 320 × 1700 in each 400 panel: 40 from the sides, 240 from the floor, 60 from the top | **insets fixed**, glass stretches. Glass width = panel − 80; height = height − 300 |
| Solid variant | panels without glass | stretch |
| Opening arrows | one per panel, at mid-height | **fixed** size, placed at the panel centres. Direction: 2C outward from the centre; 2S toward the stacking side, **mirror** with stacking L / R |
| Indicator | 69 × 300, 30 outside the opening edge, bottom at 907 above the floor; label INDICATOR | **fixed** size and height. It sits on the side away from the stacking side (the drawing shows the right). **Mirror** with stacking |

### Swing door (one leaf)

| Part | Size | Rule |
|---|---|---|
| Leaf | 800 × 2000 | **stretches** |
| Glass panel frame (glass variant) | 600 × 1800, 100 from each edge | **insets fixed**, stretches with the leaf |
| Glass (glass variant) | 520 × 1720, 40 inside the panel frame | **inset fixed**, stretches |
| Vision window (window variant) | 200 × 650; nested 173 × 623 and 153 × 603; centred on the leaf; bottom at 793 above the floor | **fixed** size and height. Stays centred on the leaf |
| Handle | lever with rose (8 circles), about 140 wide, at 1006 above the floor, about 25 inside the leaf edge | **fixed** size and height. **Mirror** with handing (the drawing shows the handle on the left) |
| Section | leaf with the glass (glass variant) or the window profile at 793–1443 (window variant) | the leaf stretches with the height. The window stays at its fixed height |

## 4. Rules: stretch, mirror, parameters

- **Parameters** for each door:
  - opening width (plan OPENING) and entrance height (the preset `Doors.Detail.ClearHeightMm`, carried from the parked G2 work);
  - type: automatic 2S / 2C, or swing;
  - variant: glass / solid (automatic), or glass / vision window (swing);
  - side: handing L / R (swing), stacking L / R (automatic).
- **Width stretch:** only the opening, the panels or leaf, the glass, the header length and the structural opening change. Frame sections, insets, handle, vision window, indicator and arrows keep their size.
- **Height stretch:** the opening, the panels or leaf, the glass (minus its fixed insets) and the jambs change. The vision window, handle and indicator keep their height above the floor.
- **Mirror:**
  - **Swing:** mirror about the door axis for the other handing; the handle moves to the other side.
  - **Automatic:** for stacking, the arrows and the indicator mirror. The drawing's arrows (both pointing left) are the 2S stacking-left case.
  - Text is never mirrored; dimensions and labels are re-placed after the mirror.
- **Blocks:**
  - Each door elevation is a named block `SM_DOOR_<type>_<variant>_W<w>_H<h>_<L|R>`, built by the program from the parameters. This is the same approach as the brackets, and the block stays editable.
  - Dynamic blocks (stretch and flip parameters) are not reliably writable from the .NET API: their parameter and action objects are not exposed for creation. So I propose named blocks.
- **Native output:**
  - layers: A-DOOR (door), A-GLAZ (glass hatch), A-WALL, A-ANNO, A-DIMS;
  - real linetypes DASHED, HIDDEN and CENTER, with LTSCALE for 1:20 (or 1:10);
  - MTEXT and MLEADER labels in the user's wording;
  - every dimension a DIMENSION;
  - checks: no overlaps or crossing leaders, and the opening and height must equal the plan (the plan-detail check of the parked work).
- **Template:** the template windows DOOR_AUTOMATIC and DOOR_SWING are replaced by the generated detail for the project's door.

## 5. Questions (answered by the Planner, see section 6)

## 6. Implementation

- **Masters as data.** `tools/door_masters.py` extracts the four doors from `g2/source` into `Templates/DoorMasters.json`
  (render: [g2/masters-clean.png](g2/masters-clean.png)). The extraction:
  - removes duplicates;
  - merges dash runs into one line, marked HIDDEN, or CENTER for dot-dash runs;
  - drops hatch strokes, glyphs, the fake dimensions, the opening arrows and the INDICATOR leader;
  - chains the pieces into polylines.
  
  Coordinates are in mm. The elevation is relative to the opening centre and the section to its landing edge; the floor is 0.
- **Stretch** (`Elevator.AutoCAD/Sheet/DoorDetails.cs`, class `Stretch`):
  - **Width:** a point with |x| > 45 (automatic) or 150 (swing) moves by ±(W − 800)/2. So the frame, the glass outer edges, the handle and the indicator move with the opening edge; the centre joint and the vision window stay.
  - **Height:** a point above 1500 moves by H − 2000. So the handle (1006), vision window (793–1443), indicator (907–1207) and sill keep their height; the header, the glass top and the wall above move.
  - **Section:** only the height rule applies.
- **Mirror:**
  - swing, hinge side Left;
  - 2S, stacking right;
  - 2C is never mirrored, and its indicator stays on the right jamb.
  
  The block is mirrored in its geometry, so texts are never mirrored.
- **Blocks:**
  - the elevation is `SM_DOOR_<2S|2C|SWING>_<SOLID|GLASS|WINDOW>_W<w>_H<h>_S<str>[_L|_R|_HL|_HR]`;
  - the section is `SM_DOOR_SEC_<master>_H<h>[_STEEL]`.
- **Native items:**
  - glass is HATCH GOST_GLASS on A-GLAZ;
  - the structural opening is a DASHED polyline;
  - the axis and the crosshairs are CENTER;
  - the section break lines are HIDDEN;
  - dimensions are real DIMENSIONs: the opening, "STR. <S>" (automatic) or "<S>" (swing, the masters' wording), ENTRANCE HEIGHT, the header and the sill;
  - labels are multileaders: INDICATOR, FINISH FLOOR LEVEL;
  - the opening arrows are redrawn per type: 2C outward; 2S toward the stacking side;
  - the C-C marks are drawn natively.
- **Structural opening.** It is the plan's wall opening (Part009 + OPENING + Part011) when the plan has it. Otherwise it is opening + 2 × margin (preset 150, measured from the clear opening: the master's 800 gives 1100, 30 beyond the 120 frame; the inventory's "150 beyond the frame" was a mistake).
- **Steel.** The frame sits between two jamb posts under the door-head beam (elevation). In the section, the beam replaces the wall above the header, and the master's wall-above outline is left out.
- **Presets** (CAR & DOORS → Landing door detail, lock / follow):
  - Door panel: automatic Solid / Glass, default Solid; swing Vision window / Glass, default Vision window;
  - Hinge side (swing): Left / Right, default Right;
  - Entrance height: 2000;
  - Structural opening beyond the clear opening: 150.
- **Check** (`fixing_check.check_door`): the block's opening, height and STR equal the plan's OPENING, "DOOR WxH" and wall opening.

## 7. Findings to note

- **Section header labels:** the swing master labels the header "160", but its geometry is 200 (as in the automatic master). The detail follows the geometry, so it shows 200.
- **Sill labels:** the masters' sill "60" is drawn about 58–59; the detail shows the measured value.
- **Plan hinge side:** the plan of a Manual door (template) has no swing leaf or arc, so there is no hinge side on the plan to compare with. The hinge-side check waits for a plan swing door, if one is wanted.
- **Plan wall opening:** the plan's wall opening is OPENING + 2 × 100. That is narrower than the masters' frame (OPENING + 2 × 120), so on concrete the dashed STR line lies just inside the frame outline. It is still the plan's value, as agreed.

## 8. Review fixes (Planner, 17:48 to 18:30)

- **Section C-C:** the wall above the header and the floor slab are associative ANSI31 hatches. Their pattern scale equals the sheet scale (acadiso, about 3 mm apart on paper, as in the master). Each cut ends in a break line: the slab end, the slab bottom and the top of the wall.
- **Pattern scale rule:** the scale is set before the pattern. AutoCAD ignores a scale set after the pattern, which is why the earlier hatches printed solid. The G1 rail details (`RailDetails.Hatched`) now use the same order and rule. `SteelDrawing.Fill` (AR-CONC in the steel drawings) is unchanged.
- **Glass:** GOST_GLASS at 0.75 × the sheet scale (15 at 1:20, the master's value).
- **Axis and marks:** the axis is CENTER and stops below the dimension rows. The C marks sit above the dimension rows and below the floor, and the dimension texts are to the left of the axis. `fixing_check.check_door_annotation` checks that no texts overlap and that no axis, mark, floor line or leader crosses a text.
- **Steel C-C:** the UPN is drawn at its real size as a C profile, at the beam level of A-A, on the frame header and hanger box. Its label leader starts from the beam's top edge. There is no wall above, so the landing-side lines stop at the header.
- **Header dimension:** rounded to 10 (the masters' 200). The sill dimension shows 60.
- **Evidence:** in `g2/evidence/`:
  - `compare-*.png`: the master next to both sides of each of the 4 doors;
  - the project cases (user-edit, user-project, steel-t89, concrete-2s-700, concrete-2c-1100);
  - `g1-hatch/`: VP5 / VP6 after the hatch fix;
  - `l3-pending/`: the two changed l3 images, waiting for the user's typed approval.
