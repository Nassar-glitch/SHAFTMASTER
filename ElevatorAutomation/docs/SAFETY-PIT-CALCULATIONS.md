# Safety gear and pit loads candidate

2026-10-06. Candidate: `bin-shaftmaster-safety-pit-candidate/ShaftMaster.exe`. Run it from the complete candidate folder. The release and previous Basic/Advanced candidates were preserved.

## Workflow

Continue using Basic P1–P4 or Advanced normally. On Calculations, expand **Safety gear & pit loads**. Confirm the car safety-gear type, physical car-guide count, and physical car/CW buffer quantities, P5–P7 are calculated live (see `PRESETS.md`, "Live calculation"). These event loads are independent of chassis reactions and do not require chassis geometry, CG offsets, member choices, source text, machine mass or rope-tension inputs.

Capacity and empty car/CW masses are reused from the canonical project. If an existing car/CW additional effective suspended mass is positive, its event-allocation choice appears automatically: all, none, or a percentage of that same mass. No mass is entered twice. Car allocation applies to both car safety and car-buffer events in this phase; CW allocation applies to the CW-buffer event. Blank optional additional mass is not included; an entered zero stays an explicit zero.

One physical buffer needs no sharing choice. Multiple buffers need an explicit **equal sharing** selection before this model produces component reactions. Selecting unresolved sharing gives unavailable buffer loads. The total event group load and the load per buffer are labelled separately. Physical quantities come from explicit user confirmation, not CAD line counts or projected symbols. The UI offers 1–4 buffers; the equal-sharing API uses a positive explicit integer count. Only progressive car safety gear with two physical car guides is initially supported.

## Directly verified source basis

Source: `Elevator_Calculations_GEARBOX_Code_Corrected_Dual_Units.docx`.

SHA-256: `ECFD591133F5F97ED923F62A3A3EEA60C2B97A67805E60BCB7131C3DA7FB13DF`.

| Load | Note equation and application | Word paragraph evidence |
|---|---|---|
| P5 | `(capacity + car + compensation) × 10 N/kg`, **per car guide rail**, progressive safety gear | zero-based descendant paragraphs 104–106; equation paraId `439730EB` |
| P6 | `(capacity + car + compensation) × max(40, buffer factor × g) N/kg`, car-buffer pit support | paragraphs 108–110; equation paraId `5429655E` |
| P7 | `counterweight × max(40, buffer factor × g) N/kg`, CW-buffer pit support | paragraphs 112–114; equation paraId `3897FF34` |

The note input table supplies safety coefficient **10 N/kg**, buffer factor **4**, and physical gravity **9.81 m/s²**. Thus the adopted buffer coefficient is `max(40,4×9.81) = 40 N/kg`. The rounded safety coefficient is not replaced by g. P5 is already per guide; there is no further guide-count division.

The note provides no P5–P7 standard clause or manufacturer basis. Its final standards discussion concerns ropes. Its compensation table says only 600 kg; the car-borne worked-example assumption is expressed by its car-side equations and confirmed by the user. It is not adopted automatically for live additional suspended masses.

For live projects the engine applies the same coefficients to the explicitly allocated **event masses**:

`car event mass = capacity + empty car + allocated car-side additions`

`CW event mass = counterweight + allocated CW-side additions`

Machine, chassis and hanger masses are excluded even when included in P1–P4. The live CW additional-mass allocation is a declared extension of event mass accounting; the source example itself has CW mass only. No rope ratio or rope tension is added to these event loads. The independent note fixture retains its 2:1 rope topology; the drawing workflow remains 1:1.

## Fixture results

Synthetic verification only: capacity 480 kg, empty car 500 kg, car-borne compensation 600 kg, CW 740 kg, two car guides and one buffer per assembly. These values are confined to labelled test artifacts and are not real-project defaults.

| Result | Substitution | Verified force |
|---|---|---:|
| P5 per guide | `(480+500+600)×10` | 15,800 N |
| P6 single car buffer | `(480+500+600)×max(40,4×9.81)` | 63,200 N |
| P7 single CW buffer | `740×max(40,4×9.81)` | 29,600 N |

P5's displayed pair total is the sum of two per-guide loads, 31,600 N, and is not a chassis or buffer reaction. For an explicitly assumed equal-sharing two-car-buffer example, P6 remains 63,200 N for the group and becomes 31,600 N per buffer. Equal sharing is a selected ideal assumption, absent from the supplied note; actual contact/stiffness sharing is not verified.

## Data, availability and preservation

**Presets (DESIGN-FLOW stage 2, `PRESETS.md`):** a new project takes progressive gear when v > 0.63 m/s (otherwise instantaneous), 2 car guide rails, energy-accumulation buffers when v ≤ 1.0 m/s (otherwise energy dissipation), 1 car / 1 counterweight buffer and equal sharing. Each follows its preset until the user changes it. `BufferType` is a project selection only; the P6 / P7 equations do not use it. Instantaneous gear leaves P5 unavailable (only progressive gear is defined in the note). Older projects keep their values, or blanks, until "Fill blanks from presets".

