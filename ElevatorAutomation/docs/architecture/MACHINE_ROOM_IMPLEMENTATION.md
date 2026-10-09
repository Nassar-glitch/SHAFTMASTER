# Machine room plan implementation — 14 September 2026

**Updated chassis behavior:** The later [chassis dependency correction](CHASSIS_DEPENDENCY_CORRECTION.md) supersedes this initial report's rigid machine/chassis/P1–P4 grouping and test counts. It defines individual spanning members/support datums, the final-geometry audit, 32 passing machine-room records and the current `bin-chassis` build.

## 1. Automated scope

`MachineRoomUpdater : IViewUpdater` updates the existing native machine-room view. UPDATEALL runs PlanUpdater and, when its embedded manifest is present, MachineRoomUpdater using the same calculated ElevatorParameters. No section, elevation or vertical reeving view is created.

The source has 1,354 model-space entities on ten layers: 1,146 polylines, 97 hatches, 51 lines, 30 splines, 14 dimensions, nine MTexts, three multileaders, two arcs and two circles. It contains no model-space component INSERTs; most equipment and P1–P4 lettering are fragmented imported outlines/fills. Paper-space objects and dimension graphics are not treated as equipment assemblies.

Automated: shaft faces, room boundaries with retained extensions, machine/traction-sheave/chassis/P1–P4 placement, reference beam spans, panel/ventilation placement, governor placement, identified rope-opening references, deflector projection and annotation, axes, native hatches/leaders/labels and all 14 dimensions. Unknown native hardware details retain their shape within their assigned assembly. Hardware selection, structural adequacy and detailed rope routing remain unresolved.

## 2. Semantic bindings

All 1,354 entities receive persistent `ELEVATOR_AUTOMATION` XData containing semantic ID and movement role. Critical references have named IDs; assembly details have stable unique IDs beneath namespaces including MachineRoom.Shaft, Machine, TractionSheave, Chassis, Support.P1–P4, ConcreteBeam, ControlPanel, Ventilation, Governor, RopeOpening, DeflectorSheave and Reference. No distinct physical safety-gear assembly could be confidently identified; one was not invented.

The DWG NOD stores the JSON `MachineRoomBindings` manifest with schema version, template identity, expected entity type, role and original visibility. Runtime lookup uses IDs, never persistent entity handles or ordering. IDs survive save/reopen and replacement by a new native object carrying the same XData. Missing/duplicate IDs reject updates. Keep identifiers, component type and reference topology intact during template editing. Mirroring or tilting out of the supported XY plane is rejected; translation and in-plane rotation are supported.

One-time bootstrap verifies the source SHA256 and matches reviewed local geometry fingerprints derived from shaft dimensions/faces. This bootstrap is deliberately limited to the supplied source. Once the semantic master is prepared, fingerprints are not used during updates. `Templates/MachineRoom_Master.dwg` is the prepared reusable view; the original DWG remains unchanged.

## 3. Shared relationships

The machine-room local origin is the existing shaft front-left datum; its axes come from native shaft faces. SW, SD, car row, CW row and TZ are consumed from the shared Core result. Room size is shared shaft size plus measured extensions: 525 left, 525 right, 900 front, zero rear.

The reference machine-room car row was 675 mm and CW row 1475 mm (TZ 800). For the same 1450×1600 shaft, the validated Core plan gives car row 725, CW row 1475 and TZ 750. UPDATEALL aligns the second view to Core, moving the car-related assembly 50 mm; it does not modify the validated plan to match the old machine-room discrepancy.

Machine/chassis/traction-sheave/P1–P4 native profiles move coherently with the car axis and shaft center. CW references follow the CW axis. Beam reference spans follow room/shaft boundaries; their bearing relationship with the retained machine assembly needs structural review after resizing. The detailed contours around suspension openings are retained, not replaced with an inferred rope path.

## 4. Deflector and governor

Core selects no sheave, 250 or 400 mm from the specified TZ thresholds. The identified edge-on deflector assembly comprises five profile entities, a center mark and the Ø annotation. All seven are hidden together when absent and restored together when needed. Geometry remains in the database so switching is reversible. The nearby counterweight opening contours and Ø250 governor are separate and are never suppressed with this assembly.

The selected pitch projection is updated; the native projection width/detail is retained. A manufacturer-specific 400 mm bracket, guard and detailed hardware design has not been invented. Above TZ 1100 the last presence/diameter is retained with explicit review status rather than extrapolated.

Governor placement uses the engineer-confirmed 100/150/200 mm schedule in Core. The measurement is from the actual car-guide axis to the start of its 250 mm diameter. A roughly 2.3 mm discrepancy between the original offset-dimension datum and the original guide axis is normalized so the updated geometry and dimension agree. It remains separate from the handling sheave.

## 5–8. Engineering traceability

See `MACHINE_ROOM_CODE_REQUIREMENTS.md` for exact clauses, implemented checks, missing inputs and referenced standards. The implemented code-derived numeric check evaluates suspension sheave/rope diameter compatibility. The 250 mm project selection is flagged FAIL against the combined 8 mm minimum rope and 40:1 ratio requirements. Other applicable access, maintenance, headroom, openings, guards, governor, traction and ventilation requirements are NOT YET EVALUABLE, with missing inputs recorded.

