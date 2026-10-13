# Guide rails: engineering basis (stage R, Part A)

Status: **Part A, engineering basis. No code.** Written 2026-10-13 from the sources listed in section 1, for a cloud session that has no
access to the standards. Everything a cloud implementation needs is in this file: equations, symbols, table values, section values and the
source clause of each. Where the sources do not settle a point it is listed in section 12 as a **question for the user**; those points must
not be implemented until they are answered. DESIGN-FLOW.md "Guide rails (agreed 2026-10-13)" is binding for the inputs, the order, the
recommendation and the presentation; this file does not repeat it.

The engineering equations are adopted source rules (EN 81-50:2014 5.10 and Annex C, EN 81-20:2014 5.7). No standards compliance,
manufacturer approval or certification is implied.

## 1. Sources

| Id | Source | Where | Used for |
|---|---|---|---|
| S1 | **EN 81-50:2014**, 5.10 "Guide rails calculation" and informative Annex C "Example for calculation of guide rails" | local copy `D:\CAD DATA---------------------\AHM Folder\CODES\bs_en_81_t50_e_d01_14.pdf` (BS EN 81-50:2014, PDF pages 42-46 and 85-91). Not in git (copyright) | equations, omega polynomials, load-case equations |
| S2 | **EN 81-20:2014**, 5.7 "Guide rails" (5.7.1 to 5.7.4) | `ElevatorAutomation/Standards/EN 81-20-2014.pdf` (not in git), PDF pages 97-102 | load cases, forces, impact factors (Table 14), permissible stresses (Table 15), deflection limits |
| S3 | User's calculation note `Elevator_Calculations_GEARBOX_Code_Corrected_Dual_Units.docx` | repository root | FF1 / FF2 / P5 / P6 / P7 only. **The note has no rail check** (no omega, no bending stress, no deflection): paragraphs "Third" and "Fifth" only |
| S4 | `docs/calculations/CAR-GUIDE-FORCES.md` | repository | the user-approved FF1 / FF2 interpretation (axes, per rail, h) |
| S5 | **Savera "Guide super" guide-rail catalogue** (T-type, ISO 7465 designations), pages 3, 5 and 6 | `D:\CAD DATA---------------------\AHM Folder\dwg samples\gb machine\Technical Submital\Technical Submital\8-Guide Rails\Savera-Guide-super.pdf` (a scanned supplier brochure) | section values, dimensions, mass per metre, mechanical properties |
| S6 | Symposium paper "Lift system calculations in EN 81-50", 4th Symposium on Lift & Escalator Technologies, pp. 148-152 (section 3 "Guide rail calculations") | `D:\CAD DATA---------------------\AHM Folder\CODES\Lift system Calculations.pdf` | **worked numerical example** of the S1 method (T127-1/B), used as hand-check values (section 11) |

ISO 7465:2007 itself is **not** on disk; S5 states ISO 7465/2001 and lists the same designations. The section values in section 8 are
the supplier's; the user should confirm them against ISO 7465 (question Q3).

A third document in the user's folders, `LIFTPRO_Guide_Rail_Selection_Report_EN81-20 (1).pdf` (a third-party report), was read and **not
used**: it models the rail as a simply supported beam (M = F·l/4) instead of EN 81-50's continuous beam (M = 3·F·l/16), mislabels Ix as
the "weak axis", and its section values for T70/65/9 and T89/62/16 do not agree with S5. It must not be a source of equations or values.

## 2. What is checked, and for which load cases

EN 81-50 5.10.1: bending stress, combined bending, buckling stress, compression / tension stress, combined bending and compression /
tension, combined buckling and bending, flange bending stress; and deflections.

EN 81-20 5.7.2.2 and Table 13 give the load cases, and which loads and forces act in each:

| Load case | P | Q | Mcwt / Mbwt | Fs | Fp | Mg | Maux | WL |
|---|---|---|---|---|---|---|---|---|
| Normal operation, **running** | x | x | x |  | x (a) | x | x | x |
| Normal operation, **loading + unloading** | x |  |  | x | x (a) | x | x | x |
| **Safety device operation** | x | x | x |  | x (a) | x | x |  |

(a) see 5.7.2.3.5 (Fp may be ignored for travel up to 40 m). "Load and forces may not act simultaneously" (S2 Table 13 note). WL (wind) is
for lifts outside a building with partially enclosed well only: not applicable to the products, out of scope. (Table 13 read from the
rendered page; the text extraction of the PDF is ambiguous.)

