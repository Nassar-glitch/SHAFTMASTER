"""Overlap check of the steel labels (DESIGN-FLOW principle 8: nothing on top of anything).

For a DXF read back from a generated drawing: every steel label (an MTEXT of an entity tagged Plan.Steel., Steel.MachineRoom.,
Steel.Section. or SectionAA.Steel.) and its leader (the steel LINE that starts at the text) must not lie on any other text of the
drawing (MTEXT, TEXT, and the texts inside the dimension blocks) or on the slab edge outline. Texts and leaders of the
generated views only; the details far away at x > 900000 are left out.
"""
from ezdxf import bbox

STEEL_PREFIXES = ("Plan.Steel.", "Steel.MachineRoom.", "Steel.Section.", "SectionAA.Steel.")
HIDDEN_LAYER = "Steel.Hidden"


def _tag(e):
    try:
        if e.xdata and "ELEVATOR_AUTOMATION" in e.xdata:
            values = [v.value for v in e.get_xdata("ELEVATOR_AUTOMATION")]
            return values[0]
    except Exception:
        pass
    return None


def _visible(e):
    return not e.dxf.get("invisible", 0) and e.dxf.get("layer", "0") != HIDDEN_LAYER


CHAR_WIDTH = 0.7      # of the text height, other texts of the drawing
LABEL_CHAR_WIDTH = 1.0   # steel labels: measured on the generated sheets, about 0.97 of the text height
LINE_HEIGHT = 1.2


def _mtext_box(e):
    import math
    try:
        lines = e.plain_text().split(chr(10))
    except Exception:
        lines = [e.text]
    h = float(e.dxf.get("char_height", 2.5))
    key = _tag(e)
    factor = LABEL_CHAR_WIDTH if key and key.startswith(STEEL_PREFIXES) else CHAR_WIDTH
    w = max(len(l) for l in lines) * factor * h
    hh = LINE_HEIGHT * h * len(lines)
    a = int(e.dxf.get("attachment_point", 1))
    col = (a - 1) % 3          # 0 left, 1 centre, 2 right
    row = (a - 1) // 3         # 0 top, 1 middle, 2 bottom
    x0 = -w * col / 2.0
    y0 = -hh * (1 - row / 2.0) if row < 2 else 0.0
    if row == 1:
        y0 = -hh / 2.0
    if row == 0:
        y0 = -hh
    corners = [(x0, y0), (x0 + w, y0), (x0 + w, y0 + hh), (x0, y0 + hh)]
    rot = e.dxf.get("rotation", None)
    if rot is None and e.dxf.hasattr("text_direction"):
        d = e.dxf.text_direction
        rot = math.degrees(math.atan2(d.y, d.x))
    ang = math.radians(rot or 0.0)
    ca, sa = math.cos(ang), math.sin(ang)
    ins = e.dxf.insert
    pts = [(ins.x + x * ca - y * sa, ins.y + x * sa + y * ca) for x, y in corners]
    return (min(p[0] for p in pts), min(p[1] for p in pts), max(p[0] for p in pts), max(p[1] for p in pts))


def _box(e):
    if e.dxftype() == "MTEXT":
        try:
            return _mtext_box(e)
        except Exception:
            pass
    try:
        b = bbox.extents([e], fast=False)
    except Exception:
        return None
    if not b.has_data:
        return None
    return (b.extmin.x, b.extmin.y, b.extmax.x, b.extmax.y)


def _overlap(a, b, shrink=0.0):
    return a[0] + shrink < b[2] - shrink and b[0] + shrink < a[2] - shrink and a[1] + shrink < b[3] - shrink and b[1] + shrink < a[3] - shrink


def _segment_hits_box(x0, y0, x1, y1, box):
    # Liang-Barsky clip of a segment against the box
    dx, dy = x1 - x0, y1 - y0
    t0, t1 = 0.0, 1.0
    for p, q in ((-dx, x0 - box[0]), (dx, box[2] - x0), (-dy, y0 - box[1]), (dy, box[3] - y0)):
        if abs(p) < 1e-12:
            if q < 0:
                return False
        else:
            t = q / p
            if p < 0:
                if t > t1:
                    return False
                t0 = max(t0, t)
            else:
                if t < t0:
                    return False
                t1 = min(t1, t)
    return t0 < t1 - 1e-9


def _what(e):
    try:
        if e.dxftype() == "DIMENSION":
            return "dim " + str(round(e.get_measurement()))
        return e.text.strip()[:24]
    except Exception:
        return "?"


AXIS_ROLES = ("mainAxis", "carAxis", "cwAxis")


def _role(e):
    try:
        if e.xdata and "ELEVATOR_AUTOMATION" in e.xdata:
            values = [v.value for v in e.get_xdata("ELEVATOR_AUTOMATION")]
            return values[1] if len(values) > 1 else ""
    except Exception:
        pass
    return ""


