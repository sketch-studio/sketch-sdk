using System.Collections.Generic;

namespace Sketch.Sdk
{
    /// <summary>
    /// Remote config keys that override ad pacing without a new build. Missing keys keep the SketchSdkSettings value.
    /// sketch_ads_placements is a JSON array: [{"placement":"level_end","enabled":true,"min_session":2,"cooldown":45}].
    /// </summary>
    public static class SketchAdsRemoteKeys
    {
        public const string INTER_START_DELAY = "sketch_inter_start_delay";
        public const string INTER_COOLDOWN = "sketch_inter_cooldown";
        public const string INTER_RESET_ON_REWARDED = "sketch_inter_reset_on_rewarded";
        public const string INTER_MIN_SESSION = "sketch_inter_min_session";
        public const string BANNER_START_DELAY = "sketch_banner_start_delay";
        public const string APP_OPEN_RESUME = "sketch_app_open_resume";
        public const string APP_OPEN_COLD_START = "sketch_app_open_cold_start";
        public const string APP_OPEN_START_DELAY = "sketch_app_open_start_delay";
        public const string APP_OPEN_EVERY = "sketch_app_open_every";
        public const string APP_OPEN_PRESENTATION_DELAY = "sketch_app_open_presentation_delay";
        public const string APP_OPEN_MIN_BACKGROUND = "sketch_app_open_min_background";
        public const string APP_OPEN_AFTER_FULLSCREEN = "sketch_app_open_after_fullscreen";
        public const string PLACEMENTS = "sketch_ads_placements";
        /// <summary>Comma-separated app versions with ads off (Bounce upAppVersion). Rewarded keeps working.</summary>
        public const string ADS_OFF_VERSION = "sketch_ads_off_version";
        /// <summary>Seconds before the native in-rect close can be tapped (OR-879; default SketchSdkSettings.NativeCloseDelaySeconds).</summary>
        public const string NATIVE_CLOSE_DELAY = "sketch_native_close_delay";

        /// <summary>Returns a new settings object: source values overlaid by the remote keys present in values.</summary>
        public static SketchAdsPolicySettings Apply(SketchAdsPolicySettings source, SketchConfigValues values)
        {
            source = source ?? new SketchAdsPolicySettings();
            var result = new SketchAdsPolicySettings
            {
                InterstitialStartDelaySeconds = values.GetFloat(INTER_START_DELAY, source.InterstitialStartDelaySeconds),
                InterstitialCooldownSeconds = values.GetFloat(INTER_COOLDOWN, source.InterstitialCooldownSeconds),
                ResetInterstitialCooldownOnRewarded = values.GetBool(INTER_RESET_ON_REWARDED, source.ResetInterstitialCooldownOnRewarded),
                InterstitialMinSession = values.GetInt(INTER_MIN_SESSION, source.InterstitialMinSession),
                AdsOffVersions = values.GetString(ADS_OFF_VERSION, source.AdsOffVersions ?? string.Empty),
                RemoveAdsBlocks = source.RemoveAdsBlocks,
                BannerStartDelaySeconds = values.GetFloat(BANNER_START_DELAY, source.BannerStartDelaySeconds),
                AppOpenOnResume = values.GetBool(APP_OPEN_RESUME, source.AppOpenOnResume),
                AppOpenOnColdStart = values.GetBool(APP_OPEN_COLD_START, source.AppOpenOnColdStart),
                AppOpenStartDelaySeconds = values.GetFloat(APP_OPEN_START_DELAY, source.AppOpenStartDelaySeconds),
                AppOpenEvery = values.GetInt(APP_OPEN_EVERY, source.AppOpenEvery),
                AppOpenPresentationDelaySeconds = values.GetFloat(APP_OPEN_PRESENTATION_DELAY, source.AppOpenPresentationDelaySeconds),
                AppOpenMinBackgroundSeconds = values.GetFloat(APP_OPEN_MIN_BACKGROUND, source.AppOpenMinBackgroundSeconds),
                AppOpenAfterFullscreenSeconds = values.GetFloat(APP_OPEN_AFTER_FULLSCREEN, source.AppOpenAfterFullscreenSeconds),
                Placements = new List<SketchPlacementRule>(),
            };
            if (source.Placements != null)
                foreach (var rule in source.Placements)
                    if (rule != null) result.Placements.Add(new SketchPlacementRule { Placement = rule.Placement, Enabled = rule.Enabled, MinSession = rule.MinSession, CooldownSeconds = rule.CooldownSeconds });

            if (values.TryGetRaw(PLACEMENTS, out object raw))
            {
                if (raw is string text && SketchJson.TryParse(text, out object parsed)) raw = parsed;
                if (raw is List<object> list)
                {
                    foreach (object item in list)
                    {
                        if (!(item is Dictionary<string, object> obj) || !SketchJson.TryGetString(obj, "placement", out string name) || string.IsNullOrEmpty(name)) continue;
                        var entry = new SketchConfigValues();
                        entry.SetRemote(obj, null);
                        SketchPlacementRule rule = result.FindPlacement(name);
                        if (rule == null) { rule = new SketchPlacementRule { Placement = name, MinSession = result.InterstitialMinSession }; result.Placements.Add(rule); }
                        rule.Enabled = entry.GetBool("enabled", rule.Enabled);
                        rule.MinSession = entry.GetInt("min_session", rule.MinSession);
                        rule.CooldownSeconds = entry.GetFloat("cooldown", rule.CooldownSeconds);
                    }
                }
            }
            return result;
        }
    }
}
