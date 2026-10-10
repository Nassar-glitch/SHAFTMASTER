# Automatic doors (D1): extracted dimensions and generalised rules

Status: D1 (Core + UI, no drawing changes). Source: the user's reference drawings `Automatic 2S800 Geared Rear CW.dwg` and
`Automatic 2C800 Geared Rear CW.dwg` (geared, rear counterweight, 1:1), read from their DXF copies. Product behaviour is governed by
`DESIGN-FLOW.md` ("Doors"); this file records the numbers and the rules built from them. Everything below is a preset in
`config/presets.json` (`Doors`), editable, with lock / follow / Reset.

## 1. Plan sketch (2S 800, reference drawing, mm)

X is measured from the shaft left wall, D from the shaft front wall (towards the rear). The governor is on the left, so the doors stack
on the right.

```
 shaft left wall X=0                                                       shaft right wall X=1720
 |  rail+bracket   car left X=185                          car right X=1535   rail+bracket |
 |   X 65..135     |                                                      |  X 1585..1655  |
 |                 |                    CAR  1350 x 1700                  |                |
 |  governor       |                                                      |                |
 |  X 79..105      |                                                      |                |
 D=245 ------------+--<fixed return 200>--+==== opening 800 ====+--<stack return 350>--+---   car interior front
 |                  car return 40 (D 205..245)                                         |
 D=205 ..........  car front-wall outer face
 D=200..115   car door panels: 2 x 40 + 5 gap, 5 clear of the return        |  CAR ZONE 130
 D=115 ------------------------------------------------------------------   outer car door face
 D=115..90   SILL GAP 25 (EN 81-20 5.3.6.2.2.1: <= 35)
 D= 90..5    landing door panels 2 x 40 + 5 gap                              |  LANDING ZONE 90
 D=  0 ------ shaft front wall

 landing frame:  X 355 |--jamb 30--| 385 ===== opening 800 ===== 1185 |-- stacking pocket 425 --| 1610     (1720 - 1610 = 110 to the wall)
 opening centre X=785 = shaft centre 860 - 75 (offset towards the fixed side)
 panels: A X 385..792.5, B X 782.5..1190 (width 407.5, overlap 10, 5 beyond the opening on the stacking side)
```

## 2. Extracted dimensions

### 2S 800 (shaft 1720 x 2800, car 1350 x 1700, DBG 1450, TZ 990)

| Item | Value | Source in the drawing |
|---|---|---|
| Front zone | 245 = 130 + 25 + 90 | dimensions 245, 130, 25, 90 |
| Car door zone (car interior front to outer car door face) | 130 | car return 40 + 5 + panel 40 + 5 + panel 40 |
| Sill gap | 25 | outer car panel D 115 to landing panel D 90 |
| Landing door zone | 90 | landing panels 5 + 40 + 5 + 40 to the front wall |
| Car return, fixed side (left) | 200 | dimension 200 (car left X 185 to opening X 385) |
| Car return, stacking side (right) | 350 = 800 / 2 - 50 | dimension 350 |
| Opening | 800 clear, height 2000 | "DOOR 800X2000" |
| Opening offset | 75 towards the fixed side | opening centre X 785, shaft centre X 860 |
| Wall to opening | 385 left, 535 right | dimensions 385, 535 |
| Landing jamb, fixed side | 30 (frame X 355 to opening X 385) | frame outline |
| Landing stacking pocket | 425 = 800 / 2 + 25 (opening edge X 1185 to frame X 1610) | frame outline |
| Panels | 2, width 407.5, thickness 40, overlap 10, 5 beyond the opening edge | panel outlines |
| Car rail + bracket | X 65..135, 65 wide along the depth (guide axis D 1160) | imported rail outline |
| Governor | X 79..105 (26 wide), starts 159 behind the guide axis, 240 long, left of the car | governor outline |
| Car depth | 1700; guide axis 785 behind the car front, 915 in front of the rear (dimensions) | |

### 2C 800 (same shaft and car)

| Item | Value | Source in the drawing |
|---|---|---|
| Front zone | 220 = 110 + 30 + 80 (the 2C drawing gives only 220; the split is read from the door boxes) | dimension 220; door boxes |
| Car door zone | 110 = car return 40 + car door box 70 (D 110..180) | polyline X 35..1686, D 110..180 |
| Sill gap | 30 (box edge D 110 to landing box edge D 80) | polylines |
| Landing door zone | 80 = 10 to the wall + landing box 70 (D 10..80) | polyline X 35..1686, D 10..80 |
| Car returns | 275 each side (car X 185 to opening X 460) | dimensions 275 |
| Opening | 800, centred (460 to each wall) | dimensions 460, 860 |
| Landing pocket per side | 425 = 800 / 2 + 25 (opening edge X 460 to frame X 35) | frame outline |
| Landing frame to the wall | 35 each side | polyline X 35.2..1685.9 |
| Panels | 2, one per side, width 399, thickness 39, gap 2 | panel outlines |

Both drawings: the 1350 car is exactly the minimum for 800 (2S: 200 + 800 + 350; 2C: 800 + 2 x 275). The 2C shaft 1720 is exactly the
minimum for 800 on the landing (800 + 2 x (425 + 35)).

