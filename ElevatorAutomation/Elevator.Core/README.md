# Elevator.Core

**Purpose.** The project model, the preset library, every sizing rule and calculation engine, and the UI-free application services (project session, preset flow, live calculation). The portable heart of ShaftMaster.

**Main namespaces.** `Elevator.Core.Application`, `.Calculations`, `.Car`, `.Counterweight`, `.Doors`, `.Geometry`, `.GuideRails`, `.MachineRoom`, `.Presets`, `.Project`, `.SafetyGear`, `.Serialization`, `.Shaft`, `.Suspension` (one folder per lift component).

**Dependencies.** netstandard2.0 and net48. Nothing project-specific; no WPF, AutoCAD, SOLIDWORKS or Office types. JSON goes through `CoreJson` / `IJsonSerializer` (`JavaScriptSerializer` on net48, `JavaScriptCompatibleJson` where chosen).

**How to test.** `./tools/test.ps1 -Suite core-net8.0,core-net48` (xUnit, `tests/Elevator.Core.Tests`: golden Core captures, presets and Q sweep, rope / traction, FF, safety / pit, application services, serializer parity).

Overview of all projects: [`docs/architecture/OVERVIEW.md`](../docs/architecture/OVERVIEW.md).
