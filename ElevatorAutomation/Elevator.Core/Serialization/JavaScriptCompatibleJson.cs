using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Elevator.Core.Serialization
{
    // A serializer with the output and the conversions of System.Web.Script.Serialization.JavaScriptSerializer (.NET
    // Framework 4.x), for runtimes that do not have it (netstandard2.0). Rules reproduced:
    // - public instance fields, then public readable properties without parameters, in reflection order;
    // - strings quoted with JavaScriptSerializer's escapes (\uXXXX in lower-case hex for control characters, ' < > &,
    //   U+0085, U+2028, U+2029);
    // - double and float in .NET Framework "R" format (15 significant digits if they round-trip, else 17), decimal and
    //   integers in invariant format, enums as their number, bool as true / false, null as null, '\0' as null,
    //   DateTime as "\/Date(ms)\/", Guid in "D" format;
    // - reading: numbers as int, then long, then decimal, then double; objects as Dictionary<string,object>; members
    //   matched case-insensitively (property with a setter, else field); other conversions through TypeConverter.
    // ScriptIgnore, type resolvers and custom converters are not supported (not used by this project).
    public sealed class JavaScriptCompatibleJson : IJsonSerializer
    {
        public const int RecursionLimit = 100;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly long EpochTicks = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        // ---------- writing ----------
        public string Serialize(object value)
        {
            var b = new StringBuilder();
            Write(value, b, 0, new Dictionary<object, object>(ReferenceComparer.Instance));
            return b.ToString();
        }

        void Write(object o, StringBuilder b, int depth, Dictionary<object, object> inUse)
        {
            if (++depth > RecursionLimit)
                throw new ArgumentException("RecursionLimit exceeded.");
            if (o == null || o is DBNull)
            {
                b.Append("null");
                return;
            }
            var s = o as string;
            if (s != null)
            {
                Quote(s, b);
                return;
            }
            if (o is char)
            {
                if ((char)o == '\0')
                {
                    b.Append("null");
                    return;
                }
                Quote(o.ToString(), b);
                return;
            }
            if (o is bool)
            {
                b.Append((bool)o ? "true" : "false");
                return;
            }
            if (o is DateTime)
            {
                WriteDate((DateTime)o, b);
                return;
            }
            if (o is DateTimeOffset)
            {
                WriteDate(((DateTimeOffset)o).UtcDateTime, b);
                return;
            }
            if (o is Guid)
            {
                b.Append('"').Append(((Guid)o).ToString()).Append('"');
                return;
            }
            var uri = o as Uri;
            if (uri != null)
            {
                Quote(uri.GetComponents(UriComponents.SerializationInfoString, UriFormat.UriEscaped), b);
                return;
            }
            if (o is double)
            {
                b.Append(RoundTrip((double)o));
                return;
            }
            if (o is float)
            {
                b.Append(RoundTrip((float)o));
                return;
            }
            var type = o.GetType();
            if (type.IsPrimitive || o is decimal)
            {
                var c = o as IConvertible;
                b.Append(c != null ? c.ToString(Inv) : o.ToString());
                return;
            }
            if (type.IsEnum)
            {
                var u = Enum.GetUnderlyingType(type);
                if (u == typeof(long) || u == typeof(ulong))
                    throw new InvalidOperationException("Enumerations with a 64-bit underlying type are not supported.");
                b.Append(((Enum)o).ToString("D"));
                return;
            }
            if (inUse.ContainsKey(o))
                throw new InvalidOperationException(
                    "A circular reference was detected while serializing an object of type '" + type.FullName + "'.");
            inUse.Add(o, null);
            try
            {
                var dictionary = o as IDictionary;
                if (dictionary != null)
                {
                    b.Append('{');
                    bool first = true;
                    foreach (DictionaryEntry e in dictionary)
                    {
                        var key = e.Key as string;
                        if (key == null)
                            throw new ArgumentException("Type '"
                                + type.FullName
                                + "' is not supported for serialization/deserialization of a dictionary, keys must be strings or objects.");
                        if (!first)
                            b.Append(',');
                        first = false;
                        Quote(key, b);
                        b.Append(':');
                        Write(e.Value, b, depth, inUse);
                    }
                    b.Append('}');
                    return;
                }
                var list = o as IEnumerable;
                if (list != null)
                {
                    b.Append('[');
                    bool first = true;
                    foreach (var item in list)
                    {
                        if (!first)
                            b.Append(',');
                        first = false;
                        Write(item, b, depth, inUse);
                    }
                    b.Append(']');
                    return;
                }
                b.Append('{');
                bool firstMember = true;
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!firstMember)
                        b.Append(',');
                    Quote(f.Name, b);
                    b.Append(':');
                    Write(f.GetValue(o), b, depth, inUse);
                    firstMember = false;
                }
                foreach (var p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty))
                {
                    var get = p.GetGetMethod();
                    if (get == null || get.GetParameters().Length > 0)
                        continue;
                    if (!firstMember)
                        b.Append(',');
                    Quote(p.Name, b);
                    b.Append(':');
                    Write(get.Invoke(o, null), b, depth, inUse);
                    firstMember = false;
                }
                b.Append('}');
            }
            finally { inUse.Remove(o); }
        }

        static void WriteDate(DateTime d, StringBuilder b)
        {
            b.Append("\"\\/Date(").Append(((d.ToUniversalTime().Ticks - EpochTicks) / 10000).ToString(Inv)).Append(")\\/\"");
        }

        // .NET Framework "R": 15 significant digits when they round-trip, otherwise 17 (".NET Core" "R" is the shortest form).
        public static string RoundTrip(double v)
        {
            if (Double.IsNaN(v))
                return "NaN";
            if (Double.IsPositiveInfinity(v))
                return "Infinity";
            if (Double.IsNegativeInfinity(v))
                return "-Infinity";
            if (v == 0)
                return "0";
            string s = v.ToString("G15", Inv);
            return RoundTrips(s, v) ? s : v.ToString("G17", Inv);
        }

        // Whether the decimal text s converts back to exactly v under IEEE round-to-nearest, ties to even. Exact arithmetic,
        // independent of the runtime's parser (the .NET Framework parser is not always correctly rounded).
        static bool RoundTrips(string s, double v)
        {
            bool negative = v < 0;
            double a = Math.Abs(v);
            string t = s.TrimStart('-');
            if ((s.StartsWith("-")) != negative)
                return false;
            int ePos = t.IndexOfAny(new[] { 'E', 'e' });
            int exp10 = ePos < 0 ? 0 : Int32.Parse(t.Substring(ePos + 1), NumberStyles.AllowLeadingSign, Inv);
            string mant = ePos < 0 ? t : t.Substring(0, ePos);
            int dot = mant.IndexOf('.');
            if (dot >= 0)
            {
                exp10 -= mant.Length - dot - 1;
                mant = mant.Remove(dot, 1);
            }
            var digits = System.Numerics.BigInteger.Parse(mant, Inv);
            long bits = BitConverter.DoubleToInt64Bits(a);
            double prev = bits > 0 ? BitConverter.Int64BitsToDouble(bits - 1) : 0, next = BitConverter.Int64BitsToDouble(bits + 1);
            System.Numerics.BigInteger mv, mp, mn;
            int ev, ep, en;
            Exact(a, out mv, out ev);
            Exact(prev, out mp, out ep);
            if (Double.IsInfinity(next))
            {
                mn = mv;
                en = ev;
            }
            else
                Exact(next, out mn, out en); // above double.MaxValue: the same gap as below
            int e = Math.Min(ev, Math.Min(ep, en));
            var xv = mv << (ev - e);
            var xp = mp << (ep - e);
            var xn = Double.IsInfinity(next) ? (xv << 1) - xp : mn << (en - e);
            // Twice the midpoints, in units of 2^e: low = xv + xp, high = xv + xn; the text in the same units: 2 * digits * 10^exp10 / 2^e.
            var left = 2 * digits;
            var low = xv + xp;
            var high = xv + xn;
            if (exp10 >= 0)
                left *= System.Numerics.BigInteger.Pow(10, exp10);
            else
            {
                var p = System.Numerics.BigInteger.Pow(10, -exp10);
                low *= p;
                high *= p;
            }
            if (e >= 0)
            {
                low <<= e;
                high <<= e;
            }
            else
                left <<= -e;
            bool even = (bits & 1) == 0;
            int cl = left.CompareTo(low), ch = left.CompareTo(high);
            return (cl > 0 || (cl == 0 && even)) && (ch < 0 || (ch == 0 && even));
        }

        static void Exact(double d, out System.Numerics.BigInteger mantissa, out int exponent)
        {
            long bits = BitConverter.DoubleToInt64Bits(d);
            int biased = (int)((bits >> 52) & 0x7FF);
            long fraction = bits & 0xFFFFFFFFFFFFFL;
            if (biased == 0)
            {
                mantissa = fraction;
                exponent = -1074;
            }
            else
            {
                mantissa = fraction | (1L << 52);
                exponent = biased - 1075;
            }
        }

        public static string RoundTrip(float v)
        {
            if (Single.IsNaN(v))
                return "NaN";
            if (Single.IsPositiveInfinity(v))
                return "Infinity";
            if (Single.IsNegativeInfinity(v))
                return "-Infinity";
            if (v == 0)
                return "0";
            string s = v.ToString("G7", Inv);
            float back;
            return Single.TryParse(s, NumberStyles.Float, Inv, out back) && back == v ? s : v.ToString("G9", Inv);
        }

        static void Quote(string s, StringBuilder b)
        {
            b.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\t':
                        b.Append("\\t");
                        break;
                    case '\n':
                        b.Append("\\n");
                        break;
                    case '\r':
                        b.Append("\\r");
                        break;
                    case '"':
                        b.Append("\\\"");
                        break;
                    case '\\':
                        b.Append("\\\\");
                        break;
                    case '\b':
                        b.Append("\\b");
                        break;
                    case '\f':
                        b.Append("\\f");
                        break;
                    default:
                        if (c < ' '
                            || c == '\''
                            || c == '<'
                            || c == '>'
                            || c == '&'
                            || c == '\u0085'
                            || c == (char)0x2028
                            || c == (char)0x2029)
                            b.Append("\\u").Append(((int)c).ToString("x4", Inv));
                        else
                            b.Append(c);
                        break;
                }
            }
            b.Append('"');
        }

        // ---------- reading ----------
        public object DeserializeObject(string json)
        {
            return Convert(Parse(json), typeof(object));
        }

        public T Deserialize<T>(string json)
        {
            return (T)Convert(Parse(json), typeof(T));
        }

        static object Parse(string json)
        {
            if (json == null)
                throw new ArgumentNullException("input");
            int i = 0;
            var v = Value(json, ref i, 0);
            Skip(json, ref i);
            if (i < json.Length)
                throw new ArgumentException("Invalid JSON primitive: " + json.Substring(i) + ".");
            return v;
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && Char.IsWhiteSpace(s[i]))
                i++;
        }

        static object Value(string s, ref int i, int depth)
        {
            if (++depth > RecursionLimit)
                throw new ArgumentException("RecursionLimit exceeded.");
            Skip(s, ref i);
            if (i >= s.Length)
                throw new ArgumentException("Invalid JSON primitive: .");
            char c = s[i];
            if (c == '{')
            {
                i++;
                var d = new Dictionary<string, object>();
                Skip(s, ref i);
                if (i < s.Length && s[i] == '}')
                {
                    i++;
                    return d;
                }
                while (true)
                {
                    Skip(s, ref i);
                    string key = MemberName(s, ref i);
                    Skip(s, ref i);
                    if (i >= s.Length || s[i] != ':')
                        throw new ArgumentException("Invalid object passed in, ':' or '}' expected. (" + i + ")");
                    i++;
                    d[key] = Value(s, ref i, depth);
                    Skip(s, ref i);
                    if (i < s.Length && s[i] == ',')
                    {
                        i++;
                        continue;
                    }
                    if (i < s.Length && s[i] == '}')
                    {
                        i++;
                        return d;
                    }
                    throw new ArgumentException("Invalid object passed in, ':' or '}' expected. (" + i + ")");
                }
            }
            if (c == '[')
            {
                i++;
                var l = new ArrayList();
                Skip(s, ref i);
                if (i < s.Length && s[i] == ']')
                {
                    i++;
                    return l;
                }
                while (true)
                {
                    l.Add(Value(s, ref i, depth));
                    Skip(s, ref i);
                    if (i < s.Length && s[i] == ',')
                    {
                        i++;
                        continue;
                    }
                    if (i < s.Length && s[i] == ']')
                    {
                        i++;
                        return l;
                    }
                    throw new ArgumentException("Invalid array passed in, ',' expected. (" + i + ")");
                }
            }
            if (c == '"' || c == '\'')
            {
                bool date = c == '"' && String.CompareOrdinal(s, i + 1, "\\/Date(", 0, 7) == 0;
                string text = Str(s, ref i);
                if (date)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(text, @"^/Date\((-?[0-9]+)(?:[a-zA-Z]|(?:\+|-)[0-9]{4})?\)/$");
                    if (m.Success)
                        return new DateTime(Int64.Parse(m.Groups[1].Value, Inv) * 10000 + EpochTicks, DateTimeKind.Utc);
                }
                return text;
            }
            int start = i;
            while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']' && !Char.IsWhiteSpace(s[i]))
                i++;
            return Primitive(s.Substring(start, i - start));
        }

        static string MemberName(string s, ref int i)
        {
            if (i < s.Length && (s[i] == '"' || s[i] == '\''))
                return Str(s, ref i);
            int start = i;
            while (i < s.Length && (Char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '$'))
                i++;
            if (i == start)
                throw new ArgumentException("Invalid object passed in, member name expected. (" + i + ")");
            return s.Substring(start, i - start);
        }

        static string Str(string s, ref int i)
        {
            char quote = s[i++];
            var b = new StringBuilder();
            while (true)
            {
                if (i >= s.Length)
                    throw new ArgumentException("Unterminated string passed in. (" + i + ")");
                char c = s[i++];
                if (c == quote)
                    return b.ToString();
                if (c != '\\')
                {
                    b.Append(c);
                    continue;
                }
                if (i >= s.Length)
                    throw new ArgumentException("Unterminated string passed in. (" + i + ")");
                char e = s[i++];
                switch (e)
                {
                    case 'b':
                        b.Append('\b');
                        break;
                    case 't':
                        b.Append('\t');
                        break;
                    case 'n':
                        b.Append('\n');
                        break;
                    case 'f':
                        b.Append('\f');
                        break;
                    case 'r':
                        b.Append('\r');
                        break;
                    case 'u':
                        if (i + 4 > s.Length)
                            throw new ArgumentException("Unrecognized escape sequence. (" + i + ")");
                        b.Append((char)Int32.Parse(s.Substring(i, 4), NumberStyles.HexNumber, Inv));
                        i += 4;
                        break;
                    default:
                        b.Append(e);
                        break;
                }
            }
        }

        static object Primitive(string input)
        {
            if (input == "null")
                return null;
            if (input == "true")
                return true;
            if (input == "false")
                return false;
            bool point = input.IndexOf('.') >= 0;
            if (input.LastIndexOf("e", StringComparison.OrdinalIgnoreCase) < 0)
            {
                if (!point)
                {
                    int n;
                    if (Int32.TryParse(input, NumberStyles.Integer, Inv, out n))
                        return n;
                    long l;
                    if (Int64.TryParse(input, NumberStyles.Integer, Inv, out l))
                        return l;
                }
                decimal m;
                if (Decimal.TryParse(input, NumberStyles.Number, Inv, out m))
                    return m;
            }
            double d;
            if (Double.TryParse(input, NumberStyles.Float, Inv, out d))
                return d;
            throw new ArgumentException("Invalid JSON primitive: " + input + ".");
        }

        // ---------- conversion to the requested type ----------
        static object Convert(object o, Type type)
        {
            if (o == null)
            {
                if (type == typeof(char))
                    return '\0';
                if (type.IsValueType && Nullable.GetUnderlyingType(type) == null)
                    throw new InvalidOperationException("Cannot convert null to a value type.");
                return null;
            }
            if (o.GetType() == type)
                return o;
            var dictionary = o as Dictionary<string, object>;
            if (dictionary != null)
                return FromDictionary(dictionary, type);
            var list = o as ArrayList;
            if (list != null)
                return FromList(list, type);
            if (type == typeof(object))
                return o;
            var target = Nullable.GetUnderlyingType(type) ?? type;
            if (o.GetType() == target)
                return o;
            var converter = TypeDescriptor.GetConverter(type);
            if (converter.CanConvertFrom(o.GetType()))
                return converter.ConvertFrom(null, Inv, o);
            if (converter.CanConvertFrom(typeof(string)))
                return converter.ConvertFromInvariantString(TypeDescriptor.GetConverter(o).ConvertToInvariantString(o));
            if (type.IsAssignableFrom(o.GetType()))
                return o;
            throw new InvalidOperationException("Cannot convert object of type '" + o.GetType() + "' to type '" + type + "'");
        }

        static object FromDictionary(Dictionary<string, object> d, Type type)
        {
            if (type == typeof(object))
            {
                var copy = new Dictionary<string, object>();
                foreach (var e in d)
                    copy[e.Key] = Convert(e.Value, typeof(object));
                return copy;
            }
            var dictType = GenericDictionary(type);
            if (dictType != null)
            {
                var args = dictType.GetGenericArguments();
                if (args[0] != typeof(string) && args[0] != typeof(object))
                    throw new InvalidOperationException(
                        "Type '" + type + "' is not supported for deserialization of a dictionary; keys must be strings or objects.");
                var target = (IDictionary)Activator.CreateInstance(type.IsInterface ? typeof(Dictionary<,>).MakeGenericType(args) : type);
                foreach (var e in d)
                    target[e.Key] = Convert(e.Value, args[1]);
                return target;
            }
            if (type == typeof(IDictionary) || type == typeof(Hashtable))
            {
                var h = new Dictionary<string, object>();
                foreach (var e in d)
                    h[e.Key] = Convert(e.Value, typeof(object));
                return h;
            }
            var instance = Activator.CreateInstance(type);
            foreach (var e in d)
            {
                if (e.Key == "__type")
                    continue;
                var p = type.GetProperty(e.Key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (p != null)
                {
                    var set = p.GetSetMethod();
                    if (set != null)
                    {
                        set.Invoke(instance, new[] { Convert(e.Value, p.PropertyType) });
                        continue;
                    }
                }
                var f = type.GetField(e.Key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                    f.SetValue(instance, Convert(e.Value, f.FieldType));
            }
            return instance;
        }

        static Type GenericDictionary(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                return type;
            foreach (var i in type.GetInterfaces())
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    return i;
            return null;
        }

        static object FromList(ArrayList list, Type type)
        {
            if (type == typeof(object)
                || type.IsArray
                || type == typeof(ArrayList)
                || type == typeof(IEnumerable)
                || type == typeof(IList)
                || type == typeof(ICollection))
            {
                var element = type.IsArray ? type.GetElementType() : typeof(object);
                var converted = new ArrayList();
                foreach (var item in list)
                    converted.Add(Convert(item, element));
                if (type == typeof(ArrayList))
                    return converted;
                return converted.ToArray(element);
            }
            Type element2 = null;
            if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                || type.GetGenericTypeDefinition() == typeof(IList<>)
                || type.GetGenericTypeDefinition() == typeof(ICollection<>)
                || type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                || type.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)))
                element2 = type.GetGenericArguments()[0];
            else
                foreach (var i in type.GetInterfaces())
                    if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>))
                    {
                        element2 = i.GetGenericArguments()[0];
                        break;
                    }
            if (element2 == null)
                throw new InvalidOperationException("Type '" + type + "' is not supported for deserialization of an array.");
            var target = (IList)Activator.CreateInstance(type.IsInterface ? typeof(List<>).MakeGenericType(element2) : type);
            foreach (var item in list)
                target.Add(Convert(item, element2));
            return target;
        }

        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object a, object b)
            {
                return Object.ReferenceEquals(a, b);
            }

            public int GetHashCode(object o)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
            }
        }
    }
}
