# D3: what the user corrected by hand, as rules

Source: `user-edit-2S-700-1320.dwg` (the user's own file, kept untouched) against the same project regenerated with `main` (D2). The project
is embedded in the DWG (`user-edit-2S-700-1320.elevator.json`): 2S, opening 700, shaft 1320 x 1800, capacity 320.
Method: both DWGs through the AutoCAD Core Console `DXFOUT`, then ezdxf; every entity of the plan compared by tag, then by geometry for the
entities the user added (they carry no tag). Result: the user changed **no entity of the manual door, the walls, the rails, the car or any
other view**; he moved or added 4 groups of door-area entities and re-arranged 9 dimensions. Nothing was deleted.

Symbols (plan-local, x from the shaft left wall inner face, depth d from the front wall inner face): `ul..ur` = outer edge of the fixed-side
landing jamb to the end of the stacking pocket (LandingLeftX..LandingRightX, mirrored for the other handing); `uf` = opening edge on the fixed
side; `lz` = landing door zone; `cd = lz + sill gap` = start of the car door zone; `cdEnd = car front - car return (40)`.

## A. Drawing of the door (2S)

| # | What the user did | Rule |
|---|---|---|
| A1 | Added a rectangle around the landing door | **Landing door frame** = one rectangle from `ul` to `ur`, from d = 0 to `lz`. It replaces the three loose pocket lines (top, bottom, outer edge) and contains both landing panels. |
| A2 | Added a rectangle around the car door panels | **Car door frame** = one rectangle from `ul` to `ur` (same x extent as the landing frame, so the two frames are aligned), from d = `cd` to `cdEnd` (the whole car door zone except the car front return). It contains both car panels, which no longer float. |
| A3 | Added a closed rectangle at the fixed side in the car zone | **Car jamb post** = rectangle `ul..uf` x `cd..cdEnd` (as wide as the landing fixed jamb, as deep as the car frame). |
| A4 | Copied the 3 mm inset line of the landing jamb to the car post and extended its right edge to the opening edge (both posts) | **Jamb post** (landing and car) = outline + an inset line 3 mm inside the outline on the three sides away from the opening, closed at the opening edge (`uf`). |
| A5 | Stretched the four "parked panel" blocks (106 wide) back to the closed panel and removed their closing edge | **Panel band**: in each of the four panel rows the panel continues from its closed end (fixed-side row: `uf + panel width`; leading row: `us + over-travel`) into the pocket up to 4 mm before `ur`. Outline (open at the closed end, 1 mm stubs) and a 1 mm inset line open at the same end, on layer `Car`: the panel and its band read as one bar in the frame. |

Unchanged by the user: panels (closed position), car front returns, sills, wall jamb blocks, centre lines.

## B. Dimensions

| # | What the user did | Rule |
|---|---|---|
| B1 | Moved the two car-return dimensions (20, 200) from their own row to the row of OPENING 700 | **Chain on one line**: return, opening, return are one chain on the template opening row (the user's rows differ by 1.25 mm, a hand drag; generated rows are exactly equal). |
| B2 | Moved landing zone 90, sill gap 25 and car zone 130 into one column | **Front-zone chain**: landing zone, sill gap and car zone on one dimension line, left of the shaft. |
| B3 | Moved the zone total 245 to the column of CAR DEPTH | **Total chain**: total and CAR DEPTH on one dimension line, one column pitch (110 mm; the user's was 98 mm) further out than the zone chain. |
| B4 | Moved the extension lines of 25, 130 (from the wall) and CAR DEPTH (from x = 250, a point in the opening) onto the frame corners and the car front return | **Extension lines start at real edges**: y = 0 at the wall inner face, y = `lz` and y = `cd` at the left outer corner of the frames, y = car front on the car front return (middle of the left return). |
| B5 | Moved the offset extension (90) to the sill line | The opening-centre extension line starts on the landing sill line (d = 0). |
| B6 | Did not touch the text of 25 (it overlaps the 90 and 130 and the ticks: "9025130" in his file) | **Text fit** (principle 8): a text longer than its interval is put outside the chain on the line (first / last interval, beyond a car wall that blocks it) or, for an inner interval, beside the chain on a second lane one text height further out. Estimated text length = 0.7 x height per character. |
| B7 | Left the rows below the wall at -116.31 / -232.61 / -341.80 (template spacing 109.19, offset row 116.31) | **Equal rows**: the offset row is one template pitch nearer than the halves row (-123.42); wall chain, halves and offset rows are equally spaced. This is the only visible difference (7 mm) from the user's file. |

Everything is expressed relative to the opening, returns, zone splits and shaft width, so it holds for every size, both handings and both
door types (checked on 2S 700 / 800 right and left, 2C 800 / 900).

## C. 2C (the same frame logic)

* Landing frame and car frame = one rectangle each across both stacking sides, centred on the opening, from d = 0 to `lz` and from `cd` to
  `cdEnd` (before: boxes 70 deep that did not reach the wall).
* Panels (two centre panels each) sit inside their frames; the panel band runs from each closed panel to the end of the frame.
* There is no jamb post: both sides stack.

## D. Manual door

The user's edit touched no manual-door entity, so nothing changes for Manual: golden approved files and the four-view snapshots are equal.

## E. Differences kept on purpose

1. Offset row 7 mm nearer, column pitch 110 instead of 98, rows exactly equal (B1, B3, B7).
2. The three loose pocket lines of the user's file (they lie on the new frame edges) are not drawn twice.
3. The inset line of the landing jamb: the user's file closes it at the opening edge as well; both posts follow the same rule (A4).
4. Interpretation to confirm: the user moved nothing for 2C; the band (A5) and frame (A1, A2) rules were applied to 2C for consistency.
