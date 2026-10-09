# ShaftMaster

ShaftMaster is a Windows desktop application for designing geared traction elevators (1:1 roping). From a site survey and editable presets it:

- builds the elevator project (shaft, car, rated load, speed, masses, ropes, sheave, safety gear and buffers);
- runs the calculations: machine-room loads, safety gear and pit loads, car guide-rail forces, and rope and traction checks;
- exports a Word calculation sheet and a separate Design Notes document;
- generates native AutoCAD drawings (plan, machine room, sections) and, optionally, a STEP model through SOLIDWORKS.

The agreed design flow, presets and build stages are in [DESIGN-FLOW.md](DESIGN-FLOW.md). It is binding for all work.

## Prerequisites

- Windows x64 with .NET Framework 4.8 (the build uses the framework's `csc.exe`; no Visual Studio is needed).
- AutoCAD 2023: needed to build (its managed assemblies are referenced) and to generate native drawings.
- SOLIDWORKS (licensed): its interop assemblies are needed to build the STEP worker, and SOLIDWORKS itself for STEP export.
- Python 3 with `python-docx` and Word: only for the rendered calculation-sheet checks in the test suites.

## Build and run

Clone into a short folder path (for example `C:\src\ShaftMaster`): some legacy test files have long names and exceed the Windows 260-character path limit under a deep folder.

Build into a new, separate folder. Without `-BinDirectory` the script writes to `bin-shaftmaster-v01`, which is a release folder.

```powershell
cd ElevatorAutomation
./build-solution.ps1 -BinDirectory bin-local
./bin-local/ShaftMaster.exe
```

Use `-AutoCADDirectory` and `-SolidWorksDirectory` when they are not installed in the default folders. Release builds are kept in `ElevatorAutomation/bin-shaftmaster-vNN/` with release notes and SHA-256 checksums; these folders are not in the repository.

Core tests, for example, after a build:

```powershell
cd ElevatorAutomation
./tests/solution/run-presets-core.ps1 -CandidateBin bin-local -EvidenceDirectory output/local/presets-core
./tests/solution/run-rope-traction-core.ps1 -CandidateBin bin-local -EvidenceDirectory output/local/rope-core
```

The other suites (UI, calculation sheet, native AutoCAD, STEP) are under `ElevatorAutomation/tests/`. Tests that need local reference files kept outside the repository (for example the original project drawings) skip with a message when those files are absent.

## Repository structure

| Path | Contents |
|---|---|
| `ElevatorAutomation/Elevator.Core/` | Project model, presets, design rules and calculations (no CAD dependency) |
| `ElevatorAutomation/Elevator.AutoCAD/` | AutoCAD 2023 drawing reader / updater for the native views |
| `ElevatorAutomation/Elevator.Solution.*` | ShaftMaster WPF application (`Desktop`), AutoCAD host, shared bridge and the SOLIDWORKS STEP worker |
| `ElevatorAutomation/config/` | Presets (`presets.json`), reviewed design context, seeds and JSON schema |
| `ElevatorAutomation/Templates/` | Native AutoCAD template drawings |
| `ElevatorAutomation/docs/` | Calculation, presets, sheet and export documentation |
| `ElevatorAutomation/tests/` | Core, UI, native and regression test suites |
| `legacy/ElevatorPlanGenerator/` | The earlier AutoCAD plan plugin, kept as a regression reference |
| `DESIGN-FLOW.md` | Agreed design flow and build stages |
| `CALCULATION_ROADMAP.md`, `SESSION_HANDOFF.md` | Calculation inventory and the current work status |

## License

Proprietary. All rights reserved; see [LICENSE](LICENSE).
