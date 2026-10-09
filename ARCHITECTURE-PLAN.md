# ShaftMaster architecture plan

Written by the Planner on 2026-10-09 from the current source (release v03, commit `1125152`). It is the reference for the clean-up and refactor. Product behaviour is governed by `DESIGN-FLOW.md`; this file governs code structure only. Only the Planner changes this file, and only with the user's agreement.

## 1. Current state (facts)

| Project | Role | Size (C#) | Target / dependencies |
|---|---|---|---|
| `Elevator.Core` | Project model, rules, geometry, calculations, presets | 43 files, ~289 k chars | .NET Framework 4.8; `System.Web.Extensions` (JavaScriptSerializer) |
| `Elevator.AutoCAD` | Native drawing generation and reading | 33 files, ~204 k chars | net48; AutoCAD 2023 (AcDbMgd/AcCoreMgd/AcMgd) |
| `Elevator.Solution.Host` | AutoCAD Core Console entry point | 1 file | net48; AutoCAD |
| `Elevator.Solution.Bridge` | Request/response contracts, Core Console job client | 5 files | net48 |
| `Elevator.Solution.StepExport` | SOLIDWORKS STEP worker | 1 file | net48; SOLIDWORKS interop |
| `Elevator.Solution.Desktop` | WPF app (`ShaftMaster.exe`), plus calculation sheet, Design Notes, DOCX writer, figures, preset flow | 24 files, ~292 k chars | net48; WPF; references Bridge and StepExport |

Observations:
- **Build:** the `.csproj` files exist (old format), but the real build is `build.ps1` / `build-solution.ps1` calling `csc.exe` directly. The default output folder of `build-solution.ps1` is the release folder `bin-shaftmaster-v01`, which is a hazard. Every build recompiles everything.
- **Code style:** long one-line members are common (235 lines over 300 characters), and a few classes sit on one line. Average line length is about 87 characters, so the effective code size is roughly 3× the line count.
- **Desktop:** `MainWindow` is one `partial class` spread over 10 files. UI, workflow, preset flow, report generation and figure rendering all live in the WPF project. Reporting cannot be tested without WPF.
- **Serialization:** `JavaScriptSerializer` (18 uses) serves project files, contracts and calculation fingerprints. It is not available on .NET Standard/.NET, which blocks a portable Core.
- **Tests:** 20 custom console test programs (`tests/solution/*.cs`, `Check()` + JSON result), 20 PowerShell runners, plus AutoCAD script suites (`tests/*.scr`) and Python render checks. There is no test framework, no tiering and no CI. A full run takes 10–15 minutes (AutoCAD Core Console, Word rendering of ~200 pages, WPF screenshots).
- **Assembly-name coupling:** about 107 references to `NETLOAD`, `Elevator.Solution.Host.dll` or `Elevator.AutoCAD.dll` in scripts and code. Renaming assemblies is expensive.

## 2. Target architecture

```
src/
  Elevator.Core             netstandard2.0 + net48   domain: model, rules, geometry, calculations, presets/selection
  Elevator.Reporting        netstandard2.0 + net48   calculation sheet, Design Notes, DOCX writer, figure model, SVG renderer
  Elevator.Solution.Bridge  netstandard2.0 + net48   contracts + Core Console job client
  Elevator.AutoCAD          net48 (AutoCAD)          native drawing adapter
  Elevator.Solution.Host    net48 (AutoCAD)          Core Console entry point
  Elevator.Solution.StepExport  net48 (SOLIDWORKS)   STEP worker (separate process)
  Elevator.Solution.Desktop net48 WPF                views + view-models only; PNG rasteriser for figures
tests/
  Elevator.Core.Tests       xUnit, net8.0 + net48    tier 1
  Elevator.Reporting.Tests  xUnit, net8.0 + net48    tier 1
  Elevator.Desktop.Tests    net48 (Windows)          tier 2 (programmatic WPF)
  integration/              existing native/Word/STEP suites, tier 3
tools/   build, release, test runner scripts
docs/    architecture, design flow, calculations, presets, release notes
```

Dependency rules, enforced by an architecture test:
- Core depends on nothing project-specific.
- Reporting depends on Core.
- Bridge depends on Core.
- The AutoCAD adapter and Host depend on Core and Bridge.
- StepExport depends on Bridge.
- Desktop depends on Core, Reporting and Bridge, and NOT on StepExport (it launches the worker as a process). Desktop then builds without SOLIDWORKS installed.
- No WPF, AutoCAD, SOLIDWORKS or Office types in Core, Reporting or Bridge.

Names: assembly and namespace names stay `Elevator.*` during the refactor, because of the coupling in section 1. A rename to `ShaftMaster.*` is a separate, optional, last phase.

## 3. Rules for every phase

1. **Behaviour-preserving.** No equation, preset, output text, drawing entity or file-format change unless the phase says so. The golden-master tests (phase 0) must pass unchanged at the end of every phase.
2. **Small and resumable.** Each phase fits one session. It ends green, committed on a branch `refactor/pN-…`, and merged into `main` after review. A phase that runs out of budget stops at the last green commit.
3. **Test tiers:**
   - **Tier 1, fast (seconds):** Core and Reporting unit and golden tests. Run after every change, and in CI.
   - **Tier 2, medium (about 1–2 min):** programmatic WPF tests, and DOCX content checks without Word. Run at the end of a phase that touches Desktop.
   - **Tier 3, slow:** AutoCAD native generate/reopen, four-view snapshots versus release, Word render, STEP. Run only before a release, or when a phase touches the AutoCAD/STEP adapters.
4. **No release folders touched.** Builds go to `artifacts/`. Releases go through a release script to `ElevatorAutomation/bin-shaftmaster-vNN/` with release notes and SHA256SUMS, only on the user's request.
5. **Serialization compatibility.** Project files (including the JSON embedded in DWGs) must stay readable both ways: v03 must open files written after the refactor, and the reverse. Any serializer change needs byte-level golden tests first.

## 4. Phases

| Phase | Content | Where | Tiers at the end |
|---|---|---|---|
| **P0 Safety net** | Golden-master capture: calculation results for fixed fixtures (P1–P4 Basic/Advanced, ropes, P5–P7, FF, rope/traction, Q sweep), project JSON round-trip bytes, calculation-sheet and Design Notes `document.xml` text, figure SVG text, and the four-view snapshots versus release. Make `build-solution.ps1` default to `artifacts/` (remove the v01 default). Fix the Basic UI flake. | Local | 1 + 3 |
| **P1 Build system** | SDK-style `.csproj` for every project, `Directory.Build.props` (LangVersion, warnings, output to `artifacts/`), configurable AutoCAD/SOLIDWORKS paths, one `ElevatorAutomation.sln`. `tools/build.ps1` wraps `msbuild`/`dotnet build` with incremental builds. Binaries must be functionally identical (golden tests). Remove the csc scripts once parity is proven. | Local | 1 + 3 |
| **P2 Portable Core** | Core multi-targets netstandard2.0/net48. A serializer abstraction replaces direct JavaScriptSerializer use; its implementation is chosen only after golden tests prove identical project JSON and fingerprints. Port the Core test programs to xUnit (Elevator.Core.Tests). GitHub Actions: an ubuntu job builds Core and runs tier 1. **After P2, Core work can run in cloud sessions.** | Local, then cloud | 1 (CI) + 3 |
| **P3 Reporting extraction** | Move the calculation sheet, Design Notes, DOCX writer, figure model and SVG renderer from Desktop into Elevator.Reporting. A PNG rasteriser interface stays in Desktop (WPF). Add DOCX content tests without Word (tier 1). Remove StepExport from Desktop's references. | Cloud for the move and tests, local for the check | 1 + 2 |
| **P4 Desktop structure** | Split the `MainWindow` partial class into one view per page plus view-models. Move preset flow, project session and live-calculation orchestration into Core as UI-free application services. | Local | 1 + 2 |
| **P5 Core clean-up** | Formatting (`.editorconfig`, `dotnet format`), one type per file, no members on one line, a 140-character line limit, long methods split. Group folders by domain (Shaft, Car, MachineRoom, Suspension, Rails, SafetyPit, Reports), remove dead code, add XML docs on public APIs only. Several small PRs. | Cloud | 1 |
| **P6 Adapters clean-up** | The same standards for Elevator.AutoCAD, Host, Bridge and StepExport, and contracts versioned explicitly. | Local | 1 + 3 |
| **P7 Test consolidation** | Port the remaining console tests to xUnit where they do not need AutoCAD/Word. One entry point, `tools/test.ps1 -Tier 1|2|3`. Retire the duplicated runners. Remove the Word render from tiers 1–2 (keep 3–4 representative sheets in tier 3). | Local + cloud | 1 + 2 + 3 |
| **P8 Repository polish** | `docs/` reorganised (architecture, design flow, calculations, presets, releases), a README per project, CHANGELOG, `tools/release.ps1`, v03 as a GitHub Release asset. Legacy outputs under `legacy/` removed from tracking. Optional `Elevator.*` → `ShaftMaster.*` rename. | Local | all |

Feature work (DESIGN-FLOW stages 3+) resumes after P4. From then on, every feature lands in the new structure with tier-1 tests.

## 5. Known risks

- **Serializer change:** double formatting, null handling and dictionary order can change project JSON and fingerprints. Mitigation: P0 golden bytes, then the P2 abstraction is proven before any switch.
- **AutoCAD loading:** the Host/AutoCAD assembly names and the `NETLOAD` paths must not change before P8, and Core Console job tests guard them in tier 3.
- **WPF behaviour:** view-model extraction can change live-calculation timing and lock/follow. Mitigation: the tier-2 preset and UI suites run unchanged.
- **Long paths:** clone into a short folder (already documented in README).
