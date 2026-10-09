# ShaftMaster UI delivery — 5 October 2026

The existing WPF/.NET Framework 4.8 application now uses ShaftMaster branding and a responsive shell. Core calculations, project models, floor/stop behavior, AutoCAD updaters, STEP geometry and transport/export services were retained.

## Current release (v03, 2026-10-09)

Run `ElevatorAutomation/bin-shaftmaster-v03/ShaftMaster.exe` (SHA-256 `9CFD5FAB73A444FB4FBF639A164A9C904DA2B51ADB2EB90163DAB74FB7918342`).

- v03 is an unchanged copy of the presets candidate that passed `output/presets-heavy-20261009/final2`.
- What changed since v02 is in `bin-shaftmaster-v03/RELEASE-NOTES.md`, and the file checksums in `bin-shaftmaster-v03/SHA256SUMS.txt`.
- Earlier releases are kept unchanged:
  - v02 (`bin-shaftmaster-v02`, `57FBE447…`): its notes are in `bin-shaftmaster-v02/RELEASE-NOTES.md` and its release check in `output/release-check-20261008/RELEASE-CHECK.md`.
  - v01 (`bin-shaftmaster-v01`): the section below describes the v01 UI delivery of 5 October 2026.

## Application and recovery (v01, previous release)

v01: `ElevatorAutomation/bin-shaftmaster-v01/ShaftMaster.exe`.

Exact delivered EXE:
`D:\CAD DATA---------------------\DWGs\2026\dwg plan codex\ElevatorAutomation\bin-shaftmaster-v01\ShaftMaster.exe`

Keep its companion runtime files together. DWG generation still needs the existing sibling `Templates` directory and AutoCAD 2023. STEP export still needs installed, licensed SOLIDWORKS and an English-named default part template; this installation was verified with SOLIDWORKS 2022. No additional software dependency was introduced.

The original `bin-solution-v01/Elevator Solution.exe` was not replaced. Its SHA256 remains `CE59D9941993AF070E2DBDF8BF15F7DF88981F581205B73EAC510DC320AB7203`.

A recovery copy made before editing is in `output/shaftmaster-ui/baseline`, including the desktop, Core, Bridge, Host and STEP sources, solution/build configuration, and original complete application folder. Copy that baseline back only if intentionally reverting; it is not the folder to launch for the new interface.

Delivered EXE SHA256: `DC8A76A3C408E4525686B4A30716D5651018D0F49B403221CB35FECE35E72AE7`.

## Presentation changes

- Native title bar retained, with title **ShaftMaster — Elevator Design Solutions**. Startup and restored bounds are fitted to the available monitor work area. The old 900-pixel starting height and 760-pixel minimum height could exceed a smaller work area; the new minimum is normally 720 × 420 logical pixels and is capped for smaller work areas.
- DPI-aware manifest; scrollable navigation/content, wrapping action groups, and stacked labels/fields at narrower widths. No oversized minimum is used to conceal layout problems.
- Compact navy/blue navigation retains the original nine pages and adds About. System requirements and the full approved logo are in About.
- Project actions and workflow actions are grouped separately. Generate Drawings remains the primary action; current save, validation, generation and export handlers are retained.
- Consistent Segoe UI fields, shorter numeric inputs, requirement hints without repetitive editable captions, friendly Geared Traction/1:1 display labels, and project-ID details with a copy action. Underlying keys and serialized values are unchanged.
- Live project name and separate dirty indicator. A concise footer retains the full message in Details/tooltips. Output details and Open Output Folder refer to the actual successful output rather than the default DWG folder.
- Busy operations lock project/navigation inputs and duplicate action submissions while retaining the native window controls and a busy indicator. Enabled state is restored after completion or failure.
- The original approved logo is embedded byte-for-byte, rendered proportionally. A simplified symbol is bundled as a PNG for the window and a seven-resolution ICO for the EXE. Assets do not depend on development paths.

