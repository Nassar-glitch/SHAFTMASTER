# Layout templates

`build_template.py` builds the reduced A0 geared sheet template from the user's DXF:

```
pip install ezdxf            # tested with 1.4.4
python -I tools/layouts/build_template.py
```

- **Reads:** `Layouts_Sec/01_Geared_MR_1-1_LAYOUT.dxf`, read-only (44 MB).
- **Writes:** `Templates/Layout_Geared_A0.dxf` (about 1.9 MB) and `Templates/Layout_Geared_A0.details.json`.
- **Deterministic:** the same source gives the same bytes. The header metadata is fixed, entities are processed in a fixed order, and coordinates are written to 0.001 mm.

The analysis behind it is `docs/design/layouts/LAYOUTS.md`. The binding decisions are in `DESIGN-FLOW.md`, "Layouts and PDF".

## What the template keeps

### Paper space

Layout `SM_Geared_A0`. The paper units are millimetres on A0 landscape (1189 x 841).

- **Units conversion:** the source was drawn in centimetres (1 unit = 9.969 mm, see LAYOUTS.md 1). It is scaled so that the outer sheet border is exactly 841 mm high, and centred on 1189 mm.
- **Kept:** the frame, the zone marks and the whole title block, including the flattened English / Arabic labels, notes, legend, north arrow and the Arabic field labels in xarab.
- **Removed from the title block:**
  - the sample project's value texts (P1–P7 and their `P1 =` labels, the data-table values, the company name lines, the owner / project placeholders, the old drawing-name value, `A1 - 1 \ 100` and the drawing number `1`);
  - the bar scale, because it was only valid for one scale and the scale cell now reads "AS SHOWN";
  - the OLE logo;
  - the old viewports and the stray plot markers.
- **Logo area:** a no-plot frame on layer `SM_LOGO_AREA` marks where the company logo image goes (60 x 45 mm).
- **Plot settings cleared:** no plot device, no plot-style table, plot type "layout", scale 1:1, no rotation. The PDFCreator printer named in the layout's extension data is removed as well. The plot setup itself is local work.
- **`$INSUNITS` = 4 (mm), `$MEASUREMENT` = 1.**

### SM_TITLEBLOCK

One block holds an attribute definition per value field. It is inserted once in paper space at (0, 0), with empty attributes. The tags are the ones in `Elevator.Core.Layouts.TitleBlockData.Tags`, and a Core test checks that the two lists match:

| Tags | Content |
|---|---|
| `P1` … `P7` | the loads, "P1 = 545 KG." |
| `LOAD`, `SPEED`, `TRAVEL`, `STOPS`, `FLOORS`, `ENTRANCES`, `ENTRANCE_NAMES`, `CONTROL`, `SUPPLY` | the data table |
| `COMPANY_AR`, `COMPANY` | the company name: the Arabic line when it is typed in Arabic, else the Latin line |
| `REV_ROW1_NO`, `REV_ROW1_DATE` | the first revision row (the row above the table header) |
| `CLIENT`, `PROJECT`, `DOC_NO` | owner, project name and project number |
| `DRAWING_NAME`, `TYPE_OF_WORK` | drawing name (no source yet) and type of work |
| `SCALE`, `PREPARED`, `CHECKED`, `APPROVED` | scale cell and signature table |
| `DATE`, `PHASE`, `SHEET_NO` | bottom row |

Each attribute uses `SM_EN` (cent.shx) or `SM_AR` (xarab.shx). The filler picks the style per value with `TitleBlockAttribute.Style` and writes `TitleBlockAttribute.DrawingText`, which is the xarab encoding for Arabic text.

### Model space

One representative of each detail kind is kept as reference geometry for the later generated details. Each is placed at the reserved offset x = 1,000,000 mm, left to right with 2 m gaps. Their windows, kinds and template scales are listed in `Layout_Geared_A0.details.json`.

| Name | Kind | Source window |
|---|---|---|
| `FISH_PLATE` | fish plate and rail fixing | the template's viewport 2 |
| `RAILS_PLAN_CW5_CAR9` | car and CW rails with brackets, plan | viewport 5 (the library's only plan set) |
| `RAIL_BRACKETS_CW5_CAR9`, `_CW5_CAR16`, `_CW9_CAR16` | rail-bracket elevation with the bracket spacing, one per rail combination in the library (5-9, 5-16, 9-16) | viewport 6 and the same window around the other two sets |
| `DOOR_AUTOMATIC` | door elevation D-D and section C-C, automatic | viewport 8 |
| `DOOR_SWING` | door elevation D-D and section C-C, swing (single leaf) | the swing-door library, first single-leaf door |

- **Dropped:** the rest of the library (about 89,900 entities) and its two duplicate copies of each entity.
- **Purged:** blocks that nothing references any more, the association network and the unused image definitions.

### Styles

- **Kept:** the template's text styles and SHX fonts (xarab, cent, txt, Arial).
- **Added:** `SM_AR` (xarab.shx) and `SM_EN` (cent.shx).
- **Renamed** (they clash with AutoCAD built-ins); every entity now uses the `SM_` copies:
  - text style `Standard` → `SM_STANDARD`;
  - dimension styles `ISO-25` → `SM_ISO25`, `Copy of ISO-25` → `SM_ISO25_COPY`, `Standard` → `SM_STANDARD`.
- The `Standard` text and dimension styles stay in the file, as DXF requires.

## Not done here (local work)

- the plot setup (device, paper, style table) and the PDF export;
- the generated viewports, from `Elevator.Core.Layouts.SheetArrangement`;
- filling the attributes, from `TitleBlockData.Attributes()`;
- the logo image;
- comparing the layer table with the generated drawing's layers (see LAYOUTS.md 5).
