# ShaftMaster architecture overview

ShaftMaster designs an elevator shaft from a site survey: it sizes the lift components, calculates the loads, and produces drawings, a calculation
sheet and a STEP model. The design flow (presets, lock / follow, build stages) is fixed in [`DESIGN-FLOW.md`](../../../DESIGN-FLOW.md); the code
structure and the refactor rules are in [`ARCHITECTURE-PLAN.md`](../../../ARCHITECTURE-PLAN.md).

## Projects and dependencies

| Project | Kind | Target | Purpose |
|---|---|---|---|
| `Elevator.Core` | library | netstandard2.0 + net48 | The project model, the presets, every sizing rule and calculation engine, the application services (project session, preset flow, live calculation). No WPF, AutoCAD, SOLIDWORKS or Office types. |
| `Elevator.Reporting` | library | netstandard2.0 + net48 | The calculation sheet, the Design Notes, the DOCX writer, the figure model and the SVG renderer. Depends on Core. |
| `Elevator.Solution.Bridge` | library | net48 | The contracts between the desktop application, the AutoCAD host and the STEP worker (one file per contract, an explicit contract version, JSON wire format). Depends on Core. |
| `Elevator.AutoCAD` | library | net48 | The native AutoCAD 2023 drawing adapter: reads and updates the plan, machine room, sections A-A and B-B and the vertical section in a DWG. Depends on Core. |
| `Elevator.Solution.Host` | library | net48 | The AutoCAD-side entry point that runs a generation request from the desktop application. Depends on Core, Bridge and AutoCAD. |
| `Elevator.Solution.StepExport` | exe | net48 | The SOLIDWORKS worker that builds the STEP model. Depends on Core and Bridge; started as a process. |
| `Elevator.Solution.Desktop` | WinExe (`ShaftMaster.exe`) | net48 | The WPF application: one view per page, the shell, the WPF figure rasterizer. Depends on Core, Reporting and Bridge, not on StepExport (it launches the worker), so it builds without SOLIDWORKS. |

```
                    Elevator.Solution.Desktop  (ShaftMaster.exe, WPF)
                     |            |          |
                     v            v          v
             Elevator.Reporting   |   Elevator.Solution.Bridge <------------+
                     |            |          ^         ^                    |
                     +------+-----+          |         |                    |
                            v                |         |                    |
                       Elevator.Core <-------+---------+------+             |
                            ^                |         |      |             |
                            |     Elevator.Solution.Host   Elevator.Solution.StepExport
                            |                |
                      Elevator.AutoCAD <-----+
```

Rules (the project references enforce them): Core depends on nothing project-specific; Reporting and Bridge depend on Core; the
AutoCAD adapter and Host depend on Core and Bridge; StepExport depends on Core and Bridge; Desktop does not depend on StepExport. Assembly and
namespace names stay `Elevator.*` (the AutoCAD command names and NETLOAD paths depend on them).

## Component folders

The code is grouped by lift component, not by technical layer.

**Elevator.Core** (namespace `Elevator.Core.<Folder>`): `Application` (project session, preset flow, live calculation), `Calculations` (modes,
fingerprints, issues, chassis inputs), `Car`, `Counterweight`, `Doors`, `Geometry` (local-frame points, vertical section, section A-A, layout),
`GuideRails` (rail rules, brackets, FF1 / FF2), `MachineRoom` (machine, room, chassis, section B-B, beam, rules), `Presets` (library, fields, catalogs,
rope sizing), `Project` (the project and its groups, JSON, floors, validator, design engine), `SafetyGear` (P5–P7), `Serialization`, `Shaft`,
`Suspension` (ropes, reeving, traction, grooves).

**Elevator.Reporting**: `Sheet` (the calculation sheet), `DesignNotes`, `Docx` (the writer), `Figures` (figure model and SVG).

**Elevator.AutoCAD**: by output — `Plan`, `MachineRoom`, `SectionAA`, `SectionBB`, `Vertical`, `Common` (commands, configuration, storage, updaters).

**Elevator.Solution.Desktop**: `Shell` (window, navigation, test facade), `Pages` (About, Calculations, CarDoors, Drawings, Floors, General, Machine,
Rails, Shaft, Suspension, Validation), `Common` (WPF helpers, the figure rasterizer), `Assets` (brand assets).

## Data flow

```
 site survey        project (JSON)           calculations (Core engines)             outputs
 -----------        --------------           ---------------------------             -------
 shaft W x D   -->  ElevatorProject    -->   Basic / Advanced chassis P1-P4    -->   native DWG views  (Host + AutoCAD)
 floors, Q, v       + presets (lock /        rope sizing + rope / traction           calculation sheet .docx (Reporting)
 (the only          follow)                  safety gear / pit P5-P7                 Design Notes .docx      (Reporting)
 mandatory          fingerprints mark        car guide forces FF1 / FF2              STEP model (StepExport -> SOLIDWORKS)
 input)             results current          machine-room and section rules
```

1. **Survey → project.** The site survey (shaft width and depth, floors, rated load) is the only mandatory input. Every other input starts from an
   editable preset ([`presets/PRESETS.md`](../presets/PRESETS.md)); an edited value is locked, an unlocked value follows upstream changes. The
   project is one JSON document (`ElevatorProject`, [`ELEVATOR_PROJECT_MODEL.md`](ELEVATOR_PROJECT_MODEL.md)) that is also stored inside the DWG.
2. **Project → calculations.** Core engines read the project and the reviewed design context. Each result carries an input fingerprint, so a changed
   input marks exactly the results that use it as not current; the desktop application recalculates about 300 ms after any input
   ([`calculations/`](../calculations/)).
3. **Calculations → outputs.** Drawings come from the shared design geometry through the Bridge to the AutoCAD host; the calculation sheet and the
   Design Notes are built from the results by Reporting without Word; the STEP model is built by the worker from the generation parameters
   ([`STEP-EXPORT.md`](STEP-EXPORT.md)). Code remarks and NOT OK items go only to the Design Notes.

## Tests

Three tiers, one command (`tools/test.ps1`); see [`testing/TESTING.md`](../testing/TESTING.md). Tier 1 (xUnit on net8.0 and net48, golden master, rope hand
check, two WPF console programs) needs no AutoCAD and no Word.