Deflector thresholds, governor offsets and 152° target are Project rules. Retained structural geometry is an EngineeringAssumption. EN 81-50:2014 5.11, 5.12 and 5.4, EN 12385-5, applicable EN ISO 13857 material and manufacturer/structural documentation are needed. No actual wrap, vertical sheave offset or whole-lift compliance is claimed.

## 9. Project data and model additions

ProjectInput adds optional MachineRoomLeftExtension, MachineRoomRightExtension, MachineRoomFrontExtension, MachineRoomRearExtension, SuspensionRopeDiameterMm and TractionSheavePitchDiameterMm. SW/SD remain the only required numeric user inputs. Extension values must match the reference in this version; rope and traction diameter fields are prepared for programmatic project-data enrichment, with no new interactive prompts yet. An entered traction diameter does not resize the retained machine hardware.

Core adds MachineRoomParameters, DeflectorSheaveSelection, GovernorOffsetSelection, ReevingGeometry and EngineeringRule. The existing Project Xrecord remains the shared project-data store. Registry and reeving records are persisted separately in the same DWG NOD. Future centers, directions, entry/exit angles, vertical offsets, required chassis elevation adjustment and actual wrap remain nullable. No additional user-input fields are needed for the status separation.

LayoutStatus, WrapAngleStatus and CodeDiameterRatioStatus are independent, reported by UPDATEALL and persisted together in `MachineRoomValidation`. Layout PASS means the existing drawing checks passed; wrap is currently NOT YET EVALUABLE and a diameter conflict remains FAIL without blocking CAD output. The future optimization policy changes vertical center offset/chassis elevation, keeping the selected market diameter fixed at 250 or 400 mm. No solver or automatic diameter substitution is implemented. Improving wrap cannot resolve D/d.

## 10. Verification

AutoCAD 2023 Core Console runs on disposable drawing copies. Original plan cases and golden comparisons are unchanged:

- 43 original Core sizing assertions; six annotation checks; 39 machine-room Core checks, including fixed market sizes and independent wrap/code results.
- 17 original CAD regression records, 14 architecture records including seven pre-refactor golden comparisons, one real command workflow record and seven plan-completeness records.
- 20 machine-room records: master/import, 16 update-and-reopen cases, missing ID rejection, duplicate ID rejection and native-object replacement retaining semantic identity.

Machine-room cases cover TZ 519.9, 520, 800, 800.1, 1100 and 1100.1; baseline 1450×1600; smaller 1200×1400; larger 1800×2000; repeated absent/250/400 switching; translated and rotated views; governor boundaries 700 and 850 (1100 occurs in the threshold cases). Checks compare both views' axes, dimensions, extensions, full assembly visibility, stored rule results, binding persistence, native curve/vertex positions within retained machine/governor assemblies, appearance and hatch patterns/loop counts.

Evidence lives in `tests/*-results.json` and `tests/generated/*.log`. Run `tests/run-tests.ps1` to rebuild and run the complete suite. Geometry tolerance remains 0.02 mm. Automated native checks do not constitute a manual desktop/printed drawing review or certification. Original user DWGs and the frozen regression source retain their SHA256 hashes.

## 11. Remaining decisions

Resolve the 250 mm suspension-sheave code conflict; confirm manufacturer hardware and detailed rope-opening/reeving relationships. Provide loads and structural design for chassis/P1–P4/concrete bearing and beams; working/access envelopes, vertical clearances, guards and ventilation data. Confirm physical safety-gear requirements from equipment documentation. Review annotation collisions and plotting for the final sheet; no automatic sheet layout was added. New unbound equipment is not automatically interpreted.

## 12. AutoCAD workflow

1. Start a fresh AutoCAD 2023 session so the previous .NET assembly is not still loaded. NETLOAD `ElevatorAutomation/bin/Elevator.AutoCAD.dll`; keep Elevator.Core.dll alongside it and retain the config/Templates folders.
2. Open an existing semantically bound Plan project or a project copy of `Templates/ExistingPlan_Master.dwg`.
3. Run ELEVATORPROJECT to store/edit project type, SW and SD. Existing stored values are reused by updates.
4. Once per drawing, run ELEVATORMACHINEROOM. Pick where the machine-room shaft front-left datum should appear in an empty model-space area. This places the existing prepared reference view aligned with the Plan axes; it is not a derived-dimension prompt.
5. Run UPDATEALL. Both installed views update, validate and save a separate uniquely named DWG plus the shared design/rule JSON in `ElevatorAutomation/output`. Review the command-line engineering status and companion JSON.
6. Open the saved output and SAVEAS the project name/location you want. For subsequent changes, edit ELEVATORPROJECT then run UPDATEALL. Do not re-import the view.

ELEVATORUPDATE remains the legacy Plan-only command. Use UPDATEALL whenever the machine-room view is installed to avoid leaving that view stale. The supplied reference is one equipment configuration; this implementation does not establish additional elevator-family master designs.
