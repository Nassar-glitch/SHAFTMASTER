# Elevator Solution — Phase 1 architecture foundation

The accepted baseline contains four views with independent native semantic bindings. Before this phase, ProjectInput lived in Core but command classes gathered inputs and coordinated calculations; A-A had its own floor project record. UPDATEALL updated Plan/MR/B-B; A-A used dedicated commands. Geometry and styles remain the accepted baseline.

The foundation now has this ownership:

```text
Future UI / project file
    → ElevatorProject (Core input model)
    → ElevatorDesignEngine + reviewed DesignContext
    → ElevatorDesignResult + structured validation
    → IProjectDrawingGenerator
        → ProjectDrawingService (AutoCAD host)
        → existing Plan / MR / B-B / A-A updaters
        → one coordinated DWG
```

Core also owns deterministic floor operations, the versioned JSON codec, legacy data conversion and an extents-based DrawingLayoutService. It has no Autodesk references. The AutoCAD adapter owns native template reading, persistent bindings, project Xrecords, imports, updates, view translation and DWG saving. UI state does not calculate DBG, select doors/deflectors, solve reeving or decide code status.

The existing solvers are reused rather than copied. Core DesignContext explicitly separates source-template measurements from user-entered project data. Future 3D, BOM, simulation and control adapters can consume the project/results without using AutoCAD command classes. No such adapter, GUI or future elevator-family implementation was added in Phase 1.

## Native generation boundary

`ProjectDrawingService.GenerateProjectDrawings(document, project, options)` updates or imports the requested views. A bound service implements the Core-only interface for host injection. Defaults select all four. Dependencies are updated even when a caller requests only a dependent view. The plan template must already be open in the target project document. The adapter uses the reviewed master files and semantic identifiers; no source DWG is overwritten.

The layout service accepts measured view rectangles and a presentation margin. It computes two columns/two rows: Plan/MR above A-A/B-B. Core returns relative translations; the adapter applies them to complete semantic groups, including their frames. Existing view geometry and style are not resized. An explicit ArrangeViews option repositions existing views; adding missing views also arranges them. Repeated layout is tested for no drift. No engineering rule contains arbitrary sheet/world placement coordinates.

The current runtime is AutoCAD 2023 with .NET Framework 4.8. A standalone UI can serialize projects and validate/compute with Core without loading Autodesk. Actual native DWG operations still run inside AutoCAD. The host transport and UI process model are intentionally left for Phase 2.

## Backward compatibility

Existing commands and audits remain registered. UPDATEALL keeps its accepted scope; A-A is not silently added to the old command. Only the new generation API defaults to all four. The legacy NOD Project record, SectionAAProject record and all existing binding/result records remain readable by existing code. A small persistence bridge synchronizes legacy writes. No existing solver/updater geometry algorithm is replaced.

The workspace has no Git repository at its root or ElevatorAutomation directory. A pre-refactor source/config/test checkpoint was saved in `output/phase1/checkpoint`, with original reference DWG hashes in `output/phase1/reference-hashes-before.json`. All native test runs use disposable drawings in `output/phase1/regression`. Accepted source/template DWGs are protected.

## Tests and evidence

The runners are `tests/project/build-project-tests.ps1`, `run-phase1.ps1`, `run-native-only.ps1` and `run-additional-regressions.ps1`. The full existing suite is copied into the isolated workspace, including pre-refactor golden fixtures, so its output cannot overwrite accepted templates/goldens. The new native comparisons run the existing command workflow and the new API for SW/SD 1450/1600, 1650/1800 and 1850/2000; every tagged entity is compared under existing geometry tolerances. Native tests also exercise synchronization, JSON/DWG persistence, fresh reopen, all-four import from a Plan master and repeatable arrangement. Final counts and file hashes are recorded in the Phase 1 verification report.

## Decisions for Phase 2

1. Choose the standalone UI runtime and AutoCAD host transport/document execution scheduler; .NET Framework 4.8 is the current compiled contract, not a cross-platform deployment promise.
2. Define project file locations, template/package selection and version distribution, output naming and save/cancel behavior. Publish only after successful host generation; full-operation rollback is a host responsibility still to implement.
3. Decide which metadata fields are editable in the first UI. Capacity, shafts, floors, Pit/OH, rope inputs, car display and bracket maximum are functional; future door/drive/rail variants must not appear to change geometry before supported adapters exist.
4. Decide how to present provisional calibration and independent layout/wrap/code statuses, and whether manufacturer/company constraint documents will be linked or embedded. No missing engineering values need to be fabricated to build the UI.

Outstanding engineering items remain those in CURRENT_INPUT_INVENTORY.md: provisional CW calibration, rail ends/fixings, physical bracket detail, rope/manufacturer data, minimum slab clearance, hanger design, full traction and structural checks. These are independent of completing the Phase 1 architecture.
