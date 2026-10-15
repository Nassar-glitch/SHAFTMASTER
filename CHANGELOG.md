# Changelog

All notable changes to ShaftMaster. Releases are folders `ElevatorAutomation/bin-shaftmaster-vNN/` with their own `RELEASE-NOTES.md` and `SHA256SUMS.txt`
(written by `ElevatorAutomation/tools/release.ps1`). `tools/release.ps1` takes the section `## [vNN]` of this file as the release notes; when a release
is prepared, the entries of "Unreleased" move to a new `## [vNN]` section.

## [Unreleased]

## [v07] — 2026-10-15

v07 adds the guide-rail check with its RAILS page, the steel shaft structure (UPN columns, beams and bracing) in all four views and on the
A0 sheet, and editable drawings for the steel items. The calculations and the drawings of concrete projects are unchanged; the guide-rail
check adds its Rails group to the project file, the calculation sheet and the Design Notes.

- **Guide-rail check and the RAILS page (R1, R2):** the guide-rail bending, deflection and bracket-spacing check of the car and
  counterweight rails (EN 81-50 5.10) with a recommended profile and the largest spacing that passes. The RAILS page holds the inputs
  (profiles, bracket spacing, machine on rails, shoe type and lining, deflection); each follows its preset until typed, is locked when edited and has
  Reset. Values that do not apply (machine values when the machine is not on the rails, the fish-plate force only above 40 m of travel, the lining
  only for sliding shoes) are not shown; the counterweight spacing follows the car spacing. The results are in the calculation sheet and the Design
  Notes.
- **Steel shaft structure:** the shaft construction (Concrete / Steel structure) is chosen on the SHAFT page. A steel shaft is four corner
  columns (UPN 140 preset, web outside) with ring beams (UPN 100) on the clear faces, the columns flush with the beams' outer faces, base plates
  with bolts on a concrete base, and the double-cross bracing (L50x50x5). Every value follows its preset until edited. The beam levels follow the car
  rail bracket spacing of the RAILS page per face (a beam at every floor, the gaps between divided equally, the door-head beam on the entrance
  face) and can be edited per face; the rail check uses the largest side or back gap. The tower is in the shaft plan, in the tower-top plan (the
  machine on the top ring, the machine supports and beams within the tower, the control panel on the landing, the dimension from the governor to the
  machine), in section A-A (tower elevation, bracing, base, machine housing, platform) and in B-B (no concrete heights). The building slab edge in
  front of the entrance and the machine housing are drawn from editable presets (Advanced on the SHAFT page).
- **The A0 sheet for a steel shaft:** the masonry notes are left out and a STEEL entry is in the legend; the arrangement takes the larger scales
  where the sheet has room (1:40 for A-A); the door section stays; the concrete-anchor rail detail is left out until its steel version is drawn.
  The calculation sheet has a "Shaft structure" block with the member list and the beam levels of each face.
- **Editable drawings (steel items):** every pointing label is one MULTILEADER (style SM_LEADER); the column section, the base plate with its
  bolts and the control panel are named blocks (SM_COLUMN_*, SM_BASE_PLATE_*, SM_CONTROL_PANEL); members and annotation are on meaningful layers
  (S-COLUMN, S-BEAM, S-BRACE, S-PLATE, S-BASE, S-HOUSING, A-SLAB, A-ANNO, A-EQUIP, A-DIMS); hatches are associative. The audit of the whole
  generated drawing, with proposals for the template, is `ElevatorAutomation/docs/design/EDITABILITY.md`.
- **Label placement without overlap:** the labels of the steel tower are placed so that no text lies on another text, a dimension text, a
  dimension, extension or axis line or the slab edge, and no leader crosses a text; the generated drawings are checked for this.
- **Tests:** new suites steel-ui (the SHAFT page), rails-ui (the RAILS page) and steel-native (the steel drawings read back from the DWG, with
  the overlap check).

## [v06] — 2026-10-13

v06 adds the A0 geared drawing sheet: every Generate Drawings run writes the drawing with a composed A0 sheet, its PDF and the project file.
Calculations, presets and the four generated views (except the removal of the B-B remark texts, below) are unchanged.

- **The A0 geared drawing sheet:** the generated DWG carries the layout SM_Geared_A0 built from the template (`ElevatorAutomation/Templates/`). The
  views are placed by an automatic arrangement that follows the sheet composition of the user's own edit: the machine-room plan above the shaft plan,
  the section A-A with the fish plate above it, the section B-B on top right with the door, the guides and brackets and the rail-bracket elevation under
  it; each view takes the largest standard scale that fits, a tall A-A takes its column. Every view and detail title states its scale on the sheet
  (one line, 5 mm printed). The title block is filled from the project, the company settings and the calculations (loads P1-P7 in kg, speed, travel,
  stops, supply, control, dates, revision, names); the company, client, project and drawing names are written in the xarab font for Arabic text.
  See `ElevatorAutomation/docs/design/layouts/`.
