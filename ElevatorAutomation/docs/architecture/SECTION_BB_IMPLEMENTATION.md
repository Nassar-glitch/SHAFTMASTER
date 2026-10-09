# Coordinated Machine Room Section B-B

This release adds the third native drawing view to the existing Plan and Machine Room project. It implements the engineer-confirmed adjustable hanger, exact external-tangent reeving geometry and bounded concrete-support height. It does not approve the elevator, structure, hanger or unresolved clearances for manufacture.

## 1. Shared Core classes

- `MachineRoomSectionParameters`, `SectionReference`, `VerticalArrangementInput/Result`, `ReevingSolution` and `MachineBeamSelection` carry source references, calculated geometry and separate statuses.
- `ReevingSolver` calculates the reviewed B-B rope branch and solves vertical separation.
- `MachineRoomSectionRules` coordinates the shared Plan/Machine Room datums, deflector selection, machine projection, support rows and engineering records.
- `MachineRoomSectionGeometry` maps source-local geometry to the calculated references without cumulative scaling.
- `IMachineBeamSelector` / `PreliminaryMachineBeamSelector` isolate the preliminary selection table from future structural calculations.
- `SuspensionProjectRules` validates the allowed rope data.

New optional project fields are `NumberOfSuspensionRopes` and `MinimumDeflectorSlabClearanceMm`. Existing `Capacity` and `SuspensionRopeDiameterMm` are reused. Capacity is required for this section's preliminary beam selection. Rope diameter can remain unknown. Count is 4, 5, 6 or 7; the creation/edit prompt explicitly offers 6. Diameter choices are 6.5, 8, 10, 11 and 12 mm. No rope diameter or minimum slab clearance is silently supplied.

## 2. AutoCAD implementation

`MachineRoomSectionUpdater : IViewUpdater`, `MachineRoomSectionDrawing`, `MachineRoomSectionGeometryAudit` and `MachineRoomSectionCommand` add placement, update, persistence and independent inspection. `UPDATEALL` registers Plan, Machine Room and Section B-B when their manifests are present. All receive one shared calculated design. No other view is created.

Commands added: `ELEVATORMRSECTION`, `ELEVATORREEVINGDATA`, `ELEVATORMRSECTIONAUDIT`. Existing Plan and Machine Room geometry transformations remain in their existing updaters.

## 3. Semantic bindings and storage

The reviewed source has 705 model entities. All are tagged; a nonplotting reference-axis line, two calculated rope-contact arcs and an engineering annotation bring the installed section to **709** entities. Existing circles, machine details, hanger, beam, stand/pad details, concrete, slab, rope lines, openings, annotations and ten dimensions are reused.

Each entity has an XData semantic key under the existing application registration, prefixed `MachineRoomSection.`. The DWG Named Objects Dictionary `ELEVATOR_AUTOMATION` stores chunked JSON Xrecords:

- `Project`: shared input;
- `MachineRoomSectionBindings`: schema, semantic identifiers, roles, immutable source-local points and reference measurements;
- `MachineRoomSectionDesign`: calculated section result;
- `MachineRoomSectionGeometryAudit`: native geometry checks and unresolved observations.

The existing `MachineRoomReeving`, `MachineRoomValidation` and `MachineRoomEngineeringRules` records receive the coordinated calculations/statuses. A sidecar JSON is also saved for inspection, but is not needed to reopen/update the DWG.

The section frame is the front shaft/slab intersection and a tagged drawing-plane axis. Placement is a one-time user choice. Subsequent calculations do not use fixed world coordinates or entity handles. The one-time seed uses the reviewed source hash plus local geometry fingerprints; runtime lookup uses XData. Missing, duplicate, orphan or changed-type bindings reject updates. Before the first section update, obsolete source DIMASSOC links on section dimensions are removed with the native DIMDISASSOCIATE command; their endpoints are subsequently owned by the semantic recipes. Plan/Machine Room associations are not selected. This migration prevents source osnap reactors overwriting calculated endpoints after a command finishes. Geometry/topology changes require a reviewed manifest update; retaining an identifier does not authorize an arbitrary replacement machine.

