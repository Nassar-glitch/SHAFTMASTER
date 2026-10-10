# Machine room plan: engineering traceability

Reviewed against the **supplied EN 81-20:2014**, `Standards/EN 81-20-2014.pdf`, 168 PDF pages, SHA256 `79A69662D45EEC8D2FCC8C685BB1AA5D149339BB93CADD287726E8DC9783F98E`. This is a scoped interpretation of that edition, not a determination of the currently applicable jurisdiction/edition or whole-lift certification. Clause references below belong to that supplied edition. Interpretations are paraphrased.

## Implemented numerical code check

| Registry ID / clauses | Interpretation | Required input | CAD/design effect | Validation |
|---|---|---|---|---|
| MR-ROPE-001: 5.5.1.2(a), 5.5.2.1 | Suspension rope nominal diameter must be at least 8 mm; sheave/pulley/drum pitch diameter divided by nominal rope diameter must be at least 40. | Selected deflector pitch diameter; suspension rope diameter and confirmation that this component carries suspension ropes. | Keep the requested project layout selection; record FAIL and print a code-conflict warning when incompatible. Never silently substitute a different sheave. | Below 320 mm cannot satisfy both requirements. At 400 mm, evaluate both limits when rope diameter is supplied; otherwise NOT YET EVALUABLE. Tests include 250 mm conflict, 400/10 numeric pass, 400/11 fail. |

Consequently, the **250 mm project selection conflicts with these two requirements for a suspension-rope sheave**. A numeric pass covers only these two checks. It does not verify grooves, rope construction, guards, traction, strength or a manufacturer's assembly. The nearby Ø250 governor is a different component and must not be evaluated using the suspension-sheave rule.

The available market diameters remain **250 and 400 mm only**. The 320 mm number above is solely the mathematical consequence of the two code limits, not an available component or an automatic substitution. No 320, 450 or other diameter is introduced. Code conflicts remain non-blocking for CAD layout generation.

## Independent validation results

`MachineRoomParameters.LayoutStatus` records successful native layout validation (PASS), or traction/reeving review above TZ 1100. It is not an engineering approval of every retained component. `Reeving.WrapAngleStatus` separately records the project wrap check and currently remains NOT YET EVALUABLE. `MachineRoomParameters.CodeDiameterRatioStatus` independently reports the suspension diameter check. All three are saved together in the DWG's `MachineRoomValidation` Xrecord and displayed by UPDATEALL; the shared design JSON also contains them.

A future result may legitimately be LayoutStatus=PASS, WrapAngleStatus=PASS, CodeDiameterRatioStatus=FAIL. Changing vertical offset or chassis elevation does not change D/d and cannot clear the diameter check. Likewise a project wrap pass does not establish the full EN 81-50 traction verification.

The future model includes rope entry/exit angles, actual wrap (`CalculatedWrapAngleDeg`), target 152°, horizontal/vertical center offsets, sheave pitch diameters/centers and required chassis elevation adjustment. Uncalculated quantities remain null. The declared future optimization strategy first changes vertical center relationship through machine/chassis elevation and/or deflector placement while retaining the selected 250/400 mm diameter. Diameter substitution is disabled. No vertical solver or additional view is implemented.

## Identified code requirements: NOT YET EVALUABLE

These are registered and reported; the current layout does not assert a pass. Retaining a drawing feature is not evidence that the corresponding safety requirement is satisfied.

