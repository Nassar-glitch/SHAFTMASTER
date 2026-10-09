# One overview image per rendered sheet (all pages tiled) for page-by-page visual review.
import sys, pathlib, pymupdf
src, dst = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]); dst.mkdir(parents=True, exist_ok=True)
for pdf in sorted(src.glob('*.pdf')):
    d = pymupdf.open(pdf); n = len(d); cols = 3; rows = (n + cols - 1) // cols
    w, h = d[0].rect.width, d[0].rect.height
    out = pymupdf.open(); page = out.new_page(width=w * cols, height=h * rows)
    for i in range(n):
        r = pymupdf.Rect((i % cols) * w, (i // cols) * h, (i % cols + 1) * w, (i // cols + 1) * h)
        page.show_pdf_page(r, d, i); page.draw_rect(r, color=(0.5, 0.5, 0.5), width=1)
    page.get_pixmap(dpi=55).save(dst / f'{pdf.stem}-overview.png')
    print(pdf.stem, n)
