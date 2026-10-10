# Elevator.AutoCAD

**Purpose.** The native AutoCAD 2023 drawing adapter: reads a DWG, updates the plan, machine room, section A-A, section B-B and the vertical section, and keeps the project data and semantic bindings inside the DWG. Commands: `ELEVATORPROJECT`, `ELEVATORUPDATE`, `UPDATEALL` and the view commands.

**Main namespaces.** `Elevator.AutoCAD.Plan`, `.MachineRoom`, `.SectionAA`, `.SectionBB`, `.Vertical`, `.Common` (one folder per output).

**Dependencies.** `Elevator.Core`; AutoCAD 2023 managed assemblies (`AutoCADDir`, not copied). net48. Assembly name and command names are fixed (NETLOAD paths).

**How to test.** Tier 3 needs AutoCAD 2023: `./tools/test.ps1 -Tier 3` (native generate / reopen, four-view comparison with the release Core). The script suites of `tests/run-tests.ps1` are not part of the tiers.

Overview of all projects: [`docs/architecture/OVERVIEW.md`](../docs/architecture/OVERVIEW.md).
