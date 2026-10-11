"""D2 / D3 tier 3: reads the automatic door geometry back from the generated DWGs and compares it with the DoorLayout numbers.

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
                       + ["Plan.Dimension.Part0%02d" % i for i in range(6, 14)] + ["Plan.Dimension.Part020", "Plan.Dimension.Part031"]
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
    want_open = []
    cd_end = front - 40

    def panel(r, outline):
        want_rect[outline].append(r)
        for k in (1, 2, 3):
            want_line["0"].append(ln(r[0], r[2] + 10 * k, r[1], r[2] + 10 * k))

    def band(x_closed, x_end, d0, d1, layer):
        """The band of a panel row (D3): open outline with 1 mm stubs at the closed end and an open 1 mm inset line."""
        if abs(x_end - x_closed) < 2:
            return
        inner = x_end + (-1 if x_closed < x_end else 1)
        want_open.append((layer, ((x_closed, d0 + 1), (x_closed, d0), (x_end, d0), (x_end, d1), (x_closed, d1), (x_closed, d1 - 1))))
        want_open.append((layer, ((x_closed, d0 + 1), (inner, d0 + 1), (inner, d1 - 1), (x_closed, d1 - 1))))

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
        # frames: one rectangle each for the landing door and the car door, from the fixed-side jamb to the end of the pocket
        want_rect["0"] += [ru(ul, ur, 0, lz), ru(ul, ur, cd, cd_end)]
        # fixed-side jamb posts in both frames, inset line closed at the opening edge
        if uf - ul > 0.5:
            for d0, d1 in ((0, lz), (cd, cd_end)):
                want_rect["0"].append(ru(ul, uf, d0, d1))
                if uf - ul > 6:
                    inset = m(ul) + (3 if m(ul) < m(uf) else -3)
                    a_, b_ = sorted((inset, m(uf)))
                    want_rect["0"].append((round(a_, 2), round(b_, 2), round(d0 + 3, 2), round(d1 - 3, 2)))
        panel(ru(uf, uf + pw, lz - t, lz), "Car")
        panel(ru(us + ot - pw, us + ot, lz - 2 * t - 5, lz - t - 5), "Car")
        panel(ru(uf, uf + pw, cd, cd + t), "Car")
        panel(ru(us + ot - pw, us + ot, cd + t + 5, cd + 2 * t + 5), "Car")
        end = ur - 4
        band(m(uf + pw), m(end), lz - t, lz, "Car")
        band(m(us + ot), m(end), lz - 2 * t - 5, lz - t - 5, "Car")
        band(m(uf + pw), m(end), cd, cd + t, "Car")
        band(m(us + ot), m(end), cd + t + 5, cd + 2 * t + 5, "Car")
    else:
        ll, lr = layout["LandingLeftX"], layout["LandingRightX"]
        want_rect["0"] += [rc(ll, lr, 0, lz), rc(ll, lr, cd, cd_end)]
        dl = lz - min(5.0, max(0.0, (lz - t) / 2)) - t
        dc = cd + min(12.0, max(0.0, (cd_end - cd - t) / 2))
        e0, e1 = ll + 4, lr - 4
        for d in (dl, dc):
            panel(rc(ol, ol + pw, d, d + t), "0")
            panel(rc(orr - pw, orr, d, d + t), "0")
            band(ol, e0, d, d + t, "0")
            band(orr, e1, d, d + t, "0")

    got_rect = {"Car": [], "0": [], "step&guides": []}
    got_line = {"0": [], "DL": [], "CL": []}
    got_open = []
    for k, e in new.items():
        ty = e.dxftype()
        if ty == "LWPOLYLINE" and e.closed and len(list(e.get_points())) == 4 and e.dxf.layer in got_rect:
            got_rect[e.dxf.layer].append(box(local(points(e))))
        elif ty == "LWPOLYLINE" and not e.closed:
            got_open.append((e.dxf.layer, tuple(local(points(e)))))
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
    got_open_s = sorted((lay, tuple(round(c, 2) for pt in v for c in pt)) for lay, v in got_open)
    want_open_s = sorted((lay, tuple(round(c, 2) for pt in v for c in pt)) for lay, v in want_open)
    check(len(got_open_s) == len(want_open_s) and all(g[0] == w[0] and len(g[1]) == len(w[1]) and all(abs(x - y) <= TOL for x, y in zip(g[1], w[1]))
                                                        for g, w in zip(got_open_s, want_open_s)),
          "%s: panel bands (open polylines): %d found, %d expected\n  found %s\n  want  %s" % (case_id, len(got_open_s), len(want_open_s), got_open_s, want_open_s))
    # every panel sits inside the frame of its door: the panel rectangles are within the frame rectangles (D3, DESIGN-FLOW principle 8)
    frames = [r for r in want_rect["0"] if abs(r[3] - r[2] - lz) < 0.01 and abs(r[2]) < 0.01] + [r for r in want_rect["0"] if abs(r[2] - cd) < 0.01 and abs(r[3] - cd_end) < 0.01]
    panels = [r for r in want_rect["Car"]] + ([r for r in want_rect["0"] if kind == "Automatic2C" and r[3] - r[2] <= t + 0.01])
    inside = all(any(f_[0] - 0.01 <= r[0] and r[1] <= f_[1] + 0.01 and f_[2] - 0.01 <= r[2] and r[3] <= f_[3] + 0.01 for f_ in frames) for r in panels)
    check(len(frames) >= 2 and inside, case_id + ": every panel is inside its door frame (floating panels: " + str([r for r in panels if not any(f_[0] - 0.01 <= r[0] and r[1] <= f_[1] + 0.01 and f_[2] - 0.01 <= r[2] and r[3] <= f_[3] + 0.01 for f_ in frames)]) + ")")

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
    rear = plan["CarRear"]
    want = [ol, o, sw - orr, o / 2, o / 2, o, ol - car_left, car_right - orr, lz, sg, front - cd, front, rear - front]
    if kind == "Automatic2S" and abs(centre - sw / 2) > 0.05:
        want.append(abs(centre - sw / 2))
    want = sorted(round(v, 1) for v in want)
    check(len(meas) == len(want) and all(abs(a - b) <= 0.06 for a, b in zip(meas, want)), case_id + ": dimensions " + str(meas) + " vs " + str(want))
    texts = sorted(e.dxf.text for e in dims if e.dxf.text.strip())
    check(any("DOOR" in t_ and "X2000" in t_ for t_ in texts) and any("OPENING" in t_ for t_ in texts), case_id + ": DOOR <o>X2000 and OPENING texts")

    def vertical(e):
        return abs(e.dxf.get("angle", 0) - 90) < 0.01

    def line_of(e):
        return round((e.dxf.defpoint.x - ox) if vertical(e) else (e.dxf.defpoint.y - oy), 2)

    def m_(e):
        return round(e.get_measurement(), 1)

    # DESIGN-FLOW principle 8: chains on one dimension line, equal spacing of the rows and columns
    zone = [e for e in dims if vertical(e) and m_(e) in (round(lz, 1), round(sg, 1), round(front - cd, 1))]
    total = [e for e in dims if vertical(e) and m_(e) == round(front, 1)]
    depth_d = [e for e in dims if vertical(e) and m_(e) == round(rear - front, 1)]
    check(len(zone) == 3 and len({line_of(e) for e in zone}) == 1, case_id + ": front-zone split on ONE dimension line " + str(sorted(line_of(e) for e in zone)))
    check(len(total) == 1 and len(depth_d) == 1 and line_of(total[0]) == line_of(depth_d[0]),
          case_id + ": zone total and CAR DEPTH on one dimension line")
    if zone and total:
        check(abs(abs(line_of(total[0]) - line_of(zone[0])) - 110) < 0.1, case_id + ": zone chain one column pitch (110) from the total")
    above = [e for e in dims if not vertical(e) and line_of(e) > front]
    check(len(above) == 3 and len({line_of(e) for e in above}) == 1, case_id + ": car returns and opening on ONE dimension line")
    below = [e for e in dims if not vertical(e) and line_of(e) < 0]
    rows = sorted({line_of(e) for e in below}, reverse=True)
    check(len(rows) in (2, 3) and all(abs((rows[i] - rows[i + 1]) - (rows[0] - rows[1])) < 0.1 for i in range(len(rows) - 1)),
          case_id + ": rows below the wall at equal spacing " + str(rows))
    # no dimension text overlaps another: text boxes from the dimension blocks (0.7 x height per character, the placement rule)
    boxes = []
    for e in dims:
        blk = doc.blocks.get(e.dxf.geometry)
        for sub in blk:
            if sub.dxftype() != "MTEXT":
                continue
            h_ = sub.dxf.char_height
            length = len(sub.text) * 0.7 * h_
            rot = round(sub.get_rotation()) % 180
            cx, cy = sub.dxf.insert.x - ox, sub.dxf.insert.y - oy
            half_x, half_y = (length / 2, h_ / 2) if rot == 0 else (h_ / 2, length / 2)
            boxes.append((cx - half_x, cx + half_x, cy - half_y, cy + half_y, sub.text))
    overlaps = [(a_[4], b_[4]) for i, a_ in enumerate(boxes) for b_ in boxes[i + 1:]
                if a_[0] < b_[1] - 0.5 and b_[0] < a_[1] - 0.5 and a_[2] < b_[3] - 0.5 and b_[2] < a_[3] - 0.5]
    check(not overlaps, case_id + ": dimension texts overlap " + str(overlaps))

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
