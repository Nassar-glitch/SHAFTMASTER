"""E1 audit (DESIGN-FLOW principle 11, "Editable drawings"): how each kind of item of a generated drawing is drawn.

usage: audit_editability.py <dxf> [<dxf> ...]   (DXFs made from the generated DWGs, e.g. by verify_steel.py / DXFOUT)

For every view (by the ELEVATOR_AUTOMATION key prefix of its entities, the template details by their place far from the views, and the
paper space of the layout SM_Geared_A0) it prints a markdown table: the entity types, the layers, the leaders (MULTILEADER / LEADER
against a loose LINE that ends at a text), the dimensions (DIMENSION against dimension-looking lines), the blocks, the hatches and the
texts with their styles. The "loose leaders" column counts LINE entities with an end point within 60 mm of a TEXT / MTEXT box of the same
view: the pairs a user has to hunt for.
"""
import collections
import sys

import ezdxf
from ezdxf import bbox

VIEWS = [
    ("Plan (concrete template)", ("Plan.",), ("Plan.Steel.",)),
    ("Plan, steel tower", ("Plan.Steel.",), ()),
    ("Machine-room plan (concrete template)", ("MachineRoom.",), ()),
    ("Machine-room plan, steel tower top", ("Steel.MachineRoom.",), ()),
    ("Section B-B (template)", ("MachineRoomSection.",), ()),
    ("Section B-B, steel", ("Steel.Section.",), ()),
    ("Section A-A (template)", ("SectionAA.",), ("SectionAA.Steel.",)),
    ("Section A-A, steel", ("SectionAA.Steel.",), ()),
]


def tag(e):
    try:
        if e.xdata and "ELEVATOR_AUTOMATION" in e.xdata:
            return [v.value for v in e.get_xdata("ELEVATOR_AUTOMATION")][0]
    except Exception:
        pass
    return None


def text_box(e):
    try:
        b = bbox.extents([e], fast=True)
        if b.has_data:
            return (b.extmin.x, b.extmin.y, b.extmax.x, b.extmax.y)
    except Exception:
        pass
    return None


def group(ents):
    types = collections.Counter(e.dxftype() for e in ents)
    layers = collections.Counter(e.dxf.layer for e in ents)
    blocks = collections.Counter(e.dxf.name for e in ents if e.dxftype() == "INSERT")
    styles = collections.Counter(getattr(e.dxf, "style", "") for e in ents if e.dxftype() in ("TEXT", "MTEXT"))
    texts = [(e, text_box(e)) for e in ents if e.dxftype() in ("TEXT", "MTEXT")]
    boxes = [b for _, b in texts if b]
    loose = 0
    for e in ents:
        if e.dxftype() != "LINE":
            continue
        for q in (e.dxf.start, e.dxf.end):
            if any(b[0] - 60 <= q.x <= b[2] + 60 and b[1] - 60 <= q.y <= b[3] + 60 for b in boxes):
                loose += 1
                break
    return types, layers, blocks, styles, loose


def row(name, ents):
    if not ents:
        return None
    types, layers, blocks, styles, loose = group(ents)
    lead = types.get("MULTILEADER", 0) + types.get("MLEADER", 0) + types.get("LEADER", 0)
    return {
        "view": name, "n": len(ents), "types": types, "layers": layers, "blocks": blocks, "styles": styles,
        "mleaders": lead, "loose": loose, "dims": types.get("DIMENSION", 0),
        "lines": types.get("LINE", 0) + types.get("LWPOLYLINE", 0) + types.get("ARC", 0) + types.get("CIRCLE", 0),
        "hatch": types.get("HATCH", 0), "text": types.get("TEXT", 0) + types.get("MTEXT", 0), "inserts": types.get("INSERT", 0),
    }


def audit(path):
    doc = ezdxf.readfile(path)
    ms = doc.modelspace()
    rows = []
    taken = set()
    for name, include, exclude in VIEWS:
        ents = []
        for e in ms:
            k = tag(e)
            if k and k.startswith(include) and not k.startswith(exclude):
                ents.append(e)
                taken.add(e.dxf.handle)
        r = row(name, ents)
        if r:
            rows.append(r)
    other_tagged = [e for e in ms if tag(e) and e.dxf.handle not in taken]
    r = row("Other tagged entities", other_tagged)
    if r:
        rows.append(r)
    details = []
    for e in ms:
        if tag(e) or e.dxf.handle in taken:
            continue
        b = None
        try:
            b = bbox.extents([e], fast=True)
        except Exception:
            pass
        if b is not None and b.has_data and b.extmin.x > 900000:
            details.append(e)
    r = row("Template reference details (model space, x > 900000)", details)
    if r:
        rows.append(r)
    layout = doc.layouts.get("SM_Geared_A0")
    if layout is not None:
        r = row("Sheet (paper space of SM_Geared_A0)", [e for e in layout if e.dxftype() != "VIEWPORT"])
        if r:
            rows.append(r)
    return rows


def print_markdown(path, rows):
    print("### " + path.replace("\\", "/").split("/")[-1])
    print()
    print("| View | Entities | Lines / arcs | DIMENSION | MULTILEADER / LEADER | Loose leaders (LINE ending at a text) | INSERT | HATCH | TEXT / MTEXT |")
    print("|---|---:|---:|---:|---:|---:|---:|---:|---:|")
    for r in sorted(rows, key=lambda r: -r["loose"]):
        print("| %s | %d | %d | %d | %d | %d | %d | %d | %d |" % (r["view"], r["n"], r["lines"], r["dims"], r["mleaders"], r["loose"], r["inserts"], r["hatch"], r["text"]))
    print()
    print("Layers per view:")
    print()
    for r in rows:
        print("- " + r["view"] + ": " + ", ".join("%s (%d)" % (k, v) for k, v in r["layers"].most_common(8)))
    print()
    print("Blocks inserted: " + (", ".join("%s x%d" % (k, v) for r in rows for k, v in r["blocks"].items()) or "none"))
    print()


if __name__ == "__main__":
    for p in sys.argv[1:]:
        print_markdown(p, audit(p))
