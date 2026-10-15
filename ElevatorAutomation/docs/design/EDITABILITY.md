# Editable drawings: audit and changes (E1, DESIGN-FLOW principle 11)

Principle 11: a generated drawing must be as easy to edit in AutoCAD as a hand-made one. A label that points at something is one
MULTILEADER; dimensions are real DIMENSION entities; a repeated part is a named block; text is TEXT / MTEXT in the drawing's styles and
hatches are HATCH entities; everything sits on meaningful layers.

How the numbers were made: `tests/integration/audit_editability.py <dxf>` reads a generated DWG (as DXF) and counts, per view (by the
`ELEVATOR_AUTOMATION` key prefix of the entities; the reference details by their place far from the views; the paper space of the layout
`SM_Geared_A0`), the entity types, the layers, the blocks, the leaders and the loose leaders. A "loose leader" is a LINE with an end point
within 60 mm of a text box of the same view: the pair of pieces a user has to hunt for. The client case (shaft 1500 x 2000, 4 stops, steel)
and the same project as concrete were audited, before and after E1. Counts are for one generated drawing.

## 1. What the program draws itself

| Item | Before E1 | After E1 |
|---|---|---|
| Steel labels (beam, column, base plate note, control panel, A-A labels) | a LINE and a separate MTEXT each (12 pairs in the client case) | **one MULTILEADER each**, style `SM_LEADER` (no arrow head, short landing, text height of the view), layer `A-ANNO` |
| Column section in plan (8 vertex outline + solid fill) | 4 polylines + 4 hatches (plan), 4 + 4 (tower top) | **two named blocks** (`SM_COLUMN_UPN_140_60x140x7x10_L` and `_R`, outline + associative SOLID hatch), 4 references per view, layer `S-COLUMN` |
| Base plate with 4 bolts | per plate: 1 polyline + 4 circles (20 pieces) | **one named block** `SM_BASE_PLATE_200x200x15_4B20`, 4 references, layer `S-PLATE` |
| Control panel symbol | 12 loose polylines (copied from the master) | **one named block** `SM_CONTROL_PANEL`, layer `A-EQUIP` |
| Ring beams (rectangle + web line) | polylines and lines on layer `Steel` | polylines and lines on `S-BEAM` |
| Bracing strips, gussets, plates, base block, housing in A-A and B-B | polylines on `Steel` | polylines on `S-BRACE`, `S-PLATE`, `S-BASE` (hatch AR-CONC, associative), `S-HOUSING` |
| Building slab edge | polyline on `Steel` | polyline on `A-SLAB` |
| Governor dimension of the tower-top plan | DIMENSION (cloned from a machine-room dimension: its style) | DIMENSION on `A-DIMS` |
| Dimension rows of the door and the plan (`DimensionLayout`) | DIMENSION entities cloned from the template ones | unchanged: real DIMENSIONs of the drawing's style |
| Titles of the views and the STEEL legend entry | MTEXT in the style of the sheet | unchanged |
| Hatches of the steel parts | HATCH, not associative | HATCH, associative with their boundary |
| Layers | one layer `Steel` for everything | `S-COLUMN`, `S-BEAM`, `S-BRACE`, `S-PLATE`, `S-BASE`, `S-HOUSING`, `A-SLAB`, `A-ANNO`, `A-EQUIP`, `A-DIMS` (plain layers of the default colour: the plotted look is unchanged); the concrete-room entities a steel tower replaces sit on `Steel.Hidden` (switched off) |

The look is the same: `docs/design/steel-shaft/e1/side-*.png` (before | after) for the tower-top plan, the shaft plan, A-A, B-B and the
whole sheet. The multileader text stands at the end of its leader as before; the leader of the left labels meets the text without a
visible landing.

The tags and keys the host relies on (`ELEVATOR_AUTOMATION`, `Plan.Steel.NNN` ...) are written on the new entity types too (MULTILEADER and
INSERT carry them); the readers `verify_steel.py` and `steel_overlap.py` read the parts of a steel block through its reference, a label
through its multileader text, and fail when a steel label is a loose text, when a repeated part is not a block reference or when the steel
entities are not on their layers.

## 2. The template and the reference details (not changed in E1)

Counts of the client case (steel and concrete are identical here: the steel overlay hides, never edits, the concrete entities):

