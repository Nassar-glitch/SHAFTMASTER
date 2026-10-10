# Elevator.Reporting

**Purpose.** The calculation sheet, the Design Notes, the DOCX writer, the figure model and the SVG renderer. Builds documents without Word.

**Main namespaces.** `Elevator.Reporting.Sheet`, `.DesignNotes`, `.Docx`, `.Figures`.

**Dependencies.** `Elevator.Core`. netstandard2.0 and net48. The PNG rasterizer is an interface (`IFigureRasterizer`); the WPF implementation lives in the desktop application.

**How to test.** `./tools/test.ps1 -Suite reporting-net8.0,reporting-net48,golden,sheet-wpf` (xUnit `tests/Elevator.Reporting.Tests`; the two console programs need WPF).

Overview of all projects: [`docs/architecture/OVERVIEW.md`](../docs/architecture/OVERVIEW.md).
