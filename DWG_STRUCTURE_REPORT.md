# Existing DWG structure and proposed parametric generator

Analysis date: 7 September 2026. Source: `plan codex.dwg`, 84,669 bytes.

**Recommendation:** retain this DWG as the authoritative template and implement the reader/updater as a C# plug-in hosted by AutoCAD 2023. Keep elevator rules and geometry calculations separate from AutoCAD. Reuse the existing entities and their appearance through verified component bindings; no redesign is needed.

This delivery is the inspection stage. A read-only C# audit was compiled and executed successfully against the native DWG. No entities, layers, blocks, dimensions, or constraints in the original DWG were changed. Inspection artifacts were created separately. The generator and its write operations have not been implemented.

The source SHA-256 before and after inspection was:

`5F2CE6B190811338D1E446EEEAC9F17E0EE3B6A8DF563654EB98F324B4649447`

**How the drawing was accessed**

The first inspection used AutoLISP `entget` in AutoCAD Core Console on a copy. This exposed entity handles and DXF group data, but its printed coordinates had limited precision. A standalone ObjectDBX COM activation attempt failed with a module-loading error in this environment. That does not establish that all COM access is unavailable.

The definitive audit used AutoCAD's managed API inside the installed AutoCAD 2023 Core Console: `Database.ReadDwgFile`, `OpenForReadAndAllShare`, a side database, and transactions that opened objects only `ForRead`. It traversed block definitions and their entities, layer/style tables, extension dictionaries, the named objects dictionary, and persistent reactor references. It exported full-precision coordinates and native dimension measurements for the geometry collected. It never called `SaveAs` or opened a source object for write. Core Console used a disposable host copy; the exported audit read the original separately. The host rewrote its disposable copy on exit, so that scratch copy was restored from the unchanged original and the runner was corrected to discard host changes. DXF conversion was unnecessary.

The JSON is an inspection inventory, not a complete geometry serialization: it does not include spline knots/weights, hatch loop curves, or all multileader vertices. The updater should operate on those original native objects and extend the reader where detailed shape/clearance validation requires it.

Files produced:

- `.dwg-inspection/DwgStructureAudit.cs`: executed C# inspection source; this is an audit utility, not the production updater.
- `.dwg-inspection/structure-audit.json`: native inventory, entity geometry, tables, dictionaries and relationship evidence.
- `.dwg-inspection/geometry-audit/geometry-findings.md`: detailed component and datum analysis.
- `.dwg-inspection/geometry-audit/component-role-seed.json`: partial role bindings with handles and geometry signatures. It is not a complete, update-ready component manifest.
- `.dwg-inspection/model-entity-inventory.csv`, `dimension-inventory.csv`, and `layer-inventory.csv`: compact review tables derived from the native audit.

**1. Actual organization of the DWG**

Units are millimetres. The DWG header version is AC1032. All 521 top-level drawing entities are in model space. `Layout1` and `Layout2` both have empty paper-space block records.

| Model-space entity type | Count |
|---|---:|
| Lightweight polyline | 262 |
| Line | 131 |
| Arc | 68 |
| Rotated dimension | 31 |
| MText | 12 |
| Hatch | 8 |
| Spline | 5 |
| Multileader | 4 |
| **Total** | **521** |

There are **11 defined layers**, of which **10 contain model-space entities**. `Defpoints` is empty at model-space level. All defined layers are on, thawed, and unlocked; `Defpoints` is nonplotting.

| Layer | Model entities | Observed organization |
|---|---:|---|
| `Wall` | 4 | Inner/exterior shaft outlines and two front wall returns; all polylines |
| `step&guides` | 17 | Cabin inner/outer linework, entrance throat details, and counterweight outline |
| `Car` | 14 | Centre crosses and section symbols; not the cabin boundary |
| `CL` | 5 | Centre/reference lines and one leader |
| `DL` | 9 | Spanning/reference/detail lines and small rectangles |
| `PDF_0` | 238 | Dense fragmented profile/detail geometry: 190 polylines and 48 arcs |
| `PDF_gide rail` | 36 | Rail-detail geometry: 16 polylines and 20 arcs |
| `0` | 192 | All 31 dimensions plus mixed equipment details, text, hatches, splines and leaders |
| `7` | 5 | Equipment and centreline text |
| `6` | 1 | One line |
| `Defpoints` | 0 | Defined but no top-level model-space objects |

The `PDF_` names and fragmented geometry suggest a previous import or decomposition workflow; the audit does not establish the drawing's editing history.

**Blocks, groups, references and constraints**