Car rails: all three load cases. **CW rails (no safety gear): normal operation only** (DESIGN-FLOW). S2 5.7.4.6 b) gives the CW rails
without safety gear δperm = 10 mm.

## 3. Symbols (S1 5.10, C.1.2, C.1.3)

| Symbol | Meaning | Unit |
|---|---|---|
| Dx, Dy | car dimension in X direction (car depth) and in Y direction (car width) | mm |
| h | distance between the car guide shoes (upper to lower) | mm |
| l | maximum distance between guide brackets | mm |
| lk | buckling length, **lk = l** (S1 5.10.3: "lk = l") | mm |
| n | number of guide rails (2) | - |
| P | mass of the empty car and what it carries (part of the travelling cable, compensation ropes / chains) | kg |
| Q | rated load | kg |
| S | car suspension (point); (xS, yS) its position | - |
| xC, yC | car centre in the guide-rail cross coordinates | mm |
| xP, yP | position of the car mass P | mm |
| xQ, yQ | position of the rated load | mm |
| xi, yi | position of the car door i (sill force) | mm |
| gn | standard acceleration of free fall, 9.81 | m/s² |
| k1, k2, k3 | impact factors (safety device; running; auxiliary parts / other) | - |
| Fx, Fy | horizontal guiding force of one guide rail in the X and Y axis | N |
| Fv | vertical force on one guide rail | N |
| Fs | vertical force on the car sill when loading / unloading | N |
| Fp | push-through force of all brackets at one guide rail | N |
| Fr | push-through force of all clips per bracket | N |
| nb | number of brackets of one rail | - |
| Mg | mass of one line of guide rails | kg |
| Maux | force in a guide rail due to auxiliary equipment | N |
| A | cross-sectional area of one rail | mm² |
| Wx, Wy | section moduli (min. values) | mm³ |
| Ix, Iy | second moments of area | mm⁴ |
| ix, iy; i | radii of gyration; **i = minimum radius of gyration** | mm |
| E | modulus of elasticity | N/mm² |
| Rm | tensile strength | N/mm² |
| St | safety factor (Table 15) | - |
| λ | slenderness, λ = lk / i | - |
| ω | omega value | - |
| σx, σy, σm, σk, σ, σF, σperm | stresses (see below) | N/mm² |
| δx, δy, δperm, δstr-x, δstr-y | deflections of the rail, permissible, of the building structure | mm |
| b, c, f, h1, l (flange bending) | see 6.4; b = half the width of the guide-shoe lining, c = width of the connection of foot and blade, f = foot depth at the connection, h1 = rail height, l = length of the guide-shoe lining | mm |

**Axes** (S1 Figure 4, and S4 "Axis mapping", user-approved 2026-10-07):

- Rail x-axis: parallel to the rail's back (flange); rail y-axis: along the blade (the symmetry axis).
- **Fx** acts on the blade side faces (perpendicular to the blade). It bends the rail about its **y-axis**: σy = My / **Wy** (the weaker axis).
- **Fy** acts on the blade tip (along the blade). It bends the rail about its **x-axis**: σx = Mx / **Wx**.
- Car axes (S4): X = car depth, perpendicular to the rail line; Y = car width, along the line joining the two car rails.
- **FF2 = Fx** (perpendicular to the rail line); **FF1 = Fy** (along the rail line). Per guide rail, at one shoe level, no division by the
  number of rails or shoes (S4 "Meaning").
- Table value naming of S5: Ixx, Wxx, ixx are the x-x values above; Iyy, Wyy, iyy the y-y values. For all four profiles iyy < ixx, so
  **i = iyy** and the weak-axis modulus is Wyy.

## 4. Forces

### 4.1 Impact factors and the safety factor (S2 Table 14, 5.7.4)

| Impact at | Factor | Value |
|---|---|---|
| Operation of instantaneous safety gear, not of the captive roller type | k1 | 5 |
| Instantaneous safety gear of the captive roller type, or pawl device with energy accumulation buffer, or energy accumulation buffer | k1 | 3 |
| **Progressive safety gear**, or pawl device with energy dissipation buffer, or energy dissipation buffer | k1 | **2** |
| Rupture valve | k1 | 2 |
| Running | k2 | **1,2** |
| Auxiliary parts fixed to the guide rail and other operational scenarios | k3 | "(....)": **to be determined by the manufacturer** (S2 footnote a) |

