# Elevator CAD automation

Refactored from the working `ElevatorPlanGenerator` AutoCAD 2023 plugin. The original implementation and its previous test outputs are kept in `legacy/ElevatorPlanGenerator/`. No additional drawing view is automated.

## ShaftMaster desktop application

The current release is `bin-shaftmaster-v03/ShaftMaster.exe` (SHA-256 `9CFD5FAB…`). Its changes are in `bin-shaftmaster-v03/RELEASE-NOTES.md`, the checksums in `bin-shaftmaster-v03/SHA256SUMS.txt`, and the application in [ShaftMaster UI results](docs/SHAFTMASTER-UI-RESULTS.md). v01 and v02 are kept unchanged. `./build-solution.ps1` builds into `artifacts/bin` by default; it refuses to write into a release folder (`bin-shaftmaster-v*`) unless `-Release` is given. Tests: `./tools/test.ps1 -Tier 1|2|3` ([testing](docs/TESTING.md)).

## AutoCAD workflow

1. Start a fresh AutoCAD 2023 session so the old DLL's identically named command is not loaded. Managed DLLs cannot be replaced in an already loaded session.
2. Copy `Templates/ExistingPlan_Master.dwg` to a project filename and open the copy in Model space.
3. Run `NETLOAD` and select `bin/Elevator.AutoCAD.dll`. Keep `Elevator.Core.dll` beside it and the `config` folder beside `bin`. Use AutoCAD's normal trusted-location/load prompt if displayed.
4. Run `ELEVATORPROJECT`. Select **ExistingPlan**, then enter SW and SD in millimetres. This stores project data and validates the fit; it does not resize the drawing yet.
5. Run `UPDATEALL`. It calculates one design, updates the plan and saves a uniquely named separate DWG in `output`. AutoCAD prints its full path. The original/template file is not overwritten.
6. Open that output and use `SAVEAS` to choose your final project name/location. Stored project data and semantic identifiers travel with the DWG.
7. Run `ELEVATORUPDATE` or `UPDATEALL` again to reuse stored inputs without prompts. To change inputs, run `ELEVATORPROJECT` first. If project data do not exist, either update command starts project entry once.

`ELEVATORPROJECT` data remain in memory until the DWG is saved. A successful update saves them in its separate output. An unavailable family is rejected before changing the project.

## Structure

```
ElevatorAutomation/
  ElevatorAutomation.sln
  Elevator.Core/             # No Autodesk references
    Models/                 # ProjectInput, ElevatorParameters, ElevatorType, EquipmentProfile
    Rules/                  # Common validation, door/rail selection, shared sizing
    Geometry/               # Local-frame point mappings, independent of AutoCAD
  Elevator.AutoCAD/
    Commands/               # ELEVATORPROJECT, ELEVATORUPDATE, UPDATEALL, shared coordinator
    Drawing/                # Reader, updater, semantic bindings, DWG data, legacy import
    Views/                  # IViewUpdater, PlanUpdater, native coordinate adapter
  config/                   # Family catalog, shared profile, one-time migration resources
  Templates/                # Prepared existing-plan master; family readiness manifest
  tests/                    # Original cases, golden DWG comparisons, persistence/editing tests
  bin/                      # Core DLL, AutoCAD DLL, test binaries
  output/                   # Separate generated DWGs and diagnostic JSON
```

The core owns all sizing and local geometry calculations. `PlanGeometryAdapter` converts between native AutoCAD points and the shaft's local frame. The coordinator computes `ElevatorParameters` once and passes it to each `IViewUpdater` within one database transaction. Both update commands use the same `PlanUpdater`. Add validated views to the registry in `UpdateAllCommand`, rather than duplicating the design engine.

## Persistent component bindings

Each of the 521 original model entities has XData under registered application `ELEVATOR_AUTOMATION`:

* Semantic ID, for example `Plan.Shaft.InnerOutline.EntranceDatum`, `Plan.Cabin.Inner.Left`, `Plan.CarGuides.Left.Contact`, `Plan.Counterweight.Outline`, `Plan.CounterweightGuides.ReferenceRow`, `Plan.Door.Opening` and `Plan.Reference.Centerline`.
* Transformation role, for example `wall`, `cabin`, `carLeft`, `cwRight` or `dimension`.

Detailed fragments and dimension chains receive stable role/part identifiers. They are stored names, not regenerated from entity order. The `PlanBindings` Xrecord in the DWG's named object dictionary stores the schema version, template ID, expected entity types, transformation roles and dimension-anchor rules. It contains **no entity handles or required layer names**. After initial migration, the reader resolves entities exclusively by semantic ID from XData and the drawing's embedded manifest; it does not read the external bootstrap manifest.

Tags persist through save/reopen, normal moves and edits, and native cloning. Replacing an entity is supported when its XData is transferred, its entity type/topology remains compatible, and the old tagged entity is removed. Copying without removing the old tagged entity creates a duplicate ID and is rejected. Missing IDs are rejected; unbound extra geometry is preserved. Layer/style edits are allowed. Keep bound layers editable for updates.

