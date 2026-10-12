using System;
using System.Linq;
using Elevator.Core.Layouts;
using Xunit;

namespace Elevator.Core.Tests
{
    // Cloud C2: the xarab.shx key-code encoding of the template (docs/design/layouts/LAYOUTS.md 2.3). Every xarab string of the template
    // decodes to its reading; encoding the reading gives back the template's codes wherever the template uses the canonical form of
    // each letter, and every reading survives a round trip.
    public class XarabTextTests
    {
        // Template string (as stored), its reading (kashida removed, outer spaces trimmed).
        public static readonly object[][] TemplateLabels =
        {
            new object[] { "Hgl___________hg; \\", "المالك :" },
            new object[] { "HsL Hgg,pm \\", "اسم اللوحة :" },
            new object[] { "Hujlh]", "اعتماد" },
            new object[] { "l%%158dhSHgvs%%226", "مقياسالرسم" },
            new object[] { "Hj{hI HgalhG", "اتجاه الشمال" },
            new object[] { "Hgj%%150_______]dbJ", "التعديلات" },
            new object[] { "w%%156v", "صفر" },
            new object[] { "Hgjhvd%%178 \\   ", "التاريخ :" },
            new object[] { "k,U HBulhG\\", "نوع الاعمال:" },
            new object[] { "vr%%226Hgg,pm \\", "رقماللوحة :" },
            new object[] { "Hgjhvd%%178", "التاريخ" },
            new object[] { "L0 H0  \\  ", "م0 ا0  :" },
            new object[] { "v,{%%154", "روجع" },
            new object[] { "Hglvpgm \\   ", "المرحلة :" },
            new object[] { "VrL ,:,] HglaV,U \\", "رقم وكود المشروع :" },
            new object[] { "L0   \\  ", "م0   :" },
            new object[] { "jwldL", "تصميم" },
            new object[] { "HulhG lwhu]", "اعمال مصاعد" },
            new object[] { "HsL Hgav:m", "اسم الشركة" },
            new object[] { "aV:m sd]vH gdtJ gglwhu] ", "شركة سيدرا ليفت للمصاعد" },
            new object[] { ",HgsgHgL HgljpV:m", "والسلالم المتحركة" },
            new object[] { "Hsâ Hglav,U \\", "اسم المشروع :" },
            new object[] { "lhsjv o,A", "ماستر خوش" },
        };

        [Theory]
        [MemberData(nameof(TemplateLabels))]
        public void Every_template_label_decodes_to_its_reading(string codes, string reading)
        {
            Assert.Equal(reading, XarabText.Plain(XarabText.Decode(codes)));
        }

        [Theory]
        [MemberData(nameof(TemplateLabels))]
        public void Every_template_label_round_trips(string codes, string reading)
        {
            string encoded = XarabText.Encode(reading);
            Assert.Equal(XarabText.Normalize(reading), XarabText.Plain(XarabText.Decode(encoded)));
            // Encoding the decoded template text keeps the reading too (kashida kept).
            string decoded = XarabText.Decode(codes);
            Assert.Equal(decoded, XarabText.Decode(XarabText.Encode(decoded)));
        }

        [Theory]
        [InlineData("المالك :", "Hglhg; \\")]
        [InlineData("اسم اللوحة :", "HsL Hgg,pm \\")]
        [InlineData("اعتماد", "Hujlh]")]
        [InlineData("روجع", "v,{%%154")]
        [InlineData("تصميم", "jwldL")]
        [InlineData("اتجاه الشمال", "Hj{hI HgalhG")]
        [InlineData("التاريخ", "Hgjhvd%%178")]
        [InlineData("التاريخ :", "Hgjhvd%%178 \\")]
        [InlineData("المرحلة :", "Hglvpgm \\")]
        [InlineData("اعمال مصاعد", "HulhG lwhu]")]
        [InlineData("أعمال مصاعد", "HulhG lwhu]")] // hamza on alef is written as alef
        [InlineData("اسم الشركة", "HsL Hgav:m")]
        [InlineData("نوع الاعمال:", "k,U HBulhG\\")]
        [InlineData("نوع الأعمال:", "k,U HBulhG\\")] // lam-alef with hamza: the lam-alef ligature
        [InlineData("مقياس", "l%%158dhS")]
        [InlineData("المشروع", "Hglav,U")]
        [InlineData("ليفت للمصاعد", "gdtJ gglwhu]")]
        public void Encoding_matches_the_template_codes(string reading, string codes)
        {
            Assert.Equal(codes, XarabText.Encode(reading));
        }

        [Fact]
        public void The_kashida_is_kept()
        {
            string k = "\u0640";
            Assert.Equal("Hgl___________hg; \\", XarabText.Encode("الم" + String.Concat(Enumerable.Repeat(k, 11)) + "الك :"));
            Assert.Equal("Hgj%%150_______]dbJ", XarabText.Encode("التع" + String.Concat(Enumerable.Repeat(k, 7)) + "ديلات"));
        }

