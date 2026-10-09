# Safety and pit shared mass draft validation fix

2026-10-06. Updated candidate: `bin-shaftmaster-safety-pit-candidate/ShaftMaster.exe`. Release, equations, templates and STEP implementation were preserved.

The shared mass editor correctly clears the canonical numeric value when text is malformed and retains a separate draft error. The safety/pit UI previously inspected only percentage draft errors. Consequently, the engine's legitimate optional-null omission rule was incorrectly applied to malformed additional-mass text. This reduced loads instead of making them unavailable.

The UI now collects only event-relevant shared mass draft errors and active percentage draft errors. Validation runs both before presenting a calculation and whenever results render, including navigation and Basic/Advanced mode switches. It marks only the affected assembly's loads `INVALID_INPUT`, clears group/per-component loads, participating mass and misleading numeric substitutions, and displays a field-specific error. It preserves independently valid event loads for the other assembly.

| Malformed field | Loads unavailable | Independent loads preserved |
|---|---|---|
| Empty car mass / car-side additional mass / active car percentage | P5 and P6 | P7 |
| CW mass / CW-side additional mass / active CW percentage | P7 | P5 and P6 |

Machine, CG, chassis and hanger draft errors are ignored for these event loads. Intentionally blank optional masses remain omitted; deliberately entered zero remains zero. Inactive percentages remain ignored. A malformed additional-mass field keeps its allocation context visible and clearly labelled invalid rather than presenting it as an omitted mass.

For the reported fixture, malformed car-side additions now produce unavailable P5/P6 while P7 remains 29,600 N. The analogous CW-side failure produces unavailable P7 while P5/P6 remain 15,800/63,200 N. Correcting the text restores calculations on the next Calculate action. No adopted coefficient or load equation changed.

Verification evidence is under `output/safety-pit-draft-fix-20261006/`:

- `wpf/safety-pit-draft-ui-tests.json`: **117 real rendered WPF assertions passed**. Includes car/CW failures, corrections, required masses, intentional blanks/zeros, cached canonical-result rendering safeguards, navigation, Basic/Advanced switches, ignored unrelated errors and active/inactive percentages.
- `existing-safety-pit/`: existing **116 engine** and **35 rendered WPF** checks, including native four-view generation/reopening.
- Candidate packaging build re-ran **190 design/annotation/MR/reeving checks** and **7 STEP input/dependency/output-consent checks**.

The WPF checks use actual textbox/combo/button handlers and rendered windows, driven programmatically; they are not interactive mouse/keyboard testing. The equations remain adopted note rules, not independent manufacturer or code certification.

Main changes: `Elevator.Solution.Desktop/SafetyPitPage.cs`; new focused test `tests/solution/SafetyPitDraftUiTests.cs` and runner `run-safety-pit-draft-ui.ps1`. The existing percentage-error UI assertion now verifies independent CW availability rather than expecting the entire safety/pit result to disappear.