The shaft inner outline's entrance vertices define the origin and local axes. Translation and in-plane rotation work without fixed world coordinates. The existing six-vertex shaft, four-vertex counterweight, centered layout and native component topology remain requirements. Tags alone cannot make a materially redesigned topology compatible with the existing plan algorithm; that needs a revised template adapter/binding contract.

`LegacyMigration` is the only code that knows old handles. It imports the exact preserved original template (verified SHA-256), or a complete drawing tagged by the old plugin. It writes semantic tags and the manifest transactionally, preserving other applications' XData. Unknown untagged drawings are rejected. Your current `plan codex.dwg` differs from the preserved original and was not modified or automatically rebound.

## Project data inside the DWG

Named Objects Dictionary → `ELEVATOR_AUTOMATION` → `Project` Xrecord contains versioned JSON split into short native text values. Initial fields are `ElevatorType`, `SW`, `SD`, `TemplateId`, and `SchemaVersion`.

The model also contains optional PlanTitle, ProjectName, Capacity, Stops, Travel, Pit, Overhead, FloorHeights, DoorType, EntranceConfiguration, CounterweightPosition, MachineType, Speed, RailType and SuspensionType, plus an `Extensions` dictionary. PlanTitle and ProjectName can drive plan annotations; Capacity and Stops are available as annotation metadata when populated. The remaining optional fields are preserved when editing SW/SD but do not yet alter the validated plan rules. Unsupported future schema versions are rejected instead of overwritten. No external input file is needed for an existing project DWG; output JSON is only a diagnostic report.

## Families and templates

`config/families.json` maps each elevator type to a template ID, master filename, equipment profile and enabled status. Adding a family means supplying its actual tagged master and validated configuration. There is one common application engine, not a copy per family.

Only the existing rear-counterweight plan has supplied/validated geometry. Its actual family was not established. `ExistingPlan_Master.dwg` therefore retains the honest **ExistingPlan** type. The catalog reserves `Hydraulic_Master.dwg`, `MRL_Gearless_Master.dwg`, `Gearbox_Master.dwg` and `HomeLift_Master.dwg`, all disabled pending their actual geometry/profiles. No renamed duplicate DWGs are presented as working family masters.

Future rear/side counterweight or door variants belong in project parameters and family rules/adapters, not one DWG per small variant. The optional fields establish that extension point; this refactor does not claim those geometries already work.

## Preserved plan rules

Car DBG = SW − 300; cabin width = SW − 400; cabin depth = SD − 400; TZ = cabin depth / 2 + 150. CW DBG and door widths select the largest fitting preferred sizes using the unchanged equipment allowances. All existing native details, clearance preflight, skew/Z offsets and 31 independent dimension checks are retained. Counterweight depth still comes from the native outline. These are the previously supplied plan-sizing rules, not a new capacity/mass calculation.

## Build and verification

Run `./build.ps1` in PowerShell (output `artifacts/bin` unless `-BinDirectory` is given). This compiles the two DLLs with the installed .NET Framework compiler and AutoCAD 2023 SDK assemblies, then runs 43 pure rule assertions. The Visual Studio solution targets .NET Framework 4.8; override `AutoCADDirectory` if needed.

Run `./tests/run-tests.ps1` for the native regression and interactive command workflow. It uses disposable copies and real AutoCAD Core Console command boundaries; AutoCAD's dimension-association callbacks run between commands. Keep the original `legacy/ElevatorPlanGenerator` directory because it contains the preserved regression template and independently generated golden DWGs. The runner restores SECURELOAD and checks result JSON for failures, not merely the console exit code.

See `TEST_RESULTS.md` for evidence and limits. Geometry/validation errors roll back the update transaction. As in the original plugin, a filesystem failure during the subsequent separate SaveAs can leave the current in-memory drawing updated; the command reports the failure rather than claiming a saved file.

## Completing the plan: annotations and coverage

The plan contains no remaining unbound *original* equipment geometry: all 521 original entities are already transformed. The next pass adds text-content automation and stronger validation without changing the validated geometry. Read `PLAN_COMPLETENESS_AUDIT.md` for the before-change inventory and `PLAN_UPDATE_RESULTS.md` for the new test results and remaining manual decisions.

After `ELEVATORPROJECT`, optional commands are:

* `ELEVATORPLANOPTIONS`: edit ProjectName and the new optional PlanTitle. Enter keeps a value; `-` clears it. A title updates the existing styled “SHAFT AND CAR” caption and preserves its printed `1:8` suffix. Clearing PlanTitle restores the original caption after UPDATEALL. The command does not change plot scale.
* `ELEVATORANNOTATION`: select an existing model-space MText or MText-content multileader, then supply a text template. Existing component IDs and movement rules stay intact. For an unbound annotation, enter a unique semantic name and choose which reference it moves with: Front, Rear, Cabin, Counterweight, DoorLeft/Right or CarLeft/Right. No coordinate or derived-dimension input is needed.
* `ELEVATORPLANAUDIT`: read-only coverage report for the current DWG, including unbound objects and static annotations. UPDATEALL also reports unbound objects and includes coverage in its output JSON.

For example, an existing note can use:

