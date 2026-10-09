using System;
using System.Linq;
using System.Collections.Generic;

// Shared by the golden programs (tests/golden/GoldenTests.cs, tests/integration/NativeGoldenTests.cs).
public static class JsonMembers
{
    // JavaScriptSerializer writes object members in reflection order, which is not stable between processes. Project JSON
    // captures therefore sort the members of every object (ordinal by the raw key); every value keeps its exact text.
    public static string Sort(string json) { int i = 0; var v = Value(json, ref i); return v + json.Substring(i); }
    static string Value(string s, ref int i)
    {
        int start = i;
        while (i < s.Length && Char.IsWhiteSpace(s[i])) i++;
        string lead = s.Substring(start, i - start);
        if (s[i] == '{')
        {
            i++; var members = new List<KeyValuePair<string, string>>();
            while (true)
            {
                while (Char.IsWhiteSpace(s[i])) i++;
                if (s[i] == '}') { i++; break; }
                string key = Str(s, ref i); while (Char.IsWhiteSpace(s[i])) i++; i++; /* ':' */
                members.Add(new KeyValuePair<string, string>(key, Value(s, ref i)));
                while (Char.IsWhiteSpace(s[i])) i++;
                if (s[i] == ',') { i++; continue; }
                i++; break; /* '}' */
            }
            return lead + "{" + String.Join(",", members.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => m.Key + ":" + m.Value)) + "}";
        }
        if (s[i] == '[')
        {
            i++; var items = new List<string>();
            while (true)
            {
                while (Char.IsWhiteSpace(s[i])) i++;
                if (s[i] == ']') { i++; break; }
                items.Add(Value(s, ref i));
                while (Char.IsWhiteSpace(s[i])) i++;
                if (s[i] == ',') { i++; continue; }
                i++; break; /* ']' */
            }
            return lead + "[" + String.Join(",", items) + "]";
        }
        if (s[i] == '"') return lead + Str(s, ref i);
        int t = i; while (i < s.Length && ",]} \t\r\n".IndexOf(s[i]) < 0) i++;
        return lead + s.Substring(t, i - t);
    }
    static string Str(string s, ref int i)
    {
        int t = i; i++;
        while (s[i] != '"') { if (s[i] == '\\') i++; i++; }
        i++; return s.Substring(t, i - t);
    }
}
