# Shared session handoff

This file is the shared status channel between the user's Planner chat and Executor chat. Chat transcripts remain separate; each session must read this file and the current workspace. Keep the status factual and concise.

## Long-term calculation goal

- The user's goal is to implement the remaining applicable calculations in `Elevator_Calculations_GEARBOX_Code_Corrected_Dual_Units.docx`, in stages. FF1/FF2 was one milestone, not the endpoint.
- After the current FF candidate review, inventory the exact note sections and compare them with the current calculation code/docs to find the next unfinished, sufficiently defined calculation.
- Preserve the existing release and FF candidate separately while progressing. The current release-target decision applies to promotion of FF1/FF2; it does not cancel the longer calculation goal.
- The FF1/FF2 axis, guide-count and shoe-contact questions were resolved by a user-approved design interpretation on 2026-10-07; see `ElevatorAutomation/docs/CAR-GUIDE-FORCES.md`. It applies to FF1/FF2 only. Do not extend it to unrelated calculations without a separate decision.
- Track the note inventory and dependencies in `CALCULATION_ROADMAP.md`. Do not guess the remaining scope from memory.

## Current task

- **Status:** Complete, awaiting Planner (2026-10-09): ARCHITECTURE-PLAN P7 "Test consolidation" on branch `refactor/p7-tests` (from `main` `780e1a5`, the P6 merge). Not merged, not pushed.
- **Goal:** P7: xUnit as the single home of the Core / Reporting tier-1 tests, tier 1 about 3 minutes, one test command (`tools/test.ps1`). No behaviour change.
- **Last updated:** 2026-10-09 (Claude Code, Executor role; P6 merge, then P7)
- **Completed:**
  - P6 merged into `main` (`780e1a5`, --no-ff) after a passing pre-merge tier 1 (`20261009-215108`, 602 s). Not pushed.
  - ARCHITECTURE-PLAN P7 "Test consolidation" (2026-10-09), branch `refactor/p7-tests`. Commits (oldest first): `ca0c11d` P7: thread-safe multi-entry rope selection cache, scoped JSON serializer override, parallel xUnit test classes; `9168004` P7: Presets / rope / FF / safety-pit console suites become xUnit tests; geometry cache; warm per-fixture caches; Table 6 grid computed once; `d26c2fc` P7: tools/test.ps1 with -Suite / -List, parallel tier 1, summary table; retire the duplicated console runners; `58db77d` P7: converted suites flush their check counts per class; `6a5bea4` P7: project JSON compared independent of member order (JavaScriptSerializer order is not stable): the real cause of the flaky net48 tests; `5d88082` P7: TESTING.md, CI runs tools/test.ps1 -Tier 1.
    - **Tier 1 time:** before 602 s (xunit-core 309 s of it; presets 106 s and golden 78 s serial) -> after 160 s (+10 s xUnit build) in `20261009-234346`, all six tier-1 suites in parallel processes. Tier 2 208 s, tier 3 140 s.
    - **Duplication removed:** the console runners `run-presets-core.ps1`, `run-rope-traction-core.ps1`, `run-car-guide-force-core.ps1` and `tests/Elevator.Core.Tests/run-xunit.ps1`, and `ConsoleSuiteTests.cs` (the wrapper) are deleted; the suites were moved (git mv) from `tests/solution` to `tests/Elevator.Core.Tests/Suites` and are real xUnit classes with `[Fact]` per section. `run-safety-pit-ui.ps1` no longer runs the Core part (it still generates the schema and runs the UI part).
    - **Old -> new mapping (checks):** presets-core 160 -> `PresetsTests` 125 (`Loader_ladder_rules_lock_follow_and_selection`) + 35 (`Q_sweep_survey_and_Table_6_shafts`); rope-core 60 -> `RopeTractionTests` 17 + 35 + 4 + 2 + 2 (five facts) and the Python hand check (10 fixtures, same `Export` calls) in `core-*`; ff-core 131 -> `CarGuideForceTests` 131; safety-pit-core 117 -> `SafetyPitTests` 117 (tier-2 `safety-pit-ui` therefore shows 35 instead of 152: its 117 Core assertions moved, none dropped). Each fact asserts its check count; the total 468 is in `checks-<fw>.json` (net8.0 and net48). xunit-core's 240 earlier "tests" (180 net48 + 60 net8.0) are now 185 + 65 tests (+5 new facts, -4 wrappers per framework... the wrappers became 9 facts) plus the 468 checks per framework. golden (566), sheet-wpf (former sheet-core, 8285), reporting xUnit (145 x 2) unchanged.
    - **Kept as console programs (they need WPF):** `golden` (figures SVG, omitted calculations, figure errors; it also repeats the Core captures that `GoldenCoreTests` compares, left as a double check) and `sheet-wpf` (CalculationSheetTests, 8285 assertions, also run with Word at tier 3). Tier 2 and 3 unchanged.
    - **Speed (Core, behaviour unchanged):** bounded, locked multi-entry caches for `ProjectPresets.SizeRopes` and `RopeTractionCalculationEngine.Geometry` (the geometry is a full design-engine run, ~120 ms on net48; key = project fingerprint + same context object; geometry is returned as a copy); `PresetLibrary.Hash` cached per library; `CoreJson.UseScoped` (AsyncLocal) for the per-test serializer. Tests: classes run in parallel (the assembly-wide `DisableTestParallelization` is removed), per-fixture captures / renders / parity results are computed in parallel on first use (`WarmCache`), the 14 Q-sweep cases and the Table 6 shaft grid run in parallel, a shared `DesignContext` per test process.
    - **Flaky test:** the reported failure (`Rope_traction_core` on net48) and two more seen while proving it (`Save_and_open_clear_the_unsaved_state...`, rope `Presets...`) were string comparisons of project JSON: JavaScriptSerializer writes object members in an unstable order, also within one process. They now compare through `JsonMembers.Sort`; the parity test uses `CoreJson.UseScoped` instead of the static `Override`. Proof: 10 consecutive runs of `core-net8.0`, `core-net48` and `reporting-net48` in parallel, all PASS; the same 10 runs before the member-order fix gave 3 failures (runs 7-9).
    - **tools/test.ps1:** `-Tier`, `-Suite` (any names), `-List`, `-Framework`, `-Serial`; tier 1 as parallel child processes; summary table + `summary.json` / `summary.md`; on Linux only the net8.0 xUnit suites. CI (`.github/workflows/ci.yml`) runs `tools/test.ps1 -Tier 1` on ubuntu; the windows job still only builds. `docs/TESTING.md` and four docs that named the deleted runners are updated.
    - **Release hashes unchanged:** ShaftMaster.exe v01 DC8A76A3, v02 57FBE447, v03 9CFD5FAB. Golden approved files unchanged (0 changes). No release folder, CAD template, DWG, DESIGN-FLOW.md or ARCHITECTURE-PLAN.md touched.
  - ARCHITECTURE-PLAN P5 merge (2026-10-09): tier 1 PASS on `refactor/p5-core` (2077 s on a temporarily slow machine), merged into `main` with --no-ff as `08a4382`. Not pushed.
  - ARCHITECTURE-PLAN P6 "Adapters clean-up" (2026-10-09), branch `refactor/p6-adapters`:
    - **Commits (oldest first):** format AutoCAD, then Host / Bridge / StepExport (assemblies byte-identical); `3aadb91` AutoCAD by output + one type per file in AutoCAD and Bridge; Bridge contract version; then method splits (SectionAADrawing Rebuild / PrepareLibrary / Audit, section B-B audit and updater, MachineRoomUpdater, Commands.Run, ProjectDrawingService, MachineRoomDependencies.Prepare, MachineRoomGeometryAudit, SectionAACommands.Edit, DrawingReader, VerticalSectionGeometryAudit, StepExport BuildModel.Geometry / Main, HostEntry.Execute, StepExportClient.Run); XML docs; unused usings and one unused field; plus this handoff commit.
    - **Formatting:** the P5 standard (`.editorconfig`, `tools/fmt` style: braces on new lines, one member per line, 140 columns, one type per file, array / anonymous-object initializers one item per line). The formatting commits left Elevator.AutoCAD.dll, Host, Bridge and StepExport.exe byte-identical (hashes compared against a build of the previous commit).
    - **New Elevator.AutoCAD tree (namespace `Elevator.AutoCAD.<Folder>`, assembly name and `[CommandMethod]` names unchanged, so NETLOAD paths and command names are unchanged):** `Plan/` (15: PlanCommands, PlanUpdater, DrawingReader, DrawingSnapshot, ShaftFrame, PlanHatches, PlanCoverage, PlanGeometryAdapter, ComponentBinding / Binding, LegacyMigration), `MachineRoom/` (14: commands, drawing, seed / manifest / snapshot / binding, dependencies, updater, geometry audit, chassis reader), `SectionAA/` (6), `SectionBB/` (9: machine-room section commands, drawing, updater, audit), `Vertical/` (5: experimental vertical section), `Common/` (12: Commands, Configuration, DrawingUpdater, ProjectDataStorage, UnifiedProjectStorage, ProjectDrawingService, IViewUpdater, ViewUpdateResult, PlanAnnotations, project commands). The old `Commands/`, `Drawing/`, `Project/`, `Views/` folders are gone.
    - **Bridge:** one file per contract (`GenerationRequest`, `GenerationResponse`, `OutputConsent`, `Wire`, `StepExportClient`, `StepExportResult`, `StepVerification`, ...). New `BridgeContract.Version = 1` and `BridgeContract.Require(int)`; the request / response default `ProtocolVersion` is that constant, `Wire.Validate` and `GenerationClient` compare with it, and `HostEntry.Execute` calls `BridgeContract.Require(request.ProtocolVersion)` first (same exception text as before). Wire format unchanged: the field value is still 1; xUnit `Bridge_contracts_are_the_same`, the golden suite and the native tier-3 tests pass.
    - **Statistics (files / lines / lines over 140 characters / longest line), before -> after:** Elevator.AutoCAD 33 / 2 441 / 441 / 1 488 -> 61 / 6 656 / 0 / 140; Host 1 / 142 / 17 / 259 -> 1 / 274 / 0 / 139; Bridge 5 / 292 / 43 / 568 -> 11 / 736 / 0 / 139; StepExport 1 / 135 / 31 / 421 -> 2 / 595 / 0 / 140 (BodyRecord split out). Methods of 100+ lines 13 -> 1 (longest was 327 lines, `SectionAADrawing.Rebuild`; now `BuildModel.Main` 100); of 60+ lines about 28 -> 17.
    - **Dead code / docs:** analyzer scan (IDE0005, IDE0051, IDE0052, IDE0059, IDE0060) of the four projects: 34 unnecessary usings removed, the unused `BuildModel.kernel` field removed (the `GetModeler()` call stays). Left on purpose: unused `db` parameters of two public methods and an unused initial value in `SectionAADrawing.OffsetByRole`. Every public type, method and constant has a one-line XML summary.
    - **Not changed:** assembly names, NETLOAD paths, command names, the StepExport worker interface. The split of `BuildModel.Geometry` / `Main` is data-flow mechanical and compile-checked, but not run against SOLIDWORKS (not part of any tier).
    - **Golden:** approved files unchanged (0 changes against `main`). Release hashes unchanged: ShaftMaster.exe v01 DC8A76A3, v02 57FBE447, v03 9CFD5FAB.
  - ARCHITECTURE-PLAN P5 "Core clean-up" (2026-10-09), branch `refactor/p5-core`:
    - **Commits (oldest first):** `8f87744` .editorconfig; `702dd39` / `93b2653` format Core / Reporting; `3e7a74f` one member per line, blank lines; `cb6c749` regroup by component + one type per file; `5423097` auto-properties on one line; `d7ca541` braces on multi-line embedded statements; `a41dc19`, `4e73c53`, `39d8358`, `d13bbdf`, `fdb06ae`, `a5846df`, `deb2fc2`, `6a3cbed`, `2993ebf` long methods split; `23dff3e`, `9c0644b` initializer / block formatting; `b4d5d0c` short type names; `812639c` XML docs; `0113e97` dead code; `c04723b` docs paths; `46d843c` tier-3 probe define; plus this handoff commit.
    - **Formatting:** `ElevatorAutomation/.editorconfig` (4 spaces, 140 columns, braces on new lines, naming rules as suggestions). Formatting commits are marked "assemblies byte-identical": Elevator.Core.dll / Elevator.Reporting.dll (net48 and netstandard2.0) hashed equal before and after. Auto-properties `{ get; set; }` and empty `catch { }` stay on one line. `dotnet format whitespace` is stable on both projects.
    - **New Core folder tree (namespace = `Elevator.Core.<Folder>`; assembly and type names unchanged; the old `Elevator.Core.Vertical` and `.SectionAA` namespaces and the root-namespace types are gone):** `Application` (6 files), `Calculations` (7: fingerprint, issue, modes, ChassisCalculationInputs, Simplified*), `Car` (3), `Counterweight` (2), `Doors` (4), `Geometry` (17: LocalPoint, vertical-section data and calculators, A-A, layout), `GuideRails` (11: rail config/rules, brackets, FF1/FF2), `MachineRoom` (37: machine, room, chassis, B-B section, beam, rules), `Presets` (17: library, fields, catalogs, rope sizing, EquipmentProfile), `Project` (29: ElevatorProject and its groups, JSON, floors, validator, design engine, legacy schema), `SafetyGear` (5: P5-P7 engine/inputs/results, governor offset), `Serialization` (4), `Shaft` (5), `Suspension` (22: ropes, reeving, traction, grooves). Reporting keeps its four folders, one type per file.
    - **Buffers:** no folder. The project has no buffer-specific type: P6/P7 are part of the P5-P7 engine (`SafetyGear`) and the buffer catalog is `Presets/BufferPreset`. Create `Buffers` when a buffer type exists, or split `SafetyPitCalculationEngine` (a logic change, not done here).
    - **Long methods:** methods of 60+ lines 42 -> 21, of 100+ lines 22 -> 0, longest 260 -> 88 lines (`ProjectPresets.SizeRopes`). Splits are mechanical (Roslyn data-flow extraction) or hand-written helpers (nested `InputChecks`, `IssueList`, `MassInputs`, `RopeSearch`). Evaluation order and texts kept.
    - **Statistics (Elevator.Core + Elevator.Reporting):** before 59 files, 5 948 lines, 968 lines over 140 characters (Core 649, Reporting 319), longest line 934, 111 lines over 300. After 196 files (170 + 26), about 17 170 lines, 0 over 140, longest 140.
    - **Dead code:** scan of all `.cs` files by identifier use found no unreferenced type, method or constant. Removed `DocxDocument.Field` and one unused local (`cuts` in the Suspension figure). 25 public data fields / properties with no reader remain on purpose (project JSON and contracts; removing them changes serialization). Unnecessary `using`s removed in 151 files. Analyzer IDE0060 on the uniform `Generate(g, d, s)` generator signature left.
    - **XML docs:** one-line `<summary>` on every public type, method and constant of Core and Reporting (existing `//` blocks above them were converted, the rest written). Public fields and properties of data classes are not documented.
    - **Other edits outside Core/Reporting:** usings and fully qualified names in AutoCAD, Desktop, Host, Bridge, StepExport and tests; `tests/calculations/DrawingRegressionProbe.cs` and `run-affected-regressions.ps1` (the release-baseline probe compiles against the old v01 Core with `/define:RELEASE_BASELINE`); `tests/vertical/summarize-vertical.ps1` file list; four docs with file paths. No release folder, template, DWG, DESIGN-FLOW.md or ARCHITECTURE-PLAN.md touched. Golden approved files: 0 changes (only `GoldenTests.cs` usings).
    - **Release hashes unchanged:** ShaftMaster.exe v01 DC8A76A3, v02 57FBE447, v03 9CFD5FAB.
  - ARCHITECTURE-PLAN P4 "Desktop structure" and clean-up (2026-10-09), branch `refactor/p4-desktop` from `main` `de64a1f`:
    - **Commits:** `01e03cb` Core.Application services (+ xUnit); `ce97165` runner fix (one-file source lists); `882639d` MainWindow split, test facade, UI tests; `5cc290b` Floors view-model commands; `aff57a1` unused code; `04bf06b` obsolete scripts; `b41f16f` superseded documents; `e2b4f0e` tracked AutoCAD-suite outputs, phase-1 copy fix; `ab43266` docs; plus this handoff commit.
    - **Services moved to Core (`Elevator.Core/Application/`, namespace `Elevator.Core.Application`, netstandard2.0 + net48):**
      - `ProjectSession` (and `FloorRow`), moved from Desktop; `ProjectSession.NewProject` (the former `MainWindow.NewProject`): new / open / save / dirty state, floor moves.
      - `CalculationWorkspace`, moved from Elevator.Reporting (the live calculation needs it; Reporting depends on Core).
      - `PresetFlow` (new, logic of the former PresetFlow.cs partial): Apply (lock / follow), Edited (lock + unsaved), Reset, CanFillBlanks / FillBlanks / EnsureLibrary, PersonsFor, preset context.
      - `LiveCalculation` (new, logic of the former live-calculation, chassis / safety-pit / FF draft code): current results (chassis, rope / traction, safety / pit, FF, validation design), typed text of invalid entries, `InputEffect EffectOf(path)` (what an input change makes outdated), staleness checks, resets, `Recalculate` (only outdated results). The WPF timer, rendering and dialogs stay in Desktop and call these services.
    - **New Desktop structure (`Elevator.Solution.Desktop/`, UI still built in code, no XAML, no MVVM package):**
      - `Shell/`: `Program`, `MainWindow` (navigation, toolbar, header, status bar, page switching, Commit), `LiveUpdates` (300 ms WPF timer around LiveCalculation, invalidate + render), `PresetEditing` (editors of the preset flow, Reset menu, Fill blanks), `ReportExport` (calculation sheet and Design Notes exports, omission dialog), `ProjectCommands` (new / open / save, validation, generation, STEP, --smoke), `TestAutomation` (internal test facade).
      - `Pages/<Page>/`: one view + one view-model each: `General` (+ `CompanySettingsWindow`), `Shaft`, `Floors`, `CarDoors`, `Machine`, `Suspension`, `Rails`, `Calculations` (`CalculationsPage`, `CalculationsViewModel`, `ChassisInputs`, `BasicPanel`, `AdvancedPanel`, `RopeTractionPanel`, `SafetyPitPanel`, `CarGuideForcePanel`, `CalculationPreview`), `Drawings`, `Validation`, `About`.
      - `Common/`: `ViewModelBase` / `PageViewModel`, `RelayCommand` (used by the Floors commands), `PageForm` (form of the visible page and the shared field builders), `PageView` / `ViewPart`, `DisplayText`, `Choice`, `DerivedGeometry`, `BrandAssets`, `WindowLayout`, `WpfFigureRasterizer`.
      - The 10-file `MainWindow` partial class is gone. Buttons keep Click handlers (the UI suites raise ClickEvent; a Command would not run then).
    - **UI tests:** the 8 tier-2 programs and `tests/shaftmaster/UiRegression.cs` use `window.Automation.Get / Set / Call` (same handle names as before) instead of private-field reflection; `InternalsVisibleTo` for the 9 programs. Two checks now catch the exception directly instead of `TargetInvocationException` (same condition).
    - **UI assertion counts before / after:** presets-ui 59 / 59, rope-ui 33 / 33, sheet-ui 63 / 63, calculation-ui 54 / 54, basic 67 / 67, safety-pit-ui 152 / 152, safety-pit-draft-ui 117 / 117, ff-ui 126 / 126.
    - **Screenshot comparison** (P3 run `20261009-105905` vs P4 run `20261009-115949`, pixel diff, `artifacts/test-results/20261009-115949/screenshot-comparison/`): 57 of 58 identical; `calculation-ui/11-drawings-generated.png` differs only in the generation time in the status bar (7.8 s vs 14.8 s, from AutoCAD).
    - **xUnit for the services:** `tests/Elevator.Core.Tests/ApplicationServicesTests.cs`, 19 tests on net8.0 and net48 (session save / open / dirty, floor edits, lock / follow, reset, fill blanks, persons, input effects, recalculation of only outdated results, invalid text, resets, nested-run guard).
    - **Removed (clean-up, all in git history):**
      - Unused code: `DocxDocument.ImageAndText`, `PageBreak` (Reporting); `UnifiedProjectStorage.ImportJson`, class `MachineRoomSectionAAUpdater` (AutoCAD; never referenced, updaters are listed explicitly); `PageViewModel.Title`.
      - Scripts: root `build.ps1`, `build-solution.ps1` (wrappers, no callers); `tests/solution/run-tests.ps1` + `SolutionTests.cs`, `run-visual-qa.ps1` + `SolutionVisualQA.cs` (v0.1 suites compiling into `bin-solution-v01`); `run-phase1-regressions.ps1` + its two `generated-run-*.ps1` copies; `tests/solution/collect-evidence.ps1`, `tests/project/collect-evidence.ps1`; `tests/calculations/run-native-readonly.ps1` + `NativeGeometryReadTests.cs` + `prepare_evidence.py` (one-off evidence).
      - Documents: `TEST_RESULTS.md`, `PLAN_COMPLETENESS_AUDIT.md`, `PLAN_UPDATE_RESULTS.md` (README now carries the plan-automation summary and remaining manual decisions), `docs/ELEVATOR_SOLUTION_V01.md` (superseded by SHAFTMASTER-UI-RESULTS), `docs/SAFETY-PIT-DRAFT-VALIDATION-FIX.md` (table merged into SAFETY-PIT-CALCULATIONS).
      - Untracked (kept on disk, ignored): `tests/generated/` (13 files) and `tests/*-results.json` (7), rewritten by `tests/run-tests.ps1` on every run.
      - Kept on purpose: `legacy/ElevatorPlanGenerator/output` (golden DWGs read by ArchitectureTests), `tests/section/verification-summary.json` (cited evidence), `tests/semantic-map.json`, `build-section-aa.ps1` / `README-section-aa.md` (working Section A-A build), the AutoCAD script suite.
      - Fixed on the way: `tests/project/run-phase1.ps1` copied the removed root `build.ps1` and missed `Elevator.Reporting` (needed since P3).
    - **Disk proposal (nothing deleted):** `ElevatorAutomation/artifacts/disk-cleanup-proposal.md` — keep v01–v03, cited evidence and tier-3 inputs; archive or delete 7 superseded candidates, old build folders, 12 superseded evidence folders, loose September outputs and P0–P3 working folders (about 1 GB); review `output/solution-v01/` (1.2 GB, also the app's default output folder).
    - **Golden:** approved files unchanged (0 changes against `main`; only `GoldenTests.cs` usings changed).
  - ARCHITECTURE-PLAN P3 "Reporting extraction" (2026-10-09), branch `refactor/p3-reporting` from `main` `b35d087`:
    - **Commits:** `60445b0` move the report code into Elevator.Reporting (and runners / tests); `731ebf9` Elevator.Reporting.Tests, tier 1 and CI; `7504b71` docs; plus this handoff commit.
    - **Moved (git mv; namespace changed, type names and logic kept):**
      - `Elevator.Solution.Desktop/CalculationSheet.cs`, `CalculationSheetRopes.cs` -> `Elevator.Reporting/Sheet/` (namespace `Elevator.Reporting.Sheet`; includes `CalculationSheetInputs`, `OmittedCalculation`);
      - `CompanyReportSettings.cs` -> `Elevator.Reporting/Sheet/` (data and storage; an unused `using System.Web.Script.Serialization` removed);
      - `CalculationWorkspace.cs` -> `Elevator.Reporting/Sheet/` (not in the Planner's list, but CalculationSheet builds P1-P4 through it; WPF-free);
      - `DesignNotes.cs` -> `Elevator.Reporting/DesignNotes/` (`Elevator.Reporting.DesignNotes`);
      - `DocxWriter.cs` (with `ReportClock`, `DocxDocument`, `DocxImage`) -> `Elevator.Reporting/Docx/` (`Elevator.Reporting.Docx`);
      - `FigureModel.cs`, `CalculationFigures.cs`, `CalculationFiguresRopes.cs`, `FigureRender.cs` -> `Elevator.Reporting/Figures/` (`Elevator.Reporting.Figures`).
    - **The only code change to break the WPF dependency:** `FigureRender` keeps the SVG renderer, `Image` and `Initialise` (unchanged order: Initialise runs before the first figure is built); its WPF text measurement, font choice and PNG rendering moved unchanged into `Elevator.Solution.Desktop/WpfFigureRasterizer.cs` behind the new interface `IFigureRasterizer` (Reporting). `MainWindow` registers it (`FigureRender.Rasterizer`); `CapRatio` and `Dash` became public for it.
    - **Stayed in Desktop and why:** `CalculationSheetExport.cs` (a MainWindow partial: save dialog, omission dialog, message boxes, window state; the export logic itself is `CalculationSheet.Build/Render/Save`, which moved), `CompanySettingsWindow.cs` (WPF window), `BrandAssets.cs` (WPF imaging), `WpfFigureRasterizer.cs` (WPF), `ProjectSession.cs` (project file session, not a report).
    - **Project:** `ElevatorAutomation/Elevator.Reporting/Elevator.Reporting.csproj` (netstandard2.0; net48). net48 uses System.IO.Packaging of .NET Framework (WindowsBase, as the DOCX writer did in Desktop); netstandard2.0 uses the System.IO.Packaging NuGet package. Output: `artifacts/bin/Elevator.Reporting.dll` next to ShaftMaster.exe (one new file in the application folder), netstandard build in `artifacts/netstandard2.0/Elevator.Reporting/`. Desktop references it.
    - **Note for the Planner:** the namespace `Elevator.Reporting.DesignNotes` has the same name as its class `DesignNotes`; inside any `Elevator.Reporting.*` namespace the name resolves to the namespace (the Reporting tests use the full name). Consider renaming the namespace later.
    - **Tests:** new `tests/Elevator.Reporting.Tests` (xUnit, net8.0 + net48), 145 tests on each: sheet and Design Notes text equal the approved golden text for the 36 fixtures (net48 byte-exact), forbidden strings absent, every figure an SVG part equal to the figure's SVG, page frame / header title block / footer, title block filled and blank, Design Notes without tables. The presets xUnit suite now runs on net8.0 too: Elevator.Core.Tests net8.0 41 (was 40), net48 161. Both in tools/test.ps1 tier 1 (`xunit-core`, `xunit-reporting`) and in CI (ubuntu net8.0; windows builds Core, Reporting and Bridge). Runners and test sources reference Elevator.Reporting (about 15 runners, 17 test sources); the golden and sheet test programs register the WPF rasterizer.
    - **Golden:** approved files unchanged (0 changed against `main`); the golden suite PASS after the move.
  - ARCHITECTURE-PLAN P2 "Portable Core" (2026-10-09), branch `refactor/p2-portable-core` from `main` `55dd2b4`. Details: `ElevatorAutomation/docs/TESTING.md` (xUnit section, serializer decision).
    - **Commits:** `d5b5e4c` Core multi-target + serializer abstraction; `268b709` remaining JavaScriptSerializer uses through CoreJson; `9aa0351` Elevator.Core.Tests (xUnit) in the solution and tier 1; `a502d13` CI workflow; `f315eee` TESTING.md; plus this handoff commit.
    - **Core:** `Elevator.Core` targets netstandard2.0 (output `artifacts/netstandard2.0/Elevator.Core/`) and net48 (`artifacts/bin`, unchanged). Nothing besides JavaScriptSerializer needed an abstraction. `tools/build.ps1` passes `BinDir` (not `OutDir`) so the netstandard output can go elsewhere.
    - **Serializer abstraction:** all JavaScriptSerializer uses: 17 places where one was created (Core 4, Bridge 3, Desktop 2, AutoCAD 7, StepExport 1; the plan counted 18) go through `Elevator.Core.Serialization.CoreJson` / `IJsonSerializer`. On .NET Framework it is JavaScriptSerializer itself with the same limit as each site had (Int32.MaxValue or the default), so the application behaviour is byte-identical by construction.
    - **Serializer decision:** `JavaScriptCompatibleJson` (new, in Core) reimplements the JavaScriptSerializer rules. Evidence (net48, same process, members sorted): identical for every character U+0000-U+FFFF, integers, decimals, enums, bool, null, char, Guid, DateTime, float, number-literal reading, all 36 fixtures + 36 approved round-trips + DWG read-back + example + new project (read and write), the configuration files, every calculation result, shared design and fingerprint of all 36 fixtures, and the Bridge contracts (36 requests, 4 responses, 4 STEP parameter files). Not identical: 157 of 199,955 random normal doubles (and 8 of 52 subnormals) are written with 15 instead of 17 significant digits or the reverse (same value; .NET Framework "R" decides with its own inexact conversion, e.g. `3.6455822429999998` vs `3.645582243`). Identity is therefore not proven for every double: the netstandard2.0 build has **no default serializer** (`CoreJson.Use` is required; only the tests set `JavaScriptCompatibleJson`).
    - **Member order:** JavaScriptSerializer's member order is not stable even within one process (two serializations of the same object seconds apart differed), so all comparisons sort members, as the golden captures already do.
    - **xUnit** (`ElevatorAutomation/tests/Elevator.Core.Tests`, `run-xunit.ps1`, tier-1 suite `xunit-core`): net8.0 40 tests PASS, net48 161 tests PASS. Golden Core captures (round-trip, results, fingerprints, new project) against the same approved files: net48 byte-exact for all 36 fixtures + new project; net8.0 byte-exact except 17 fixtures with numbers-only differences (accepted only if every differing line matches apart from its numbers, all within 1e-12 relative; each accepted file is printed). Causes, outside the serializer: (1) `double.ToString()` without a format (.NET Framework 15 digits, .NET Core shortest form) in the FF detail texts, 23 lines; (2) 1 ulp in `Sf8150` for `sweep-table6-q1000` (Math functions), 3 lines. Console suites hosted unchanged: rope 60, FF 131, safety / pit 117 on both; presets 160 on net48 only (needs the WPF calculation-sheet sources). Serializer parity tests (net48). Sheet / Design Notes / figure captures stay in the console golden suite.
    - **Planner decisions suggested:** make Core number formatting explicit (e.g. `G15`) to remove the net8.0 text differences without changing .NET Framework output; whether to port the .NET Framework "R" conversion exactly to make the compatible serializer the netstandard default.
    - **CI** (`.github/workflows/ci.yml`, not pushed): ubuntu-latest builds Core netstandard2.0 and runs the tests on net8.0; windows-latest builds Core and Bridge. The same commands pass locally on Windows (Core 1 s, tests 40/40 in 23 s, Core + Bridge 1 s). The YAML was checked by review (no YAML parser installed; none downloaded). Not run on Linux.
    - **presets-ui 400 s note (P1):** not reproduced (57 s in the final P2 run).
  - ARCHITECTURE-PLAN P1 "Build system" (2026-10-09), branch `refactor/p1-build` from `main` `ee898b6`:
    - **Projects:** the six projects (Core, AutoCAD, Bridge, Host, StepExport, Desktop) are Microsoft.NET.Sdk projects (net48, `Microsoft.NETFramework.ReferenceAssemblies` 1.0.3 from nuget.org), plus `tools/BrandIconBuilder` and the test programs the csc scripts built into the output (`tests/projects/`: RuleTests, MachineRoomRuleTests, ReevingTests, Elevator.Automation.Tests, StepExportTests). One `ElevatorAutomation.sln` (classic format).
    - **Settings** (`ElevatorAutomation/Directory.Build.props`): C# 5 (`LangVersion` 5, as csc v4.0), deterministic, warnings as before (no analyzers, not errors), no generated assembly info and no `TargetFrameworkAttribute` (the csc assemblies had none; it would change runtime compatibility switches), no pdb, no supportedRuntime / binding-redirect config, no 32-bit preference, all output in `artifacts/bin`, intermediates in `artifacts/obj/<project>`, project references not copied again (one shared folder).
    - **AutoCAD / SOLIDWORKS:** AcDbMgd / AcCoreMgd / AcMgd through `$(AutoCADDir)` (Private=false); SOLIDWORKS interop through `$(SolidWorksDir)`, copied next to the worker as before.
    - **Desktop:** WinExe, x64, UseWPF, `app.manifest`, icon and embedded brand resources as before: target `BrandAssets` copies `Assets/` to `<output>/brand-assets` and runs BrandIconBuilder (incremental). Desktop does not reference StepExport: a solution dependency builds the worker (and the icon tool) first into the same folder, and target `CheckStepExportWorker` fails a solution build if the worker is missing. Data and documents copied as before.
    - **tools/build.ps1:** `dotnet build ElevatorAutomation.sln -c Release` into `artifacts/bin` (-Bin), -AutoCADDir / -SolidWorksDir, the P0 -Release guard, then the pure rule tests and the STEP-input tests (as the csc scripts). `build.ps1` and `build-solution.ps1` are thin wrappers; `build-section-aa.ps1` builds through it. Runners, `tools/test.ps1`, `run-golden.ps1`, `run-tests.ps1`, the project / vertical scripts and the phase-1 isolated copies call `tools/build.ps1`. README, ElevatorAutomation/README.md, README-section-aa.md, SHAFTMASTER-UI-RESULTS.md and TESTING.md updated.
    - **Parity** (old csc build in `artifacts/bin-old` vs new build): the same 26 files. Data files, `brand-assets` (generated icon and mark included) and `ShaftMaster.exe.config` byte-identical. Every binary keeps its identity, version, PE kind (x64 for ShaftMaster, StepExport, StepExportTests; AnyCPU otherwise), runtime and referenced assemblies; ShaftMaster.exe has the same embedded resources, manifest and icon. The binaries differ as expected from a different compiler: Roslyn's compiler-generated types, and a `DebuggableAttribute(IgnoreSymbolStoreSequencePoints)` (Release keeps `Optimize=true`, so the JIT optimises as for the csc assemblies; `Optimize=false` would disable JIT optimisation).
    - **Build timings:** old csc build 8 s; new clean build 6.3 s (incl. restore, rule and STEP-input tests); no-op build 2.1 s (nothing recompiled); one-line Desktop change 2.4 s (Desktop recompiled, Core not).
    - **Note:** the first `dotnet` run on this machine (SDK first-run experience) created a local, untrusted ASP.NET Core HTTPS development certificate.
  - ARCHITECTURE-PLAN P0 "Safety net" (2026-10-09), branch `refactor/p0-safety-net` from `main` `1125152`. Details: `ElevatorAutomation/docs/TESTING.md`.
    - **Build hazard:** `build.ps1` and `build-solution.ps1` default to `ElevatorAutomation/artifacts/bin` (ignored) and refuse a `bin-shaftmaster-v*` folder unless `-Release` is given (tested with a non-existent `bin-shaftmaster-v99`: both refuse, nothing written). README / docs updated.
    - **Needed consequence (product code, behaviour unchanged for releases):** the desktop (`MainWindow`), `Elevator.AutoCAD.Configuration.Root` and three test programs took the parent of the binary folder as the root (config/, Templates/). For `artifacts/bin` that was wrong, so the root is now the nearest ancestor with `config/` and `Templates/`; for a release folder that is still the parent.
    - **Report clock:** `ReportClock.Now` (DocxWriter.cs) replaces `DateTime.Now` / `UtcNow` in the calculation sheet, Design Notes and the DOCX `Created` property; production still uses the local time.
    - **Golden master (tier 1):** `tests/golden/GoldenTests.cs`, runner `tools/run-golden.ps1` (`-Approve` only for an empty approved folder; `-MakeFixtures`). 36 fixtures: new project Q 630; Q sweep 320–1600 on the survey shaft and the Table 6 shafts of `presets-heavy-20261009`; 7 locked rope / groove / CW / sheave fixtures; Basic and Advanced complete; missing h, masses and pit, rope count; malformed text (draft errors) and malformed values; rope / traction OK and NOT OK; title block filled and blank; older v01 example and a project without the Report group. 37 capture sets in all: the 36 fixtures plus "new project from presets". Captures per fixture: results (selection n × d, Dt, β, q, CW; P1–P4 with rope tensions; P5–P7; FF; rope / traction), fingerprints, project JSON after load → save, sheet and Design Notes text, figure SVGs; plus "new project from presets". 566 approved files (`tests/golden/approved`), baseline from commit `aa4f5e8` (= behaviour of `main` `1125152`).
    - **Evidence check:** `tests/golden/check_evidence.py`: 237 checks PASS against the Q sweep of `output/presets-heavy-20261009/final2` (selection, n × d, Dt, β, q, CW, rope checks, traction cases, deflector, Design Notes), FF1 / FF2 2200 / 2100 N, P5 9800 N, P6 39200 N, P7 29600 / 14800 N, new project 4 × 11, β 100, q 0.5.
    - **Determinism:** two compare runs PASS with 566 identical captures. Normalisations (listed in TESTING.md): fixed report clock; `RopeSizing.Milliseconds` = 0; project JSON member order sorted (`tests/golden/JsonMembers.cs`); new-project GUIDs → `GUID-n`; result JSON keys sorted; DOCX compared as document / header / footer text only.
    - **Finding for P2:** `JavaScriptSerializer` writes object members in reflection order, which differs between processes: the same project saved twice can list the `Calculations` members in a different order (values identical). Golden captures normalise the order; product files today are not byte-stable between saves.
    - **Tier 3:** `tests/integration/run-tier3.ps1` wraps the affected regressions (ported from the evidence runners: rules, machine room, reeving, project, 200 calculation assertions, four-view snapshots equal to the v01 Core, 32 protected files), the FF native / STEP integration, the STEP-input tests and `NativeGoldenTests` (default new project natively: entity counts per view Plan 521, MachineRoomPlan 1354, SectionBB 709, SectionAA 3043, generated = reopened; reopened project JSON), approved in `tests/golden/approved-native` (2 files).
    - **Basic UI flake:** root cause: the test window is activated and runs in smoke mode (no unsaved-changes prompt), so an external close request (title-bar X, Alt+F4, taskbar, WM_CLOSE; e.g. input meant for another window on a desktop in use) closed it at once; `app.Run` returned while the test was awaiting, leaving no result, no error and no event-log entry. Reproduced exactly by posting WM_CLOSE (trace `Window.WmClose` → Closed → exit 1); without it 15 in-suite-like runs passed. Fix in the test only: close requests the test did not make are refused and logged (`ui-trace.log`), the window opens without activation. Proof: 20 consecutive runs PASS; the injected WM_CLOSE is refused and the run passes. Log: `ElevatorAutomation/docs/evidence/basic-ui-flake-proof-20261009.txt`. The other UI suites use the same window pattern (not changed; candidate for P7).
    - **Commits** (branch `refactor/p0-safety-net`, oldest first): `5b73b8b` build default artifacts/bin and release guard; `a0860a8` ReportClock; `4da9275` golden suite and fixtures; `3452318` source-root lookup for artifacts/bin; `aa4f5e8` golden normalisations and .gitattributes; `b235175` golden baseline (566 files); `b0acdaa` tools/test.ps1, tier-3 runner, TESTING.md; `fdc3d3a` Basic UI flake fix; `2c8b80b` native golden baseline; `8fb8b67` -Tier 1,2,3 parsing; `e0549d0` TESTING.md reference run and handoff.
    - **Test tiers:** `tools/test.ps1 -Tier 1|2|3` (or `1,2,3`) builds into `artifacts/bin` and runs the existing runners into `artifacts/test-results/<timestamp>/` with per-suite logs, assertion counts, `summary.json` / `summary.md` and the time per tier. `run-calculation-sheet.ps1 -WordRender` (tier 3 only) adds the Word render, raster and pagination checks.
  - Release v03 and repository hygiene (2026-10-09):
    - **v03:** pre-checks passed (candidate `9CFD5FAB…`; v03 did not exist). `bin-shaftmaster-presets-candidate/` copied unchanged (51 files, all hashes equal), plus `RELEASE-NOTES.md` and `SHA256SUMS.txt` (52 entries, 0 mismatches). `docs/SHAFTMASTER-UI-RESULTS.md` and `ElevatorAutomation/README.md` point to v03; v01 / v02 unchanged.
    - **Open point (v03 docs):** the candidate, and so v03, carries the deflector-policy versions of `PRESETS.md` and `ROPE-TRACTION-CALCULATIONS.md` (the build does not copy them; the updated versions are in `ElevatorAutomation/docs/`). The copy was kept unchanged as instructed.
    - **Remote:** `git ls-remote origin` returned nothing (empty), so the history was rewritten. Nothing was pushed.
    - **Removed from the repository** (index and history; kept on disk; added to `.gitignore`): `ElevatorAutomation/Standards/` (EN 81-20 PDF), `ElevatorAutomation/tests/machine-room/en-page-*.png` (5), `ElevatorAutomation/tests/machine-room/standard.txt` (full extracted text of BS EN 81-20, found by the scan; no test reads it), `review_internal/` (159 files), `.audit_independent/` (3), `.dwg-inspection/` (16), the 5 root `*.dwg`, `acad.err` (was not tracked).
    - **Legacy:** `ElevatorPlanGenerator/` → `legacy/ElevatorPlanGenerator/` (git mv). References fixed: `CLAUDE.md`, `ElevatorAutomation/README.md`, `tests/CadIntegrationTests.cs`, `tests/ArchitectureTests.cs`, `tests/run-tests.ps1`, `tests/project/run-phase1.ps1`, `tests/solution/generated-run-phase1.ps1`, `tests/section/summarize-results.ps1`, and the legacy `.scr` files, `CadIntegrationTests.cs` and `generate-bindings.ps1`. Not changed: `DESIGN-FLOW.md` (not to be edited), the "proposed layout" block in `DWG_STRUCTURE_REPORT.md` (historical proposal), and recorded evidence JSON (`tests/section/source-hashes.json`, `verification-summary.json`, legacy run records).
    - **Tests that read removed local files** now skip with a clear message when the file is absent: the machine-room native suite (`Machine room codex.dwg`; `MachineRoomTests.MRTESTPREPARE` and `run-tests.ps1`), the source-drawing hashes in `summarize-results.ps1`, the root-drawing copies in both phase-1 runners, legacy `ELEVATORCHECK` (`plan codex.dwg`) and legacy `generate-bindings.ps1` (`.dwg-inspection`). `tests/machine-room/audit.scr` (manual helper, not run by any script) still loads the DLL from the local `.dwg-inspection/`.
    - **Scan findings:** no secrets (passwords, tokens, keys). No other standard text or other companies' documents found in the kept files. Kept and reported: `Elevator_Calculations_GEARBOX_Code_Corrected_Dual_Units.docx` (the source calculation note; python-docx generated, last modified by "AbdAllah AbdAlfatah", no company branding; its origin is for the user to confirm). Byte-identical copies of two removed root drawings remain as tracked test fixtures (`tests/machine-room/audit-input.dwg`, `tests/section/audit-input.dwg` = `Machine room codex.dwg`; `tests/section/reference-copy.dwg` = `Vert MR and Reeving codex.dwg`), and `.dwg-inspection/inspection-copy.dwg` equals the legacy template and generated test inputs; these were left in place.
    - **New files:** root `README.md` (what ShaftMaster is, prerequisites, build / run, structure, pointer to DESIGN-FLOW.md) and `LICENSE` (proprietary, all rights reserved, owner "Abdullah Nassar": name to be confirmed by the user).
    - **History:** one commit on `main` (the old `4d86aab` is unreachable and pruned); `git rev-list --objects --all` reaches none of the removed paths. 440 tracked files, 40.0 MB, no tracked file > 5 MB (largest `tests/machine-room/structure.json` 2.04 MB). A bundle of the old history is kept locally in `ElevatorAutomation/output/git-history-backup-20261009/` (ignored; it contains the removed files).
    - **Fresh clone** (temp folder): ﻿`git clone` of the clean repository into `C:\Users\…\Temp\smclone2-20261009` (441 files). `build-solution.ps1 -BinDirectory bin-clone` PASS (STEP-input tests 7 and the rule / machine-room / reeving checks), then core tests all PASS: presets 160, rope 60 + 10 hand-check fixtures, calculation sheet 8285, FF 131, safety/pit 117, calculation workflow 25. Evidence: `ElevatorAutomation/output/repo-hygiene-20261009/clean-clone3/`. Two problems were found and fixed on the way: (1) a clone into the long scratchpad path failed on Windows path length (legacy test file names; the README now says to clone into a short folder; failed attempt `clean-clone/`); (2) five tests (calculation sheet, FF, safety/pit, Basic, Basic UI) read the synthetic fixture `SYNTHETIC.elevator.json` from the ignored `output/calculation-ui-20261006/delivery-ui/` folder: the identical file is now tracked as `ElevatorAutomation/tests/fixtures/SYNTHETIC.elevator.json` and the five tests read it there (one path each; failed run `clean-clone2/`). The clone build gives a different `ShaftMaster.exe` hash (`BA5B989C…`) because compiled output depends on the build path and time; v03 is the packaged candidate, not rebuilt.
  - Heavy-load selection (2026-10-09). Full description: `ElevatorAutomation/docs/PRESETS.md`.
    - **Joint selection** (`ProjectPresets.SizeRopes`): when the ladder ropes do not pass, the unlocked levers are searched together, in order of preference (least change first):
      - (a) the rope arrangement (current rules: d ≤ Dt/40, ladder d downwards, then the smallest n);
      - (b) β steps 95 → 100 → 105°;
      - (c) balance factor q steps 0.50 → 0.45 (`BalanceFactorSteps` in `config/presets.json`; CW = P + q·Q while the CW is unlocked; a stepped q is stored as `RopeTractionInputs.SelectedBalanceFactor`, not a user value);
      - (d) Dt steps 520 → 560 → 620 (invalid in the native B-B section, so skipped).
      - The first passing combination is taken. Locked values never change.
    - **Speed:** new engine overload `RopeTractionCalculationEngine.Calculate(p, ctx, lib, geometry)` reuses the geometry per (Dt, d). This is a refactor, not an equation change; a test shows equal results with and without reuse (Q 630, 1600). An all-empty `RopeTraction` group is normalised to null so the selection cache key stays stable.
    - **Capacity table:** car mass P 1000 → 1100, 1275 → 1350, 1600 → 1650 kg; other rows unchanged.
    - **No pass on 1:1:** one Design Notes recommendation: the limiting check, "2:1 roping is recommended for this rated load", and the minimum rope MBL at 7 ropes of the kept diameter.
    - **Live timer:** the tick is guarded (an exception sets the status bar instead of escaping); `BasicUiTests` logs unhandled exceptions to `ui-crash.json`. This was added after a Basic UI flake in `final` (no result file, no crash event; 5/5 PASS when run alone); `final2` passed with no crash file.
    - **Q sweep** (`output/presets-heavy-20261009/final2/presets-core/q-sweep.md`):
      - Survey shaft 1500×1800: 320 → 4×10; 480 → 5×8; 630 → 4×11 β 100; 800 → 6×11 β 105; 1000 → 6×12 β 100; 1275 → 7×12 β 95, q 0.45, CW 1923.75; all OK. 1600 → NoPass (Sf 13.66 < 16.21), recommendation MBL ≥ 75.1 kN at 7 × 12.
      - Table 6 shafts: 320 (1300×1400, 250 mm deflector, D/d 25 quotation) → 4×10; 480 → 5×8; 630 → 4×11 β 100; 800 → 6×11 β 105; 1000 → 7×12 β 105; all OK. 1275 (2200×2000) → NoPass (Sf), MBL ≥ 60.9 kN; 1600 (2200×2200) → NoPass, MBL ≥ 75.1 kN. Each NoPass has exactly one 2:1 recommendation.
      - New project Q 630 unchanged: 4 × 11, β 100, q 0.5.
    - **Open point:** the two updated docs (`docs/PRESETS.md`, `docs/ROPE-TRACTION-CALCULATIONS.md`) were not copied into the candidate folder; the candidate still has the deflector-policy versions of these two files.
  - Deflector policy, DESIGN-FLOW 11a (2026-10-08):
    - **Engine** (`RopeTractionCalculation.cs`): no `dd-deflector` check row. `RopeTractionResult.DpOverDr` holds the value; Kp = (Dt/Dp)^4 is unchanged. `RopeTractionRules.Apply` still records MR-ROPE-001 using the value. MachineRoomRules (drawing side) and MR-DS-001 are untouched.
    - **Rope search** (`ProjectPresets.SizeRopes`):
      - d ≤ Dt/40 only. Start at the ladder d for Q: the largest d ≤ the ladder d that passes, then the smallest n; a larger d only if nothing passes.
      - Then the sheave steps, then the β steps.
    - **Design Notes:** one quotation under "Notes" when Dp/d < 40 (e.g. "Deflector pitch diameter 250 mm gives D/d = 25 for 10 mm ropes; EN 81-20 5.5.2.1 requires D/d ≥ 40 for pulleys (≥ 400 mm). The smaller deflector is accounted for in the rope safety factor (EN 81-50 5.12)."). The deflector D/d warning is removed.
    - **Sheet / app:** no deflector D/d row; the sheet shows "Deflector sheave pitch diameter" as a value.
    - **Q sweep** (`output/presets-deflectorpolicy-20261008/final/presets-core/q-sweep.md`):
      - Survey shaft: 320 → 4×10, 480 → 5×8, 630 → 4×11 (β 100), 800 → 6×11 (β 105), all OK. 1000 / 1275 / 1600 → NoPass (braking traction, or Sf at 1600), one recommendation each.
      - Table 6 shafts: 1300×1400 (250 mm deflector) at Q 320 → 4×10 OK with the D/d 25 quotation; 480 / 630 / 800 OK; 1000+ NoPass as on the survey shaft.
    - **Reviewed example project:** its only rope result was the deflector D/d, so it now has no rope section (tests updated).
  - Stage-2 selection fix (2026-10-08). Full description: `ElevatorAutomation/docs/PRESETS.md`.
    - **Rope selection** (`ProjectPresets.SizeRopes`):
      - Searches n 4–7 and d from the rope table with d ≤ min(Dt, Dp) / 40, ordered by smallest d, then n. The first combination with every check OK wins (diameter, count, Sf, both D/d, all traction cases, no omission).
      - Then β steps 95 → 100 → 105° (`UndercutAngleStepsDeg`, stored as `RopeTractionInputs.SelectedUndercutAngleDeg` while β is unlocked). Then sheave steps; Dt ≠ 520 is invalid in the native B-B section and is skipped.
      - If nothing passes: the ladder values stay, and one Design Notes recommendation names the limiting check.
      - Locked values never change. Cached by a canonical project hash.
    - **Capacity table:** P 450 / 550 / 700 / 850 / 1000 / 1250 / 1500 kg; machine masses and q unchanged.
    - **Live calculation:**
      - 300 ms debounce after any input on any page. The Calculate buttons and the "not calculated / stale" states are removed.
      - Only results whose inputs changed are recalculated.
      - An invalid entry shows its field message and the section shows nothing.
    - **UI:**
      - Status codes are shown as words everywhere, including the status bar and messages.
      - Basic lever arms are shown as a table in mm with 0–1 decimals.
      - The long h label in messages is shortened.
    - **Q sweep** (`output/presets-selectfix-20261008/final/presets-core/q-sweep.md`):
      - Survey shaft: Q 320–800 all OK (4×8, 5×8, 6×8 β100, 6×10 β105). Q 1000 / 1275 / 1600 have no passing combination: braking traction, rope Sf, and d ≤ 10 by the 400 mm deflector. Each has exactly one recommendation.
      - Table 6 shafts: Q 480 / 630 / 800 OK. Q 320 needs SD 1400 for a small enough car, which gives a 250 mm deflector, so d ≤ 6.25 mm < 8 mm minimum and no pass. Q ≥ 1000 as on the survey shaft.
  - DESIGN-FLOW stage 2, "Flow foundation" (2026-10-08). Full description: `ElevatorAutomation/docs/PRESETS.md`.
    - **Presets** (`config/presets.json`, validated by the Core loader):
      - Survey example: 1500 × 1800, pit 1300, OH 4200, 5 stops at 3000 mm; new floors 3000 mm.
      - Q 630 with persons = floor(Q/75); v 1.0.
      - Capacity table (interpolated, clamped) for P and machine mass; CW = P + 0.5·Q.
      - Rope ladder 4×10 / 5×11 / 6×12 / 7×12; sheave 520 (steps 560 / 620); h 2800.
      - Gear and buffer rules by v; 2 guide rails; 1 / 1 buffers, equal sharing; brackets 1500.
      - Basic exclusions; Advanced offsets 0, transfer model and load case.
    - **Mechanism** (`Elevator.Core/Project/ProjectPresets.cs`):
      - Materialized `PresetField`s and `ElevatorProject.FollowsPresets`.
      - `Apply` writes unlocked fields; editing locks a field; Reset unlocks it.
      - The UI refreshes the fields that follow while the user types; the rope sizing runs on Commit.
    - **Rope auto-upsize** (`SizeRopes`):
      - Steps the count up to 7, then the next ladder diameter; the sheave steps only for a sheave D/d failure; locked values never change.
      - If nothing passes, the ladder values are kept and the Design Notes add a recommendation.
      - New project: 5 × 11 kept (deflector D/d 36.4 and braking traction are NOT OK, and ropes cannot fix them).
    - **Older projects:** open unchanged; their values count as edited and blanks stay blank.
      - "Fill blanks from presets": one action per page (shown when the page has blanks) and one global action in the Project toolbar.
      - Stage-1 rope / traction data fields keep their stage-1 behaviour.
    - **UI cleanup:**
      - Hints, guidance notes, disclaimers, read-only metadata fields and the source / assumption fields removed (the values stay in the model).
      - Validation page: plain status words.
      - Design Notes: "(project rule MR-DS-001)" removed.
      - Full list: `output/presets-20261008/final2/REMOVED-UI-STRINGS.md`.
    - **New fields:** `ElevatorProject.FollowsPresets`, `SafetyPitInputs.BufferType` (not used by equations), and the safety-gear choice "Instantaneous" (P5 then unavailable, as for any non-progressive type).
  - Design Notes / sheet fix (2026-10-08):
    - **Design Notes** (`DesignNotes.cs`, rewritten): headings "Warnings" / "Recommendations", shown only when they have notes; one- or two-line notes with the clause in brackets. No intro, no check table, no basis, no notes about passed checks. With nothing to say: the single line "No design notes for this project."
    - **Warnings, each with a computed recommendation:**
      - Table 6: minimum rated load by inverse interpolation (new `CarAreaRules.MinimumRatedLoadKg`; 2.31 m² → 955 kg).
      - Rope Sf NOT OK: the smallest rope count up to 7 and the next listed rope diameter with OK Sf and D/d, both from the same engine on a project copy.
      - D/d NOT OK: minimum pitch diameter 40·d.
      - Traction NOT OK: case, position, e^(fα) margin, and the α or f it needs.
      - Invalid layout: TZ, with the valid range 520–1100 mm.
      - Rope diameter or count below the minimum.
    - **Recommendations:** inputs needed to complete a check.
    - **Sheet:**
      - Termination row: required "0.8 × MBL = 35.2 kN", actual "MBL = 44 kN", result "Required strength".
      - Rope / traction section flows (`DocxDocument.Flow`: equation lines and table rows not chained; headings keep with their first line).
      - Basic P1–P4 figures F2/F3 placed after the results ("text first, then figures").
      - Pagination enforced and passing for 01, 12, 14 and 15.
  - Rope / traction checks (2026-10-08); full details in `ElevatorAutomation/docs/ROPE-TRACTION-CALCULATIONS.md`.
    - Core (`Elevator.Core/Calculations/RopeTractionCalculation.cs`, rule `EN81-20-5.5_EN81-50-5.11-5.12_RopeTraction-v1`, own fingerprint):
      - checks: d ≥ 8 mm; n ≥ 2; D/d ≥ 40 for the sheave and the deflector; Sf = MBL/Tmax ≥ max(12/16, EN 81-50 5.12 with Nequiv from Table 2 and Kp); termination 0.8·MBL;
      - traction: car loading at 125 % Q; emergency braking, loaded at the lowest landing and empty at the highest, with mDP; stalled with the counterweight on its buffers;
      - μ, f per groove type, e^(fα), T1/T2 per EN 81-50 5.11.3 a) with the Annex D positions.
      - `CarAreaRules` implements EN 81-20 Table 6.
      - `RopeTractionRules.Apply` evaluates MR-ROPE-MIN-001, MR-ROPE-001 and MR-TRACTION-001 from the results.
    - Presets (`Elevator.Core/Project/Presets.cs`, `config/presets.json`, shipped to the bin):
      - Core loader with validation.
      - Generic `PresetField` lock / follow / reset, with flags in `ElevatorProject.UserEditedFields`, backward-compatible.
      - Applied only to the new inputs: rope designation, MBL and kg/m by diameter, groove (U β 95° γ 30°), travelling cable 0.5 kg/m, a 0.5 m/s², deflector mass by pitch (400/520/620 → 45/70/95 kg), traction limiting method.
    - Application:
      - Suspension page "Rope and traction data" editors: no preset wording; context menu "Reset to preset"; β/γ shown per groove type; unsafe values rejected with a short message.
      - Calculations page "Ropes and traction" panel (Basic and Advanced) with "Calculate ropes & traction", results and equations.
      - Workflow → "Export Design Notes…".
      - The calculation-sheet export dialog also lists NOT OK checks.
    - Calculation sheet:
      - Section "Suspension rope and traction checks" after the rope tensions: geometry table (Htr, H, Dp, α, mDP); equations with substitution; check table; traction cases.
      - Summary row, for example "Rope Sf 17.89 ≥ 12.56 OK; D/d OK; traction NOT OK", and the new input rows.
      - F10 traction sheave: α dimensioned from the reeving geometry, T1/T2 arrows, groove detail with β/γ.
      - The sheet stays clean.
    - Design Notes (`DesignNotes.cs`):
      - same title block and page frame as the sheet;
      - code checks with clause, requirement, value and status, including EN 81-20 Table 6 (480 kg / 1100 × 2100 → 2.31 m² > 1.36 m² NOT OK);
      - NOT OK items with an explanation; checks not calculated, with the reason; basis.
    - Sheet layout kept within the pagination rule: F4 narrowed slightly with tighter notes, F10 compact.
  - Rope/traction task, standards review only (2026-10-08). Read from `D:\CAD DATA---------------------\AHM Folder\CODES\` (EN 81-20-2014.pdf; bs_en_81_t50_e_d01_14.pdf, formulas read from page images):
    - EN 81-20:
      - 5.5.1.2 a: rope d ≥ 8 mm.
      - 5.5.1.3: at least 2 ropes.
      - 5.5.2.1: pitch diameter / d ≥ 40 for sheaves and pulleys.
      - 5.5.2.2:
        - Sf ≥ 12 (traction, 3 or more ropes) or ≥ 16 (2 ropes), and ≥ the EN 81-50 5.12 value.
        - Definition: Sf = MBL of one rope / maximum force in that rope, with the car stationary at the lowest landing with rated load.
      - 5.5.2.3: terminations ≥ 80 % MBL.
      - 5.5.3: the three traction conditions.
    - EN 81-50, traction (5.11):
      - 5.11.2.1: T1/T2 ≤ e^(fα) for loading and braking; T1/T2 ≥ e^(fα) for stalled.
      - 5.11.2.2: car loading at 125 % Q, worst position; braking empty or rated load, a ≥ 0.5 m/s² (normal case); stalled empty car.
      - 5.11.2.3.1, groove friction factor:
        - U/undercut: f = μ·4(cos γ/2 − sin β/2)/(π − β − γ − sin β + sin γ), with β ≤ 105° and γ ≥ 25°.
        - V unhardened: f = μ·4(1 − sin β/2)/(π − β − sin β).
        - V hardened, and every V groove when stalled: f = μ/sin(γ/2), with γ ≥ 35°.
      - 5.11.2.3.2: μ = 0.1 (loading), 0.1/(1 + v/10) (braking), 0.2 (stalled).
      - 5.11.3 a: general T1/T2 for machinery above, with MSR = ns·(0.5H ± y)·w and MTrav = nt·(0.25H ± 0.5y)·w.
    - EN 81-50, rope safety factor (5.12):
      - Nequiv = Nequiv(t) + Kp·(Nps + 4·Npr), with Kp = (Dt/Dp)^4.
      - Table 2 gives Nequiv(t): V γ 35/36/38/40/42/45/50° → 18.5/16/12/10/8/6.5/5; U-undercut β 75/80/85/90/95/100/105° → 2.5/3.0/3.8/5.0/6.7/10.0/15.2; U without undercut = 1; linear interpolation allowed.
      - Sf(required) = 10^(2.6834 − log(695.85·10^6·Nequiv/(Dt/dr)^8.567) / log(77.09·(Dt/dr)^−2.894)).
    - Annex D/E settle several items:
      - Positions: car loading and loaded braking at the lowest landing; empty braking and stalled at the highest landing.
      - Stalled case: T1 = (P + MTrav)·g/r, T2 = MSRcwt·g (CW mass dropped).
      - y sign convention: at the lowest landing MSRcar = ns·H·w, MSRcwt = 0, MTrav = 0.
      - Figure E.2 (1:1 with deflector) gives Nps = 1, Npr = 0, consistent with the project's external-tangent reeving.
    - Project data present:
      - rated speed: `Elevator.SpeedMetresPerSecond`;
      - travel: shared vertical geometry;
      - α with a deflector: `ReevingSolver.CalculatedWrapAngleDeg` through `MachineRoomSectionRules` and the reviewed context;
      - deflector: required when TZ ≥ 520, pitch 250/400.
  - v02 promotion (2026-10-08):
    - Pre-checks passed: the candidate `ShaftMaster.exe` = `57FBE447C633F648…`, and v02 did not exist.
    - `bin-shaftmaster-calcsheet-candidate/` copied unchanged to `ElevatorAutomation/bin-shaftmaster-v02/` (42 files, not rebuilt). Added `RELEASE-NOTES.md` and `SHA256SUMS.txt`, for 44 files in total.
    - `SHA256SUMS.txt` lists 43 entries (every file except itself). All verify: 42 equal the candidate's files, and `RELEASE-NOTES.md` exists only in v02.
    - `ElevatorAutomation/docs/SHAFTMASTER-UI-RESULTS.md` now names v02 as the current release, with v01 kept unchanged as the previous release.
  - Release check (2026-10-08):
    - Fixes:
      - F9 (document Figure 6): per-buffer arrows on each buffer's centre line, down to the pit floor at the base, labelled "P6 39.2 kN" / "P7 14.8 kN"; totals line kept.
      - FF equations panel: the Definitions paragraph is shown once (`CarGuideForcePage.cs`, text only).
      - `CalculationUiTests.cs:48`: not a UI defect. At 760 × 640 every Calculate button (P1–P4 ×2, P5–P7, FF1 / FF2) scrolls fully into view (screenshots `calculation-ui/07b-narrow-calculate-1..4.png`). The old check assumed the last P1–P4 button sits at the page bottom, which stopped holding once FF was added below. Only the test changed: it now brings each button into view and asserts full visibility.
    - New test `tests/solution/ReportFieldsNativeTests.cs`: native four-view DWGs with report fields blank vs filled. Entity counts, geometry JSON and statuses are identical, and the fields survive reopen.
  - FF1 direction and Phase B figures (2026-10-08); details in `ElevatorAutomation/docs/CALCULATION-SHEET-FIGURES.md`:
    - FF1/FF2 convention, forces drawn on the rail:
      - Load towards +X: at the upper level the loaded-side (right) rail is pushed outward; at the lower level the left rail is pushed outward.
      - FF2, load towards +Y: at the upper level both rails go towards the rear; the lower level is reversed (unchanged).
      - `GuideForcePlan`/`GuideForceElevation` take the offset signs and derive every arrow from them; the sheet uses +1/+1.
      - F5a shows "Q at W/4" on the loaded side.
      - During the work, the new moment test caught an FF2 sign slip in the refactor; it was fixed before the final run.
    - F1:
      - Rails at true scale, enlarged about the blade tip only below 3 mm on paper (`MinRailSymbolMm`).
      - Leader labels "car rail T70/65/9" and "CW rail T50/50/5". The old "Rails: …" note is removed.
    - F7 suspension:
      - 1:1 roping elevation: traction sheave over the car drop.
      - Deflector only when `MachineRoomRules.SelectDeflector(TZ).IsRequired`.
      - T(car)/T(cwt) arrows with all-rope and per-rope values, "n = 6 ropes, 1:1", and the sheave pitch label when `Machine.TractionSheavePitchDiameterMm` is set.
      - Omitted without the shared plan.
    - F8 safety gear: front elevation of the lower car frame, with one rail, safety-gear block and downward P5 arrow per car guide rail, "per car guide rail" and the rail count; plan detail of the jaws on the blade.
    - F9 pit buffers:
      - Car and CW buffers by count, positions schematic.
      - A downward per-buffer arrow onto the pit floor at each base.
      - P6/P7 totals labelled; pit depth dimensioned only when `Shaft.PitDepthMm` is set.
    - Placement: F7 after the rope table, F8 after P5, F9 after P6/P7.
    - F6 and F9 compacted, so the complete sheet is 9 pages with no page except the last over 40 % empty.
    - Notes in the new figures avoid commas after letters (Word SVG glyph issue) and word hyphens.
  - Figfix (2026-10-07); details in `ElevatorAutomation/docs/CALCULATION-SHEET-FIGURES.md`:
    - F5 is now two views.
      - a) Front elevation: both car rails. FF1 is a couple: the upper shoe pushes the left rail outward and the lower shoe pushes the right rail outward. Labelled "upper shoe level" and "lower shoe level", with h dimensioned.
      - b) Side view of one rail: FF2 is a couple on the blade side faces, upper towards the rear and lower towards the front, with h dimensioned.
      - FF2 is never drawn in view a.
    - F4 level labels.
      - FF1: "upper level" on one rail and "lower level" on the other, both outward.
      - FF2: "upper level" on both rails, plus "forces reverse at the lower level".
      - Notes: "FF1 / FF2: force per guide rail at one guide shoe level" and "Directions reverse between the upper and lower shoe levels".
      - The shoes are taller and the FF1 arrows sit 3 mm off the rail line, so they clear the resultant marker.
    - F1 additions:
      - filled T rail symbols;
      - car DBG (`CarDbg`) and CW DBG (`CwDbg`) dimensions, with "W …" / "D …" labels;
      - an entrance opening in the front wall, with jamb lines and "ENTRANCE", taken from `DoorWidth` / `doorLeft` / `doorRight`.
    - Hyphens: `Figure.Text` turns a hyphen between letters into a space, because Word's SVG renderer spaces U+002D and ISOCPEUR has no U+2011. Labels were rephrased, and trailing full stops and semicolons were removed from the notes.
    - Pagination:
      - In the FF section the text (inputs, equations, results) now comes before F4–F6.
      - F4 and F5 are about 106 mm tall, so they share a page.
      - Keep-with-next remains only between a heading or caption and its own content.
      - New check `tests/solution/check_pagination.py`. Complete fixture: 8 pages (was 9), worst non-last page 22 % empty.
  - Page layout and figures (2026-10-07); details in `ElevatorAutomation/docs/CALCULATION-SHEET-EXPORT.md` and `ElevatorAutomation/docs/CALCULATION-SHEET-FIGURES.md`:
    - Page border frame: thin 0.5 pt outer line, thick 1.5 pt inner line, about 10 mm from the edge.
    - Header title block on every page: logo | company over title | Project/Client | Doc. No./Rev./Page X of Y. Fixed geometry; blank rows dropped.
    - Footer band: Prepared with ShaftMaster · date | Doc. No. · Rev.
    - Page 1 body: title, project table, design basis, approval block. The logo is in the header only.
    - Figure engine (`FigureModel.cs`, `FigureRender.cs`, `CalculationFigures.cs`):
      - layers, hatch, dimensions, load arrows and text placement that avoids overlaps;
      - SVG plus a 300 dpi PNG fallback via WPF, embedded with svgBlip;
      - each figure framed with a caption strip, numbered in order of appearance, and present only with its section.
    - Rail profile: drawn proportionally behind `IRailSection`, so a future rail table can replace it.
  - Title block / approval block (2026-10-07 ~14:45); details in `ElevatorAutomation/docs/CALCULATION-SHEET-EXPORT.md`:
    - Company settings: company name and logo in `%AppData%\ShaftMaster\` via General → Company / report settings… (Browse, Remove, preview).
    - New optional project group `ElevatorProject.Report`, edited on the General page under Calculation sheet details.
    - Page 1:
      - logo and company name;
      - the title;
      - the project table with filled rows only;
      - the design-basis line;
      - the approval block: Prepared/Checked/Approved by × Name | Signature | Date. It is always present; signature and date cells are empty.
    - Running header: company · title · project, with a small logo. Footer: Doc. No. · Rev · Prepared with ShaftMaster · date · Page X of Y. Blank parts are dropped and no placeholder is printed.
    - `DocxImage` / `DocxDocument.Image` / `ImageAndText` / `HeaderImage`: a reusable PNG/JPEG inline image API, sized in mm with the aspect ratio kept.
  - Clean client sheet (2026-10-07 ~13:30); details in `ElevatorAutomation/docs/CALCULATION-SHEET-EXPORT.md`:
    - Title block, then the exact line "Design basis: EN 81-20:2014 and EN 81-50:2014."
    - Header `Elevator Calculation Sheet · <project>`; footer `Prepared with ShaftMaster · <date> · Page X of Y`.
    - Summary is Calculation | Key result; inputs are Input | Value.
    - Each section has a technical description, equations with substitutions, and a results table.
    - All preliminary/incomplete/certification text, source-note name/hash/paragraphs, rule IDs, GUID, application name, "adopted"/interpretation labels, Section 7 and "not included" statements were removed, along with the Basis/Status/Page/Used-by columns and the duplicated FF paragraph.
    - Unavailable calculations are omitted entirely and the remaining sections renumbered. Inputs are listed only when supplied, valid and used. The per-rope column is omitted without a rope count.
    - App: an "Export anyway / Cancel" dialog lists omitted calculations and their inputs; export is blocked with an explanation when nothing is available.
    - Internal traceability is kept in `CALCULATION-SHEET-EXPORT.md` and `CAR-GUIDE-FORCES.md`.
  - Calculation-sheet export (2026-10-07 ~02:20); details in `ElevatorAutomation/docs/CALCULATION-SHEET-EXPORT.md`:
    - Workflow → "Export Calculation Sheet…" (compact "Calc Sheet…" label in narrow windows) opens a Save As dialog with a suggested `<project> - Calculation Sheet.docx`.
    - The .docx is written with System.IO.Packaging; Word is not required.
    - Every section is recalculated and fingerprint-checked at export.
    - Missing inputs show as NOT PROVIDED; malformed text shows as INVALID TEXT; never zero.
    - The INCOMPLETE — NOT FOR ISSUE banner and header appear when any of P1–P4, P5, P6, P7 or FF is unavailable. Sections are independent.
    - Complete sheets still say PRELIMINARY / engineering review required / not certified.
    - The adopted FF equations are unchanged.
  - FF1/FF2 implementation (earlier session, 2026-10-06 21:24–21:29; transcript not available here):
    - The Calculations page has an FF1/FF2 section in Basic and Advanced modes. The only input is `h` (vertical upper-to-lower car guide-shoe spacing, mm), stored as `ElevatorProject.Calculations.CarGuideForces.GuideShoeSpacingMm`. `h` has no default.
    - Capacity and internal cabin width/depth are reused through the shared `DesignRules.Calculate` and shown read-only.
    - The candidate was built with `build-solution.ps1 -BinDirectory bin-shaftmaster-ff-candidate`.
  - Native/STEP integration regression (this session, 2026-10-07 00:56): `tests/solution/run-ff-integration.ps1` was run with default parameters against the unchanged candidate. **PASS, 22 assertions.**
  - `ElevatorAutomation/docs/CAR-GUIDE-FORCES.md` has a new "Native/STEP integration" section.
  - FF audit (2026-10-07 ~01:35):
    - Equations, 10 N/kg, ×2 once, the fixture (1100/2200, 1050/2100 N), h meaning, reused internal width/depth and invalidation all match the note, code and tests.
    - There is no rail/shoe division and no group sum.
    - Defect fixed: the UI results note and engine `Definitions` still said the interpretation was unresolved. They now show the approved interpretation (`ApprovedInterpretation` constant, added to result details). The math, inputs, persistence, schema and `RuleVersion` are unchanged.
    - Regression checks were added, and the candidate was rebuilt.
  - Engineering interpretation recorded (2026-10-07, documentation only): the new section "Engineering interpretation (user-approved 2026-10-07)" in `CAR-GUIDE-FORCES.md` replaces the "unresolved" wording. The bundled copy in the candidate is identical (SHA-256 `8BC248AC…E4AA8`). `CALCULATION_ROADMAP.md` is updated.
- **Decisions:**
  - DESIGN-FLOW stage 2: Executor interpretations, documented in `docs/PRESETS.md`, for the Planner to confirm:
    - **"Keep the last values" when nothing passes:** read as the values before upsizing (the ladder values), not the last combination tried. This matches the expected 5 × 11 for the new project.
    - **New project rope sizing:** NoPass. The deflector D/d (400 / 11) and the braking traction case fail, and ropes cannot fix either. The upsize stops after the first trial when the deflector D/d fails, because a larger d only lowers Dp/d.
    - **Sheave step 520 → 560 → 620:** implemented, but it cannot trigger with ladder diameters ≤ 12 mm, and the native B-B section accepts only 520 mm.
    - **Older projects:** the stage-1 rope / traction data fields (MBL, kg/m, groove, cable, a, deflector mass, limiting method) keep their stage-1 behaviour (they follow presets). All new stage-2 fields count as edited until "Fill blanks".
    - **Advanced:** the carrying-member pair has no preset (not in the Planner list); it remains a selection. The hidden basis texts are neutral ("Traction sheave centre", "Released two-stage beams with rigid spreader", "Deflector centre", "Not included") and print on the Advanced sheet.
    - **Validation page:** the Core rule messages are kept as real validation messages, with plain status words. "NOT YET EVALUABLE" and "NOT PROVIDED" are reworded, and bracketed rule IDs removed. Long rule texts such as "SELECTED BY PROJECT RULE: …" are kept.
    - **BufferType:** a selection only, not used by the equations; the sheet does not print it.
  - Rope / traction (Planner A1–A6 applied as given). Executor choices within them, documented in `ROPE-TRACTION-CALCULATIONS.md`:
    - P and Mcwt include the optional car-side and counterweight-side additional suspended masses, as in the rope tensions section.
    - Rope MBL and kg/m presets apply to an exact diameter match only. An unlisted diameter has no preset, so the dependent checks are omitted.
    - The deflector-mass preset uses the nearest end outside the table, so a 250 mm deflector gets 45 kg.
    - A deflector without a valid path (TZ > 1100, no size selected) also omits the deflector D/d and the EN 81-50 Sf, because both need a selected deflector. Traction is omitted per A3.
    - The travelling cable mass uses the travel height. Rope masses use H = travel + overhead (A6).
    - Rope / traction section availability:
      - The section appears only when a D/d, Sf or traction result exists.
      - Its omissions form one grouped "Suspension rope and traction checks" entry in the export dialog.
      - The reviewed example project (8 mm ropes on the 250 mm deflector) now has a D/d NOT OK rope section, so it is no longer "nothing available".
    - The existing project rope-count rule (4–7) blocks the shared geometry for 2–3 ropes. Geometry-dependent checks are then omitted, so the 2-rope Sf minimum of 16 is never reached in the live project.
    - Test fixture 01 (`BasicComplete`) now also sets Dt = 520 and v = 1.0. All P1–P7 and FF numbers are unchanged (asserted).
  - Figures (this task, no new engineering decisions):
    - F3 is drawn for Basic only, because Advanced has no single resultant. Advanced F2 shows its load points and L, s(f), s(r).
    - The F1 walls are a symbolic 200 mm hatched band, not dimensioned.
    - F4 does not repeat D, which is dimensioned in F1 and listed in the FF inputs table.
    - The F6 rail is dimensioned with b1, h1 and k only; other features are proportional (foot 0.12·h1, edge 0.09·h1, root 0.8·k). F3, F5 and F6 are marked NTS.
    - SVG user unit = 0.1 mm, to avoid Word's glyph-spacing problem at small user-unit font sizes.
  - Title block (this task):
    - A blank project name is also omitted from the project table and header (the suggested file name still uses "Untitled project").
    - Viewing the General page creates an empty `Report` group (all fields null); it is not marked dirty.
    - `tests/project/SchemaGenerator.cs` treats `Report` as optional, like `Calculations`.
  - Clean sheet (user direction 2026-10-07): the client document holds calculation content only.
    - Missing data is never mentioned in the document; it is reported only in the application.
    - Supplied zero offsets (Advanced Δ values) are shown as data.
    - Inputs are listed only when used by a calculation that is present.
  - Calculation sheet (this task, no new engineering decisions):
    - P1–P4 are labelled as ShaftMaster project models (Basic single resultant vs Advanced rope/member actions); P5–P7 and FF are labelled as adopted note rules.
    - Basic rope tensions are shown as informational only.
    - Equations are formatted text, not Word equation objects.
    - Word is used only as a test renderer.
  - **User-approved engineering interpretation (2026-10-07).** This is a design interpretation by the user as responsible design engineer, not certification. Basis: EN 81-50:2014 Annex C.2.1 (informative).
    - FF1 = Fy, along the car-rail line (project car-width direction; rails at `railLeft`/`railRight`). It acts on the blade tip and bends the rail about x-x.
    - FF2 = Fx, perpendicular to the rail line (car-depth direction), on the blade side. It bends the rail about y-y (the weaker axis).
    - Each value is the force per guide rail at one shoe level. It is not divided by rails/shoes and not summed. For bending it is applied at mid-span (M = 3·F·l/16).
    - h is the centre-to-centre distance between the upper and lower car guide shoes. It stays a manual input with no default.
    - Valid for 2 rails with a centrally guided car only.
    - The note values (about 2× EN, conservative) are adopted.
    - Not covered: k2 normal running, Fs sill loading, and the rail check itself.
  - Earlier decisions, recorded in the docs:
  - Adopted source-note rules from `Elevator_Calculations_GEARBOX_Code_Corrected_Dual_Units.docx` (paragraphs 77–87):
    - `FF1 base = Q × 10 × internal width / (4h)`
    - `FF2 base = Q × 10 × internal depth / (8h)`
    - Design force = base × 2, applied once.
  - The 10 N/kg coefficient is kept separate from g = 9.81.
  - Missing/invalid input makes the forces unavailable, never zero, and does not block P1–P7 or drawing generation.
  - The FF input is excluded from the chassis and safety/pit fingerprints.
  - The implementation still reports scalar FF1/FF2 only. Since the audit, the UI and result details display the approved interpretation text. No calculation changed, and there is no certification claim.
- **Changed files (P4 Desktop structure and clean-up, 2026-10-09; branch `refactor/p4-desktop`):**
  - New: `Elevator.Core/Application/{PresetFlow,LiveCalculation}.cs`; `Elevator.Solution.Desktop/{Shell,Pages,Common}/` (see Completed); `tests/Elevator.Core.Tests/ApplicationServicesTests.cs`.
  - Moved: `Desktop/ProjectSession.cs` and `Reporting/Sheet/CalculationWorkspace.cs` -> `Elevator.Core/Application/`; `Desktop/{WindowLayout,BrandAssets,WpfFigureRasterizer}.cs` -> `Desktop/Common/`; `Desktop/CompanySettingsWindow.cs` -> `Desktop/Pages/General/`.
  - Removed: Desktop `App.cs`, `Chrome.cs`, `PresetFlow.cs`, `CalculationsPage.cs`, `BasicCalculationsPage.cs`, `RopeTractionPage.cs`, `SafetyPitPage.cs`, `CarGuideForcePage.cs`, `CalculationSheetExport.cs` (content moved into Shell / Pages / Common); the clean-up removals listed under Completed.
  - Changed: `Desktop/AssemblyInfo.cs` (InternalsVisibleTo); `Reporting/Sheet/CalculationSheet.cs`, `Reporting/Docx/DocxWriter.cs`; `Elevator.AutoCAD/Drawing/SectionAADrawing.cs`, `Project/UnifiedProjectStorage.cs`; UI test sources and their helpers; test sources (usings); runners in `tests/solution`, `tools/run-golden.ps1`, `tests/project/run-phase1.ps1`; test projects (no ProjectSession link; rasterizer path); `.gitignore`; `README.md`, `ElevatorAutomation/README.md`, `docs/TESTING.md`, `docs/SAFETY-PIT-CALCULATIONS.md`, `docs/SHAFTMASTER-UI-RESULTS.md`; `SESSION_HANDOFF.md`.
- **Changed files (P3 reporting extraction, 2026-10-09; branch `refactor/p3-reporting`):**
  - Moved: the 10 files listed under Completed (Desktop -> `ElevatorAutomation/Elevator.Reporting/{Sheet,DesignNotes,Docx,Figures}/`).
  - New: `Elevator.Reporting/Elevator.Reporting.csproj`, `Elevator.Solution.Desktop/WpfFigureRasterizer.cs`, `tests/Elevator.Reporting.Tests/` (csproj, ReportContentTests.cs).
  - Changed: Desktop `App.cs` (rasterizer registration, usings), `CalculationSheetExport.cs`, `CalculationsPage.cs`, `CompanySettingsWindow.cs`, `RopeTractionPage.cs` (usings), `Elevator.Solution.Desktop.csproj`; `ElevatorAutomation.sln`; `tests/Elevator.Core.Tests` (csproj, ConsoleSuiteTests.cs, TestData.cs JavaScriptSerializer stand-in for net8.0, run-xunit.ps1 -Project, usings); test sources (usings; GoldenTests.cs and CalculationSheetTests.cs register the rasterizer; RopeTractionTests.cs qualified name); runners in `tests/solution`, `tests/shaftmaster/run-ui-tests.ps1`, `tools/run-golden.ps1`, `tools/test.ps1`; `.github/workflows/ci.yml`; `docs/TESTING.md`; `README.md`; `SESSION_HANDOFF.md`.
- **Changed files (P2 portable Core, 2026-10-09; branch `refactor/p2-portable-core`):**
  - New: `ElevatorAutomation/Elevator.Core/Serialization/CoreJson.cs`, `JavaScriptCompatibleJson.cs`, `Elevator.Core/Properties/InternalsVisibleTo.cs`; `ElevatorAutomation/tests/Elevator.Core.Tests/` (csproj, TestData.cs, GoldenCoreTests.cs, ConsoleSuiteTests.cs, SerializerParityTests.cs, run-xunit.ps1); `.github/workflows/ci.yml`.
  - Changed: `Elevator.Core/Elevator.Core.csproj`, `Project/ProjectJson.cs`, `Project/Presets.cs`, `Calculations/CalculationFingerprint.cs`, `Calculations/ChassisGeometryProvenance.cs`; Bridge `Contracts.cs`, `StepInputFactory.cs`; Desktop `CompanyReportSettings.cs`; AutoCAD `Commands/SectionAACommands.cs`, `Commands/VerticalExperimentCommands.cs`, `Drawing/Configuration.cs`, `Drawing/ProjectDataStorage.cs`, `Drawing/SectionAADrawing.cs`, `Drawing/VerticalExperimentDrawing.cs`; StepExport `BuildModel.cs`; `Directory.Build.props`, `ElevatorAutomation.sln`, `tools/build.ps1`, `tools/test.ps1`, `docs/TESTING.md`; `SESSION_HANDOFF.md`.
  - Golden approved files: unchanged (0 files changed against `main`).
- **Changed files (P1 build system, 2026-10-09; branch `refactor/p1-build`):**
  - New: `ElevatorAutomation/Directory.Build.props`, `ElevatorAutomation/tools/build.ps1`, `ElevatorAutomation/tools/BrandIconBuilder/BrandIconBuilder.csproj`, `ElevatorAutomation/tests/projects/*/` (5 test projects).
  - Rewritten: the six `.csproj` files, `ElevatorAutomation/ElevatorAutomation.sln`, `build.ps1`, `build-solution.ps1` (wrappers), `build-section-aa.ps1`.
  - Updated references: `tests/solution/run-basic-ui.ps1`, `run-calculation-ui.ps1`, `run-car-guide-force-core.ps1`, `run-safety-pit-ui.ps1`, `generated-run-phase1.ps1`; `tests/run-tests.ps1`, `tests/project/build-project-tests.ps1`, `tests/project/run-phase1.ps1`, `tests/vertical/build-vertical.ps1`; `tools/test.ps1`, `tools/run-golden.ps1`; docs `README.md`, `ElevatorAutomation/README.md`, `README-section-aa.md`, `docs/SHAFTMASTER-UI-RESULTS.md`, `docs/TESTING.md`; `SESSION_HANDOFF.md`.
  - No source (`.cs`) file changed.
- **Changed files (P0 safety net, 2026-10-09; branch `refactor/p0-safety-net`):**
  - Build: `.gitignore` (`ElevatorAutomation/artifacts/`), `ElevatorAutomation/build.ps1`, `ElevatorAutomation/build-solution.ps1`; docs `README.md`, `ElevatorAutomation/README.md`, `ElevatorAutomation/docs/SHAFTMASTER-UI-RESULTS.md`.
  - Product (refactor only): `Elevator.Solution.Desktop/DocxWriter.cs` (`ReportClock`), `CalculationSheet.cs`, `CalculationSheetExport.cs`, `RopeTractionPage.cs` (clock); `Elevator.Solution.Desktop/App.cs`, `Elevator.AutoCAD/Drawing/Configuration.cs` (root lookup).
  - Tests: new `tests/golden/` (GoldenTests.cs, JsonMembers.cs, check_evidence.py, .gitattributes, 36 fixtures + options, `approved/` 566 files, `approved-native/` 2 files), new `tests/integration/` (run-tier3.ps1, run-affected-regressions.ps1, verify_affected.py, run-native-test.ps1, NativeGoldenTests.cs), new `tools/run-golden.ps1`, `tools/test.ps1`; `tests/solution/BasicUiTests.cs` (flake fix, trace, root), `SimplifiedTests.cs`, `SafetyPitTests.cs` (root), `run-calculation-sheet.ps1` (`-WordRender`).
  - Docs: new `ElevatorAutomation/docs/TESTING.md`, `ElevatorAutomation/docs/evidence/basic-ui-flake-proof-20261009.txt`; `SESSION_HANDOFF.md`.
- **Changed files (v03 release and repository hygiene, 2026-10-09):**
  - New: `ElevatorAutomation/bin-shaftmaster-v03/` (candidate copy + `RELEASE-NOTES.md`, `SHA256SUMS.txt`); root `README.md`, `LICENSE`.
  - Moved: `ElevatorPlanGenerator/` → `legacy/ElevatorPlanGenerator/`.
  - Edited: `.gitignore`, `CLAUDE.md` (legacy path only), `SESSION_HANDOFF.md`, `ElevatorAutomation/README.md`, `ElevatorAutomation/docs/SHAFTMASTER-UI-RESULTS.md`, `ElevatorAutomation/docs/PRESETS.md` (Q sweep path → `final2`), `ElevatorAutomation/tests/{MachineRoomTests.cs, CadIntegrationTests.cs, ArchitectureTests.cs, run-tests.ps1, project/run-phase1.ps1, solution/generated-run-phase1.ps1, section/summarize-results.ps1}`, `legacy/ElevatorPlanGenerator/{LOAD_ELEVATOR.scr, tests/first-run.scr, tests/preview.scr, tests/integration-run.scr, tests/CadIntegrationTests.cs, tests/generate-bindings.ps1}`.
  - New fixture: `ElevatorAutomation/tests/fixtures/SYNTHETIC.elevator.json` (copy of the synthetic file in `output/calculation-ui-20261006/delivery-ui/`); one path changed in each of `tests/solution/{CalculationSheetTests, CarGuideForceTests, SafetyPitTests, SimplifiedTests, BasicUiTests}.cs`.
  - Git: history rebuilt as one commit; no file deleted from disk.
- **Changed files (heavy-load selection, 2026-10-09):**
  - `ElevatorAutomation/Elevator.Core/Project/ProjectPresets.cs` (joint search, q selection, cache key, geometry reuse)
  - `ElevatorAutomation/Elevator.Core/Project/Presets.cs` (`BalanceFactorSteps` and its validation)
  - `ElevatorAutomation/Elevator.Core/Calculations/RopeTractionCalculation.cs` (`SelectedBalanceFactor`; Calculate overload with geometry)
  - `ElevatorAutomation/config/presets.json` (P table, `BalanceFactorSteps`); `ElevatorAutomation/config/ElevatorProject.schema.json` (regenerated)
  - `ElevatorAutomation/Elevator.Solution.Desktop/DesignNotes.cs` (2:1 recommendation with MBL), `PresetFlow.cs` (guarded live timer)
  - `ElevatorAutomation/tests/solution/PresetsTests.cs`, `PresetsUiTests.cs`, `BasicUiTests.cs`
  - `ElevatorAutomation/docs/PRESETS.md`, `ElevatorAutomation/docs/ROPE-TRACTION-CALCULATIONS.md`
  - Rebuilt: `ElevatorAutomation/bin-shaftmaster-presets-candidate/` (`9CFD5FAB…`)
- **Changed files (deflector policy 11a, 2026-10-08):**
  - Core: `Elevator.Core/Calculations/RopeTractionCalculation.cs` (no deflector check row, `DpOverDr`, omission wording, rule record), `Elevator.Core/Project/ProjectPresets.cs` (search order and constraint).
  - Desktop: `Elevator.Solution.Desktop/DesignNotes.cs` ("Notes" quotation, wording).
  - Tests: `PresetsTests.cs` (brute-force order, sweep with Dp and all Design Notes lines), `PresetsUiTests.cs`, `PresetsNativeTests.cs`, `RopeTractionTests.cs`, `RopeTractionUiTests.cs`, `CalculationSheetTests.cs`, `CalculationSheetUiTests.cs`.
  - Docs: `PRESETS.md` (`7F1CB05F…`), `ROPE-TRACTION-CALCULATIONS.md`; copied into the candidate.
  - Candidate:
    - `ShaftMaster.exe` `E230246192C0DB1A8D9364657A45CB33010930115255B0FB23928F14865077D9`
    - `Elevator.Core.dll` `300FDBA93178C610…`
    - `Elevator.Solution.Bridge.dll` `13772A3CC8F7D9DF…`
  - Unchanged: `presets.json` `1D8CF412…`, schema `80670A5C…`.
  - Unchanged, verified: v01 `DC8A76A3…`; v02 `57FBE447…` (SHA256SUMS 0 mismatches); calc-sheet; FF; safety/pit; ropes.
- **Changed files (stage-2 selection fix, 2026-10-08):**
  - Core:
    - `Project/ProjectPresets.cs`: selection, β, limiting check, canonical cache key.
    - `Project/Presets.cs`: `UndercutAngleStepsDeg` and validation; β preset reads the selection.
    - `Calculations/RopeTractionCalculation.cs`: `SelectedUndercutAngleDeg` field only.
  - Config: `config/presets.json` (`1D8CF412…`); schema `80670A5C…`.
  - Desktop:
    - `PresetFlow.cs`: live calculation, `StatusWord` / `Plain`.
    - Also changed: `App.cs`, `Chrome.cs`, `BasicCalculationsPage.cs`, `CalculationsPage.cs`, `SafetyPitPage.cs`, `CarGuideForcePage.cs`, `RopeTractionPage.cs`, `DesignNotes.cs`.
  - Tests:
    - `PresetsTests.cs` (+ sweep, brute-force order check), `PresetsUiTests.cs` (live edits, status-code scan), `PresetsNativeTests.cs`.
    - Calculate-button / status-code updates in `CalculationUiTests.cs`, `BasicUiTests.cs`, `SafetyPitUiTests.cs`, `SafetyPitDraftUiTests.cs`, `CarGuideForceUiTests.cs`, `CalculationSheetUiTests.cs`, `RopeTractionUiTests.cs`.
    - `run-presets-core.ps1` now references `ShaftMaster.exe`.
  - Docs: `PRESETS.md`, `ROPE-TRACTION-CALCULATIONS.md`, `CAR-GUIDE-FORCES.md`, `SAFETY-PIT-CALCULATIONS.md`, `SIMPLIFIED-CALCULATIONS.md` (copied into the candidate).
  - Candidate:
    - `ShaftMaster.exe` `E617312BA6C8C41C1B74DE86103FD6F465B3B027A42249B67936D9E83184F04A`
    - `Elevator.Core.dll` `DC89207B6D909A8F…`
    - `Elevator.Solution.Bridge.dll` `23FFBC090463F7E8…`
  - Unchanged, verified: v01 `DC8A76A3…`; v02 `57FBE447…` (SHA256SUMS 0 mismatches); calc-sheet `57FBE447…`; FF `D3111222…`; safety/pit `EA9B1F5E…`; ropes `519984A1…`.
- **Changed files (DESIGN-FLOW stage 2 presets, 2026-10-08):**
  - Core:
    - `Elevator.Core/Project/ProjectPresets.cs` (new).
    - `Presets.cs`: library sections, validation, `MassesFor` / `RopesFor` / `PersonsFor` / `CounterweightFor`, `PresetField.Materialized` / `Page` / `IsBlank`.
    - `ElevatorProject.cs`: `FollowsPresets`.
    - `SafetyPitCalculationEngine.cs`: `BufferType` field only.
  - Desktop:
    - `PresetFlow.cs` (new).
    - Changed: `App.cs`, `Chrome.cs`, `BasicCalculationsPage.cs`, `CalculationsPage.cs`, `SafetyPitPage.cs`, `CarGuideForcePage.cs`, `RopeTractionPage.cs`, `CompanySettingsWindow.cs`, `CalculationSheetExport.cs`, `CalculationWorkspace.cs`, `ProjectSession.cs`, `DesignNotes.cs`.
  - Config: `config/presets.json` (4083B365…); schema regenerated (`config/ElevatorProject.schema.json` 3B310A22…).
  - Tests:
    - New: `tests/solution/PresetsTests.cs`, `PresetsUiTests.cs`, `PresetsNativeTests.cs`, `run-presets-core.ps1`, `run-presets-ui.ps1`.
    - Updated for the new behaviour: `CalculationUiTests.cs`, `SafetyPitUiTests.cs`, `CarGuideForceUiTests.cs`, `BasicUiTests.cs`, `tests/project/SchemaGenerator.cs`.
  - Docs:
    - New: `docs/PRESETS.md`.
    - Updated where "no default" was stated: `CAR-GUIDE-FORCES.md`, `SAFETY-PIT-CALCULATIONS.md`, `ROPE-TRACTION-CALCULATIONS.md`, `SIMPLIFIED-CALCULATIONS.md`, `ELEVATOR_PROJECT_MODEL.md`.
    - Copied into the candidate.
  - Candidate `bin-shaftmaster-presets-candidate` (new):
    - `ShaftMaster.exe` `5152F49FEC6EF503BD8E7BB3D715E7A31BDDA17104126B1C56A05C031E779FB7`
    - `Elevator.Core.dll` `1C47DE7885ECADA5…`
    - `Elevator.Solution.Bridge.dll` `A379D36F77A1C949…`
  - Unchanged, verified:
    - v01 `DC8A76A3…`; v02 `57FBE447…` (SHA256SUMS 0 mismatches).
    - Candidates: calc-sheet `57FBE447…`, FF `D3111222…`, safety/pit `EA9B1F5E…`, ropes `519984A1…` (presets.json `B6FBD967…`).
- **Changed files (Design Notes / pagination fix, 2026-10-08):**
  - `ElevatorAutomation/Elevator.Solution.Desktop/DesignNotes.cs` (rewritten, `82D81B78…`).
  - `CalculationSheetRopes.cs`: Flow, termination row.
  - `CalculationSheet.cs`: F2/F3 after the P1–P4 results.
  - `DocxWriter.cs`: `Flow`.
  - `RopeTractionPage.cs`: status text.
  - `Elevator.Core/Calculations/RopeTractionCalculation.cs`: added `CarAreaRules.MinimumRatedLoadKg`; no equation changed.
  - Tests: `tests/solution/CalculationSheetTests.cs` (fixtures 16–19 Design Notes, termination row, helpers `Body` / `DesignNotesClean`), `RopeTractionUiTests.cs`.
  - Docs: `ROPE-TRACTION-CALCULATIONS.md` (`EB503F61…`), `CALCULATION-SHEET-EXPORT.md` (`56A384FB…`), copied into the candidate.
  - Candidate rebuilt:
    - `ShaftMaster.exe` `519984A125B91E2F09B6AEF0CC8F211A925A1B27C4EA0CFA26B3DD8409BC3536`
    - `Elevator.Core.dll` `96B80197E3A2E6F9…`
    - `Elevator.Solution.Bridge.dll` `B0E53D99BC0FE08B…`
  - Backup: `ElevatorAutomation/output/ropes-20261008/pre-notesfix-backup/` (previous `84E556C9…`).
  - Evidence: `ElevatorAutomation/output/ropes-notesfix-20261008/final/`
    - suites: `rope-core`, `rope-ui`, `calc-sheet-core`, `calc-sheet-ui`, `ff-core`, `safety-pit-core`
    - renders: `calc-sheet-rendered`, `calc-sheet-rendered-ui`, `calc-sheet-overview`, `review200`
  - Housekeeping: 50 intermediate folders moved to the Windows Recycle Bin (recoverable). The list is in `output/ropes-20261008/housekeeping-recycled-folders.txt`.
    - `output/ropes-20261008/`: `dev-*` (37), `run1`, `final`, `final2`, `final3`, `final4`.
    - `output/ropes-notesfix-20261008/`: this task's own `dev-*` (8), so that folder holds only the final run.
    - Kept: `final5`, `pre-notesfix-backup`.
  - Unchanged, verified: v01 `DC8A76A3…`, v02 `57FBE447…` (SHA256SUMS 0 mismatches), calc-sheet `57FBE447…`, FF `D3111222…`, safety/pit `EA9B1F5E…`, schema `EE9E959B…`, presets `B6FBD967…`.
- **Changed files (rope / traction, 2026-10-08):**
  - New Core: `Elevator.Core/Calculations/RopeTractionCalculation.cs`, `Elevator.Core/Project/Presets.cs`.
  - Edited Core:
    - `ElevatorProject.cs`: `UserEditedFields`.
    - `CalculationModels.cs`: `Calculations.RopeTraction`.
    - `CalculationFingerprint.cs`: RopeTraction excluded from the P1–P4 fingerprint.
    - No existing equation changed.
  - New config: `config/presets.json` (`B6FBD967…`). `build-solution.ps1` copies it to the bin.
  - Desktop:
    - New: `RopeTractionPage.cs`, `CalculationSheetRopes.cs`, `CalculationFiguresRopes.cs` (F10), `DesignNotes.cs`.
    - Edited: `App.cs` (presets load, Suspension page, invalidation), `CalculationsPage.cs`, `BasicCalculationsPage.cs`, `Chrome.cs` (Design Notes button), `CalculationSheet.cs` (partial; section, summary, inputs), `CalculationSheetExport.cs` (NOT OK list), `CalculationFigures.cs` (partial; F4 compact).
  - Tests:
    - New: `RopeTractionTests.cs`, `run-rope-traction-core.ps1`, `verify_rope_traction.py`, `RopeTractionUiTests.cs`, `run-rope-traction-ui.ps1`.
    - Edited: `CalculationSheetTests.cs` (computed section order, fixtures 14–17), `CalculationSheetUiTests.cs`, `run-calculation-sheet.ps1` (sources), `tests/project/SchemaGenerator.cs` (new optional fields).
  - Docs: new `ElevatorAutomation/docs/ROPE-TRACTION-CALCULATIONS.md` (`67720A46…`); updated `CALCULATION-SHEET-EXPORT.md` (`EFC1EFFE…`) and `CALCULATION-SHEET-FIGURES.md` (`BE019806…`). All copied into the candidate.
  - Candidate `bin-shaftmaster-ropes-candidate` (new, built from the current source):
    - `ShaftMaster.exe` `84E556C96B8EB5E3A7C2150EE303C458B2555795DC22F0308DE4E8253939119D`
    - `Elevator.Core.dll` `5988565F38CAFD34…`
    - `Elevator.Solution.Bridge.dll` `A4FBB785F52FB175…`
  - Schema: `config/ElevatorProject.schema.json` is now `EE9E959B…`; it was `117B4A03…` before the new optional `RopeTraction` / `UserEditedFields` fields.
  - Evidence: `ElevatorAutomation/output/ropes-20261008/final5/`
    - suites: `rope-core` (with `hand-check-report.json`), `rope-ui`, `calc-sheet-core`, `calc-sheet-ui`, `ff-core`, `safety-pit-core`, `calculation-ui`, `affected-regressions`
    - renders: `calc-sheet-rendered`, `calc-sheet-rendered-ui`, `calc-sheet-overview`, `review200`
    - `runners/`, `logs/`
  - Earlier `final`…`final4` and `dev-*` folders are superseded iterations.
  - Unchanged, verified: v01 `DC8A76A3…`, v02 `57FBE447…` (SHA256SUMS 0 mismatches), calc-sheet candidate `57FBE447…`, FF `D3111222…`, safety/pit `EA9B1F5E…`. Not touched: CAD templates, original DWGs, planning files, DESIGN-FLOW.md.
- **Changed files (v02 promotion, 2026-10-08):**
  - New: `ElevatorAutomation/bin-shaftmaster-v02/` (copy of the candidate, plus `RELEASE-NOTES.md` and `SHA256SUMS.txt`).
  - Edited: `ElevatorAutomation/docs/SHAFTMASTER-UI-RESULTS.md` (current release section).
  - Unchanged, verified: v01 `DC8A76A3…`, safety/pit candidate `EA9B1F5E…`, FF candidate `D3111222…`, calc-sheet candidate `57FBE447…`.
- **Changed files (release check, 2026-10-08):**
  - Source: `ElevatorAutomation/Elevator.Solution.Desktop/CalculationFigures.cs` (F9 arrows), `CarGuideForcePage.cs` (Definitions once; `using System.Linq`).
  - Tests: `ElevatorAutomation/tests/solution/CalculationSheetTests.cs` (F9 axis/label checks), `CalculationUiTests.cs` (per-button viewport check), new `ReportFieldsNativeTests.cs`.
  - Candidate rebuilt in place:
    - `ShaftMaster.exe` `57FBE447C633F648…`
    - `Elevator.Core.dll` `29D7F3C749843F59…` (Core source unchanged)
    - `Elevator.Solution.Bridge.dll` `AB56147A1CE0F466…`
  - Backup: `ElevatorAutomation/output/calcsheet-20261007/pre-release-check-backup/` (previous `F212F0F9…`).
  - Evidence: `ElevatorAutomation/output/release-check-20261008/`. One fresh folder per suite, plus `logs/`, `runners/`, `figure6-review200/` and `RELEASE-CHECK.md`.
  - Protected binaries unchanged: release `DC8A76A3…`, safety/pit `EA9B1F5E…`, FF `D3111222…`. Schema `117B4A03…`.
- **Changed files (FF1 direction + Phase B, 2026-10-08):**
  - Source:
    - `ElevatorAutomation/Elevator.Solution.Desktop/CalculationFigures.cs` (`ACDDA15B…`): F1, F4, F5, F6 compaction, F7–F9.
    - `CalculationSheet.cs` (`134B88E3…`): F7–F9 placement.
  - Tests:
    - `ElevatorAutomation/tests/solution/CalculationSheetTests.cs` (`9C65DEFC…`): moment balance, F1 leaders/size, F7–F9 values, counts fixture 13, no unsourced numbers; `using Elevator.Core`.
    - `CalculationSheetUiTests.cs` (`3976EDA0…`): nine SVG figures.
  - Docs: `ElevatorAutomation/docs/CALCULATION-SHEET-FIGURES.md` (`470C0393…`; convention section and F7–F9 tables), `CALCULATION-SHEET-EXPORT.md` (`3339B1C4…`). Both copied into the candidate.
  - Candidate rebuilt: `ShaftMaster.exe` `F212F0F90A36E760…`, `Elevator.Core.dll` `CEE591E463CAECF0…` (recompiled, Core source unchanged).
  - Backup before rebuild: `ElevatorAutomation/output/calcsheet-20261007/pre-phaseB-backup/` (previous build `61015D45…`).
  - Evidence: `ElevatorAutomation/output/calcsheet-phaseB-20261007/`
    - `core` (with `previews/`), `rendered` (with `pagination-report.json`), `overview`, `review200`
    - `ui` (first run, failed on the old six-figure count), `ui-rerun` and `rendered-ui-rerun` (after the fix)
    - `regressions/ff-core`, `regressions/ff-ui`, `regressions/safety-pit-core`
  - Dev iterations are in `output/calcsheet-20261007/pb-dev-*`.
  - Protected binaries unchanged: release `DC8A76A3…`, FF `D3111222…`, safety/pit `EA9B1F5E…`. Schema content `117B4A03…`.
- **Changed files (figfix, 2026-10-07):**
  - Source: `ElevatorAutomation/Elevator.Solution.Desktop/CalculationFigures.cs` (`0A7AD14B…`), `FigureModel.cs` (`C0CA45ED…`, hyphen rule), `CalculationSheet.cs` (`95DCCDDD…`, FF figure order).
  - Tests: `ElevatorAutomation/tests/solution/CalculationSheetTests.cs` (`6BC9048D…`); new `tests/solution/check_pagination.py` (`A9285546…`).
  - Docs: `ElevatorAutomation/docs/CALCULATION-SHEET-FIGURES.md` (`A602B422…`), `CALCULATION-SHEET-EXPORT.md` (`9E1D32E2…`; pagination, and the stale "no figures" limitation removed). Both copied into the candidate.
  - Candidate rebuilt: `ShaftMaster.exe` `61015D45D434B14B…`, `Elevator.Core.dll` `E30511AFCA8D583D…` (recompiled, Core source unchanged).
  - Backup before rebuild: `ElevatorAutomation/output/calcsheet-20261007/pre-figfix-backup/` (previous build `EAD0C9E6…`).
  - Evidence: `ElevatorAutomation/output/calcsheet-figfix-20261007/`
    - `core` (with `previews/`), `ui`
    - `rendered` (with `pagination-report.json`), `rendered-ui`, `overview`
    - `review200` (200 % crops of every figure: complete, changed geometry, advanced)
    - `regressions/ff-core`, `regressions/ff-ui`, `regressions/safety-pit-core`
  - Dev iterations are in `output/calcsheet-20261007/ff-dev-*`.
  - Protected binaries unchanged: release `DC8A76A3…`, FF `D3111222…`, safety/pit `EA9B1F5E…`. Schema content `117B4A03…`.
- **Changed files (page layout and figures, 2026-10-07):**
  - New source: `ElevatorAutomation/Elevator.Solution.Desktop/FigureModel.cs` (`44FFDA1F…`), `FigureRender.cs` (`215D4766…`), `CalculationFigures.cs` (`7F84E138…`).
  - Edited:
    - `DocxWriter.cs` (`C8128D4E…`): SVG images, figure frame, title-block header, footer band, page frame, margins.
    - `CalculationSheet.cs` (`75AAEDF0…`): page-1 body, title block, plan/rail data, figure placement.
  - Tests:
    - `ElevatorAutomation/tests/solution/CalculationSheetTests.cs`: layout, title-block and figure checks, previews; `[STAThread]`.
    - `CalculationSheetUiTests.cs`: header checks.
    - `run-calculation-sheet.ps1`: WPF references and the figure sources.
  - Docs: new `ElevatorAutomation/docs/CALCULATION-SHEET-FIGURES.md` (`1792510B…`); updated `CALCULATION-SHEET-EXPORT.md` (`8A835E8D…`). Both are copied into the candidate.
  - Candidate rebuilt: `ShaftMaster.exe` `EAD0C9E6…6B7B`, `Elevator.Core.dll` `7681823F…63CD4` (recompiled, Core source unchanged).
  - Backup before rebuild: `ElevatorAutomation/output/calcsheet-20261007/pre-figures-backup/` (previous build `329969FB…`).
  - Evidence: `ElevatorAutomation/output/calcsheet-figures-20261007/`
    - `core` (with `previews/` PNG and SVG of F1–F6 for the default and changed cases), `ui`
    - `rendered`, `rendered-ui`, `overview`
    - `regressions/ff-core`, `regressions/ff-ui`, `regressions/safety-pit-core`
  - The schema was rewritten by the safety/pit runner; its content is unchanged (`117B4A03…`).
  - Not touched: Elevator.Core sources and equations, release, FF and safety/pit candidates, CAD templates, original DWGs, roadmap.
- **Changed files (title block, 2026-10-07 ~14:45):**
  - Core: `ElevatorAutomation/Elevator.Core/Project/ElevatorProject.cs` adds the `Report` field and `ProjectReportDetails`. No equation or engine change.
  - Desktop:
    - `DocxWriter.cs`: image API, row height, header image.
    - `CalculationSheet.cs`: title block, approval block, header and footer.
    - `CalculationSheetExport.cs`: passes the company settings to the export.
    - `App.cs`: report fields skip calculation invalidation; adds the General-page section call.
    - New: `CompanyReportSettings.cs`, `CompanySettingsWindow.cs`.
  - Schema: `ElevatorAutomation/tests/project/SchemaGenerator.cs` (Report optional). `ElevatorAutomation/config/ElevatorProject.schema.json` regenerated: only the Report group was added (`117B4A03…`).
  - Tests:
    - `tests/solution/CalculationSheetTests.cs`, `CalculationSheetUiTests.cs`.
    - `run-calculation-sheet.ps1`: adds System.Drawing and `CompanyReportSettings.cs`.
  - Doc: `ElevatorAutomation/docs/CALCULATION-SHEET-EXPORT.md`, copied into the candidate (`C4B7F137…`).
  - Candidate rebuilt: `ShaftMaster.exe` `329969FB…6F26`, `Elevator.Core.dll` `E0B965B0…21F3`.
  - Backup before rebuild: `ElevatorAutomation/output/calcsheet-20261007/pre-titleblock-backup/` (previous build `B74AB61F…`, plus the previous sources and schema).
  - Evidence: `ElevatorAutomation/output/calcsheet-titleblock-20261007/`
    - `core`, `ui`, `rendered`, `rendered-ui`, `page1-overview.png`
    - `regressions/ff-core`, `regressions/ff-ui`, `regressions/safety-pit-core`, `regressions/ff-native-step`
  - Not touched: release, FF and safety/pit candidates, CAD templates, original DWGs, roadmap. The real `%AppData%\ShaftMaster` was not created (tests redirect it).
- **Changed files (clean client sheet, 2026-10-07 ~13:30):**
  - Rewritten `ElevatorAutomation/Elevator.Solution.Desktop/CalculationSheet.cs` (render only; Build/currency logic kept). SHA-256 `9AFC13BF…BE80`.
  - `ElevatorAutomation/Elevator.Solution.Desktop/CalculationSheetExport.cs`: omission dialog and blocked export. SHA-256 `7590E408…E622`.
  - `ElevatorAutomation/Elevator.Solution.Desktop/DocxWriter.cs`: single-line footer, no Subject. SHA-256 `F8F1648F…6582`.
  - Tests: `ElevatorAutomation/tests/solution/CalculationSheetTests.cs` and `CalculationSheetUiTests.cs`.
  - Doc: `ElevatorAutomation/docs/CALCULATION-SHEET-EXPORT.md`, copied into the candidate.
  - Rebuilt candidate `bin-shaftmaster-calcsheet-candidate/`: `ShaftMaster.exe` `B74AB61F…600A`, `Elevator.Core.dll` `BC7B241E…EC59` (recompiled, source unchanged).
  - Backup before rebuild: `ElevatorAutomation/output/calcsheet-20261007/pre-clean-backup/` (previous candidate `AD1DE13E…`, plus the previous sources).
  - Evidence: `ElevatorAutomation/output/calcsheet-clean-20261007/`
    - `core`, `ui`, `ui-run2`, `rendered`, `rendered-ui`, `overview`
    - `regressions/ff-core`, `regressions/ff-ui`, `regressions/safety-pit-core`
  - `config/ElevatorProject.schema.json` was rewritten by the safety/pit runner; its content is byte-identical.
  - Not touched: release, FF and safety/pit candidates, Elevator.Core sources, CAD templates, original DWGs, roadmap.
- **Changed files (calculation-sheet export, 2026-10-07 ~02:20):**
  - New source:
    - `ElevatorAutomation/Elevator.Solution.Desktop/DocxWriter.cs`
    - `ElevatorAutomation/Elevator.Solution.Desktop/CalculationSheet.cs`
    - `ElevatorAutomation/Elevator.Solution.Desktop/CalculationSheetExport.cs`
  - Edited: `ElevatorAutomation/Elevator.Solution.Desktop/Chrome.cs` (toolbar button and responsive label).
  - New tests and scripts in `ElevatorAutomation/tests/solution/`:
    - `CalculationSheetTests.cs`, `CalculationSheetUiTests.cs`
    - `run-calculation-sheet.ps1`, `run-calculation-sheet-ui.ps1`
    - `render-calculation-sheets.ps1` (test-only Word PDF render)
    - `rasterise_calculation_sheets.py`, `contact_sheets.py`
  - New doc: `ElevatorAutomation/docs/CALCULATION-SHEET-EXPORT.md`, copied with `CAR-GUIDE-FORCES.md` into the candidate.
  - New candidate: `ElevatorAutomation/bin-shaftmaster-calcsheet-candidate/` (`ShaftMaster.exe` `AD1DE13E…A51B`, `Elevator.Core.dll` `F87432DE…DF08`).
  - Evidence: `ElevatorAutomation/output/calcsheet-20261007/`
    - `core`, `ui`, `rendered`, `rendered-ui`, `overview`, `regressions`
    - `baseline-ff-bin`, a copy of the FF candidate used only for the baseline UI check
  - New job folders under `output/solution-v01/jobs/`.
  - `config/ElevatorProject.schema.json` was regenerated by the safety/pit runner; its content is byte-identical.
  - Not touched:
    - release `bin-shaftmaster-v01` (`DC8A76A3…`)
    - safety/pit candidate (`EA9B1F5E…`)
    - FF candidate (`D3111222…`)
    - core engines and equations
    - CAD templates and original DWGs
- **Changed files (FF audit, 2026-10-07 ~01:35):**
  - Source:
    - `ElevatorAutomation/Elevator.Core/Calculations/CarGuideForceCalculationEngine.cs`: `Definitions` text, new `ApprovedInterpretation`, added to `Details`, comment.
    - `ElevatorAutomation/Elevator.Solution.Desktop/CarGuideForcePage.cs`: results note text.
  - Tests:
    - `ElevatorAutomation/tests/solution/CarGuideForceTests.cs`: +5 checks.
    - `ElevatorAutomation/tests/solution/CarGuideForceUiTests.cs`: +2 checks.
  - Docs: `ElevatorAutomation/docs/CAR-GUIDE-FORCES.md`, with an audit section and an implementation-note update. The copy in `bin-shaftmaster-ff-candidate/` is identical (SHA-256 `A485E351…681D`).
  - Candidate rebuilt in place, `ElevatorAutomation/bin-shaftmaster-ff-candidate/`, so all binaries were rebuilt:
    - New `ShaftMaster.exe` `D3111222…292E`.
    - New `Elevator.Core.dll` `492E23D8…6A69`.
    - The previous binaries are backed up in the session scratchpad only.
  - New evidence: `ElevatorAutomation/output/ff-audit-20261007/{core,wpf,integration}`, plus 2 new job folders under `output/solution-v01/jobs/`.
  - `output/step-export-tests/` was rewritten by the build, with identical content.
  - Not touched: the release, the safety/pit candidate, the schema, original DWGs and earlier evidence folders.
- **Changed files (earlier this session, documentation-only interpretation task):**
  - `ElevatorAutomation/docs/CAR-GUIDE-FORCES.md`: interpretation section added, and the "unresolved" wording replaced.
  - `ElevatorAutomation/bin-shaftmaster-ff-candidate/CAR-GUIDE-FORCES.md`: identical copy.
  - `CALCULATION_ROADMAP.md`.
  - `SESSION_HANDOFF.md`.
  - No source, schema, test, UI, binary or rebuild change. The four protected binary hashes were re-verified unchanged.
- **Changed files (earlier this session, integration run):**
  - `ElevatorAutomation/docs/CAR-GUIDE-FORCES.md`: appended the "Native/STEP integration" section.
  - `ElevatorAutomation/bin-shaftmaster-ff-candidate/CAR-GUIDE-FORCES.md`: synced copy.
  - `ElevatorAutomation/bin-shaftmaster-ff-candidate/CarGuideForceIntegrationTests.exe`: new, compiled by the test script.
  - `ElevatorAutomation/output/ff-ui-20261006/integration/`: new evidence folder.
  - `ElevatorAutomation/output/solution-v01/jobs/4c07190d8fdf41c7baa3b9a9f4480b03/` and `ElevatorAutomation/output/solution-v01/jobs/1cab7f761133412f9e8ee1361801afeb/`: new job folders.
  - `SESSION_HANDOFF.md`.
  - No source, schema, test, binary rebuild, release, safety/pit candidate or original DWG was touched.
- **Changed files (FF work, from timestamps, not a diff; the directory is not a Git repository):**
  - Source: `Elevator.Core` (rebuilt `Elevator.Core.dll`), desktop UI sources (rebuilt `ShaftMaster.exe`) and `config/ElevatorProject.schema.json` (regenerated 21:28:59).
  - Tests: `tests/solution/CarGuideForceTests.cs`, `CarGuideForceUiTests.cs`, `CarGuideForceIntegrationTests.cs`, `run-car-guide-force-core.ps1`, `run-ff-ui.ps1`, `run-ff-integration.ps1`, `verify_car_guide_force_note.py`.
  - Docs: `docs/CAR-GUIDE-FORCES.md`.
  - Outputs: `bin-shaftmaster-ff-candidate/`, `output/ff-ui-20261006/`, `output/ff-source-review-20261006/`.
- **Checks:**
  - P7 (2026-10-09): final run `ElevatorAutomation/artifacts/test-results/20261009-234346`, all PASS: tier 1 160 s (core-net8.0 533, reporting-net8.0 145, core-net48 653, reporting-net48 145, golden 566, sheet-wpf 8285); tier 2 208 s (presets-ui 59, rope-ui 33, sheet-ui 63, calculation-ui 54, basic 67, safety-pit-ui 35, safety-pit-draft-ui 117, ff-ui 126); tier 3 140 s (integration 22, presets-native 6, sheet-word-render 8285).
    - Not run: the new CI step on ubuntu (`pwsh tools/test.ps1 -Tier 1` has only been run on Windows; the Linux path is by reading); the golden console was not trimmed of the Core captures that GoldenCoreTests repeats.
  - P6 (2026-10-09): final run `ElevatorAutomation/artifacts/test-results/20261009-212749` on the last commit of the branch, all PASS:
    - tier 1 (579 s): golden 566, presets 160, rope 60, sheet 8285, FF 131, safety / pit 117, xunit-core 240, xunit-reporting 290;
    - tier 2 (209 s): presets-ui 59, rope-ui 33, sheet-ui 63, calculation-ui 54, basic 67, safety-pit-ui 152, safety-pit-draft-ui 117, ff-ui 126;
    - tier 3 (132 s): integration 22 (native generate / reopen, four-view equality with v01, 32 protected files, FF native, STEP input 7, native golden 2), presets-native 6, sheet Word render 8285.
    - During the work: tier 1 on the regroup commit in a worktree PASS (834 s); tier-3 integration PASS after the AutoCAD splits (`artifacts/test-results/p6-t3a`) and in the final run. Tier 1 was not run after every single step (it takes 10-35 min); AutoCAD / Host / Bridge steps were checked by build + tier-3 integration at milestones.
    - Not run: STEP export against a real SOLIDWORKS session; the interactive AutoCAD commands (prompts) other than through the native tests; Python was not on PATH in this session's shell, so the test runs used `PATH` with the Python 3.14 folder.
  - P5 (2026-10-09): tiers 1 and 2 on `c04723b` in a temporary worktree (`artifacts/test-results/20261009-172613` there), all PASS:
    - tier 1 (593 s): golden 566, presets 160, rope 60, sheet 8285, FF 131, safety / pit 117, xunit-core 240, xunit-reporting 290 (same counts as the P4 baseline).
    - tier 2 (219 s): presets-ui 59, rope-ui 33, sheet-ui 63, calculation-ui 54, basic 67, safety-pit-ui 152, safety-pit-draft-ui 117, ff-ui 126.
    - tier 3: presets-native 6 PASS and sheet Word render PASS (8285) in the worktree run. Its `integration` step FAILED there for two reasons: the worktree lacks the untracked `output/` evidence, and the release-baseline probe could not compile against the old v01 Core (namespaces). The probe was fixed (`46d843c`) and the integration step re-run in the main folder (`artifacts/test-results/p5-tier3-integration`): affected-regressions (200 calculation assertions, four-view equality with v01, 32 protected files), ff-integration, step-input 7, native-golden 2: all PASS. The full tiers were not re-run after `46d843c`, which changes only that probe and its runner.
    - During the work, `xunit-core` net48 `Rope_traction_core` failed once under heavy machine load and passed on re-run and in every later run. Probable cause: static `CoreJson.Override` shared by xUnit test classes that run in parallel. Not investigated.
  - P4 and clean-up (2026-10-09), final run `ElevatorAutomation/artifacts/test-results/20261009-152008`, all PASS:
    - build 3 s; tier 1 (599 s): golden 566, presets 160, rope 60, sheet 8285, FF 131, safety / pit 117, xunit-core 240 (net8.0 60 + net48 180; was 202), xunit-reporting 290;
    - tier 2 (212 s): presets-ui 59, rope-ui 33, sheet-ui 63, calculation-ui 54, basic 67, safety-pit-ui 152, safety-pit-draft-ui 117, ff-ui 126;
    - tier 3 (135 s): integration 22, presets-native 6, sheet Word render PASS.
    - During the work: tier 1 after the services (`20261009-114209`, three suites failed on a runner bug, fixed in `ce97165` and re-run PASS) and after the split (`20261009-150411`, PASS, 615 s); tier 2 after the split (`20261009-115949`, PASS; 685 s on a busy machine, single suites re-timed at the P3 times).
    - Hashes unchanged: v01 `DC8A76A3…`, v02 `57FBE447…` (SHA256SUMS 0 mismatches), v03 `9CFD5FAB…` (SHA256SUMS 0 mismatches).
    - Not run: CI on GitHub / Linux (not pushed); `tests/shaftmaster/run-ui-tests.ps1` (UiRegression; not in a tier, writes into `output/shaftmaster-ui`), the AutoCAD script suites of `tests/run-tests.ps1` and `tests/project/run-phase1.ps1` (updated, not run); SOLIDWORKS STEP export; manual clicking of dialogs.
  - P3 reporting extraction (2026-10-09), final run `ElevatorAutomation/artifacts/test-results/20261009-105905`, all PASS:
    - build 3 s; tier 1 (541 s): golden 566, presets 160, rope 60, sheet 8285, FF 131, safety / pit 117, xunit-core 202 (net8.0 41 + net48 161), xunit-reporting 290 (145 + 145);
    - tier 2 (258 s): presets UI 59, rope UI 33, sheet UI 63, calculation UI 54, Basic 67, safety / pit 152, draft 117, FF UI 126;
    - tier 3 (132 s): integration (affected regressions, FF integration 22, STEP input 7, native golden 2 files), presets native 6, sheet Word render PASS.
    - Hashes unchanged: v01 `DC8A76A3…`, v02 `57FBE447…` (SHA256SUMS 0 mismatches), v03 `9CFD5FAB…` (SHA256SUMS 0 mismatches), and every candidate.
    - Not run: CI on GitHub / Linux (not pushed); a YAML parser check (none installed); SOLIDWORKS STEP export; manual dialogs (the export and settings dialogs were not clicked); the AutoCAD script suites of `tests/run-tests.ps1`.
  - P2 portable Core (2026-10-09), final run `ElevatorAutomation/artifacts/test-results/20261009-100253`, all PASS:
    - build 7 s; tier 1 (472 s): golden 566, presets 160, rope 60, sheet 8285, FF 131, safety / pit 117, xunit-core 201 (net8.0 40 + net48 161);
    - tier 2 (222 s): presets UI 59 (57 s), rope UI 33, sheet UI 63, calculation UI 54, Basic 67, safety / pit 152, draft 117, FF UI 126;
    - tier 3 (148 s): integration (affected regressions, FF integration 22, STEP input 7, native golden 2 files compared), presets native 6, sheet Word render PASS.
    - Hashes unchanged: v01 `DC8A76A3…`, v02 `57FBE447…` (SHA256SUMS 0 mismatches), v03 `9CFD5FAB…` (SHA256SUMS 0 mismatches), and every candidate.
    - Not run: the CI workflow on GitHub / Linux (not pushed); a YAML parser check; SOLIDWORKS STEP export; manual dialogs; the AutoCAD script suites of `tests/run-tests.ps1`.
  - P1 build system (2026-10-09), run `ElevatorAutomation/artifacts/test-results/20261009-060258` on the new build, all PASS, golden files unchanged:
    - build 2 s (incremental); tier 1 (228 s): golden 566, presets 160, rope 60, sheet 8285, FF 131, safety/pit 117;
    - tier 2 (648 s): presets UI 59, rope UI 33, sheet UI 63, calculation UI 54, Basic 67, safety/pit 152, draft 117, FF UI 126. The time was machine load: presets UI took 400 s in this run, and 55 s on both the new and the old build when rerun alone;
    - tier 3 (139 s): integration (affected regressions, FF integration 22, STEP input 7, native golden 2 files compared), presets native 6, sheet Word render PASS.
    - Wrappers: `build-solution.ps1` builds; `build.ps1 -BinDirectory bin-shaftmaster-v99` is refused.
    - Not run: the phase-1 runners, `tests/run-tests.ps1` (AutoCAD script suites), `build-section-aa.ps1`, `tests/vertical/build-vertical.ps1` and `tests/project/build-project-tests.ps1` after their reference updates; SOLIDWORKS STEP export; manual dialogs.
  - P0 safety net (2026-10-09), final run `ElevatorAutomation/artifacts/test-results/20261009-053328` (local, ignored), all PASS:
    - build 8 s;
    - tier 1 (204 s): golden 566 files, presets core 160, rope core 60 + hand check, sheet core 8285, FF core 131, safety/pit core 117;
    - tier 2 (215 s): presets UI 59, rope UI 33, sheet UI 63, calculation UI 54 (workflow 25 + UI 29), Basic 67 (49 + 18), safety/pit 152 (117 + 35), safety/pit draft UI 117, FF UI 126;
    - tier 3 (229 s): integration (affected regressions 414 + four-view snapshots + 32 protected files; FF integration 22; STEP input 7; native golden 2 files), presets native 6, sheet Word render (8285 + 23 sheets / 169 pages OK, pagination PASS).
    - Golden: two compare runs identical; evidence check 237 PASS. Basic UI: 20 consecutive PASS.
    - Hashes unchanged: v01 `DC8A76A3…`, v02 `57FBE447…` (SHA256SUMS 0 mismatches), v03 `9CFD5FAB…` (SHA256SUMS 0 mismatches), presets `9CFD5FAB…`, calc-sheet `57FBE447…`, FF `D3111222…`, safety/pit `EA9B1F5E…`, ropes `519984A1…`, basic `CE1BFF69…`, calculations `1CB9FD4F…`.
    - Not run: the AutoCAD script suites of `tests/run-tests.ps1` (not part of the tiers); SOLIDWORKS STEP export; manual dialogs.
  - v03 / repository hygiene (2026-10-09):
    - v03 `SHA256SUMS.txt` 52 entries, 0 mismatches; v02 43 entries, 0 mismatches. Unchanged: v01 `DC8A76A3…`, v02 `57FBE447…`, presets `9CFD5FAB…`, calc-sheet `57FBE447…`, FF `D3111222…`, safety/pit `EA9B1F5E…`, ropes `519984A1…`, basic `CE1BFF69…`, calculations `1CB9FD4F…`.
    - `git ls-remote origin`: empty. `git rev-list --objects --all`: no removed path reachable; old commit pruned.
    - Fresh clone: build and core tests PASS (see Completed).
    - Not run: the native AutoCAD suites after the legacy move and the skip changes (`tests/run-tests.ps1`, phase-1 runners, legacy `integration-run.scr`), UI suites from the clone, SOLIDWORKS STEP export.
  - Heavy-load selection, `output/presets-heavy-20261009/final2` (2026-10-09), all PASS:
    - presets core 160 (with the Q sweep), presets UI 59, presets native 6;
    - rope core 60 + hand check 10 fixtures, rope UI 33;
    - sheet core 8285, sheet UI 63, Word render / raster / pagination PASS;
    - calculation UI 25, Basic 49, safety/pit 117, safety/pit draft UI 117;
    - FF core 131, FF UI 126, FF integration 22;
    - affected regressions PASS (32 protected files unchanged).
    - The earlier run `final` is kept as evidence of the Basic UI flake (every other suite PASS).
  - Hashes (2026-10-09): candidate `9CFD5FAB73A444FB…`; unchanged: v01 `DC8A76A3…`, v02 `57FBE447…`, calc-sheet `57FBE447…`, FF `D3111222…`, safety/pit `EA9B1F5E…`, ropes `519984A1…`.
  - Not run: SOLIDWORKS STEP export by hand; interactive dialogs.
  - Deflector policy. Definitive run `output/presets-deflectorpolicy-20261008/final/` (runners in `final/runners/`), all PASS:

    | Suite | Result |
    |---|---|
    | Presets core | 148 |
    | Presets UI | 59 |
    | Presets native | 6 |
    | Rope core | 60 + 10 hand-check fixtures |
    | Rope UI | 33 |
    | Sheet core | 8285 |
    | Sheet UI | 63 |
    | Calculation UI | 29 + 25 |
    | Basic | 49 + 18 |
    | Safety/pit | 117 + 35 |
    | Safety/pit draft | 117 |
    | FF core | 131 |
    | FF UI | 126 |
    | FF integration | 22 |
    | Affected regressions | 43 + 6 + 39 + 102 + 24 + 200; four views equal the release; 32 protected files |

    - Pagination PASS. Visual-tree scan clean (850 / 807 / 847 strings).
    - Not run: SOLIDWORKS STEP export; interactive dialogs.
  - Stage-2 selection fix. Definitive run `output/presets-selectfix-20261008/final/`, all PASS:

    | Suite | Result |
    |---|---|
    | Presets core (incl. Q sweep + brute-force order check) | 134 |
    | Presets UI | 58 |
    | Presets native | 6 |
    | Rope core | 60 + 10 hand-check fixtures |
    | Rope UI | 33 |
    | Sheet core | 8285 |
    | Sheet UI | 63 |
    | Calculation UI | 29 rendered + 25 workflow |
    | Basic | 49 + 18 |
    | Safety/pit | 117 + 35 |
    | Safety/pit draft | 117 |
    | FF core | 131 |
    | FF UI | 126 |
    | FF integration | 22 |
    | Affected regressions | 43 + 6 + 39 + 102 + 24 + 200; four views equal the release; 32 protected files |

    - Pagination PASS.
    - Visual-tree scan, removed strings and UPPER_SNAKE_CASE codes: none found (852 / 809 / 849 strings; new project, older project, after live edits).
    - Dev runs `dev-1` … `dev-11` are kept.
  - Runner scripts: the scratchpad was cleared by the system during the run. The scripts were recovered from earlier evidence and are kept in `final/runners/`.
  - Not run: SOLIDWORKS STEP export; interactive dialogs.
  - DESIGN-FLOW stage 2 presets, final build. Definitive run `output/presets-20261008/final3/`, all PASS:

    | Suite | Result |
    |---|---|
    | Presets core | 101 assertions |
    | Presets UI | 45 |
    | Presets native (new project → all four views) | 6 |
    | Rope / traction core | 60 + hand check 10 fixtures |
    | Rope / traction UI | 32 |
    | Calc sheet core | 8285 |
    | Calc sheet UI | 63 |
    | Calculation UI | 30 rendered + 25 workflow |
    | Basic | 49 core + 18 UI |
    | Safety/pit | 117 core + 35 UI |
    | Safety/pit draft UI | 117 |
    | FF core | 131 |
    | FF UI | 126 |
    | FF native / STEP integration | 22 |
    | Affected regressions | 43 rule + 6 annotation + 39 machine-room + 102 reeving + 24 project + 200 calculation; four-view snapshots equal the release; 32 protected files unchanged |

    - Pagination PASS (01 / 12 / 14 / 15). Word render: the new-project sheet is 11 pages and its Design Notes 1 page.
    - Visual-tree scan, removed strings: none found (844 strings, new project; 772 strings, older project; plus the Advanced page).
    - Sheet and Design Notes from a new project contain no forbidden string.
  - Earlier full runs, kept as evidence:
    - `final`: found a real regression. Editing a preset field cleared every result: a pit-only or h-only edit dropped the current P1–P4. Fixed in `PresetFlow.ManagedEdited`; that run's safety/pit UI and FF UI failures are fixed.
    - `final2`: Basic UI ended once without a result file (no crash event logged). It passed on the rerun, in `final3`, and in 3 more repeats (`dev-10` to `dev-13`). Cause not identified.
    - The status-bar texts "Review independent engineering statuses…" and "Engineering reviews remain independent." were removed after `final2`.
  - Intermediate folders `output/presets-20261008/dev-1` … `dev-13` are kept (nothing deleted).
  - Not run: SOLIDWORKS STEP export; manual interactive dialogs (the Save As dialog is not automated).
  - Design Notes / pagination fix, final build. Fresh folder `output/ropes-notesfix-20261008/final/`, all PASS:

    | Suite | Result |
    |---|---|
    | Rope / traction core | 60 assertions |
    | Hand check | 10 fixtures |
    | Rope / traction UI | 32 |
    | Calc sheet core | 8285 |
    | Calc sheet UI | 63 |
    | FF core | 131 |
    | Safety/pit core | 116 |

    - Sheet core covers:
      - Design Notes: no forbidden wording ("not credited", "basis", "code checks", "status", …), no body tables, no OK items, notes ≤ 2 lines;
      - Table 6 → 955 kg; the rope upsize (6 ropes / 10 mm); the traction margin; the clean "No design notes" case;
      - the termination row.
    - Word render: 23 + 5 documents, 169 + 36 pages, none outside margins. Pagination PASS for 01, 12, 14, 15 (worst non-last page 39 %).
    - Design Notes (fixtures 16–19) and the rope pages reviewed at 200 %.
    - Not run: calculation UI / affected regressions (not requested this time); SOLIDWORKS; manual dialogs.
  - Rope / traction, final candidate build. Each suite in a fresh folder under `output/ropes-20261008/final5/`, all PASS:

    | Suite | Result |
    |---|---|
    | Rope / traction core | 60 assertions |
    | Independent Python hand check | 10 fixtures; every engine value matches the EN 81-50 formulas |
    | Rope / traction UI | 32 |
    | Calc sheet core | 8240 (P1–P7 and FF numbers unchanged) |
    | Calc sheet UI | 63 |
    | FF core | 131 |
    | Safety/pit core | 116 |
    | Calculation workflow | 25 |
    | Calculation UI | 30 |
    | Affected regressions | four-view snapshots equal the release; 32 protected files unchanged |

    - Word render: 21 + 5 sheets, 169 + 36 pages, none outside margins. Pagination PASS for 01 and 12.
    - Fixtures 14/15 have a P1–P4 page at 50/58 % empty: the existing F2 flow for those shaft depths, ahead of the new section, and not part of the enforced pair.
    - New sheet pages, F10 and both Design Notes reviewed at 200 % (`review200/`).
    - Not run: SOLIDWORKS export; manual file dialogs; native DWG generation for this candidate beyond the calculation-UI run; Basic/safety-pit/FF UI suites (not requested).
  - Release check, rebuilt calc-sheet candidate, all PASS. Each suite in a fresh folder under `output/release-check-20261008/`.

    | Suite | Assertions |
    |---|---|
    | Build STEP-input | 7 |
    | Calc sheet core | 6823 |
    | Calc sheet UI | 62 |
    | Calculation workflow | 25 |
    | Calculation UI | 29 |
    | Basic | 49 |
    | Basic UI | 18 |
    | Safety/pit core | 116 |
    | Safety/pit UI | 35 |
    | Safety/pit draft UI | 117 |
    | FF core | 131 |
    | FF UI | 126 |
    | FF native/STEP integration | 22 |
    | Report-field native | 13 |

    - Affected-regression set: rules 43, annotation 6, machine room 39, reeving 102, project 24, calculations 200; four-view snapshots equal to the release; 32 protected artifacts unchanged.
    - Word render: 137 + 36 pages, none outside margins. Pagination PASS.
    - Not run:
      - SOLIDWORKS export;
      - manual file dialogs;
      - the full AutoCAD Phase 1 native matrix (`tests/run-tests.ps1`, `tests/project/run-phase1.ps1`), which writes to fixed, reused folders and was not part of the reference set.
  - FF1 direction + Phase B, final candidate build, fresh folder `output/calcsheet-phaseB-20261007/`:
    - Sheet core: PASS 6805 assertions. Covers:
      - the FF1/FF2 moment balance from the drawn arrows (F4 FF1/FF2, F5a FF1, F5b FF2) for loads at ±X/±Y in fixtures 01 and 12, plus detection of a reversed arrow;
      - F7–F9 present with their sections and omitted with them (fixtures 03/04/05/13);
      - labels: T(car) 9.614, T(cwt) 7.259, per rope 1.602/1.21, P5 9.8 per rail, P6 39.2, P7 14.8 per buffer and 29.6 total;
      - counts follow the inputs: 4 ropes, 2 car and 3 CW buffers, and 3 rails via the builder; 4 rails make P5/F8 unavailable;
      - only sourced numbers and dimensions; no text overlap; no U+002D between letters; unchanged calculation numbers.
    - Sheet UI: first run FAIL on the stale "six SVG figures" assertion. Corrected to nine, then rerun: PASS 62.
    - FF core 131, FF UI 126, safety/pit core 116: all PASS.
    - Word render (test viewer only): 17 core sheets, 137 pages; 5 UI sheets, 36 pages. All OK and unchanged by Word.
    - Pagination: PASS for 01 and 12 (worst non-last page 27 %). Fixture 13 (not enforced) has page 7 at 48 %.
    - Every figure was reviewed at 200 % (01, 12, 13, 02).
    - Not run: the FF native/STEP integration; Basic/calculation/safety-pit UI suites; manual dialogs.
  - Figfix, final candidate build, fresh folder `output/calcsheet-figfix-20261007/`:
    - Sheet core: PASS 4495 assertions. New checks cover:
      - F5 two views: FF1 arrows on opposite rails at the upper/lower levels, pointing outward; FF2 in the side view only; every arrow ends on a rail face with its tail outside the rail;
      - F4 level labels and notes;
      - F1 T symbols, DBG = CarDbg/CwDbg, W/D labels, entrance;
      - no U+002D between letters in any figure text;
      - unchanged numbers.
    - Sheet UI: PASS 62. FF core 131, FF UI 126, safety/pit core 116: all PASS.
    - Word render (test viewer only): 16 core sheets, 116 pages; 5 UI sheets, 33 pages. All OK and unchanged by Word.
    - Pagination: PASS for 01-basic-complete and 12-changed-geometry (pages 20/3/5/14/16/22/1–2/47 % empty; the last page is exempt). Not enforced for incomplete fixtures, where one page each exceeds 40 %: 04 p4, 05 p1, 08 p5.
    - Every figure was reviewed at 200 % in the Word render.
    - Not run: the FF native/STEP integration; Basic/calculation/safety-pit UI suites; manual dialogs.
  - Page layout and figures, final candidate build (each run into a fresh folder):
    - Sheet core: PASS 3198. Covers:
      - layout: A4, page border, one default header on every page, PAGE/NUMPAGES fields, fixed title-block geometry, keep-with-next, figure/caption kept together;
      - title-block cases: complete, all blank, logo only, company only, older project; the logo in the header only, no placeholders;
      - figures: present or omitted with their sections; SVG and PNG parts; SVG labels and coordinates matching the calculation (P1–P4 kN; FF 2.2 / 2.1 kN; h 1200; W 1100; D 2100; SW/SD; rail tips at railLeft/railRight; car-rail line at CarRow);
      - no overlapping text, everything inside the figure frame, text 2.5 / 3.5 mm;
      - a changed-geometry case (SW 1650, SD 2600, Q 630, h 1000);
      - unchanged numbers (FF 2200/2100 N, P5 9800 N, P6 39200 N, P7 29600/14800 N).
    - Sheet UI: PASS 62. FF core 131, FF UI 126, safety/pit core 116: all PASS.
    - 21 sheets rendered through Word, test viewer only: 161 pages, sources unchanged, no text outside margins. Pages reviewed via overviews and page renders; figures reviewed at 200% in the Word render and as 300 dpi previews.
    - Not run: the FF native/STEP integration (not requested this time); Basic/calculation/safety-pit UI suites; manual dialogs.
    - Known cosmetic issue (fixed by figfix): Word's SVG renderer added space around hyphens in figure text.
  - Title block, final candidate build (each run into a fresh folder):
    - Sheet core: PASS 1198. Covers:
      - five title-block cases (complete, all blank incl. whitespace, logo only (JPEG), company only, older project without the group);
      - shown and hidden rows, no placeholders, the approval block with empty signature/date cells;
      - the logo part only when set, with the aspect ratio kept in body and header;
      - unchanged numbers (FF 2200/2100, P5 9800, P6 39200, P7 29600/14800 N);
      - Basic/Advanced P1–P4, P5–P7 and FF still current after report edits; STEP parameters and plan geometry unchanged;
      - save/reopen, the schema, and the settings copy/replace/remove/reject cases.
    - Sheet UI: PASS 61. Covers:
      - the General-page section with blank editors;
      - report edits that keep UI results current;
      - the settings dialog with preview and save;
      - export with the company name and logo;
      - Save/reopen through the real Save handler;
      - an omission dialog that does not mention title-block fields.
    - FF core 131, FF UI 126, safety/pit core 116, FF native/STEP integration 22: all PASS.
    - 20 sheets rendered through Word, test viewer only: 70 pages; sources unchanged; no clipping. Page 1 of each was reviewed (`page1-overview.png`).
    - Not run:
      - A native DWG round trip with filled report fields. The integration test uses the reference project (Report null); STEP/plan equality with filled fields is covered in the core test.
      - Basic/calculation UI and safety/pit UI suites (not requested).
      - Manual Browse file picker and Save As dialog clicks.
  - Clean sheet, final candidate build (each run into a fresh folder):
    - Sheet core: PASS 700. This covers the removed-text list in document, header, footer and docProps; no zero placeholders; renumbering; and unchanged numbers (FF 2200/2100 N, P5 9800 N, P6 39200 N, P7 29600/14800 N).
    - Sheet UI: PASS 35, rerun PASS 35 in `ui-run2` for a corrected dialog screenshot. Covers the omission dialog content, Cancel / Export anyway, and the blocked export.
    - FF core: PASS 131. FF UI: PASS 126. Safety/pit core: PASS 116.
    - 14 sheets rendered through Word, test viewer only: 40 pages; sources unchanged; no text outside margins. Every page was reviewed.
    - Not run this time: Basic/calculation UI, safety/pit UI and native/STEP integration (not requested).
  - Calculation-sheet candidate (2026-10-07 ~02:20, final build):
    - Sheet tests: PASS 92.
    - WPF export: PASS 19.
    - FF fixture through the live project: 1100/2200 N, 1050/2100 N.
    - 16 sheets rendered through Word, test viewer only: 4–6 pages each; sources unchanged; no text outside margins; every page reviewed.
    - FF core 131, FF WPF 126, safety/pit 116 + 35, safety/pit draft 117, Basic 49 + 18, native DWG + STEP parameters 22: all PASS.
    - Build STEP-input tests: PASS.
    - `run-calculation-ui.ps1` stops at the pre-existing check "Bottom calculate control lies within viewport". The unchanged FF candidate fails the same check, so it is not caused by this task. Workflow 25: PASS.
    - Not run: SOLIDWORKS STEP export; manual Save As dialog click-through.
  - FF audit run (2026-10-07 ~01:35), on the rebuilt candidate:
    - Build: design-rule/STEP-input tests PASS.
    - Core: PASS, 131 assertions.
    - Rendered WPF: PASS, 126 assertions, including the two new interpretation checks. Screenshots are in `output/ff-audit-20261007/wpf/`.
    - Integration: PASS, 22 assertions. The STEP parameters are byte-identical to the 2026-10-06 integration run.
    - Release `DC8A76A3…2AE7` and safety/pit `EA9B1F5E…E396` are unchanged.
    - Not re-run after the rebuild: the broader affected-regression set (604 assertions) and the safety/pit UI regressions. The change is display text only.
    - Known cosmetic issue, pre-existing and not fixed: the expanded equations panel shows the `Definitions` paragraph twice.
  - Integration run (2026-10-07): PASS, 22 assertions.
    - STEP: `StepInputFactory.Create` parameters are unchanged for an absent FF group and h = null, 1200, 0, −1, NaN, +∞. `step-parameters-without-ff.json` and `step-parameters-with-ff.json` are byte-identical (SHA-256 `9A998E24…95C4`). **No SOLIDWORKS export was run.**
    - Native: the two four-view DWGs `StoredFFInput` (h = 1200) and `MissingOrMalformedDraftCanonicalH` (h = null) were generated with AutoCAD 2023 Core Console, then reopened and audited in a fresh process. Input hashes match, h and Basic mode are read back correctly, and the entity counts match (Plan 521 / MachineRoomPlan 1354 / SectionBB 709 / SectionAA 3045).
    - Every `verify.log` ends with Core Console's "Can't delete temp file … errno=13". This is pre-existing shutdown noise: it appears in 145 of 153 job logs. It is not a test failure.
  - Hashes before and after the run are unchanged:
    - FF candidate `ShaftMaster.exe` `1FAD9185…DFC1E`
    - FF candidate `Elevator.Core.dll` `41252572…9417`
    - Release `bin-shaftmaster-v01/ShaftMaster.exe` `DC8A76A3…2AE7`
    - Safety/pit candidate `ShaftMaster.exe` `EA9B1F5E…E396`
  - Earlier evidence (2026-10-06):
    - Note verification: PASS.
    - Core tests: PASS, 126 assertions.
    - WPF tests: PASS, 124 assertions (programmatic, not interactive).
    - Affected regressions: 604 assertions; four-view snapshots equal the release.
  - Not run: an end-to-end SOLIDWORKS STEP export for FF.
- **Blockers:**
  - (Resolved 2026-10-08 by Planner decisions A1–A6; kept for traceability.) Rope/traction task questions:
    1. **Emergency-braking retardation a.**
       - EN 81-50 5.11.2.2.2 gives only a minimum: 0.5 m/s² in the normal case, higher for reduced-stroke buffers. The project has no retardation or buffer-stroke data.
       - Question: use a = 0.5 m/s² (normal buffer stroke assumed), or add an input a ≥ 0.5 m/s²?
    2. **Deflector reduced mass mDP** (EN 81-50 5.11.3, counterweight-side term II in T2 for braking; mDP = J_DP·(v_pulley/v)²/R²).
       - The project has no deflector inertia, and leaving the term out is non-conservative for the loaded-car braking case.
       - Question: add an optional input (mDP in kg, or J_DP in kg·m²) and omit the braking case while it is blank, or neglect the term (to be stated in the sheet)?
    3. **Wrap angle α without a deflector.** When TZ < 520 the project's section rules give no α ("DIRECT ROPE ROUTING REVIEW"); the same applies when TZ > 1100 or there is no calculated path.
       - Question: omit the traction checks when α is unavailable, or use α = 180°?
    4. **Friction forces FRcar / FRcwt.** EN 81-50 5.11.3 says they "should be deleted … if a minimum friction force cannot be ensured". The project has no friction data.
       - Proposal: set them to zero (conservative). Please confirm.
    5. **Nequiv(t) outside Table 2** (V γ > 50°, U undercut β < 75°). The standard allows interpolation only within the table.
       - Question: omit the EN 81-50 required Sf (and so the Sf check) in that case?
    6. **Minor definitions, proposed:**
       - Proposal: the "travelling cable mass per metre" input is the total of all cables (= nt·w).
       - Proposal: T_max for Sf = (P + Q + ns·H·w)·gn / (ns·r), with H the travel height as instructed. The real hanging length also includes the sheave-to-top-landing distance, which is not in the project data. Please confirm this approximation.
       - Proposal: the stalled check covers only the counterweight-on-buffers case (μ = 0.2 is given for "counterweight stalled"; EN 81-50 5.11.2.2.3 also mentions the lowest position). It applies only where EN 81-20 5.5.3 c) 1) traction limiting is used, not the 5.5.3 c) 2) electric safety device.
  1. Release target is the user's decision: keep FF as a separate candidate, combine it with the safety/pit candidate, or replace `bin-shaftmaster-v01`. This applies to release promotion and does not block planning or implementing the next calculation in a separate candidate.
  2. ~~Open FF engineering questions~~: resolved by the user decision on 2026-10-07 (user-approved interpretation; see `CAR-GUIDE-FORCES.md`).
- **Next step:** Awaiting Planner (review and merge of `refactor/p7-tests`).

## Decisions and constraints

- Planner chat: discuss/review the project and produce prompts for the Executor. Do not implement unless the user explicitly asks.
- Executor chat: implement the user's approved request, then update this file before wrapping up or pausing.
- Keep both chats attached to `D:\CAD DATA---------------------\DWGs\2026\dwg plan codex`.
- Preserve original DWGs/backups and previously delivered binaries. Treat candidate builds separately from releases.
- For engineering work, record source assumptions and unresolved physical interpretations; do not silently guess.

## Executor update template

Replace the current-task fields and add a short entry to Recent updates whenever the Executor makes meaningful progress:

- **Status:** Not started / In progress / Blocked / Complete
- **Goal:** What the current implementation request is
- **Completed:** What is actually done
- **Decisions:** Relevant user-approved choices and assumptions
- **Changed files:** Exact paths, or “none”
- **Checks:** What ran and the result, or “not run” with reason
- **Blockers:** What is waiting on user input or an external condition, or “none”
- **Next step:** The single next action

## Recent updates

- 2026-10-09: Executor merged P4 into main (`da8335e`) and committed the Planner ARCHITECTURE-PLAN change (`426f306`). Not pushed. Next: P5 in a new Executor chat.
- 2026-10-09: Executor completed P4 "Desktop structure" and the clean-up on branch `refactor/p4-desktop` (not merged, not pushed): Core.Application services (session, preset flow, live calculation) with 19 xUnit tests, MainWindow split into Shell / Pages / Common with view-models and a test facade, UI counts unchanged, 57 of 58 screenshots identical, unused code / obsolete scripts / superseded docs removed, disk proposal written (nothing deleted). Golden unchanged; tiers 1-3 PASS. Awaiting Planner.
- 2026-10-09: Executor merged P3 into main (`723f32d`; tier 1 PASS on the branch, 552 s). Not pushed. P4 and the clean-up started on `refactor/p4-desktop`.
- 2026-10-09: Executor merged P2 into main (`a1fff15`), committed Planner section 4a (`f94d4d5`) and completed P3 on branch `refactor/p3-reporting` (not merged, not pushed): report code in the internal DLL Elevator.Reporting (netstandard2.0, net48), WPF rasterizer in Desktop, Elevator.Reporting.Tests 145 / 145, presets xUnit on net8.0. Golden unchanged; tiers 1-3 PASS. Awaiting Planner.
- 2026-10-09: Executor merged P1 into main (`01ecbe4`) and completed P2 on branch `refactor/p2-portable-core` (not merged, not pushed): Core netstandard2.0 / net48, CoreJson serializer abstraction (JavaScriptSerializer kept on .NET Framework; the compatible serializer is not a default on netstandard: 157 of 199,955 doubles differ in digit count), xUnit Core tests (net8.0 40, net48 161), CI workflow. Tiers 1-3 PASS. Awaiting Planner.
- 2026-10-09: Executor closed P0 (merged into main `ac9fdcc`, Planner files `ee898b6`) and completed P1 on branch `refactor/p1-build` (not merged, not pushed): SDK-style projects, Directory.Build.props, one solution, tools/build.ps1; same 26-file output; tiers 1-3 PASS. Awaiting Planner.
- 2026-10-09: Executor completed ARCHITECTURE-PLAN P0 on branch `refactor/p0-safety-net` (not merged, not pushed): artifacts/ build default with release guard, golden master (566 files) and native golden, tools/test.ps1 tiers, Basic UI flake fixed (external close of the test window). Tiers 1–3 PASS. Awaiting Planner.
- 2026-10-09: Executor released v03 (`bin-shaftmaster-v03`, `9CFD5FAB…`) and cleaned the repository: one clean commit on `main`, standards / third-party / inspection files and root drawings out of Git (kept on disk), legacy plugin under `legacy/`. Not pushed. Awaiting Planner.
- 2026-10-09: Executor finished the heavy-load selection (joint β / q / Dt search, heavier car masses, 2:1 recommendation). Candidate `9CFD5FAB…`, `final2` all PASS. Awaiting Planner.
- 2026-10-08: Executor applied DESIGN-FLOW 11a (deflector D/d as a Design Notes quotation only; ladder-first rope search). New project 4 × 11 mm, β 100°, all OK. All suites PASS in `presets-deflectorpolicy-20261008/final`. Candidate `E2302461…`. Awaiting Planner.
- 2026-10-08: Executor selection fix: the new project opens with every check OK (6 × 8 mm, β 100°, P 700, CW 1015). Rope / groove search, live calculation, no status codes. All suites PASS in `presets-selectfix-20261008/final`. Candidate `E617312B…`. Awaiting Planner.
- 2026-10-08: Executor completed DESIGN-FLOW stage 2 "Flow foundation" in the new `bin-shaftmaster-presets-candidate` (`5152F49F…`):
  - New project complete from presets; lock / follow / Reset; rope auto-upsize; Fill blanks; UI text cleanup. No equation change.
  - Presets 101 / 45 / 6 native; sheet 8285 / 63; rope 60 + 10 / 32; FF 131 / 126 / 22; safety/pit 117 / 35 / 117; Basic 49 / 18; calc UI 30 + 25; affected regressions; all PASS in `final3`.
  - Awaiting Planner.
- 2026-10-08: Executor rewrote the Design Notes as short advisory notes (DESIGN-FLOW 7) with computed recommendations:
  - Table 6 955 kg; rope upsize; D/d 40·d; traction margin; TZ range.
  - Termination row fixed. The rope section flows and pagination passes for 01/12/14/15.
  - Sheet 8285 / 63, rope 60 + 10 / 32, FF 131, safety/pit 116: all PASS. Candidate `519984A1…`.
  - 50 intermediate folders moved to the Recycle Bin. Awaiting Planner.
- 2026-10-08: Executor completed the rope / traction checks in the new `bin-shaftmaster-ropes-candidate` (`84E556C9…`):
  - EN 81-20 5.5 and EN 81-50 5.11/5.12 with Planner decisions A1–A6;
  - `config/presets.json` with generic lock/follow;
  - sheet section and F10; Design Notes export with Table 6.
  - Core 60 + hand check 10, UI 32, sheet 8240/63, FF 131, safety/pit 116, calculation 25/30, affected regressions: all PASS. v01 and v02 unchanged. Awaiting Planner.
- 2026-10-08: The Planner added `DESIGN-FLOW.md` (the agreed design flow, presets, ladders and build stages), referenced it from `CLAUDE.md` with a product principle, and narrowed the "do not guess" constraint to engineering interpretation. No code changes. The answers to the rope/traction blockers come in the next Planner prompt.
- 2026-10-08: Executor reviewed EN 81-20 5.5 and EN 81-50 5.11/5.12 (with Annexes D/E) for the rope/traction task. No code or candidate was created. Stopped on 5 engineering questions (braking retardation, deflector inertia, α without a deflector, Table 2 range; FR = 0 to be confirmed) plus 3 definition confirmations. See Blockers. Awaiting Planner.
- 2026-10-08: Executor promoted the release-checked calc-sheet candidate to v02 (`bin-shaftmaster-v02`, `57FBE447…`). It was copied unchanged, and the SHA256SUMS verify. v01 is unchanged. Awaiting Planner.
- 2026-10-08: Executor ran the final fixes and a full release check of the calc-sheet candidate. Not promoted.
  - Fixes: F9 arrows on the buffer axes; FF Definitions shown once; per-button viewport test.
  - All suites PASS (`output/release-check-20261008/RELEASE-CHECK.md`).
  - Candidate `57FBE447…`. Awaiting Planner.
- 2026-10-08: Executor fixed the FF1 direction (statics convention from the load-offset sign) and added the Phase B figures.
  - Tests: moment-balance test; F1 true-scale rails with leaders.
  - New figures: F7 suspension, F8 safety gear, F9 pit buffers.
  - Sheet core 6805, UI 62, FF 131/126, safety/pit 116: all PASS.
  - Candidate `F212F0F9…`. Awaiting Planner.
- 2026-10-07: Executor applied the figfix corrections.
  - F5: front and side views with FF1/FF2 couples.
  - F4: level labels.
  - F1: T rails, DBG dimensions, entrance.
  - No word hyphens in figure text; FF figures after the FF text, so the complete sheet is 8 pages with no page over 40 % empty.
  - Sheet core 4495, UI 62, FF 131/126, safety/pit 116: all PASS.
  - Candidate `61015D45…`. Awaiting Planner.
- 2026-10-07: Executor added the CAD-style page layout and Phase A figures F1–F6 to the calculation sheet.
  - Page frame, title-block header on every page, footer band.
  - Figures as SVG with PNG fallback, in caption frames, built from the calculation data.
  - Sheet core 3198, UI 62, FF 131/126, safety/pit 116: all PASS. 161 rendered pages reviewed.
  - Candidate rebuilt (`EAD0C9E6…`), previous build backed up.
- 2026-10-07 ~14:45: Executor added the optional title block and approval block to the calculation sheet.
  - Company name/logo come from application settings; client, document no., revision and prepared/checked/approved by come from the new optional `Report` project group.
  - Added a reusable DocxWriter image API.
  - Sheet core 1198, UI 61, FF 131/126, safety/pit 116, FF native/STEP 22: all PASS.
  - Candidate rebuilt (`329969FB…`), previous build backed up.
- 2026-10-07 ~13:30: Executor made the calculation sheet a clean client-facing report (text and layout only).
  - Disclaimers, traceability and missing-data rows removed; unavailable calculations omitted with renumbering.
  - Export-anyway/Cancel dialog added, and the export is blocked when nothing is available.
  - Sheet core 700, UI 35, FF 131/126, safety/pit 116: all PASS. 40 rendered pages reviewed.
  - Candidate rebuilt (`B74AB61F…`), with the previous build backed up.
- 2026-10-07 ~02:20: Executor added the Word calculation-sheet export in the new `bin-shaftmaster-calcsheet-candidate`.
  - 92 + 19 new assertions; 16 sheets rendered and reviewed page by page.
  - FF, safety/pit, Basic and native/STEP regressions PASS on the candidate.
  - The pre-existing calculation-UI viewport check fails on both this candidate and the FF candidate.
  - Release and the other candidates are untouched.
- 2026-10-07 ~01:35: Executor audited FF1/FF2 against the note, code, tests and UI.
  - Equations and fixture are correct.
  - Fixed the stale "unresolved interpretation" UI/engine text so it shows the approved interpretation. Text only; math unchanged.
  - Added 7 regression checks and rebuilt the FF candidate.
  - Core 131, WPF 126 and integration 22: all PASS.
  - Word export not started.
- 2026-10-07 ~01:20: The Planner removed the Codex-era transition summary `CLAUDE_HANDOFF.md` at the user's request, because it was stale and still listed the FF questions as open. Its pointer was also removed from `CLAUDE.md`. `CALCULATION_ROADMAP.md` is kept. No code or docs were changed.
- 2026-10-07 ~01:10: Executor recorded the user-approved FF1/FF2 engineering interpretation (EN 81-50 C.2.1 basis; FF1 = Fy, FF2 = Fx, per rail per shoe level, h = shoe centre-to-centre). Updated `CAR-GUIDE-FORCES.md` and its bundled copy (SHA-256 `8BC248AC…E4AA8`, identical) and `CALCULATION_ROADMAP.md`, which now lists the guide-rail check as the Pending next stage. Documentation only. Remaining blocker: release target.
- 2026-10-07 ~01:00: Executor ran the FF native/STEP integration regression: PASS, 22 assertions. STEP parameters are identical with and without h, and both DWGs were generated, reopened and read h back correctly. Docs and the bundled copy were updated, and the protected hashes are unchanged. The release decision remains with the user.
- 2026-10-07: Claude Code session ran a read-only review of the FF1/FF2 candidate and recorded its state. The FF integration run was found to be never executed.
- 2026-10-07: Handoff protocol created.
- 2026-10-06 21:24–21:29: FF1/FF2 candidate built, with core/WPF/regression evidence in `output/ff-ui-20261006/` and `output/ff-source-review-20261006/`. This entry comes from file timestamps; that session's transcript is not available here.
