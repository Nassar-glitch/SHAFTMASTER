# Testing

The tests are organised in three tiers (ARCHITECTURE-PLAN section 3). One entry point runs them:

```powershell
cd ElevatorAutomation
./tools/test.ps1 -Tier 1          # after every change
./tools/test.ps1 -Tier 1,2,3      # end of a phase / before a release
```

`tools/test.ps1` first builds into `artifacts/bin` (`tools/build.ps1`; `-NoBuild` skips it, `-Bin` chooses another non-release folder), then runs the suites of each requested tier. Every run writes into a fresh folder `artifacts/test-results/<yyyyMMdd-HHmmss>/`: one subfolder per suite (`tier<N>/<suite>/`), one log per suite, `build.log`, and `summary.json` / `summary.md` with the result, the assertion count and the time of every suite and the time per tier. A failing suite fails the run. Tests never run in, or build into, a release folder (`bin-shaftmaster-v*`).

| Tier | Suites | Needs |
|---|---|---|
| 1 | `golden` (tests/golden), `presets-core`, `rope-core` (with the Python hand check), `sheet-core` (no Word), `ff-core`, `safety-pit-core`, `xunit-core` (tests/Elevator.Core.Tests on net8.0 and net48) | .NET Framework, .NET 8 runtime, Python 3 |
| 2 | Programmatic WPF suites: `presets-ui`, `rope-ui`, `sheet-ui`, `calculation-ui`, `basic`, `safety-pit-ui`, `safety-pit-draft-ui`, `ff-ui` | as tier 1, a desktop session; `calculation-ui` and `basic` also generate native drawings (AutoCAD 2023) |
| 3 | `integration` (tests/integration/run-tier3.ps1: affected regressions, FF native / STEP integration, STEP input, native golden), `presets-native`, `sheet-word-render` | AutoCAD 2023, Microsoft Word, Python with PyMuPDF and python-docx, the local evidence folder `output/calculation-phase1` and the release `bin-shaftmaster-v01` (read only) |

Reference run (2026-10-09, `artifacts/test-results/20261009-053328`, all PASS): build 8 s, tier 1 204 s, tier 2 215 s, tier 3 229 s (the Word render of 23 sheets, 169 pages, takes 185 s of it). The golden suite takes about 70–85 s, most of it the rope selection of the heavy-Q fixtures.

The AutoCAD script suites of `tests/run-tests.ps1` (plan, machine room, section, legacy regression) are not part of the tiers yet.

## Tier 1: golden master (`tests/golden`)

`tools/run-golden.ps1` compiles `tests/golden/GoldenTests.cs` with the report sources of the desktop application (calculation sheet, Design Notes, DOCX writer, figures, calculation workspace, project session) against `Elevator.Core.dll` and `Elevator.Solution.Bridge.dll` of the build, and runs it. No AutoCAD and no Word; WPF is used only to measure figure text.

For every fixture `tests/golden/fixtures/<name>.elevator.json` (optional `<name>.options.json`: description, company name, logo, malformed-text draft errors) it captures, in `tests/golden/approved/<name>/`:

| File | Content |
|---|---|
| `roundtrip.elevator.json` | The project JSON after load → save through `ProjectSession` (values byte-exact; member order normalised, see below) |
| `results.json` | The automatic rope selection (`ProjectPresets.Apply`, for projects that follow the presets) with n × d, Dt, β, q, CW, P and the locked fields; the full P1–P4 result (Basic or Advanced, rope tensions), P5–P7, FF1 / FF2 and rope / traction results |
| `fingerprints.json` | The input fingerprints of the four results, whether each is current, and the preset-library hash |
| `sheet-omitted.json`, `sheet-figure-errors.json` | Calculations left out of the sheet (with reasons) and figure-builder errors |
| `sheet.txt`, `design-notes.txt` | Calculation-sheet and Design Notes text: `word/document.xml`, then header and footer parts, one line per paragraph (`w:t` text; `w:br` new line; `w:tab` tab; field codes in braces) |
| `figures/NN-<title>.svg` | Every figure placed in the calculation sheet, as SVG |

