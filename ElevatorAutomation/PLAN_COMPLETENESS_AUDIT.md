# Plan audit before further geometry changes

The supplied plan contains 521 model-space entities, all already semantically bound: 262 polylines, 131 lines, 68 arcs, 31 dimensions, 12 MTexts, eight hatches, five splines and four multileaders. No original model entity is wholly outside the current geometry updater. This audit covers the validated ExistingPlan master, not an assumption that arbitrary edited drawings have the same coverage.

| Component | Current automation | Remaining work / decision |
|---|---|---|
| Shaft and cabin | Outline sizing, entrance datum, centered placement | Preserve validated geometry; no new sizing rule needed |
| Car/CW rails and supports | Placement and DBG; native profiles translate intact | Profile selection needs equipment rules beyond SW/SD |
| Counterweight | Width, position and DBG | Thickness remains the native outline depth; mass/capacity sizing unspecified |
| Door | Preferred opening width and centered entrance geometry | Other mechanisms/entrances have no supplied rules |
| Reference lines | Shaft/cabin/CW lines and existing A markers move | A markers remain plan symbols, not new automated views |
| Dimensions | All 31 updated and independently checked | Preserve labels, style, native associations and regression |
| Hatches | All eight move with their rigid components | Add in-command regeneration/validation and check local extents |
| Governor/other equipment | Native shape retained and positioned | Selection, resizing and equipment variants require additional rules |
| Text/annotations | All text/leader positions move; contents remain static | Add optional project-driven title and semantic annotation templates |
| New unbound objects | Preserved, not updated | Report explicitly so omissions are visible |
| Clearances | Existing envelope checks and dimension calculations | Use local-frame native bounds, report front clearance too |

New required geometry parameters: **none**. Add optional `PlanTitle`; reuse existing ProjectName and derived dimensions for annotations. Annotations must not require entering derived values manually.

Ambiguous rules: P5/P6/P7 identifiers; selecting car rail T70/65/9, CW rail T50/50/5 or governor equipment; interpreting the printed 1:8 as a plot-scale requirement; other entrance/CW arrangements; capacity-based CW mass. These remain unchanged, not inferred. Any new annotation recipe is stored against an existing semantic ID and must not change physical equipment geometry.

Proceed with project-driven annotations, coverage reporting, hatch validation and clearer clearance checks. Preserve the current plan geometry and golden outputs when optional annotations are unused. UPDATEALL continues to run the PlanUpdater only.
