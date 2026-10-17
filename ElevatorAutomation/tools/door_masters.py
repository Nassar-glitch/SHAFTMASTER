"""G2: extracts the user's four master doors (docs/design/details/g2/source, PDF imports) into clean geometry for the program.

The masters are PDF imports: every line is a short polyline, dashed and centre lines are runs of short segments, hatches are strokes, texts
are glyph strokes and dimensions are lines and ticks. This script keeps only the door geometry of each view, merges dash runs into one line
(marked HIDDEN or CENTER, so the program draws a real linetype), drops hatch strokes, glyphs, dimensions, the opening arrows and the
section mark, chains the pieces into polylines and writes Templates/DoorMasters.json in mm relative to the opening centre (elevation) or
the section's landing edge (section), floor = 0. The program stretches and mirrors this geometry by the rules of DOORS-G2.md and draws
every dimension, text, hatch and mark natively.

Usage: python -I tools/door_masters.py <source dir> <out json>
"""
import json
import math
import os
import sys
from collections import defaultdict

import ezdxf

# view boxes (absolute coordinates of the source files): origin, include box (relative), exclusions (relative boxes)
DOORS = {
    "AUTO_GLASS": dict(file="Automaitc700-Glass&solid.dxf", elev=(799, 874), sect=(2463, 874), glass=True),
    "AUTO_SOLID": dict(file="Automaitc700-Glass&solid.dxf", elev=(6430, 913), sect=(8094, 913), glass=False),
    "SWING_GLASS": dict(file="Swing-Glass&Window.dxf", elev=(812, 5415), sect=(2476, 4799 + 616), glass=True),
    "SWING_WINDOW": dict(file="Swing-Glass&Window.dxf", elev=(7069, 5413), sect=(8732, 4798 + 615), glass=False),
}
ELEV_BOX = (-536, -2, 536, 2186)
SECT_BOX = (-6, -640, 700, 2700)


def segments(entities):
    for e in entities:
        t = e.dxftype()
        if t == "LWPOLYLINE":
            p = [(v[0], v[1]) for v in e.vertices()]
            if e.closed and len(p) > 2:
                p.append(p[0])
        elif t == "LINE":
            p = [(e.dxf.start.x, e.dxf.start.y), (e.dxf.end.x, e.dxf.end.y)]
        else:
            continue
        for a, b in zip(p, p[1:]):
            if math.hypot(b[0] - a[0], b[1] - a[1]) > 0.3:
                yield a, b


def inside(pt, box, tol=0.0):
    return box[0] - tol <= pt[0] <= box[2] + tol and box[1] - tol <= pt[1] <= box[3] + tol


def rel(seg, o):
    return tuple((round(p[0] - o[0], 1), round(p[1] - o[1], 1)) for p in seg)


def key(seg):
    a, b = sorted(seg)
    return (round(a[0]), round(a[1]), round(b[0]), round(b[1]))


def collect(doc, origin, box, drop):
    out, seen = [], set()
    for s in segments(doc.modelspace()):
        r = rel(s, origin)
        if not all(inside(p, box) for p in r):
            continue
        if any(f(r) for f in drop):
            continue
        k = key(r)
        if k in seen:
            continue
        seen.add(k)
        out.append(r)
    return out


