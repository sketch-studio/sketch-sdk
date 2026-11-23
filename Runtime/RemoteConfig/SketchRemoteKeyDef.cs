using System.Globalization;

namespace Sketch.Sdk
{
    public enum SketchRemoteValueType { String, Int, Float, Bool, Json }

    /// <summary>One remote config key: name, type, local default (invariant text), short description, owner (SDK or game).</summary>
    public sealed class SketchRemoteKeyDef
    {
        public string Key { get; }
        public SketchRemoteValueType Type { get; }
        public string DefaultValue { get; }
        public string Description { get; }
        public bool IsSdkKey { get; internal set; }

        public SketchRemoteKeyDef(string key, SketchRemoteValueType type, string defaultValue, string description)
        {
            Key = key ?? string.Empty;
            Type = type;
            DefaultValue = defaultValue ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public static SketchRemoteKeyDef String(string key, string value, string description) => new SketchRemoteKeyDef(key, SketchRemoteValueType.String, value, description);
        public static SketchRemoteKeyDef Int(string key, int value, string description) => new SketchRemoteKeyDef(key, SketchRemoteValueType.Int, value.ToString(CultureInfo.InvariantCulture), description);
        public static SketchRemoteKeyDef Float(string key, float value, string description) => new SketchRemoteKeyDef(key, SketchRemoteValueType.Float, value.ToString("0.###", CultureInfo.InvariantCulture), description);
        public static SketchRemoteKeyDef Bool(string key, bool value, string description) => new SketchRemoteKeyDef(key, SketchRemoteValueType.Bool, value ? "true" : "false", description);
        public static SketchRemoteKeyDef Json(string key, string json, string description) => new SketchRemoteKeyDef(key, SketchRemoteValueType.Json, json, description);

        /// <summary>The default as a config value (number, bool, parsed JSON or text) so getters read it like a server value.</summary>
        public object ParsedDefault
        {
            get
            {
                switch (Type)
                {
                    case SketchRemoteValueType.Int:
                    case SketchRemoteValueType.Float:
                        return double.TryParse(DefaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? (object)d : DefaultValue;
                    case SketchRemoteValueType.Bool:
                        return DefaultValue == "true";
                    case SketchRemoteValueType.Json:
                        return SketchJson.TryParse(DefaultValue, out object parsed) ? parsed : DefaultValue;
                    default:
                        return DefaultValue;
                }
            }
        }
    }
}
