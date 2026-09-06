using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TonightsTheNight.Util
{
    public enum JsonKind { Null, Bool, Number, String, Array, Object }

    /// <summary>
    /// A small, deliberately lenient JSON reader/writer.
    ///
    /// Hand-rolled rather than pulled from NuGet for three reasons: the mod ships as a single
    /// DLL with no assembly-resolution tricks and no chance of colliding with another mod's
    /// copy of a JSON library; the schema is entirely ours; and config files meant to be edited
    /// by hand really want // comments and trailing commas, which strict JSON forbids.
    /// </summary>
    public sealed class JsonValue
    {
        public JsonKind Kind { get; private set; }

        private readonly bool _bool;
        private readonly double _number;
        private readonly string _string;
        private readonly List<JsonValue> _array;
        private readonly Dictionary<string, JsonValue> _object;

        public static readonly JsonValue Null = new JsonValue();

        private JsonValue() { Kind = JsonKind.Null; }
        private JsonValue(bool v) { Kind = JsonKind.Bool; _bool = v; }
        private JsonValue(double v) { Kind = JsonKind.Number; _number = v; }
        private JsonValue(string v) { Kind = JsonKind.String; _string = v; }
        private JsonValue(List<JsonValue> v) { Kind = JsonKind.Array; _array = v; }
        private JsonValue(Dictionary<string, JsonValue> v) { Kind = JsonKind.Object; _object = v; }

        public static JsonValue Of(bool v) { return new JsonValue(v); }
        public static JsonValue Of(double v) { return new JsonValue(v); }
        public static JsonValue Of(string v) { return v == null ? Null : new JsonValue(v); }
        public static JsonValue NewArray() { return new JsonValue(new List<JsonValue>()); }
        public static JsonValue NewObject() { return new JsonValue(new Dictionary<string, JsonValue>(StringComparer.OrdinalIgnoreCase)); }

        public bool IsObject { get { return Kind == JsonKind.Object; } }
        public bool IsArray { get { return Kind == JsonKind.Array; } }
        public bool IsNull { get { return Kind == JsonKind.Null; } }

        public IEnumerable<KeyValuePair<string, JsonValue>> Members
        {
            get { return _object ?? (IEnumerable<KeyValuePair<string, JsonValue>>)new KeyValuePair<string, JsonValue>[0]; }
        }

        public IReadOnlyList<JsonValue> Items
        {
            get { return (IReadOnlyList<JsonValue>)_array ?? new JsonValue[0]; }
        }

        public int Count { get { return _array != null ? _array.Count : (_object != null ? _object.Count : 0); } }

        public bool Has(string key)
        {
            return _object != null && _object.ContainsKey(key);
        }

        /// <summary>Missing keys return Null rather than throwing, so partial config is fine.</summary>
        public JsonValue this[string key]
        {
            get
            {
                JsonValue value;
                if (_object != null && _object.TryGetValue(key, out value)) { return value; }
                return Null;
            }
        }

        public void Set(string key, JsonValue value)
        {
            if (_object == null) { throw new InvalidOperationException("Not a JSON object."); }
            _object[key] = value ?? Null;
        }

        public void Add(JsonValue value)
        {
            if (_array == null) { throw new InvalidOperationException("Not a JSON array."); }
            _array.Add(value ?? Null);
        }

        public bool AsBool(bool fallback = false)
        {
            if (Kind == JsonKind.Bool) { return _bool; }
            if (Kind == JsonKind.Number) { return Math.Abs(_number) > double.Epsilon; }
            if (Kind == JsonKind.String) { bool b; return bool.TryParse(_string, out b) ? b : fallback; }
            return fallback;
        }

        public double AsDouble(double fallback = 0)
        {
            if (Kind == JsonKind.Number) { return _number; }
            if (Kind == JsonKind.Bool) { return _bool ? 1 : 0; }
            if (Kind == JsonKind.String)
            {
                double d;
                if (double.TryParse(_string, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) { return d; }
            }
            return fallback;
        }

        public float AsFloat(float fallback = 0) { return (float)AsDouble(fallback); }

        public int AsInt(int fallback = 0)
        {
            double d = AsDouble(fallback);
            return (int)Math.Round(d, MidpointRounding.AwayFromZero);
        }

        public string AsString(string fallback = null)
        {
            switch (Kind)
            {
                case JsonKind.String: return _string;
                case JsonKind.Bool: return _bool ? "true" : "false";
                case JsonKind.Number: return _number.ToString(CultureInfo.InvariantCulture);
                default: return fallback;
            }
        }

        public List<string> AsStringList()
        {
            var result = new List<string>();
            if (_array == null)
            {
                string single = AsString(null);
                if (single != null) { result.Add(single); }
                return result;
            }
            foreach (JsonValue item in _array)
            {
                string s = item.AsString(null);
                if (s != null) { result.Add(s); }
            }
            return result;
        }

        // --- parsing -------------------------------------------------------

        public static JsonValue Parse(string text)
        {
            int index = 0;
            JsonValue value = ParseValue(text, ref index);
            SkipTrivia(text, ref index);
            if (index < text.Length)
            {
                throw new FormatException("Unexpected trailing content at offset " + index + ".");
            }
            return value;
        }

        public static bool TryParse(string text, out JsonValue value, out string error)
        {
            try
            {
                value = Parse(text);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                value = Null;
                error = ex.Message;
                return false;
            }
        }

        private static void SkipTrivia(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '/' && i + 1 < s.Length)
                {
                    if (s[i + 1] == '/')
                    {
                        while (i < s.Length && s[i] != '\n') { i++; }
                        continue;
                    }
                    if (s[i + 1] == '*')
                    {
                        i += 2;
                        while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/')) { i++; }
                        i = Math.Min(i + 2, s.Length);
                        continue;
                    }
                }
                return;
            }
        }

        private static JsonValue ParseValue(string s, ref int i)
        {
            SkipTrivia(s, ref i);
            if (i >= s.Length) { throw new FormatException("Unexpected end of input."); }

            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"':
                case '\'': return Of(ParseString(s, ref i));
            }

            if (Match(s, ref i, "true")) { return Of(true); }
            if (Match(s, ref i, "false")) { return Of(false); }
            if (Match(s, ref i, "null")) { return Null; }

            return Of(ParseNumber(s, ref i));
        }

        private static bool Match(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length) { return false; }
            if (string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0) { return false; }
            i += literal.Length;
            return true;
        }

        private static JsonValue ParseObject(string s, ref int i)
        {
            JsonValue result = NewObject();
            i++; // '{'
            while (true)
            {
                SkipTrivia(s, ref i);
                if (i >= s.Length) { throw new FormatException("Unterminated object."); }
                if (s[i] == '}') { i++; return result; }

                string key = s[i] == '"' || s[i] == '\'' ? ParseString(s, ref i) : ParseBareKey(s, ref i);

                SkipTrivia(s, ref i);
                if (i >= s.Length || s[i] != ':') { throw new FormatException("Expected ':' after key '" + key + "'."); }
                i++;

                result.Set(key, ParseValue(s, ref i));

                SkipTrivia(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }   // trailing commas tolerated
                if (i < s.Length && s[i] == '}') { i++; return result; }
                if (i >= s.Length) { throw new FormatException("Unterminated object."); }
                throw new FormatException("Expected ',' or '}' at offset " + i + ".");
            }
        }

        private static string ParseBareKey(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '-' || s[i] == '.')) { i++; }
            if (i == start) { throw new FormatException("Expected an object key at offset " + i + "."); }
            return s.Substring(start, i - start);
        }

        private static JsonValue ParseArray(string s, ref int i)
        {
            JsonValue result = NewArray();
            i++; // '['
            while (true)
            {
                SkipTrivia(s, ref i);
                if (i >= s.Length) { throw new FormatException("Unterminated array."); }
                if (s[i] == ']') { i++; return result; }

                result.Add(ParseValue(s, ref i));

                SkipTrivia(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return result; }
                if (i >= s.Length) { throw new FormatException("Unterminated array."); }
                throw new FormatException("Expected ',' or ']' at offset " + i + ".");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            char quote = s[i];
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i];
                if (c == quote) { i++; return sb.ToString(); }

                if (c == '\\')
                {
                    i++;
                    if (i >= s.Length) { break; }
                    char e = s[i];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case '/': sb.Append('/'); break;
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        case '\'': sb.Append('\''); break;
                        case 'u':
                            if (i + 4 < s.Length)
                            {
                                int code;
                                if (int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                {
                                    sb.Append((char)code);
                                    i += 4;
                                }
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                    i++;
                    continue;
                }

                sb.Append(c);
                i++;
            }
            throw new FormatException("Unterminated string.");
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) { i++; }
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' ||
                                    ((s[i] == '-' || s[i] == '+') && (s[i - 1] == 'e' || s[i - 1] == 'E'))))
            {
                i++;
            }
            string token = s.Substring(start, i - start);
            double d;
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            {
                throw new FormatException("Expected a value at offset " + start + ", found '" + token + "'.");
            }
            return d;
        }

        // --- writing -------------------------------------------------------

        public string ToJson(bool indented = true)
        {
            var sb = new StringBuilder();
            WriteTo(sb, indented, 0);
            return sb.ToString();
        }

        private void WriteTo(StringBuilder sb, bool indented, int depth)
        {
            string pad = indented ? new string(' ', (depth + 1) * 2) : string.Empty;
            string closePad = indented ? new string(' ', depth * 2) : string.Empty;
            string newline = indented ? Environment.NewLine : string.Empty;

            switch (Kind)
            {
                case JsonKind.Null: sb.Append("null"); break;
                case JsonKind.Bool: sb.Append(_bool ? "true" : "false"); break;
                case JsonKind.Number: sb.Append(FormatNumber(_number)); break;
                case JsonKind.String: WriteString(sb, _string); break;

                case JsonKind.Array:
                    if (_array.Count == 0) { sb.Append("[]"); break; }
                    sb.Append('[').Append(newline);
                    for (int i = 0; i < _array.Count; i++)
                    {
                        sb.Append(pad);
                        _array[i].WriteTo(sb, indented, depth + 1);
                        if (i < _array.Count - 1) { sb.Append(','); }
                        sb.Append(newline);
                    }
                    sb.Append(closePad).Append(']');
                    break;

                case JsonKind.Object:
                    if (_object.Count == 0) { sb.Append("{}"); break; }
                    sb.Append('{').Append(newline);
                    int n = 0;
                    foreach (var pair in _object)
                    {
                        sb.Append(pad);
                        WriteString(sb, pair.Key);
                        sb.Append(": ");
                        pair.Value.WriteTo(sb, indented, depth + 1);
                        if (++n < _object.Count) { sb.Append(','); }
                        sb.Append(newline);
                    }
                    sb.Append(closePad).Append('}');
                    break;
            }
        }

        private static string FormatNumber(double d)
        {
            if (Math.Abs(d - Math.Round(d)) < 1e-9 && Math.Abs(d) < 1e15)
            {
                return ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture);
            }
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) { sb.Append("\\u").Append(((int)c).ToString("x4")); }
                        else { sb.Append(c); }
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