        [Fact]
        public void Template_variants_decode_to_the_same_letter()
        {
            // The template writes some letters with a second glyph: ر "V", م final "â" (%%226), ف medial %%156.
            Assert.Equal(XarabText.Decode("Hglav,U"), XarabText.Decode("HglaV,U"));
            Assert.Equal(XarabText.Decode("HsL"), XarabText.Decode("Hs%%226"));
            Assert.Equal(XarabText.Decode("Hs%%226"), XarabText.Decode("Hsâ"));
            Assert.Equal("صفر", XarabText.Decode("wtv"));
            Assert.Equal("صفر", XarabText.Decode("w%%156v"));
        }

        [Theory]
        [InlineData("شركة النيل للمصاعد")]
        [InlineData("المصاعد الحديثة")]
        [InlineData("أعمال مصاعد")]
        [InlineData("فيلا الأستاذ محمد")]
        [InlineData("برج الظافر - الدور الثالث")]
        [InlineData("ضياء غزال ثروت ذكي")]
        [InlineData("مسئول طوارئ مؤسسة")]
        [InlineData("سماء")]
        [InlineData("مستشفى الهدى")]
        [InlineData("لا لأ لإ لآ")]
        [InlineData("مصعد ١٢ رقم ٣")]
        [InlineData("مُهَنْدِس")]
        [InlineData("ﺷﺮﻛﺔ")] // presentation forms
        public void Arabic_company_and_project_names_round_trip(string text)
        {
            string encoded = XarabText.Encode(text);
            Assert.DoesNotContain(encoded, c => c > 127); // codes above 127 are written as %%nnn
            Assert.Equal(XarabText.Normalize(text), XarabText.Decode(encoded));
        }

        [Fact]
        public void Every_arabic_letter_has_a_code_and_decodes_back()
        {
            string letters = "ءآأؤإئابةتثجحخدذرزسشصضطظعغفقكلمنهوىي";
            foreach (char c in letters)
            {
                foreach (string word in new[] { c.ToString(), "ب" + c, c + "ب", "ب" + c + "ب" })
                {
                    string encoded = XarabText.Encode(word);
                    Assert.NotEqual(word, encoded);
                    Assert.Equal(XarabText.Normalize(word), XarabText.Decode(encoded));
                }
            }
        }

        [Fact]
        public void Joining_forms_follow_the_neighbours()
        {
            // ع: initial u, medial %%150, final %%154, isolated U (all four seen in the template).
            Assert.Equal("U", XarabText.Encode("ع"));
            Assert.Equal("u", XarabText.Encode("عب").Substring(0, 1));
            Assert.Equal("f%%150F", XarabText.Encode("بعب"));
            Assert.Equal("f%%154", XarabText.Encode("بع"));
            // A right-joining letter cuts the join: ا after ب is final, the letter after ا starts again.
            Assert.Equal("fhf", XarabText.Encode("باب").Replace("F", "f"));
            Assert.Equal("Hf", XarabText.Encode("اب").Replace("F", "f"));
            // A space or a digit cuts the join.
            Assert.Equal("F F", XarabText.Encode("ب ب"));
        }

        [Theory]
        [InlineData("ELEVATORS COMPANY", TextScript.Latin)]
        [InlineData("Nile Lifts 2", TextScript.Latin)]
        [InlineData("SIMPLEX COLLECTIVE DOWN.", TextScript.Latin)]
        [InlineData("", TextScript.Latin)]
        [InlineData(null, TextScript.Latin)]
        [InlineData("١٢٣", TextScript.Latin)]
        [InlineData("شركة النيل", TextScript.Arabic)]
        [InlineData("أعمال مصاعد", TextScript.Arabic)]
        [InlineData("Nile شركة", TextScript.Arabic)]
        public void The_script_chooses_the_style(string text, TextScript script)
        {
            Assert.Equal(script, XarabText.Detect(text));
            Assert.Equal(script == TextScript.Arabic ? "SM_AR" : "SM_EN", XarabText.StyleFor(text));
        }

        [Fact]
        public void A_company_name_is_written_in_the_language_it_is_typed()
        {
            Assert.Equal("ELEVATORS COMPANY", XarabText.ForDrawing("ELEVATORS COMPANY"));
            Assert.Equal("Nile  Lifts ", XarabText.ForDrawing("Nile  Lifts "));
            string arabic = XarabText.ForDrawing("شركة النيل للمصاعد");
            Assert.Equal("av:m HgkdG gglwhu]", arabic);
            Assert.Equal("شركة النيل للمصاعد", XarabText.Decode(arabic));
            Assert.Equal("", XarabText.ForDrawing(null));
            Assert.Equal("", XarabText.Encode(""));
            Assert.Equal("", XarabText.Decode(null));
        }
    }
}
