# Plan automation update

## New automation

* Optional project-driven title on the existing styled plan caption, preserving its printed 1:8 text.
* Persistent text recipes for bound MText and multileaders, using one shared Core value renderer for project metadata, calculated sizes and clearance values.
* Additional plan annotations can be semantically named and attached to existing shaft/cabin/door/CW reference movements through ELEVATORANNOTATION.
* UPDATEALL reports unbound entities and writes plan coverage plus rendered annotation values to its output report. ELEVATORPLANAUDIT provides the same read-only coverage inventory on demand.
* All eight existing hatches are regenerated and checked for area, loop count, pattern, scale, angle and expected position during updates.
* Equipment clearances use native bounds after conversion into the shaft frame. This includes bulged polylines, arcs, splines and hatches; the minimum front clearance is now reported. Nonfinite dimension validation values are rejected.

All original equipment, hatch and annotation **positions were already automated**. No new physical equipment shapes were invented, and the validated geometry calculations were not redesigned. Only PlanUpdater is registered by UPDATEALL.

## Project fields

New optional field: **PlanTitle**. No new required geometry input. ELEVATORPLANOPTIONS exposes PlanTitle and the already existing ProjectName. SW and SD remain the only sizing inputs. Recipes are persistent semantic binding metadata, not duplicated project parameters or manual derived dimensions.

## Regression results

The packaged `tests/run-tests.ps1` completed successfully in AutoCAD 2023 Core Console:

| Suite | Result |
|---|---|
| Existing Core sizing assertions | 43 passed |
| New Core annotation-rule checks | 6 passed |
| Existing native CAD regression records | 17 passed |
| Architecture/golden-reference checks | 14 passed |
| Existing command workflow | 1 passed |
| New plan automation checks | 7 passed |

All seven previous saved DWG golden comparisons remain passing at 0.02 mm tolerance with optional annotation features unused. The same SW/SD sequences, repeat updates, translation/rotation, geometry/style preservation, saved-file reopening and invalid-size rejections remain covered.

New plan tests cover optional command data entry; literal Unicode/backslash/brace/newline text; derived MText and multileader content; a newly bound annotation; one reported unbound object; save/reopen of recipes and data; repeated resizing; rotation; hatch validity; invalid-token rollback; restoring the default title; and rejecting NaN validation values. The new annotated plan test intentionally adds test-only objects and is not a production master.

Reports: `tests/plan-results.json`, `tests/architecture-results.json`, `tests/cad-test-results.json`, `tests/workflow-results.json`. Native transcripts are under `tests/generated`. The annotation selection command is compiled, but the automated tests configure semantic recipes through the database API; desktop mouse selection itself was not exercised. No manual desktop visual sign-off is claimed.

## Remaining manual decisions

* P5/P6/P7 identifier meaning and numbering rules.
* Rail/governor selection or physical equipment changes; current native details and labels remain unchanged.
* Counterweight mass/capacity sizing and thickness selection; thickness still comes from the template outline.
* Other door mechanisms, entrances and counterweight arrangements; optional future model fields do not enable these geometries yet.
* Printed scale versus actual plotting requirements; the supplied 1:8 caption is preserved.
* Choosing desired annotation recipes and checking placement/readability of long custom labels. Existing text heights and styles are deliberately retained.
* Binding or removing newly introduced equipment geometry reported as unbound. The annotation command can bind text, not arbitrary new equipment assemblies.

For the validated plan, every original component participates in the update. These outstanding design/drafting decisions prevent an unconditional production-release claim for all projects or equipment variants.

## AutoCAD use

Restart AutoCAD after rebuilding, NETLOAD `bin/Elevator.AutoCAD.dll`, open a project copy and run ELEVATORPROJECT. Optionally run ELEVATORPLANOPTIONS and ELEVATORANNOTATION. Run **UPDATEALL** to update the plan and save a separate DWG. Use ELEVATORPLANAUDIT to review coverage. No additional drawing view was created.