- **One company / report settings screen:** the company name (as typed, Arabic or English), the logo and the default prepared / checked / approved
  names are one set of settings, used by the calculation sheet, the Design Notes, the title block and the file names. A project's own value wins
  while it is not blank. Client, document number, revision and phase stay in the project (General page).
- **Revision NN everywhere:** the revision is a number (00, 01, 02 …), shown the same on the sheet, the title block, the calculation sheet and the file
  name; a blank revision is 00. A stored non-number keeps loading and shows the validation message, never changed silently.
- **The output set:** one Generate Drawings run writes the DWG (with the sheet), the PDF of that sheet and the project file under one name
  `COMPANY-TYPE-CW-W{SW}D{SD}-PROJECT-MODEL{NN}` into the folder the user chooses. When any of the three files exists the program asks: replace them,
  or raise the revision to the next NN, or cancel; nothing is replaced silently. An error of the PDF step keeps the DWG and the project file and says
  what failed.
- **PDF with the logo:** the PDF is plotted from the layout by the AutoCAD Core Console plot engine (DWG To PDF, ISO full bleed A0, monochrome.ctb,
  1:1). The company logo is put onto the page at the logo area with **PdfSharp 1.50.5147, which is MIT licensed** (`PdfSharp.dll` in the program
  folder; no other component is added). Without a logo nothing is added.
- **Remark texts removed from B-B:** the status block beside the machine in the section B-B ("Beam PRELIMINARY", "Concrete NOT EVALUATED", "REVIEW
  REQUIRED", "NOT YET EVALUABLE" ...) is no longer drawn in any generated drawing; such items belong in the Design Notes only.

## [v05] — 2026-10-12

v05 adds automatic car and landing doors (2S side opening, 2C centre opening) with their drawings, the Doors Offset, drawing-quality rules for the door plan, 
and loads also shown in kg. Manual doors, existing projects and every calculation result are unchanged.

- **Loads also in kg:** wherever a load or force is shown in N or kN (application pages, calculation sheet, figures, Design Notes) its kg value is shown
  beside it: kg = N / 9.81 rounded up to the next multiple of 5 kg. Display only; every calculation and OK / NOT OK result keeps the unrounded newtons.
  See `ElevatorAutomation/docs/design/loads-kg/`.
- **Automatic doors, D4 (Doors Offset):** a "Doors Offset" field on the door page (2S and 2C): the distance in mm from the shaft centre line to the
  opening centre line and a Left / Right selector, as seen in the plan drawing. It replaces "Opening offset, preferred". Default: 2S the preferred offset
  from the returns (with the automatic shift in narrow shafts), 2C centred. The allowed range is shown under the field; a value outside it is kept,
  flagged "out of range: max N Left" (Design Notes and the export warning give the limit) and drawn / exported at the nearest allowed position.
  The plan drawing shows the offset dimension for 2C too; figure F1 uses the D3 door style. Old projects with a locked opening offset give the
  same position. See `ElevatorAutomation/docs/design/doors-d4/`.
- **Automatic doors, D3 (drawing quality of the door plan):** the landing and car door panels sit inside a door frame (one rectangle each,
  aligned), the fixed-side jamb post is drawn in both frames, panels continue into the stacking pocket, and the dimensions are arranged
  (chains on one line, equal rows, extension lines from the real edges, a text too long for its interval placed outside or beside the chain).
  Follows the user's own correction of the 2S drawing; values and Manual drawings are unchanged. See `ElevatorAutomation/docs/design/doors-d3/`.
- **Automatic doors, D1 (Core and UI; no drawing change yet):** door type Manual / Automatic 2S / Automatic 2C on the Car & Doors page, optional clear
  opening (700 / 800 / 900 / 1000 / 1200; selected by the rules when empty), 2S handing, and an Advanced dimensions expander with every clearance
  (lock, follow, Reset). The front zone sets the car depth; a size or type that does not fit is reported with the needed car or shaft width (field
  message and Design Notes). Manual doors and existing projects are unchanged. See `ElevatorAutomation/docs/design/DOORS.md`.

- **Automatic doors, D2 (drawings):** the native plan draws a 2S / 2C door from the door layout (closed car and landing panels, landing frame and
  pocket, car returns, sill lines, wall opening, dimensions in the style of the reference drawings; handing mirrored); the machine-room and A-A
  recalculations use the door of the project; the calculation-sheet figure F1 and the STEP model show the door. Manual drawings are unchanged.
  Rules corrected: the returns and offsets of the references are preferred values, the minimums bind (see `docs/design/DOORS.md`).

## [v04] — 2026-10-10

v04 is the first release built from the refactored sources (ARCHITECTURE-PLAN P0–P8). Internal; no user-visible change: no equation, preset, output text, drawing entity or file format differs from v03
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
