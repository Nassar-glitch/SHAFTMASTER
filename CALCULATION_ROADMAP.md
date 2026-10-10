# Calculation roadmap

## Overall objective

Implement and verify the remaining applicable calculations in `Elevator_Calculations_GEARBOX_Code_Corrected_Dual_Units.docx` in controlled stages. FF1/FF2 is an intermediate milestone, not the end of the project.

## How to choose the next stage

1. Read each candidate section directly from the source note. Record equations, definitions, load cases, units, and assumptions.
2. Check the current calculation engines, UI, persistence, and project documents to determine what is already implemented.
3. Inventory note sections as **Implemented**, **Pending**, **Ambiguous**, or **Out of scope**, with source/code evidence and dependencies.
4. Select the next pending item only after confirming its meaning and inputs. Do not guess unresolved physical interpretations.
5. After each implementation, update this roadmap and `SESSION_HANDOFF.md` with actual status, evidence, and next work.

## Source

- `Elevator_Calculations_GEARBOX_Code_Corrected_Dual_Units.docx`
- Existing FF1/FF2 record: `ElevatorAutomation/docs/calculations/CAR-GUIDE-FORCES.md`

## Current milestone and next step

- **FF1/FF2:** Implemented in `ElevatorAutomation/bin-shaftmaster-ff-candidate/`; the latest shared session note reports core/UI/regression checks and native/STEP integration checks passed. The release target remains undecided. Verify current files before relying on the report.
- **FF questions: resolved by user-approved interpretation (2026-10-07).** This is a design interpretation, not certification. The basis is EN 81-50:2014 Annex C.2.1 (informative). The full record is in `ElevatorAutomation/docs/calculations/CAR-GUIDE-FORCES.md` § "Engineering interpretation":
  - FF1 = Fy, along the car-rail line (project car-width direction). It bends the rail about x-x (Wx).
  - FF2 = Fx, perpendicular to the rail line (car-depth direction). It bends the rail about y-y (Wy).
  - Each value is the force per guide rail at one shoe level. It is not divided by rails/shoes and not summed.
  - h is the centre-to-centre distance between the upper and lower car guide shoes, a manual input with no default.
  - Valid only for 2 rails with a centrally guided car.
  - The application code still reports scalar values only.
- **Next candidate stage (Pending, not implemented):** a car guide-rail check per EN 81-50 5.10, covering bending/combined/buckling/flange/deflection. It would use FF1/FF2 and P5 as Fv. See the table below for dependencies.
- **Next roadmap work:** Inventory the remaining note sections and map them to existing calculations. The complete note inventory has not yet been made; do not infer which calculation comes next from chat memory alone.

| Note section / calculation | Status | Evidence | Open questions or dependencies |
|---|---|---|---|
| FF1 / FF2 car guide forces | Implemented in separate candidate; native/STEP integration PASS; release decision pending | `ElevatorAutomation/docs/calculations/CAR-GUIDE-FORCES.md`, current `SESSION_HANDOFF.md` | Interpretation questions resolved by user-approved interpretation (2026-10-07); release target undecided |
| Calculation sheet export (.docx) of P1–P4, rope tensions, P5–P7, FF1/FF2 | Implemented in separate candidate `bin-shaftmaster-calcsheet-candidate`; release decision pending | `ElevatorAutomation/docs/calculations/CALCULATION-SHEET-EXPORT.md`, `output/calcsheet-20261007/` | Reporting only, no new equations; new calculations must add their own sheet section |
| Car guide-rail check per EN 81-50 5.10 (bending/combined/buckling/flange/deflection, using FF1/FF2 + P5 as Fv) | Pending (next candidate stage; not implemented) | FF interpretation in `CAR-GUIDE-FORCES.md`; car rail `T70/65/9` labelled in drawings (`VerticalGenerators.cs`, `DWG_STRUCTURE_REPORT.md`); `VerticalProjectData.RequestedBracketMaxSpacingMm` (mandatory, no default) | Rail section properties for T70/65/9; bracket spacing l (from `RequestedBracketMaxSpacingMm`); rail material/A5; Mg; Fp treatment; deflection limit |
| Remaining note calculations | Pending inventory | Source document must be reviewed directly | Identify exact sections, equations, current coverage, ambiguities, and dependencies |

