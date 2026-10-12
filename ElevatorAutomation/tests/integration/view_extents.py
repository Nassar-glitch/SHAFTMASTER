"""Layout spike (L2b): the model extents of every generated view, dimensions and labels included.

usage: view_extents.py <folder written by ViewExtentsSpike> <view-extents.json to write>

For each <id>.dwg: DWG -> DXF with the AutoCAD Core Console (read only), then ezdxf. Every model entity carries its semantic key
(XDATA ELEVATOR_AUTOMATION, e.g. Plan.Shaft.InnerOutline); the key's first part names the view:
Plan -> plan, MachineRoom -> machine-room plan, SectionAA -> A-A, MachineRoomSection -> B-B. Per view: extents of all its entities
(geometry, dimensions and text), extents of the geometry alone (no Dimension / Annotation / Text keys), size, entity counts, and the
bounds the generator itself reports (GenerationResponse.Bounds), so the arrangement engine can see how much the dimensions add.
Text extents are ezdxf's estimate (cap height x characters), dimensions are measured from their rendered geometry.
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile

import ezdxf
from ezdxf import bbox

CORE_CONSOLE = r"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe"
VIEWS = [("Plan", "plan", 1), ("MachineRoom", "machineRoomPlan", 2), ("SectionAA", "sectionAA", 4), ("MachineRoomSection", "sectionBB", 8)]
ANNOTATION_PARTS = ("Dimension", "Annotation", "Annotations", "Label", "Text", "Leader", "Mark", "Title", "Note")


def to_dxf(dwg, dxf):
    work = tempfile.mkdtemp(prefix="vx_")
    src, out, scr = os.path.join(work, "in.dwg"), os.path.join(work, "out.dxf"), os.path.join(work, "run.scr")
    shutil.copyfile(dwg, src)
    with open(scr, "w", newline="") as f:
        f.write("\r\n".join(["FILEDIA", "0", "DXFOUT", out, "16", ""]))
    try:
        subprocess.run([CORE_CONSOLE, "/i", src, "/s", scr, "/l", "en-US"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=200)
    except subprocess.TimeoutExpired:
        pass
    if not os.path.exists(out):
        raise RuntimeError("DXF not written for " + dwg)
    shutil.copyfile(out, dxf)
    shutil.rmtree(work, ignore_errors=True)


def key(e):
    if not e.xdata or "ELEVATOR_AUTOMATION" not in e.xdata:
        return None
    return e.get_xdata("ELEVATOR_AUTOMATION")[0].value


def entity_box(e):
    try:
        b = bbox.extents([e], fast=False)
        if b.has_data:
            return b
    except Exception:
        pass
    return None


def union(a, b):
    return b if a is None else a.union(b)


def rect(b):
    if b is None:
        return None
    return {"minX": round(b.extmin.x, 1), "minY": round(b.extmin.y, 1), "maxX": round(b.extmax.x, 1), "maxY": round(b.extmax.y, 1),
            "width": round(b.size.x, 1), "height": round(b.size.y, 1)}


def main():
    folder, out_path = sys.argv[1], sys.argv[2]
    cases = []
    for name in sorted(os.listdir(folder)):
        if not name.endswith(".dwg"):
            continue
        case = name[:-4]
        dxf = os.path.join(folder, case + ".dxf")
        if not os.path.exists(dxf):
            to_dxf(os.path.join(folder, name), dxf)
        expected = json.load(open(os.path.join(folder, case + "-expected.json"), encoding="utf-8-sig"))
        generated = json.load(open(os.path.join(folder, case + "-generation.json"), encoding="utf-8-sig"))
        bounds = {b["View"]: b for b in generated["Bounds"]}
        doc = ezdxf.readfile(dxf)
        per = {v[0]: {"all": None, "geometry": None, "n": 0, "annotations": 0, "unmeasured": 0} for v in VIEWS}
        for e in doc.modelspace():
            k = key(e)
            if k is None:
                continue
            root = k.split(".")[0]
            if root not in per:
                continue
            p = per[root]
            p["n"] += 1
            b = entity_box(e)
            annotation = any(part in ANNOTATION_PARTS for part in k.split(".")[1:]) or e.dxftype() in ("DIMENSION", "TEXT", "MTEXT", "MULTILEADER", "LEADER")
            if annotation:
                p["annotations"] += 1
            if b is None:
                p["unmeasured"] += 1
                continue
            p["all"] = union(p["all"], b)
            if not annotation:
                p["geometry"] = union(p["geometry"], b)
        views = {}
        for root, name_, flag in VIEWS:
            p = per[root]
            g = bounds.get(flag)
            views[name_] = {
                "keyPrefix": root,
                "entities": p["n"],
                "annotationEntities": p["annotations"],
                "unmeasuredEntities": p["unmeasured"],
                "extentsWithDimensionsAndLabels": rect(p["all"]),
                "extentsGeometryOnly": rect(p["geometry"]),
                "generatorBounds": None if g is None else {"minX": round(g["MinX"], 1), "minY": round(g["MinY"], 1), "maxX": round(g["MaxX"], 1), "maxY": round(g["MaxY"], 1)},
            }
        plan = expected["Plan"]
        cases.append({"id": case, "stops": expected["Stops"], "shaftWidthMm": plan.get("SW"), "shaftDepthMm": plan.get("SD"), "views": views})
    result = {
        "note": "Model-space millimetres of the generated four-view DWG (ArrangeViews on). extentsWithDimensionsAndLabels is the extents of every "
                "entity of the view (dimensions and text included, text width estimated by ezdxf); extentsGeometryOnly leaves the dimension and "
                "annotation entities out; generatorBounds is GenerationResponse.Bounds (the placement bounds the generator uses).",
        "method": "AutoCAD 2023 Core Console GenerationClient -> DWG -> DXFOUT -> ezdxf, views told apart by the semantic key prefix.",
        "cases": cases,
    }
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(result, f, indent=2)
    for c in cases:
        print(c["id"])
        for n, v in c["views"].items():
            r = v["extentsWithDimensionsAndLabels"]
            print("  %-16s %5d entities  %s" % (n, v["entities"], "%.0f x %.0f" % (r["width"], r["height"]) if r else "none"))


main()
