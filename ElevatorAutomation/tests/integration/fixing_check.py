"""G1 round 3b: the main plan and the rail details agree on how each rail group is fixed.

The detail blocks are named SM_RAIL_BRACKET_PLAN_<key>_<CAR|CW>_<WALL|ARM>_G<gap>; the program's own plan symbol (drawn when the
template plan does not show the fixing of the detail) SM_RAIL_FIXING_PLAN_<key>_<CAR|CW>_<WALL|ARM>_G<gap>_SYMBOL. The template plan
draws the car brackets as wall brackets and the counterweight brackets on the rear wall (a cross arm): a group without a symbol must be
fixed that way in the detail, a group with a symbol must have the detail's type and gap.
"""
import re

DETAIL = re.compile(r"^SM_RAIL_BRACKET_PLAN_.*_(CAR|CW)_(WALL|ARM)_G(\d+)_L(\d+)$")
SYMBOL = re.compile(r"^SM_RAIL_FIXING_PLAN_.*_(CAR|CW)_(WALL|ARM)_G(\d+)_L(\d+)_SYMBOL$")
SECTION = re.compile(r"^SM_RAIL_BRACKET_SECTION_.*_(CAR|CW)_(WALL|ARM)_G(\d+)_L(\d+)_[A-Z]+$")
TEMPLATE = {"CAR": "WALL", "CW": "ARM"}


def check_fixing(doc, case, check):
    names = [e.dxf.name for e in doc.modelspace() if e.dxftype() == "INSERT"]
    detail, symbol, section = {}, {}, {}
    for n in names:
        for rx, store in ((DETAIL, detail), (SYMBOL, symbol), (SECTION, section)):
            m = rx.match(n)
            if m:
                store[m.group(1)] = (m.group(2), int(m.group(3)), int(m.group(4)))
    check(set(detail) == {"CAR", "CW"}, case + ": the details draw the fixing of the car and the counterweight rails " + str(sorted(detail)))
    for fam, (typ, gap, length) in sorted(detail.items()):
        check(section.get(fam) == (typ, gap, length), case + ": VP6 of the " + fam + " rails equals VP5 " + str((typ, gap, length)) + " " + str(section.get(fam)))
        if fam in symbol:
            check(symbol[fam] == (typ, gap, length), case + ": the plan symbol of the " + fam + " rails equals the detail " + str((typ, gap, length)) + " " + str(symbol[fam]))
        else:
            check(TEMPLATE[fam] == typ, case + ": the template plan draws the " + fam + " rails as " + TEMPLATE[fam] + ", the detail is " + typ)


def _boxes_and_leaders(doc):
    """Text boxes (multileader texts, dimension texts, detail texts) and leader segments of the rail details (x < 960000 or the VP5 strip)."""
    from ezdxf import bbox
    boxes, leaders = [], []
    for e in doc.modelspace():
        t = e.dxftype()
        if t not in ("MULTILEADER", "DIMENSION", "MTEXT") or e.dxf.get("invisible", 0):
            continue
        try:
            parts = list(e.virtual_entities()) if t != "MTEXT" else [e]
        except Exception:
            continue
        for v in parts:
            vt = v.dxftype()
            try:
                b = bbox.extents([v], fast=True)
            except Exception:
                continue
            if not b.has_data:
                continue
            if not (940000 < b.extmin.x < 1007000):
                continue
            if vt in ("MTEXT", "TEXT"):
                boxes.append((b.extmin.x, b.extmin.y, b.extmax.x, b.extmax.y, (v.plain_text() if vt == "MTEXT" else v.dxf.text)[:20]))
            elif t == "MULTILEADER" and vt in ("LINE", "LWPOLYLINE", "POLYLINE"):
                pts = [(p[0], p[1]) for p in ([v.dxf.start, v.dxf.end] if vt == "LINE" else (list(v.vertices_in_wcs()) if vt == "LWPOLYLINE" else [x.dxf.location for x in v.vertices]))]
                for i in range(len(pts) - 1):
                    leaders.append((pts[i], pts[i + 1], id(e)))
    return boxes, leaders


def _cross(a, b, c, d):
    def o(p, q, r):
        return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])
    return o(a, b, c) * o(a, b, d) < 0 and o(c, d, a) * o(c, d, b) < 0


def check_detail_overlap(doc, case, check):
    boxes, leaders = _boxes_and_leaders(doc)
    bad = []
    for i in range(len(boxes)):
        for j in range(i + 1, len(boxes)):
            a, b = boxes[i], boxes[j]
            if a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]:
                bad.append((a[4], b[4]))
    check(not bad, case + ": no two texts of the rail details overlap " + str(bad[:4]))
    crossing = []
    for i in range(len(leaders)):
        for j in range(i + 1, len(leaders)):
            if leaders[i][2] != leaders[j][2] and _cross(leaders[i][0], leaders[i][1], leaders[j][0], leaders[j][1]):
                crossing.append(i)
    check(not crossing, case + ": no two leaders of the rail details cross (" + str(len(crossing)) + ")")
