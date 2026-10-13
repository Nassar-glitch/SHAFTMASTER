# ShaftMaster design flow

Agreed between the user and the Planner on 2026-10-08. This is the reference for every feature built from now on. Planner prompts must follow it, and Executor work must not contradict it. Only the Planner changes this file, and only with the user's agreement.

## Principles

1. **Design, not cost.** The application talks design only. There are no budget, price or cost fields.
2. **Survey first.** The site survey is the only data the user must replace; a new project opens with a typical example so that everything renders at once. Every other value is either proposed from a preset or derived. The user edits; the user does not fill in from scratch.
3. **Presets live in one editable library** (`config/presets.json`). Company practice can change there without a code change.
4. **Lock and follow:**
   - Any value the user edits is locked.
   - Unlocked values follow when an upstream value changes.
   - Every field has "Reset to preset".
5. **Standard ladders.** Components with standard sizes (rails, ropes, beams, door openings) are picked from a ladder.
   - When a check exists, the application proposes the smallest size that passes.
   - It upsizes automatically while the value is unlocked.
   - A locked value that fails is kept. The calculation sheet shows the check's true result (values and OK / NOT OK, with no commentary). The application warns before export, and the Design Notes file holds the clause and the explanation.
6. **Clean outputs.** The UI, the drawings and the calculation sheet carry no remarks, disclaimers or "preset / assumed / not from catalog / review" text.
7. **Design Notes file.** This is a separate, short exported document of simple advisory notes, written as an engineer's advice. It holds:
   - short quotations from the design code or the calculation note, with the clause;
   - warnings, for example: "car available area exceeds the EN 81-20 Table 6 maximum for the rated load";
   - recommendations, for example: "bracket spacing ≤ 2000 mm recommended", "car rail T89/62/16 recommended", "these dimensions cannot be used because…".
   
   Each note is one or two lines. There are no long tables of passed checks. It is the only place for such text.
8. **Drawing quality (agreed 2026-10-11).** Every generated drawing must look like a draughtsman's drawing:
   - **Dimensions are arranged.** Chained dimensions sit on one common dimension line, and successive rows keep an equal spacing. Extension lines start at the real geometry edges. No text overlaps another text, line or dimension. A value too small to fit between its arrows is placed outside the chain, beside it, never on top of a neighbour.
   - **Every component is drawn as a real part,** not as floating lines. For example, automatic door panels sit inside their door frame or header outline.
9. **Clear names (agreed 2026-10-11).** Field and option names in the application are short, plain and self-explanatory, for example "Doors Offset". Directions are given as Left / Right (as seen in the plan drawing), never as internal terms such as "towards the fixed side" or a sign convention.
10. **Loads also in kg (agreed 2026-10-11).** Wherever a load or force is shown in N or kN (application pages, calculation sheet, figures, Design Notes), its kg value is shown beside it.
    - kg = N / 9.81, rounded UP to the next multiple of 5 kg (1201 → 1205; an exact multiple stays).
    - This is a display rule only. Every calculation and every OK / NOT OK result keeps using the unrounded N values.

## Chain

