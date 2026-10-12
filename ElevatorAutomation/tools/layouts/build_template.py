"""Builds the reduced geared A0 sheet template (Cloud C2).

    python -I tools/layouts/build_template.py [source.dxf] [target.dxf]

Defaults: source ../Layouts_Sec/01_Geared_MR_1-1_LAYOUT.dxf (read-only, never written), target Templates/Layout_Geared_A0.dxf
plus Templates/Layout_Geared_A0.details.json (the reference-detail windows). Needs ezdxf (tested with 1.4.4). Deterministic: the
same source gives the same bytes (fixed header meta data, fixed processing order, rounded coordinates).

What it does (see tools/layouts/README.md and docs/design/layouts/LAYOUTS.md):
- paper space: keeps the frame and the title block, removes the sample project's value texts, the bar scale, the OLE logo and the
  old viewports, converts the paper units from the template's centimetres (1 unit = 9.969 mm) to millimetres on an A0 landscape
  sheet (1189 x 841), and clears the plot settings;
- one block SM_TITLEBLOCK holds an attribute definition per value field; it is inserted once in paper space;
- model space: keeps one representative of each detail kind at a reserved offset and drops the rest of the library;
- styles: keeps the SHX fonts, adds SM_AR (xarab.shx) and SM_EN (cent.shx), and renames the text and dimension styles that clash
  with AutoCAD built-ins (prefix SM_).
"""
import json
import os
import re
import sys

import ezdxf
from ezdxf import bbox, recover
from ezdxf.enums import TextEntityAlignment
from ezdxf.math import Matrix44

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))  # ElevatorAutomation
REPO = os.path.dirname(ROOT)
SOURCE = os.path.join(REPO, "Layouts_Sec", "01_Geared_MR_1-1_LAYOUT.dxf")
TARGET = os.path.join(ROOT, "Templates", "Layout_Geared_A0.dxf")

LAYOUT_NAME = "SM_Geared_A0"
BLOCK_NAME = "SM_TITLEBLOCK"
STYLE_AR = "SM_AR"  # xarab.shx: Arabic (xarab key codes, see Elevator.Core.Layouts.XarabText)
STYLE_EN = "SM_EN"  # cent.shx: English
PAPER_W, PAPER_H = 1189.0, 841.0

# The template's outer sheet border in its paper units (cm), LAYOUTS.md section 1; it scales to the A0 height. The inner frame
# (the layer-0 border lines) is checked to be sure the source is the analysed one.
BORDER = (-44.32, -136.74, 74.39, -52.38)
FRAME = (-44.115, -136.539, 74.188, -52.578)

# ---------- reference details (model space, source mm) ----------
# One representative of each detail kind. Window = (x0, y0, x1, y1); an entity is kept when its extents lie inside.
# The rail-bracket elevations are found by the "C.W. GUIDE RAILS" text of each set: the window has the same offsets from that
# text as the template's own viewport 6 (set 5-9).
def _rails_window(ax, ay):
    return (ax - 334, ay - 379, ax + 634, ay + 533)


DETAILS = [
    # name, kind, attributes, window, scale shown in the template
    ("FISH_PLATE", "RailFixing", {}, (-12586, 32686, -11519, 34348), 10),
    ("RAILS_PLAN_CW5_CAR9", "RailsPlan", {"CwRailMm": 5, "CarRailMm": 9}, (15667, 28334, 17761, 29142), 10),
    ("RAIL_BRACKETS_CW5_CAR9", "RailBrackets", {"CwRailMm": 5, "CarRailMm": 9}, _rails_window(15901, 32349), 10),
    ("RAIL_BRACKETS_CW5_CAR16", "RailBrackets", {"CwRailMm": 5, "CarRailMm": 16}, _rails_window(18355, 32316), 10),
    ("RAIL_BRACKETS_CW9_CAR16", "RailBrackets", {"CwRailMm": 9, "CarRailMm": 16}, _rails_window(18355, 31057), 10),
    ("DOOR_AUTOMATIC", "DoorElevationSection", {"Door": "Automatic"}, (-29676, 32352, -25854, 36460), 20),
    ("DOOR_SWING", "DoorElevationSection", {"Door": "Swing"}, (-64700, 32100, -60900, 36500), 20),
]
DETAIL_ORIGIN = (1000000.0, 0.0)  # reserved model-space offset (mm), far from every generated view
DETAIL_GAP = 2000.0

