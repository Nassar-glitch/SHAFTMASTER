# Project presets (DESIGN-FLOW stage 2, "Flow foundation")

A new ShaftMaster project opens complete: every input has a value, and every calculation, the calculation sheet, the Design Notes and the native four-view drawing are available with no input. Values come from the editable preset library `config/presets.json`, which the build copies beside `ShaftMaster.exe`.

These are company design defaults approved in DESIGN-FLOW. They are not standard factors, manufacturer data or certification. No calculation equation uses the library directly: presets only fill project inputs, and the engines read the project as before.

## Preset table (shipped `presets.json`)

| Input | Preset |
|---|---|
| Shaft (survey example) | SW 1500 mm, SD 1800 mm, pit 1300 mm, overhead 4200 mm |
| Floors | 5 stops (G + 4) at 3000 mm; a floor added by the user gets 3000 mm |
| Rated load Q | 630 kg; persons = floor(Q / 75) is shown next to Q (8 persons) |
| Speed v | 1.0 m/s |
| Car mass P / machine mass | Capacity table, linear between rows and clamped at the ends: 320 → 450 / 250; 480 → 550 / 300; 630 → 700 / 350; 800 → 850 / 400; 1000 → 1100 / 450; 1275 → 1350 / 550; 1600 → 1650 / 650 kg |
| Counterweight | CW = P + q·Q with q = 0.5 (follows P and Q while unlocked); q steps 0.50 → 0.45 (`BalanceFactorSteps`) in the joint selection |
| Ropes (count × diameter) | Selected by the search below. Ladder (fallback only): Q ≤ 480: 4 × 10; ≤ 800: 5 × 11; ≤ 1275: 6 × 12; above: 7 × 12 mm. Count limit 7 |
| Groove undercut β | 95° preset; steps 95 → 100 → 105° while a traction case is NOT OK |
| Traction sheave pitch diameter | 520 mm; steps 520 → 560 → 620 mm, used only by the rope selection |
| Guide-shoe spacing h | 2800 mm |
| Safety gear | Progressive when v > 0.63 m/s, otherwise instantaneous; 2 car guide rails |
| Buffers | Energy accumulation when v ≤ 1.0 m/s, otherwise energy dissipation; 1 car / 1 counterweight buffer; equal sharing |
| Bracket spacing | 1500 mm |
| P1–P4 Basic | Optional additions excluded; chassis and hanger assembly excluded |
| P1–P4 Advanced | Machine CG offsets 0 at the traction sheave centre; hanger attachment offsets 0; additions 0; the existing transfer model `ReleasedTwoStageWithRigidSpreader-v1` and load case `StaticRatedLoad1To1`. The basis texts are kept in the project and hidden from the UI. The left / right carrying-member pair has no preset; it remains a selection |
| Rope / traction data | Unchanged from stage 1 (see `ROPE-TRACTION-CALCULATIONS.md`) |
| Not preset | Machine-room extensions, counterweight reference pose and drawing options keep their existing values; the minimum deflector / slab gap stays a project input |

New-project example: Q 630 → P 700, machine 350, CW 1015, ropes 4 × 11 mm with β 100° (every rope and traction check OK), h 2800, progressive gear, energy-accumulation buffers. Its Design Notes carry only the deflector D/d quotation (DESIGN-FLOW 11a).

The loader (`PresetLibrary.Parse`) rejects a file with a missing section, fewer than two capacity rows, duplicate capacities, a ladder diameter that is not in the rope table, a ladder count outside 4–7, ladder capacities that do not rise, a bounded last ladder row, a sheave step list without the sheave preset, q outside 0–1, sharing other than equal, a guide count other than 2, another transfer model, a non-positive floor height or fewer than 2 stops.

## Lock, follow and Reset

- **Mechanism:** `ProjectPresets` (`Elevator.Core/Project/ProjectPresets.cs`) extends the stage-1 mechanism. Each project input is a `PresetField` with `Materialized = true`: its value is an ordinary project value, written by `ProjectPresets.Apply` while it is unlocked.
- **New project:** `ElevatorProject.FollowsPresets = true`, nothing locked.
- **Lock:** editing a field locks it (its key is added to `UserEditedFields`). A locked value is never changed by a preset or by the rope / groove selection. Clearing a field locks it blank.
- **Follow:** an unlocked field takes its preset again whenever a driver changes: P, machine and the ropes follow Q; CW follows P and Q; gear and buffer type follow v. Typing updates the fields that follow at once. The rope and groove selection runs with the live calculation (and on Commit, page change, Save and export).
- **Reset to preset** (editor or choice context menu) removes the lock; the field follows again.
- Flags and locked values are saved in the project file and survive the native DWG save / reopen.

## Joint selection: ropes, groove β, balance factor q, sheave

While the rope count and / or diameter are unlocked, `ProjectPresets.SizeRopes` searches with the existing rope / traction engine (no equation change):

1. **Candidates:** n = 4 … 7, and d from the rope table limited to d ≤ Dt / 40. The deflector D/d is not a constraint (DESIGN-FLOW 11a).
   - **Order:** start at the ladder diameter for Q. Take the largest d ≤ the ladder d that passes, then the smallest n. A larger d is used only if nothing passes.