| # | Step | Kind | Rule / preset (editable) |
|---|---|---|---|
| 1 | Site survey: shaft width SW, depth SD, pit depth, overhead, number of stops, floor heights, machine room (yes/size), entrance side(s) | Input (opens with a typical example) | A new project opens complete with a typical shaft: SW 1500, SD 1800, pit 1300, overhead 4200, 5 stops at 3000 mm. The user replaces these with the survey. |
| 2 | System | Selection | Current: geared traction, machine room above, 1:1, with or without deflector. Future: 2:1, MRL gearless, hydraulic, home lift. |
| 3 | Rated load Q | Input | Entered by the designer, or taken from the client requirement. A new project opens with 630 kg. Persons = floor(Q / 75) is shown. |
| 4 | Rated speed | Preset | Geared traction: 1.0 m/s. |
| 5 | Doors | User choice + optional size | See "Doors (agreed 2026-10-10)" below. |
| 6 | Car internal dimensions | Derived | From the shaft, rails, counterweight and door allowances (the shared plan recipe). The Table 6 area-versus-load check goes to the Design Notes file only. |
| 7 | Masses | Preset / derived | Car mass P and machine mass from the capacity table (interpolated). CW = P + q·Q, with q = 0.5. |
| 8 | Guide rails | Ladder | Car rails: Q ≤ 800 kg T70/65/9; 801–1600 kg T89/62/16; above that, or goods/bed lifts, T90/75/16. CW rails: Q ≤ 1000 kg T50/50/5; above that T70/65/9. Verified by the EN 81-50 5.10 rail check once it exists. |
| 9 | Safety gear | Rule | Progressive, unless v ≤ 0.63 m/s, where instantaneous is allowed (EN 81-20 5.6.2.1.2.1). 2 car guide rails. |
| 10 | Buffers | Rule | Energy accumulation only when v ≤ 1.0 m/s; otherwise energy dissipation (EN 81-20 5.8.1.5–5.8.1.6). Counts preset to 1 car and 1 CW buffer. |
| 11a | Deflector sheave | Rule (market practice) | Keep the existing TZ rule: 250 mm for TZ 520–800, 400 mm for TZ 800–1100. Deflector D/d is not a selection constraint and is not a check row in the calculation sheet. A deflector D/d below 40 (EN 81-20 5.5.2.1) is a Design Notes quotation only. The rope safety factor (EN 81-50 5.12, Kp) still uses the actual deflector diameter. Agreed 2026-10-08. |
| 11 | Ropes and machine | Ladder | Q ≤ 480: 4 × 10 mm; ≤ 800: 5 × 11; ≤ 1275: 6 × 12; above that 7 × 12. Sheave pitch diameter 520 mm. Rope data table by diameter (MBL, kg/m). Verified by Sf, D/d and traction checks once they exist. |
| 12 | Guide-shoe spacing h | Preset | 2800 mm, the vertical centre-to-centre distance between the upper and lower car guide shoes on the car frame. Agreed 2026-10-08. |
| 13 | Machine room | Ladder / check | Machine beams (existing selector). Lifting hook capacity of at least 10 kN. |
| 14 | Outputs | Generated | Native drawings, STEP, the calculation sheet, and the Design Notes file. |

## Doors (agreed 2026-10-10)

- **Types:** Manual (existing), Automatic 2S (two-panel telescopic side opening), Automatic 2C (two-panel centre opening). The user chooses the type; new projects keep Manual.
- **Clear opening:** optional. Standard sizes: 700, 800, 900, 1000, 1200.
  - When blank, the application selects the largest size that fits by the rules.
  - When the user enters a size that does not fit, the field shows a short "does not fit" message with the reason. Design Notes gives the car or shaft width needed.
- **Doors Offset (agreed 2026-10-11), 2S and 2C:** a field named "Doors Offset" holds the distance in mm from the shaft centre line to the opening centre line, plus a direction **Left / Right** as seen in the plan drawing (standing at the landing, facing the door). It replaces the former "Opening offset, preferred".
  - Defaults: 2S uses the preferred offset from the returns (75 mm towards the fixed side at 800) and shifts automatically in narrow shafts; 2C uses 0 (centred).
  - The field shows the allowed range in Left/Right terms. The range keeps the returns at or above their minimums and the landing boxes at least 25 mm (the wall clearance preset) from the side walls.
  - A value outside the range is kept, never clamped. The field shows a short "out of range" message with the maximum, and Design Notes explains why.
- **Door page labels (agreed 2026-10-11):** "Side opening (2S)", "Centre opening (2C)", "Panels stack to" (Auto / Left / Right), "Car return, left / right", "Car door depth", "Landing door depth".
- **Handing (2S):** the stacking side is opposite the speed governor by default (right in the reference drawings). The user can choose left or right.
- **Clearances are presets, calculated by default and editable** in an "Advanced dimensions" expander on the door page:
  - front zone split: car door zone, sill gap, landing door zone;
  - car side returns (fixed side and stacking side for 2S, both sides for 2C);
  - landing jamb clearances;
  - the 2S opening offset.
  Default values come from the user's reference drawings `Automatic 2S800 Geared Rear CW.dwg` and `Automatic 2C800 Geared Rear CW.dwg`:
  - 2S front zone 245 = 130 + 25 + 90; fixed return 200; stacking return = opening/2 − 50; opening offset 75 mm towards the fixed side for 800 mm;
  - 2C front zone 220; centred opening.
  The shaft and car sizes in those drawings are examples, not minimums.
