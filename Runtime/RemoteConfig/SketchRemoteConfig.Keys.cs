using System.Collections.Generic;

namespace Sketch.Sdk
{
    /// <summary>
    /// SDK-owned half of SketchRemoteConfig: every SDK key with type, default and description, typed accessors, and the hook
    /// for game keys. The game declares its own keys in Assets/SketchDev/SketchRemoteConfig.Dev.cs (created once by the
    /// Editor, never overwritten by SDK updates; SketchDev.asmref compiles it into this assembly) by implementing RegisterGameKeys.
    /// </summary>
    public static partial class SketchRemoteConfig
    {
        /// <summary>EditorPrefs key prefix (+ Application.dataPath) of the local overrides typed in Sketch SDK > Manager > Remote Config.</summary>
        public const string EDITOR_OVERRIDES_PREF = "SketchSdk.RemoteOverrides.";

        static List<SketchRemoteKeyDef> _gameKeys;

        /// <summary>Implemented by the game in Assets/SketchDev/SketchRemoteConfig.Dev.cs: add one SketchRemoteKeyDef per game key.</summary>
        static partial void RegisterGameKeys(List<SketchRemoteKeyDef> keys);

        /// <summary>Keys the game declared in SketchRemoteConfig.Dev.cs (empty when it declares none).</summary>
        public static IReadOnlyList<SketchRemoteKeyDef> GameKeys
        {
            get
            {
                if (_gameKeys == null)
                {
                    var keys = new List<SketchRemoteKeyDef>();
                    RegisterGameKeys(keys);
                    keys.RemoveAll(k => k == null || string.IsNullOrWhiteSpace(k.Key));
                    _gameKeys = keys;
                }
                return _gameKeys;
            }
        }

        /// <summary>Every SDK key with its local default taken from the settings asset (code defaults when null).</summary>
        public static List<SketchRemoteKeyDef> SdkKeys(SketchSdkSettings settings)
        {
            SketchAdsPolicySettings p = settings != null && settings.Pacing != null ? settings.Pacing : new SketchAdsPolicySettings();
            var keys = new List<SketchRemoteKeyDef>
            {
                SketchRemoteKeyDef.Float(SketchAdsRemoteKeys.INTER_START_DELAY, p.InterstitialStartDelaySeconds, "Seconds after the first app open before any interstitial."),
                SketchRemoteKeyDef.Float(SketchAdsRemoteKeys.INTER_COOLDOWN, p.InterstitialCooldownSeconds, "Minimum seconds between an interstitial and the previous fullscreen ad."),
                SketchRemoteKeyDef.Int(SketchAdsRemoteKeys.INTER_MIN_SESSION, p.InterstitialMinSession, "Session number from which interstitials may show (1 = first)."),
                SketchRemoteKeyDef.Float(SketchAdsRemoteKeys.BANNER_START_DELAY, p.BannerStartDelaySeconds, "Seconds after the first app open before the banner shows."),
                SketchRemoteKeyDef.Bool(SketchAdsRemoteKeys.APP_OPEN_RESUME, p.AppOpenOnResume, "Show an app-open ad when the app comes back from background."),
                SketchRemoteKeyDef.Bool(SketchAdsRemoteKeys.APP_OPEN_COLD_START, p.AppOpenOnColdStart, "Show an app-open ad once on cold start (splash)."),
                SketchRemoteKeyDef.Float(SketchAdsRemoteKeys.APP_OPEN_START_DELAY, p.AppOpenStartDelaySeconds, "Seconds after the first app open before any app-open ad."),
                SketchRemoteKeyDef.Int(SketchAdsRemoteKeys.APP_OPEN_EVERY, p.AppOpenEvery, "Number of eligible app opens per app-open ad."),
                SketchRemoteKeyDef.Float(SketchAdsRemoteKeys.APP_OPEN_PRESENTATION_DELAY, p.AppOpenPresentationDelaySeconds, "Delay after the eligible opening event."),
                SketchRemoteKeyDef.Float(SketchAdsRemoteKeys.APP_OPEN_MIN_BACKGROUND, p.AppOpenMinBackgroundSeconds, "Seconds in background needed before a resume app-open ad."),
                SketchRemoteKeyDef.Float(SketchAdsRemoteKeys.APP_OPEN_AFTER_FULLSCREEN, p.AppOpenAfterFullscreenSeconds, "No app-open ad this many seconds after any fullscreen ad."),
                SketchRemoteKeyDef.Json(SketchAdsRemoteKeys.PLACEMENTS, "[]", "Per-placement interstitial rules, e.g. [{\"placement\":\"level_end\",\"enabled\":true,\"min_session\":2,\"cooldown\":45}]."),
                SketchRemoteKeyDef.String(SketchAdsRemoteKeys.ADS_OFF_VERSION, p.AdsOffVersions ?? string.Empty, "Comma-separated app versions with ads off (rewarded keeps working)."),
                SketchRemoteKeyDef.Float(SketchAdsRemoteKeys.NATIVE_CLOSE_DELAY, settings != null ? settings.NativeCloseDelaySeconds : SketchNativeClose.DEFAULT_COUNTDOWN_SECONDS,
                    "Seconds before the native in-rect close (X) can be tapped (0 = at once, max 30)."),
                SketchRemoteKeyDef.Bool(SketchNotificationsRemoteKeys.ENABLED, true, "Kill switch for local notifications (false = none scheduled)."),
                SketchRemoteKeyDef.Int(SketchAppUpdatePolicy.FORCE_MIN_VERSION_CODE_KEY, 0, "Force an immediate update when the installed versionCode is below this (0 = off)."),
            };
            foreach (SketchRemoteKeyDef key in keys) key.IsSdkKey = true;
            return keys;
        }