def merge_runs(segs):
    """Merges collinear pieces of horizontal / vertical lines: touching pieces into one continuous line, a run of separate short pieces
    (a PDF dash pattern) into one HIDDEN line, or CENTER when the run mixes dots and long dashes. Other segments pass unchanged."""
    groups = defaultdict(list)
    rest = []
    for a, b in segs:
        if abs(a[1] - b[1]) < 0.6:
            groups[("H", round((a[1] + b[1]) / 2))].append((min(a[0], b[0]), max(a[0], b[0])))
        elif abs(a[0] - b[0]) < 0.6:
            groups[("V", round((a[0] + b[0]) / 2))].append((min(a[1], b[1]), max(a[1], b[1])))
        else:
            rest.append(((a, b), "CONTINUOUS"))
    lines = []
    for (o, c), iv in groups.items():
        iv.sort()
        runs = [[iv[0][0], iv[0][1], [iv[0][1] - iv[0][0]]]]
        for s, e in iv[1:]:
            cur = runs[-1]
            if s <= cur[1] + 0.8:
                cur[1] = max(cur[1], e)
                cur[2][-1] = cur[1] - cur[0] if len(cur[2]) == 1 else cur[2][-1] + max(0, e - cur[1])
            else:
                runs.append([s, e, [e - s]])
        # dash runs: consecutive short pieces separated by short gaps
        merged, i = [], 0
        while i < len(runs):
            j = i
            while j + 1 < len(runs) and runs[j + 1][0] - runs[j][1] < 40 and runs[j][1] - runs[j][0] < 160 and runs[j + 1][1] - runs[j + 1][0] < 160:
                j += 1
            if j > i:
                pieces = [r[1] - r[0] for r in runs[i:j + 1]]
                lt = "CENTER" if min(pieces) < 15 and max(pieces) > 25 else "HIDDEN"
                merged.append((runs[i][0], runs[j][1], lt))
            else:
                merged.append((runs[i][0], runs[i][1], "CONTINUOUS"))
            i = j + 1
        for s, e, lt in merged:
            if e - s < 0.5:
                continue
            seg = ((s, c), (e, c)) if o == "H" else ((c, s), (c, e))
            lines.append((seg, lt))
    return lines + rest


def chain(lines):
    """Chains continuous segments sharing end points into polylines (dashed / centre lines stay single)."""
    cont = [s for s, lt in lines if lt == "CONTINUOUS"]
    other = [{"lt": lt, "pts": [list(s[0]), list(s[1])]} for s, lt in lines if lt != "CONTINUOUS"]
    pt = lambda p: (round(p[0], 0), round(p[1], 0))
    ends = defaultdict(list)
    for i, (a, b) in enumerate(cont):
        ends[pt(a)].append(i)
        ends[pt(b)].append(i)
    used = [False] * len(cont)
    polys = []
    for i in range(len(cont)):
        if used[i]:
            continue
        used[i] = True
        path = [cont[i][0], cont[i][1]]
        for forward in (True, False):
            while True:
                tip = path[-1] if forward else path[0]
                nxt = [j for j in ends[pt(tip)] if not used[j]]
                if len(ends[pt(tip)]) != 2 or not nxt:
                    break
                j = nxt[0]
                used[j] = True
                a, b = cont[j]
                p = b if pt(a) == pt(tip) else a
                if forward:
                    path.append(p)
                else:
                    path.insert(0, p)
        closed = len(path) > 3 and pt(path[0]) == pt(path[-1])
        if closed:
            path = path[:-1]
        polys.append({"lt": "CONTINUOUS", "closed": closed, "pts": [[round(x, 1), round(y, 1)] for x, y in path]})
    return polys + other


def hatches(doc, origin, box, pattern):
    out = []
    for h in doc.modelspace().query("HATCH"):
        if h.dxf.pattern_name != pattern:
            continue
        for path in h.paths:
            pts = []
            if hasattr(path, "vertices"):
                pts = [(v[0], v[1]) for v in path.vertices]
            else:
                for edge in path.edges:
                    if edge.EDGE_TYPE == "LineEdge":
                        pts.append((edge.start[0], edge.start[1]))
            r = [(round(x - origin[0], 1), round(y - origin[1], 1)) for x, y in pts]
            if r and all(inside(p, box) for p in r):
                k = tuple(sorted((round(x), round(y)) for x, y in r))
                if k not in [tuple(sorted((round(x), round(y)) for x, y in o)) for o in out]:
                    out.append(r)
    return [[list(p) for p in r] for r in out]


