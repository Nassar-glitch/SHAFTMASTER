"""S3 tier 3 (steel-native): reads the generated steel drawings back from the DWGs.

usage: verify_steel.py <folder SteelNativeTests wrote> [<summary json to write>]

For each <id>.dwg (DWG -> DXF with the AutoCAD Core Console, read only; then ezdxf) and its <id>-expected.json:
  * steel project: the plan has four column sections (8 vertices) with their plates and bolts and the beam ring; the template wall
    entities are still there and hidden; the machine-room plan has the four columns and the ring and the room walls are hidden; B-B has
    the two top beams and the slab is hidden; A-A has the tower of the expected faces: beams at the levels of the left face, the beams
    of the entrance and back faces end-on, two diagonals in every panel, gusset plates, the base block, and the walls and slabs are hidden;
  * the sheet: the masonry notes are gone, a STEEL legend entry is there;
  * concrete project: no steel entity anywhere and the walls are visible.
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile

import ezdxf

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import steel_overlap  # noqa: E402
import fixing_check  # noqa: E402

CORE_CONSOLE = r"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe"
LAYOUT = "SM_Geared_A0"
failures = []
checks = 0


def check(ok, name):
    global checks
    checks += 1
    if not ok:
        failures.append(name)
        print("FAIL:", name)


def to_dxf(dwg, dxf):
    work = tempfile.mkdtemp(prefix="stl_")
    src, out, scr = os.path.join(work, "in.dwg"), os.path.join(work, "out.dxf"), os.path.join(work, "run.scr")
    shutil.copyfile(dwg, src)
    with open(scr, "w", newline="") as f:
        f.write("\r\n".join(["FILEDIA", "0", "DXFOUT", out, "16", ""]))
    try:
        subprocess.run([CORE_CONSOLE, "/i", src, "/s", scr, "/l", "en-US"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=300)
    except subprocess.TimeoutExpired:
        pass
    if not os.path.exists(out):
        raise RuntimeError("DXF not written for " + dwg)
    shutil.copyfile(out, dxf)
    shutil.rmtree(work, ignore_errors=True)


def tag(e):
    if not e.xdata or "ELEVATOR_AUTOMATION" not in e.xdata:
        return None
    values = [v.value for v in e.get_xdata("ELEVATOR_AUTOMATION")]
    return (values[0], values[1] if len(values) > 1 else "")


def invisible(e):
    return bool(e.dxf.get("invisible", 0))


def closed_poly(e, n):
    return e.dxftype() == "LWPOLYLINE" and e.closed and len(e) == n


def main():
    folder = sys.argv[1]
    summary = sys.argv[2] if len(sys.argv) > 2 else None
    cases = sorted(f[:-len("-expected.json")] for f in os.listdir(folder) if f.endswith("-expected.json"))
    for case in cases:
        expected = json.load(open(os.path.join(folder, case + "-expected.json")))
        dwg = os.path.join(folder, case + ".dwg")
        dxf = os.path.join(folder, case + ".dxf")
        to_dxf(dwg, dxf)
        doc = ezdxf.readfile(dxf)
        ms = doc.modelspace()
        fixing_check.check_fixing(doc, case, check)
        fixing_check.check_detail_overlap(doc, case, check)
        # the insertions of the steel blocks are read through their parts (principle 11: named blocks), a label is a multileader
        tagged = steel_overlap.expand(ms)
        steel_keys = [t[0] for e, t in tagged if t[0].startswith(("Plan.Steel.", "Steel.", "SectionAA.Steel."))]
        if expected["Steel"]:
            overlaps = steel_overlap.check(ms)
            check(len(overlaps) == 0, case + ": no steel label lies on a text, a dimension text or the slab edge, no leader crosses a text " + str(overlaps[:4]))
        if not expected["Steel"]:
            check(len(steel_keys) == 0, case + ": a concrete project has no steel entity")
            walls = [e for e, t in tagged if t[1] == "wall" and t[0].startswith("Plan.")]
            check(len(walls) > 0 and all(not invisible(e) for e in walls), case + ": the template walls are visible")
            slabs = [e for e, t in tagged if t[0].startswith("SectionAA.Floor.") and ".Slab." in t[0]]
            check(len(slabs) > 0 and all(not invisible(e) for e in slabs), case + ": the A-A slabs are visible")
            continue
        tower = expected["Tower"]
        bolts = tower["BoltCount"]
        # ---- plan ----
        plan = [e for e, t in tagged if t[0].startswith("Plan.Steel.")]
        cols = [e for e in plan if closed_poly(e, 8)]
        rects = [e for e in plan if closed_poly(e, 4)]
        circles = [e for e in plan if e.dxftype() == "CIRCLE"]
        check(len(cols) == 4, case + ": the plan has four column sections (" + str(len(cols)) + ")")
        check(len(rects) == 9, case + ": four base plates, four ring beams and the slab edge in plan (" + str(len(rects)) + ")")
        check(len(circles) == 4 * bolts, case + ": " + str(4 * bolts) + " bolt circles in plan (" + str(len(circles)) + ")")
        check(sum(1 for e in plan if e.dxftype() == "HATCH") == 4, case + ": four solid column sections")
        check(any(tower["Column"] in steel_overlap.text_of(e) and "STEEL COLUMN" in steel_overlap.text_of(e) for e in plan), case + ": the plan names the column profile")
        check(any(tower["Beam"] in steel_overlap.text_of(e) and "STEEL BEAM" in steel_overlap.text_of(e) for e in plan), case + ": the plan names the beam profile")
        walls = [e for e, t in tagged if t[1] == "wall" and t[0].startswith("Plan.")]
        check(len(walls) > 0 and all(invisible(e) for e in walls), case + ": the template walls of the plan are hidden (" + str(len(walls)) + ")")
        # editable drawing: every pointing label is one multileader, repeated parts are named blocks, members are on their layers
        steel_all = [(e, t) for e, t in [(e, tag(e)) for e in ms] if t and t[0].startswith(("Plan.Steel.", "Steel.", "SectionAA.Steel."))]
        check(sum(1 for e, t in steel_all if e.dxftype() == "MULTILEADER") >= 8, case + ": the steel labels are multileaders")
        check(not any(e.dxftype() == "MTEXT" and ("STEEL COLUMN" in e.text or "STEEL BEAM" in e.text or "BASE PL" in e.text) for e, t in steel_all),
              case + ": no steel label is a loose text")
        block_names = [e.dxf.name for e, t in steel_all if e.dxftype() == "INSERT"]
        plan_blocks = [e.dxf.name for e, t in steel_all if e.dxftype() == "INSERT" and t[0].startswith("Plan.Steel.")]
        check(sorted(n[-1] for n in plan_blocks if n.startswith("SM_COLUMN_")) == ["L", "L", "R", "R"], case + ": the four column sections are references of two named blocks (left and right web)")
        check(sum(1 for n in plan_blocks if n.startswith("SM_BASE_PLATE_")) == 4, case + ": the four base plates are references of one named block")
        check(any(n == "SM_CONTROL_PANEL" for n in block_names), case + ": the control panel is a named block")
        layers = set(e.dxf.layer for e, t in steel_all)
        check({"S-COLUMN", "S-BEAM", "S-PLATE", "A-ANNO", "A-SLAB"} <= layers and "Steel" not in layers, case + ": the steel entities are on S- and A- layers " + str(sorted(layers)))
        check(any(e.dxftype() == "MULTILEADER" and e.dxf.layer == "A-ANNO" for e, t in steel_all), case + ": the labels are on A-ANNO")
        # ---- machine-room plan ----
        mr_all = [(e, t) for e, t in tagged if t[0].startswith("Steel.MachineRoom.")]
        mr = [e for e, t in mr_all if not t[0].startswith("Steel.MachineRoom.Controller.")]
        controller = [e for e, t in mr_all if t[0].startswith("Steel.MachineRoom.Controller.")]
        check(sum(1 for e in mr if closed_poly(e, 8)) == 4, case + ": the machine-room plan has the four column sections")
        check(sum(1 for e in mr if closed_poly(e, 4)) == 5, case + ": and the four ring beams and the slab edge")
        check(not any(e.dxftype() == "CIRCLE" for e in mr), case + ": no base plate bolts at the tower top")
        room = [e for e, t in tagged if t[1] == "room" and t[0].startswith("MachineRoom.")]
        check(len(room) > 0 and all(invisible(e) for e in room), case + ": the room walls are hidden")
        check(any("TOWER TOP" in steel_overlap.text_of(e) and "1:" in steel_overlap.text_of(e) for e in mr), case + ": the tower-top plan has its title with the scale")
        check(not any("CONTROLLER AND SWITCHES" in steel_overlap.text_of(e) for e in mr), case + ": no long controller note")
        check(not any("CONTROLLER" in steel_overlap.text_of(e) for e in plan), case + ": no controller on the shaft plan")
        check(any("CONTROL PANEL" in steel_overlap.text_of(e) for e in controller), case + ": the tower-top plan labels the control panel")
        check(sum(1 for e in controller if e.dxftype() == "LWPOLYLINE") >= 10, case + ": the control panel symbol is drawn on the tower-top plan")
        cols8 = [e for e in mr if closed_poly(e, 8)]
        rects4 = [e for e in mr if closed_poly(e, 4)]

        def area(e):
            pts = list(e.get_points())
            return (max(p[0] for p in pts) - min(p[0] for p in pts)) * (max(p[1] for p in pts) - min(p[1] for p in pts))
        slab_edge = max(rects4, key=area) if rects4 else None
        ring = [e for e in rects4 if e is not slab_edge]
        if cols8 and ring and controller:
            ex0 = min(min(p[0] for p in e.get_points()) for e in cols8)
            ex1 = max(max(p[0] for p in e.get_points()) for e in cols8)
            ey0 = min(min(p[1] for p in e.get_points()) for e in cols8)
            ey1 = max(max(p[1] for p in e.get_points()) for e in cols8)
            inside = all(min(p[0] for p in e.get_points()) >= ex0 - 0.5 and max(p[0] for p in e.get_points()) <= ex1 + 0.5
                         and min(p[1] for p in e.get_points()) >= ey0 - 0.5 and max(p[1] for p in e.get_points()) <= ey1 + 0.5 for e in ring)
            check(inside, case + ": the ring beams lie within the column envelope")
            flush = [any(abs(min(p[0] for p in e.get_points()) - ex0) < 0.5 for e in ring), any(abs(max(p[0] for p in e.get_points()) - ex1) < 0.5 for e in ring),
                     any(abs(min(p[1] for p in e.get_points()) - ey0) < 0.5 for e in ring), any(abs(max(p[1] for p in e.get_points()) - ey1) < 0.5 for e in ring)]
            check(all(flush), case + ": a beam is flush with the outer column face on every side")
        hidden_leftovers = [e for e, t in tagged if t[1] == "governor" and invisible(e)]
        check(len(hidden_leftovers) > 0, case + ": the wall-opening leftover of the concrete room is hidden (" + str(len(hidden_leftovers)) + ")")
        # the slab edge: W + 2 x 850 wide, 250 deep, directly outside the front face; the supports and machine beams within the footprint
        if slab_edge is not None and cols8:
            pts = list(slab_edge.get_points())
            width = max(p[0] for p in pts) - min(p[0] for p in pts)
            depth_ = max(p[1] for p in pts) - min(p[1] for p in pts)
            check(abs(width - (tower["Width"] + 1700)) < 1 and abs(depth_ - 250) < 1, case + ": the building slab edge is W + 1700 by 250 (" + str(round(width)) + " x " + str(round(depth_)) + ")")
            check(abs(max(p[1] for p in pts) - min(min(q[1] for q in e.get_points()) for e in ring)) < 1, case + ": the slab edge touches the front beam face")
        machine = [e for e, t in mr_all if t[0].startswith("Steel.MachineRoom.Machine.")]
        if cols8 and ring and machine:
            ey0 = min(min(q[1] for q in e.get_points()) for e in ring)
            ey1 = max(max(q[1] for q in e.get_points()) for e in ring)
            from ezdxf import bbox as _bbox
            out = 0
            for e in machine:
                try:
                    b = _bbox.extents([e], fast=True)
                except Exception:
                    continue
                if b.has_data and (b.extmin.y < ey0 - 1 or b.extmax.y > ey1 + 1):
                    out += 1
            check(out == 0, case + ": the machine supports and beams are within the steel footprint (" + str(out) + " outside)")
        # ---- B-B ----
        bb = [e for e, t in tagged if t[0].startswith("Steel.Section.")]
        check(sum(1 for e in bb if closed_poly(e, 8)) == 0, case + ": B-B draws no steel profile sections (the section carries no labels)")
        check(not any(steel_overlap.text_of(e) for e in bb), case + ": B-B has no steel label")
        titles_bb = [e for e in ms if e.dxftype() == "MTEXT" and not tag(e) is None and "B-B" in e.text and "SEC" in e.text]
        check(len(titles_bb) >= 1 and all(not invisible(e) for e in titles_bb), case + ": the B-B title stays")
        slab = [e for e, t in tagged if t[1] in ("slab", "concrete") and t[0].startswith("MachineRoomSection.") and not (e.dxftype() == "MTEXT" and "B-B" in e.text)]
        check(len(slab) > 0 and all(invisible(e) for e in slab), case + ": the B-B slab and the concrete blocks are hidden (" + str(len(slab)) + ")")
        # ---- A-A ----
        aa = [e for e, t in tagged if t[0].startswith("SectionAA.Steel.")]
        axis = [e for e, t in tagged if t[0] == "SectionAA.Frame.Axis"]
        check(len(axis) == 1, case + ": the A-A frame is there")
        a = axis[0]
        o = a.dxf.start
        vx = (a.dxf.end - a.dxf.start).normalize()
        closed4 = [e for e in aa if closed_poly(e, 4)]
        expected_rects = tower["LeftBeams"] + tower["FrontBeams"] + tower["BackBeams"] + 2 + tower["Plates"] + 1 + tower["LeftGussets"] + 2 * tower["LeftBraces"] + 3  # + the machine housing: two walls and the roof plate
        check(len(closed4) == expected_rects, case + ": A-A rectangles and strips " + str(len(closed4)) + " of " + str(expected_rects))
        check(sum(1 for e in aa if e.dxftype() == "HATCH") == 1, case + ": A-A has the hatched base block")
        # beam levels of the face: rectangles as wide as the face and as high as the beam, by their centre height
        length = tower["Depth"]
        levels = []
        for e in closed4:
            pts = [p for p in e.get_points()]
            xs = [(p[0] - o.x) * vx.x + (p[1] - o.y) * vx.y for p in pts]
            ys = [-(p[0] - o.x) * vx.y + (p[1] - o.y) * vx.x for p in pts]
            if abs((max(xs) - min(xs)) - length) < 1.0:
                levels.append((max(ys) + min(ys)) / 2)
        want = sorted(tower["Levels"]["Left"])
        got = sorted(levels)
        check(len(got) == len(want) and all(abs(x - y) < 1.0 for x, y in zip(got, want)),
              case + ": the A-A beams are at the levels of the face (" + str(len(got)) + " of " + str(len(want)) + ")")
        check(any("STEEL COLUMN" in steel_overlap.text_of(e) for e in aa), case + ": the A-A labels name the column")
        check(any("L50X50X5" in steel_overlap.text_of(e) for e in aa), case + ": and the bracing angle")
        replaced = [e for e, t in tagged if t[0].startswith("SectionAA.Floor.") and ".Slab." in t[0]]
        replaced += [e for e, t in tagged if t[0].startswith(("SectionAA.Shaft.", "SectionAA.MR.Wall.", "SectionAA.MR.Ceiling"))]
        check(len(replaced) > 0 and all(invisible(e) for e in replaced), case + ": the A-A walls, slabs and room walls are hidden (" + str(len(replaced)) + ")")
        # ---- details of a concrete shaft left out of a steel sheet ----
        untagged = [e for e in ms if e.dxftype() == "MTEXT" and not tag(e)]
        found = [e for e in untagged if "SIDE - GUIDE RAIL" in e.text and invisible(e)]
        check(len(found) >= 1, case + ": the concrete detail SIDE - GUIDE RAIL is hidden")
        check(any("SEC. C-C" in e.text and not invisible(e) for e in untagged), case + ": the door section C-C stays")
        check(any("VIEW at D-D" in e.text and not invisible(e) for e in untagged), case + ": the door view D-D stays")
        check(any("FISH PLATE" in e.text and not invisible(e) for e in untagged), case + ": the fish plate stays")
        # ---- the sheet ----
        layout = doc.layouts.get(LAYOUT)
        texts = [e.text for e in layout if e.dxftype() == "MTEXT"]
        check(any("STEEL" == t.strip() or t.strip().endswith("STEEL") for t in texts), case + ": the legend has a STEEL entry")
        from ezdxf import bbox
        stray = 0
        for e in layout:
            if e.dxftype() in ("LWPOLYLINE", "SPLINE", "LINE", "HATCH"):
                try:
                    b = bbox.extents([e], fast=True)
                except Exception:
                    continue
                if b.has_data and 1021 < b.center.x < 1170 and 566 < b.center.y < 591 and not (1029 < b.center.x < 1048):
                    stray += 1
        check(stray == 0, case + ": the masonry notes are gone from the title block (" + str(stray) + " left)")
    print("steel-native read-back: " + str(checks) + " checks, " + str(len(failures)) + " failures")
    if summary:
        with open(summary, "w") as f:
            json.dump({"Checks": checks, "Failures": failures}, f, indent=1)
    return 1 if failures else 0


sys.exit(main())