## 4. Exact reeving method

For traction center T, deflector center D, radii R and r, distance L and unit vector u = (D-T)/L:

`c = (R-r)/L`

`n = c*u +/- sqrt(1-c*c)*perpendicular(u)`

`traction tangent = T + R*n`

`deflector tangent = D + r*n`

The branch consistent with the source's upward right-side car entry and counterclockwise traction wrap is selected. The radius/tangent dot product is zero. Actual wrap is the radial sweep from the right-hand entry to the traction tangent. Entry/exit directions and angles, horizontal/vertical center offsets and center distance are calculated. Intersecting/touching pitch circles are rejected.

For horizontal center separation H and target beta = 152 degrees, theta = 180 degrees - beta:

`V_target = (H*cos(theta) - (R-r))/sin(theta)`

The vertical solution also keeps the native hanger attachment order and prevents the deflector pitch circle penetrating the underside elevation band of the upper steel member. These are zero-penetration geometric constraints, not approved mechanical minimum clearances. They can increase V and produce wrap above 152 degrees. The chosen circle diameter is never changed to improve wrap.

## 5. Reference and generated wrap

The supplied source's actual circles calculate to **160.102635776296 degrees**. Its existing angular dimension is approximately 160.2169 degrees; its vertical dimension also differs slightly from the actual circle-center separation. The updater uses calculated circles/tangents, not either drafting discrepancy.

The coordinated SW1450 / SD1600 baseline has TZ750 and a 250 mm deflector. Its geometry calculates to approximately **159.783985 degrees**, V600.516287 mm, concrete350 mm and hanger237.546923 mm. This differs from the original source because the source's horizontal dimension chain is approximately1564.995 mm, whereas the project uses the validated shared1600 mm shaft.

## 6. Adjustable hanger and concrete solution

Let A be the measured lower stand/isolation stack, selected upper-steel depth and retained machine-center offset above that steel. Let C be a supplied minimum slab clearance. After solving V:

`required concrete = r + C + V - A`

`concrete = max(350, required concrete)`

`traction Z = concrete + A`

`deflector Z = traction Z - V`

`hanger length = V - machineOffsetAboveSteel - topAttachmentOffset - lowerAttachmentOffset`

`actual slab clearance = deflector Z - r`

If C is absent, zero is used only to avoid pitch-circle penetration of the slab. Actual clearance is reported and `DeflectorSlabClearanceStatus = NOT YET EVALUABLE`, including at zero clearance. This is not permission to install a sheave at the slab. Manufacturer outer diameter, guards, tolerances and an approved minimum remain necessary.

A required concrete height above1000 mm returns `REEVING REDESIGN REQUIRED` before any view geometry is committed. There is no rounding increment. Raising supports moves the machine/steel/stand assembly and deflector together while preserving the already-solved center separation; the hanger length remains determined by that separation. The deflector center is not held at a fixed slab elevation. Hanger strength and allowable adjustment remain `MECHANICAL REVIEW REQUIRED`.

The result exposes `RequiredVerticalCenterSeparationMm`, `RequiredDeflectorCenterZ`, `RequiredTractionSheaveCenterZ`, `RequiredHangerLengthMm`, `DeflectorToSlabClearanceMm`, `ConcreteSupportHeightMm` and `CalculatedWrapAngleDeg`, plus the chassis elevation change from the source reference.

## 7. Preliminary UPN behavior

| Capacity / geometric span | Selection | Status |
|---|---|---|
| Capacity <480 kg, span <2200 mm | UPN140 | PRELIMINARY |
| Capacity <480 kg, span >=2200 mm | UPN160 | CALCULATION REQUIRED |
| 480 through800 kg | UPN160 | PRELIMINARY |
| Above800 through1500 kg | UPN200 | PRELIMINARY |
| Above1500 kg | UPN240 | PRELIMINARY |

