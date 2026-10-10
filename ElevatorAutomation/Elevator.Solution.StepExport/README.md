# Elevator.Solution.StepExport

**Purpose.** The SOLIDWORKS worker that builds the STEP model from the generation parameters. Started as a process by the desktop application.

**Main namespaces.** `Elevator.Solution.StepExport`.

**Dependencies.** `Elevator.Core`, `Elevator.Solution.Bridge`; SOLIDWORKS interop assemblies (`SolidWorksDir`). net48.

**How to test.** STEP input and parameters: `./tools/test.ps1 -Suite integration`. The export itself needs a licensed SOLIDWORKS session and is tested by hand.

Overview of all projects: [`docs/architecture/OVERVIEW.md`](../docs/architecture/OVERVIEW.md).
