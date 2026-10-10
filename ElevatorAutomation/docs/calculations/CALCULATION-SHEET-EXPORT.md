# Calculation sheet export (.docx) — candidate

Internal document. The client-facing sheet deliberately contains no traceability, workflow or disclaimer text; that record is kept here and in `CAR-GUIDE-FORCES.md`.

**Workflow → Export Calculation Sheet…** builds the sheet, warns about omitted calculations, then opens a Save As dialog. In narrow windows the button is labelled "Calc Sheet…". The suggested file name is `<project name> - Calculation Sheet.docx`; invalid filename characters are replaced, and a blank name becomes `Untitled project`. The sheet is an editable WordprocessingML document written with `System.IO.Packaging` (`DocxWriter.cs`). Microsoft Word is **not** required.

The candidate package is `bin-shaftmaster-calcsheet-candidate/`, built with `build-solution.ps1 -BinDirectory bin-shaftmaster-calcsheet-candidate`. The pre-clean build and sources are kept in `output/calcsheet-20261007/pre-clean-backup/`. The release `bin-shaftmaster-v01/`, the FF candidate and the safety/pit candidate are untouched. CAD templates are unchanged, and calculation loads are not published on drawings.

## Client document (clean output, 2026-10-07)

The sheet contains calculation content only, laid out as an engineering calculation note on A4 with the existing table styling.

- **Title block:** "Elevator Calculation Sheet", then a table of project, date, elevator system and rated load. It is followed by exactly one design-basis line: `Design basis: EN 81-20:2014 and EN 81-50:2014.` There is no compliance, certification or approval wording.
- **Header / footer:** see "Page layout and figures" below (the CAD title block and footer band replace the earlier plain header and footer).
- **docProps:** Title `Elevator Calculation Sheet · <project>` and Creator `ShaftMaster`. No Subject.
- **Sections:** numbered consecutively over the sections actually present.
  1. Calculation summary: Calculation | Key result.
  2. Input data: Input | Value. Only supplied, valid, nonzero inputs used by the calculations present.
  3. Then, as available:
     - Machine-room bearing reactions P1–P4 (Basic or Advanced method text).
     - Suspension rope tensions. The per-rope column appears only when the rope count is known.
     - Safety-gear and buffer loads P5–P7: P5 per car guide rail; P6/P7 per buffer and total.
     - Car guide-rail forces FF1 / FF2.
- **Each calculation section:** a short technical description of the force and where it acts, equations with full substitutions and units, and a results table.
  - The 10 N/kg coefficient, max(40, 4 × 9.81) N/kg and the ×2 design factor appear as values in the equations.
  - FF text states that FF1 acts along the car-rail line (car width) and FF2 perpendicular to it (car depth), per guide rail at one guide-shoe level. h is defined as the centre-to-centre distance between the upper and lower car guide shoes.
- **Text removed from the client document** (now kept only in internal documents):
  - preliminary / review / not-certified / compliance banners, header and footer;
  - the INCOMPLETE banner and missing-input list;
  - the source note name, SHA-256, paragraph numbers, paraIds and equation hashes;
  - rule and model identifiers, "engine record", project GUID, application/executable name, result-currency and fingerprint text;
  - "adopted", "calculated under …", the interpretation label and date, "reference only", "SYNTHETIC" and the equal-sharing caveat;
  - the former "Rule sources, assumptions and limitations" section;
  - statements of what is not included or not covered;
  - summary Basis/Status columns and inputs Page/Used-by columns;
  - the duplicated FF interpretation paragraph;
  - all NOT PROVIDED / INVALID TEXT / UNAVAILABLE / not-included rows.

## Page layout and figures (2026-10-07)

- **Page frame:** A4 portrait. Every page has a Word page border (`w:pgBorders`, offset from the page, about 10 mm from the edge) with a thin 0.5 pt outer line and a thick 1.5 pt inner line. Body margins are 20 mm left/right, so the body has about 10 mm padding inside the frame.
- **Header title block on every page**, page 1 included (one default header, no first-page variant). It is a bordered table, heavy outside and thin inside, with fixed columns 1500 | 3000 | 3038 | 2100 twips and a fixed 18 mm height:
  1. Logo, fitted with its aspect ratio kept. Without a logo, this cell merges with the next and the company name and title use the space.
  2. Company name over "Elevator Calculation Sheet".
  3. Nested Project / Client rows.
  4. Nested Doc. No. / Rev. / Page rows. Page X of Y uses PAGE / NUMPAGES fields.

  Labels are 8 pt on a light shade and values 9 pt. Blank rows are dropped inside their cells, so the frame geometry is identical in every case.
