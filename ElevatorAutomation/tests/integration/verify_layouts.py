"""L2b tier 3 (layouts-native): reads the generated sheets back from the DWGs.

usage: verify_layouts.py <folder LayoutsNativeTests wrote> [<summary json to write>]

For each <id>.dwg (DWG -> DXF with the AutoCAD Core Console, read only; then ezdxf) and its <id>-expected.json:
  * the layout SM_Geared_A0 exists, and is the only layout added (the master has Model, Layout1, Layout2);
  * every viewport is inside the drawing area, shows a standard scale (1 : 10 / 20 / 25 / 50 / 75 / 100 / 150 / 200) and is locked,
    and no two viewports overlap;
  * every attribute of SM_TITLEBLOCK equals the TitleBlockData value the test computed (the xarab codes for Arabic), in the cent / xarab style;
  * a scale label "1:N" under each viewport;
  * the logo image is on the layout;
  * model space: every tagged view entity is still there (count per view equals the host's), and the reference details are untagged.
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile

import ezdxf

CORE_CONSOLE = r"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe"
STANDARD = (10, 20, 25, 50, 75, 100, 150, 200)
failures = []
checks = 0


def check(ok, name):
    global checks
    checks += 1
    if not ok:
        failures.append(name)
        print("FAIL:", name)


def to_dxf(dwg, dxf):
    work = tempfile.mkdtemp(prefix="lay_")
    src, out, scr = os.path.join(work, "in.dwg"), os.path.join(work, "out.dxf"), os.path.join(work, "run.scr")
    shutil.copyfile(dwg, src)
    with open(scr, "w", newline="") as f:
        f.write("\r\n".join(["FILEDIA", "0", "DXFOUT", out, "16", ""]))
    try:
        subprocess.run([CORE_CONSOLE, "/i", src, "/s", scr, "/l", "en-US"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=240)
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


def overlaps(a, b):
    return a[0] < b[2] - 1e-6 and b[0] < a[2] - 1e-6 and a[1] < b[3] - 1e-6 and b[1] < a[3] - 1e-6


def run_case(folder, case_id):
    expected = json.load(open(os.path.join(folder, case_id + "-expected.json"), encoding="utf-8-sig"))
    dxf = os.path.join(folder, case_id + ".dxf")
    to_dxf(os.path.join(folder, case_id + ".dwg"), dxf)
    doc = ezdxf.readfile(dxf)
    names = [l.name for l in doc.layouts]
    check(expected["Layout"] in names, case_id + ": layout " + expected["Layout"] + " exists")
    # the master drawing already has Layout1 and Layout2; the sheet is the only layout the generation adds
    check(sorted(n for n in names if n not in ("Model", "Layout1", "Layout2")) == [expected["Layout"]], case_id + ": the sheet is the only layout added " + str(names))
    lay = doc.layouts.get(expected["Layout"])
    area = expected["Area"]
    ax0, ay0 = area["DrawingAreaX"], area["DrawingAreaY"]
    ax1, ay1 = ax0 + area["DrawingAreaWidth"], ay0 + area["DrawingAreaHeight"]

    # viewports
    vps = [e for e in lay if e.dxftype() == "VIEWPORT" and e.dxf.id != 1]
    check(len(vps) == len(expected["Viewports"]), case_id + ": %d viewports as the host reported (%d)" % (len(vps), len(expected["Viewports"])))
    rects = []
    for v in vps:
        d = v.dxf
        w, h = d.width, d.height
        r = (d.center.x - w / 2, d.center.y - h / 2, d.center.x + w / 2, d.center.y + h / 2)
        rects.append(r)
        check(r[0] >= ax0 - 1e-3 and r[1] >= ay0 - 1e-3 and r[2] <= ax1 + 1e-3 and r[3] <= ay1 + 1e-3,
              case_id + ": viewport at (%.0f, %.0f) %.0f x %.0f inside the drawing area" % (r[0], r[1], w, h))
        den = d.view_height / h
        check(any(abs(den - s) / s < 0.002 for s in STANDARD), case_id + ": viewport scale 1:%.2f is a standard scale" % den)
        check(bool(d.flags & 16384), case_id + ": viewport scale is locked (flags %d)" % d.flags)
    for i in range(len(rects)):
        for j in range(i + 1, len(rects)):
            check(not overlaps(rects[i], rects[j]), case_id + ": viewports %d and %d do not overlap" % (i, j))
    # the host's own report matches the DWG
    exp = sorted((round(v["PaperX"], 1), round(v["PaperY"], 1), v["ScaleDenominator"]) for v in expected["Viewports"])
    got = sorted((round(r[0], 1), round(r[1], 1), round(1.0 * v.dxf.view_height / v.dxf.height + 1e-9)) for r, v in zip(rects, vps))
    check(exp == got, case_id + ": viewport positions and scales equal the arrangement")

    # scale labels: only under a view that has no title of its own stating the scale
    labels = [e.dxf.text for e in lay if e.dxftype() == "TEXT" and e.dxf.text.startswith("1:")]
    want = sorted(v["Label"] for v in expected["Viewports"] if v["HasLabel"])
    check(sorted(labels) == want, case_id + ": a scale label only under the views without their own title " + str(sorted(labels)))
    # one title per view with the real scale: the four view titles in model space
    titles = {}
    for e in doc.modelspace():
        if e.dxftype() != "MTEXT":
            continue
        t = tag(e)
        if t and "1:" in e.text and any(k in e.text for k in ("SHAFT AND CAR", "MACHINE ROOM", "SEC AT")):
            titles[t.split(".")[0]] = e.text
    scale_of = {v["Name"]: v["Label"] for v in expected["Viewports"]}
    for root, view in (("Plan", "Shaft plan"), ("MachineRoom", "Machine-room plan"), ("SectionAA", "Section A-A"), ("MachineRoomSection", "Section B-B")):
        check(root in titles and scale_of[view] in titles[root], case_id + ": the title of %s states %s (%r)" % (view, scale_of[view], titles.get(root)))
        for old in ("1:8", "1:35"):
            check(root not in titles or (old + "}") not in titles[root], case_id + ": no old scale %s in the title of %s" % (old, view))
    # the fit report: every attribute inside its box
    for f in expected.get("Fit", []):
        check(f["Fits"], case_id + ": attribute %s fits its box" % f["Tag"])

    # title block
    block = [e for e in lay if e.dxftype() == "INSERT" and e.dxf.name == "SM_TITLEBLOCK"]
    check(len(block) == 1, case_id + ": one SM_TITLEBLOCK")
    values = {a.dxf.tag: a for a in block[0].attribs} if block else {}
    for a in expected["Attributes"]:
        att = values.get(a["Tag"])
        check(att is not None, case_id + ": attribute " + a["Tag"] + " present")
        if att is None:
            continue
        got = att.dxf.text
        if a["Tag"].endswith("_2"):
            continue  # a second line: part of its first attribute below
        second = values.get(a["Tag"] + "_2")
        if second is not None and second.dxf.text:
            got = got + " " + second.dxf.text  # a long value takes two lines (the value is the first line, a space, the second line)
        check(" ".join(got.split()) == " ".join(a["DrawingText"].split()), case_id + ": attribute %s = %r (expected %r)" % (a["Tag"], got, a["DrawingText"]))
        if a["DrawingText"]:
            check(att.dxf.style == a["Style"], case_id + ": attribute %s in style %s (%s)" % (a["Tag"], a["Style"], att.dxf.style))
    check(len(values) == len(expected["Attributes"]), case_id + ": every attribute of the block is a title-block field")

    # logo
    check(any(e.dxftype() == "IMAGE" for e in lay), case_id + ": the logo image is on the layout")

    # model space: the semantic views and the untagged reference details
    counts = {}
    untagged = 0
    for e in doc.modelspace():
        t = tag(e)
        if t is None:
            untagged += 1
            continue
        root = t.split(".")[0]
        view = {"Plan": "Plan", "MachineRoom": "MachineRoomPlan", "SectionAA": "SectionAA", "MachineRoomSection": "SectionBB"}.get(root)
        if view:
            counts[view] = counts.get(view, 0) + 1
    check(counts == expected["EntityCounts"], case_id + ": tagged entities per view equal the host's counts " + str(counts))
    check(untagged > 0, case_id + ": the reference details are in model space (%d untagged entities)" % untagged)
    return {"id": case_id, "viewports": len(vps), "attributes": len(values), "untaggedModelEntities": untagged}


def main():
    folder = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else None
    ids = sorted(n[:-len("-expected.json")] for n in os.listdir(folder) if n.endswith("-expected.json"))
    check(len(ids) >= 5, "five sheets were generated")
    summary = [run_case(folder, i) for i in ids]
    if out:
        json.dump({"checks": checks, "failures": failures, "cases": summary}, open(out, "w"), indent=2)
    print("%d checks, %d failures" % (checks, len(failures)))
    return 1 if failures else 0


sys.exit(main())
