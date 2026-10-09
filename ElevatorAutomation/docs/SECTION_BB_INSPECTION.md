# Section B-B inspection and implementation status

Source: `Vert MR and Reeving codex.dwg`. Read-only shared-access snapshot SHA256 `B57E042BE9E36DB3F61471923035E00FCAB66E014FD019D40342020E1584D0BC`. The source was open in another process, so inspection used `tests/section/reference-copy.dwg`; no source drawing was changed.

## Native inventory

705 model entities: 426 polylines, 206 arcs, 30 hatches, 16 lines, 10 dimensions, six solids, four MTexts, three splines, three circles and one INSERT. Eight layers: 0, Defpoints, Car, CL, DL, PDF_0, PDF_Text and Wall. The sole inserted block is an anonymous six-entity detail at the deflector center. Most remaining equipment, Arabic isolation-pad notes and lettering are native fragmented geometry. The two paper-space blocks are empty.

The complete source audit is in `tests/section/structure.json`; the diagnostic overview is `tests/section/reference-overview.png`. The overview plots native curves and major dimension values; it is inspection evidence, not a generated coordinated section.

## Measured references

World positions below identify the inspected source only. They must not become runtime world-coordinate constants.

| Reference | Native measurement |
|---|---|
| Traction pitch circle | Diameter 520; center (1575.752461, 1988.134179) |
| Deflector pitch circle | Diameter 250; center (1200.613297, 1348.341185) |
| Horizontal center separation | 375.139165 |
| Vertical center separation | 639.792994 |
| Reference vertical dimension | 640.000125; its extension endpoint differs slightly from the actual traction center |
| Existing angle dimension | 2.796295452 radians = approximately 160.2169 degrees |
| Exact external-tangent wrap from the actual two circles | **160.102635776296 degrees** |
| Rear-side reference dimension | 129.996400 |
| TZ reference dimension | 759.999047 |
| Front-to-car reference dimension | 674.999769 |
| Sum of reference horizontal chain | 1564.995215; not the current shared 1600 mm baseline shaft |
| Upper longitudinal steel depth | 139.996378 |
| Lower support/stand depth | 140.003202 |
| Concrete height | 450 |

The three CL rope lines depict a vertical car drop at the traction circle's right side, a common tangent descending left to the deflector, and a vertical CW drop at the deflector's left side. Their endpoints contain small drafting discrepancies. Exact regenerated tangency must use the actual circle calculation, not reproduce the angle annotation or the old approximately vertical lines.

The machine drawing is rigid hardware associated with the traction circle. The longitudinal upper steel member bears on two lower 140 mm support/stand details, isolation pads and concrete supports. The deflector has a hub/shaft detail and a hanger reaching the upper steel member. Slab edges and two rope-drop openings are present; their widths should be preserved pending an opening-clearance requirement. No rope-groove spacing or full rope-bundle envelope is specified.

## Proposed semantic dependencies

| Namespace | Intended dependency |
|---|---|
| MachineRoomSection.Machine / TractionSheave | FixedToMachine / FixedToTractionSheave; exact center plus retained hardware offsets |
| MachineRoomSection.DeflectorSheave | FixedToDeflectorSheave; selected pitch circle and retained hub detail |
| MachineRoomSection.Rope | DerivedFromReevingGeometry; replace the reference approximation with exact tangent/drop/arc geometry |
| MachineRoomSection.Chassis / SteelBeam | FixedToChassis / FixedToSteelBeam; span between actual coordinated supports, selected upper beam nominal depth |
| MachineRoomSection.ConcreteBeam | FixedToConcreteBeam / SpanBetweenReferences; slab-to-support elevation |
| MachineRoomSection.Slab | FixedToSlab; front/rear shaft relationships |
| MachineRoomSection.RopeOpening | DerivedFromReevingGeometry; corresponding car/CW drop reference, retained width pending review |
| MachineRoomSection.Dimension / Annotation / Reference | AnnotationOfReference; native measured endpoints and true centers |

These dependencies are now installed as semantic XData and an embedded `MachineRoomSectionBindings` manifest. See `SECTION_BB_IMPLEMENTATION.md` for the released updater and verification.

## Confirmed adjustable-hanger dependency

The engineer confirmed that the hanger is adjustable. Required center separation is calculated first. The hanger positions the deflector below the steel beam. Concrete height supplies enough vertical space for the resulting arrangement, within 350–1000 mm. The deflector center is not fixed above the slab. An absent slab-clearance minimum remains NOT YET EVALUABLE; hanger mechanics remain MECHANICAL REVIEW REQUIRED.
Additional decisions: the lower 140 mm stand is a separate detail from the upper longitudinal machine beam; an approved UPN flange/web table is absent. Only selected nominal upper depth can safely be derived from the supplied selection rule. Exact manufacturer UPN contours cannot be invented. Actual span should come from coordinated bearing references, not the source's horizontal dimension chain.

For TZ below 520, the project selects no deflector. With a retained 520 mm traction circle and two strictly vertical opposite tangent drops, drop spacing must equal 520 mm. A smaller TZ cannot satisfy all three constraints. A direct-drop alternative needs a defined routing/machine arrangement; it must not receive a geometry PASS simply because the deflector was hidden.

## Implemented Core work

- `ReevingSolver.Calculate`: exact common external tangent on the reviewed B-B rope branch, tangent points/directions, center offsets/distance, entry/exit angles and actual wrap. Rejects intersecting/touching pitch circles. This is a specific external-tangent branch, not a general multi-sheave routing solver.
- `ReevingSolver.SolveVertical`: conditional adjustable-hanger geometry, fixed selected 250/400 diameter, bounded 350–1000 mm concrete height, no rounding increments or structural adequacy claim. Missing support confirmation returns a review status.
- `PreliminaryMachineBeamSelector : IMachineBeamSelector`: all supplied capacity/span thresholds; only UPN 140/160/200/240. Result includes nullable future structural calculation fields and PRELIMINARY/CALCULATION REQUIRED, never structural PASS.
- `ProjectInput.NumberOfSuspensionRopes` and `SuspensionProjectRules`: counts 4–7; rope diameters 6.5/8/10/11/12. Unknown diameter remains null, not silently assumed. Existing code-diameter warnings remain independent.

## Exact method

For traction center T, deflector center D, radii R and r, a common external tangent normal n satisfies:

`n · (D − T) = R − r`, with `|n| = 1`.

The tangent points are `T + R n` and `D + r n`. Both unit-normal solutions are calculated, then the branch consistent with the source's upward right-hand car entry and counterclockwise traction wrap is selected. Radius/tangent dot products must be zero. Wrap is obtained from the radial angle swept from the entry point to the traction tangent point, not from a dimension's text.

For the confirmed adjustable-hanger arrangement, horizontal separation H, vertical separation V and target wrap beta give theta = 180 degrees - beta:

`V = (H cos(theta) - (R - r)) / sin(theta)`.

Geometric attachment constraints can increase V, so wrap may exceed 152 degrees. With A equal to the measured stand/pad stack plus selected steel depth and machine-center offset, required concrete height is `r + suppliedMinimumClearance + V - A`. When the clearance is unspecified, zero is used only as a nonpenetration boundary; it is never treated as an approved engineering clearance. Concrete height is at least 350; a required height over 1000 rejects the update. Traction Z is `Hconcrete + A`; deflector Z is traction Z minus V. The hanger follows V and the measured attachment offsets.

The implementation and final native verification are reported in `SECTION_BB_IMPLEMENTATION.md`. Only 250/400 deflector diameters are available. D/d, wrap, slab clearance, structural and hanger statuses remain independent.
