# Elevator.Solution.Host

**Purpose.** The AutoCAD-side entry point (`HostEntry`) that runs a generation request sent by the desktop application and writes the response.

**Main namespaces.** `Elevator.Solution.Host`.

**Dependencies.** `Elevator.Core`, `Elevator.Solution.Bridge`, `Elevator.AutoCAD`; AutoCAD 2023 assemblies. net48.

**How to test.** `./tools/test.ps1 -Tier 3` (native generate / reopen through the host).

Overview of all projects: [`docs/architecture/OVERVIEW.md`](../docs/architecture/OVERVIEW.md).
