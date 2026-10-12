import ezdxf
from ezdxf.enums import TextEntityAlignment
doc = ezdxf.new("R2018", setup=False)
doc.styles.add("SM_AR", font="xarab.shx"); doc.styles.add("SM_EN", font="cent.shx")
msp = doc.modelspace()
def t(text, x, y, style, h, align=TextEntityAlignment.LEFT):
    e = msp.add_text(text, dxfattribs={"style": style, "height": h}); e.set_placement((x, y), align=align)
codes = list(range(33, 128))
per_row = 16
for i, c in enumerate(codes):
    r, k = divmod(i, per_row)
    x, y = 20 + k * 60, -r * 30
    t(str(c), x, y, "SM_EN", 4)            # the code number (cent)
    s = "%%" + ("%03d" % c) if c >= 128 else chr(c)
    t(s, x + 38, y, "SM_AR", 10, TextEntityAlignment.RIGHT)
doc.saveas("xarab-codes-low.dxf")
print(len(codes), "codes,", -(len(codes)//per_row) * 30)