- There are **zero model-space block references**, zero dynamic component blocks, and no external-reference block definitions.
- The block table has **36 records**: three model/paper-space containers, 31 anonymous dimension graphics blocks, and the two named dimension symbols `_Oblique` and `_ArchTick`.
- These dimension graphics blocks are generated representations; they are not reusable cabin, rail or door components.
- The `ACAD_GROUP` dictionary is empty.
- No geometric/dimensional constraint group, parameter network, or custom elevator-role metadata was found in the inspected dictionaries and object relationships.
- Ten `AcDbDimAssoc` objects were found. Those are dimension association records, not a parametric design solver.
- No model-space proxy entity was reported by the native API.

**Dimension and hatch behavior**

All 31 dimensions use style `top`, have `Dimlfac=1` and `Dimscale=1`, and reside on layer `0`. The drawing also defines four unused dimension styles: `ISO-25`, `Annotative`, `Copy of ISO-25`, and `Nassar2`. Text styles include SHX-based `cent.shx` styles and inline MText font overrides; production output should retain and verify them.

Only these ten dimensions carry `DIMASSOC` reactor records: `612`, `615`, `620`, `62B`, `634`, `639`, `63C`, `677`, `67A`, `683`. The remaining 21 have no such record in the audit. The native generic wrapper did not expose individual point-reference validity, so the presence of a record is not proof that every endpoint association is healthy.

Examples of useful shared association records link shaft-width dimension `62B` with shaft polyline `628`, car-width dimension `63C` with rear cabin line `525`, and car-DBG dimension `639` with rail-face lines `58E` and `5EB`.

Shaft depth `671`, cabin depth `633`, door width `61B`, opening `61F`, and counterweight DBG `519` have no dimension association record. A future updater must explicitly update their defining points. Changing the value shown by `SHAFT WIDTH` or `SHAFT DEPTH` is not sufficient to move the plan geometry.

Dimension labels contain measurement placeholders such as `SHAFT WIDTH <>`, `CAR WIDTH <>`, and `DOOR <>X2000`. Preserve the label formats and update measured geometry. The `2000` door height is literal annotation text; it is not a vertical measurement in this plan. All dimensions are rotated dimensions: use the projected measurement direction, not a three-dimensional distance between their extension-line origins.

All **eight hatches are non-associative**, including four ANSI31 hatches and four solid fills. They will not automatically follow edited boundary lines. Moving an existing rigid assembly must include its hatch entities; changing a hatch boundary needs explicit updater support.

**2. Component evidence and reference points**

The following handles are verified evidence in this source version, not an instruction to select every object between two handle numbers. Complete component membership still needs binding review.

| Semantic role | Native evidence | Baseline |
|---|---|---|
| Shaft inner faces | Open polyline `628`; dimensions `62B`, `671` | 1450 wide × 1600 deep |
| Exterior walls | Open polyline `468`; returns `626`, `627` | Side walls 195; front/rear 200 |
| Cabin inner outline | Lines `521`, `522`, `525`, `528`, `5F3`; dimensions `63C`, `633` | 950 wide × 1200 deep |
| Cabin outer outline | Lines `523`, `524`, `526`, `529`, `5F4` | 25 offset from the inner outline |
| Door/opening | Dimensions `61B`, `61F`; jamb outlines `60A`, `60F`; related entrance primitives | 800 clear width, centered on shaft |
| Counterweight outline | Closed polyline `51E`, text `46B`, centreline `675` | Nominal 1000 × 100 plan outline |
| Car guide row | Centreline `52A`; opposing faces `5BB`/`5EB` and `55D`/`58E`; dimension `639` | DBG 1050, row 675 behind the inner front datum |
| Counterweight guide row | Centreline `675`; face evidence `473`/`4C8` and overlapping nearby lines; dimension `519` | DBG 1040, row 125 forward of the rear inner face |
| Car rail label | `52F`, leader `673` | `T70/65/9` |
| Counterweight rail label | `67E`, leader `67F` | `T50/50/5` |
| Speed governor | Text `663`, leader `674`, adjacent detail primitives | Separate assembly; dimensions/specification not established |

There must be two distinct parameters, `car_dbg_mm` and `counterweight_dbg_mm`. Searching for the text `DBG` alone is ambiguous.

Use the intersection of the left inner shaft face and the inner front datum as local origin. Derive it from the shaft's topology, checking against dimension anchors. In this file its measured world coordinate is approximately `(659.852974, 998.584294, 0)`. This number is diagnostic evidence and must not become a hard-coded origin in the generator.

Define local +X across the shaft, +Y from the entrance toward the rear, and the shaft centreline at local `x = shaft_width / 2`. All new positions should be calculated in that frame, then transformed to the existing drawing's placement. Use face directions and entrance orientation, not global drawing extents or the bounding box of all annotations. Neither shaft polyline is closed, so a generic closed-rectangle recognizer alone will fail.

**Measured allowances versus design rules**