k1 = 0 where no safety device acts on the guide rail (S2 5.7.2.3.5), i.e. the CW rails.

### 4.2 Vertical force Fv (S2 5.7.2.3.5)

- Car, safety device operation: **Fv = (k1·gn·(P + Q)) / n + Mg·gn + Fp**
- Counterweight / balancing weight: Fv = (k1·gn·(Mcwt)) / n + Mg·gn + Fp (k1 = 0 without safety gear → Fv = Mg·gn + Fp)
- Running and loading (S1 C.2.2.2, C.2.3.2): **Fv = Mg·gn + Fp** (Table 13 marks Fp in all three load cases, with the 40 m allowance)
- Fp = n_b · Fr (rails supported at the pit or hanging at the top), Fp = (1/3)·n_b·Fr (freely hanging rails). For travel not exceeding
  40 m Fp may be ignored (S2 5.7.2.3.5 note).
- The user's note P5 = (Capacity + Car + Compensation) × 10 N/kg "per guide rail" is k1·g·(P+Q)/n with k1/n = 1 (progressive, two rails) and
  10 N/kg for gn. It excludes Mg·gn and Fp. (S3 "Fifth".)

### 4.3 Horizontal guiding forces on the car rails (S1 Annex C, n guide rails)

With the car dimensions Dx, Dy, the load Q at the **3/4-area eccentric position** (S2 5.7.2.3.4) and the car mass P and suspension S at
the rail cross (xP = yP = xS = yS = 0, S4 "Basis"):

| Load case | Fx | Fy |
|---|---|---|
| Safety device operation (C.2.1.1) | k1·gn·(Q·xQ + P·xP) / (n·h) | k1·gn·(Q·yQ + P·yP) / ((n/2)·h) |
| Normal operation, running (C.2.2.1) | k2·gn·[Q·(xQ − xS) + P·(xP − xS)] / (n·h) | k2·gn·[Q·(yQ − yS) + P·(yP − yS)] / ((n/2)·h) |
| Normal operation, loading (C.2.3.1) | [gn·P·(xP − xS) + Fs·(xi − xS)] / (n·h) | [gn·P·(yP − yS) + Fs·(yi − yS)] / ((n/2)·h) |

- Load distribution cases (S1 Figures C.2, C.3): **case 1** relative to the x-axis: xQ = xC + Dx/8, yQ = yC; **case 2** relative to the
  y-axis: xQ = xC, yQ = yC + Dy/8. Each case gives one of the two forces; they are not added (the paper S6 combines Fx(1) with
  Fy(1) = 0 and Fy(2) with Fx(2) = 0 and takes the worse σm).
- Sill force (S2 5.7.2.3.6): Fs = 0,40·gn·Q passenger lifts (0,60 goods passenger; 0,85 goods with heavy handling devices); the car is
  empty (Q = 0) when Fs is applied; at the most unfavourable entrance only; **when the car is at the landing and the guide shoes (top and
  bottom of the car) are positioned within 10 % of the distance between the brackets, bending due to sill forces may be ignored**.
- **The user-approved convention for the safety device case (S4)**: n = 2, P at the rail cross, eccentricity **Dim/4** (not Dim/8),
  **10 N/kg** instead of gn, k1 = 2 → FF1 = Q·10·Dy / (4·h) × 2 and FF2 = Q·10·Dx / (8·h) × 2 (the note's equations); these are
  the values the application already calculates (`CarGuideForces`, with h from `GuideShoeSpacingMm`). The rail check in the safety
  device load case takes **Fy = FF1 design value, Fx = FF2 design value**. The application's FF values are the design values (already
  multiplied by k1 = 2).
- The note and the application do **not** cover running (k2 = 1,2) and loading (Fs): question Q4.

### 4.4 Counterweight rails

S2 5.7.2.3.3: the guiding forces of a counterweight are evaluated from the acting point of the mass, the suspension and the forces of
compensating ropes / chains; **a centrally guided and suspended counterweight is taken with an eccentricity of the acting point of at
least 5 % of the width and 10 % of the depth**. S1 Annex C gives load-case equations for the car only. The CW guiding-force equations are
therefore **not given by the sources**: question Q6.

