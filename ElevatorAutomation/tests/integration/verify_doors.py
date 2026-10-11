"""D2 tier 3: reads the automatic door geometry back from the generated DWGs and compares it with the DoorLayout numbers.

usage: verify_doors.py <output folder>   (the folder DoorsNativeTests wrote: <id>.dwg and <id>-expected.json)

For each case: DWG -> DXF with the AutoCAD Core Console (read only), then ezdxf. Every new plan entity (key Plan.AutoDoor.NNN) is matched
with the geometry the layout calls for: rectangles (panels, parked blocks, frame, jambs, returns, boxes) and lines (construction lines,
pocket lines, sills, centre lines) by layer, and the dimension values. The plan is rendered to <id>-plan.png (only the entities of the
plan view, hidden template geometry left out) for the visual comparison with the reference drawings.
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile

import ezdxf
from ezdxf.addons.drawing import matplotlib as mpl

PREFIX = "Plan.AutoDoor."
CORE_CONSOLE = r"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe"
TOL = 0.05
WALL_PIECES = 5  # keys 001-005: the redrawn wall pieces (clones of template entities)
failures = []
checks = 0


def check(ok, name):
    global checks
    checks += 1
    if not ok:
        failures.append(name)
        print("FAIL:", name)


def to_dxf(dwg, dxf):
    # The Core Console is run on a copy in a short, space-free work folder (it fails on some long paths).
    work = tempfile.mkdtemp(prefix="doors_")
    src = os.path.join(work, "in.dwg")
    out = os.path.join(work, "out.dxf")
    shutil.copyfile(dwg, src)
    scr = os.path.join(work, "run.scr")
    lines = ["FILEDIA", "0", "DXFOUT", out, "16"]
    with open(scr, "w", newline="") as f:
        f.write((chr(13) + chr(10)).join(lines) + chr(13) + chr(10))
    try:
        subprocess.run([CORE_CONSOLE, "/i", src, "/s", scr, "/l", "en-US"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=150)
    except subprocess.TimeoutExpired:
        pass
    if not os.path.exists(out):
        raise RuntimeError("DXF not written for " + dwg)
    shutil.copyfile(out, dxf)
    shutil.rmtree(work, ignore_errors=True)


def tag(e):
    if not e.xdata or "ELEVATOR_AUTOMATION" not in e.xdata:
        return None
    t = e.get_xdata("ELEVATOR_AUTOMATION")
    return t[0].value, t[1].value


def points(e):
    t = e.dxftype()
    if t == "LINE":
        return [(e.dxf.start.x, e.dxf.start.y), (e.dxf.end.x, e.dxf.end.y)]
    if t == "LWPOLYLINE":
        return [(p[0], p[1]) for p in e.get_points()]
    return None


def near(a, b):
    return abs(a - b) <= TOL


def box(pts):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return (round(min(xs), 2), round(max(xs), 2), round(min(ys), 2), round(max(ys), 2))


def same(got, want):
    """Multiset equality of tuples with the drawing tolerance."""
    got, want = sorted(got), sorted(want)
    return len(got) == len(want) and all(all(abs(a - b) <= TOL for a, b in zip(g, w)) for g, w in zip(got, want))


def run_case(folder, case_id):
    expected = json.load(open(os.path.join(folder, case_id + "-expected.json"), encoding="utf-8-sig"))
    layout, plan, kind = expected["Layout"], expected["Plan"], expected["Type"]
    dxf = os.path.join(folder, case_id + ".dxf")
    to_dxf(os.path.join(folder, case_id + ".dwg"), dxf)
    doc = ezdxf.readfile(dxf)
    msp = doc.modelspace()
    ents = {}
    for e in msp:
        t = tag(e)
        if t:
            ents[t[0]] = (e, t[1])
    datum = ents["Plan.Shaft.InnerOutline.EntranceDatum"][0]
    p = points(datum)
    ox, oy = p[1]
    sx = p[4][0] - ox
    check(near(sx, plan["SW"]), case_id + ": shaft width in the DWG is " + str(plan["SW"]))
    sw = plan["SW"]

    def local(pts):
        return [(x - ox, y - oy) for x, y in pts]

    mine = {k: v[0] for k, v in ents.items() if k.startswith(PREFIX)}
    ordered = sorted(mine)
    wall_keys = set(ordered[:WALL_PIECES])
    new = {k: e for k, e in mine.items() if k not in wall_keys}

    # ---- the manual door geometry is hidden, nothing of it is cloned, the new geometry is visible
    hidden = sorted(k for k, (e, r) in ents.items() if k.startswith("Plan.") and not k.startswith(PREFIX) and e.dxf.invisible == 1)
    expected_hidden = (["Plan.Door.Opening"] + ["Plan.CenterFront.Part%03d" % i for i in range(1, 14)] + ["Plan.Entrance.Part00%d" % i for i in range(2, 8)] + ["Plan.DoorLeft.Part00%d" % i for i in range(1, 5)]
                       + ["Plan.DoorRight.Part00%d" % i for i in range(1, 8)] + ["Plan.DoorSpan.Part00%d" % i for i in range(1, 4)]
                       + ["Plan.DoorLeader.Part001", "Plan.Cabin.Inner.Front", "Plan.Cabin.Part002", "Plan.Cabin.Part003", "Plan.Cabin.Part004"]
                       + ["Plan.Dimension.Part0%02d" % i for i in range(6, 14)]
                       + ["Plan.Wall.Part001", "Plan.Wall.Part004", "Plan.Wall.Part005", "Plan.Wall.Part006",
                          "Plan.Shaft.InnerOutline.EntranceDatum"])
    check(hidden == sorted(expected_hidden), case_id + ": hidden template entities (%d) equal the expected list; found %s" % (len(hidden), hidden))
    check(all(e.dxf.invisible == 0 for e in mine.values()), case_id + ": automatic door entities visible")
    check(not any(e.dxftype() == "MULTILEADER" for e in mine.values()), case_id + ": no door leader left")
    visible_door = [k for k, (e, r) in ents.items() if not k.startswith(PREFIX) and e.dxf.invisible == 0
                    and (k.startswith("Plan.DoorLeft") or k.startswith("Plan.DoorRight") or k.startswith("Plan.DoorSpan")
                         or k.startswith("Plan.DoorLeader") or k == "Plan.Door.Opening")]
    check(not visible_door, case_id + ": no manual door entity visible " + str(visible_door))

    # ---- the layout numbers
    o, ol, orr = layout["OpeningMm"], layout["OpeningLeftX"], layout["OpeningRightX"]
    lz, sg = layout["LandingZoneMm"], layout["SillGapMm"]
    cd = lz + sg
    front = plan["CarFront"]
    pw, t = layout["PanelWidthMm"], layout["PanelThicknessMm"]
    right = layout["StackingSide"] == "Right"
    jamb = plan["JambWidth"]
    car_left = (sw - plan["CarWidth"]) / 2
    car_right = car_left + plan["CarWidth"]
    wallx = 25.0

    def m(x):
        return x if right else sw - x

    def rc(x0, x1, d0, d1):
        a, b = sorted((x0, x1))
        return (round(a, 2), round(b, 2), round(d0, 2), round(d1, 2))

    def ru(u0, u1, d0, d1):
        return rc(m(u0), m(u1), d0, d1)

    def ln(x0, d0, x1, d1):
        a, b = sorted(((x0, d0), (x1, d1)))
        return (round(a[0], 2), round(a[1], 2), round(b[0], 2), round(b[1], 2))

    want_rect = {"Car": [], "0": [], "step&guides": []}
    want_line = {"0": [], "DL": [], "CL": []}

    def panel(r, outline):
        want_rect[outline].append(r)
        for k in (1, 2, 3):
            want_line["0"].append(ln(r[0], r[2] + 10 * k, r[1], r[2] + 10 * k))

    # jamb blocks in the wall opening (double line)
    jp = local(points(ents["Plan.DoorLeft.Part001"][0]))
    depth = abs(jp[2][1] - jp[0][1])
    for x0, x1 in ((ol - jamb, ol), (orr, orr + jamb)):
        want_rect["0"].append(rc(x0, x1, -depth, 0))
        want_rect["0"].append(rc(x0 + 3, x1 - 3, -depth + 3, -3))
    want_rect["step&guides"] += [rc(car_left - wallx, ol, front - 40, front), rc(orr, car_right + wallx, front - 40, front)]
    want_line["CL"] += [ln(ol, front - 40, ol, front), ln(orr, front - 40, orr, front)]
    want_line["DL"] += [ln(ol, 0, orr, 0), ln(ol, front - 40, orr, front - 40)]
    if kind == "Automatic2S":
        uf = ol if right else sw - orr
        us = uf + o
        ot = max(0.0, 2 * pw - o - layout["PanelOverlapMm"])
        ul = layout["LandingLeftX"] if right else sw - layout["LandingRightX"]
        ur = layout["LandingRightX"] if right else sw - layout["LandingLeftX"]
        want_rect["0"].append(ru(ul, uf, 0, lz))
        if uf - ul > 8:
            a, b = sorted((m(ul), m(uf)))
            want_rect["0"].append((round(a + 3, 2), round(b - 3, 2), 3.0, round(lz - 3, 2)))
        want_line["0"] += [ln(m(uf + pw), lz, m(ur), lz), ln(m(us + ot), 0, m(ur), 0), ln(m(ur), 0, m(ur), lz)]
        panel(ru(uf, uf + pw, lz - t, lz), "Car")
        panel(ru(us + ot - pw, us + ot, lz - 2 * t - 5, lz - t - 5), "Car")
        panel(ru(uf, uf + pw, cd, cd + t), "Car")
        panel(ru(us + ot - pw, us + ot, cd + t + 5, cd + 2 * t + 5), "Car")
        p1 = ur - 4
        p0 = p1 - 106
        if p0 > us + ot:
            for d in (lz - t, lz - 2 * t - 5, cd, cd + t + 5):
                want_rect["Car"].append(ru(p0, p1, d, d + t))
                a, b = sorted((m(p0), m(p1)))
                want_rect["Car"].append((round(a, 2), round(b - 1, 2), round(d + 1, 2), round(d + t - 1, 2)))
    else:
        ll, lr = layout["LandingLeftX"], layout["LandingRightX"]
        want_rect["0"] += [rc(ll, lr, lz - 70, lz), rc(ll, lr, cd, cd + 70)]
        dl, dc = lz - 5 - t, cd + 12
        for d in (dl, dc):
            panel(rc(ol, ol + pw, d, d + t), "0")
            panel(rc(orr - pw, orr, d, d + t), "0")
            e0, e1 = ll + 4, lr - 4
            for x0, x1 in ((e0, e0 + 141), (e1 - 141, e1)):
                want_rect["0"].append(rc(x0, x1, d, d + t))
                want_rect["0"].append((round(x0, 2), round(x1 - 1, 2), round(d + 1, 2), round(d + t - 1, 2)))

    got_rect = {"Car": [], "0": [], "step&guides": []}
    got_line = {"0": [], "DL": [], "CL": []}
    for k, e in new.items():
        ty = e.dxftype()
        if ty == "LWPOLYLINE" and e.closed and len(list(e.get_points())) == 4 and e.dxf.layer in got_rect:
            got_rect[e.dxf.layer].append(box(local(points(e))))
        elif ty == "LINE" and e.dxf.layer in got_line:
            pts = local(points(e))
            got_line[e.dxf.layer].append(ln(pts[0][0], pts[0][1], pts[1][0], pts[1][1]))
        elif ty != "DIMENSION":
            check(False, case_id + ": unexpected new entity " + ty + " on " + e.dxf.layer + " " + k)
    for layer in want_rect:
        check(same(got_rect[layer], want_rect[layer]),
              "%s: rectangles on %s: %d found, %d expected\n  found %s\n  want  %s" % (
                  case_id, layer, len(got_rect[layer]), len(want_rect[layer]), sorted(got_rect[layer]), sorted(want_rect[layer])))
    for layer in want_line:
        check(same(got_line[layer], want_line[layer]),
              "%s: lines on %s: %d found, %d expected\n  found %s\n  want  %s" % (
                  case_id, layer, len(got_line[layer]), len(want_line[layer]), sorted(got_line[layer]), sorted(want_line[layer])))

    # ---- the wall opening follows the door: inner outline with its opening at the opening edges +- jamb
    wall_pl = [local(points(e)) for k, e in mine.items() if k in wall_keys and e.dxf.layer == "Wall" and e.dxftype() == "LWPOLYLINE"
               and len(list(e.get_points())) == 6]
    inner = [w for w in wall_pl if abs(w[0][1]) < 0.01 and abs(w[2][1] - plan["SD"]) < 0.01]
    check(len(inner) == 1 and near(inner[0][0][0], ol - jamb) and near(inner[0][5][0], orr + jamb),
          case_id + ": shaft inner outline opening at the opening edges +- jamb")

    # ---- dimensions: measurements and texts
    dims = [e for k, e in new.items() if e.dxftype() == "DIMENSION"]
    meas = sorted(round(e.get_measurement(), 1) for e in dims)
    centre = (ol + orr) / 2
    want = [ol, o, sw - orr, o / 2, o / 2, o, ol - car_left, car_right - orr, lz, sg, front - cd]
    if kind == "Automatic2S" and abs(centre - sw / 2) > 0.05:
        want.append(abs(centre - sw / 2))
    want = sorted(round(v, 1) for v in want)
    check(len(meas) == len(want) and all(abs(a - b) <= 0.06 for a, b in zip(meas, want)), case_id + ": dimensions " + str(meas) + " vs " + str(want))
    texts = sorted(e.dxf.text for e in dims if e.dxf.text.strip())
    check(any("DOOR" in t_ and "X2000" in t_ for t_ in texts) and any("OPENING" in t_ for t_ in texts), case_id + ": DOOR <o>X2000 and OPENING texts")
    # the three zone dimensions sit in their own columns (no overlap) beside the template front-zone dimension
    zone = sorted(((round(e.dxf.defpoint.x - ox, 1), round(e.get_measurement(), 1)) for e in dims
                   if abs(e.dxf.get("angle", 0) - 90) < 0.01 and round(e.get_measurement(), 1) in (lz, sg, front - cd)))
    check(len(zone) == 3 and len({c for c, _ in zone}) == 3 and all(b[0] - a[0] >= 50 for a, b in zip(zone, zone[1:])),
          case_id + ": front-zone split dimensions in separate columns " + str(zone))

    # ---- plan PNG (hidden entities left out)
    png = os.path.join(folder, case_id + "-plan.png")

    def plan_only(e):
        t_ = tag(e)
        return t_ is not None and t_[0].startswith("Plan.") and e.dxf.invisible == 0

    mpl.qsave(msp, png, bg="#FFFFFF", dpi=170, filter_func=plan_only)
    print(case_id, "checked", len(new), "new entities,", len(hidden), "hidden; png", png)
    return {"Id": case_id, "AutomaticDoorEntities": len(mine), "HiddenTemplateEntities": hidden,
            "Rectangles": {k: len(v) for k, v in got_rect.items()}, "Lines": {k: len(v) for k, v in got_line.items()},
            "Dimensions": meas}


def main():
    folder = sys.argv[1]
    cases = sorted(f[:-len("-expected.json")] for f in os.listdir(folder) if f.endswith("-expected.json"))
    summary = [run_case(folder, c) for c in cases]
    result = {"Status": "PASS" if not failures else "FAIL", "Checks": checks, "Failures": failures, "Cases": summary}
    json.dump(result, open(os.path.join(folder, "verify-doors.json"), "w"), indent=2)
    print("PASS" if not failures else "FAIL", checks, "checks")
    return 0 if not failures else 1


sys.exit(main())
