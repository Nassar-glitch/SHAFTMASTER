"""Overlap check of the steel labels (DESIGN-FLOW principle 8: nothing on top of anything) and the reading helpers for the steel entities
(principle 11: labels are multileaders, repeated parts are named blocks).

For a DXF read back from a generated drawing: every steel label (an MTEXT or a MULTILEADER of an entity tagged Plan.Steel.,
Steel.MachineRoom., Steel.Section. or SectionAA.Steel.) and its leader must not lie on any other text of the drawing (MTEXT, TEXT, the
texts of the multileaders and inside the dimension blocks), on a dimension / extension / axis line, on another leader or on the slab
edge outline. Only the leader of a label may cross lines. The details far away at x > 900000 are left out.
"""
import math

from ezdxf import bbox

STEEL_PREFIXES = ("Plan.Steel.", "Steel.MachineRoom.", "Steel.Section.", "SectionAA.Steel.")
HIDDEN_LAYER = "Steel.Hidden"
CHAR_WIDTH = 0.7          # of the text height, other texts of the drawing
LABEL_CHAR_WIDTH = 1.0    # steel labels: measured on the generated sheets, about 0.97 of the text height
LINE_HEIGHT = 1.0
AXIS_ROLES = ("mainAxis", "carAxis", "cwAxis")


def tag_of(e):
    """(key, role) of the ELEVATOR_AUTOMATION xdata of an entity, or None."""
    try:
        if e.xdata and "ELEVATOR_AUTOMATION" in e.xdata:
            values = [v.value for v in e.get_xdata("ELEVATOR_AUTOMATION")]
            return (values[0], values[1] if len(values) > 1 else "")
    except Exception:
        pass
    return None


def _tag(e):
    t = tag_of(e)
    return t[0] if t else None


def _role(e):
    t = tag_of(e)
    return t[1] if t else ""


def _visible(e):
    return not e.dxf.get("invisible", 0) and e.dxf.get("layer", "0") != HIDDEN_LAYER


def expand(ms):
    """[(entity, (key, role))] of the tagged model space: an insertion of a steel block (SM_...) is followed by its parts in drawing
    coordinates, which carry the tag of the insertion."""
    out = []
    for e in ms:
        t = tag_of(e)
        if not t:
            continue
        if e.dxftype() == "INSERT" and e.dxf.name.startswith("SM_"):
            try:
                for v in e.virtual_entities():
                    out.append((v, t))
            except Exception:
                pass
        out.append((e, t))
    return out


def text_of(e):
    """The text of an MTEXT, TEXT or MULTILEADER ('' for the others)."""
    try:
        t = e.dxftype()
        if t == "MTEXT":
            return e.text
        if t == "TEXT":
            return e.dxf.text
        if t == "MULTILEADER":
            return e.context.mtext.default_content or ""
    except Exception:
        pass
    return ""


def _text_box(insert, height, attach, text, factor, rotation_deg=0.0):
    lines = text.replace("\\P", "\n").split("\n")
    w = max(len(l) for l in lines) * factor * height
    hh = LINE_HEIGHT * height * len(lines)
    col = (attach - 1) % 3        # 0 left, 1 centre, 2 right
    row = (attach - 1) // 3       # 0 top, 1 middle, 2 bottom
    x0 = -w * col / 2.0
    y0 = -hh if row == 0 else (-hh / 2.0 if row == 1 else 0.0)
    corners = [(x0, y0), (x0 + w, y0), (x0 + w, y0 + hh), (x0, y0 + hh)]
    ang = math.radians(rotation_deg or 0.0)
    ca, sa = math.cos(ang), math.sin(ang)
    pts = [(insert[0] + x * ca - y * sa, insert[1] + x * sa + y * ca) for x, y in corners]
    return (min(p[0] for p in pts), min(p[1] for p in pts), max(p[0] for p in pts), max(p[1] for p in pts))


def _mtext_box(e, steel):
    try:
        text = e.plain_text()
    except Exception:
        text = e.text
    h = float(e.dxf.get("char_height", 2.5))
    rot = e.dxf.get("rotation", None)
    if rot is None and e.dxf.hasattr("text_direction"):
        d = e.dxf.text_direction
        rot = math.degrees(math.atan2(d.y, d.x))
    ins = e.dxf.insert
    return _text_box((ins.x, ins.y), h, int(e.dxf.get("attachment_point", 1)), text, LABEL_CHAR_WIDTH if steel else CHAR_WIDTH, rot)


def _ml_parts(e, steel):
    """(box of the text or None, [leader segments]) of a MULTILEADER."""
    c = e.context
    m = c.mtext
    segs = []
    for ld in c.leaders:
        for ln in ld.lines:
            pts = [(v.x, v.y) for v in ln.vertices]
            for a, b in zip(pts, pts[1:]):
                segs.append((a[0], a[1], b[0], b[1]))
    box = None
    if m is not None and m.default_content:
        ins = (m.insert.x, m.insert.y)
        # the text of a multileader is left aligned and inserted at its left middle (alignment 1; a native left label is aligned right)
        attach = 1 if m.alignment == 1 else (3 if m.alignment == 3 else 2)    # the insertion point is the top of the frame
        box = _text_box(ins, c.char_height, attach, m.default_content, LABEL_CHAR_WIDTH if steel else CHAR_WIDTH)
    return box, segs


def _box(e):
    if e.dxftype() == "MTEXT":
        try:
            return _mtext_box(e, False)
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
        return text_of(e).strip()[:24]
    except Exception:
        return "?"


