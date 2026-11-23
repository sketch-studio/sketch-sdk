using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Game-facing remote config. Values: server config (GET /config, A/B variant) over the defaults in SketchSdkSettings.
    /// The last server answer is cached, so a returning player gets it before the network answers.
    /// <code>int lives = SketchRemoteConfig.GetInt("start_lives", 3);</code>
    /// </summary>
    public static partial class SketchRemoteConfig
    {
        const string CACHE_KEY = "sketchsdk.rc_cache";
        static readonly SketchConfigValues Values = new SketchConfigValues();

        /// <summary>True after the server answered in this launch (cached values do not count).</summary>
        public static bool IsFetched { get; private set; }
        /// <summary>True once the fetch finished or failed (or was skipped); use it to gate the first screen.</summary>
        public static bool IsReady { get; private set; }
        /// <summary>A/B config id from the server ("Default" or "testId_variant"); stamped on every event.</summary>
        public static string ConfigId => Values.ConfigId;
        /// <summary>Raised on the main thread whenever values change (cache load, server answer).</summary>
        public static event Action Updated;

        public static string GetString(string key, string fallback = "") => Values.GetString(key, fallback);
        public static int GetInt(string key, int fallback = 0) => Values.GetInt(key, fallback);
        public static float GetFloat(string key, float fallback = 0f) => Values.GetFloat(key, fallback);
        public static double GetDouble(string key, double fallback = 0) => Values.GetDouble(key, fallback);
        public static bool GetBool(string key, bool fallback = false) => Values.GetBool(key, fallback);
        public static bool Has(string key) => Values.Has(key);
        public static IEnumerable<string> Keys => Values.Keys;
        internal static SketchConfigValues Store => Values;

        /// <summary>Wait for the first fetch (or timeout). Fires at once when already ready.</summary>
        public static void WhenReady(Action action)
        {
            if (action == null) return;
            if (IsReady) { action(); return; }
            PendingReady.Add(action);
        }

        static readonly List<Action> PendingReady = new List<Action>();

        internal static void ApplyDefaults(SketchSdkSettings settings)
        {
            ApplyGameKeyDefaults();
            ApplyEditorOverrides();
            if (settings?.RemoteDefaults == null) return;
            foreach (SketchRemoteDefault entry in settings.RemoteDefaults)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key)) continue;
                string raw = entry.Value ?? string.Empty;
                // Defaults typed in the Inspector are text; JSON objects/arrays/numbers/bools are parsed so getters behave like server values.
                object value = raw;
                string trimmed = raw.Trim();
                if (trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '[') && SketchJson.TryParse(trimmed, out object parsed)) value = parsed;
                Values.SetDefault(entry.Key.Trim(), value);
            }
        }

        internal static void LoadCache()
        {
            string body = PlayerPrefs.GetString(CACHE_KEY, string.Empty);
            if (string.IsNullOrEmpty(body)) return;
            if (SketchConfigValues.TryParseServerResponse(body, out var config, out string configId))
            {
                Values.SetRemote(config, configId);
                RaiseUpdated();
            }
        }

        internal static void ApplyServerResponse(string body)
        {
            if (!SketchConfigValues.TryParseServerResponse(body, out var config, out string configId))
            {
                SketchLog.Warn("Remote config answer is not {config:{...}}; keeping previous values.");
                return;
            }
            Values.SetRemote(config, configId);
            IsFetched = true;
            PlayerPrefs.SetString(CACHE_KEY, body);
            PlayerPrefs.Save();
            SketchLog.Info($"Remote config fetched: configId={configId}, {config.Count} keys.");
            RaiseUpdated();
        }

        /// <summary>Values from a provider module (Firebase Remote Config). The Sketch server answer still wins.</summary>
        public static void ApplyProviderValues(IDictionary<string, object> values, string source)
        {
            Values.SetProvider(values);
            SketchLog.Info($"Remote config from {source}: {(values != null ? values.Count : 0)} keys.");
            RaiseUpdated();
        }

        internal static void MarkReady()
        {
            if (IsReady) return;
            IsReady = true;
            Action[] callbacks = PendingReady.ToArray();
            PendingReady.Clear();
            foreach (Action callback in callbacks)
            {
                try { callback(); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        static void RaiseUpdated()
        {
            try { Updated?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Values.ClearRemote();
            Values.SetOverrides(null);
            IsFetched = false;
            IsReady = false;
            Updated = null;
            PendingReady.Clear();
        }
    }
}
