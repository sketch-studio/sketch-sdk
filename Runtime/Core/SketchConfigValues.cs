using System;
using System.Collections.Generic;
using System.Globalization;

namespace Sketch.Sdk
{
    /// <summary>Layered key/value store: remote values override local defaults. Pure C#, no Unity.</summary>
    public sealed class SketchConfigValues
    {
        readonly Dictionary<string, object> _defaults = new Dictionary<string, object>(StringComparer.Ordinal);
        readonly Dictionary<string, object> _remote = new Dictionary<string, object>(StringComparer.Ordinal);

        public string ConfigId { get; private set; } = string.Empty;
        public bool HasRemote { get; private set; }
        public IEnumerable<string> Keys
        {
            get
            {
                foreach (var key in _defaults.Keys) yield return key;
                foreach (var key in _remote.Keys) if (!_defaults.ContainsKey(key)) yield return key;
            }
        }

        public void SetDefault(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) return;
            _defaults[key] = value;
        }

        /// <summary>Replace remote values (top-level keys of the server "config" object).</summary>
        public void SetRemote(IDictionary<string, object> values, string configId)
        {
            _remote.Clear();
            if (values != null) foreach (var pair in values) _remote[pair.Key] = pair.Value;
            ConfigId = configId ?? string.Empty;
            HasRemote = values != null;
        }

        public void ClearRemote()
        {
            _remote.Clear();
            ConfigId = string.Empty;
            HasRemote = false;
        }

        public bool Has(string key) => key != null && (_remote.ContainsKey(key) || _defaults.ContainsKey(key));

        public bool TryGetRaw(string key, out object value)
        {
            if (key != null && _remote.TryGetValue(key, out value)) return true;
            if (key != null && _defaults.TryGetValue(key, out value)) return true;
            value = null;
            return false;
        }

        public string GetString(string key, string fallback = "")
        {
            if (!TryGetRaw(key, out object raw) || raw == null) return fallback;
            if (raw is string s) return s;
            if (raw is IDictionary<string, object> || raw is IList<object>) return SketchJson.Serialize(raw);
            if (raw is bool b) return b ? "true" : "false";
            return Convert.ToString(raw, CultureInfo.InvariantCulture);
        }

        public double GetDouble(string key, double fallback = 0)
        {
            if (!TryGetRaw(key, out object raw) || raw == null) return fallback;
            switch (raw)
            {
                case double d: return d;
                case float f: return f;
                case int i: return i;
                case long l: return l;
                case bool b: return b ? 1 : 0;
                case string s:
                    return double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : fallback;
                default: return fallback;
            }
        }

        public float GetFloat(string key, float fallback = 0f) => (float)GetDouble(key, fallback);

        public int GetInt(string key, int fallback = 0)
        {
            double value = GetDouble(key, double.NaN);
            if (double.IsNaN(value) || value > int.MaxValue || value < int.MinValue) return fallback;
            return (int)Math.Round(value);
        }

        public bool GetBool(string key, bool fallback = false)
        {
            if (!TryGetRaw(key, out object raw) || raw == null) return fallback;
            switch (raw)
            {
                case bool b: return b;
                case double d: return d != 0;
                case string s:
                    s = s.Trim();
                    if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
                    if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase) || s.Equals("no", StringComparison.OrdinalIgnoreCase)) return false;
                    return fallback;
                default: return fallback;
            }
        }

        /// <summary>Parse the server response {ok, configId, config:{...}}. Returns false when the body is not a config response.</summary>
        public static bool TryParseServerResponse(string body, out Dictionary<string, object> config, out string configId)
        {
            config = null;
            configId = null;
            if (!SketchJson.TryParse(body, out object root) || !(root is Dictionary<string, object> obj)) return false;
            if (obj.TryGetValue("ok", out object ok) && ok is bool okFlag && !okFlag) return false;
            if (!obj.TryGetValue("config", out object cfg) || !(cfg is Dictionary<string, object> cfgObj)) return false;
            config = cfgObj;
            SketchJson.TryGetString(obj, "configId", out configId);
            return true;
        }
    }
}