def circles(doc, origin, box):
    out = []
    for c in doc.modelspace().query("CIRCLE"):
        p = (round(c.dxf.center.x - origin[0], 1), round(c.dxf.center.y - origin[1], 1), round(c.dxf.radius, 1))
        if inside(p, box) and p not in out:
            out.append(p)
    return [list(p) for p in out]


def is_hatch_stroke(s):
    (a, b) = s
    dx, dy = b[0] - a[0], b[1] - a[1]
    return abs(dx) > 1 and abs(dy) > 1 and abs(abs(dx) - abs(dy)) < 2.5


def run(src, out):
    docs = {}
    result = {"source": "docs/design/details/g2/source", "openingMm": 800, "heightMm": 2000, "doors": {}}
    for name, d in DOORS.items():
        doc = docs.setdefault(d["file"], ezdxf.readfile(os.path.join(src, d["file"])))
        auto = name.startswith("AUTO")
        # elevation: drop the centre line pieces on the axis (rebuilt as CENTER), the opening arrows (rebuilt per type and side) and the
        # leader of INDICATOR
        def elev_drop(s, auto=auto):
            (a, b) = s
            xs, ys = (a[0], b[0]), (a[1], b[1])
            if auto and all(780 < y < 900 for y in ys) and all(abs(x) < 400 for x in xs):
                return True     # arrows
            if all(abs(x) < 0.8 for x in xs) and abs(ys[0] - ys[1]) < 160 and not (auto and abs(ys[0] - ys[1]) > 1999):
                return True     # axis dashes (the panel joint of an automatic door is one 2000 line)
            if not (abs(a[1] - b[1]) < 0.6 or abs(a[0] - b[0]) < 0.6) and min(ys) < 1100 and max(xs) > 440:
                return True     # INDICATOR leader
            return False

        # section: drop the dimension band (dimension line, its ticks and extension lines, the vertical texts), hatch strokes and the
        # "60" dimension at the right
        def sect_drop(s):
            (a, b) = s
            xs = (a[0], b[0])
            if is_hatch_stroke(s):
                return True
            if all(-12 < x < 62 for x in xs) and not all(abs(x + 3) < 1.5 for x in xs):
                return True     # dimension line, ticks and the vertical texts (the slab break line at x -3 stays)
            if abs(a[1] - b[1]) < 0.6 and min(xs) > 25 and min(xs) < 40:
                return True     # extension lines starting at the dimension band
            ys = (a[1], b[1])
            glyph = min(xs) > 555 and all(15 < y < 220 for y in ys)
            if glyph or min(xs) > 640 or (abs(a[1] - b[1]) < 0.6 and max(xs) > 640):
                return True     # the "60" text and its extension lines
            return False

        elev = chain(merge_runs(collect(doc, d["elev"], ELEV_BOX, [elev_drop])))
        sect = chain(merge_runs(collect(doc, d["sect"], SECT_BOX, [sect_drop])))
        result["doors"][name] = {
            "elevation": {
                "lines": elev,
                "circles": circles(doc, d["elev"], ELEV_BOX),
                "glass": hatches(doc, d["elev"], ELEV_BOX, "GOST_GLASS"),
                "solids": [s for s in hatches(doc, d["elev"], ELEV_BOX, "SOLID") if not (auto and all(780 < p[1] < 900 for p in s))],
            },
            "section": {
                "lines": sect,
                "circles": circles(doc, d["sect"], SECT_BOX),
                "solids": hatches(doc, d["sect"], SECT_BOX, "SOLID"),
            },
        }
    with open(out, "w", encoding="utf-8") as f:
        json.dump(result, f, indent=1)
    for n, v in result["doors"].items():
        print(n, "elevation", len(v["elevation"]["lines"]), "lines", len(v["elevation"]["circles"]), "circles", len(v["elevation"]["glass"]),
              "glass", len(v["elevation"]["solids"]), "solids; section", len(v["section"]["lines"]), "lines")


if __name__ == "__main__":
    run(sys.argv[1], sys.argv[2])