plus `new-project-from-presets/new-project.elevator.json`: the project of File > New, built exactly as the application builds it.

Fixtures (36):
- `new-project-q630`: the default new project.
- `sweep-survey-q0320` … `q1600` and `sweep-table6-q0320` … `q1600`: the Q sweep 320 / 480 / 630 / 800 / 1000 / 1275 / 1600 on the default survey shaft (1500 × 1800) and on the Table 6 shafts of `output/presets-heavy-20261009` (1300 × 1400, 1300 × 1800, 1300 × 2200, 1500 × 2200, 1900 × 2000, 2200 × 2000, 2200 × 2200).
- Locked rope / groove values: `locked-mbl-q320`, `locked-rope-count-7`, `locked-rope-count-and-diameter`, `locked-beta-95`, `locked-groove-type`, `locked-counterweight-q1000`, `locked-sheave-q1600` (the unlocked counterparts are the new project and the sweep).
- Calculation-sheet cases: `basic-complete`, `advanced-complete`, `advanced-missing-cg`, `missing-h`, `missing-masses-and-pit`, `missing-rope-count`, `malformed-text` (draft errors), `malformed-values`, `rope-traction-ok`, `rope-traction-not-ok`, `titleblock-filled`, `titleblock-blank`.
- Older projects: `older-v01-example` (the shipped v01-format example file, unchanged) and `older-no-report-group`.

The fixtures reuse the existing test inputs (`PresetsTests`, `CalculationSheetTests`, `tests/fixtures/SYNTHETIC.elevator.json`, `examples/ElevatorProject.example.json`). They were written once with `./tools/run-golden.ps1 -MakeFixtures` (the generator is in `GoldenTests.cs`).

**Comparison.** Every captured file is compared byte for byte with the approved file. A difference, a missing approved file or an approved file that is no longer produced fails the run; the received file (`*.received`) and a readable diff (`*.diff.txt`, approved lines `-`, received lines `+`, with context) are written to `<run>/mismatch/`. Every capture is also kept in `<run>/captured/`. `tests/golden/.gitattributes` turns off line-ending conversion so a fresh clone compares the same bytes.

**Approval.** `./tools/run-golden.ps1 -Approve` writes the approved files and refuses to run when `tests/golden/approved` is not empty: it creates the initial baseline only. A later intended behaviour change needs a Planner decision on how to update the baseline.

**Baseline.** Created on 2026-10-09 from branch `refactor/p0-safety-net` at commit `aa4f5e8`, whose behaviour is that of `main` `1125152` (release v03): the only code changes before it are the build default, the report clock and the source-root lookup, none of which changes a captured value. `tests/golden/check_evidence.py` confirmed the baseline against the earlier evidence (237 checks): the Q sweep of `output/presets-heavy-20261009/final2` (selection, n × d, Dt, β, q, CW, rope checks, traction cases, deflector, Design Notes), FF1 / FF2 2200 / 2100 N, P5 9800 N, P6 39200 N, P7 29600 / 14800 N, and the new project 4 × 11, β 100, q 0.5.

**Determinism.** Two consecutive runs produce identical files. The report date comes from `ReportClock.Now` (the local time in the application), fixed by the tests at 2026-10-09 12:00.

### Normalisations

Only values that are volatile by nature are normalised:

1. **Report date:** `ReportClock.Now` is fixed (2026-10-09 12:00 local) for the calculation sheet, the Design Notes and the DOCX `Created` property.
2. **Selection run time:** `RopeSizing.Milliseconds` (the time the rope search took) is set to 0.
3. **Project JSON member order:** `JavaScriptSerializer` writes object members in reflection order, which is not stable between processes (the same project saved twice can list the `Calculations` members in a different order). The project JSON captures (`roundtrip.elevator.json`, `new-project.elevator.json`, the native `reopened-project.elevator.json`) therefore sort the members of every object by key (`tests/golden/JsonMembers.cs`); every value keeps its exact text. Note for P2: product files today can differ in member order between saves.
4. **New-project GUIDs:** in `new-project-from-presets/new-project.elevator.json` the random 32-hex project and floor identifiers are replaced by `GUID-1`, `GUID-2`, … in order of appearance (references stay consistent). Fixture files keep their stored identifiers.
5. **Result JSON:** `results.json`, `fingerprints.json` and the other JSON captures are written with sorted keys and indentation (values from the serializer).
6. **DOCX:** only the text of `document.xml`, the header and the footer is compared, not the package (part GUIDs, relationship IDs, `docProps`, images); figures are compared as SVG.

