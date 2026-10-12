using System;
using System.Collections.Generic;
using System.Text;
using Elevator.Core.Layouts;
public static class Forms
{
    public static int Main(string[] args)
    {
        // every base Arabic letter the encoder knows (hamza forms on alef included), in alphabetical order
        string letters = "ءآأؤإئابةتثجحخدذرزسشصضطظعغفقكلمنهوىي";
        var sb = new StringBuilder("[");
        bool first = true;
        foreach (char c in letters)
        {
            string L = c.ToString(), B = "ب";
            string isolated = XarabText.Encode(L);
            string initial = Strip(XarabText.Encode(L + B), "", XarabText.Encode(B));   // L joined to the next ب: drop ب's final code
            string final = XarabText.Encode(B + L);                                         // ب initial + L final
            string medial = XarabText.Encode(B + L + B);
            // codes of ب in each position
            string bInit = XarabText.Encode(B + B).Substring(0, XarabText.Encode(B + B).Length - XarabText.Encode(B).Length);
            string bFinal = XarabText.Encode(B);
            // initial: L + ب  -> code(L initial) + code(ب final)
            string enc = XarabText.Encode(L + B);
            initial = enc.EndsWith(bFinal) ? enc.Substring(0, enc.Length - bFinal.Length) : enc;
            // final: ب + L -> code(ب initial) + code(L final)
            enc = XarabText.Encode(B + L);
            final = enc.StartsWith(bInit) ? enc.Substring(bInit.Length) : enc;
            // medial: ب + L + ب -> code(ب initial) + code(L medial) + code(ب final)
            enc = XarabText.Encode(B + L + B);
            medial = enc.StartsWith(bInit) && enc.EndsWith(bFinal) ? enc.Substring(bInit.Length, enc.Length - bInit.Length - bFinal.Length) : enc;
            if (!first) sb.Append(",");
            first = false;
            sb.Append("{\"letter\":\"" + L + "\",\"isolated\":" + J(isolated) + ",\"initial\":" + J(initial) + ",\"medial\":" + J(medial) + ",\"final\":" + J(final) + "}");
        }
        // lam-alef: alone (isolated) and joined to the previous letter (final)
        string la = XarabText.Encode("لا");
        string bInit2 = XarabText.Encode("بب").Substring(0, XarabText.Encode("بب").Length - XarabText.Encode("ب").Length);
        string laJoined = XarabText.Encode("بلا"); laJoined = laJoined.StartsWith(bInit2) ? laJoined.Substring(bInit2.Length) : laJoined;
        sb.Append(",{\"letter\":\"لا\",\"isolated\":" + J(la) + ",\"initial\":" + J(la) + ",\"medial\":" + J(laJoined) + ",\"final\":" + J(laJoined) + "}");
        sb.Append("]");
        System.IO.File.WriteAllText(args[0], sb.ToString(), new UTF8Encoding(false));
        return 0;
    }
    static string Strip(string a, string b, string c) { return a; }
    static string J(string s) { return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }
}
