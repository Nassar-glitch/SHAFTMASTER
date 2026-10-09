# Suspension rope and traction checks

This document covers the rope checks of EN 81-20:2014 5.5 and the traction and rope safety-factor methods of EN 81-50:2014 5.11 and 5.12. It applies to the current system: geared traction with the machine room above and 1:1 roping, with or without a deflector sheave.

- **Code:**
  - Calculation: `Elevator.Core/Suspension/RopeTractionCalculationEngine.cs` (`RopeTractionCalculationEngine`, rule version `EN81-20-5.5_EN81-50-5.11-5.12_RopeTraction-v1`).
  - Presets: `Elevator.Core/Presets/` (`PresetLibrary`, `PresetFields`, `ProjectPresets`).
  - Application: `Elevator.Solution.Desktop/RopeTractionPage.cs`, `CalculationSheetRopes.cs`, `CalculationFiguresRopes.cs` (F10), `DesignNotes.cs`.
- **Sources:**
  - `AHM Folder/CODES/EN 81-20-2014.pdf` and `bs_en_81_t50_e_d01_14.pdf`. Every formula was read from the page images of these files.
  - The engineering decisions marked "Planner A1–A6" were given by the Planner on 2026-10-08.

## Clauses and equations

| Item | Clause | Equation / limit |
|---|---|---|
| Nominal rope diameter | EN 81-20 5.5.1.2 a) | d ≥ 8 mm |
| Number of ropes | EN 81-20 5.5.1.3 | n ≥ 2 |
| D/d, traction sheave | EN 81-20 5.5.2.1 | Dt / d ≥ 40. The deflector Dp / d is a value only (DESIGN-FLOW 11a): no check row; quoted in the Design Notes when below 40; the smaller deflector enters Sf through Kp (EN 81-50 5.12) |
| Safety factor minimum | EN 81-20 5.5.2.2 | 12 for traction with 3 or more ropes; 16 with two ropes. Also not less than the EN 81-50 5.12 value |
| Safety factor definition | EN 81-20 5.5.2.2 | Sf = MBL of one rope / maximum force in that rope, car stationary at the lowest landing with rated load |
| Terminations | EN 81-20 5.5.2.3 | Termination strength ≥ 0.8 × MBL (a computed requirement; the sheet row shows "0.8 × MBL = … kN", actual "MBL = … kN", result "Required strength") |
| Traction conditions | EN 81-20 5.5.3 a)–c) | Car loading; emergency braking; car or counterweight stalled |
| Traction criterion | EN 81-50 5.11.2.1 | T1/T2 ≤ e^(f·α) for car loading and braking; T1/T2 ≥ e^(f·α) for stalled |
| Car loading | EN 81-50 5.11.2.2.1 | 125 % Q, worst position |
| Emergency braking | EN 81-50 5.11.2.2.2 | Empty or rated load, worst position, a ≥ 0.5 m/s² (normal case) |
| Stalled | EN 81-50 5.11.2.2.3 | Empty car |
| Friction factor, semi-circular / undercut groove | EN 81-50 5.11.2.3.1.1 | f = μ · 4 (cos(γ/2) − sin(β/2)) / (π − β − γ − sin β + sin γ); β ≤ 105°, γ ≥ 25° |
| Friction factor, V-groove | EN 81-50 5.11.2.3.1.2 | Loading and braking: unhardened f = μ · 4 (1 − sin(β/2)) / (π − β − sin β); hardened f = μ / sin(γ/2). Stalled, both: f = μ / sin(γ/2). γ ≥ 35°, β ≤ 105° |
| Friction coefficient | EN 81-50 5.11.2.3.2 | μ = 0.1 (loading); 0.1 / (1 + v/10) (braking); 0.2 (stalled) |
| T1, T2 | EN 81-50 5.11.3 a) | Machinery above, r = 1, no compensation and no tensioning device (formulas below) |
| Nequiv | EN 81-50 5.12.2 | Nequiv = Nequiv(t) + Nequiv(p); Nequiv(p) = Kp · (Nps + 4·Npr); Kp = (Dt / Dp)^4 |
| Nequiv(t) | EN 81-50 Table 2 | V by γ: 35° → 18.5, 36° → 16, 38° → 12, 40° → 10, 42° → 8, 45° → 6.5, 50° → 5. U-undercut by β: 75° → 2.5, 80° → 3.0, 85° → 3.8, 90° → 5.0, 95° → 6.7, 100° → 10.0, 105° → 15.2. U without undercut = 1. Linear interpolation |
| Required Sf | EN 81-50 5.12.3 | Sf = 10^(2.6834 − log(695.85·10⁶ · Nequiv / (Dt/dr)^8.567) / log(77.09 · (Dt/dr)^−2.894)), log = log10 |
| Car area (Design Notes only) | EN 81-20 5.4.2.1, Table 6 | Maximum available car area for the rated load, with linear interpolation. Beyond 2500 kg add 0.16 m² per extra 100 kg |