- **Constraints (logic and code; violations go to Design Notes with the needed dimension, never silently changed):**
  - the opening fits the car width minus the returns;
  - the landing door panels stack inside the shaft width with clearance;
  - no clash with guide rails, brackets, the speed governor or the counterweight;
  - sill gap ≤ 35 mm and front car-to-wall distance ≤ 150 mm (EN 81-20);
  - the existing running clearances.
- **Effects:** the door type changes the front zone, and so the car depth. Every dependent value (car area, Table 6, masses, calculations) follows automatically.

## Guide rails (agreed 2026-10-13)

- **Order on the rails page,** each step recommended from everything entered before it, and every choice the user's:
  1. **Machine supported on the guide rails:** Yes / No. This is the first choice, because it changes the loads on the rails.
  2. **Car rail profile:** a list (T50/50/5, T70/65/9, T89/62/16, T90/75/16). The recommendation is the smallest profile that passes the checks with the recommended bracket spacing.
  3. **Car rail bracket spacing:** a number in mm, not a list. The recommendation is the largest spacing that passes with the chosen profile, never above the 2000 mm preset.
  4. **CW bracket spacing follows the car bracket spacing (user, 2026-10-13).** It changes with the car value until the user types a CW spacing. Fields that only matter for "machine on the guide rails = Yes" (number of rails carrying the machine) are hidden when the answer is No. Spacings above 2000 mm, for example one bracket per floor slab at 3000 mm, are normal inputs: the check decides OK / NOT OK, and nothing is refused.
  5. **The same for the counterweight rails:** profile, then bracket spacing. The CW rails are checked for normal operation (no safety gear).
- **Generative:** while unlocked, every value follows when an upstream value changes (lock and follow). A typed or chosen value is kept.
- **Checks:** EN 81-50 5.10 for bending, combined stress, buckling, flange bending and deflection, using FF1 / FF2, P5 and the machine loads when the machine sits on the rails. The equations come from the user's calculation note or EN 81-50, and are never guessed.
- **Material:** Rm 370 N/mm² for every profile, as a catalog value.
- **A choice that fails is kept.** The calculation sheet shows the true values and OK / NOT OK only. The application, the sheet and the drawings say nothing else. Design Notes gives the reason and the needed profile or spacing.
- **The catalog** (dimensions, section properties, mass per metre) is editable data in the presets library.
- **Answers of 2026-10-13 to GUIDE-RAILS.md section 12:**
  - Q1: A5 > 12 % (St 2.25 / 1.8), editable per profile.
  - Q2: E 210 000.
  - Q3: the Savera values; T50 flange bending uses 5 mm.
  - Q4: EN 81-50 for running and loading; the safety-gear case keeps the approved FF1 / FF2 convention.
  - Q5: k1 from the project's safety gear.
  - Q9: δstr presets 0.
  - Q10: λ > 250 is NOT OK; the spacing floor is 500 mm.
  - Q11 as proposed; Q13 strict; Q14 two rails each.
  - Q8: with "machine on the guide rails = Yes", the machine load is shared by the car and CW rails together. How it is shared and checked is researched from published practice, and presented as simply as the rest of the sheet.
  - Q6, Q7 and Q12: presets from typical catalogues (CW shoe spacing, shoe type and lining, clip push-through force), all editable. Keep it simple.
  - The research and implementation run in the cloud as far as possible.

## Steel shaft structure (requested 2026-10-13, in planning)

- **Shaft construction** on the shaft page: Concrete (today, the default) or Steel structure. The first steel system is UPN.
- **Reference drawing:** `UPN STEEL SHAFT.dxf` (repository root; shaft only, no lift). The first client case is:
  - UPN 140 columns at the corners;
  - UPN 100 beams every 1500 mm, at the same levels as the guide-rail brackets;
  - double-cross (X) bracing L50×50×5.