## 5. Bending (S1 5.10.2)

Model: the rail is a continuous beam with flexible fixing points at distance l; the resultant force acts in the middle between brackets;
the moments act on the neutral axis.

- **Mm = 3·Fh·l / 16** (Fh the horizontal force, l in mm; Mm in N·mm)
- **σm = Mm / W** per axis: **σy = (3·Fx·l / 16) / Wy**, **σx = (3·Fy·l / 16) / Wx** (S1 C.2.1.1)
- More than two rails of identical profile: equal distribution of the forces (5.10.2.3). Several safety gears on different rails: the
  braking force is equally distributed (5.10.2.4). (The products have two car rails, one safety gear per rail: Fv above is already per rail.)
- If Wx / Wy are the table minima and σperm is not exceeded, no further proof is needed (5.10.2.2). The application uses the table moduli.

## 6. Stresses and conditions

### 6.1 Buckling (S1 5.10.3)

- **σk = (Fv + k3·Maux)·ω / A**
- λ = lk / i with lk = l, i = minimum radius of gyration (= iyy for the four profiles)
- ω for **Rm = 370 N/mm²** (all four profiles use 370, DESIGN-FLOW):

| Range of λ | ω |
|---|---|
| 20 ≤ λ ≤ 60 | 0,00012920·λ^1,89 + 1 |
| 60 < λ ≤ 85 | 0,00004627·λ^2,14 + 1 |
| 85 < λ ≤ 115 | 0,00001711·λ^2,35 + 1,04 |
| 115 < λ ≤ 250 | 0,00016887·λ^2,00 |

- For **Rm = 520 N/mm²** (for completeness; not needed with Rm 370):

| Range of λ | ω |
|---|---|
| 20 ≤ λ ≤ 50 | 0,00008240·λ^2,06 + 1,021 |
| 50 < λ ≤ 70 | 0,00001895·λ^2,41 + 1,05 |
| 70 < λ ≤ 89 | 0,00002447·λ^2,36 + 1,03 |
| 89 < λ ≤ 250 | 0,00025330·λ^2,00 |

- Between 370 and 520: ωR = [(ω520 − ω370)/(520 − 370)]·(Rm − 370) + ω370.
- The polynomials are defined for **20 ≤ λ ≤ 250 only**: below 20 and above 250 the standard gives no value (question Q10).
- Checked against S6: λ = 169,4 → ω = 4,85; λ = 106 → ω = 2,02.

### 6.2 Combined stresses (S1 5.10.4; C.2.x.3)

- Bending: **σ = σm = σx + σy ≤ σperm**
- Bending and compression / tension: **σ = σm + (Fv + k3·Maux) / A ≤ σperm**
- Bending and buckling: **σ = σk + 0,9·σm ≤ σperm**
- Footnote of S1 C.2.1.3: if σperm < σm, the figures of 5.10.2.2 (stresses at the outer edge where tension is maximal) may be used.
  Not implemented: it only relaxes a failing result; state it in the Design Notes only if asked (question Q13).

### 6.3 Permissible stresses (S2 5.7.4.5, Table 15)

**σperm = Rm / St.** Safety factor St (Table 15):

| Load case | Elongation A5 | St |
|---|---|---|
| Normal operation and loading / unloading | A5 > 12 % | **2,25** |
|  | 8 % ≤ A5 ≤ 12 % | 3,75 |
| Safety device operation | A5 > 12 % | **1,8** |
|  | 8 % ≤ A5 ≤ 12 % | 3,0 |

Materials with A5 < 8 % are too brittle and shall not be used. "The strength values shall be taken from the manufacturer."

For Rm = 370 and A5 > 12 %: σperm = 370 / 2,25 = **164,44 N/mm²** (normal operation, loading) and 370 / 1,8 = **205,56 N/mm²** (safety device).
For Rm = 370 and 8 ≤ A5 ≤ 12: 370 / 3,75 = 98,67 and 370 / 3,0 = 123,33 N/mm². Which A5 applies to which profile: question Q1.

### 6.4 Flange bending (S1 5.10.5, C.2.1.4)

Dimensions: S1 Figure 5 (b, c, f, h1, l).