| Registry ID / clauses | Short interpretation | Missing input | CAD/design effect and future validation |
|---|---|---|---|
| MR-ROOM-001: 5.2.6.3.2.1(a) | Working headroom at least 2100 mm; panel working depth at least 700 mm and width at least the larger of 500 mm or full panel width. | Actual cabinet operating face, service zone, obstructions and headroom. | Preserve panel. Later compare the unobstructed service polygon and vertical clearance with these limits. |
| MR-ROOM-002: 5.2.6.3.2.1(b) | At required moving-part maintenance/inspection and emergency-operation points, provide at least 500 × 600 mm clear working area. | Manufacturer's service points and occupied/swept envelopes. | Preserve machine; later test each required working zone for interference. |
| MR-ROOM-003: 5.2.6.3.2.2, .3 | Movement height at least 1800 mm; access route width at least 500 mm, with 400 mm permitted only under the stated absence of hot surfaces/moving parts; at least 300 mm above unprotected rotating machine parts. | Continuous routes, hazard classification, ceiling and equipment elevations. | Later verify route connectivity and vertical separations. Room extensions alone cannot establish these. |
| MR-OPEN-001: 5.2.6.3.3 | Floor openings should be limited to what is necessary; ferrules project at least 50 mm above finished floor/slab. | Rope swept paths, installation tolerances, opening usage and ferrule heights. | Keep reference openings; later compare envelope containment and ferrule height. |
| MR-ACCESS-001: 5.2.3.2(a), 5.2.3.3(a) | Machine-room access door clear height at least 2000 mm, width at least 600 mm; no inward opening. | Finished clear dimensions, leaf operation, locks and access arrangement. | The native approximately 900 mm opening is retained, but a drawn opening is not the finished usable width. Later validate the door assembly and access. |
| MR-TRACTION-001: 5.5.3, 5.5.2.2 | Traction must satisfy the specified loading/stopping conditions; rope safety factors and calculations apply. | Loads/masses, rope number and construction, breaking force, friction/grooves and full reeving geometry; EN 81-50. | With Section B-B installed, actual wrap is calculated against the project target; full traction compliance remains NOT YET EVALUABLE. EN 81-50:2014 5.11 covers traction calculation; 5.12 covers suspension-rope safety-factor evaluation. |
| MR-GUARD-001: 5.5.7.1/Table 10, 5.5.7.2, 5.9.1.2 | Provide applicable protection at sheaves/rotating equipment, considering location and rope direction, while retaining inspection/maintenance access. | Guards, rope directions, reachable spaces, retention details and perforations. | Preserve reference geometry; later check guard and maintenance envelopes. Table 10 conditions/footnotes must be applied individually. EN ISO 13857:2008 Table 4 is referenced for relevant perforations. |
| MR-GOV-001: 5.6.2.2.1.3, .4, .7 | Governor rope/sheave ratio at least 30, with the additional rope safety/tensioning provisions; governor accessibility and safety-component verification apply. | Governor rope diameter/strength, certified component, tripping data, tensioning and service access. | Retain Ø250 governor and apply only the engineer's placement rule. Component verification is referred to **EN 81-50:2014 5.4** by 5.6.2.2.1.7. |
| MR-VENT-001: 5.2.1.3 | Suitable ventilation of lift spaces; unrelated rooms must not ventilate through them. | Heat losses, ambient conditions, air paths and building ventilation design. | Preserve openings; no invented opening-area formula. Annex E.3.3 provides informative guidance. |

Other relevant identified provisions, not separately encoded: 5.5.1.2(b) refers rope properties to EN 12385-5; 5.5.1.3/.4 address minimum number and independence of suspension means. Sections 5.2.6.3.2.4/.5 address different working levels/recesses when present. These need equipment specifications or vertical geometry. Section B-B now supplies the reviewed vertical geometry; it does not resolve these component/code checks.

## Project rules and assumptions

| ID / type | Rule and implementation |
|---|---|
| MR-DS-001 / Project | Shared TZ <520: no deflector; 520 through 800: pitch 250; above 800 through 1100: pitch 400. Above 1100 retain the last drawing state and require traction/reeving review. These thresholds do not come from EN 81-20. |
| MR-GOV-OFFSET-001 / Project | From car guide axis to start of governor diameter: TZ <700 →100; 700≤TZ<850 →150; 850≤TZ≤1100 →200 mm. Above 1100 retain the existing offset and require review. Governor diameter remains 250. Boundary values explicitly confirmed by the engineer. |
| MR-WRAP-001 / Project | Target wrap 152°. Actual wrap and vertical center offset remain null in top-view-only projects; Section B-B calculates both from the coordinated circles and exact tangent. This is not labelled an EN minimum. |
| MR-STRUCT-001 / EngineeringAssumption | Retain native machine/chassis/P1–P4 details as a coherent assembly. Their selection, loading, bearing on concrete, reinforcement and strength require engineering review. No structural standard or manufacturer approval is invented. |

Machine-room extensions are measured from the reference: left/right 525 mm, front 900 mm, rear 0 mm. Preserving these is a reference-layout decision, not a code-derived room-sizing rule. Project extension fields are prepared but cannot change those measured values in this version.

## Storage and scope

`Elevator.Core/MachineRoomRules` creates a lightweight registry with source, edition, clause, input values, required/calculated values, status and engineering note. `MachineRoomUpdater` stores it in the DWG's `ELEVATOR_AUTOMATION` Named Objects Dictionary under `MachineRoomEngineeringRules`, and stores future reeving data under `MachineRoomReeving`. The shared design JSON saved beside each output DWG also includes these records. UPDATEALL reports code conflicts and review status in the command line. These results are not yet rendered as an on-sheet compliance schedule.

Needed next: manufacturer machine/chassis/guard and governor documentation, suspension rope data, load/structure calculations and the referenced EN 81-50 material. These inputs may change the engineering selection; the automated reference layout is not released for manufacture by passing its geometry regressions.

## Section B-B integration

The section adds `SECTION-HANGER-001`, `SECTION-BEAM-001` and `SECTION-SLAB-001` as project rules. Hanger adequacy remains MECHANICAL REVIEW REQUIRED; steel selection is PRELIMINARY or CALCULATION REQUIRED; concrete strength/reinforcement is NOT EVALUATED. Unspecified slab-clearance minimum is NOT YET EVALUABLE even where actual pitch-circle clearance is nonnegative. The section annotation and shared JSON show these statuses alongside the existing D/d status. Raising supports or lengthening the hanger never clears a diameter-ratio conflict. A 6.5 mm rope has a separate minimum-diameter failure, including when no deflector is selected.