T1 and T2 come from EN 81-50 5.11.3 a), with gn = 9.81 m/s², FR = 0 (Planner A4) and the deflector on the counterweight side (term II).

- **Car loading,** lowest landing (Annex D): T1 = (P + 1.25 Q)·gn + MSRcar·gn; T2 = Mcwt·gn.
- **Braking, rated load, lowest landing, retarding downwards:** T1 = (P + Q)(gn + a) + MSRcar(gn + a); T2 = Mcwt(gn − a) − mDP·a.
- **Braking, empty car, highest landing, retarding upwards** (Note 1: Q deleted, T1 and T2 exchange): T1 = Mcwt(gn + a) + MSRcwt(gn + a) + mDP·a; T2 = (P + MTrav)(gn − a).
- **Stalled, counterweight on its buffers, empty car at the highest position** (Annex D): T1 = (P + MTrav)·gn; T2 = MSRcwt·gn.

Rope and cable masses use MSR = ns·(0.5H ± y)·w and MTrav = (0.25H ± 0.5y)·nt·w, with y taken from Annex D:
- car at the lowest landing: MSRcar = ns·H·w, MSRcwt = 0, MTrav = 0;
- car at the highest landing: MSRcar = 0, MSRcwt = ns·H·w, MTrav = 0.5·Htr·w(cable).

## Inputs (Suspension page)

Each input below is optional. When it is not locked it takes its preset from `config/presets.json`.

| Input | Preset (DESIGN-FLOW) |
|---|---|
| Rope designation | "8×19 Seale FC 1370/1770" |
| MBL per rope (kN), mass per metre (kg/m) | By rope diameter: 6.5 mm → 18.5 kN / 0.144 kg/m; 8 → 28.1 / 0.218; 10 → 44.0 / 0.340; 11 → 53.2 / 0.411; 12 → 63.3 / 0.490. They follow the diameter by exact match. An unlisted diameter has no preset and is left blank |
| Groove type | Semi-circular / undercut, with β = 95°, γ = 30° |
| β, γ | Shown only where the groove type uses them |
| Travelling cable mass per metre, all cables (= nt·w) | 0.5 kg/m (Planner A6) |
| Emergency braking retardation a | 0.5 m/s²; rejected below 0.5 (Planner A1) |
| Deflector sheave mass | By the deflector pitch diameter: 400 mm → 45 kg, 520 → 70, 620 → 95, interpolated. Outside the table the nearest end value is used, so a 250 mm deflector gets 45 kg |
| Traction limiting method | Ropes slip (EN 81-20 5.5.3 c) 1)). With an electric safety device (c) 2)) the stalled check does not apply and is not shown |

Reused project data: rope count and diameter, sheave pitch diameter, rated speed, rated load Q, and the floors (served travel). Also reused:
- **Overhead:** `Shaft.OverheadMm`.
- **P and M:** P = car mass plus the car-side additional suspended mass; Mcwt = counterweight mass plus the counterweight-side additional mass, as in the rope tensions section.

## Lock and follow

- **Mechanism:** generic, in `PresetLibrary`, `PresetField` and `PresetFields`.
  - An edited field is locked: its key is added to `ElevatorProject.UserEditedFields` and its value is stored in `Calculations.RopeTraction`.
  - An unlocked field always shows the current preset. MBL and kg/m follow the rope diameter, and the deflector mass follows the deflector pitch.
  - "Reset to preset" (the editor context menu) removes the lock.
- **Older projects:** they have neither the group nor the list, so they load unchanged and resolve every field to its preset. (This stays true for these rope / traction data fields in stage 2.)
- **Rope count, diameter and sheave (stage 2):** they follow the rope ladder by Q and the 520 mm sheave preset in new projects, with the rope and groove selection (smallest passing d, then n; β 95 → 105°); see `PRESETS.md`.
- **Preset library validation:** the Core loader checks the shipped file. It rejects missing tables, non-positive values, a < 0.5, groove angles outside the standard and unknown keys.
- **Editor validation:** the editors reject V γ < 35°, U γ < 25°, β > 105° and a < 0.5 with a short message, and the value is not stored.

## Derived from the shared geometry

