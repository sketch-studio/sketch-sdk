namespace Sketch.Sdk
{
    /// <summary>Pure ad pacing rules (no Unity, no provider). All times are seconds on one clock supplied by the caller.</summary>
    public sealed class SketchAdsPolicy
    {
        double _firstOpen;
        double _lastFullscreen = double.NegativeInfinity;

        public SketchAdsPolicySettings Settings { get; set; }
        /// <summary>1-based session counter.</summary>
        public int Session { get; set; } = 1;
        /// <summary>Remove-ads purchased: blocks interstitial, app-open and banner; rewarded stays available.</summary>
        public bool AdsRemoved { get; set; }
        /// <summary>Why the last Can* call returned false (empty when allowed).</summary>
        public string LastBlockReason { get; private set; } = string.Empty;

        public SketchAdsPolicy(SketchAdsPolicySettings settings, double firstOpenTime)
        {
            Settings = settings ?? new SketchAdsPolicySettings();
            _firstOpen = firstOpenTime;
        }

        public double LastFullscreenTime => _lastFullscreen;

        public bool CanShowBanner(double now)
        {
            if (AdsRemoved) return Block("ads_removed");
            if (now - _firstOpen < Settings.BannerStartDelaySeconds) return Block("banner_start_delay");
            return Pass();
        }

        public bool CanShowInterstitial(string placement, double now, bool skipCooldown = false)
        {
            if (AdsRemoved) return Block("ads_removed");
            SketchPlacementRule rule = Settings.FindPlacement(placement);
            if (rule != null && !rule.Enabled) return Block("placement_disabled");
            int minSession = rule != null ? rule.MinSession : Settings.InterstitialMinSession;
            if (Session < minSession) return Block("session_gate");
            if (now - _firstOpen < Settings.InterstitialStartDelaySeconds) return Block("start_delay");
            float cooldown = rule != null && rule.CooldownSeconds >= 0f ? rule.CooldownSeconds : Settings.InterstitialCooldownSeconds;
            if (!skipCooldown && now - _lastFullscreen < cooldown) return Block("cooldown");
            return Pass();
        }

        public bool CanShowAppOpen(double now, bool coldStart, double backgroundSeconds)
        {
            if (AdsRemoved) return Block("ads_removed");
            if (coldStart ? !Settings.AppOpenOnColdStart : !Settings.AppOpenOnResume) return Block("app_open_disabled");
            if (now - _firstOpen < Settings.AppOpenStartDelaySeconds) return Block("start_delay");
            if (!coldStart && backgroundSeconds < Settings.AppOpenMinBackgroundSeconds) return Block("short_background");
            if (now - _lastFullscreen < Settings.AppOpenAfterFullscreenSeconds) return Block("after_fullscreen");
            return Pass();
        }

        /// <summary>Call when any fullscreen ad (interstitial, rewarded, app open) closes.</summary>
        public void OnFullscreenClosed(double now) => _lastFullscreen = now;
        /// <summary>Restart the interstitial cooldown without showing one.</summary>
        public void ResetInterstitialCooldown(double now) => _lastFullscreen = now;

        bool Block(string reason) { LastBlockReason = reason; return false; }
        bool Pass() { LastBlockReason = string.Empty; return true; }
    }
}
