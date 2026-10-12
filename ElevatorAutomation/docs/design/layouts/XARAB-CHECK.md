# xarab check: how the encoder's codes render (spike, 2026-10-12)

Question: does `XarabText` (`origin/cloud/layouts-c2`, read only) write every Arabic letter in a form that `xarab.shx` draws correctly?

Method: Core Console, AutoCAD 2023, font `xarab.shx` (`C:\Program Files\Autodesk\AutoCAD 2023\Fonts\XARAB.SHX`). Every letter was encoded in its isolated, initial, medial and final form (and lam-alef), written as TEXT in a test DWG
([xarab-check.dwg](xarab-check.dwg)) beside the Arial presentation form of the same letter as a reference, and plotted through `DWG To PDF.pc3` to
[xarab-check.png](xarab-check.png) (each cell: reference left, xarab right). A second DWG draws every single code 33..255
([xarab-codes.png](xarab-codes.png): the number is left of its glyph) to find the code that holds the right glyph. The judgement is by eye
against the reference; AutoCAD's own log reports only codes with no glyph at all (`Undefined shape`, see 3).

## Result

37 letters and lam-alef were checked in four forms each. Five letters have forms that render wrong or not at all (the rest are fine, see below):

| # | Letter | Forms | Encoder writes | What xarab draws | Glyph that holds the right shape |
|---|---|---|---|---|---|
| 1 | ء hamza | isolated | `x` | أ (alef with hamza above) | none: no bare hamza glyph in the font |
| 2 | ز zain | all (isolated, final; initial and medial are the isolated form) | `.` (46) | a tiny period-like mark | **`>` (62)** draws ز |
| 3 | ط tah | initial, medial | `"` (34) | nothing (`** Undefined shape 34`) | none: the font has only the isolated / final ط (`'`, 39) |
| 4 | ظ zah | initial, medial | `?` (63) | nothing (`** Undefined shape 63`) | none: only the isolated / final ظ (`/`, 47) |
| 5 | ى alef maksura | all four | `n` (initial, medial), `N` (final, isolated) | آ (alef with madda) | **`D` (68)** and `%%229` draw ى |
| 6 | ي yeh | isolated, final | `D` (68) | ى: the dotless shape (Egyptian style), reads as alef maksura | no dotted isolated / final ي glyph found; initial / medial `d` (100) are correct |

Known by design, not a rendering error: أ إ آ are written as a plain alef (the table says so), so hamza and madda are lost; the font does have
**`X` / `x` (88 / 120) = أ** and **`N` = آ**, so أ and آ could be written with them.

Everything else renders as the reference shows: ا ب ة ت ث ج ح خ د ذ ر س ش ص ض ع غ ف ق ك ل م ن ه و ؤ ئ in all their forms (right-joining
letters have no initial or medial form and use the isolated or final glyph, as intended), ط ظ isolated / final, ي initial / medial, and
lam-alef both alone (`B`) and joined (`b`). The isolated ه draws as the "٥"-like naskh heh (`I`); that is the font's own shape.

## Fix list for `XarabText`

1. ز: use `>` instead of `.` in every form.
2. ى: use `D` (68) for every form. The font has no connected final form of ى, so it will not join to the letter before it (the isolated shape is drawn).
3. ط ظ initial / medial: no glyph exists. Either write the isolated / final glyph (the letter will not connect to the next one) or leave ط ظ out of text that must join; check the strings the sheet really writes (company and project names typed by the user can contain them).
4. ء: no bare hamza: write it as `x` knowing it shows أ, or drop it.
5. ي isolated / final: the font draws it dotless; if dots are wanted, a glyph would have to be added to the font, which is not planned. For text that ends in ي (for example "في") the reader sees "ى". Keep, but note it.
6. أ إ آ: optional improvement, `X` for أ, `N` for آ, إ stays alef (no glyph with hamza below).

These are findings only; no cloud file was changed or merged.