| View | Entities | DIMENSION | MULTILEADER | Loose leaders (LINE ending at a text) | INSERT | HATCH | TEXT / MTEXT |
|---|---:|---:|---:|---:|---:|---:|---:|
| Plan (shaft plan, with the door) | 521 | 31 | 4 | 30 | 0 | 8 | 12 |
| Machine-room plan | 1354 | 14 | 3 | 2 | 0 | 97 | 9 |
| Section B-B | 708 | 10 | 0 | 10 | 1 | 30 | 4 |
| Section A-A | 3013 | 7 | 1 | 3 | 1 | 35 | 18 |
| Template reference details (door, rails, fish plate) | 2016 | 7 | 0 | 0 | 0 | 4 | 16 |
| Sheet (paper space: frame, title block, notes) | 2922 | 0 | 0 | 41 | 1 | 1 | 31 |

What the numbers say:

- **Dimensions** are real DIMENSION entities everywhere (69 in the views). They are the templates' own, written by the updaters
  (`PlanAnnotations`, `AutomaticDoorPlan`, `DimensionLayout`). Associativity is not stored in a DXF; whether the extension points are
  associated with geometry is to be read in AutoCAD (`DIMREASSOCIATE`). The program moves and re-measures them from the design, so they
  follow the design rather than the geometry; this is the intended behaviour and stays.
- **Leaders**: the template has 8 MULTILEADERs (door leader, car and counterweight guide labels, the governor and chassis notes, the
  hoisting hook). The remaining labels of the plan (guide labels "T70/65/9", "T50/50/5", "SPEED GOVERNOR", "C.L. OF GUIDES", the
  section letters ...) are a LINE and a text: about 30 pairs in the plan, 10 in B-B, 3 in A-A, 2 in the machine-room plan. The 41
  "loose leaders" of the sheet are mostly the table lines of the title block that end at their texts (false positives of the count).
- **Repeated symbols**: the guide-rail brackets and clips, the door panels (12 polylines and 12 lines per automatic door, written by
  `AutomaticDoorPlan`), the rail bracket symbol of the plan and the fish plate bolts of the reference detail are drawn as separate
  lines and polylines; there are 2 INSERT in the model (the title block and an anonymous block).
- **Hatches** are HATCH entities (8 + 97 + 30 + 35 + 4); no hatch is made of lines.
- **Text styles** in use: `Standard` (27 MTEXT), `SM_STANDARD` (9), `$2$nassar2` (9), `nassar2` (3), `PDF Arial`, `PDF Arial Bold` and
  `$0$top` (the PDF import of the template): 9 styles for what is one font family.
- **Layers**: the template is a PDF import: `PDF_0`, `$0$PDF_0`, `$1$PDF_0`, `$2$PDF_0`, `$0$PDF_SAS_STB_Side`, `PDF_gide rail`,
  `$0$PDF_PDF6_Geometry` ... hold about 2100 of the 3000 entities of A-A and most of the plan; the meaningful ones (`Car`, `DL`, `CL`, `Wall`,
  `step&guides`, `AA_Datum`, `AA_Architecture`) are a minority and the prefixes `$0$ $1$ $2$` are leftovers of binding external references.

## 3. Proposals (a later step, one at a time, each with its goldens)

Small and safe, proposed for E2:

1. **Merge the text styles** `nassar2`, `$0$nassar2`, `$2$nassar2`, `$0$top`, `PDF Arial*`, `Standard` into `SM_STANDARD` (same font file):
   a style change only, no geometry.
2. **Rename and merge layers** with `LAYTRANS`-like mapping: the `$n$` prefixed copies into their plain name, `PDF_0` families into one
   layer per view (`A-PLAN-GEOM`, `A-SECT-GEOM`, ...). Plot styles keep the look because the colours map one to one.
3. **Turn the 8 + 30 + 10 + 3 + 2 loose label pairs into MULTILEADERs** with the same `SM_LEADER` style: each is a LINE (and
   sometimes a polyline "landing") ending at an MTEXT; the program's updaters move them as keyed entities, so the conversion has to
   keep their keys (the `Plan.Dimension.*` / `Plan.Annotation.*` readers) and re-approve the native goldens by entity type.

Larger, to be decided with the user:

4. **Door panels and rail brackets as named blocks** (`SM_DOOR_PANEL_<width>`, `SM_RAIL_BRACKET_<profile>`): the door is audited by
   the doors-native geometry audit entity by entity; the audit has to read block references first.
5. **The reference details** of the sheet (fish plate, brackets, door sections) are a PDF import of 2000 entities: re-draw them as
   blocks with their dimensions, or keep them as a read-only underlay (`PDFUNDERLAY`).
6. **Associative dimensions** (extension points tied to the geometry with `DIMREASSOCIATE`) where the geometry is a program entity
   (the car, the door), so a hand edit of the geometry moves the dimension: only after step 2, because it needs stable geometry.
