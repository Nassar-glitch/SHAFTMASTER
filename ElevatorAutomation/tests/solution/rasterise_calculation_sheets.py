# Rasterise rendered PDFs and validate each .docx with python-docx (independent reader).
import sys, json, pathlib, pymupdf, docx
sheets, rendered = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
report = []
for pdf in sorted(rendered.glob('*.pdf')):
    d = pymupdf.open(pdf); pages = []
    for i, page in enumerate(d):
        png = rendered / f'{pdf.stem}-p{i+1}.png'
        page.get_pixmap(dpi=80).save(png)
        # Text outside the printable area would indicate clipping.
        w, h = page.rect.width, page.rect.height
        outside = [b[4][:40] for b in page.get_text('blocks') if b[2] > w - 20 or b[0] < 20]
        pages.append({'png': png.name, 'outsideMargins': outside})
    doc = docx.Document(str(sheets / (pdf.stem + '.docx')))
    report.append({'file': pdf.stem, 'pdfPages': len(d), 'pythonDocxParagraphs': len(doc.paragraphs), 'pythonDocxTables': len(doc.tables), 'pages': pages})
(rendered / 'raster-report.json').write_text(json.dumps(report, indent=1), encoding='utf-8')
for r in report: print(r['file'], r['pdfPages'], 'pages', r['pythonDocxTables'], 'tables', 'clip:', [p['outsideMargins'] for p in r['pages'] if p['outsideMargins']])