The section projects the mean centers of paired front support plates and paired rear support plates from the Machine Room. Each native plate's individual source offset remains intact and is audited. The resulting span is a geometric projection, not an approved structural effective span. Structural design must establish actual load paths, effective span, reactions and bearing.

Only the upper longitudinal member's nominal depth changes. The separate approximately140 mm lower stands and isolation details remain native. The source inner-flange lines are retained for UPN140 and suppressed for other selections, since no approved section contour table has been supplied. This is a nominal envelope, not a fabricated detailed UPN profile. Bending, shear, deflection, utilization, concrete reinforcement and bearing capacity are not calculated.

## 8. Cross-view and native checks

The audit checks shared car/CW reference rows and TZ; selected deflector diameter/presence; actual P1–P4 plate positions and projected section support rows; traction/deflector centers/radii; hardware dependency points; actual tangent endpoints, radii and orthogonality; drawn wrap arcs; vertical rope drops and opening references; actual structural hatch loops/positions/areas; dimensions and overrides; and the engineering annotation anchor. It runs before commit, after regeneration and on reopened test drawings.

The audit reports actual pitch-circle/slab distance, pitch-circle/upper-beam distance and rope-centerline separation. A common tangent crosses the upper steel-beam projection in tested sections. The necessary out-of-plane/opening relationship is unresolved and is explicitly reported. A scoped geometry PASS does not resolve that intersection or establish general collision freedom. Complete rope bundles, groove spacing, guards and manufacturer outer envelopes are not available.

## 9. Independent engineering statuses

Layout, wrap, diameter ratio, minimum rope diameter, slab clearance, hanger mechanics, steel selection, concrete strength and full traction verification remain independent. For example, a250 mm deflector with8 mm rope can have LayoutPASS and WrapPASS while D/d remainsFAIL. Changing vertical geometry never changes this ratio. A6.5 mm rope also has its separate minimum-diameter failure, even in no-deflector layouts.

152 degrees and the UPN table are project rules. They are not presented as EN minima. Existing clause traceability is retained in `MACHINE_ROOM_CODE_REQUIREMENTS.md`. Full EN81-50 traction/safety-factor verification still needs loads, masses, friction/grooves, rope construction/strength and safety factors; wrap alone does not prove traction compliance.

TZ<520 suppresses the deflector/hanger and all uncalculated rope geometry, with `DIRECT ROPE ROUTING REVIEW`: the retained520 mm traction circle cannot have two opposite vertical tangent drops closer than520 mm. AboveTZ1100, the last allowed250/400 deflector state is retained and uncalculated rope paths are suppressed with reeving/traction review. Neither case receives a fabricated wrap PASS.

## 10. Verification

Pure tests:43 sizing assertions,6 annotation checks,39 Machine Room checks and102 reeving/beam assertions. The latter cover all capacity/span boundaries, allowed rope/count combinations, tangent mathematics, wrap below/at/above152, concrete350/1000 limits, supplied versus missing slab clearance, preserved separation when supports rise, infeasible circles and independent rope-minimum checks.

Native regression reports are in `tests/*-results.json`. Existing suites contain17 CAD,14 architecture (including seven original golden comparisons),1 command-workflow,7 plan-completeness and32 Machine Room records. The section suite contains32 records: preparation,16 three-view saved/reopened cases,12 deliberately displaced component/annotation faults, missing/duplicate identifiers and an over1000 rejection/rollback check. A separate AutoCAD process then opens the saved rotated three-view DWG and updates it large-to-small, producing two more records.

Section cases include TZ519.9/520/800/800.1/1100/1100.1,250/400/absent/review states, all four UPN depths, all allowed rope diameters and counts, repeated resizing, rotation/translation, persistence, duplicate prevention and failure handling. JSON records, rather than the Core Console exit code alone, establish pass/fail. All 105 native records and 190 pure assertions pass. The seven existing golden Plan comparisons remain unchanged. All four protected source DWG hashes match their pre-change values. See the final run summary in `tests/section/verification-summary.json`.

