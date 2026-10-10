"""D2 tier 3: reads the automatic door geometry back from the generated DWGs and compares it with the DoorLayout numbers.

usage: verify_doors.py <output folder>   (the folder DoorsNativeTests wrote: <id>.dwg and <id>-expected.json)

For each case: DWG -> DXF with the AutoCAD Core Console (read only), then ezdxf. The plan is rendered to <id>-plan.png (only the entities
of the plan view, hidden template geometry left out) for the visual comparison with the reference drawings.
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
        f.write(chr(13).join([]) + (chr(13) + chr(10)).join(lines) + chr(13) + chr(10))
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
    # ---- entity counts
    wall, jambs, dims = 5, 15, 11
    if kind == "Automatic2S":
        geometry = 3 + 4 + 2 + 2
    else:
        geometry = 2 + 4 + 2 + 2
    expected_count = wall + jambs + geometry + dims
    check(len(mine) == expected_count, "%s: %d automatic door entities (expected %d)" % (case_id, len(mine), expected_count))
    # ---- the manual door geometry is hidden, the new one visible
    hidden = [k for k, (e, r) in ents.items() if not k.startswith(PREFIX) and e.dxf.invisible == 1]
    check(len(hidden) >= 25, case_id + ": template door geometry hidden (%d entities)" % len(hidden))
    check(all(e.dxf.invisible == 0 for e in mine.values()), case_id + ": automatic door entities visible")
    # ---- rectangles by layer
    rects = {}
    for k, e in mine.items():
        if e.dxftype() == "LWPOLYLINE" and e.closed and len(list(e.get_points())) == 4 and e.dxf.layer in ("Car", "0", "step&guides"):
            rects.setdefault(e.dxf.layer, []).append(box(local(points(e))))
    o, ol, orr = layout["OpeningMm"], layout["OpeningLeftX"], layout["OpeningRightX"]
    lz, sg, cz = layout["LandingZoneMm"], layout["SillGapMm"], layout["CarZoneMm"]
    cd = lz + sg
    front = plan["CarFront"]
    pw, t = layout["PanelWidthMm"], layout["PanelThicknessMm"]
    right = layout["StackingSide"] == "Right"

    def m(x):
        return x if right else sw - x

    def rc(u0, u1, d0, d1):
        a, b = sorted((m(u0), m(u1)))
        return (round(a, 2), round(b, 2), round(d0, 2), round(d1, 2))

    car_left = (plan["SW"] - plan["CarWidth"]) / 2
    car_right = car_left + plan["CarWidth"]
    wallx = 25.0
    want_returns = [(round(car_left - wallx, 2), round(ol, 2), round(front - 40, 2), round(front, 2)),
                    (round(orr, 2), round(car_right + wallx, 2), round(front - 40, 2), round(front, 2))]
    got = sorted(rects.get("step&guides", []))
    check(len(got) == 2 and all(any(all(abs(a - b) <= TOL for a, b in zip(g, w)) for w in want_returns) for g in got),
          case_id + ": car front returns " + str(got) + " vs " + str(want_returns))
    if kind == "Automatic2S":
        uf = ol if right else sw - orr
        us = uf + o
        ot = max(0.0, 2 * pw - o - layout["PanelOverlapMm"])
        want_car = sorted([rc(uf, uf + pw, cd, cd + t), rc(us + ot - pw, us + ot, cd + t + 5, cd + 2 * t + 5),
                           rc(uf, uf + pw, lz - t, lz), rc(us + ot - pw, us + ot, lz - 2 * t - 5, lz - t - 5)])
        ul = layout["LandingLeftX"] if right else sw - layout["LandingRightX"]
        ur = layout["LandingRightX"] if right else sw - layout["LandingLeftX"]
        want_frame = sorted([rc(ul, uf, 0, lz), rc(us, ur, 0, lz)])
    else:
        dl = lz - 5 - t
        dc = cd + 12
        want_car = sorted([(round(ol, 2), round(ol + pw, 2), round(dl, 2), round(dl + t, 2)),
                           (round(orr - pw, 2), round(orr, 2), round(dl, 2), round(dl + t, 2)),
                           (round(ol, 2), round(ol + pw, 2), round(dc, 2), round(dc + t, 2)),
                           (round(orr - pw, 2), round(orr, 2), round(dc, 2), round(dc + t, 2))])
        ll, lr = layout["LandingLeftX"], layout["LandingRightX"]
        want_frame = sorted([(round(ll, 2), round(lr, 2), round(lz - 70, 2), round(lz, 2)),
                             (round(ll, 2), round(lr, 2), round(cd, 2), round(cd + 70, 2))])
    got_car = sorted(rects.get("Car", []))
    check(len(got_car) == 4 and all(all(abs(a - b) <= TOL for a, b in zip(g, w)) for g, w in zip(got_car, want_car)),
          case_id + ": door panels " + str(got_car) + " vs " + str(want_car))
    got_frame = sorted(rects.get("0", []))
    jamb_boxes = [g for g in got_frame if g[3] <= 0.01]
    frame_boxes = [g for g in got_frame if g[3] > 0.01]
    check(len(frame_boxes) == len(want_frame) and all(all(abs(a - b) <= TOL for a, b in zip(g, w)) for g, w in zip(frame_boxes, want_frame)),
          case_id + ": landing frame " + str(frame_boxes) + " vs " + str(want_frame))
    # ---- the wall opening follows the door: jamb blocks end at the opening edges, the wall polyline has its opening at +-jamb
    jamb = plan["JambWidth"]
    wall_pl = [local(points(e)) for k, e in mine.items() if e.dxf.layer == "Wall" and e.dxftype() == "LWPOLYLINE" and len(list(e.get_points())) == 6]
    inner = [w for w in wall_pl if abs(w[0][1]) < 0.01 and abs(w[2][1] - plan["SD"]) < 0.01]
    check(len(inner) == 1 and near(inner[0][0][0], ol - jamb) and near(inner[0][5][0], orr + jamb),
          case_id + ": shaft inner outline opening at the opening edges +- jamb")
    # ---- sill lines
    sills = sorted(box(local(points(e))) for k, e in mine.items() if e.dxftype() == "LINE" and e.dxf.layer == "DL")
    want_sills = sorted([(round(ol, 2), round(orr, 2), 0.0, 0.0), (round(ol, 2), round(orr, 2), round(front - 40, 2), round(front - 40, 2))])
    check(len(sills) == 2 and all(all(abs(a - b) <= TOL for a, b in zip(g, w)) for g, w in zip(sills, want_sills)),
          case_id + ": landing and car sill lines " + str(sills))
    # ---- dimensions: measurements
    meas = sorted(round(e.get_measurement(), 1) for k, e in mine.items() if e.dxftype() == "DIMENSION")
    want = [ol - 0, o, sw - orr, o / 2, o / 2, o, ol - car_left, car_right - orr, lz, sg, front - cd]
    want = sorted(round(v, 1) for v in want)
    check(len(meas) == 11 and all(abs(a - b) <= 0.06 for a, b in zip(meas, want)), case_id + ": dimensions " + str(meas) + " vs " + str(want))
    texts = sorted(e.dxf.text for k, e in mine.items() if e.dxftype() == "DIMENSION" and e.dxf.text.strip())
    check(any("DOOR" in t_ and "X2000" in t_ for t_ in texts) and any("OPENING" in t_ for t_ in texts), case_id + ": DOOR <o>X2000 and OPENING texts")
    # ---- plan PNG (hidden entities left out)
    png = os.path.join(folder, case_id + "-plan.png")

    def plan_only(e):
        t_ = tag(e)
        return t_ is not None and t_[0].startswith("Plan.") and e.dxf.invisible == 0

    mpl.qsave(msp, png, bg="#FFFFFF", dpi=170, filter_func=plan_only)
    print(case_id, "ok" if not failures else "see failures", "png", png)
    return {"Id": case_id, "AutomaticDoorEntities": len(mine), "Hidden": len(hidden), "Panels": got_car, "Frame": frame_boxes, "Dimensions": meas}


def main():
    folder = sys.argv[1]
    cases = sorted(f[:-len("-expected.json")] for f in os.listdir(folder) if f.endswith("-expected.json"))
    summary = [run_case(folder, c) for c in cases]
    result = {"Status": "PASS" if not failures else "FAIL", "Checks": checks, "Failures": failures, "Cases": summary}
    json.dump(result, open(os.path.join(folder, "verify-doors.json"), "w"), indent=2)
    print("PASS" if not failures else "FAIL", checks, "checks")
    return 0 if not failures else 1


sys.exit(main())
