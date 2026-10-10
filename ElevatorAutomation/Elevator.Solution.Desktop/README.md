# Elevator.Solution.Desktop

**Purpose.** The WPF application `ShaftMaster.exe`: one view per page, the shell and navigation, the WPF figure rasterizer, the project, preset and calculation pages.

**Main namespaces.** `Elevator.Solution.Desktop.Shell`, `.Pages.*` (About, Calculations, CarDoors, Drawings, Floors, General, Machine, Rails, Shaft, Suspension, Validation), `.Common`.

**Dependencies.** `Elevator.Core`, `Elevator.Reporting`, `Elevator.Solution.Bridge`; not StepExport (it starts the worker). net48, WPF.

**How to test.** `./tools/test.ps1 -Tier 2` (programmatic WPF suites through the internal `TestAutomation` facade; needs a desktop session).

Overview of all projects: [`docs/architecture/OVERVIEW.md`](../docs/architecture/OVERVIEW.md).