```
{{ProjectName}} — cabin {{CarWidth}} x {{CarDepth}} mm
Car DBG {{CarDBG}} / CW DBG {{CWDBG}} / door {{DoorWidth}} / TZ {{TZ}}
Cabin door return: {{Clearance.cabinDoorReturnMm}} mm
```

Tokens are case-sensitive. SW, SD, CarWidth, CarDepth, CarDBG, CWDBG, DoorWidth, TZ, the other public `ElevatorParameters` values, ProjectName, PlanTitle and ElevatorType are available. Clearance tokens use the keys in the output clearance report with a `Clearance.` prefix. Capacity and Stops require stored values; unknown/unset tokens reject the update rather than printing an assumed zero. Core renders the values; the AutoCAD adapter escapes text and keeps the entity's text style, height, attachment and movement binding. Native MText formatting codes may be included in an explicit template.

Annotation recipes are stored as the additive `AnnotationTemplate` property on the DWG's embedded semantic bindings. The XData ID remains the lookup key. Rendering never destroys the recipe. Using `-` when configuring a template disables text automation and leaves the current text static; its geometry remains bound. On the title annotation this also clears PlanTitle to prevent re-enabling it automatically. Edit the recipe, rather than the rendered text, when changing an automated annotation permanently.

For the Plan view, UPDATEALL prepares all annotation values before geometry changes, updates native geometry, regenerates/checks all eight hatch patterns and loops, and validates dimensions and text. Clearance preflight uses native extents in the shaft coordinate frame instead of sampled curves or world-aligned hatch bounds; the front equipment clearance is reported too. When a machine-room view is installed, UPDATEALL also updates and validates that view before saving.

No default equipment callouts were rewritten: P5/P6/P7, T70/65/9, T50/50/5, SPEED GOVERNOR and the A markers stay as supplied unless explicitly configured as annotations. A different rail label does **not** select or resize hardware. The optional future hardware/entrance fields still do not implement variants. Custom label length and placement require drafting review; this pass does not introduce automatic text-collision layout or change printing scale. Additional unbound equipment is reported and preserved, not silently inferred or resized.

## Second view: Machine Room Plan

The current chassis correction is documented in [Chassis dependencies, audit and verification](docs/CHASSIS_DEPENDENCY_CORRECTION.md). The earlier two-view release used **bin-chassis/Elevator.AutoCAD.dll**; the previous bin DLL was locked by a running process and was preserved. The current suite has 32 machine-room records. Use ELEVATORMRAUDIT to check the final native geometry independently of UPDATEALL. This correction supersedes the earlier rigid chassis/support grouping described below and in the initial implementation report.

Run ELEVATORPROJECT, then ELEVATORMACHINEROOM once to place the prepared native view from `Templates/MachineRoom_Master.dwg`. Run UPDATEALL for subsequent updates of both views from the same Core result. ELEVATORUPDATE remains Plan-only. The third-view release below adds the native Section B-B.

Read [Machine room implementation and exact workflow](docs/MACHINE_ROOM_IMPLEMENTATION.md) and [code requirements and unresolved engineering inputs](docs/MACHINE_ROOM_CODE_REQUIREMENTS.md). The 250 mm project-selected suspension sheave is explicitly flagged as conflicting with the supplied EN 81-20:2014 minimum rope diameter and diameter-ratio requirements. Outputs are reference layouts requiring the documented engineering review.

`tests/run-tests.ps1` now includes the machine-room suite: 20 native records and 39 additional Core checks, alongside all original plan regressions. The complete run passed on 14 September 2026.

The engineering report keeps LayoutStatus, WrapAngleStatus and CodeDiameterRatioStatus independent. The available deflector sizes remain 250 and 400 mm only. A code diameter conflict does not block CAD generation and cannot be cleared by improving wrap. The third-view release implements the supplied B-B external-tangent branch with an adjustable hanger; other routing arrangements still require review.

## Third view: Machine Room Section B-B

Use **bin-section/Elevator.AutoCAD.dll** after restarting AutoCAD. This build includes all previous Plan and Machine Room behavior plus the coordinated native section. Read [Section implementation, engineering limits and exact workflow](docs/SECTION_BB_IMPLEMENTATION.md), [reference inspection](docs/SECTION_BB_INSPECTION.md), and [generated comparison](tests/section/comparison.png).

Run ELEVATORPROJECT, ELEVATORMACHINEROOM (once), ELEVATORMRSECTION (once), then UPDATEALL. ELEVATORREEVINGDATA edits capacity, rope diameter/count and an optional specified slab-clearance minimum. No minimum is assumed when omitted. ELEVATORMRSECTIONAUDIT checks native section geometry. UPDATEALL uses one shared calculation for all installed views and saves a separate DWG. ELEVATORUPDATE remains Plan-only; use UPDATEALL for coordinated work.

Run `./tests/run-tests.ps1 -BinDirectory bin-section` for pure rules and all native suites, including section boundary/fault tests and a separate AutoCAD process that reopens and updates the saved three-view drawing. This uses the prepared source snapshot in `tests/section/reference-copy.dwg` and never overwrites the supplied original drawing.