# ---------- paper space ----------
# Value texts of the sample project (handles in the source), replaced by attributes.
VALUE_TEXTS = {
    "7AD97", "7AD9F", "7ADA0", "7ADA1", "7AD96", "7AD94", "7AD95",  # "P1 =" ... "P7 =" labels (the value now carries "P1 = ")
    "7ADAB", "7ADAC", "7ADAD", "7ADAE", "7ADA8", "7ADA9", "7ADAA",  # P1..P7 values
    "7AD8E", "7AD8D", "7AD8F", "7AD90", "7ADA4", "7AE4B", "7AE4C", "7AD91", "7AD92",  # data table values
    "7ADA6", "7ADB2", "7ADB3",  # company name (French line, two Arabic lines)
    "7ADB1", "A4DB2",  # owner / project placeholders
    "79FF3",  # old value in the drawing-name box ("HulhG lwhu]")
    "79FC4",  # "A1 - 1 \ 100"
    "7AD98",  # drawing number "1"
}
BAR_SCALE = (61.1, -133.7, 64.7, -132.6)  # every entity inside is the bar scale (valid for one scale only)
LOGO_AREA = (62.31, -106.68, 68.33, -102.17)

L, C = TextEntityAlignment.TOP_LEFT, TextEntityAlignment.MIDDLE_CENTER
# tag, source position (cm), height (cm), alignment, style, prompt
ATTRIBUTES = [
    ("P1", (58.37, -58.40), 0.36, L, STYLE_EN, "P1 load"),
    ("P2", (65.53, -58.40), 0.36, L, STYLE_EN, "P2 load"),
    ("P3", (58.37, -60.11), 0.36, L, STYLE_EN, "P3 load"),
    ("P4", (65.53, -60.12), 0.36, L, STYLE_EN, "P4 load"),
    ("P5", (58.32, -62.47), 0.36, L, STYLE_EN, "P5 load, one car guide rail"),
    ("P6", (58.32, -66.01), 0.36, L, STYLE_EN, "P6 load, one car buffer"),
    ("P7", (58.32, -67.16), 0.36, L, STYLE_EN, "P7 load, one counterweight buffer"),
    ("LOAD", (63.55, -87.03), 0.36, L, STYLE_EN, "Capacity"),
    ("SPEED", (63.56, -87.99), 0.36, L, STYLE_EN, "Speed"),
    ("TRAVEL", (63.56, -88.95), 0.36, L, STYLE_EN, "Travel"),
    ("STOPS", (63.56, -90.07), 0.36, L, STYLE_EN, "Stops"),
    ("FLOORS", (64.41, -90.07), 0.30, L, STYLE_EN, "Stop names"),
    ("ENTRANCES", (63.56, -91.07), 0.36, L, STYLE_EN, "Entrances"),
    ("ENTRANCE_NAMES", (64.42, -91.07), 0.30, L, STYLE_EN, "Entrance names"),
    ("CONTROL", (63.53, -92.09), 0.36, L, STYLE_EN, "Control"),
    ("SUPPLY", (63.53, -93.03), 0.36, L, STYLE_EN, "Current"),
    ("COMPANY_AR", (65.33, -96.98), 0.64, C, STYLE_AR, "Company name (Arabic)"),
    ("COMPANY", (65.33, -100.46), 0.60, C, STYLE_EN, "Company name"),
    ("REV_ROW1_NO", (63.15, -109.67), 0.35, C, STYLE_EN, "Revision"),
    ("REV_ROW1_DATE", (70.50, -109.67), 0.35, C, STYLE_EN, "Revision date"),
    ("CLIENT", (65.33, -115.32), 0.60, C, STYLE_EN, "Owner"),
    ("PROJECT", (65.34, -122.79), 0.60, C, STYLE_EN, "Project name"),
    ("DOC_NO", (63.70, -127.90), 0.35, C, STYLE_EN, "Project number"),
    ("DRAWING_NAME", (66.00, -129.75), 0.29, C, STYLE_AR, "Drawing name"),
    ("TYPE_OF_WORK", (59.18, -130.00), 0.20, C, STYLE_AR, "Type of work"),
    ("SCALE", (62.90, -132.00), 0.34, C, STYLE_EN, "Scale"),
    ("PREPARED", (68.85, -131.55), 0.24, C, STYLE_EN, "Prepared by"),
    ("CHECKED", (68.85, -132.60), 0.24, C, STYLE_EN, "Checked by"),
    ("APPROVED", (68.85, -133.35), 0.24, C, STYLE_EN, "Approved by"),
    ("DATE", (62.60, -134.75), 0.24, C, STYLE_EN, "Date"),
    ("PHASE", (66.20, -134.75), 0.24, C, STYLE_EN, "Phase"),
    ("SHEET_NO", (69.93, -134.70), 0.50, C, STYLE_EN, "Drawing number"),
]

