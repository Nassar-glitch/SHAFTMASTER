"""L2b evidence: plots the layout SM_Geared_A0 of generated DWGs to PNG through the AutoCAD 2023 Core Console.

usage: plot_layouts.py <out folder> <dwg> [<dwg> ...] [<zoom name>=<x0>,<y0>,<x1>,<y1> ...]

A zoom is a rectangle of the sheet in fractions of its width and height (y from the top): <name>-<zoom>.png at 130 dpi.

For each DWG: a copy in a short path without spaces (a space ends a script answer), -PLOT of the layout with DWG To PDF.pc3 on ISO A0
landscape, monochrome.ctb, layout area, 1:1; the PDF is rasterised with PyMuPDF to <name>-sheet.png (the whole sheet) and
<name>-title-block.png (a zoom on the title-block column). Read-only on the DWGs.
"""
import os
import shutil
import subprocess
import sys
import tempfile

import pymupdf
from PIL import Image

CORE_CONSOLE = r"C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe"
LAYOUT = "SM_Geared_A0"


def plot(dwg, pdf):
    work = tempfile.mkdtemp(prefix="plt", dir=os.environ.get("PLOT_WORK") or None)
    src, out, scr = os.path.join(work, "in.dwg"), os.path.join(work, "out.pdf"), os.path.join(work, "run.scr")
    shutil.copyfile(dwg, src)
    # a logo image is linked from the DWG (relative to its folder): the copy gets the images that sit beside the original
    for f in os.listdir(os.path.dirname(dwg)):
        if f.lower().endswith((".png", ".jpg", ".jpeg")):
            shutil.copyfile(os.path.join(os.path.dirname(dwg), f), os.path.join(work, f))
    answers = ["FILEDIA", "0", "-PLOT", "Y", LAYOUT, "DWG To PDF.pc3", "ISO full bleed A0 (841.00 x 1189.00 MM)", "Millimeters", "Landscape", "N",
               "Layout", "1=1", "0,0", "Y", "monochrome.ctb", "Y", "N", "N", "N", out, "N", "Y", ""]
    with open(scr, "w", newline="") as f:
        f.write("\r\n".join(answers))
    log = os.path.join(work, "console.log")
    with open(log, "w") as lf:
        try:
            subprocess.run([CORE_CONSOLE, "/i", src, "/s", scr, "/l", "en-US"], stdout=lf, stderr=subprocess.STDOUT, timeout=300)
        except subprocess.TimeoutExpired:
            pass
    if not os.path.exists(out):
        text = open(log, "rb").read().replace(b"\x00", b"").decode("utf-8", "replace")
        raise RuntimeError("no PDF for " + dwg + "\n" + text[-1800:])
    shutil.copyfile(out, pdf)
    shutil.rmtree(work, ignore_errors=True)


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    zooms = [a for a in sys.argv[2:] if "=" in a]
    for dwg in [a for a in sys.argv[2:] if "=" not in a]:
        name = os.path.splitext(os.path.basename(dwg))[0]
        pdf = os.path.join(out, name + ".pdf")
        plot(dwg, pdf)
        doc = pymupdf.open(pdf)
        page = doc[0]
        # the sheet: 150 dpi is about 7000 px wide; 60 dpi keeps the PNG readable and small
        full = page.get_pixmap(dpi=60)
        full.save(os.path.join(out, name + "-sheet.png"))
        # the title-block column is the right 13 % of the A0 sheet, about 157 mm wide
        w, h = page.rect.width, page.rect.height
        clip = pymupdf.Rect(w * 0.855, 0, w, h)
        page.get_pixmap(dpi=110, clip=clip).save(os.path.join(out, name + "-title-block.png"))
        for z in zooms:
            zname, box = z.split("=")
            x0, y0, x1, y1 = [float(v) for v in box.split(",")]
            page.get_pixmap(dpi=130, clip=pymupdf.Rect(w * x0, h * y0, w * x1, h * y1)).save(os.path.join(out, name + "-" + zname + ".png"))
        rect = page.rect
        doc.close()
        os.remove(pdf)
        print(name, "ok", rect)


main()