Figure SVG depends on the fonts installed (text is measured with ISOCPEUR, Arial Narrow or Arial, the first one installed).

## Tier 1: xUnit Core tests (`tests/Elevator.Core.Tests`, ARCHITECTURE-PLAN P2)

`tests/Elevator.Core.Tests/run-xunit.ps1` (and `tools/test.ps1` suite `xunit-core`) runs the xUnit project on net8.0 (against the netstandard2.0 build of `Elevator.Core`) and on net48 (against the application's build). It contains:

- **GoldenCoreTests:** the Core-level golden captures of `tests/golden/GoldenTests.cs` for all 36 fixtures plus the new project (round-trip project JSON, `results.json`, `fingerprints.json`), compared with the same approved files in `tests/golden/approved`. The calculation-sheet, Design Notes and figure captures need the WPF report code and stay in the console golden suite.
- **ConsoleSuiteTests:** the rope / traction (60), FF (131) and safety / pit (117) console suites run unchanged inside xUnit; presets (160) on net48 only, because it needs the Design Notes and so the WPF calculation-sheet sources. The console runners stay until P7.
- **SerializerParityTests** (net48): the evidence for the serializer decision below.

CI (`.github/workflows/ci.yml`, not pushed yet): ubuntu-latest builds `Elevator.Core` for netstandard2.0 and runs the tests on net8.0; windows-latest builds Core and Bridge.

### Serializer decision (P2)

The contract is the output of `System.Web.Script.Serialization.JavaScriptSerializer`, which exists only on .NET Framework. Core reaches JSON only through `CoreJson` / `IJsonSerializer`:

- **net48 (the application):** `JavaScriptSerializer` itself, with the same limits as before. Behaviour byte-identical by construction.
- **netstandard2.0:** no default. `CoreJson.Use` chooses the serializer; today only the tests do, with `JavaScriptCompatibleJson` (a reimplementation of the JavaScriptSerializer rules).

Evidence (net48, the compatible serializer against JavaScriptSerializer in the same process, texts compared after sorting object members):

| Check | Result |
|---|---|
| Every character U+0000–U+FFFF in a string | identical |
| Integers, decimals, enums, bool, null, char, Guid, DateTime, float | identical |
| Number literals read back (int / long / decimal / double rules) | identical |
| Project JSON: 36 fixtures, 36 approved round-trips, the DWG read-back, the example and the new project (read and write) | identical |
| Configuration files (presets, reviewed context, chassis evidence, 3D reference, equipment profile) | identical |
| Every calculation result, the shared design and the fingerprints of all 36 fixtures, computed with either serializer | identical |
| Bridge contracts: 36 generation requests, 4 generation responses, 4 STEP parameter files | identical |
| 199,955 random doubles | 157 normal doubles differ, 8 of 52 subnormals differ |

**What differs:** only how a double is written, and only in the choice between 15 and 17 significant digits for the same value. .NET Framework's "R" format picks 15 digits when its own (not always correctly rounded) conversion reads them back as the same double; `JavaScriptCompatibleJson` decides with exact arithmetic. Example: JavaScriptSerializer writes `3.6455822429999998` where the compatible serializer writes `3.645582243`. The test asserts that every difference is of this kind. Identity is therefore not proven for every double, so the netstandard2.0 build does not use the compatible serializer by default.

**Member order:** JavaScriptSerializer writes object members in reflection order, and that order is not stable even within one process (two serializations of the same object seconds apart can differ). Member order cannot be a byte criterion; all comparisons sort the members (as the golden captures do).

### net8.0 against the approved files

On net8.0, 17 of the 36 fixtures differ from the approved files, all in numbers only. The test accepts a file when every differing line is the same apart from its numbers, and all of them agree within 1e-12 relative; each accepted file is printed. Nothing else is tolerated, and net48 stays byte-exact. The two causes are outside the serializer:

1. `double.ToString()` without a format: .NET Framework writes 15 significant digits, .NET Core 3.0+ the shortest round-trip form. This affects the FF detail texts built by string concatenation, e.g. `1571.42857142857` vs `1571.4285714285713` (23 lines).
2. The last bit of some Math functions: `Sf8150` for `sweep-table6-q1000` is `20.263343160486862` on .NET Framework and `20.263343160486858` on .NET 8 (3 lines).

Making Core's number formatting explicit (for example `G15`) would remove cause 1 without changing the .NET Framework output; that is a separate decision.

## Tier 3: integration (`tests/integration`)

`tests/integration/run-tier3.ps1` runs, each into its own folder:
- `affected-regressions` (`run-affected-regressions.ps1`, `verify_affected.py`): rule, machine-room, reeving and project tests, the 200 calculation assertions, the four-view snapshots compared with the release Core of `bin-shaftmaster-v01` (1450 / 1600, 1650 / 1800, 1850 / 2000), and the 32 protected files (original drawings, templates, v01 files) unchanged. Needs the local `output/calculation-phase1` evidence.
- `ff-integration`: `CarGuideForceIntegrationTests` through `run-native-test.ps1` (FF native generate / reopen and STEP parameters).
- `step-input`: `StepExportTests` (STEP input and parameters; no SOLIDWORKS session).
- `native-golden`: `NativeGoldenTests` generates the golden fixture `new-project-q630` (presets applied) as all four native views through the AutoCAD host and compares `tests/golden/approved-native/new-project-q630/entity-counts.txt` (generated and reopened entity counts per view) and `reopened-project.elevator.json` (project JSON read back from the saved DWG, member order normalised). `-ApproveNative` creates the initial baseline only.

`tools/test.ps1 -Tier 3` adds `presets-native` (`PresetsNativeTests`) and `sheet-word-render`: the calculation-sheet suite with `-WordRender`, which renders every sheet through Word to PDF, rasterises the pages and checks the pagination. The Word render is off by default and runs in tier 3 only.

## Basic UI flake (fixed 2026-10-09)

**Symptom.** `BasicUiTests` occasionally ended inside a full suite run with exit code 1, no `basic-ui-tests.json`, no `ui-error.json` / `ui-crash.json` and no Application-log event, at a different point each time (before the first screenshot in `output/presets-heavy-20261009/final`; after screenshot `02b` in `output/presets-20261008/final2`). It always passed when run alone.

**Root cause.** The test window is an ordinary top-level window that is activated (takes the keyboard focus) when it opens, and the test runs it in smoke mode, which skips the unsaved-changes prompt on close. Any close request (the title-bar X, Alt+F4, the taskbar, a `WM_CLOSE`), for example input meant for another window on a desktop in use, closes it at once. `app.Run` then returns while the test is still awaiting inside its `Loaded` handler, so neither the result nor an error is written and `Main` returns 1. Reproduced exactly by posting `WM_CLOSE` to the running test window (lifecycle trace: `Window.WmClose` → Closed → `app.Run` returned, exit 1; the same files and messages as the failures). Without an external close, 15 runs with the in-suite predecessor (calculation UI) all passed.

**Fix (test harness, no product change).** The test owns its window: a close request it did not make is refused and logged (`ui-trace.log`, with the call stack), the window opens without activation (`ShowActivated = false`), and only the test's own `finally` closes it after writing the result. With the fix, the same injected `WM_CLOSE` is refused and the run passes.

**Proof.** 20 consecutive runs after the fix: 20 PASS (22–23 s each). Log: [docs/evidence/basic-ui-flake-proof-20261009.txt](evidence/basic-ui-flake-proof-20261009.txt).