        // ---------------- typed accessors for SDK keys (local default = settings asset) ----------------

        static SketchAdsPolicySettings Pacing => SketchSdkSettings.Load()?.Pacing ?? new SketchAdsPolicySettings();

        public static float InterstitialStartDelaySeconds => GetFloat(SketchAdsRemoteKeys.INTER_START_DELAY, Pacing.InterstitialStartDelaySeconds);
        public static float InterstitialCooldownSeconds => GetFloat(SketchAdsRemoteKeys.INTER_COOLDOWN, Pacing.InterstitialCooldownSeconds);
        public static int InterstitialMinSession => GetInt(SketchAdsRemoteKeys.INTER_MIN_SESSION, Pacing.InterstitialMinSession);
        public static float BannerStartDelaySeconds => GetFloat(SketchAdsRemoteKeys.BANNER_START_DELAY, Pacing.BannerStartDelaySeconds);
        public static bool AppOpenOnResume => GetBool(SketchAdsRemoteKeys.APP_OPEN_RESUME, Pacing.AppOpenOnResume);
        public static bool AppOpenOnColdStart => GetBool(SketchAdsRemoteKeys.APP_OPEN_COLD_START, Pacing.AppOpenOnColdStart);
        public static string AdsOffVersions => GetString(SketchAdsRemoteKeys.ADS_OFF_VERSION, Pacing.AdsOffVersions ?? string.Empty);
        public static bool NotificationsEnabled => GetBool(SketchNotificationsRemoteKeys.ENABLED, true);
        public static int ForceUpdateMinVersionCode => GetInt(SketchAppUpdatePolicy.FORCE_MIN_VERSION_CODE_KEY, 0);

        /// <summary>Game key defaults become config defaults, so GetX(key) without a fallback returns the declared default.</summary>
        static void ApplyGameKeyDefaults()
        {
            foreach (SketchRemoteKeyDef key in GameKeys) Values.SetDefault(key.Key.Trim(), key.ParsedDefault);
        }

        /// <summary>Editor only: loads the local overrides typed in the Manager window (top layer). No-op in players.</summary>
        public static void ApplyEditorOverrides()
        {
#if UNITY_EDITOR
            string json = UnityEditor.EditorPrefs.GetString(EDITOR_OVERRIDES_PREF + UnityEngine.Application.dataPath, string.Empty);
            Dictionary<string, object> map = null;
            if (!string.IsNullOrEmpty(json) && SketchJson.TryParse(json, out object parsed) && parsed is Dictionary<string, object> obj && obj.Count > 0)
            {
                map = new Dictionary<string, object>(obj.Count);
                foreach (var pair in obj)
                {
                    object value = pair.Value;
                    if (value is string text)
                    {
                        string t = text.Trim();
                        if (t.Length > 0 && (t[0] == '{' || t[0] == '[') && SketchJson.TryParse(t, out object inner)) value = inner;
                    }
                    map[pair.Key] = value;
                }
            }
            Values.SetOverrides(map);
            if (map != null) SketchLog.Info($"Remote config: {map.Count} Editor override(s) active.");
            RaiseUpdated();
#endif
        }
    }
}