def texts_of(ms):
    """(owner, box) of every text of the model space: MTEXT, TEXT, the texts of the multileaders and inside the dimension blocks."""
    out = []
    for e in ms:
        if not _visible(e):
            continue
        t = e.dxftype()
        steel = bool(_tag(e) and _tag(e).startswith(STEEL_PREFIXES))
        if t == "MTEXT":
            try:
                b = _mtext_box(e, steel)
            except Exception:
                b = _box(e)
            if b is not None and b[0] < 900000:
                out.append((e, b))
        elif t == "TEXT":
            b = _box(e)
            if b is not None and b[0] < 900000:
                out.append((e, b))
        elif t == "MULTILEADER":
            try:
                b, _ = _ml_parts(e, steel)
            except Exception:
                b = None
            if b is not None and b[0] < 900000:
                out.append((e, b))
        elif t == "DIMENSION":
            try:
                for v in e.virtual_entities():
                    if v.dxftype() in ("MTEXT", "TEXT"):
                        b = _mtext_box(v, False) if v.dxftype() == "MTEXT" else _box(v)
                        if b is not None and b[0] < 900000:
                            out.append((e, b))
            except Exception:
                continue
    return out


def lines_of(ms):
    """(owner, segment) a label text box may not touch: the lines inside the dimension blocks (dimension and extension lines, arrows),
    the axes of the drawing and the leaders of the multileaders."""
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
                        out.append((e, (v.dxf.start.x, v.dxf.start.y, v.dxf.end.x, v.dxf.end.y)))
                    elif vt == "LWPOLYLINE":
                        pts = list(v.get_points())
                        for i in range(len(pts) - 1):
                            out.append((e, (pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1])))
                    elif vt in ("SOLID", "TRACE", "INSERT"):
                        b = _box(v)
                        if b is not None:
                            out += [(e, g) for g in ((b[0], b[1], b[2], b[1]), (b[2], b[1], b[2], b[3]), (b[2], b[3], b[0], b[3]), (b[0], b[3], b[0], b[1]))]
            except Exception:
                continue
        elif t == "LINE" and _role(e) in AXIS_ROLES:
            out.append((e, (e.dxf.start.x, e.dxf.start.y, e.dxf.end.x, e.dxf.end.y)))
        elif t == "MULTILEADER":
            try:
                _, segs = _ml_parts(e, False)
                out += [(e, g) for g in segs]
            except Exception:
                continue
    return [(o, g) for o, g in out if g[0] < 900000 and g[2] < 900000]


def check(ms):
    """Returns the list of overlaps found (strings); empty when every steel label is clear."""
    plain = list(ms)
    items = expand(plain)
    texts = texts_of(plain)
    segs = lines_of(plain)
    labels = []        # (entity, box, own leader segments)
    slab_lines = []
    loose = []
    for e, (key, role) in items:
        if not key.startswith(STEEL_PREFIXES) or not _visible(e):
            continue
        t = e.dxftype()
        if t == "MTEXT":
            try:
                labels.append((e, _mtext_box(e, True), []))
            except Exception:
                pass
        elif t == "MULTILEADER":
            try:
                b, own = _ml_parts(e, True)
            except Exception:
                continue
            if b is not None:
                labels.append((e, b, own))
        elif t == "LINE":
            loose.append(e)
        elif t == "LWPOLYLINE" and e.closed and len(e) == 4:
            pts = list(e.get_points())
            w = max(p[0] for p in pts) - min(p[0] for p in pts)
            h = max(p[1] for p in pts) - min(p[1] for p in pts)
            if 200 < h < 400 and w > 1500:
                slab_lines.append(e)
    found = []
    for e, b, own in labels:
        text = text_of(e).strip()[:30]
        if b[0] > 900000:
            continue
        for other, ob in texts:
            if other is e:
                continue
            if _overlap(b, ob, 0.5):
                found.append("label '" + text + "' on a text of " + other.dxftype() + " '" + _what(other) + "' at (%.0f, %.0f)" % (b[0], b[1]))
        inner = (b[0] + 1, b[1] + 1, b[2] - 1, b[3] - 1)
        for owner, g in segs:
            if owner is e:
                continue
            if _segment_hits_box(g[0], g[1], g[2], g[3], inner):
                found.append("label '" + text + "' on a dimension, extension, axis or leader line at (%.0f, %.0f)" % (g[0], g[1]))
                break
        for s in slab_lines:
            pts = list(s.get_points())
            n = len(pts)
            for i in range(n):
                a, c = pts[i], pts[(i + 1) % n]
                if _segment_hits_box(a[0], a[1], c[0], c[1], inner):
                    found.append("label '" + text + "' on the slab edge")
                    break
        # its own leader may cross lines but no text
        for g in own:
            for other, ob in texts:
                if other is e:
                    continue
                inner_t = (ob[0] + 1, ob[1] + 1, ob[2] - 1, ob[3] - 1)
                if inner_t[0] < inner_t[2] and inner_t[1] < inner_t[3] and _segment_hits_box(g[0], g[1], g[2], g[3], inner_t):
                    found.append("the leader of '" + text + "' crosses a text of " + other.dxftype() + " '" + _what(other) + "'")
                    break
    # a loose steel line that ends at a steel text is an old style leader: a multileader replaces it
    for ln in loose:
        for e, b, _ in labels:
            if e.dxftype() == "MTEXT" and any(b[0] - 80 <= q.x <= b[2] + 80 and b[1] - 160 <= q.y <= b[3] + 160 for q in (ln.dxf.start, ln.dxf.end)):
                found.append("a loose line leads to the text '" + text_of(e).strip()[:30] + "': it is not a multileader")
                break
    return found
