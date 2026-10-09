# Elevator Solution V0.1

This is a standalone WPF desktop application for the accepted traction elevator and four accepted native drawing views. It targets Windows x64 and .NET Framework 4.8. No elevator geometry solver or accepted view updater was changed for this phase.

## Run

Launch `D:\CAD DATA---------------------\DWGs\2026\dwg plan codex\ElevatorAutomation\bin-solution-v01\Elevator Solution.exe`.

Keep the executable beside Elevator.Core.dll, Elevator.Solution.Bridge.dll, Elevator.AutoCAD.dll, Elevator.Solution.Host.dll, the executable .config file, reviewed-context.json and reviewed-example.elevator.json. The bin-solution-v01 directory must remain directly below ElevatorAutomation, beside its config and Templates directories. AutoCAD 2023 must be installed at `C:\Program Files\Autodesk\AutoCAD 2023`; its managed assemblies are not redistributed. Editing, JSON save/open and preview validation do not load Autodesk assemblies.

Normal workflow:

1. Click New Project, or Open an existing `.elevator.json` file. A ready example is `output/solution-v01/Example.elevator.json`.
2. In General, enter a project name and capacity. Speed is optional and is not a full speed-dependent verification. Only the accepted traction system and 1:1 topology are enabled.
3. In Shaft, enter SW and SD; review Pit and Overhead. New projects retain the established A-A defaults (1000 and 4200 mm), clearly editable. Validate displays the shared engine's derived car dimensions, DBG, TZ and selected door width read-only.
4. In Floors, enter the interval above each floor except the final normal floor. Edit names and Served directly; unchecked means NO STOP. Select a floor above the lowest datum, enter a new name and interval, then Add before selected. Remove/reorder operate on intermediate floors. The lowest and final datums are protected. Floor IDs stay stable. Select the displayed car landing under Car & Doors.
5. Review Suspension inputs, the provisional bracket spacing in Rails and any supplied minimum deflector/slab clearance in Machine. Blanks stay unknown. Do not enter guessed engineering minima.
6. Save the project. Click Validate and review blocking inputs and independent layout/wrap/code statuses. A code diameter-ratio FAIL is visible and does not automatically block an otherwise supported layout.
7. In Drawings, choose the output folder and DWG filename. All four views and automatic arrangement are selected by default. Click Generate Drawings. Existing output replacement requires explicit confirmation.
8. Wait for generation and fresh-process verification. On success, use Open Drawing or Open Folder. The normal workflow requires no manual AutoCAD command, NETLOAD or insertion click.

## Desktop and Core boundary

Elevator.Solution.Desktop contains the WPF shell and a ProjectSession. It directly edits the existing ElevatorProject through typed groups/field adapters and uses ProjectJson, ProjectFloors and ElevatorDesignEngine. FloorRow is a view adapter around FloorDefinition, not another persisted project model. UI code contains parsing, editing and navigation; it contains no DBG, clearance, sizing, sheave-selection or reeving formulas.

The nine pages are General, Shaft, Floors, Car & Doors, Machine, Suspension, Rails, Drawings and Validation. Metadata for unsupported variants is read-only. Calculated dimensions are not serialized as new authoritative inputs. The shipped reviewed context supports offline preview; the native host re-reads real drawing references on every generation. If templates change, regenerate/review this context before distributing another build.

Core remains Autodesk-free and WPF-free. No future elevator family, simulation, 3D or hydraulic capability is implemented. Future systems must register supported templates/rules in Core and the host, then expose actual supported choices in the UI.

## Project file lifecycle

New Project creates a fresh project identity and two normal floor identities. Required SW/SD, capacity and floor interval remain unfilled. Established Pit/OH/bracket defaults and audited provisional counterweight calibration are retained with visible provenance. Rope count/diameter are not silently invented. The example project is explicitly a populated test example.

Open/Save/Save As use the Phase 1 versioned ProjectJson envelope. Unsupported schemas show an error without replacing the current session. Extension data is retained. Save writes a temporary sibling file, then atomically moves/replaces it. Unsaved changes prompt before New/Open/Close. Drawing selection, output location and filename are session/job settings rather than a second project-file format; reopen the project and review those options before generating.

## AutoCAD host and transport

Elevator.Solution.Bridge defines versioned GenerationRequest/GenerationResponse envelopes. A request contains an ID, the exact existing ProjectJson string and GenerationOptions. Each job gets its own directory under output/solution-v01/jobs. The request, project snapshot, responses and process logs are retained there for diagnosis.

GenerationClient starts AutoCAD 2023 Core Console with a disposable copy of ExistingPlan_Master.dwg. A small script performs runtime bootstrap (NETLOAD and SOLUTIONHOST with request-folder/operation arguments). It does **not** run ELEVATORUPDATE, UPDATEALL, ELEVATORSECTIONAA or a series of drawing commands. SOLUTIONHOST reads the JSON request and calls `IProjectDrawingGenerator.GenerateProjectDrawings` through the existing ProjectDrawingService in a valid native document context. Core Console was exercised successfully on this installation. No interactive AutoCAD window is required during generation, and existing user AutoCAD documents are not touched.

