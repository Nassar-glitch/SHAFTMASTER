# G1 round 5: the user's edit of the two plan details

Files: `user-edit.dwg` (saved 2026-10-17 11:33) and `program-output.dwg` (the `.bak`, program output of 2026-10-16 18:38), copied
from `output/solution-v01/NASSAR DEVELOPMENTS-GB-RCW-W1500D1800-5araaaaaaaaaaaaaa-MODEL00.*` (originals untouched); `project.elevator.json`.

**Diff by handle (VP5 area):** removed 4 program entities of the car detail (the two anchor rectangles, the bracket block
`SM_RAIL_BRACKET_PLAN_..._CAR_WALL_G85` and the rail block); added about 2500 entities on PDF layers (`PDF_0`, `PDF_SAS_STB_Side`,
`PDF_gide rail`): the library assembly pasted at library proportions into the car detail and the cross-arm detail. Dimensions and labels
left as they were (not correct, as the user said).

**Rules taken (scale from the rail foot 70 = 97 drawing units):**
1. Wall leg of the wall angle edge on against the support, the full bracket length along the wall; anchors D12x150 near the two ends
   (about 20 from each end), going into the wall through that leg.
2. The two horizontal legs seen from above: the wall angle's leg (about 40) and the rail angle's leg (about 50) overlapping; the 4 assembly
   bolts in crossed slots (one slot along x, one along the wall, about 30 x 12).
3. The rail angle's vertical leg edge on, parallel to the wall, with the plate on its wall side (the clip bolts' nuts sit on the plate).
4. The rail foot on the outer face of that leg; the 2 clips over the foot edges with their bolts.
5. Cross-arm case: the arm is a band along the wall leg (seen from above), the wall leg bolted to it (no wall, no anchors).

**Generated (parametric):** rail profile, gap from the plan (overlap = 2 legs + thickness - gap, legs grow when the overlap would fall
below the preset 30), bracket length 270 along the wall (the library crop is shorter: we keep the preset 270 as the Planner stated), fixing
(anchors, beam bolts, arm bolts). VP6 is the section perpendicular to the wall at two levels: the two L sections (wall angle leg down the
support, rail angle leg up at the rail), assembly bolt cut, plate, clip and its bolt, anchors / beam / arm end on, the rail in side view,
broken, with the real spacing; the gap in VP6 equals VP5.
