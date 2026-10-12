# xarab check (spike)

Reproduces `../xarab-check.png`, `../xarab-codes.png` and `../xarab-check.dwg`. Core Console only (AutoCAD 2023, `accoreconsole.exe`).

1. `git show origin/cloud/layouts-c2:ElevatorAutomation/Elevator.Core/Layouts/XarabText.cs > XarabText.cs` (read only, not merged here).
2. `csc /out:Forms.exe Forms.cs XarabText.cs` then `Forms.exe forms.json`: the encoder's codes for every letter in its four forms and lam-alef.
3. `python -I build_xarab_check.py`: `xarab-check.dxf`, text styles `SM_AR` (xarab.shx), `SM_EN` (cent.shx), `REF` (arial.ttf, the Unicode presentation form as reference).
4. Core Console script in a path without spaces (a space ends a script answer):
   `FILEDIA 0`, `SAVEAS 2018 <dir>\xarab-check.dwg`, then `-PLOT` with `Y, Model, DWG To PDF.pc3, ISO A1 (594.00 x 841.00 MM), Millimeters, Portrait, No, Extents, Fit, Center, Yes, monochrome.ctb, Yes, A, <dir>\xarab-check.pdf, N, Y`.
5. PyMuPDF rasterises the PDF to PNG. `xarab_codes_grid.py` does the same for the single codes 33..127 (the codes 128..255 use `%%nnn`).
