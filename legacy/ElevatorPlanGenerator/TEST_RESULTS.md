# First-version verification

Final code and binding configuration tested on AutoCAD 2023 Core Console on 7 September 2026. The command prompts were exercised directly with script input; the tests did not call a substitute update implementation.

**43 design-rule assertions passed**, covering formulas, door/CW preference boundaries and invalid numeric/size inputs.

| Run | SW × SD (mm) | Car W × D | Car DBG | CW DBG | Door | TZ | Result |
|---|---|---|---:|---:|---:|---:|---|
| 1 | 1450 × 1600 | 1050 × 1200 | 1150 | 1040 | 900 | 750 | Pass; output reopened |
| 2 | 1800 × 2000 | 1400 × 1600 | 1500 | 1040 | 1000 | 950 | Pass; output reopened |
| 3 | 1200 × 1400 | 800 × 1000 | 900 | 740 | 600 | 650 | Pass; output reopened |
| 4 | 1450 × 1600 | 1050 × 1200 | 1150 | 1040 | 900 | 750 | Pass after returning from another size |
| 5 | 1450 × 1600 | 1050 × 1200 | 1150 | 1040 | 900 | 750 | Pass on repeated identical input |
| 6 | 1600 × 1800 | 1200 × 1400 | 1300 | 1040 | 1000 | 850 | Pass after translation and rotation |
| 7 | 2000 × 1800 | 1600 × 1400 | 1700 | 1040 | 1000 | 850 | Pass while still rotated |

The pose test applied translation `(23456, -78210, 0)` and 0.37 radians of rotation in the XY plane. Output geometry was compared in the shaft-local frame against a direct transformation from the original source, checking for accumulated drift.

**Rejected inputs:** SW=1100, SD=1400 has no fitting catalogue door. SW=1450, SD=800 does not fit the preserved governor detail. Both failed before geometry changes, confirmed by before/after curve positions and dimension measurements.

Checks included all 521 model entities, layer/color/linetype/lineweight preservation, unchanged polyline topology and bulges, rigid profile radii, sampled curve geometry, hatch areas, unchanged text and dimension label formats, all 31 independent expected dimension values, and reading each saved DWG into a separate database.

Visual QA caught an initially swapped binding for the entrance-side dimensions. That binding was corrected, independent checks for all 31 dimensions were added to the updater, and the complete integration suite was rerun successfully. The corrected 1450 × 1600 preview shows both entrance-side dimensions as 275 mm.

Machine-readable results: `tests/cad-test-results.json` (17 passing records, zero failures: seven current-drawing checks, seven reopen checks, one pose setup and two rejection checks). Core Console logs are retained in `tests/integration-run.log`.

The original `plan codex.dwg` stayed byte-for-byte unchanged, SHA-256:

`5F2CE6B190811338D1E446EEEAC9F17E0EE3B6A8DF563654EB98F324B4649447`