# Clashes with AutoCAD built-in names: the template's entities move to the SM_ copies.
TEXT_STYLE_RENAMES = {"Standard": "SM_STANDARD"}
DIM_STYLE_RENAMES = {"ISO-25": "SM_ISO25", "Copy of ISO-25": "SM_ISO25_COPY", "Standard": "SM_STANDARD"}


def inside(b, w, tol=1e-6):
    return b.extmin.x >= w[0] - tol and b.extmin.y >= w[1] - tol and b.extmax.x <= w[2] + tol and b.extmax.y <= w[3] + tol


def key(e):
    """Identity of an entity without its handle: exact duplicates of the library (it holds two copies) are dropped."""
    attribs = sorted((k, repr(v)) for k, v in e.dxfattribs().items() if k not in ("handle", "owner"))
    extra = ()
    t = e.dxftype()
    if t == "LWPOLYLINE":
        extra = tuple(tuple(round(c, 6) for c in p) for p in e.get_points())
    elif t == "MTEXT":
        extra = (e.text,)
    elif t in ("SPLINE",):
        extra = tuple(tuple(round(c, 6) for c in p) for p in e.control_points)
    elif t == "HATCH":
        extra = (len(e.paths),)
    return (t, tuple(attribs), extra)


def model_details(doc):
    msp = doc.modelspace()
    cache = bbox.Cache()
    boxes = []
    for e in msp:
        try:
            b = bbox.extents([e], cache=cache, fast=True)
        except Exception:
            continue
        if b.has_data:
            boxes.append((e, b))
    keep, manifest, seen = set(), [], set()
    x = DETAIL_ORIGIN[0]
    for name, kind, attrs, w, scale in DETAILS:
        chosen = []
        for e, b in boxes:
            if inside(b, w):
                k = key(e)
                if (name, k) in seen:
                    continue
                seen.add((name, k))
                chosen.append(e)
        if not chosen:
            raise SystemExit("detail %s: no entity in its window" % name)
        dx, dy = x - w[0], DETAIL_ORIGIN[1] - w[1]
        m = Matrix44.translate(dx, dy, 0)
        for e in chosen:
            if e.dxf.handle in keep:  # an entity inside two windows: copy it for the second detail
                c = e.copy()
                msp.add_entity(c)
                c.transform(m)
                keep.add(c.dxf.handle)
            else:
                keep.add(e.dxf.handle)
                e.transform(m)
        width, height = w[2] - w[0], w[3] - w[1]
        manifest.append({
            "Name": name, "Kind": kind, "Attributes": attrs, "TemplateScale": scale, "Entities": len(chosen),
            "Window": [round(x, 3), round(DETAIL_ORIGIN[1], 3), round(x + width, 3), round(DETAIL_ORIGIN[1] + height, 3)],
        })
        x += width + DETAIL_GAP
    # everything else (the rest of the library, entities without extents) is dropped
    for e in list(msp):
        if e.dxf.handle not in keep:
            msp.delete_entity(e)
    return manifest