| Item | Source |
|---|---|
| Deflector present, and its pitch diameter | `MachineRoomRules.SelectDeflector(TZ)`. Required for TZ ≥ 520; 250 / 400 mm. For TZ > 1100 no size is selected |
| Wrap angle α | With a deflector and a calculated path: `MachineRoomSectionRules` → `ReevingSolver.CalculatedWrapAngleDeg`. Without a deflector (direct roping): α = 180° (Planner A3) |
| Nps / Npr | 1:1 with a deflector: one simple bend, Npr = 0. The rope path is an external tangent in the same bending direction, as in EN 81-50 Annex E, Figure E.2. Without a deflector: Nequiv(p) = 0 |
| Travel Htr | Served floors (`ProjectFloors`) |
| H, the rope-mass length | Travel + overhead (Planner A6). The travelling cable uses the travel |
| mDP | J/R² · (v_pulley/v)², J = ½·m·R² (solid sheave), so mDP = m/2 at 1:1 (Planner A2) |
| Car W × D (Table 6) | Shared plan internal dimensions |

**Deflector without a valid path** (TZ > 1100, no deflector size selected; Planner A3):
- The traction checks are omitted, together with the EN 81-50 safety factor, which needs a selected deflector.
- The omission appears in the export dialog and in the Design Notes.

## Results policy

- **Calculation sheet:** each check shows the equation, the substitution, the required value, the actual value and OK / NOT OK, with no commentary.
- **Export dialog (application only):** lists every NOT OK result and every omitted check.
- **Design Notes** (Workflow → Export Design Notes…, `DesignNotes.cs`, DESIGN-FLOW principle 7): short advisory notes only, one or two lines each, written as an engineer's advice with the clause in brackets. Nothing is written about checks that pass.
  - **Warnings:** every NOT OK result and every check left out for an invalid layout. Each carries a recommendation computed from the project:
    - **Table 6:** the minimum rated load by inverse Table 6 interpolation (`CarAreaRules.MinimumRatedLoadKg`; 2.31 m² → 955 kg), or the maximum car area for the rated load.
    - **Rope safety factor:** the smallest rope count up to 7, and the next listed rope diameter (taking that diameter's preset rope data) that gives an OK safety factor without a D/d failure. Both are computed with the same engine on a copy of the project.
    - **D/d:** the minimum pitch diameter = 40·d.
    - **Traction:** the failing case and position with its e^(f·α) margin, and the wrap angle or friction factor f the case needs, from ln(T1/T2) / f or / α.
    - **Invalid layout:** the cause and the valid TZ range, 520–1100 mm. The note does not name the internal rule ID.
    - **Rope sizing (stage 2):** when the rope selection finds no passing arrangement, one recommendation names the limiting check, recommends 2:1 roping for the rated load and gives the minimum rope MBL at 7 ropes (joint selection over ropes, β, q and Dt: see `PRESETS.md`).
  - **Recommendations:** the inputs to enter to complete a check that could not be calculated.
  - Only headings that have notes are shown. With no notes the document holds the single line "No design notes for this project."
- **Partial availability:** each check is calculated only when its own inputs exist. A missing value is never treated as zero.
- **No automatic upsizing** in this stage.
- **Machine-room rules:** `RopeTractionRules.Apply` evaluates MR-ROPE-MIN-001, MR-ROPE-001 and MR-TRACTION-001 from the results. Their status is PASS or FAIL instead of NOT YET EVALUABLE once the inputs exist.
- **Fingerprint:** separate from P1–P4, P5–P7 and FF. Rope inputs are excluded from the P1–P4 fingerprint, and FF / P5–P7 edits do not stale this result.

## Tests

| Suite | Runner | What it covers |
|---|---|---|
| Core | `tests/solution/RopeTractionTests.cs` (`run-rope-traction-core.ps1`) | Presets, lock / follow / reset and round trip; fixtures with and without a deflector; every groove type, including the clamped edges; D/d = 40 exactly; Sf exactly at the limit and just below it; NOT OK cases; omissions; invalid geometry; rule connection; Table 6; independence from the other calculations |
| Independent hand check | `tests/solution/verify_rope_traction.py` | Re-implements the formulas above from the standard and compares every engine value of the exported fixtures: Nequiv, Kp, Sf, Tmax, μ, f, e^(fα), T1, T2 and OK |
| UI | `tests/solution/RopeTractionUiTests.cs` (`run-rope-traction-ui.ps1`) | Suspension editors (presets shown, follow, lock, reset, rejection messages); Calculate; export dialog with NOT OK; Design Notes; save and reopen |
| Sheet | `tests/solution/CalculationSheetTests.cs` | Fixtures 14–15: section, summary row, input rows, termination row, F10, NOT OK. Design Notes fixtures 16–19: Table 6 warning with 955 kg, invalid layout, traction margin, rope upsize, the clean "No design notes" case, and no forbidden wording or tables |