## 11. Comparison evidence

`tests/section/comparison.png` compares baseline, TZ1100, UPN240 and no-deflector review cases. Full-size `generated-1.png`, `generated-10.png`, `generated-14.png` and `generated-6.png` show the machine, steel, stands/pads, concrete, slab/openings, hanger, sheaves, calculated rope and major dimensions. These are diagnostics from saved/reopened native curves and dimension values, not AutoCAD plot-style proofs; the tiny nested hub detail and nonstructural hatch pattern rendering are not fully reproduced by the diagnostic exporter.

The material comparison increases V from about600.516 to1075.862 mm, hanger from237.547 to712.893 mm and concrete from350 to598.770 mm. At the latter case the calculated pitch-circle/slab clearance is zero, explicitly NOT YET EVALUABLE. Each `case-N.json` identifies the actual separate output DWG and complete calculated result/audit.

## 12. Remaining engineering inputs

An approved minimum deflector/slab clearance; manufacturer outer envelopes/guards and hanger attachment/adjustment limits; beam contour and structural data; concrete/load/bearing design; effective structural span; rope bundle/groove spacing and opening/ferrule sizing; out-of-plane rope/beam relationship; direct routing belowTZ520 and reeving aboveTZ1100; and the full traction/code inputs remain unresolved. Original equipment is retained rather than silently replaced or mechanically certified.

## 13–14. Exact AutoCAD workflow and build

Restart AutoCAD2023 to release previously loaded DLLs. NETLOAD:

`D:\CAD DATA---------------------\DWGs\2026\dwg plan codex\ElevatorAutomation\bin-section\Elevator.AutoCAD.dll`

Keep adjacent `Elevator.Core.dll` and the project `config`/`Templates` folders in place.

1. Open a project drawing based on the validated Plan master. Run `ELEVATORPROJECT` to create/edit SW, SD and family data.
2. Run `ELEVATORMACHINEROOM` once if not already installed.
3. Run `ELEVATORMRSECTION` once. Enter capacity, rope data and any specified minimum slab clearance; press Enter for unknown clearance. Select the front-shaft/slab placement datum in an empty area.
4. Run `UPDATEALL` to update all three installed views and save a separate DWG plus JSON in `ElevatorAutomation/output`.
5. Use `ELEVATORPROJECT` or `ELEVATORREEVINGDATA` when inputs change, then `UPDATEALL`. No repeated placement or derived-dimension entry is needed.
6. Run `ELEVATORMRAUDIT` and `ELEVATORMRSECTIONAUDIT` for read-only native geometry checks. Review engineering statuses before engineering release.

`ELEVATORUPDATE` remains the original Plan-only command; use `UPDATEALL` for coordination. The original source DWGs and earlier locked `bin`/`bin-chassis` releases are preserved.

Build: `./ElevatorAutomation/build.ps1 -BinDirectory bin-section`.

Complete regression runner: `./ElevatorAutomation/tests/run-tests.ps1 -BinDirectory bin-section`.

Autodesk documents the selected-dimension association removal used by this migration: [Dimension associativity](https://help.autodesk.com/cloudhelp/2022/ENU/AutoCAD-Core/files/GUID-04334508-47C9-43E3-875C-DD5FEC9F9930.htm).


Verified example copies: 
- `output/SectionBB_Baseline_Verified.dwg` (SW1450 / SD1600).
- `output/SectionBB_TZ1100_ReviewClearance.dwg` (SW1450 / SD2300; zero calculated pitch-circle/slab clearance is explicitly NOT YET EVALUABLE).

Each is a byte-for-byte copy of its audited, saved/reopened case DWG. Adjacent JSON retains the original test output path for traceability.