- **Footer band:** a bordered band with "Prepared with ShaftMaster · date" on the left and "Doc. No. … · Rev …" on the right. Blank parts are dropped.
- **Page 1 body:** title, project information table (filled rows only), design-basis line, approval block. The logo appears only in the header.
- **Pagination:** keep-with-next only between a heading or caption and its own content (each figure row keeps with its caption strip); table rows do not split. A figure moves to the next page only when it does not fit, and text may flow under a figure. In the P1–P4 section (Basic) and in the FF section the text (inputs, equations, results) comes first and the figures follow (F2/F3; F4–F6). In the rope / traction section the equation blocks and tables may flow across pages (`DocxDocument.Flow`); headings still keep with their first line. F4 and F5 are at most about 107 mm tall so that two figures share a page. `tests/solution/check_pagination.py` checks the Word render: no page except the last may be more than 40 % empty unless a section ends there. It is enforced for fixtures 01, 12, 14 and 15.
- **Figures F1–F9** (Phase A F1–F6, Phase B F7–F9): see `CALCULATION-SHEET-FIGURES.md`.
  - F1 is in the Input data section.
  - F2 and F3 are in P1–P4. Advanced gets F2 only.
  - F7 follows the rope-tension table. F10 ends the rope / traction section. F8 follows the P5 subsection. F9 follows the last of P6/P7.
  - F4–F6 are in FF1/FF2, after the FF results.
  - Each figure is SVG with a 300 dpi PNG fallback, in a frame with the caption strip "Figure n — title | Dimensions in mm | NTS".

## Suspension rope and traction checks; Design Notes (2026-10-08)

- **Section:** "Suspension rope and traction checks" follows the rope tensions. It has:
  - a geometry table: travel, H = travel + overhead, Dp, α, mDP;
  - rope check equations with substitution, and a check table (check | clause | equation | required | actual | result);
  - each traction case with μ, f, e^(f·α) and the T1/T2 equations per position, plus a result table;
  - F10.
- **Summary row:** for example "Rope Sf 17.89 ≥ 12.56 OK; D/d OK; traction NOT OK".
- **Inputs table:** lists only the new rope / traction inputs that the checks shown actually use.
- **Availability:** the section appears when a D/d, safety-factor or traction result exists.
- **Omitted rope / traction items:** grouped as one entry in the omission list. Missing inputs and invalid geometry are both included.
- **Export dialog:** also lists every NOT OK result, in the application only. The sheet shows the true result without commentary.
- **Termination row:** required "0.8 × MBL = … kN", actual "MBL = … kN", result "Required strength"; no empty cells.
- **Design Notes** (Workflow → "Export Design Notes…", `DesignNotes.cs`): same title block and page frame as the sheet.
  - Short advisory notes under "Warnings" and "Recommendations". Only headings with notes are shown.
  - Each warning carries a computed recommendation (see `ROPE-TRACTION-CALCULATIONS.md`).
  - No tables, no basis section and no notes about passed checks.
  - A project with nothing to say gets the single line "No design notes for this project."

  This is the only place for remarks.
- See `ROPE-TRACTION-CALCULATIONS.md`.

## Title block and approval block (optional, 2026-10-07)

Every field is optional, has no default, and is never required. Blank fields are left out; no placeholder ("N/A", "—", "TBD", "NOT PROVIDED") is printed.

- **Company-level** (application settings, shared by all projects, never in the project file):
  - Company name and company logo (PNG/JPG).
  - Set in **General → Company / report settings…** (`CompanySettingsWindow.cs`), which has Browse / Remove and a preview.
  - Stored in `%AppData%\ShaftMaster\report-settings.json` (`CompanyReportSettings.cs`). The logo is validated and copied there as `company-logo-*.png|jpg`; replacing or removing a logo deletes the old copy.
- **Project-level** (saved with the project):
  - The optional group `ElevatorProject.Report` (`ProjectReportDetails`): client name, document number, revision (free text), prepared by, checked by, approved by.
  - Shown on the General page under "Calculation sheet details".
  - Older projects open with it blank, and the schema version stays 1. `tests/project/SchemaGenerator.cs` treats `Report` like `Calculations`: nullable and not required. `config/ElevatorProject.schema.json` was regenerated with it.
  - No calculation, chassis/geometry, safety/pit or FF fingerprint includes it, and editing it does not invalidate UI results. It does not change drawing geometry or STEP parameters.
- **Page 1 of the sheet:**
  1. The title. The logo and company name appear in the header title block only.
  2. The project table, showing only filled rows: Project, Client, Document No., Revision, Date, Elevator system, Rated load.
  3. The design-basis line.
  4. The approval block: Prepared by / Checked by / Approved by × Name | Signature | Date. It is always present. Names appear when given; signature and date cells are always empty for hand signing.
