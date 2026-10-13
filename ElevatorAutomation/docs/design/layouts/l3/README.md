# L3: the output set (DWG, PDF, project file)

`tests/integration/run-layouts-pdf.ps1` (suite `layouts-pdf`) generates two projects through the packaged `OutputSetWriter` and the AutoCAD
2023 Core Console and renders page 1 of each PDF here (60 dpi):

| Project | PDF page | Checks |
|---|---|---|
| 6 stops, Arabic company and project, logo | [6-stops-arabic-logo-pdf.png](6-stops-arabic-logo-pdf.png) | one page 1189 x 841 mm, "P1 =" and the project name in the text, the logo image inside the logo area |
| Automatic 2S, 4 stops, no logo | [2S-4-stops-no-logo-pdf.png](2S-4-stops-no-logo-pdf.png) | one page 1189 x 841 mm, project name and "P1 =", no image object |

Both runs write exactly three files with one base name into the output folder (`<base>.dwg`, `<base>.pdf`, `<base>.elevator.json`), and a
second run into the same folder is refused by the writer (the desktop asks first). The desktop flow, including the overwrite / next-revision
prompt and a failing PDF step, is tested by the tier 2 suite `output-set-ui`.