def lines_of(ms):
    """Segments a label text box may not touch: the lines inside the dimension blocks (dimension and extension lines, arrows) and the
    axes of the drawing. Only the leader of a label may cross them."""
    out = []
    for e in ms:
        if not _visible(e):
            continue
        t = e.dxftype()
        if t == "DIMENSION":
            try:
                for v in e.virtual_entities():
                    vt = v.dxftype()
                    if vt == "LINE":
                        out.append((v.dxf.start.x, v.dxf.start.y, v.dxf.end.x, v.dxf.end.y))
                    elif vt == "LWPOLYLINE":
                        pts = list(v.get_points())
                        for i in range(len(pts) - 1):
                            out.append((pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1]))
                    elif vt in ("SOLID", "TRACE", "INSERT"):
                        b = _box(v)
                        if b is not None:
                            out += [(b[0], b[1], b[2], b[1]), (b[2], b[1], b[2], b[3]), (b[2], b[3], b[0], b[3]), (b[0], b[3], b[0], b[1])]
            except Exception:
                continue
        elif t == "LINE" and _role(e) in AXIS_ROLES:
            out.append((e.dxf.start.x, e.dxf.start.y, e.dxf.end.x, e.dxf.end.y))
    return [g for g in out if g[0] < 900000 and g[2] < 900000]


def texts_of(ms):
    """(owner, box) of every text of the model space, the dimension blocks included."""
    out = []
    for e in ms:
        if not _visible(e):
            continue
        t = e.dxftype()
        if t in ("MTEXT", "TEXT"):
            b = _box(e)
            if b is not None and b[0] < 900000:
                out.append((e, b))
        elif t == "DIMENSION":
            try:
                for v in e.virtual_entities():
                    if v.dxftype() in ("MTEXT", "TEXT"):
                        b = _box(v)
                        if b is not None and b[0] < 900000:
                            out.append((e, b))
            except Exception:
                continue
    return out


def check(ms):
    """Returns the list of overlaps found (strings); empty when every steel label is clear."""
    texts = texts_of(ms)
    segs = lines_of(ms)
    labels = []
    leaders = []
    slab_lines = []
    for e in ms:
        if not _visible(e):
            continue
        key = _tag(e)
        if not key or not key.startswith(STEEL_PREFIXES):
            continue
        t = e.dxftype()
        if t == "MTEXT":
            b = _box(e)
            if b is not None:
                labels.append((e, b))
        elif t == "LINE":
            leaders.append(e)
        elif t == "LWPOLYLINE" and e.closed and len(e) == 4:
            pts = list(e.get_points())
            w = max(p[0] for p in pts) - min(p[0] for p in pts)
            h = max(p[1] for p in pts) - min(p[1] for p in pts)
            if 200 < h < 400 and w > 1500:
                slab_lines.append(e)
    found = []
    for e, b in labels:
        text = e.text.strip()[:30]
        if b[0] > 900000:
            continue
        for other, ob in texts:
            if other is e:
                continue
            if _overlap(b, ob, 0.5):
                found.append("label '" + text + "' on a text of " + other.dxftype() + " '" + _what(other) + "' at (%.0f, %.0f)" % (b[0], b[1]))
        inner = (b[0] + 1, b[1] + 1, b[2] - 1, b[3] - 1)
        for g in segs:
            if _segment_hits_box(g[0], g[1], g[2], g[3], inner):
                found.append("label '" + text + "' on a dimension, extension or axis line at (%.0f, %.0f)" % (g[0], g[1]))
                break
        for s in slab_lines:
            pts = list(s.get_points())
            n = len(pts)
            for i in range(n):
                a, c = pts[i], pts[(i + 1) % n]
                if _segment_hits_box(a[0], a[1], c[0], c[1], (b[0] + 1, b[1] + 1, b[2] - 1, b[3] - 1)):
                    found.append("label '" + text + "' on the slab edge")
                    break
    # a leader is a steel line that ends at a steel label's text box (a member web line does not)
    def ends_at_label(ln):
        for _, b in labels:
            for q in (ln.dxf.start, ln.dxf.end):
                if b[0] - 80 <= q.x <= b[2] + 80 and b[1] - 160 <= q.y <= b[3] + 160:
                    return True
        return False
    for ln in leaders:
        if not ends_at_label(ln):
            continue
        s, c = ln.dxf.start, ln.dxf.end
        if abs(s.x - c.x) < 1e-6 and abs(s.y - c.y) < 1e-6:
            continue
        for other, ob in texts:
            inner = (ob[0] + 1, ob[1] + 1, ob[2] - 1, ob[3] - 1)
            if inner[0] < inner[2] and inner[1] < inner[3] and _segment_hits_box(s.x, s.y, c.x, c.y, inner):
                # a leader that belongs to the label whose text it hits is the label itself
                if other.dxftype() == "MTEXT" and _tag(other) and _tag(other).startswith(STEEL_PREFIXES):
                    lb = _box(other)
                    # the end of its own leader touches its text: allow when the segment ends at the box edge
                    if lb is not None and (abs(c.x - lb[0]) < 80 or abs(c.x - lb[2]) < 80) and abs(c.y - lb[1]) < 120:
                        continue
                found.append("a steel leader crosses a text of " + other.dxftype() + " '" + _what(other) + "' leader " + str(_tag(ln)) + " (%.0f, %.0f)-(%.0f, %.0f)" % (s.x, s.y, c.x, c.y))
                break
    return found
