# Refactor verification — 13 September 2026

Executed with AutoCAD 2023 Core Console (T.53.0.0) on disposable drawing copies. The preserved original, the current user's drawing, the working old plugin and its earlier outputs were not changed.

## Results

* 43 original pure design-rule assertions passed against `Elevator.Core.dll`, which has no Autodesk references.
* 17 original CAD regression records passed: seven updated current drawings, seven saved-file reopens, the translation/rotation setup and two invalid-size rejections without geometry changes.
* 14 architecture records passed, including seven comparisons against the actual pre-refactor saved DWGs, semantic editing/persistence checks and master creation.
* The actual command-line workflow passed: `ELEVATORPROJECT`, repeated `ELEVATORUPDATE`, `UPDATEALL`, another `ELEVATORPROJECT` edit and `UPDATEALL`. Its saved output was reopened independently and verified.

No failing records remain in `tests/cad-test-results.json`, `tests/architecture-results.json` or `tests/workflow-results.json`. Console transcripts are in `tests/generated/`.

## Same SW/SD cases

| Order | SW × SD (mm) | Cabin (mm) | Car DBG | CW DBG | Door | TZ | Result |
|---|---|---|---|---|---|---|---|
| 1 | 1450 × 1600 | 1050 × 1200 | 1150 | 1040 | 900 | 750 | Matches old output |
| 2 | 1800 × 2000 | 1400 × 1600 | 1500 | 1040 | 1000 | 950 | Matches old output |
| 3 | 1200 × 1400 | 800 × 1000 | 900 | 740 | 600 | 650 | Matches old output |
| 4 | 1450 × 1600 | 1050 × 1200 | 1150 | 1040 | 900 | 750 | Matches old output |
| 5 | 1450 × 1600 repeated | 1050 × 1200 | 1150 | 1040 | 900 | 750 | Matches old output |
| 6 | 1600 × 1800 after rotation/translation | 1200 × 1400 | 1300 | 1040 | 1000 | 850 | Matches old output |
| 7 | 2000 × 1800 after rotation/translation | 1600 × 1400 | 1700 | 1040 | 1000 | 850 | Matches old output |

Rejected sizes remain 1100 × 1400 (no fitting door) and 1450 × 800 (native governor detail outside shaft).

## What “unchanged output” means

The seven actual saved DWGs from the old regression run are the independent golden references. Every new saved case is reopened and compared in its shaft-relative frame: all 521 bound entity types/styles, polyline vertices/bulges, sampled native curves, hatch areas/extents, MText and leader positions, all 31 dimension measurements, extension points, dimension lines and text positions. The tolerance remains 0.02 mm. No geometry/formula change was needed for the refactor.

DWGs are not byte-identical: semantic XData, project data, embedded binding manifests, timestamps and normal AutoCAD save metadata differ. Validation samples curves; it does not compare every binary spline/hatch serialization detail. Native regression is automated through Core Console, not a manual desktop visual review.

The runner uses separate real AutoCAD commands. An initial test that ran the whole sequence inside one command exposed different dimension-association callback timing; using the same command boundaries as the original regression produced matching dimension extension locations in every case.

## Architecture checks

* A bound car-guide reference was cloned, giving it a new native handle; the original was erased and the replacement moved to layer `0`. Updating and saved-file reopening still passed.
* Added unbound geometry survived; model count rose from 521 to 522 without preventing updates.
* Duplicate semantic IDs and deleted semantic IDs were rejected. No handle fallback repairs missing tags silently.
* Unicode project name, future optional values and extension data survived save/reopen.
* Unsupported future project schema versions and unconfigured elevator families were rejected.
* A tagged master was produced without resizing the original geometry.

The four additional family masters remain pending actual family drawings/profiles. Their catalog entries are disabled; this test run does not validate hydraulic, MRL, gearbox or home-lift designs.

## Source protection

At task start, current `plan codex.dwg` SHA-256 was `DA5449721797C26D3D0CDE3836AA9F5FD76CA2F49D6B1AE83C6DC23F17578C9A`.

The preserved regression template SHA-256 was `5F2CE6B190811338D1E446EEEAC9F17E0EE3B6A8DF563654EB98F324B4649447`. Testing uses that preserved file's disposable copies, not the edited current drawing.
