# Golden diff summary: blank revision shown as 00 (2026-10-12)

User decision: the calculation sheet and the Design Notes show the revision as NN, and a blank revision is 00 ("Rev 00" in the title
block and the footer). Proof by `tests/golden/verify_revision_only.py approved mismatch`: for every changed `sheet.txt` /
`design-notes.txt` the only differences are

- the line pair `Revision` / `00` in the sheet's document block (sheet only; the Design Notes have no such block),
- the line pair `Rev.` / `00` in the page-header title block,
- the footer: the empty right footer paragraph now holds `Rev 00`, or `Doc. No. <n>` became `Doc. No. <n> · Rev 00`.

No other file changed: no `results.json`, `fingerprints.json`, `roundtrip.elevator.json`, figure or omitted file. 93 files in 47 fixtures
were re-approved (46 `sheet.txt`, 47 `design-notes.txt`). `titleblock-filled` stores revision "A": it still loads, is shown as stored and
reported by the validation, and its fixture did **not** change (not re-approved).

| fixture | file | revision-only | Revision row | Rev. header row | footer Rev 00 |
|---|---|---|---|---|---|
| advanced-complete | design-notes.txt | yes | 0 | 1 | 1 |
| advanced-complete | sheet.txt | yes | 1 | 1 | 1 |
| advanced-missing-cg | design-notes.txt | yes | 0 | 1 | 1 |
| advanced-missing-cg | sheet.txt | yes | 1 | 1 | 1 |
| basic-complete | design-notes.txt | yes | 0 | 1 | 1 |
| basic-complete | sheet.txt | yes | 1 | 1 | 1 |
| doors-2c-1200-no-fit | design-notes.txt | yes | 0 | 1 | 1 |
| doors-2c-1200-no-fit | sheet.txt | yes | 1 | 1 | 1 |
| doors-2c-800-1720 | design-notes.txt | yes | 0 | 1 | 1 |
| doors-2c-800-1720 | sheet.txt | yes | 1 | 1 | 1 |
| doors-2c-auto | design-notes.txt | yes | 0 | 1 | 1 |
| doors-2c-auto | sheet.txt | yes | 1 | 1 | 1 |
| doors-2c-offset-60-right | design-notes.txt | yes | 0 | 1 | 1 |
| doors-2c-offset-60-right | sheet.txt | yes | 1 | 1 | 1 |
| doors-2s-700-1300 | design-notes.txt | yes | 0 | 1 | 1 |
| doors-2s-700-1300 | sheet.txt | yes | 1 | 1 | 1 |
| doors-2s-800-1720 | design-notes.txt | yes | 0 | 1 | 1 |
| doors-2s-800-1720 | sheet.txt | yes | 1 | 1 | 1 |
| doors-2s-auto | design-notes.txt | yes | 0 | 1 | 1 |
| doors-2s-auto | sheet.txt | yes | 1 | 1 | 1 |
| doors-2s-left-1000 | design-notes.txt | yes | 0 | 1 | 1 |
| doors-2s-left-1000 | sheet.txt | yes | 1 | 1 | 1 |
| doors-2s-offset-40-left | design-notes.txt | yes | 0 | 1 | 1 |
| doors-2s-offset-40-left | sheet.txt | yes | 1 | 1 | 1 |
| doors-2s-offset-out-of-range | design-notes.txt | yes | 0 | 1 | 1 |
| doors-2s-offset-out-of-range | sheet.txt | yes | 1 | 1 | 1 |
| doors-2s-old-opening-offset | design-notes.txt | yes | 0 | 1 | 1 |
| doors-2s-old-opening-offset | sheet.txt | yes | 1 | 1 | 1 |
| doors-2s-shifted | design-notes.txt | yes | 0 | 1 | 1 |
| doors-2s-shifted | sheet.txt | yes | 1 | 1 | 1 |
| locked-beta-95 | design-notes.txt | yes | 0 | 1 | 1 |
| locked-beta-95 | sheet.txt | yes | 1 | 1 | 1 |
| locked-counterweight-q1000 | design-notes.txt | yes | 0 | 1 | 1 |
| locked-counterweight-q1000 | sheet.txt | yes | 1 | 1 | 1 |
| locked-groove-type | design-notes.txt | yes | 0 | 1 | 1 |
| locked-groove-type | sheet.txt | yes | 1 | 1 | 1 |
| locked-mbl-q320 | design-notes.txt | yes | 0 | 1 | 1 |
| locked-mbl-q320 | sheet.txt | yes | 1 | 1 | 1 |
| locked-rope-count-7 | design-notes.txt | yes | 0 | 1 | 1 |
| locked-rope-count-7 | sheet.txt | yes | 1 | 1 | 1 |
| locked-rope-count-and-diameter | design-notes.txt | yes | 0 | 1 | 1 |
| locked-rope-count-and-diameter | sheet.txt | yes | 1 | 1 | 1 |
| locked-sheave-q1600 | design-notes.txt | yes | 0 | 1 | 1 |
| locked-sheave-q1600 | sheet.txt | yes | 1 | 1 | 1 |
| malformed-text | design-notes.txt | yes | 0 | 1 | 1 |
| malformed-text | sheet.txt | yes | 1 | 1 | 1 |
| malformed-values | design-notes.txt | yes | 0 | 1 | 1 |
| malformed-values | sheet.txt | yes | 1 | 1 | 1 |
| missing-h | design-notes.txt | yes | 0 | 1 | 1 |
| missing-h | sheet.txt | yes | 1 | 1 | 1 |
| missing-masses-and-pit | design-notes.txt | yes | 0 | 1 | 1 |
| missing-masses-and-pit | sheet.txt | yes | 1 | 1 | 1 |
| missing-rope-count | design-notes.txt | yes | 0 | 1 | 1 |
| missing-rope-count | sheet.txt | yes | 1 | 1 | 1 |
| new-project-q630 | design-notes.txt | yes | 0 | 1 | 1 |
| new-project-q630 | sheet.txt | yes | 1 | 1 | 1 |
| older-no-report-group | design-notes.txt | yes | 0 | 1 | 1 |
| older-no-report-group | sheet.txt | yes | 1 | 1 | 1 |
| older-v01-example | design-notes.txt | yes | 0 | 1 | 1 |
| rope-traction-not-ok | design-notes.txt | yes | 0 | 1 | 1 |
| rope-traction-not-ok | sheet.txt | yes | 1 | 1 | 1 |
| rope-traction-ok | design-notes.txt | yes | 0 | 1 | 1 |
| rope-traction-ok | sheet.txt | yes | 1 | 1 | 1 |
| sweep-survey-q0320 | design-notes.txt | yes | 0 | 1 | 1 |
| sweep-survey-q0320 | sheet.txt | yes | 1 | 1 | 1 |
| sweep-survey-q0480 | design-notes.txt | yes | 0 | 1 | 1 |
| sweep-survey-q0480 | sheet.txt | yes | 1 | 1 | 1 |
| sweep-survey-q0630 | design-notes.txt | yes | 0 | 1 | 1 |
| sweep-survey-q0630 | sheet.txt | yes | 1 | 1 | 1 |
| sweep-survey-q0800 | design-notes.txt | yes | 0 | 1 | 1 |
| sweep-survey-q0800 | sheet.txt | yes | 1 | 1 | 1 |
| sweep-survey-q1000 | design-notes.txt | yes | 0 | 1 | 1 |
| sweep-survey-q1000 | sheet.txt | yes | 1 | 1 | 1 |
| sweep-survey-q1275 | design-notes.txt | yes | 0 | 1 | 1 |
| sweep-survey-q1275 | sheet.txt | yes | 1 | 1 | 1 |
| sweep-survey-q1600 | design-notes.txt | yes | 0 | 1 | 1 |
| sweep-survey-q1600 | sheet.txt | yes | 1 | 1 | 1 |
| sweep-table6-q0320 | design-notes.txt | yes | 0 | 1 | 1 |
| sweep-table6-q0320 | sheet.txt | yes | 1 | 1 | 1 |
| sweep-table6-q0480 | design-notes.txt | yes | 0 | 1 | 1 |
| sweep-table6-q0480 | sheet.txt | yes | 1 | 1 | 1 |
| sweep-table6-q0630 | design-notes.txt | yes | 0 | 1 | 1 |
| sweep-table6-q0630 | sheet.txt | yes | 1 | 1 | 1 |
| sweep-table6-q0800 | design-notes.txt | yes | 0 | 1 | 1 |
| sweep-table6-q0800 | sheet.txt | yes | 1 | 1 | 1 |
| sweep-table6-q1000 | design-notes.txt | yes | 0 | 1 | 1 |
| sweep-table6-q1000 | sheet.txt | yes | 1 | 1 | 1 |
| sweep-table6-q1275 | design-notes.txt | yes | 0 | 1 | 1 |
| sweep-table6-q1275 | sheet.txt | yes | 1 | 1 | 1 |
| sweep-table6-q1600 | design-notes.txt | yes | 0 | 1 | 1 |
| sweep-table6-q1600 | sheet.txt | yes | 1 | 1 | 1 |
| titleblock-blank | design-notes.txt | yes | 0 | 1 | 1 |
| titleblock-blank | sheet.txt | yes | 1 | 1 | 1 |
