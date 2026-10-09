# Simplified load distribution candidate — 2026-10-06

Candidate: `bin-shaftmaster-basic-candidate/ShaftMaster.exe`. Keep the complete candidate folder together with its dependencies. The release `bin-shaftmaster-v01` and earlier detailed-calculation candidate were not replaced.

## Workflow and authoritative inputs

Open or create a project, enter the existing shaft dimensions and capacity on their usual pages, then open **Calculations**. New projects use **Simplified load distribution**. Enter empty car, counterweight and machine masses in kg, P1–P4 are calculated live (see `PRESETS.md`, "Live calculation"); the lever arms are shown as a small table in mm. In a new project the masses start from the presets (P and machine mass from the capacity table, CW = P + 0.5·Q; see `PRESETS.md`) and follow Q until edited. Older projects keep their masses, or blanks, until "Fill blanks from presets". Capacity, suspension ratio, SW/SD, machine/template identity and other drawing inputs retain their existing authoritative locations.

The optional section reuses the canonical effective car-side and CW-side additional suspended masses; compensation may be included in those contributions when appropriate. Blank means not included; an entered zero remains a deliberate zero. Chassis and hanger assembly masses are included only when their respective include decisions are selected. No automatic self-weight is invented from incomplete section data, and Basic does not require assembly CGs.

All calculation inputs remain under `ElevatorProject.Calculations`. `Mode` is the only new project field. Existing mass fields are shared by both modes. Projects without a mode and with any existing calculation data remain Advanced; empty legacy calculation groups resolve to Basic. Opening the calculation page persists that resolved choice. Switching mode retains inputs and invalidates results. Results are in memory, not saved as authoritative project inputs.

Advanced remains available and retains its separate detailed engine, member selections, CG/load offsets, sources and validations. Basic does not validate those unused fields. Malformed Advanced editor drafts do not block Basic calculation or Basic saving. Older drawing workflows still permit missing calculation data.

## Geometry and declared sharing model

`ElevatorDesignEngine.CalculateBasicChassisGeometry` obtains the same native contact geometry and equipment datum through the shared Plan/MR/B-B rules. The Basic geometry recipe removes unrelated floor, rope-component metadata and Advanced rope-path requirements; it does not change drawing geometry. Context-aware provenance seals the generating inputs, context and geometry. Unsupported traction diameter and retained stale geometry remain rejected independently by the calculation API.

P1 = right/rear, P2 = right/front, P3 = left/rear, P4 = left/front. Reference positions are centroids of the identified contact-region polygons, not arbitrary equipment contour centres. The load point is the native traction-sheave centre, explicitly an **assumed machine load reference, not measured machine CG**. No user-entered lever arms or note-example distances are used.

For the supported static 1:1 case:

`M = capacity + empty car + counterweight + machine + included effective additions + included assembly masses`

`W = M × 9.81 N/kg`

One downward load W is applied at `(xL,yL)`. No detailed rope/sheave loads are added again. The user-accepted two-stage sharing rule is:

```
                 +Y rear
       P3 ── rear share R ── P1
                  ↑
           W at (xL,yL)
                  ↓
       P4 ─ front share F ── P2
        left      entrance    right +X
```

Let `yf = yP4 = yP2`, `yr = yP3 = yP1`, `L = yr-yf`:

`F = W(yr-yL)/L`, `R = W(yL-yf)/L`

`P4 = F(xP2-xL)/(xP2-xP4)`, `P2 = F(xL-xP4)/(xP2-xP4)`

`P3 = R(xP1-xL)/(xP1-xP3)`, `P1 = R(xL-xP3)/(xP1-xP3)`

Each row uses its own actual transverse span. Required geometry includes four unique oriented contact regions, coplanar bearings, aligned front and rear rows, positive spans and a finite native load point. Incompatible rows are rejected rather than averaged. Loads outside the support footprint retain signed negative reactions and report uplift review.

This additional sharing rule makes the four reactions determinate. It does not calculate stiffness-dependent chassis distribution or verify stress, deflection, welds, bolts or anchors. Concentrating all included masses at one datum is the declared Basic approximation; Advanced retains separate load locations/actions.

## Presentation and invalidation

