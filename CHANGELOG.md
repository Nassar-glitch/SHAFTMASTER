# Changelog

All notable changes to ShaftMaster. Releases are folders `ElevatorAutomation/bin-shaftmaster-vNN/` with their own `RELEASE-NOTES.md` and `SHA256SUMS.txt`
(written by `ElevatorAutomation/tools/release.ps1`). `tools/release.ps1` takes the section `## [vNN]` of this file as the release notes; when a release
is prepared, the entries of "Unreleased" move to a new `## [vNN]` section.

## [Unreleased]

Internal refactor ARCHITECTURE-PLAN P0–P8. No user-visible change: no equation, preset, output text, drawing entity or file format differs from v03
(golden-master tests and the four-view comparison with the release Core pass; the v01, v02 and v03 files are unchanged).

- **P0 Safety net:** golden-master capture of 36 fixtures (results, fingerprints, project JSON round trips, calculation-sheet and Design Notes text, SVG
  figures), the native four-view snapshot comparison, and `tools/test.ps1` tiers 1–3.
- **P1 Build system:** SDK-style projects, `Directory.Build.props`, one `ElevatorAutomation.sln`, `tools/build.ps1` building into `artifacts/bin`
  (a release folder is written only with `-Release`).
- **P2 Portable Core:** `Elevator.Core` targets netstandard2.0 and net48 behind a JSON serializer abstraction (proven equal to `JavaScriptSerializer`
  on all fixtures); xUnit tests on net8.0 and net48; CI.
- **P3 Reporting:** calculation sheet, Design Notes, DOCX writer and figures moved to `Elevator.Reporting` (netstandard2.0 + net48); the WPF
  rasterizer stays in the desktop application.
- **P4 Desktop structure:** one view per page; preset flow, project session and live calculation moved to UI-free application services in Core.
- **P5 Core clean-up:** `.editorconfig`, one type per file, folders by lift component, long methods split, short XML docs.
- **P6 Adapters clean-up:** the same standard for `Elevator.AutoCAD` (by output: Plan, MachineRoom, SectionAA, SectionBB, Vertical, Common), Host, Bridge
  (one file per contract, explicit contract version) and StepExport.
- **P7 Test consolidation:** xUnit is the single home of the Core and Reporting tier-1 tests; tier 1 runs in about 2.5 minutes as parallel suites;
  rope selection and geometry caches; flaky JSON-order comparisons fixed; `tools/test.ps1` with `-Suite` and `-List`.
- **P8 Repository polish:** `docs/` reorganised, a README per project, this changelog, `tools/release.ps1`, legacy outputs that no test reads untracked.

## [v03] — 2026-10-09

v03 is an unchanged copy of the presets candidate that passed `output/presets-heavy-20261009/final2`. `ShaftMaster.exe` SHA-256 `9CFD5FAB…`.

- **Rope and traction checks** (EN 81-20 5.5; EN 81-50 5.11 / 5.12): rope diameter and count, rope safety factor with the equivalent number of pulleys,
  sheave D/d; traction in loading, emergency braking and stalled cases; figure F10 (suspension and traction) in the calculation sheet.
- **Design Notes export:** a separate document with the warnings, recommendations and notes for the project. The application, drawings and calculation
  sheet carry no remarks.
- **Preset library with lock / follow** (`presets.json`): a new project opens complete (survey example, rated load, speed, masses and counterweight,
  ropes, sheave, safety gear and buffers); an edited value is locked, unlocked values follow upstream changes, Reset unlocks; older projects open
  unchanged, with "Fill blanks from presets" per page and for the whole project.
- **Automatic selection:** ropes, groove undercut β, balance factor q and traction sheave are selected jointly, in that order of preference, from the
  unlocked values; the first combination with every check OK is taken.
- **Live calculation:** results update about 300 ms after any input; there are no Calculate buttons.
- **UI text cleanup:** hints, disclaimers and status codes removed; statuses are plain words.
- Scope: geared traction, 1:1 roping. Rated loads that cannot pass every rope and traction check on 1:1 get a recommendation in the Design Notes.

## [v02] — 2026-10-08

v02 is an unchanged copy of the release-checked calculation-sheet candidate. `ShaftMaster.exe` SHA-256 `57FBE447…`.

- **Safety gear and pit loads P5–P7:** P5 per car guide rail, P6 car-buffer reaction, P7 counterweight-buffer reaction, each with a per-buffer value and
  a total; a separate workflow on the Calculations page.
- **FF1/FF2 car guide-rail forces:** FF1 along the car-rail line, FF2 perpendicular to it; per guide rail at one guide-shoe level, ×2 design factor;
  h is the vertical guide-shoe spacing. The equations are adopted from the source calculation note.
- **Word calculation-sheet export** (`Calc Sheet…`): clean client format (missing calculations are omitted after an Export anyway / Cancel dialog),
  CAD-style page frame and title-block header, approval block, optional company logo and name, optional report fields (client, document number,
  revision, names).
- **Nine CAD-style figures** in the sheet, as SVG with a PNG fallback: shaft plan, P1–P4 plan and free body, suspension (1:1 roping), progressive
  safety gear, pit buffers, FF load cases, guide-shoe forces, car guide-rail section.

## [v01] — 2026-10-05

First ShaftMaster desktop release (WPF, .NET Framework 4.8), `ShaftMaster.exe` SHA-256 `DC8A76A3…`. The existing calculations, project model,
floor/stop behaviour, AutoCAD updaters, STEP geometry and export services were retained and given the ShaftMaster shell.

- ShaftMaster branding and a responsive shell: native title bar, DPI-aware manifest, scrollable navigation and content, project and workflow actions
  grouped, live project name and dirty indicator, busy state that locks inputs during long operations, About page with system requirements.
- Nine project pages plus About; Generate Drawings (AutoCAD 2023) and STEP export (licensed SOLIDWORKS) retained.
- The original approved logo is embedded byte for byte; a simplified symbol is bundled as PNG and as a seven-resolution ICO for the EXE.
- Details: `ElevatorAutomation/docs/releases/SHAFTMASTER-UI-RESULTS.md`.
