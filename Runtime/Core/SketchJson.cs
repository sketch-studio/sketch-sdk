using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sketch.Sdk
{
    /// <summary>Tiny dependency-free JSON reader/writer. Objects parse to Dictionary&lt;string, object&gt;, arrays to List&lt;object&gt;, numbers to double.</summary>
    public static class SketchJson
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var reader = new Reader(json);
            object value = reader.ReadValue();
            reader.SkipWhitespace();
            if (!reader.End) throw new FormatException("Trailing characters in JSON at " + reader.Position);
            return value;
        }

        public static bool TryParse(string json, out object value)
        {
            try { value = Parse(json); return true; }
            catch (Exception) { value = null; return false; }
        }

        public static string Serialize(object value)
        {
            var builder = new StringBuilder(128);
            Write(builder, value);
            return builder.ToString();
        }

        public static void Write(StringBuilder b, object value)
        {
            switch (value)
            {
                case null: b.Append("null"); return;
                case string s: WriteString(b, s); return;
                case bool flag: b.Append(flag ? "true" : "false"); return;
                case double d: WriteNumber(b, d); return;
                case float f: WriteNumber(b, f); return;
                case decimal m: b.Append(m.ToString(CultureInfo.InvariantCulture)); return;
                case int _:
                case long _:
                case short _:
                case byte _:
                case uint _:
                case ulong _:
                case ushort _:
                case sbyte _:
                    b.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); return;
                case Enum e: WriteString(b, e.ToString()); return;
                case IDictionary dict:
                {
                    b.Append('{');
                    bool first = true;
                    foreach (DictionaryEntry entry in dict)
                    {
                        if (!first) b.Append(',');
                        first = false;
                        WriteString(b, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                        b.Append(':');
                        Write(b, entry.Value);
                    }
                    b.Append('}');
                    return;
                }
                case IEnumerable list:
                {
                    b.Append('[');
                    bool first = true;
                    foreach (object item in list)
                    {
                        if (!first) b.Append(',');
                        first = false;
                        Write(b, item);
                    }
                    b.Append(']');
                    return;
                }
                default: WriteString(b, Convert.ToString(value, CultureInfo.InvariantCulture)); return;
            }
        }

        static void WriteNumber(StringBuilder b, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) { b.Append('0'); return; }
            b.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        public static void WriteString(StringBuilder b, string s)
        {
            b.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    case '\b': b.Append("\\b"); break;
                    case '\f': b.Append("\\f"); break;
                    default:
                        if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4"));
                        else b.Append(c);
                        break;
                }
            }
            b.Append('"');
        }

        // ---- typed reads from a parsed object tree ----
        public static bool TryGetString(IDictionary<string, object> obj, string key, out string value)
        {
            value = null;
            if (obj == null || !obj.TryGetValue(key, out object raw) || raw == null) return false;
            value = raw as string ?? Convert.ToString(raw, CultureInfo.InvariantCulture);
            return true;
        }

        sealed class Reader
        {
            readonly string _s;
            int _i;
            public Reader(string s) { _s = s; }
            public int Position => _i;
            public bool End => _i >= _s.Length;

            public void SkipWhitespace()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            public object ReadValue()
            {
                SkipWhitespace();
                if (End) throw new FormatException("Unexpected end of JSON");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ReadNumber();
                }
            }

            void Expect(string word)
            {
                if (_i + word.Length > _s.Length || string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0)
                    throw new FormatException("Invalid token at " + _i);
                _i += word.Length;
            }

            Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                _i++;
                SkipWhitespace();
                if (!End && _s[_i] == '}') { _i++; return result; }
                while (true)
                {
                    SkipWhitespace();
                    if (End || _s[_i] != '"') throw new FormatException("Expected key at " + _i);
                    string key = ReadString();
                    SkipWhitespace();
                    if (End || _s[_i] != ':') throw new FormatException("Expected ':' at " + _i);
                    _i++;
                    result[key] = ReadValue();
                    SkipWhitespace();
                    if (End) throw new FormatException("Unterminated object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return result; }
                    throw new FormatException("Expected ',' or '}' at " + _i);
                }
            }

            List<object> ReadArray()
            {
                var result = new List<object>();
                _i++;
                SkipWhitespace();
                if (!End && _s[_i] == ']') { _i++; return result; }
                while (true)
                {
                    result.Add(ReadValue());
                    SkipWhitespace();
                    if (End) throw new FormatException("Unterminated array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return result; }
                    throw new FormatException("Expected ',' or ']' at " + _i);
                }
            }

            string ReadString()
            {
                _i++;
                var b = new StringBuilder();
                while (true)
                {
                    if (End) throw new FormatException("Unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return b.ToString();
                    if (c != '\\') { b.Append(c); continue; }
                    if (End) throw new FormatException("Bad escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': b.Append('"'); break;
                        case '\\': b.Append('\\'); break;
                        case '/': b.Append('/'); break;
                        case 'b': b.Append('\b'); break;
                        case 'f': b.Append('\f'); break;
                        case 'n': b.Append('\n'); break;
                        case 'r': b.Append('\r'); break;
                        case 't': b.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw new FormatException("Bad unicode escape");
                            b.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw new FormatException("Bad escape");
                    }
                }
            }

            object ReadNumber()
            {
                int start = _i;
                while (_i < _s.Length && "+-0123456789.eE".IndexOf(_s[_i]) >= 0) _i++;
                if (start == _i) throw new FormatException("Unexpected character at " + start);
                return double.Parse(_s.Substring(start, _i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }
}
