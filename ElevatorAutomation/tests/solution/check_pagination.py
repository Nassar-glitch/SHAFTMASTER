# Pagination check on Word-rendered PDFs (test viewer only).
# A page other than the last fails when its body is more than 40 % empty, unless a numbered section
# ends there (the next page starts with a numbered section heading).
# Usage: python check_pagination.py <rendered folder> <pdf stem to enforce> [more stems...]
import sys, json, re, pathlib, pymupdf

MM = 72 / 25.4
BODY_TOP = 2041 / 20.0      # top margin in pt (twips / 20)
BODY_BOTTOM_MARGIN = 1531 / 20.0

def body_extent(page):
    top, bottom = BODY_TOP, page.rect.height - BODY_BOTTOM_MARGIN
    ys = []
    for b in page.get_text('blocks'):
        if b[1] >= top - 1 and b[3] <= bottom + 1 and b[4].strip():
            ys.append(b[3])
    for info in page.get_image_info():
        r = info['bbox']
        if r[1] >= top - 1 and r[3] <= bottom + 1:
            ys.append(r[3])
    for d in page.get_drawings():
        r = d['rect']
        if r.y0 >= top - 1 and r.y1 <= bottom + 1 and r.height < (bottom - top) * 0.95:
            ys.append(r.y1)
    return top, bottom, (max(ys) if ys else top)

def first_body_text(page):
    top = BODY_TOP
    blocks = sorted([b for b in page.get_text('blocks') if b[1] >= top - 1 and b[4].strip()], key=lambda b: (b[1], b[0]))
    return blocks[0][4].strip() if blocks else ''

folder = pathlib.Path(sys.argv[1]); enforce = set(sys.argv[2:])
report = {}; failures = []
for pdf in sorted(folder.glob('*.pdf')):
    d = pymupdf.open(pdf); pages = []
    for i, page in enumerate(d):
        top, bottom, used = body_extent(page)
        empty = (bottom - used) / (bottom - top)
        section_ends = i + 1 < len(d) and re.match(r'^\d+\s+[A-Z]', first_body_text(d[i + 1])) is not None
        pages.append({'page': i + 1, 'empty': round(empty, 3), 'sectionEndsHere': section_ends})
        if pdf.stem in enforce and i + 1 < len(d) and empty > 0.40 and not section_ends:
            failures.append(f'{pdf.stem} page {i+1}: {empty:.0%} empty')
    report[pdf.stem] = pages
(folder / 'pagination-report.json').write_text(json.dumps({'status': 'FAIL' if failures else 'PASS', 'failures': failures, 'pages': report}, indent=1), encoding='utf-8')
for stem in sorted(enforce):
    print(stem, [f"{p['page']}:{p['empty']:.0%}{'*' if p['sectionEndsHere'] else ''}" for p in report.get(stem, [])])
print('PAGINATION', 'FAIL' if failures else 'PASS', failures)
sys.exit(1 if failures else 0)