- **Roller guide shoes:** σF = 1,85·Fx / c² ≤ σperm
- **Sliding guide shoes:** σF = 6·Fx·(h1 − b − f) / ( c²·(l + 2·(h1 − f)) ) ≤ σperm
- Fx is the force exerted by a guide shoe on the flange (the horizontal force Fx of the load case). The inputs b and l are guide-shoe data
  (question Q7); c, f, h1 are rail section dimensions (section 8).

### 6.5 Deflections (S1 5.10.6, C.2.1.5)

- **δx = 0,7·Fx·l³ / (48·E·Iy) + δstr-x ≤ δperm**
- **δy = 0,7·Fy·l³ / (48·E·Ix) + δstr-y ≤ δperm**
- Fx uses Iy and Fy uses Ix (the force bends the rail about the other axis). E: question Q2 (210 000 N/mm² in S6).
- δperm (S2 5.7.4.6, T-profile rails): **5 mm** in both directions for car, counterweight or balancing-weight rails on which safety gears
  operate; **10 mm** for counterweight / balancing-weight rails without safety gears.
- δstr (deflection of the building structure) and the deflection of brackets / play in the shoes (S2 5.7.2.1.2): question Q9.

## 7. Calculation sequence (per rail line, per load case)

1. Inputs: profile (section values), l, Rm, A5 → St, E; car: Q, P, Dx, Dy, h, k1 (k2, Fs); rail length → Mg.
2. Fx, Fy of the load case (section 4.3); Fv (section 4.2); Maux and k3 (section 12, Q8).
3. σy = 3·Fx·l / (16·Wy); σx = 3·Fy·l / (16·Wx); σm = σx + σy.
4. λ = l / iy; ω (6.1); σk = (Fv + k3·Maux)·ω / A.
5. σ(bending) = σm; σ(bending + compression) = σm + (Fv + k3·Maux)/A; σ(bending + buckling) = σk + 0,9·σm; each against σperm of the load case.
6. σF (6.4) against σperm.
7. δx, δy (6.5) against δperm.
8. Load cases: running (σperm normal), loading (σperm normal), safety device (σperm safety device). Overall = every item OK.

Every item reports the value, the permissible value and OK / NOT OK; N and kg for forces (principle 10, `LoadUnits.KgText`).

## 8. Rail section values (catalog data, editable)

Source S5 (Savera, ISO 7465 designations; pages 3, 5, 6). The project names map to the catalogue's cold-drawn "/A" designations
(the machined "/B" rows have identical section values for T89 and T90). Units converted: 1 cm² = 100 mm², 1 cm³ = 1000 mm³, 1 cm⁴ = 10 000 mm⁴.

| Project name | S5 designation | b1 | h1 | blade k | n | c | g | f | A mm² | m kg/m | e mm | Ixx mm⁴ | Wxx mm³ | ixx mm | Iyy mm⁴ | Wyy mm³ | iyy mm |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| T50/50/5 | T50/A | 50 | 50 | 5 (k2) | - | n/a | n/a | n/a | 475 | 3,73 | 14,3 | 112 400 | 3 150 | 15,4 | 52 500 | 2 100 | 10,5 |
| T70/65/9 | T70-1/A | 70 | 65 | 9 | 34 | 6 | 6 | 8 | 951 | 7,47 | 20,4 | 413 000 | 9 240 | 20,9 | 186 500 | 5 350 | 14,0 |
| T89/62/16 | T89/A (= T89/B) | 89 | 62 | 16 | 34 | 10 | 8 | 11 | 1 570 | 12,3 | 20,2 | 595 200 | 14 250 | 19,5 | 524 000 | 11 800 | 18,3 |
| T90/75/16 | T90/A (= T90/B) | 90 | 75 | 16 | 42 | 10 | 8 | 10 | 1 730 | 13,55 | 26,1 | 1 020 000 | 20 870 | 24,3 | 530 000 | 11 800 | 17,5 |

- b1 = rail width (flange), h1 = rail height (back to blade tip), n = blade (head) height, c, g, f as in the S5 figure (c = neck width,
  g = flange-edge thickness, f = foot depth at the blade connection: the same c and f as S1 Figure 5). All mm.
- Mass: S5 "Pesos" table, kg per 5 m bar ÷ 5: T50/A 18,65 → 3,73; T70-1/A 37,35 → 7,47; T89/A 61,5 → 12,3; T90/A 67,75 → 13,55. These agree
  with the "Peso" column of the technical table.
