"""D3 tier 3: the regenerated drawing of the user's case equals the user's own correction.

usage: verify_user_edit.py <user edit DWG> <generated DWG> <output folder>

The user generated "2S 700 on a 1320 x 1800 shaft" with the bin-try build and corrected the door plan by hand in AutoCAD (car door frame,
jamb post, the way the panels continue into the pocket, the arrangement of the dimensions). docs/design/doors-d3/ keeps that DWG; the
generated drawing of the same project must contain the same line work in the door area and the same dimensions.

Both DWGs are converted to DXF with the AutoCAD Core Console (read only) and compared with ezdxf:
* line work: every visible LINE / LWPOLYLINE of the door area as merged axis-parallel segments per layer (so a rectangle drawn as one
  polyline or four lines, and duplicate lines, compare equal): must be identical;
* dimensions: each of the user's dimensions has a generated one with the same measured interval and text, the dimension line within
  LINE_TOLERANCE mm (the user dragged the lines by hand: his rows differ from each other by 1.25 mm, and the generated rows are at the
  template spacing, 110 mm column pitch).
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile

import ezdxf

CORE_CONSOLE = r"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe"
TOL = 0.06
LINE_TOLERANCE = 15.0
WINDOW = (-420.0, -210.0, 1600.0, 260.0)


def to_dxf(dwg, dxf):
    work = tempfile.mkdtemp(prefix="doors_")
    src = os.path.join(work, "in.dwg")
    out = os.path.join(work, "out.dxf")
    shutil.copyfile(dwg, src)
    scr = os.path.join(work, "run.scr")
    with open(scr, "w", newline="") as f:
        f.write((chr(13) + chr(10)).join(["FILEDIA", "0", "DXFOUT", out, "16"]) + chr(13) + chr(10))
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
    return e.get_xdata("ELEVATOR_AUTOMATION")[0].value


def load(dxf):
    doc = ezdxf.readfile(dxf)
    msp = doc.modelspace()
    ox = oy = None
    for e in msp:
        if tag(e) == "Plan.Shaft.InnerOutline.EntranceDatum":
            pts = [(p[0], p[1]) for p in e.get_points()]
            ox, oy = pts[1]
    return msp, ox, oy


def line_work(msp, ox, oy):
    x0, y0, x1, y1 = WINDOW
    per = {}
    other = set()
    for e in msp:
        if e.dxf.invisible:
            continue
        t = e.dxftype()
        if t == "LINE":
            pts = [(e.dxf.start.x - ox, e.dxf.start.y - oy), (e.dxf.end.x - ox, e.dxf.end.y - oy)]
            segs = [(pts[0], pts[1])]
        elif t == "LWPOLYLINE":
            pts = [(p[0] - ox, p[1] - oy) for p in e.get_points()]
            segs = list(zip(pts, pts[1:]))
            if e.closed and len(pts) > 2:
                segs.append((pts[-1], pts[0]))
        else:
            continue
        for a, b in segs:
            if not (x0 <= min(a[0], b[0]) and max(a[0], b[0]) <= x1 and y0 <= min(a[1], b[1]) and max(a[1], b[1]) <= y1):
                continue
            if abs(a[0] - b[0]) <= TOL:
                key = (e.dxf.layer, "v", round(a[0], 1))
                lo, hi = sorted((a[1], b[1]))
            elif abs(a[1] - b[1]) <= TOL:
                key = (e.dxf.layer, "h", round(a[1], 1))
                lo, hi = sorted((a[0], b[0]))
            else:
                other.add((e.dxf.layer, round(a[0], 1), round(a[1], 1), round(b[0], 1), round(b[1], 1)))
                continue
            per.setdefault(key, []).append((lo, hi))
    out = {}
    for key, spans in per.items():
        spans.sort()
        merged = []
        for lo, hi in spans:
            if merged and lo <= merged[-1][1] + TOL:
                merged[-1][1] = max(merged[-1][1], hi)
            else:
                merged.append([lo, hi])
        out[key] = [(round(a, 1), round(b, 1)) for a, b in merged if b - a > TOL]
    return out, other


def dimensions(msp, ox, oy):
    res = []
    for e in msp:
        if e.dxftype() != "DIMENSION" or e.dxf.invisible:
            continue
        d = e.dxf
        p1 = (d.defpoint2.x - ox, d.defpoint2.y - oy)
        p2 = (d.defpoint3.x - ox, d.defpoint3.y - oy)
        line = (d.defpoint.x - ox, d.defpoint.y - oy)
        angle = d.get("angle", 0.0)
        vertical = abs(angle - 90.0) < 1 or abs(angle - 270.0) < 1
        span = (p1[1], p2[1]) if vertical else (p1[0], p2[0])
        pos = line[0] if vertical else line[1]
        if max(abs(span[0]), abs(span[1])) > 3000:
            continue
        if vertical and not (-600 <= pos <= 0):
            continue
        if not vertical and not (-600 <= pos <= 400):
            continue
        res.append({"vertical": vertical, "lo": round(min(span), 1), "hi": round(max(span), 1), "text": d.get("text", ""), "line": round(pos, 2)})
    return res


def main():
    user_dwg, new_dwg, out = sys.argv[1], sys.argv[2], sys.argv[3]
    os.makedirs(out, exist_ok=True)
    user_dxf = os.path.join(out, "user-edit.dxf")
    new_dxf = os.path.join(out, "generated-for-user-edit.dxf")
    to_dxf(user_dwg, user_dxf)
    to_dxf(new_dwg, new_dxf)
    um, uox, uoy = load(user_dxf)
    nm, nox, noy = load(new_dxf)
    problems = []
    uw, uo = line_work(um, uox, uoy)
    nw, no = line_work(nm, nox, noy)
    for key in sorted(set(uw) | set(nw)):
        if uw.get(key, []) != nw.get(key, []):
            problems.append("line work %s: user %s, generated %s" % (key, uw.get(key, []), nw.get(key, [])))
    if uo != no:
        problems.append("diagonal segments differ: user-only %s, generated-only %s" % (sorted(uo - no), sorted(no - uo)))
    ud, nd = dimensions(um, uox, uoy), dimensions(nm, nox, noy)
    report = []
    used = set()
    for u in ud:
        candidates = [(i, n) for i, n in enumerate(nd) if i not in used and n["vertical"] == u["vertical"]
                      and abs(n["lo"] - u["lo"]) < 0.6 and abs(n["hi"] - u["hi"]) < 0.6]
        candidates.sort(key=lambda c: (c[1]["text"] != u["text"], abs(c[1]["line"] - u["line"])))
        if not candidates:
            problems.append("user dimension %s %s..%s %r has no generated dimension" % ("V" if u["vertical"] else "H", u["lo"], u["hi"], u["text"]))
            continue
        i, n = candidates[0]
        used.add(i)
        delta = abs(n["line"] - u["line"])
        report.append({"vertical": u["vertical"], "from": u["lo"], "to": u["hi"], "text": u["text"], "user_line": u["line"], "generated_line": n["line"], "delta": round(delta, 2)})
        if n["text"] != u["text"]:
            problems.append("dimension %s..%s text %r vs %r" % (u["lo"], u["hi"], u["text"], n["text"]))
        if delta > LINE_TOLERANCE:
            problems.append("dimension %s..%s line differs by %.2f mm" % (u["lo"], u["hi"], delta))
    extra = [n for i, n in enumerate(nd) if i not in used]
    if extra:
        problems.append("generated dimensions the user's drawing does not have: " + str([(n["lo"], n["hi"], n["text"]) for n in extra]))
    result = {"Status": "PASS" if not problems else "FAIL", "Problems": problems, "LineWorkKeys": len(set(uw) | set(nw)), "Dimensions": report}
    json.dump(result, open(os.path.join(out, "verify-user-edit.json"), "w"), indent=2)
    for p in problems:
        print("FAIL:", p)
    print(result["Status"], "user edit:", len(set(uw) | set(nw)), "line work groups,", len(report), "dimensions matched")
    return 0 if not problems else 1


sys.exit(main())