## 3. Generalised rules (700 / 800 / 900 / 1000 / 1200)

Let `o` be the clear opening.

| Rule | 2S (side opening) | 2C (centre opening) |
|---|---|---|
| Front zone | 130 + 25 + 90 = 245 | 110 + 30 + 80 = 220 |
| Car return, fixed side | 200 | o / 4 + 75 |
| Car return, stacking side | o / 2 - 50 | o / 4 + 75 |
| Opening offset (toward the fixed side) | (stack return - fixed return) / 2 = o / 4 - 125 | 0 |
| Landing jamb, fixed side | 30 | o / 2 + 25 (pocket) |
| Landing stacking pocket | o / 2 + 25 | o / 2 + 25 |
| Landing frame to the side wall | 35 | 35 |
| Panel width | (o + 5 + 10) / 2 | (o - 2) / 2 |
| Minimum car width | 200 + o + (o / 2 - 50) = 1.5 o + 150 | o + 2 (o / 4 + 75) = 1.5 o + 150 |
| Minimum shaft width (landing) | o + 2 max(o/2 + 25 + 35 - offset, 30 + 35 + offset) | o + 2 (o / 2 + 25 + 35) = 2 o + 120 |

Per size (mm):

| o | 2S stack return | 2S offset | 2S pocket | min car | 2S min shaft (landing) | 2C return | 2C pocket | 2C min shaft (landing) |
|---|---|---|---|---|---|---|---|---|
| 700 | 300 | 50 | 375 | 1200 | 1420 | 250 | 375 | 1520 |
| 800 | 350 | 75 | 425 | 1350 | 1570 | 275 | 425 | 1720 |
| 900 | 400 | 100 | 475 | 1500 | 1720 | 300 | 475 | 1920 |
| 1000 | 450 | 125 | 525 | 1650 | 1870 | 325 | 525 | 2120 |
| 1200 | 550 | 175 | 625 | 1950 | 2170 | 375 | 625 | 2520 |

The car width in a project comes from the existing plan recipe (shaft width minus the rail allowances), so the shaft that fits a size is
the larger of "minimum car + 400" and the landing column.

Selection and checks (all in `Elevator.Core/Doors/DoorRules`):

- **Blank size:** the largest standard size that passes every check; when none passes, the smallest, with its problems.
- **User size:** validated. Reasons given: not a standard size; needs car width X mm (shaft width Y mm); landing frame or parked panels do
  not fit the shaft width; sill gap above 35 mm (EN 81-20 5.3.6.2.2.1); distance from the front wall to the car door above 150 mm
  (EN 81-20 5.2.5.2.1; landing zone + sill gap); clash.
- **Clash** (plan rectangles): the landing frame and the car door with its parked panels, against the car guide rails with brackets,
  the counterweight rails and frame, and the speed governor. Envelope sizes are the measured ones above (`Doors.Envelopes`).
- **Handing (2S):** stacking side opposite the governor (governor on the left in the catalog, so right); Left or Right can be locked.
  The mirror image about the shaft centre is exact.
- **Returns are minimums.** In a wider car the opening keeps its offset and the extra width is shared equally by both returns.
- **The front zone sets the car depth:** `CarDepth = SD - frontZone - rear allowances` (Manual: the existing 125 mm front allowance,
  unchanged).

## 4. Findings and open decisions for the Planner

1. **The equipment profile gives a 1320 mm car in the 1720 shaft** (150 mm rail allowance per side, 100 mm car deduction), while the
   reference drawings have 135 mm and a 1350 mm car. On the reference shaft 1720 x 2800 the rules therefore select 700 (both types);
   800 needs a car of 1350, that is a shaft of at least 1750. The 800 reference numbers are reproduced exactly when the car is
   1350 (tests `Reference_2S_800_reproduces_the_drawing`, `Reference_2C_800_reproduces_the_drawing`). Changing the 150 allowance would
   change the Manual recipe, which D1 does not touch.
2. **Car depth in the references is 1700 on SD 2800**, the recipe gives 2400 - 245 + 125 = 2280. The recipe is unchanged; the references
   are examples.
3. **2C car return generalisation** (o / 4 + 75, giving 275 at 800) is a proposal: one drawing gives one point. It keeps the minimum car
   width equal to 2S. The alternative is a constant 275. It is a preset row per size and can be edited.
4. **2C front-zone split (110 / 30 / 80)** is read from the door box outlines, not from dimensions; the 2C sill gap of 30 is within 35.
5. **Landing frame to the wall (35)** is measured on the 2C drawing; the 2S drawing has more room (110 on the stacking side).
6. **Rail, bracket and governor envelopes** are measured from the imported outlines (rail 70 x 65, governor 26 x 240). There is no rail
   profile library yet (DESIGN-FLOW stage 3); they are presets.
7. **Drawings:** the plan and sections still draw the Manual door. `DoorLayout.CentreShiftMm` (opening centre shift from the shaft centre) and the layout are available for the
   drawing stage (D2).