- **Header and footer:** the title block (logo, company, project/client, Doc. No./Rev./Page) and the footer band, as described in "Page layout and figures".
- The export omission dialog does not mention these fields.
- **Reusable image API** (`DocxWriter.cs`), also intended for drawings later; no drawings are added yet:
  - `DocxImage.FromFile/FromBytes` reads PNG/JPEG data and pixel size without WPF; `Fit` keeps the aspect ratio inside a box.
  - `DocxDocument.Image(...)` places an inline body image sized in mm; `ImageAndText(...)` places a borderless logo band.
  - `HeaderImage` places a header image. Media parts are created once, with relationships from the document and/or the header.

## Currency, omission and the export warning

`CalculationSheet.Build` (`CalculationSheet.cs`) recalculates every section from the current canonical project at export, then checks each result against its input fingerprint. Numbers previously displayed in the UI are never reused. Before building, the export commits the open editors.

- P1–P4: the active workflow only, Basic or Advanced, through a fresh `CalculationWorkspace` with shared geometry. Rope tensions come from the same result.
- P5–P7: `SafetyPitCalculationEngine`, independent of P1–P4.
- FF1/FF2: `CarGuideForceCalculationEngine` with freshly issued shared internal cabin geometry. The audited equations are unchanged.

A calculation whose inputs are missing, invalid or malformed in the UI (draft text, same routing as the Calculations page), or whose currency cannot be verified, is **omitted entirely**: summary row, section and section number. The remaining sections are renumbered. No zero, placeholder or stale value is written. The omissions are recorded in `CalculationSheet.Omitted`, with the missing or invalid inputs, for the application only.

In the application (`CalculationSheetExport.cs`):

- If some calculations will be omitted, a dialog lists them with their missing or invalid inputs and offers **Export anyway** and **Cancel**.
- If no calculation is available, nothing is exported. A message explains why, listing every omitted calculation with its inputs. `CalculationSheet.Save` also refuses in that case.

Save handling is unchanged. The sheet is written to a temporary file in the target folder and then moved or `File.Replace`d. A file locked by Word gives a clear error and leaves the original unchanged. A missing folder, a non-`.docx` extension and a blank path are rejected.

## Internal traceability (not printed)

- P5–P7: rule `GearboxNoteSafetyPit-v1`. P1–P4: ShaftMaster project models `TwoStageVerticalMachineDatum-v1` (Basic) and `ReleasedTwoStageWithRigidSpreader-v1` (Advanced).
- FF1/FF2: rule `GearboxNoteCarGuideForces-v1` and its interpretation record, both in `CAR-GUIDE-FORCES.md`.
- Source note: `Elevator_Calculations_GEARBOX_Code_Corrected_Dual_Units.docx`, SHA-256 `ECFD591133F5F97ED923F62A3A3EEA60C2B97A67805E60BCB7131C3DA7FB13DF`. Paragraph and equation references are in the engines' `RuleBasis` constants.

## Verification (clean output)

Evidence is under `output/calcsheet-clean-20261007/`; see "Results" in `SESSION_HANDOFF.md` for the counts of the final run.

- `CalculationSheetTests.cs` (`run-calculation-sheet.ps1`):
  - The full removed-text list is absent from document, header, footer and docProps for complete, missing-input and malformed-input sheets.
  - No zero placeholders (supplied Δ offsets excepted); design-basis line, title block, header and footer formats present.
  - Exact section numbering after omission; omission reasons recorded.
  - Numbers unchanged: FF 1100/2200 N and 1050/2100 N; P5 9800 N; P6 39200 N; P7 29600 N total / 14800 N per buffer; Basic ΣP = W.
  - Per-rope column omitted without a rope count; blocked export when nothing is available; save-path cases.
- `CalculationSheetUiTests.cs` (`run-calculation-sheet-ui.ps1`), real WPF window:
  - The same removed-text checks.
  - The omission dialog lists exactly the omitted calculations and inputs; **Cancel** stops the export and **Export anyway** continues it.
  - No dialog for a complete sheet; export blocked with an explanation when nothing is available.
  - Recalculation after edits on another page; Advanced renumbering; toolbar wide and narrow layout.
- Rendering: Word is used only as a test viewer, with a read-only PDF export (`render-calculation-sheets.ps1`). PyMuPDF rasterises the pages and builds overviews; every page is reviewed.

## Known limitations

- The Save As dialog is not automated in tests. The UI test calls the same `WriteCalculationSheet` path and drives the omission dialog through its own buttons with a non-modal stub.
- A malformed value on a non-calculation page (for example capacity text) stops the export at commit with the existing field message, as Save does.
- Equations are formatted text (Cambria Math font), not Word equation objects.
