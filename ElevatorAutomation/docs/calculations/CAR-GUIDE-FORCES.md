# FF1 / FF2 candidate

The Calculations page now has **Car guide forces — FF1 / FF2** in both Basic and Advanced modes. Supply only **vertical upper-to-lower car guide-shoe spacing h, in mm**, FF1 / FF2 are calculated live (see `PRESETS.md`, "Live calculation"). Capacity comes from General; internal cabin width/depth come from the same plan recipe used by native drawings and STEP geometry. They are displayed read-only.

The new canonical value is `ElevatorProject.Calculations.CarGuideForces.GuideShoeSpacingMm`. A new project takes h = 2800 mm from `config/presets.json` (see `PRESETS.md`); h follows the preset until the user edits it. Older projects keep their value, or no value, until "Fill blanks from presets". The optional group preserves schema version 1 and the existing Basic/Advanced legacy-mode resolution. It is excluded from chassis and safety/pit fingerprints; changing h does not invalidate those independent results.

## Adopted source rule

Source: `Elevator_Calculations_GEARBOX_Code_Corrected_Dual_Units.docx`, Word `word/document.xml`, zero-based descendant paragraphs 77–87, “Third” / irregular load distribution inside the car. The input table is at paragraphs 22–30. SHA-256: `ECFD591133F5F97ED923F62A3A3EEA60C2B97A67805E60BCB7131C3DA7FB13DF`.

| Force | Base force (N) | Design force (N) |
|---|---|---|
| FF1 | Q (kg) × 10 N/kg × internal width / (4h) | FF1 base × 2 |
| FF2 | Q (kg) × 10 N/kg × internal depth / (8h) | FF2 base × 2 |

All lengths must use the same units. The live project uses mm. The scalar verification also checks equivalent metre inputs. The adopted **10 N/kg** coefficient is kept distinct from physical g = 9.81 m/s²; the design factor 2 is applied once.

The note explicitly says “Vertical Guide Shoe Distance.” The h endpoints are now defined in the user-approved engineering interpretation below. The application has no verified shoe-spacing geometry. Car internal or overall height is not substituted for h, nor is guide-rail spacing or a bounding-box measurement.

The source calls FF1 along the car guide-rail axis and FF2 perpendicular. The axis mapping and the per-guide-rail meaning are now defined in the user-approved engineering interpretation below. **Implementation note:** the application still reports scalar FF1/FF2 values only. It assigns no spatial vector, divides by no guide count, and sums nothing for the group. Since the 2026-10-07 audit, the results panel and result details display the approved interpretation below (`CarGuideForceCalculationEngine.ApprovedInterpretation`). The equations, coefficient, factor and `RuleVersion` are unchanged. These are adopted note calculations, not independently certified standard/manufacturer requirements or guide strength/connection verification.

## Engineering interpretation (user-approved 2026-10-07)

The user, acting as responsible design engineer, approved this interpretation on 2026-10-07. It is a design interpretation recorded for this project, not standards certification or manufacturer approval. The application code is unchanged by it.

**Basis.** EN 81-50:2014 Annex C.2.1 (informative), for n guide rails:

- Fx = k1·gn·(Q·xQ + P·xP) / (n·h)
- Fy = k1·gn·(Q·yQ + P·yP) / ((n/2)·h)

EN axes: X = car depth Dx, perpendicular to the rail line. Y = car width Dy, along the line joining the car rails. These are EN axes, not project drawing coordinates; the project mapping is given below.

The note's formulas are this form with:

- n = 2.
- P at the guide-rail cross (xP = yP = 0).
- Load eccentricity W/4 and D/4. EN 81-20 5.7.2.3.4 uses 3/4-area loading, i.e. Dim/8.
- 10 N/kg instead of gn.
- Design factor 2 = k1 for progressive safety gear.

**Fixture comparison** (Q 480 kg, W 1.1 m, D 2.1 m, h 1.2 m, n 2, k1 2):

