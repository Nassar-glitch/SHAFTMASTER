"""Read-only inspection of a layout template DXF (L1 analysis). Never writes the DXF.

    python -I inspect_layout_dxf.py <file.dxf> [layout name]

Prints, for the paper-space layout: plot settings, every viewport (paper rectangle, scale, model-space window and the
number of model entities inside it), and every TEXT / MTEXT with its position. Needs ezdxf (tested with 1.4.4).
Viewport model window: the DXF stores the view centre in the viewport's DCS, whose origin is the view target, so the model
centre is view_target + view_center (plan views, twist 0).
"""
import collections
import sys

import ezdxf
from ezdxf import bbox

CM_PER_INCH = 2.54


def main(path, layout_name=None):
    doc = ezdxf.readfile(path)
    names = [l.name for l in doc.layouts if l.name != "Model"]
    layout = doc.layouts.get(layout_name or names[0])
    d = layout.dxf_layout.dxf
    unit_mm = 25.4 / d.scale_denominator if d.get("plot_paper_units") == 0 else 1.0 / d.scale_denominator
    print("file", path, "dxf", doc.dxfversion, "layouts", names)
    print("paper %.1f x %.1f mm, rotation %s, pc3 %s, style %s, plot_type %s" % (
        d.paper_width, d.paper_height, d.plot_rotation, d.plot_configuration_file, d.current_style_sheet, d.plot_type))
    print("plot scale %s : %.4f in %s -> 1 paper unit = %.3f mm" % (
        d.scale_numerator, d.scale_denominator, "inches" if d.plot_paper_units == 0 else "mm", unit_mm))
    print("plot window", [round(d.get(k), 1) for k in ("plot_window_x1", "plot_window_y1", "plot_window_x2", "plot_window_y2")])

    model = doc.modelspace()
    boxes = []
    for e in model:
        try:
            b = bbox.extents([e])
        except Exception:
            continue
        if b.has_data:
            boxes.append((e, b))
    print("model entities", len(model), "with extents", len(boxes))

    for e in layout.query("VIEWPORT"):
        v = e.dxf
        if v.id == 1:
            continue
        vh = v.view_height
        vw = vh * v.width / v.height
        cx = v.view_center_point.x + v.view_target_point.x
        cy = v.view_center_point.y + v.view_target_point.y
        x0, x1, y0, y1 = cx - vw / 2, cx + vw / 2, cy - vh / 2, cy + vh / 2
        inside = collections.Counter()
        for ent, b in boxes:
            if not (b.extmax.x < x0 or b.extmin.x > x1 or b.extmax.y < y0 or b.extmin.y > y1):
                inside[ent.dxf.layer] += 1
        ratio = v.height / vh
        print("VP%-2d paper centre (%.1f,%.1f) size %.1f x %.1f units = %.0f x %.0f mm | scale 1:%.2f | model x[%.0f,%.0f] y[%.0f,%.0f] | %d entities %s" % (
            v.id, v.center.x, v.center.y, v.width, v.height, v.width * unit_mm, v.height * unit_mm,
            1.0 / (ratio * unit_mm), x0, x1, y0, y1, sum(inside.values()), dict(inside.most_common(4))))

    for e in layout:
        t = e.dxftype()
        if t == "TEXT":
            print("TEXT  (%.1f,%.1f) h=%.2f %-12s %s %r" % (e.dxf.insert.x, e.dxf.insert.y, e.dxf.height, e.dxf.style, e.dxf.layer, e.dxf.text))
        elif t == "MTEXT":
            print("MTEXT (%.1f,%.1f) h=%.2f w=%.1f %r" % (e.dxf.insert.x, e.dxf.insert.y, e.dxf.char_height, e.dxf.get("width", 0), e.text))
        elif t == "OLE2FRAME":
            print("OLE2FRAME", {k: e.dxf.get(k) for k in e.dxf.all_existing_dxf_attribs()})


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