Basic displays three mass fields, optional additions, Calculate, signed P1–P4 reactions, physical locations, total mass/load, derived longitudinal/transverse distances and a labelled geometry preview. Composition, assumptions, model identifiers, technical validation identifiers and separate ideal static rope tensions are expandable. Rope count is needed only for per-rope values; missing or invalid unrelated rope metadata does not block Basic P1–P4 results. Suspended-side bundle tensions are informational and exclude machine/chassis/hanger contributions.

Changing geometry-driving inputs/context invalidates results and regenerates source-bound geometry. Changing relevant masses recalculates using current geometry. Changing mode invalidates results. Missing/invalid/stale results show no old reaction numbers. `CALCULATED_UNDER_ASSUMPTIONS` and `UPLIFT_REVIEW_REQUIRED` remain distinct from complete structural verification.

## Verification and evidence

- Basic engine/canonical workflow: **49 assertions passed**. Independent hand examples, symmetric/eccentric loads, unequal transverse spans, force/moment equilibrium, uplift, incompatible rows, contact-centre validation, units, unsupported machine, Basic without Advanced inputs or rope count, optional mass accounting, geometry regeneration, save/reopen and restored Advanced reactions.
- Basic rendered desktop workflow: **18 assertions passed**. Compact controls, missing masses, invalid unused Advanced draft, optional mass without CG, automatic updated lever arms, stale-number removal, mode switching, legacy detection, save/reopen, resized scrolling and actual native four-view generate/reopen.
- Existing Advanced workflow: **25 automated assertions** and **25 rendered WPF assertions passed**, including source-bound invalidation, sources/CG persistence, uplift, missing inputs, narrow-window controls and native drawing generation with incomplete/populated calculations.
- Existing Core/project/calculation regressions: **214 design/project checks** and **200 detailed calculation checks passed**. Four-view release-baseline results matched exactly at SW/SD 1450/1600, 1650/1800 and 1850/2000 mm.
- Application regressions: **16 passed**, including floor edits, Unicode roundtrip, all four views, individual view selection, repeated placement and preservation of existing output after failed generation.
- STEP regression/package checks: **7 passed**, including current dimensions, floor pose, independent code warning, unsupported machine, dependency error and output replacement consent. STEP geometry/export code was not changed. A new STEP export/reimport was not performed in this change.
- Protected drawing/template/release checks: **32 files unchanged**; both the release EXE and previous calculation candidate retain their prior SHA-256 hashes.

Rendered tests run real WPF windows with programmatic control events and capture their actual rendered contents. They are not an interactive mouse/keyboard Computer Use pass. Native DWG generation/reopening uses AutoCAD Core Console. Synthetic test masses are labelled and confined to test artifacts, never real-project defaults.

Final Basic screenshots and test artifacts: `output/basic-calculation-ui-20261006/review-ui/`. Advanced evidence: `output/basic-calculation-ui-20261006/advanced-ui/`. Existing application evidence: `output/solution-v01/test-20261006-160349/`. Calculation baseline/protected-file evidence: `output/calculation-phase1/`.

No Word report, P5–P7 calculation, drawing-load annotation, template change or release rebuild is included.

## Main code locations

- `Elevator.Core/Calculations/SimplifiedCalculationEngine.cs`: independent Basic solver, mode resolution, accounting, separate tensions and currency.
- `Elevator.Core/Project/ElevatorDesignEngine.cs`: shared source-bound Basic geometry recipe and result dispatch.
- `Elevator.Core/Calculations/ChassisGeometry.cs` / `ChassisGeometryProvenance.cs`: native contact/datum extraction and retained-geometry validation.
- `Elevator.Solution.Desktop/BasicCalculationsPage.cs`: compact form, mode selection, inclusion decisions, distances and Basic result rendering.
- `Elevator.Solution.Desktop/CalculationWorkspace.cs`: context-aware geometry cache and mode-specific calculation dispatch.
- `Elevator.Solution.Desktop/CalculationsPage.cs`: preserved Advanced workflow and shared labelled preview.
- `tests/solution/SimplifiedTests.cs`, `BasicUiTests.cs`, `run-basic-ui.ps1`: reproducible Basic checks.
