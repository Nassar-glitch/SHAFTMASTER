using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Elevator.Core.Layouts
{
    /// <summary>The script of a text, which chooses its font on the drawing sheet.</summary>
    public enum TextScript
    {
        /// <summary>No Arabic letter: the cent.shx style.</summary>
        Latin,
        /// <summary>At least one Arabic letter: the xarab.shx style.</summary>
        Arabic
    }

    /// <summary>
    /// Unicode Arabic to the key-code string of the template's xarab.shx font, and back (DESIGN-FLOW "Layouts and PDF": the template's
    /// SHX fonts stay; normal Arabic typing is converted). The font stores one glyph per character code: the Arabic letter in its joined
    /// form (initial, medial, final, isolated), in reading order, and draws right to left from the insertion point. The table is the
    /// one decoded from the template's 30 xarab strings (docs/design/layouts/LAYOUTS.md 2.3; marked "seen" below), completed by the
    /// font's rule visible there: a dual-joining letter on a letter key uses the lowercase key joined to the next letter (initial /
    /// medial) and the uppercase key otherwise (final / isolated); on a punctuation key the shifted key is the joined form (ج "{" / "[",
    /// ك ":" / ";"). Letters not in the template follow the Arabic (101) keyboard key by that rule; check them once in AutoCAD with
    /// xarab.shx. Hamza on alef (أ إ آ) has no decoded glyph and is written as alef; tashkeel is dropped; Arabic-Indic digits become
    /// ASCII digits.
    /// </summary>
    public static class XarabText
    {
        /// <summary>The template text style for Arabic (xarab.shx).</summary>
        public const string ArabicStyle = "SM_AR";
        /// <summary>The template text style for other text (cent.shx).</summary>
        public const string LatinStyle = "SM_EN";

        const char Tatweel = 'ـ';
        const char Lam = 'ل';
        const string LamAlef = "لا";

        // Joining forms of one letter: joined to the next letter (initial / medial, "Joined"), final, isolated; medial when it differs.
        sealed class Forms
        {
            public string Initial, Medial, Final, Isolated;
            public bool Dual; // joins to the next letter
        }

        static readonly Dictionary<char, Forms> Letters = new Dictionary<char, Forms>();
        static readonly Dictionary<string, string> Decoding = new Dictionary<string, string>(StringComparer.Ordinal);
        // The code of the lam-alef ligature: joined to the previous letter "b" (seen), not joined "B" (seen).
        const string LamAlefJoined = "b", LamAlefAlone = "B";

        static void Dual(char letter, string joined, string alone)
        {
            Dual(letter, joined, joined, alone, alone);
        }

        static void Dual(char letter, string initial, string medial, string final, string isolated)
        {
            Letters[letter] = new Forms { Initial = initial, Medial = medial, Final = final, Isolated = isolated, Dual = true };
            foreach (var c in new[] { initial, medial, final, isolated })
                Decode(c, letter.ToString());
        }

        static void Right(char letter, string final, string isolated)
        {
            Letters[letter] = new Forms { Initial = isolated, Medial = final, Final = final, Isolated = isolated, Dual = false };
            Decode(final, letter.ToString());
            Decode(isolated, letter.ToString());
        }

        static void Decode(string code, string text)
        {
            if (!Decoding.ContainsKey(code))
                Decoding[code] = text;
        }

        static string Code(int n)
        {
            return ((char)n).ToString();
        }

        static XarabText()
        {
            // Right-joining letters (join to the previous letter only).
            Right('ا', "h", "H");      // ا  seen: final h, isolated H
            Right('أ', "h", "H");      // أ  written as ا
            Right('إ', "h", "H");      // إ  written as ا
            Right('آ', "h", "H");      // آ  written as ا
            Right('د', "]", "]");      // د  seen
            Right('ذ', "`", "`");      // ذ
            Right('ر', "v", "v");      // ر  seen (also "V", decoded below)
            Right('ز', ".", ".");      // ز
            Right('و', ",", ",");      // و  seen
            Right('ؤ', "c", "c");      // ؤ
            Right('ة', "m", "m");      // ة  seen final
            Right('ء', "x", "x");      // ء  (joins neither side)
            // Dual-joining letters.
            Dual('ب', "f", "F");                                   // ب
            Dual('ت', "j", "J");                                   // ت  seen: initial / medial j, final J
            Dual('ث', "e", "E");                                   // ث
            Dual('ج', "{", "[");                                   // ج  seen: initial / medial {
            Dual('ح', "p", "P");                                   // ح  seen: initial / medial p
            Dual('خ', "o", "o", Code(178), Code(178));             // خ  seen: final %%178
            Dual('س', "s", "S");                                   // س  seen
            Dual('ش', "a", "A");                                   // ش  seen: joined a
            Dual('ص', "w", "W");                                   // ص  seen: joined w
            Dual('ض', "q", "Q");                                   // ض
            Dual('ط', "\"", "'");                                  // ط
            Dual('ظ', "?", "/");                                   // ظ
            Dual('ع', "u", Code(150), Code(154), "U");             // ع  seen: all four forms
            Dual('غ', "y", "Y");                                   // غ
            Dual('ف', "t", "T");                                   // ف  seen: medial t (and %%156)
            Dual('ق', "r", Code(158), "R", "R");                   // ق  seen: initial r, medial %%158
            Dual('ك', ":", ";");                                   // ك  seen: initial :, final ;
            Dual('ل', "g", "G");                                   // ل  seen: joined g, isolated G
            Dual('م', "l", "L");                                   // م  seen: joined l, final / isolated L (also %%226)
            Dual('ن', "k", "K");                                   // ن  seen: initial k
            Dual('ه', "i", "I");                                   // ه  seen: isolated I
            Dual('ي', "d", "D");                                   // ي  seen: joined d
            Dual('ى', "n", "N");                                   // ى
            Dual('ئ', "z", "Z");                                   // ئ
            Dual(Tatweel, "_", "_");                                    // kashida  seen
            // Variants seen in the template (decoded, never written).
            Decode("V", "ر");                  // ر
            Decode(Code(226), "م");            // م final
            Decode(Code(156), "ف");            // ف medial
            Decode(LamAlefJoined, LamAlef);
            Decode(LamAlefAlone, LamAlef);
            Decode("\\", ":");                      // the colon after a label
            for (char d = '0'; d <= '9'; d++)
                Decode(d.ToString(), d.ToString());
            Decode(" ", " ");
        }

        // ---------- script ----------
        /// <summary>True for an Arabic letter, mark or presentation form.</summary>
        public static bool IsArabic(char c)
        {
            return (c >= '؀' && c <= 'ۿ' && !(c >= '٠' && c <= '٩'))
                || (c >= 'ݐ' && c <= 'ݿ')
                || (c >= 'ﭐ' && c <= '﷿')
                || (c >= 'ﹰ' && c <= '﻿');
        }

        /// <summary>The script of a text: Arabic when it holds an Arabic letter, else Latin (also for null or blank text).</summary>
        public static TextScript Detect(string text)
        {
            return text != null && text.Any(c => IsArabic(c) && Char.IsLetter(c)) ? TextScript.Arabic : TextScript.Latin;
        }

        /// <summary>The template text style for a text: <see cref="ArabicStyle"/> or <see cref="LatinStyle"/>.</summary>
        public static string StyleFor(string text)
        {
            return Detect(text) == TextScript.Arabic ? ArabicStyle : LatinStyle;
        }

        /// <summary>
        /// The text as it is written into the drawing: Arabic text encoded for xarab (<see cref="Encode"/>), other text unchanged.
        /// </summary>
        public static string ForDrawing(string text)
        {
            return Detect(text) == TextScript.Arabic ? Encode(text) : (text ?? "");
        }

        // ---------- encode ----------
        /// <summary>The comparable form of an Arabic text after a round trip: presentation forms and hamza on alef resolved, tashkeel
        /// dropped, Arabic-Indic digits as ASCII digits.</summary>
        public static string Normalize(string text)
        {
            if (String.IsNullOrEmpty(text))
                return "";
            var b = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c >= 'ﭐ' && c <= '﻿')
                    b.Append(c.ToString().Normalize(NormalizationForm.FormKC));
                else
                    b.Append(c);
            }
            var o = new StringBuilder(b.Length);
            foreach (char c in b.ToString())
            {
                if (c >= 'ً' && c <= 'ْ' || c == 'ٰ')
                    continue; // tashkeel
                if (c == 'أ' || c == 'إ' || c == 'آ')
                    o.Append('ا');
                else if (c >= '٠' && c <= '٩')
                    o.Append((char)('0' + (c - '٠')));
                else if (c >= '۰' && c <= '۹')
                    o.Append((char)('0' + (c - '۰')));
                else
                    o.Append(c);
            }
            return o.ToString();
        }

        static bool IsAlef(char c)
        {
            return c == 'ا' || c == 'أ' || c == 'إ' || c == 'آ';
        }

        // A unit of the text: a letter, the lam-alef ligature (Letter = '\0') or any other character.
        struct Unit
        {
            public char Letter;
            public bool Ligature;
            public string Other;
        }

        static bool JoinsNext(Unit u)
        {
            Forms f;
            return !u.Ligature && u.Other == null && Letters.TryGetValue(u.Letter, out f) && f.Dual;
        }

        static bool JoinsPrevious(Unit u)
        {
            if (u.Ligature)
                return true;
            return u.Other == null && Letters.ContainsKey(u.Letter) && u.Letter != 'ء';
        }

        /// <summary>
        /// Unicode Arabic (reading order) to the xarab code string. Codes above 127 are written as %%nnn. Characters the font has no
        /// code for (Latin letters, most punctuation) are left as they are; write such text in its own Latin text.
        /// </summary>
        public static string Encode(string text)
        {
            if (String.IsNullOrEmpty(text))
                return "";
            string s = text;
            var b0 = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c >= 'ﭐ' && c <= '﻿')
                    b0.Append(c.ToString().Normalize(NormalizationForm.FormKC));
                else
                    b0.Append(c);
            }
            s = b0.ToString();
            var units = new List<Unit>();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= 'ً' && c <= 'ْ' || c == 'ٰ')
                    continue;
                if (c == Lam && i + 1 < s.Length && IsAlef(s[i + 1]))
                {
                    units.Add(new Unit { Ligature = true });
                    i++;
                    continue;
                }
                if (Letters.ContainsKey(c))
                    units.Add(new Unit { Letter = c });
                else if (c >= '٠' && c <= '٩')
                    units.Add(new Unit { Other = ((char)('0' + (c - '٠'))).ToString() });
                else if (c >= '۰' && c <= '۹')
                    units.Add(new Unit { Other = ((char)('0' + (c - '۰'))).ToString() });
                else if (c == ':')
                    units.Add(new Unit { Other = "\\" });
                else
                    units.Add(new Unit { Other = c.ToString() });
            }
            var b = new StringBuilder(units.Count * 2);
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Other != null)
                {
                    b.Append(u.Other);
                    continue;
                }
                bool previous = i > 0 && JoinsNext(units[i - 1]) && JoinsPrevious(u);
                if (u.Ligature)
                {
                    b.Append(previous ? LamAlefJoined : LamAlefAlone);
                    continue;
                }
                var f = Letters[u.Letter];
                bool next = f.Dual && i + 1 < units.Count && JoinsPrevious(units[i + 1]);
                string code = previous ? (next ? f.Medial : f.Final) : (next ? f.Initial : f.Isolated);
                foreach (char c in code)
                    b.Append(c > 127 ? "%%" + ((int)c).ToString("000", CultureInfo.InvariantCulture) : c.ToString());
            }
            return b.ToString();
        }

        // ---------- decode ----------
        /// <summary>
        /// The xarab code string (as stored in the drawing, %%nnn or the character itself) back to Unicode Arabic in reading order. The
        /// joined forms map to their letter; the kashida "_" becomes U+0640; a code with no decoding is kept.
        /// </summary>
        public static string Decode(string codes)
        {
            if (String.IsNullOrEmpty(codes))
                return "";
            var b = new StringBuilder(codes.Length);
            for (int i = 0; i < codes.Length; i++)
            {
                string code = codes[i].ToString();
                if (codes[i] == '%' && i + 4 < codes.Length && codes[i + 1] == '%'
                    && Char.IsDigit(codes[i + 2]) && Char.IsDigit(codes[i + 3]) && Char.IsDigit(codes[i + 4]))
                {
                    code = ((char)Int32.Parse(codes.Substring(i + 2, 3), CultureInfo.InvariantCulture)).ToString();
                    i += 4;
                }
                string text;
                b.Append(Decoding.TryGetValue(code, out text) ? text : code);
            }
            return b.ToString();
        }

        /// <summary>The decoded text without kashida and outer spaces: what a reader sees.</summary>
        public static string Plain(string unicode)
        {
            return (unicode ?? "").Replace(Tatweel.ToString(), "").Trim();
        }
    }
}
