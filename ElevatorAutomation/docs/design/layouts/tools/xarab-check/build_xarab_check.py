import json, unicodedata, ezdxf
from ezdxf.enums import TextEntityAlignment
forms = json.load(open("forms.json", encoding="utf-8-sig"))
doc = ezdxf.new("R2018", setup=False)
doc.styles.add("SM_AR", font="xarab.shx")
doc.styles.add("SM_EN", font="cent.shx")
doc.styles.add("REF", font="arial.ttf")
msp = doc.modelspace()
H = 7
def label(text, x, y, style="SM_EN", h=H, align=TextEntityAlignment.LEFT):
    t = msp.add_text(text, dxfattribs={"style": style, "height": h})
    t.set_placement((x, y), align=align)
RIGHT_JOIN = set("اأإآدذرزوؤةء")
def ref(letter, form):
    if letter == "لا":
        names = {"isolated": "ARABIC LIGATURE LAM WITH ALEF ISOLATED FORM", "initial": "ARABIC LIGATURE LAM WITH ALEF ISOLATED FORM",
                 "medial": "ARABIC LIGATURE LAM WITH ALEF FINAL FORM", "final": "ARABIC LIGATURE LAM WITH ALEF FINAL FORM"}
        return unicodedata.lookup(names[form])
    base = unicodedata.name(letter)  # ARABIC LETTER BEH
    f = form.upper()
    for suffix in (f + " FORM",):
        try:
            return unicodedata.lookup(base + " " + suffix)
        except KeyError:
            pass
    return None
cols = [("isolated", 150), ("initial", 250), ("medial", 350), ("final", 450)]
y = 0
label("LETTER", 0, y); [label(n.upper() + " (ref / xarab)", x - 70, y, h=5) for n, x in cols]
y -= 26
rows = []
for item in forms:
    letter = item["letter"]
    nm = "LAM-ALEF" if letter == "لا" else unicodedata.name(letter).replace("ARABIC LETTER ", "").replace("ALEF WITH ", "ALEF ").replace("WITH ", "").replace(" ABOVE", "-UP").replace(" BELOW", "-DN").replace("HAMZA", "HMZ").replace("MADDA", "MDA").replace("TEH MARBUTA", "TEH-MARB")
    label(nm, 0, y)
    for form, x in cols:
        code = item[form]
        if letter in RIGHT_JOIN and form in ("initial", "medial") and letter != "لا":
            code = item["isolated"] if form == "initial" else item["final"]
        if letter == "ء" and form != "isolated":
            continue
        r = ref(letter, form)
        # reference glyph (Arial presentation form) above, the xarab code string below
        if r:
            label(r, x - 40, y, "REF", H, TextEntityAlignment.RIGHT)
        label(code, x, y, "SM_AR", H, TextEntityAlignment.RIGHT)
        rows.append({"letter": letter, "name": nm, "form": form, "code": code, "reference": r})
    y -= 26
doc.saveas("xarab-check.dxf")
json.dump(rows, open("cells.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("rows", len(rows), "height", -y)