UI shell code is separated into `Chrome.cs`, `WindowLayout.cs` and `BrandAssets.cs`; the existing input/controller logic remains in `App.cs`. Packaging changes are limited to the new output folder/name, embedded assets, icon and manifest. `tools/BrandIconBuilder.cs` deterministically produces the icon from the bundled SVG using WPF; it is a build utility, not a runtime dependency.

## Completed checks

| Check | Result / evidence |
|---|---|
| Existing design-rule suite | PASS: 43 sizing assertions, 6 annotation checks, 39 machine-room checks and 102 reeving/beam checks |
| STEP input/dependency/output regressions | All 7 existing cases passed |
| Compiled packaged WPF application | 500 UI/layout assertions passed in `output/shaftmaster-ui/layout-tests/ui-regression.json` |
| All navigation pages | Original nine plus About instantiated/rendered; all editors committed without changing project values |
| Existing project compatibility | Save/reopen retained every serialized value, including floor identifiers, intervals and Served flags; JSON object-key order was excluded from comparison |
| Native project actions | Open, Save As and Save used a disposable project copy; New Project was checked; declining the unsaved-change prompt retained the edit |
| Validation / invalid inputs | Representative input retained independent PASS/PASS/FAIL engineering statuses; empty-project generation displayed blocking feedback and remained usable |
| Busy and failure behavior | Actual STEP export disabled duplicate actions; actual Maximize/Restore remained available. Injected missing-template background failure restored controls and published no DWG |
| Native window controls | Initial title-bar buttons visible; Maximize, Restore, Minimize/reactivation and Close checked on the application; operation-details window closed using its native control |
| Output/details actions | Actual output folder opened in File Explorer; operation Details retained the full STEP destination; About/logo verified in the packaged EXE; Copy identifier completed successfully |
| Branding / original code | Approved logo embedding matched original bytes; Core, Bridge, Host and STEP source files matched the recovery baseline; original EXE hash unchanged |

Actual desktop was 1920 × 1080 at **125%** scaling, with WPF work area 1536 × 816 logical pixels. The WPF test resized the real compiled application window through logical viewports representing **1366 × 768** and **1920 × 1080** at **100%, 125%, 150%**, reserving taskbar space, and checked toolbar bounds and scrollable page access on every page. It also checked the 720 × 420 minimum and off-screen/negative-monitor-origin fitting. These viewport tests are **simulation of screen/scaling combinations**, not changes to Windows display settings or tests of physical monitor transitions.

Screenshots:

- Actual packaged window: `output/shaftmaster-ui/screenshots/General-native.jpg`, `About-native.jpg`, `Shaft-native.jpg`.
- Crisp WPF content captures of all ten pages: `output/shaftmaster-ui/layout-tests/*.png`.
- Simulated scaling/size examples: `GENERAL-1366-100.png`, `GENERAL-1366-125.png`, `GENERAL-1366-150.png`, the corresponding 1920 variants, and `FLOORS-MINIMUM.png`. These capture content; native title bars are in the native screenshots.

## Native DWG and STEP results

The packaged desktop generation handler produced and reopened two coordinated four-view DWGs:

- `output/solution-v01/ui-e2e-20261005-112721/AllFour_1450_1600.dwg`
- `output/solution-v01/ui-e2e-20261005-112721/Unequal_NO_STOP.dwg`

The native host's independent reopen/audit passed for both. Reference entity counts are Plan 521, Machine Room Plan 1354, A-A 3043, B-B 709. The unequal/no-stop case has A-A 3040; the other view counts remain unchanged. Compared with the earlier accepted desktop reference run `ui-e2e-20260927-075128`, reference view bounds, entity counts and all three engineering statuses are identical. See `output/shaftmaster-ui/dwg-baseline-comparison.json` and the native result files. This is a parameter/audit/bounds comparison, not a binary DWG-file comparison.