The baseline has 250 from each shaft side face to the corresponding cabin inner face, 125 from the inner front datum to the cabin inner front, and 275 from the cabin inner rear to the rear shaft face. These spans include equipment space; they are not necessarily the free clearance between moving assemblies.

The expressions `cabin_width = W - 500` and `cabin_depth = D - 400` reproduce this one sample. They are **unvalidated template hypotheses**, not rules established by multiple drawings or a manufacturer specification.

Similarly, the observed car guide row can be described equally well as `front_datum + 675`, `cabin_front + 550`, or `cabin_midpoint - 50`. Those produce different results when depth changes. The single DWG cannot choose among them. The `TZ` dimension is currently 800 between guide rows; its behavior under resizing is also unspecified.

The door is currently centered and 800 wide. Inferring `door_width = cabin_width - 150` would reproduce this sample, but it does not establish which standardized door sizes or entrance assemblies can be selected. Counterweight width likewise needs an equipment/frame rule, not automatic scaling from shaft width alone.

**Existing geometry that must be preserved or explicitly addressed**

- Eight entities have nonzero Z: centreline `527` slopes from Z=-110.4673237 to 0, the upper section-symbol entities `66B`–`670` are at Z=-114, and title `664` is at Z=-137. The drawing is a plan with elevation anomalies, not a complete 3D elevator model. Do not silently flatten it during the initial binding stage.
- Counterweight polyline `51E` is slightly skewed: its right vertices are 0.019303506670 mm lower than the corresponding left vertices. Its nominal 100 mm depth is exact at the vertical sides. Preserve the residual in a baseline replay; geometric normalization would be a distinct change.
- Full-precision inspection confirms duplicate/overlapping rail lines. `473` and `476` are identical. `478` and `4BF` represent another repeated segment, offset from the main row. Do not deduplicate during parametric conversion without identifying their purpose.
- Rail profiles, shoes/brackets and nearby reference geometry span multiple layers. Lines `531`–`536` connect the two car-rail regions, so their left and right endpoints need different attachments. They cannot be moved wholesale with one rail assembly.
- A global scale, layer-wide stretch, automatic purge, or explode/recreate pass would alter equipment details or break relationships. The updater should preserve entity identity wherever possible.

**3. Choice of access method**

| Method | Role in this project | Reason |
|---|---|---|
| **Hosted C# / AutoCAD .NET** | Primary reader and drawing updater | Proven successful on this DWG; typed access, native transactions, object relationships, precise geometry and dimension APIs |
| AutoLISP | Optional selection/tagging commands and inspection helper | Already worked for initial extraction; convenient inside AutoCAD, but less suitable for a growing rules/validation engine |
| Python COM / ObjectDBX | Optional orchestration or reporting | Direct ObjectDBX activation failed here; COM would add another dependency and does not replace a controlled dimension-update strategy |
| DXF conversion | Fallback inspection/interchange | Not required for access; any future round trip should be checked for geometry, dimension, font, metadata and association fidelity |

Target the installed AutoCAD 2023 host using **.NET Framework 4.8**. Autodesk's compatibility table confirms this runtime. Keep CAD references restricted to the reader and updater so a future host upgrade does not require rewriting design rules. [Autodesk compatibility](https://help.autodesk.com/cloudhelp/2023/ENU/AutoCAD-Customization/files/GUID-A6C680F2-DE2E-418A-A182-E4884073338A.htm)

The reader can use a side database, as demonstrated. For production changes, open an explicit working copy as the current document and use transactions plus the appropriate document-locking context. Recompute dimension graphics and verify the result before saving a new output DWG. Autodesk documents dimension-block limitations when dimensions are edited in noncurrent databases, which is a reason to avoid a side-database-only production updater. [Dimension API behavior](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_DatabaseServices_Dimension.html)

**4. The requested five-part project**

| Part | Responsibility | Main output |
|---|---|---|
| **1. Drawing reader** | Inventory native entities; resolve semantic roles; derive local datums; read dimensions and associations; verify template fingerprints | `DrawingSnapshot`, `TemplateBindings`, baseline diagnostics |
| **2. Elevator design rules** | Select the configured equipment family; define cabin allowances, guide-row policy, separate DBG rules, counterweight/door selection, accepted clearance requirements and supported input range | A resolved design specification or explicit unresolved/invalid-rule result |
| **3. Geometry calculator** | Pure calculations in the shaft-local frame; derive component anchors and spans; compute actual equipment-envelope gaps; detect clashes and out-of-range dimensions | `LayoutSolution` plus clearance results and an entity-change plan |
| **4. AutoCAD drawing updater** | Validate all bindings before writing; stretch intended span members; translate rigid assemblies; update dimensions, leaders and affected hatch geometry; save a new DWG | Updated drawing and verification report |
| **5. Configuration/input file** | Store routine shaft inputs, the versioned rule profile, template identity and semantic bindings | `input.json`, `equipment-profile.json`, `template-bindings.json` |