The Phase 1 report matched the source. Two implementation details matter: the existing API generates dependency views, and the A-A audit requires Plan/MR/B-B context. The host therefore generates and audits dependencies first, then removes unselected semantic view entities from the disposable deliverable. The legacy API/commands are unchanged. An individual-view output is a drawing deliverable; regenerate it from its JSON project rather than expecting all legacy multi-view update commands to operate on missing dependencies.

Responses contain success/failure, request ID/input hash, output path, generated views, entity counts, measured bounds, validation rows, independent status strings, errors and elapsed time. The complete Core result is retained as DesignJson: serializing it as a raw result document avoids a .NET Framework JavaScriptSerializer bug when rehydrating dictionaries with null object values. This is result transport, not a duplicate project model or calculation.

## Generation, audits and publication

1. UI parses and validates, then snapshots ProjectJson before background execution.
2. The bridge validates protocol/view selection and copies the reviewed Plan master into a new job directory.
3. The host runs ProjectDrawingService with no final output path. Existing Plan validation includes geometry, annotations and hatches; native MR/B-B/A-A audits run before selection pruning.
4. The host checks selected semantic groups, unique component IDs, nonempty measurable bounds and no overlap between view extents. Layout uses the existing DrawingLayoutService; the UI provides no WCS placement constants.
5. Only a successful host saves generated.dwg in the job directory. Failure exits/discards the in-memory drawing; it does not save partial imports into the requested final file.
6. A second, fresh Core Console process reopens generated.dwg. It verifies the exact transport snapshot, persisted unified project, view membership/counts/IDs and extents. Full A-A native audit is rerun when all its dependency views are present.
7. The bridge verifies response identity and input hash. It copies the verified drawing to a temporary sibling of the requested output, then atomically moves/replaces it. Existing output must have been explicitly confirmed and must still match the hash captured at confirmation; otherwise publication is refused.
8. The UI reports DRAWING GENERATED SUCCESSFULLY and provides file/folder actions. This means drawing-generation success, not engineering certification.

AutoCAD child processes have a ten-minute timeout. Only the child process launched for that job is stopped on timeout. Intermediate job files are retained; they are not published as final deliverables. There is no active-document rollback risk because generation never uses a user's open project drawing.

## Build and test

Run `build-solution.ps1` from ElevatorAutomation. It builds into bin-solution-v01 and does not overwrite bin-phase1. The solution includes the three new projects. The script is the supported packaging build; it copies the reviewed example and executable runtime configuration. reviewed-context.json is exported by a successful all-four native integration test and is included in the delivered build.

- `tests/solution/run-tests.ps1`: project session, floor editing, transport, dependencies, output consent, native all-four/individual views, repeated layout and native failure publication checks.
- `tests/solution/run-tests.ps1 -UnitOnly`: checks without starting AutoCAD.
- `tests/solution/run-phase1-regressions.ps1`: all Phase 1 suites in output/solution-v01/regression.
- `Elevator Solution.exe --smoke`: starts the actual WPF window, exercises editable controls and floor rows, saves/reopens projects, generates baseline and unequal/NO STOP projects through the same desktop generation method, and records screenshots/results.
- `--smoke --screenshots-only`: captures all nine pages without native generation.
- `tests/solution/run-visual-qa.ps1 -UiEvidenceDirectory ...`: compares desktop output to the accepted legacy native workflow entity by entity after normalizing view frames; then plots the actual published DWGs through AutoCAD to PDF.

## Current limitations and manual review

The existing counterweight reference pose and bracket spacing remain provisional. Minimum deflector/slab clearance, hanger mechanical design, rail ends/fixings, detailed structure, manufacturer information and full traction/compliance checks remain unresolved where not supplied. Deflectors stay restricted to 250 and 400 mm. The existing 250 mm diameter-ratio conflict remains visible.

All four view extents are separated, but native drawing text layout is not redesigned. The test floor name “Upper” crowds its adjacent A-A dimension exactly as in the accepted workflow. Review long floor names and plotted annotations; there is no new automatic text-collision solver. Existing source extents also produce generous whitespace in the combined arrangement, which was retained to preserve the approved geometry.

This release is a folder deployment for this installed AutoCAD 2023 path, not an installer or general host-version detector. It has no user cancellation button, remote/server host, multi-job queue or cloud service. Generation output is verified in fresh Core Console processes; graphical AutoCAD open/zoom experience, Windows file associations for Open Drawing, dialog interaction, different display scaling and another workstation/license installation still need manual acceptance testing. Automated WPF smoke tests drive the real application's controls/methods; they are not a claim of human mouse/keyboard testing of every dialog.

Current deployment retains job snapshots and logs. Define retention, installer/signing, configurable host discovery, cancellation, broader accessibility/DPI testing and template/package versioning before wider distribution.