def paper(doc):
    old = [l for l in doc.layouts.names_in_taborder() if l != "Model"]
    if len(old) != 1:
        raise SystemExit("expected one paper layout, found %s" % old)
    layout = doc.layouts.get(old[0])
    border = [e for e in layout.query("LINE[layer=='0']")]
    xs = [p.x for e in border for p in (e.dxf.start, e.dxf.end)]
    ys = [p.y for e in border for p in (e.dxf.start, e.dxf.end)]
    got = (min(xs), min(ys), max(xs), max(ys))
    if any(abs(a - b) > 0.01 for a, b in zip(got, FRAME)):
        raise SystemExit("sheet border moved: %s" % (got,))
    s = PAPER_H / (BORDER[3] - BORDER[1])
    tx = (PAPER_W - (BORDER[2] - BORDER[0]) * s) / 2 - BORDER[0] * s
    ty = -BORDER[1] * s
    m = Matrix44.chain(Matrix44.scale(s, s, s), Matrix44.translate(tx, ty, 0))
    to_mm = lambda p: (p[0] * s + tx, p[1] * s + ty)

    for e in list(layout):
        t = e.dxftype()
        drop = e.dxf.handle in VALUE_TEXTS or t in ("VIEWPORT", "OLE2FRAME") or e.dxf.layer == "Text"
        if not drop and t not in ("VIEWPORT",):
            try:
                b = bbox.extents([e])
                drop = b.has_data and inside(b, BAR_SCALE)
            except Exception:
                pass
        if drop:
            layout.delete_entity(e)
    missing = [h for h in VALUE_TEXTS if doc.entitydb.get(h) is not None and doc.entitydb.get(h).is_alive]
    if missing:
        raise SystemExit("value texts not removed: %s" % missing)
    for e in layout:
        e.transform(m)

    doc.layouts.rename(old[0], LAYOUT_NAME)
    layout = doc.layouts.get(LAYOUT_NAME)
    layout.page_setup(size=(PAPER_W, PAPER_H), margins=(0, 0, 0, 0), units="mm", scale=(1, 1), name="A0 landscape", device="")
    d = layout.dxf_layout.dxf
    d.plot_configuration_file = ""
    d.current_style_sheet = ""
    d.plot_rotation = 0
    d.plot_type = 5  # layout
    for l in doc.layouts:  # the old printer is also named in each layout's plot-settings extension data
        if l.dxf_layout.has_xdata("ACAD_PSEXT"):
            l.dxf_layout.discard_xdata("ACAD_PSEXT")

    # logo area: a no-plot frame where the company logo image goes
    doc.layers.add("SM_LOGO_AREA", color=8).dxf.plot = 0
    a0, a1 = to_mm(LOGO_AREA[:2]), to_mm(LOGO_AREA[2:])
    layout.add_lwpolyline([a0, (a1[0], a0[1]), a1, (a0[0], a1[1])], close=True, dxfattribs={"layer": "SM_LOGO_AREA"})

    # SM_TITLEBLOCK: one attribute definition per value field, inserted once at the paper origin
    block = doc.blocks.new(BLOCK_NAME)
    for tag, pos, h, align, style, prompt in ATTRIBUTES:
        a = block.add_attdef(tag, dxfattribs={"height": h * s, "style": style, "layer": "ACDC BANDA", "prompt": prompt})
        a.set_placement(to_mm(pos), align=align)
        a.dxf.text = ""
    ref = layout.add_blockref(BLOCK_NAME, (0, 0), dxfattribs={"layer": "ACDC BANDA"})
    ref.add_auto_attribs({})
    return s


def styles(doc):
    doc.styles.add(STYLE_AR, font="xarab.shx")
    doc.styles.add(STYLE_EN, font="cent.shx")
    for old, new in TEXT_STYLE_RENAMES.items():
        doc.styles.duplicate_entry(old, new)
    for old, new in DIM_STYLE_RENAMES.items():
        if old in doc.dimstyles:
            doc.dimstyles.duplicate_entry(old, new)

    def fix(entity_space):
        for e in entity_space:
            if e.dxf.hasattr("style") and e.dxf.style in TEXT_STYLE_RENAMES:
                e.dxf.style = TEXT_STYLE_RENAMES[e.dxf.style]
            if e.dxftype() == "DIMENSION" and e.dxf.dimstyle in DIM_STYLE_RENAMES:
                e.dxf.dimstyle = DIM_STYLE_RENAMES[e.dxf.dimstyle]
            if e.dxftype() == "MTEXT":
                pass
    for b in doc.blocks:
        fix(b)
    current = doc.header.get("$DIMSTYLE", "Standard")
    doc.header["$DIMSTYLE"] = DIM_STYLE_RENAMES.get(current, current)
    doc.header["$TEXTSTYLE"] = STYLE_EN
    for old in DIM_STYLE_RENAMES:
        if old != "Standard" and old in doc.dimstyles:
            doc.dimstyles.remove(old)


