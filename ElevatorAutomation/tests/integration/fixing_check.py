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


DOOR = re.compile(r"^SM_DOOR_(2S|2C|SWING)_(SOLID|GLASS|WINDOW)_W(\d+)_H(\d+)_S(\d+)")


def check_door(doc, case, check):
    """G2: the door detail and the plan agree: the elevation block's opening, height and structural opening equal the plan's OPENING,
    DOOR WxH and wall opening (the jambs either side of OPENING)."""
    names = [e.dxf.name for e in doc.modelspace() if e.dxftype() == "INSERT" and DOOR.match(e.dxf.name)]
    check(len(names) == 1, case + ": the door detail is generated (" + str(names) + ")")
    if not names:
        return
    m = DOOR.match(names[0])
    w, h, strw = int(m.group(3)), int(m.group(4)), int(m.group(5))
    opening = door_text = None
    jambs = {}
    for e in doc.modelspace():
        if e.dxftype() != "DIMENSION":
            continue
        try:
            x = [v.value for v in e.get_xdata("ELEVATOR_AUTOMATION")]
        except Exception:
            continue
        if x and x[0] == "Plan.Dimension.Part010":
            opening = e.get_measurement()
        if x and x[0] == "Plan.Dimension.Part008":
            door_text = e.dxf.get("text", "")
        if x and x[0] in ("Plan.Dimension.Part009", "Plan.Dimension.Part011"):
            jambs[x[0]] = e.get_measurement()
    check(opening is not None and abs(opening - w) < 0.6, case + ": the door detail opening " + str(w) + " equals the plan OPENING " + str(opening))
    mh = re.search(r"X(\d+)", door_text or "")
    if len(jambs) == 2 and opening is not None:
        plan_str = opening + sum(jambs.values())
        check(abs(plan_str - strw) < 1.1, case + ": the door detail STR. " + str(strw) + " equals the plan wall opening " + str(round(plan_str)))
    check(mh is not None and int(mh.group(1)) == h, case + ": the door detail height " + str(h) + " equals the plan DOOR text " + str(door_text))


def check_door_annotation(doc, case, check):
    """G2: in the door detail no two texts overlap, and no line of the axis, the C marks or the floor (layer A-ANNO) and no leader
    crosses a text (multileader, dimension text, MTEXT)."""
    from ezdxf import bbox
    ins = [e for e in doc.modelspace() if e.dxftype() == "INSERT" and DOOR.match(e.dxf.name)]
    if not ins:
        return
    ox, oy = ins[0].dxf.insert.x, ins[0].dxf.insert.y
    win = (ox - 4000, oy - 1500, ox + 7000, oy + 4500)
    inside = lambda x, y: win[0] < x < win[2] and win[1] < y < win[3]
    boxes, segs = [], []
    for e in doc.modelspace():
        t = e.dxftype()
        if e.dxf.get("invisible", 0):
            continue
        if t in ("MULTILEADER", "DIMENSION", "MTEXT"):
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
                if not b.has_data or not inside(b.extmin.x, b.extmin.y):
                    continue
                if vt in ("MTEXT", "TEXT"):
                    boxes.append((b.extmin.x, b.extmin.y, b.extmax.x, b.extmax.y, (v.plain_text() if vt == "MTEXT" else v.dxf.text)[:20], id(e)))
                elif t == "MULTILEADER" and vt == "LINE":
                    segs.append(((v.dxf.start.x, v.dxf.start.y), (v.dxf.end.x, v.dxf.end.y), "leader", id(e)))
        elif t in ("LINE", "LWPOLYLINE") and e.dxf.layer == "A-ANNO":
            pts = [(e.dxf.start.x, e.dxf.start.y), (e.dxf.end.x, e.dxf.end.y)] if t == "LINE" else [(p[0], p[1]) for p in e.get_points("xy")]
            if all(inside(x, y) for x, y in pts):
                for i in range(len(pts) - 1):
                    segs.append((pts[i], pts[i + 1], "line", id(e)))
    bad = []
    for i in range(len(boxes)):
        for j in range(i + 1, len(boxes)):
            a, b = boxes[i], boxes[j]
            if a[5] != b[5] and a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]:
                bad.append((a[4], b[4]))
    check(not bad, case + ": no two texts of the door detail overlap " + str(bad[:4]))
    hits = []
    for p, q, kind, owner in segs:
        for b in boxes:
            if b[5] == owner:
                continue
            # shrink the box a little: a leader may end at its own text, a line may touch a box corner
            x0, y0, x1, y1 = b[0] + 0.1 * (b[2] - b[0]), b[1] + 0.2 * (b[3] - b[1]), b[2] - 0.1 * (b[2] - b[0]), b[3] - 0.2 * (b[3] - b[1])
            edges = [((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x1, y1), (x0, y1)), ((x0, y1), (x0, y0))]
            if any(_cross(p, q, a, c) for a, c in edges) or (x0 < p[0] < x1 and y0 < p[1] < y1):
                hits.append((kind, b[4]))
    check(not hits, case + ": no axis, mark, floor line or leader of the door detail crosses a text " + str(hits[:4]))