2. **Pass:** the first combination whose checks are all OK is taken: nominal diameter ≥ 8 mm, rope count, Sf (with Kp from the actual deflector), the traction sheave D/d and every traction case (complete results, no omission). The rope ladder is only the fallback value.
3. **Joint levers, in order of preference (least change from the presets first):** (a) the rope arrangement; (b) β steps 95 → 100 → 105° (`UndercutAngleStepsDeg`; groove type and γ stay); (c) balance factor q steps 0.50 → 0.45 (`BalanceFactorSteps`, CW = P + q·Q, only while the CW is unlocked); (d) Dt steps 520 → 560 → 620. The first passing combination in that order is taken. A stepped β or q is stored as a selection (`RopeTractionInputs.SelectedUndercutAngleDeg`, `SelectedBalanceFactor`), not a user value.
4. **Sheave:** the native B-B section accepts only 520 mm, so a larger sheave gives an invalid layout and is skipped.
5. **No pass on 1:1:** the ladder / preset values are kept, the checks stay NOT OK, and the Design Notes add one recommendation: the limiting check, "2:1 roping is recommended for this rated load", and the minimum rope MBL that would satisfy the safety factor at 7 ropes of the kept diameter.
6. Locked count, diameter, β, groove type, counterweight (so q) and sheave are never changed.

**Search speed:**
- A diameter that fails the minimum diameter or a D/d check is not tried with more ropes.
- After the first β, only the combinations that failed on traction alone are tried again. A larger β raises Nequiv(t) (EN 81-50 Table 2), and so the required Sf.
- The result is cached for an unchanged project.

**Deflector policy (DESIGN-FLOW 11a, user decision, market practice):**
- The deflector D/d is not a check row in the app or the calculation sheet; the sheet shows the deflector pitch diameter as a value.
- The rope safety factor keeps the actual deflector (EN 81-50 5.12, Kp = (Dt / Dp)^4).
- When Dp / d < 40, the Design Notes carry one quotation under "Notes", e.g. "Deflector pitch diameter 400 mm gives D/d = 36.36 for 11 mm ropes; EN 81-20 5.5.2.1 requires D/d ≥ 40 for pulleys (≥ 440 mm). The smaller deflector is accounted for in the rope safety factor (EN 81-50 5.12)."
- The deflector selection rule (MR-DS-001), the drawing-side rule record MR-ROPE-001, the drawings and STEP are unchanged.

**Survey shaft:**
- The new project (Q 630) selects 4 × 11 mm with β 100°, and every check is OK. The Design Notes carry only the deflector quotation (D/d 36.36).
- The Q sweep (320–1600, survey shaft and Table 6 shafts) is in `output/presets-heavy-20261009/final2/presets-core/q-sweep.md`.

## Live calculation

- Every calculation (P1–P4, ropes and traction, P5–P7, FF1 / FF2) is recalculated automatically 300 ms after any input change, on every page. There are no Calculate buttons.
- Only results whose inputs changed are recalculated.
- An invalid entry shows its message on the field, and the dependent section shows nothing until it is fixed. An outdated or zero value is never shown.
- The UI and the status bar show short words instead of engine status codes.

## Older projects

- A project without `FollowsPresets` (every file saved before this stage) opens unchanged: its values count as user-edited, and its blanks stay blank. `Apply` writes nothing.
- The stage-1 rope / traction data fields keep their stage-1 behaviour (they follow the presets in every project).
- **Fill blanks from presets:** one action per page (shown while the page has a blank preset field) and one global action in the Project toolbar.
  - The project then follows the presets. Every existing value is kept and locked, and only the blank fields take their presets.
  - Floors: a blank interval gets 3000 mm.
  - Advanced-only values of a Basic project are left as they are.

## UI text

DESIGN-FLOW: the pages show short labels with units, results and real validation messages only.

- Hints, guidance notes, disclaimers, read-only metadata fields and the "source / assumption" text fields were removed from the UI. The values stay in the project model.
- Validation page: status words OK / Warning / Not OK / Not evaluated / Check. Core rule messages are kept, with "NOT YET EVALUABLE" and "NOT PROVIDED" spelled plainly and bracketed rule IDs removed.
- Design Notes: internal rule IDs removed (e.g. "(project rule MR-DS-001)").
- Native DWG annotation is unchanged.

## Model and schema

- `ElevatorProject.FollowsPresets` (bool?, optional).
- `SafetyPitInputs.BufferType` ("EnergyAccumulation" / "EnergyDissipation"): a project selection only; the P6 / P7 equations do not use it.
- The safety-gear choice adds "Instantaneous". P5 stays defined for progressive gear only, so P5 is unavailable (UNSUPPORTED_CONFIGURATION) for instantaneous gear, as before for any other type.
- `config/ElevatorProject.schema.json` keeps schema version 1; both fields are optional.

## Tests

| Suite | File | Covers |
|---|---|---|
| Core | `tests/solution/PresetsTests.cs` (`run-presets-core.ps1`) | Loader validation; interpolation at and between the table points and clamping; CW; persons; ladder; gear / buffer rules; new project complete with no input (P1–P4, P5–P7, FF, ropes); lock / follow / Reset and round trip; rope and groove selection (independent brute-force order check, locked count / diameter / β / groove type, no pass with a named limiting check), Q sweep on the survey and Table 6 shafts; older projects and Fill blanks; identical numbers for identical inputs |
| UI | `tests/solution/PresetsUiTests.cs` (`run-presets-ui.ps1`) | Real window: new-project pages; every result shown live with no button (no Calculate buttons); live recalculation after edits on each page with no stale or zero value; invalid input shows only its message; sheet and Design Notes exported from the new project, free of forbidden wording; lock / follow / Reset through the editors; Save and reopen; older project unchanged; page and global Fill blanks; visual-tree scan of every page and the status bar for the removed strings and UPPER_SNAKE_CASE status codes |
| Native | `tests/solution/PresetsNativeTests.cs` | New project → all four native views through the AutoCAD host, with fresh verification and reopen |