- **T50/50/5 has no c, f, g in S5** (flat-bar type: flange p = 5 mm, blade k2 = 5 mm): flange bending needs an interpretation (question Q3).
- Mechanical data in S5 for cold-drawn blades: tensile strength 470-840 (blade > 5 ≤ 8 mm, > 8 ≤ 10 mm) and 420-710 N/mm² (> 10 ≤ 16 mm),
  yield 355 / 355 / 300 N/mm², A5 min **8 % / 12 % / 12 %**. DESIGN-FLOW fixes **Rm = 370 N/mm²** for every profile as a catalog value
  (the user's decision; S6 uses 370 as well): the catalog entry holds Rm and A5, editable (question Q1).
- E = 210 000 N/mm² (S6; question Q2).

## 9. Recommendation logic (DESIGN-FLOW "Guide rails"; the algorithm is a proposal, see Q11)

Inputs in this order, each recommended from the earlier ones; a chosen value that fails is kept (values and OK / NOT OK only on the
sheet; Design Notes one line with the reason and the needed profile or spacing).

1. Machine supported on the guide rails: Yes / No (preset No).
2. Car rail profile: the smallest profile (order by mass: T50/50/5 < T70/65/9 < T89/62/16 < T90/75/16) that passes all items of all load
   cases at the **recommended spacing**.
3. Car rail bracket spacing: the largest l (a whole number of mm, rounded **down to 50 mm**, at most the **2000 mm** preset) for which
   the chosen profile passes. The pass test is monotonic in l: stresses and deflections grow with l (σ ∝ l; δ ∝ l³; ω grows with λ = l/i), so the
   largest passing l is found by stepping down from 2000 in 50 mm.
4. CW rail profile and CW bracket spacing the same way, for normal operation (no safety gear).

Proposed resolution of the circularity of 2 and 3 (to be confirmed): the profile is the smallest one that passes at l = 2000 mm
(the preset); if none passes at 2000, the **largest** profile is recommended and the spacing steps down from 2000 until it passes (down to
the floor of question Q10; below that: NOT OK with the required spacing stated). The spacing recommendation then follows the chosen
(possibly user-changed) profile. Monotonicity in the profile: a larger profile never needs a smaller spacing (the order by mass is not strictly an order
by Wy / Iy for T89 vs T90 — Wy is equal; Ix, Iy, A differ — so the profile order must be checked with a property test, not assumed).

Existing Q-based rail ladder: unchanged until the check exists (DESIGN-FLOW); projects that locked a rail keep it.

## 10. What the sheet and the Design Notes show

(Presentation rules are DESIGN-FLOW's.) Sheet section "Guide rails": inputs (machine on rails, profile, spacing, l, h, Rm, A5/St, E,
k1, k2, Fs), the equations with values, results: Fx, Fy (N and kg), Fv, σx, σy, σm, λ, ω, σk, the three combined stresses, σF, δx, δy, each
with its permissible value and OK / NOT OK, per load case and per rail line (car, CW). No other text. Design Notes: one line per failed item
with the reason and the needed profile or spacing (found by the same search); nothing else.

## 11. Hand-check values (for xUnit)

All from S6 (T127-1/B, conventional guiding), reproduced here as numbers. S6 rounds; use a tolerance of 1 N/mm² or 0,1 mm.

**Example 1** (S6 section 3.1): Rm = 370, l = 4000 mm, n = 2, Q = 1000 kg, P = 1250 kg, k1 = 3, h = 2800 mm, Dx = 1400 mm, Dy = 1600 mm,
gn = 9,81, A = 2274 mm², i = 23,61 mm, Fv = 36 611 N (S6's value; the Mg term gives 36 611 − 3·9,81·2250/2 = 3502 N = 357 kg of rail),
k3·Maux = 0, Fp = 0; flange data c = 10, h1 = 89, f = 11, b = 19, l(lining) = 140 mm (S6 assumptions); E = 210 000.

| Quantity | Value |
|---|---|
| λ = 4000 / 23,61 | 169,4 (115 < λ ≤ 250: ω = 0,00016887·λ² = **4,85**) |
| σk = Fv·ω / A | 36 611·4,85 / 2274 = **78** N/mm² |
| Case 1 (xQ = Dx/8 = 175 mm, yQ = 0): Fx | 3·9,81·(1000·175) / (2·2800) = **920** N; σy = 29 N/mm² (Wy ≈ 23,4 cm³) |
| Case 2 (xQ = 0, yQ = Dy/8 = 200 mm): Fy | 3·9,81·(1000·200) / (1·2800) = **2102** N; σx = 51 N/mm² (Wx ≈ 30,4 cm³) |
| σm (worst: case 2) | **51** N/mm² |
| σ bending + compression | 51 + 36 611/2274 = **67** N/mm² |
| σ bending + buckling | 78 + 0,9·51 = **124** N/mm² (S6 prints 82 + 0,9·51 = 128, inconsistent with its own σk = 78: the computed value is used) |
| σperm | 370 / 1,8 = **205** N/mm² |
| δx (Fx = 920, Iy ≈ 148 cm⁴) | 0,7·920·4000³ / (48·210000·1,48·10⁶) = **2,8** mm + δstr-x |
| δy (Fy = 2102, Ix ≈ 186 cm⁴) | **5,0** mm + δstr-y (exceeds 5 mm with any structure deflection) |

**Example 2** (S6 section 3.2, cantilever guiding): l = 2500, Dx = 1100, Dy = 2100, c = 200 mm, xP = 500 mm, yP = 0, other data as above.
λ = 2500/23,61 = 106 → ω = 0,00001711·λ^2,35 + 1,04 = **2,02**; σk = 36 611·2,02/2274 = **33** N/mm².
Case 1: xQ = c + 5·Dx/8 = 888 mm: Fx = 3·9,81·(1000·888 + 1250·500)/(2·2800) = **7951** N, σy ≈ 158 N/mm² (S6 gives 7951 N and 158).
Case 2: xQ = c + Dx/2 = 750 mm, yQ = Dy/8 = 263 mm: Fx = **7226** N, σy = 143; Fy = **2764** N, σx = 42; σm = **185**; σ = 185 + 36 611/2274 = **201**;
σ = 33 + 0,9·185 = **200**. Flange bending, Fx = 7951: roller **147** N/mm² (1,85·7951/10²), sliding **96** N/mm² (6·7951·59 / (100·296)). Deflection δx = **5,8** mm, δy = 1,6 mm.

**Project fixtures** (arithmetic only, not from a source): with the preset h = 2800 mm, Q = 480 kg, Dy = 1100 mm, Dx = 2100 mm:
FF1 = 480·10·1,1/(4·2,8) = 471,4 N → design 942,9 N; FF2 = 480·10·2,1/(8·2,8) = 450,0 N → design 900,0 N. T70/65/9 at l = 2000:
σy = 3·900·2000 / (16·5350) = 63,1; σx = 3·942,9·2000/(16·9240) = 38,3; σm = 101,4 N/mm² (< 205,56); δx = 0,7·900·2000³/(48·210000·186 500) = 2,68 mm (< 5).
With the note's own fixture (h = 1200 mm) the design forces are 2200 N and 2100 N and σm of T70/65/9 at 2000 mm is 236,5 N/mm² > 205,56: NOT OK.

## 12. Questions for the user

The check must not be coded for these points until they are answered.

- **Q1 Material per profile.** DESIGN-FLOW gives Rm = 370. Table 15 needs the elongation A5: A5 > 12 % → St 2,25 / 1,8; 8 % ≤ A5 ≤ 12 % → 3,75 / 3,0.
  The catalogue S5 gives A5 min 8 % (blade 5 mm class) and 12 % (blade 8-16 mm): an A5 of exactly 12 % is in the **8-12 % band**
  (St 3,75 / 3,0), which is much stricter than the 2,25 / 1,8 an A5 of 17 % would give. Which A5 (and therefore which St) per profile? Proposal:
  catalog value A5 per profile, editable, default 12 for T70/T89/T90 and 8 for T50 (the strict reading of S5).
- **Q2 Modulus of elasticity.** E = 210 000 N/mm² (S6, steel)?
- **Q3 Section values.** Confirm the S5 values of section 8 as the catalog (ISO 7465 not on disk). For T50/50/5 the flange-bending data
  c, f are not in S5: use c = blade thickness 5 mm and f = flange thickness 5 mm (derived from the flat-bar section)? Or skip flange bending for T50?
- **Q4 Running and loading load cases of the car.** The note and `CAR-GUIDE-FORCES.md` cover safety-gear operation only. For running (k2 = 1,2)
  and loading (Fs = 0,4·gn·Q, car empty): (a) use the same convention as the note (eccentricity Dim/4, 10 N/kg)
  or EN 81-50 (Dim/8, gn = 9,81)? (b) position of the entrance (xi, yi) for the sill force (one front entrance centred on the car front? with two
  entrances, the most unfavourable); (c) apply the "within 10 % of the bracket distance → sill bending may be ignored" rule? (d) P and S at the rail
  cross (xP = yP = xS = yS = 0), as for FF1 / FF2? (e) Table 13 has no Fs under "safety device operation" (only under loading): confirm that loading is its own load case with Q = 0.
- **Q5 Safety gear type.** k1: progressive 2 (used by the note), instantaneous 3 or 5. Is k1 an input (it exists in the safety gear data)? Which value
  does the rail check take: the project's safety gear type?
- **Q6 Counterweight rails (no safety gear).** The sources give no guiding-force equations for the counterweight. Proposal (not in the
  standard): Fx = k2·gn·Mcwt·ex / (n·h_cw), Fy = k2·gn·Mcwt·ey / ((n/2)·h_cw) with ex = 5 % of the CW width (or depth?) and ey = 10 % of the
  depth (S2 5.7.2.3.3 says "5 % of the width and 10 % of the depth"), h_cw the CW guide-shoe spacing. Needed from the user: the equations to adopt,
  the CW shoe spacing h_cw (a new input? the car's h?), the CW frame width / depth (existing CW geometry?), which axis is "width", the effect
  of compensation, k2 on the CW, and whether the CW check includes only normal operation running (DESIGN-FLOW) — loading does not apply.
- **Q7 Flange bending inputs.** b (half the width of the shoe lining), l (length of the lining) and the shoe type (roller / sliding): new inputs?
  Defaults? (S6 assumed 19 mm and 140 mm.) The result depends on them strongly (roller 147 vs sliding 96 N/mm² in S6).
- **Q8 Machine supported on the guide rails = Yes.** S2 5.7.2.3.7 only says "additional load cases according to Table 13 shall be considered"
  and "forces and torques per guide rail due to auxiliary equipment Maux"; k3 is "to be determined by the manufacturer". Needed:
  (a) which vertical force the rails take: the machine mass (+ bedplate) only, or also the rope suspension forces (for 1:1 roping about
  (P+Q)·gn + CW + ropes on the machine) — and on which rails (the car rails, the CW rails, both) and how many; (b) the value of k3 (the
  note's buffer factor? 1,0? 2?); (c) whether the machine also adds horizontal forces / torques to the guiding forces; (d) which of the
  load cases (running, loading, safety device) carry Maux. Without answers the check can only use Maux = 0 for "No".
- **Q9 Deflection of structure.** δstr-x, δstr-y (building structure), bracket deflection and play (S2 5.7.2.1.2): inputs with default 0? In
  S6 the building and brackets were not included.
- **Q10 Slenderness range and spacing floor.** ω is defined for 20 ≤ λ ≤ 250. For λ > 250 (e.g. T50/50/5 at l > 2625 mm): NOT OK ("no ω
  value")? For λ < 20: use λ = 20? Smallest spacing the recommendation may return (e.g. 500 mm), below which the result is NOT OK with the
  needed profile?
- **Q11 Recommendation algorithm.** Confirm section 9 (smallest profile passing at 2000 mm; if none, the largest profile and the largest passing
  spacing; profile order by mass).
- **Q12 Rail mass and length.** Mg = catalog mass per metre × the length of one rail line. Which length: the full well height (pit + travel +
  overhead) or travel + 2·margin? The product's data has the shaft height; S2 says "the mass of one line of guide rails". And Fp for travel
  over 40 m (S2 5.7.2.3.5: Fp = n_b·Fr): input Fr (clip push-through force) needed, or ignore with a Design Notes line?
- **Q13 Combined-stress relaxation.** S1 C.2.1.3 footnote (use the stress at the outer edge when σperm < σm): use it, or keep the strict
  Wx / Wy check? (The strict check is conservative.)
- **Q14 Number of rails and shoes.** n = 2 always (two car rails, two CW rails)? Rail forces per rail "at one shoe level, equal and opposite
  at upper and lower shoes" (S4): confirm that the same per-rail force is used at mid-span of the bracket distance.