Proposed implementation layout:

```text
ElevatorPlanGenerator/
  src/
    DrawingReader/
    ElevatorDesignRules/
    GeometryCalculator/
    AutoCADDrawingUpdater/
  config/
    input.json
    equipment-profile.json
    template-bindings.json
  templates/
    plan codex.dwg
  output/
```

This is a proposed layout; it has not been scaffolded or used to modify the drawing.

Routine input can remain exactly two values after a complete equipment profile is configured:

```json
{
  "shaft_width_mm": 1450,
  "shaft_depth_mm": 1600
}
```

The proposed interpretation is **clear internal shaft dimensions**. Units, equipment selection, door catalogue, counterweight family, rule revision, source template and output naming belong to configuration rather than hard-coded drawing coordinates.

The calculator should use named quantities such as:

```text
cabin_width = shaft_width - left_cabin_allowance - right_cabin_allowance
cabin_depth = shaft_depth - front_cabin_allowance - rear_cabin_allowance
car_rail_x_left/right = car_reference_x -/+ car_dbg / 2
cw_rail_x_left/right = counterweight_reference_x -/+ counterweight_dbg / 2
door_x_left/right = entrance_center_x -/+ selected_door_width / 2
world_point = shaft_origin + local_x * shaft_x_axis + local_y * shaft_y_axis
```

Allowances, guide-row positions, counterweight dimensions and permitted door widths come from the resolved profile. Required clearances must be represented separately from observed template gaps. The reader provides evidence; the rules layer decides what is required; the calculator measures whether the result satisfies it.

**Semantic bindings without redesign**

Initially bind roles using existing handles plus expected type, layer, topology, dimension direction and related reference geometry. Include the source hash/fingerprint and a binding schema version. If a handle is missing, replaced, or no longer matches its expected role, stop before modifying anything; do not silently choose the nearest line.

After the initial mapping is reviewed, semantic tags such as `SHAFT_INNER`, `CAR_REAR_INNER`, `CAR_RAIL_LEFT`, and `COUNTERWEIGHT_RAIL_RIGHT` can be added to a working template through XData or extension-dictionary Xrecords. Existing objects already carrying AutoCAD metadata must retain it. Entity handles are suitable bootstrap identities within this template; runtime ObjectIds must not be persisted between drawing sessions. [Autodesk object identity](https://help.autodesk.com/view/OARX/2026/ENU/?guid=GUID-8D56532D-2B17-48D1-8C81-B4AD89603A1C)

Named blocks may later wrap verified rigid components using their existing geometry and a meaningful base point. This is optional, not a prerequisite for the first updater. Block conversion/cloning can change identifiers and dimension references, so it should not be mixed into the initial geometry-preservation pass. Keep adjustable wall/cabin members individually bound until their stretch behavior is established.

**5. What the rule profile still needs to define**

The drawing establishes a baseline layout, but not its behavior for other shaft sizes. Width and depth can be the only routine inputs only after the following are fixed in configuration:

1. The supported elevator/equipment family, cabin sizing policy and supported shaft-size range.
2. Whether car guide-row position follows the entrance datum, cabin centre, or another manufacturer reference; how `TZ` changes.
3. Separate car and counterweight DBG rules and whether rail sections remain fixed or require selection from a catalogue.
4. Counterweight frame dimensions/selection and the equipment data governing that selection. A plan outline alone does not establish counterweight mass, frame height, or balancing requirements.
5. Door type, available widths, entrance assembly allowances, placement policy and fixed/configured door height.
6. Required gaps for the chosen components, distinct from the single drawing's measured offsets. Do not encode this drawing's 75 mm gaps as universal required clearances.

A template-reproduction mode can be implemented separately using clearly labelled observed offsets. It must not present those inferred offsets as a validated elevator design profile.

**Implementation gates and verification**

The next implementation stage should complete the component manifest and replay the current 1450 × 1600 input before enabling a changed size. An unchanged-input replay should preserve topology, rail/profile dimensions, hatch geometry, dimension values/text formats, local residuals, and the original drawing's appearance. Rendering/overlay verification remains future work; the present audit did not test an updater.

After rules are defined, meaningful cases are: change width only, change depth only, change both, minimum supported dimensions, a door that cannot fit, and an equipment-envelope collision. Verify both DBG values independently, preserve rigid profile sizes, check entrance placement and label layout, reopen each generated DWG, and ensure results are computed from the baseline instead of accumulating transformation drift across runs. Reject invalid geometry before writing and keep the original source template unchanged.

Inspection completed: native access is proven, the drawing's current organization is documented, and the existing geometry can support the proposed generator once semantic bindings and the equipment rule profile are completed.
