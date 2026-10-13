"""L3 tier 3 (layouts-pdf): reads the PDFs the output-set run wrote and checks them.

usage: verify_pdf.py <evidence folder> <png folder> <result json>

Per case of layouts-pdf-cases.json:
  * the PDF has one page of 1189 x 841 mm (+-1 mm);
  * its text holds the project name and "P1 =" (SHX text plotted by AutoCAD is stored as text annotations when PDFSHX is on; TrueType
    text is page text; the Arabic project name is compared in its drawing encoding as well);
  * the logo image object exists only when a logo is set, and it lies inside the logo area of the sheet;
  * page 1 is rendered to <png folder>/<case>-pdf.png (checked by eye).
"""
import json
import os
import sys

import pymupdf

MM = 72.0 / 25.4
failures = []
checks = 0


def check(ok, message):
    global checks
    if ok:
        checks += 1
    else:
        failures.append(message)
        print("FAIL: " + message)


def page_text(page):
    parts = [page.get_text()]
    for annot in page.annots() or []:
        info = annot.info or {}
        parts.append(info.get("content", "") or "")
        parts.append(info.get("title", "") or "")
    return "\n".join(parts)


def main(folder, png, result):
    cases = json.load(open(os.path.join(folder, "layouts-pdf-cases.json"), encoding="utf-8-sig"))
    report = []
    for c in cases:
        cid = c["Id"]
        doc = pymupdf.open(c["Pdf"])
        try:
            check(doc.page_count == 1, cid + ": the PDF has one page (%d)" % doc.page_count)
            page = doc[0]
            w, h = page.rect.width / MM, page.rect.height / MM
            check(abs(w - 1189) <= 1 and abs(h - 841) <= 1, cid + ": the page is 1189 x 841 mm (%.1f x %.1f)" % (w, h))
            text = page_text(page)
            has_name = c["ProjectName"] in text or (c.get("ProjectNameInDrawing") or "<none>") in text
            check(has_name, cid + ": the text holds the project name (%r)" % (c.get("ProjectNameInDrawing"),))
            check("P1 =" in text, cid + ": the text holds 'P1 ='")
            images = page.get_images(full=True)
            check((len(images) > 0) == bool(c["HasLogo"]), cid + ": the logo image object exists only with a logo (%d images, logo=%s)" % (len(images), c["HasLogo"]))
            if c["HasLogo"] and images:
                infos = page.get_image_info()
                check(len(infos) >= 1, cid + ": the logo image is on the page")
                if infos:
                    # image boxes are in the unrotated media box (origin top left): the sheet is a portrait media box plotted with
                    # /Rotate 270, so displayed X = box y and displayed Y (up from the bottom) = box x
                    r = pymupdf.Rect(infos[0]["bbox"])
                    ax, ay, aw, ah = c["LogoAreaX"], c["LogoAreaY"], c["LogoAreaWidth"], c["LogoAreaHeight"]
                    if page.rotation == 270:
                        left, right = r.y0 / MM, r.y1 / MM
                        bottom_up, top_up = r.x0 / MM, r.x1 / MM
                    else:
                        left, right = r.x0 / MM, r.x1 / MM
                        bottom_up, top_up = h - r.y1 / MM, h - r.y0 / MM
                    inside = left >= ax - 0.6 and right <= ax + aw + 0.6 and bottom_up >= ay - 0.6 and top_up <= ay + ah + 0.6
                    check(inside, cid + ": the logo lies in the logo area (%.1f..%.1f x %.1f..%.1f vs %.1f,%.1f %.1f x %.1f)" % (left, right, bottom_up, top_up, ax, ay, aw, ah))
                    # centred, aspect kept (the logo is 2:1)
                    check(abs((right - left) / max(top_up - bottom_up, 1e-6) - 2.0) < 0.05, cid + ": the logo keeps its aspect ratio")
                    check(abs((left + right) / 2 - (ax + aw / 2)) < 0.6 and abs((bottom_up + top_up) / 2 - (ay + ah / 2)) < 0.6, cid + ": the logo is centred in its area")
            os.makedirs(png, exist_ok=True)
            pix = page.get_pixmap(dpi=60)
            pix.save(os.path.join(png, cid + "-pdf.png"))
            report.append({"Id": cid, "Pages": doc.page_count, "WidthMm": round(w, 1), "HeightMm": round(h, 1), "HasName": has_name,
                           "Images": len(images), "TextSample": text[:200]})
        finally:
            doc.close()
    json.dump({"Checks": checks, "Failures": failures, "Cases": report}, open(result, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    print("%d checks, %d failures" % (checks, len(failures)))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2], sys.argv[3]))