- **Beam levels:** there is always a beam at every floor level, because the doors are fixed to the structure: automatic doors always, manual doors by default. The levels between floors follow the rule in "Answers of 2026-10-13" item 1 below; this replaces the earlier "2 spaces per floor" idea. The levels are editable.
- **One model:** the steel structure is generated from the same project as everything else (plan, A-A, the sheet, and later STEP and the calculations). The rail brackets sit on the beams, so the rail bracket spacing follows the beam levels.
- **Goal:** the same four main views (shaft plan, machine-room plan, A-A and B-B) and the same A0 sheet as for a concrete shaft, drawn with the steel tower. The reference DXF is only a source of the steel look and rules.
- **Settled (user, 2026-10-13):**
  - When the door is fixed to the structure, there is also a beam at the door head (the header beam).
  - The current product is an external lift: the machine room sits on top of the steel structure. A steel tower inside a stairwell comes later.
  - The bracing (which faces, single or double cross) is drawn as in the reference side elevation for now. The face-by-face questions belong to the later STEEL module.
- **Answers of 2026-10-13 (STEEL-SHAFT.md section 7):**
  1. **Beam levels:** the fixed beams are the floor beam and the door-head beam (at the door construction height above the floor). Going up from the door-head beam, a beam is placed every s mm (the beam-spacing preset, 1500 mm, editable). When the rest up to the next floor beam is not more than s, the next beam is the floor beam. A tall floor therefore gets more beams automatically. The floor-to-door-head gap is the door height. The pit and the top follow the same spacing. The rail bracket spacing is the largest gap.
  2. **No outriggers.** The machine sits on top of the tower, within the shaft footprint. The controller and the switches go inside the top floor, for example to the right of the door as seen standing in front of the lift. The machine-room plan becomes the tower-top plan with the machine.
  3. W × D is the clear size between the inner steel faces; the steel is outside it.
  4. The shaft plan is cut just above the floor beam (the Planner's decision).
  5. **The concrete base** is drawn from editable presets (size, thickness, bars: count and diameter), sized from the steel footprint plus a margin. Its structural design belongs to the later STEEL STRUCTURE module.
- **Bracing:** double cross (X) of L50×50×5 in every panel; the faces wait for the STEEL module.
- **Not now:** structural calculations (a later STEEL STRUCTURE module), and the generated details stage G (postponed).
- **Profile catalogue:** UPN sizes (dimensions and mass) and L angles, as editable data.

## Layouts and PDF (agreed 2026-10-12)

- **Sheets:** the generated DWG carries ready paper-space layouts built from the user's templates in `Layouts_Sec/`. The first is `01_Geared_MR_1-1_LAYOUT.dxf` (A0). The Gearless 2:1, Forklift and Hydraulic layouts wait for those systems.
- **Everything is generated:**
  - viewports show the project's own views;
  - the title block is filled from the project and the calculations: company name and logo, project, client, speed, rated load and persons, travel, stops, supply, loads P1–P7 and the other values in the block, scale, date, prepared / checked / approved, and the revision.
  
  When the design changes, the layout follows it. Nothing on the sheet is typed twice.
- **Company name:** entered once in the application settings, and used in the title block and in the file names.
- **PDF:** exported from the same layout through AutoCAD, next to the DWG and the project file.
- **File names** (DWG, PDF and project) follow one pattern: `COMPANY-TYPE-CW-W{SW}D{SD}-PROJECT-MODEL{NN}`, for example `ELEVATORSCOMPANY-GB-RCW-W1500D1600-VILLA bx12-MODEL00.pdf`.
  - Type: GB geared (gearbox); later GL gearless, HY hydraulic, MRL.
  - Counterweight: RCW rear, SCWL side left, SCWR side right.
  - The project name is kept exactly as the user typed it. Only the characters Windows forbids in file names are replaced.
  - NN is the two-digit drawing revision: 00 first, then 01, 02, … It is the same revision as the calculation sheet.
- **Decisions of 2026-10-12 (L2a review):**
  - Every current geared project is RCW; side counterweight positions come with that layout later.
  - The company name and all report data (logo, client, prepared / checked / approved, revision) are ONE set of settings, used by the calculation sheet, Design Notes, the title block and the file names. The company name is used exactly as typed.
  - The revision is a number only: 00, 01, 02 …, shown the same on the sheet, the title block and the file name.
  - The title block shows P1–P7 in kg only, as calculated: "P1 = 545 KG.". P6 is the load of one car buffer and P7 of one counterweight buffer. The date is written as dd/MM/yyyy.
- **Sheet arrangement (agreed 2026-10-12, geared A0):**
  - **Paper:** A0 landscape (1189 × 841). The title block column is on the right.
  - **Left column:** the main shaft plan at the bottom, and the machine-room plan above it.
  - **Right of it:** the tall vertical section with the floors (A-A).
  - **Top right:** the machine section with the beams and the traction angle (B-B).
  - **Under those:** the auxiliary detail views.
  - **Scales and positions are chosen automatically.** Standard scales are used; each view is shown at its scale under it, and the scale cell reads "AS SHOWN". The sheet must look composed: no overlap and no large empty areas. For example, a 15-stop section may take the full height, and a 2–3 stop section must not leave the space above it empty.
- **Details become generated** from the detail library in the template's model space. For each kind (rail fixing and fish plate, car and CW rails with brackets, bracket spacing, door elevation and section), one representative view becomes parametric from the project: rail size, bracket spacing, door type and size.
- **Fonts:** the template's SHX fonts stay. Arabic text uses xarab; the program converts normal Arabic typing to the xarab encoding. English text uses cent. The fonts ship with the application, so every PDF looks the same. The company name is written in whichever language the user types it.
- **Revision table:** the first row is filled with the current revision number and date. The user may edit it in the DWG.
- **Other fields:**
  - logo from the settings, as an image;
  - project number from the document number;
  - type of work "أعمال مصاعد";
  - phase optional;
  - drawing number 1;
  - entrances = number of stops (one entrance per floor).
- **Detailing (later stage):** detail views (rail size and bracket spacing, fixings, door elevation and so on) become generated from the project. Until then, the template's detail views stay as drawn.

## Where we are (2026-10-13)

- **Done and released:** doors (v05); the A0 sheet with PDF and file names (v06); the guide-rail check R1 and R2 (merge pending the user's golden approval, then a push).
- **Postponed:** stage G, the generated detail views (rails and brackets with the real profile and spacing, the door detail, STEP rails).
- **Now:** the steel shaft structure (UPN), for a real client project.

## Build stages (order)

Status 2026-10-09: stages 1 and 2 are done. After the heavy-load selection (joint β / q / Dt search; heavier car masses), 1:1 passes Q 320–1000 on every tested shaft, and 1275 on the default survey. The remaining 1275/1600 cases get a "2:1 roping is recommended" Design Notes recommendation and wait for the 2:1 stage. Next come release v03, then the repository and architecture cleanup and a push to the private GitHub repo (legacy ElevatorPlanGenerator under legacy/). Feature stages 3+ resume after that.

1. **Rope and traction checks** (EN 81-20 5.5, EN 81-50 5.11/5.12). This completes the calculation note. It also introduces the generic preset library and the lock/follow mechanism, applied first to the new rope/traction inputs, plus the Design Notes export.
2. **Flow foundation:**
   - apply presets and lock/follow to all remaining inputs (survey, Q, speed, masses/CW, safety gear and buffer rules, rope ladder with auto-upsize);
   - the Table 6 note;
   - cleanup of UI chatter.
   
   No drawing change.
3. **Rail profile library** (T50/50/5, T70/65/9, T89/62/16, T90/75/16) used by the drawings, STEP and figures, plus the EN 81-50 5.10 rail check with auto-upsizing.
4. **Doors:** manual/automatic, side/centre opening, automatic opening selection, drawings.
5. **Further systems** (2:1, MRL gearless, hydraulic, home lift), the ready layout (general arrangement) and component detailing.