| | EN 81-50 C.2.1 (Dim/8, gn) | Note design value |
|---|---|---|
| Along the rail line (Fy ↔ FF1) | 1079.1 N | 2200 N |
| Perpendicular to the rail line (Fx ↔ FF2) | 1030.05 N | 2100 N |

The note gives about 2× the EN values, which is conservative. The project adopts the note values.

**Axis mapping.**

- **FF1 = Fy.** It acts along the car-rail line, which is the project car-width direction. The project's car rails are on the left/right walls: `railLeft`/`railRight` = Center ∓ CarDbg/2, with `CarDbg = SW − 2·CarRailSideAllowanceMm` (`ElevatorParameters.cs`, `SizingRules.cs`). FF1 is applied to the rail blade tip and bends the rail about its x-x axis (Wx).
- **FF2 = Fx.** It acts perpendicular to the rail line (car-depth direction), on the blade side, and bends the rail about its y-y axis (Wy, the weaker axis).
- The note's diagram labels FY on the blade tip and FX on the side, consistent with EN 81-50 Figure 4.

**Meaning.**

- Each value is the horizontal force **per guide rail at one guide-shoe level**. It is equal and opposite at the upper and lower shoes.
- It must not be divided by the number of rails or shoes, and must not be summed for the group.
- For rail bending it is applied at mid-span between brackets (EN 81-50 5.10.2.1: M = 3·F·l/16).

**Validity.**

- Two car guide rails, with a centrally guided/suspended car and the car mass centre at the guide-rail cross.
- Rucksack/eccentric suspension, or n ≠ 2, is out of scope for these formulas.

**h.**

- h is the vertical centre-to-centre distance between the upper and lower car guide shoes. For sliding shoes it is measured at the mid-length of the guiding insert; for roller guides, at the roller axle centre.
- It is a project input. New projects take the 2800 mm design preset (`PRESETS.md`), because the application has no car-frame / shoe geometry to derive it from.
- A smaller h gives larger forces.

**Not covered:**

- Normal running (k2 = 1.2).
- Loading on the car sill (Fs).
- The guide-rail stress/deflection/buckling check itself.

## Geometry and validity

`ElevatorDesignEngine.CalculateCarGuideForceGeometry` calls shared `DesignRules.Calculate`; it does not introduce another sizing equation. `Plan.CarWidth` / `CarDepth` correspond to semantic `Plan.Cabin.Inner.Left/Right/Front/Rear` faces (`DrawingReader.cs`, `SizingRules.cs`). STEP side/rear panels are outside those internal boundaries.

The geometry object captures SW/SD, registered template/system/rule selection, equipment profile and counterweight depth **before** calculation. Only the shared engine can issue it. Retained geometry after a relevant input/context change returns stale/invalid with no forces. Geometry is regenerated by the UI before FF calculation. Capacity and h additionally bind result currency. FF geometry intentionally needs no chassis-bearing, CG, floor, rope or safety/pit calculation inputs.

Missing h/capacity produces `MISSING_INPUT`; nonpositive/nonfinite values produce `INVALID_INPUT`, with unavailable forces. Malformed h text clears its canonical numeric value but keeps a separate draft/error; calculation and rendering apply that error after navigation and mode changes. It cannot become an intentionally missing input. Correcting h removes the error. Relevant edits hide previous numbers. Unrelated mass, CG, chassis and safety/pit errors remain independent. Missing/invalid FF inputs do not block P1–P7 or drawing generation. Saving malformed h text requires correcting it or deliberately blanking it.

## Verification and packaging

Independent source fixture (never project defaults): Q480 kg, internal width1100 mm, depth2100 mm, h1200 mm → FF1 base1100 N/design2200 N; FF2 base1050 N/design2100 N. A separate live UI fixture uses SW1500/SD1800 and therefore the actual shared internal cabin1100×1400 mm → FF1 1100/2200 N and FF2 700/1400 N.