def purge_blocks(doc):
    """Removes block definitions that nothing references any more (the library's)."""
    used = set()

    def walk(space):
        for e in space:
            if e.dxftype() == "INSERT":
                if e.dxf.name not in used:
                    used.add(e.dxf.name)
                    walk(doc.blocks.get(e.dxf.name))
            elif e.dxftype() == "DIMENSION" and e.dxf.hasattr("geometry"):
                used.add(e.dxf.geometry)
    walk(doc.modelspace())
    for l in doc.layouts:
        walk(l)
    for ds in doc.dimstyles:  # arrowheads named by the dimension styles
        for a in ("dimblk", "dimblk1", "dimblk2", "dimldrblk"):
            if ds.dxf.hasattr(a) and ds.dxf.get(a):
                used.add(ds.dxf.get(a))
    for b in list(doc.blocks):
        n = b.name
        if b.is_any_layout or n in used or n.startswith("*D") and n in used:
            continue
        if n.upper().startswith("*") and not n.upper().startswith("*D") and not n.upper().startswith("*U"):
            continue
        doc.blocks.delete_block(n, safe=False)


NUMBER = re.compile(r"^(\s*(?:1[0-9]|2[0-9]|3[0-9]|4[0-9]|5[0-9])\n)(-?\d+\.\d{4,})$", re.M)


def round_coordinates(text):
    """Writes coordinate and size groups (10-59) with 3 decimals (0.001 mm in model and paper space): the same drawing, a much
    smaller file, still deterministic."""
    def repl(m):
        v = ("%.3f" % float(m.group(2))).rstrip("0").rstrip(".")
        return m.group(1) + ("0" if v in ("-0", "") else v)
    return NUMBER.sub(repl, text)


def slim(doc):
    """Drops what the reduced file no longer uses: the association network (center-mark actions of removed library entities),
    the image definitions (no IMAGE entity is left) and, on paper space, explicit colour / linetype values equal to their layer's
    (the same look, written once on the layer)."""
    root = doc.rootdict
    for name in ("ACAD_ASSOCNETWORK", "ACAD_IMAGE_DICT"):
        if name in root:
            root.remove(name)
    for e in list(doc.objects):
        if e.dxftype().startswith("ACDBASSOC") or e.dxftype() in ("ACDBCENTERMARKACTIONBODY", "IMAGEDEF", "IMAGEDEF_REACTOR"):
            doc.entitydb.delete_entity(e)
    for d in doc.objects.query("DICTIONARY"):
        if not d.is_alive:
            continue
        for k, v in list(d.items()):
            if not isinstance(v, str) and not v.is_alive:
                d.discard(k)
    doc.objects.purge()
    layout = doc.layouts.get(LAYOUT_NAME)
    for e in layout:
        if e.dxf.layer not in doc.layers:
            continue
        layer = doc.layers.get(e.dxf.layer)
        if e.dxf.get("linetype", "BYLAYER") == layer.dxf.linetype:
            e.dxf.discard("linetype")
        if e.dxf.get("color", 256) == layer.dxf.color:
            e.dxf.discard("color")


def main(source=SOURCE, target=TARGET):
    ezdxf.options.write_fixed_meta_data_for_testing = True
    doc, auditor = recover.readfile(source)
    manifest = model_details(doc)
    s = paper(doc)
    styles(doc)
    purge_blocks(doc)
    slim(doc)
    doc.header["$INSUNITS"] = 4  # millimetres
    doc.header["$MEASUREMENT"] = 1
    for v in ("$TDCREATE", "$TDUPDATE", "$TDUCREATE", "$TDUUPDATE"):
        if v in doc.header:
            doc.header[v] = 2460000.0
    doc.audit()
    # ezdxf adds the required CLASS definitions on save from a set (hash order): keep them sorted so the bytes do not change.
    add_required = doc.classes.add_required_classes

    def add_required_sorted(version):
        add_required(version)
        doc.classes.classes = dict(sorted(doc.classes.classes.items()))
    doc.classes.add_required_classes = add_required_sorted
    os.makedirs(os.path.dirname(target), exist_ok=True)
    tmp = target + ".tmp"
    doc.saveas(tmp, encoding="utf-8")
    with open(tmp, "r", encoding="utf-8", newline="") as f:
        text = f.read()
    with open(target, "w", encoding="utf-8", newline="") as f:
        f.write(round_coordinates(text))
    os.remove(tmp)
    with open(os.path.splitext(target)[0] + ".details.json", "w", encoding="utf-8", newline="\n") as f:
        json.dump({"Origin": DETAIL_ORIGIN, "PaperScaleFromSourceUnits": round(s, 6), "Details": manifest}, f, indent=2)
        f.write("\n")
    print("wrote %s (%.2f MB), %d details" % (target, os.path.getsize(target) / 1e6, len(manifest)))


if __name__ == "__main__":
    main(*sys.argv[1:3])
