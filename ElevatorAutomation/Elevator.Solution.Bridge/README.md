# Elevator.Solution.Bridge

**Purpose.** The contracts between the desktop application, the AutoCAD host and the STEP worker, and the clients that use them. One file per contract, JSON wire format (`Wire`), explicit `BridgeContract.Version` checked by the host.

**Main namespaces.** `Elevator.Solution.Bridge`.

**Dependencies.** `Elevator.Core`. net48.

**How to test.** `./tools/test.ps1 -Suite core-net48` (serializer parity covers the contracts); tier 3 `integration` exercises them end to end.

Overview of all projects: [`docs/architecture/OVERVIEW.md`](../docs/architecture/OVERVIEW.md).