Test sources: `tests/Elevator.Core.Tests/Suites/CarGuideForceTests.cs`, `CarGuideForceUiTests.cs`, `verify_car_guide_force_note.py`; runners: `tools/test.ps1 -Suite core-net48` (the Core assertions are the xUnit class `tests/Elevator.Core.Tests/Suites/CarGuideForceTests.cs` since P7), `run-ff-ui.ps1`. Evidence is under `output/ff-ui-20261006/`; affected engine regression evidence is under `output/ff-source-review-20261006/`.

Candidate: `bin-shaftmaster-ff-candidate/ShaftMaster.exe`, built using `build-solution.ps1 -BinDirectory bin-shaftmaster-ff-candidate`. Existing runtime dependencies and STEP packaging are unchanged. The release and previous safety/pit candidate remain separate.

WPF checks drive real rendered controls through their normal textbox/navigation/selection/button handlers and capture screenshots. They are programmatic UI checks, not interactive mouse/keyboard testing. Native drawing checks use AutoCAD Core Console and reopen in a fresh process. STEP input/regression checks do not by themselves constitute a new end-to-end SOLIDWORKS STEP export.

## Native/STEP integration

Run 2026-10-07 against the unchanged candidate, using `tests/solution/run-ff-integration.ps1` (source `CarGuideForceIntegrationTests.cs`) with default parameters: **PASS, 22 assertions**. Evidence: `output/ff-ui-20261006/integration/`.

- STEP: packaged `StepInputFactory.Create` parameters are compared for an absent FF group and h = null, 1200, 0, −1, NaN, +∞. The shared design stays available, and the parameters are identical in every case. `step-parameters-without-ff.json` and `step-parameters-with-ff.json` are byte-identical. **No SOLIDWORKS STEP export was run.**
- Native: two synthetic four-view DWGs (`StoredFFInput`, h = 1200, and `MissingOrMalformedDraftCanonicalH`, h = null) were generated with AutoCAD 2023 Core Console, then reopened and audited in a fresh process. The verification input hash matches the request, and h and Basic mode are read back unchanged from each DWG. The generated and reopened entity counts match (Plan 521, MachineRoomPlan 1354, SectionBB 709, SectionAA 3045). Job logs are copied under `*-job-evidence/`.
- The malformed-draft case is represented by its canonical null value. Malformed text handling remains covered by the rendered WPF tests above.
- This run verified software behaviour only. It did not decide the h endpoints, the FF1/FF2 axis mapping, or the per-guide/per-shoe/group meaning. Those were later settled by the user-approved engineering interpretation above.

## Audit and UI interpretation correction (2026-10-07)

- **Audit findings:**
  - The engine equations match note paragraphs 79–80 and 84–86: 10 N/kg, internal width/(4h) and internal depth/(8h), design ×2 applied once.
  - There is no rail/shoe division, no group sum, and no other factor.
  - The note fixture gives FF1 1100/2200 N and FF2 1050/2100 N.
- **Defect found:**
  - The rendered results and `Definitions` still said the axis and per-guide/per-shoe meanings were unresolved, contradicting the user-approved interpretation above.
  - Only that text was corrected, in `CarGuideForceCalculationEngine.cs` (new `ApprovedInterpretation`, added to result details, and the h definition) and `CarGuideForcePage.cs` (results note).
  - No equation, input, persistence or schema changed.
- **Regression checks added:**
  - Core, in `CarGuideForceTests.cs`: interpretation reported, stale wording absent, rule version/coefficient/factor unchanged, and design = 2 × base for the live result.
  - Rendered WPF, in `CarGuideForceUiTests.cs`: interpretation shown, stale wording absent.
- **Candidate rebuilt** in place with `run-car-guide-force-core.ps1` (`build-solution.ps1 -BinDirectory bin-shaftmaster-ff-candidate`).
- **Results** (evidence in `output/ff-audit-20261007/`):
  - Core: PASS, 131 assertions.
  - Rendered WPF: PASS, 126 assertions, with screenshots.
  - Native/STEP integration: PASS, 22 assertions. The STEP parameters are byte-identical to the earlier run, and no SOLIDWORKS export was run.
  - Build-time STEP input tests and design-rule tests: PASS.