An additional test using the actual toolbar in the final delivered EXE generated `output/shaftmaster-ui/ShaftMaster_UI_FourViews.dwg` in 6.9 seconds, including fresh-process native verification. All four entity counts and independent statuses match the reference. The scrollable result area exposed **Open Drawing**, which opened that exact file in interactive AutoCAD 2023; its returned window title was `Autodesk AutoCAD 2023 - [ShaftMaster_UI_FourViews.dwg]`. See `output/shaftmaster-ui/native-toolbar-result.json`.

The first restricted test could not initialize AutoCAD's user profile and failed before loading the plugin. Repeating with the normal user-profile access passed; no application code workaround or original drawing modification was made.

Actual **Export STEP** from the delivered packaged EXE produced:

`output/shaftmaster-ui/ShaftMaster_Representative.step`

Editable source:
`output/shaftmaster-ui/ShaftMaster_Representative_source_ce295b79/Elevator_Assembly.SLDPRT`

Input was the existing representative 450-kg, SW 1450 / SD 1600 project with five levels at 3000-mm intervals. The export took 240.7 seconds. SOLIDWORKS reopened STEP AP214: **67 source/reopened solids, mm units, zero solid faults, zero positive-volume intersections**, matching total volumes. Maximum source-to-reopened bounds difference was `2.18278728425503E-11 mm`.

Against the earlier verified `UI_Representative_source_5b605f7f`, all 67 component names and per-body bounds are identical. Maximum volume difference is floating-point noise of `2.384185791015625E-07 mm³`. Model parameters are identical. See `output/shaftmaster-ui/step-baseline-comparison.json` and the new source folder's `verification.json`, `Parameters.json` and `export.log`.

Layout PASS, wrap PASS and D/d FAIL remain independent and visible. No code/component warning was suppressed to obtain generation success.

## Existing limits and checks not performed

- The existing command intentionally defers closing the main window during an active operation. Minimize/Maximize/Restore remain available; this task did not introduce safe cancellation. The STEP service's existing worker wait has no timeout. A stalled CAD/COM process can therefore keep the operation active until it exits; that is a separate operation-lifecycle improvement, not a geometry/UI regression fixed here.
- Physical 100%/150% Windows scaling, moving between monitors with different DPI, unplugging/reconnecting monitors and restoring a saved position on another real monitor were not tested. The old application did not persist window position; current native restore/work-area fitting was added, with off-screen mathematics verified.
- Floor insertion rules, required preceding intervals, final-datum restrictions, precision formatting and engineering validation messages remain as before. The earlier user confusion around floor insertion was not redesigned in this UI task.
- Existing manufacturer/code gaps and unsupported-machine restrictions remain. No mechanical/code certification is implied by valid DWG/STEP output.
- Copy-ID was invoked successfully, but a separate read-back of clipboard contents was not performed. Full geometry verification uses AutoCAD Core Console's reopen/audit; the interactive AutoCAD check confirms the Open Drawing action opens the intended generated file.
- No claim of being error-free, full hardware/display coverage or manufacturing adequacy is made.

## Workflow

1. Open `bin-shaftmaster-v01/ShaftMaster.exe`.
2. Open an existing `.elevator.json`, or choose New Project and enter the same project inputs as before. Use General → Project details when the identifier is needed.
3. Save or Save As. The project name and dirty indicator show the current state.
4. Validate, then review the separate engineering statuses.
5. Use Drawings to choose views/output conventions; choose Generate Drawings for the coordinated DWG.
6. Choose Export STEP and select the `.step` destination for the current inputs. After completion, use Open Output Folder or Details. The editable native model and reports are in the sibling source folder.

Rebuild with `build-solution.ps1`; its default output is the separate `bin-shaftmaster-v01` folder. Run `tests/shaftmaster/run-ui-tests.ps1` for the packaged WPF regression matrix. Existing `--smoke` generation diagnostics remain available; AutoCAD needs access to its normal user profile.
