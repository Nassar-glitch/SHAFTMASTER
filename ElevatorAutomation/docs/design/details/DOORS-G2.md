# Landing door detail (stage G2): the user's master doors

Status: inventory and rules for the Planner's review. No code yet.

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
| Structural opening (dashed) | 150 beyond the frame each side (1100 = 800 + 2 × 150), top 50 above the frame | **width follows**: opening + 2 × margin. The margin is a lock / follow preset; the default is taken from the drawing, see question Q1 |
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

## 5. Questions for the Planner / user

1. **Q1 Structural margin:** this drawing has 150 each side beyond the frame (1100 for an 800 opening). The old template had 200 beyond the clear opening (1100 for 700). Which is the default? (It is a lock / follow preset either way.)
2. **Q2 Automatic types:** 2S and 2C share the same elevation (two equal panels) and differ only in the arrows. Is that correct? The file shows one arrow set (both left).
3. **Q3 Indicator side:** for 2C, which side does the indicator go on (the drawing: right)? For 2S: away from the stacking side?
4. **Q4 Swing handle:** is the handle on the left in the drawing a left-hand or a right-hand door in the project's terms?
5. **Q5 Variant choice:** glass or solid (automatic), and glass or window (swing). Is this a new preset on the CAR & DOORS page? (The default would be glass / glass? or solid / window?)