New fields are optional under `ElevatorProject.Calculations.SafetyPit`: safety-gear type, physical guide/buffer quantities, multiple-buffer sharing choices, additional event-mass allocation choices and optional participating percentages. Older projects open with this group missing, not populated with fixture values. Schema version remains 1 with the new nullable optional group.

SafetyPit is excluded from legacy Basic/Advanced resolution and from chassis calculation fingerprints. Event-only edits therefore do not invalidate otherwise current chassis reactions. Safety-only older projects stay Basic; populated older Advanced projects remain Advanced. Chassis engines and equations were preserved.

P5/P6/P7 each return `CALCULATED_UNDER_NOTE_ASSUMPTIONS`, `MISSING_INPUT`, `INVALID_INPUT` or `UNSUPPORTED_CONFIGURATION`. Missing/invalid loads are null, never fabricated zeros. Independent assemblies may remain available when another load is unavailable. Relevant edits hide old event numbers; a separate input fingerprint prevents stale display. Inactive percentages and retained multi-buffer sharing choices cannot become hidden mandatory inputs.

Missing event data does not block Basic P1–P4, Advanced calculations or drawing generation. Inputs survive save/open/save-as and native drawing storage/reopening. Equations, substitutions, composition, assumptions, statuses and source identifiers are expandable; no calculated loads are published on drawings.

**Malformed mass text (fix of 2026-10-06):** a malformed mass field clears its value and keeps a field error; it is never treated as an omitted mass. Only the event loads that use the field become `INVALID_INPUT`; the other assembly keeps its loads:

| Malformed field | Loads unavailable | Independent loads preserved |
|---|---|---|
| Empty car mass / car-side additional mass / active car percentage | P5 and P6 | P7 |
| CW mass / CW-side additional mass / active CW percentage | P7 | P5 and P6 |

Machine, CG, chassis and hanger draft errors are ignored for these event loads. Intentionally blank optional masses remain omitted; an entered zero stays zero; inactive percentages are ignored. Covered by `tests/solution/SafetyPitDraftUiTests.cs` (`run-safety-pit-draft-ui.ps1`, tier 2, 117 assertions).

## Verification

- Direct read-only DOCX verification passed for all three equations, adopted coefficients, units, per-guide/single-buffer meanings, source checksum and independent expected substitutions.
- **116** new engine/workflow assertions passed: note fixtures; mass accounting; per-guide/group meanings; equal sharing; supported/unsupported types/counts; missing/invalid values; allocation; units; overflow; stale-result checks; persistence; old-project modes; unchanged Basic/Advanced reactions and their currency; nonblocking drawings.
- **35** rendered WPF checks passed: compact choices; no fixture defaults; note results; physical labels; expandable substitutions; immediate mass invalidation; live allocation visibility; multiple-buffer sharing; partial availability; unsupported safety type; percentage handling; Basic independence; save/open/save-as; resized scrolling; and two actual native four-view generate/reopen runs with missing/populated event inputs.
- Existing Basic suite: **49** engine/canonical and **18** rendered UI checks passed. Existing Advanced suite: **25** workflow and **25** rendered UI checks passed.
- Existing Core/project suite: **214** checks passed; detailed chassis calculations: **200** passed. All four calculated drawing views matched the release baseline at SW/SD 1450/1600, 1650/1800 and 1850/2000 mm. **32** protected files remained unchanged.
- Existing STEP input/dependency/output-consent checks: **7** passed. STEP generation source/geometry was not changed; a fresh STEP export/reimport was not repeated in this phase.

Evidence: `output/safety-pit-ui-20261006/source-note-verification.json`, `delivery-ui/`, `basic-regression/`, `advanced-regression/`, and `output/calculation-phase1/verification.json`. Screenshots are real WPF renders driven by programmatic control events. They are not an interactive mouse/keyboard pass. Native DWGs were generated, reopened and audited with AutoCAD Core Console.

## Remaining engineering limits

The note coefficients have not been independently certified against an applicable standard clause or manufacturer data. Confirm actual safety-gear/buffer applicability, physical component quantities and event participation of additional masses for each project. The shared car participation choice assumes the same additional car-borne mass participates in both P5 and P6; differing event allocations need a future model extension. Non-progressive safety gear, non-two-guide P5 recipes and unequal multiple-buffer sharing remain unavailable. Detailed pit structures, guide strength, equipment ratings, connection/anchor design and final compliance are not established by these load values.

## Main code and test locations

- `Elevator.Core/SafetyGear/SafetyPitCalculationEngine.cs`: rule constants/basis, event allocation, independent per-load validation, units, group/component outputs and currency.
- `Elevator.Solution.Desktop/SafetyPitPage.cs`: compact independent section, live allocation controls, explicit sharing, result/details display.
- `Elevator.Core/Calculations/ChassisCalculationInputs.cs`: optional canonical input group.
- `tests/solution/SafetyPitTests.cs`, `SafetyPitUiTests.cs`, `run-safety-pit-ui.ps1`: reproducible fixture, API and rendered UI checks.
- `tests/calculations/verify_safety_pit_note.py`: direct read-only source verification.

No Word report, drawing annotations, template edits or structural member/connection design were added.
