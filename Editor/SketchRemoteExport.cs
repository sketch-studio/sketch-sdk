using System.Collections.Generic;
using System.Text;

namespace Sketch.Sdk.Editor
{
    /// <summary>Every remote key the game can tune: SDK keys, keys declared in SketchRemoteConfig.Dev.cs, then free-form settings defaults.</summary>
    public static class SketchRemoteKeyCatalog
    {
        public static List<SketchRemoteKeyDef> Build(SketchSdkSettings settings)
        {
            List<SketchRemoteKeyDef> keys = SketchRemoteConfig.SdkKeys(settings);
            var seen = new HashSet<string>();
            foreach (SketchRemoteKeyDef key in keys) seen.Add(key.Key);
            foreach (SketchRemoteKeyDef key in SketchRemoteConfig.GameKeys)
                if (seen.Add(key.Key)) keys.Add(key);
            if (settings != null && settings.RemoteDefaults != null)
            {
                foreach (SketchRemoteDefault d in settings.RemoteDefaults)
                {
                    string name = d?.Key?.Trim();
                    if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                    keys.Add(new SketchRemoteKeyDef(name, GuessType(d.Value), d.Value, "Settings > Remote defaults entry."));
                }
            }
            return keys;
        }

        public static SketchRemoteValueType GuessType(string raw)
        {
            string t = (raw ?? string.Empty).Trim();
            if (t == "true" || t == "false") return SketchRemoteValueType.Bool;
            if (t.Length > 0 && (t[0] == '{' || t[0] == '[')) return SketchRemoteValueType.Json;
            if (int.TryParse(t, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _)) return SketchRemoteValueType.Int;
            if (double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)) return SketchRemoteValueType.Float;
            return SketchRemoteValueType.String;
        }

        /// <summary>{"key": value, ...} with typed values: the Sketch server config format.</summary>
        public static string DefaultsJson(List<SketchRemoteKeyDef> keys)
        {
            var b = new StringBuilder("{\n");
            for (int i = 0; i < keys.Count; i++)
            {
                b.Append("  ");
                SketchJson.WriteString(b, keys[i].Key);
                b.Append(": ").Append(TypedLiteral(keys[i]));
                b.Append(i < keys.Count - 1 ? ",\n" : "\n");
            }
            return b.Append('}').ToString();
        }

        static string TypedLiteral(SketchRemoteKeyDef key)
        {
            switch (key.Type)
            {
                case SketchRemoteValueType.Int:
                case SketchRemoteValueType.Float:
                case SketchRemoteValueType.Bool:
                    return string.IsNullOrEmpty(key.DefaultValue) ? "null" : key.DefaultValue;
                case SketchRemoteValueType.Json:
                    return SketchJson.TryParse(key.DefaultValue, out _) ? key.DefaultValue : "null";
                default:
                    var b = new StringBuilder();
                    SketchJson.WriteString(b, key.DefaultValue);
                    return b.ToString();
            }
        }
    }

    /// <summary>Export formats for remote keys and the Editor override map (pure, tested).</summary>
    public static class SketchRemoteExport
    {
        /// <summary>Firebase Remote Config template (console: Remote Config > menu > Publish from a file).</summary>
        public static string FirebaseTemplateJson(List<SketchRemoteKeyDef> keys)
        {
            var b = new StringBuilder("{\n  \"conditions\": [],\n  \"parameters\": {\n");
            for (int i = 0; i < keys.Count; i++)
            {
                SketchRemoteKeyDef key = keys[i];
                b.Append("    ");
                SketchJson.WriteString(b, key.Key);
                b.Append(": {\n      \"defaultValue\": { \"value\": ");
                SketchJson.WriteString(b, key.DefaultValue);
                b.Append(" },\n      \"description\": ");
                SketchJson.WriteString(b, key.Description);
                b.Append(",\n      \"valueType\": \"").Append(FirebaseType(key.Type)).Append("\"\n    }");
                b.Append(i < keys.Count - 1 ? ",\n" : "\n");
            }
            return b.Append("  }\n}").ToString();
        }

        public static string FirebaseType(SketchRemoteValueType type)
        {
            switch (type)
            {
                case SketchRemoteValueType.Int:
                case SketchRemoteValueType.Float: return "NUMBER";
                case SketchRemoteValueType.Bool: return "BOOLEAN";
                case SketchRemoteValueType.Json: return "JSON";
                default: return "STRING";
            }
        }

        /// <summary>Android res/xml defaults file (FirebaseRemoteConfig.setDefaultsAsync(R.xml.remote_config_defaults)).</summary>
        public static string DefaultsXml(List<SketchRemoteKeyDef> keys)
        {
            var b = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<defaultsMap>\n");
            foreach (SketchRemoteKeyDef key in keys)
                b.Append("  <entry>\n    <key>").Append(Xml(key.Key)).Append("</key>\n    <value>").Append(Xml(key.DefaultValue)).Append("</value>\n  </entry>\n");
            return b.Append("</defaultsMap>\n").ToString();
        }

        static string Xml(string s) => (s ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        public static string OverridesToJson(Dictionary<string, string> map)
        {
            var obj = new Dictionary<string, object>();
            if (map != null) foreach (var pair in map) obj[pair.Key] = pair.Value ?? string.Empty;
            return SketchJson.Serialize(obj);
        }

        public static Dictionary<string, string> ParseOverrides(string json)
        {
            var map = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json) || !SketchJson.TryParse(json, out object root) || !(root is Dictionary<string, object> obj)) return map;
            foreach (var pair in obj)
                map[pair.Key] = pair.Value is string s ? s : SketchJson.Serialize(pair.Value);
            return map;
        }
    }
}
