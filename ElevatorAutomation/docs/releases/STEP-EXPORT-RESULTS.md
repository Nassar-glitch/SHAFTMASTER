# STEP export verification — 2026-10-05

Implemented Export STEP… in the global toolbar and DRAWINGS page, using the current in-memory project snapshot and the existing preliminary 3D workflow. Built the updated x64 Elevator Solution.exe and packaged STEP worker, SOLIDWORKS interop DLLs and reviewed configuration. Existing CAD updater/Core calculation code was not changed.

Tested the action through the actual WPF UI in a relocated package folder containing only runtime files. Opened a representative 450 kg, SW 1450 / SD 1600 mm, five-floor project, made an unsaved project-name edit, chose a destination through the save dialog, and exported. The exact edited snapshot accompanied the output. Animated busy feedback was observed; the application returned to enabled controls after completion. No source drawing or project input file was modified.

- Export/reopen: PASS, STEP AP214, explicit millimetre units.
- 67 source and 67 reopened solid bodies; 0 solid faults; 0 positive-volume intersections.
- Maximum reopened per-body bounds difference: 2.18e-11 mm.
- Comparison with the earlier preliminary assembly: maximum bounds difference 1.14e-13 mm; maximum per-body volume difference 1.49e-8 mm³. Geometry/level of detail retained.
- Elapsed time on this workstation: 145.5 seconds, including SOLIDWORKS startup, solid construction, collision checks, export and reopen.
- Independent statuses: Layout PASS, Wrap PASS, CodeDiameterRatio FAIL. The nonblocking D/d failure remained visible in the success message and did not block export.
- Seven STEP input/dependency/replacement regressions passed, covering changed SW/SD, 400 mm selection, unequal floors, NO STOP, car/CW pose, unsupported machine rejection, missing package dependencies, and protection of existing STEP files.
- Existing rule suite passed: 43 sizing assertions, 6 annotation checks, 39 machine-room checks, 102 reeving/beam checks. Nine existing desktop/session/transport/output unit tests passed. A fresh AutoCAD four-view native regression was not rerun, since the CAD geometry/updater implementation was unchanged.

An initial SOLIDWORKS temporary-body mass-properties failure was caught without publishing a partial STEP. The worker now checks inserted persistent bodies. A whitespace-sensitive STEP unit check was also corrected. The final complete UI export passed after these repairs.

Evidence: output/step-export-tests/verified-ui-result.json and UI_Representative_source_5b605f7f/verification.json. Output: UI_Representative.step. Editable source: the corresponding Elevator_Assembly.SLDPRT. No preview model formats were generated. SOLIDWORKS 2022 was available and used; an installed/licensed SOLIDWORKS and configured part template remain runtime requirements.
