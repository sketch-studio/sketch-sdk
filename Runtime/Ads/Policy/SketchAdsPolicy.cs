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
        /// <summary>Remove-ads purchased: blocks the formats listed in Settings.RemoveAdsBlocks; rewarded stays available.</summary>
        public bool AdsRemoved { get; set; }
        /// <summary>Why the last Can* call returned false (empty when allowed).</summary>
        public string LastBlockReason { get; private set; } = string.Empty;

        public SketchAdsPolicy(SketchAdsPolicySettings settings, double firstOpenTime)
        {
            Settings = settings ?? new SketchAdsPolicySettings();
            _firstOpen = firstOpenTime;
        }

        /// <summary>True when the remove-ads purchase is owned and Settings.RemoveAdsBlocks lists this format.</summary>
        public bool IsRemoved(SketchRemoveAdsScope format) => AdsRemoved && (Settings.RemoveAdsBlocks & format) != 0;

        public double LastFullscreenTime => _lastFullscreen;
        /// <summary>App version of this build (the facade passes Application.version); compared with Settings.AdsOffVersions.</summary>
        public string AppVersion { get; set; } = string.Empty;

        /// <summary>True when AppVersion exactly matches one trimmed entry of the comma-separated Settings.AdsOffVersions.</summary>
        public bool IsAdsOffForVersion
        {
            get
            {
                // Cached per (list, version) string instance: readiness checks run every frame and Split allocates.
                string list = Settings.AdsOffVersions, version = AppVersion;
                if (!ReferenceEquals(list, _offList) || !ReferenceEquals(version, _offVersion))
                {
                    _offList = list;
                    _offVersion = version;
                    _offCached = IsVersionListed(list, version);
                }
                return _offCached;
            }
        }
        string _offList, _offVersion;
        bool _offCached;

        /// <summary>Pure: exact, trimmed match of version against a comma-separated list. Empty list or version = false.</summary>
        public static bool IsVersionListed(string list, string version)
        {
            if (string.IsNullOrWhiteSpace(list) || string.IsNullOrWhiteSpace(version)) return false;
            string v = version.Trim();
            string[] entries = list.Split(',');
            for (int i = 0; i < entries.Length; i++)
                if (string.Equals(entries[i].Trim(), v, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Rewarded is never blocked by pacing, remove-ads or the ads-off version, so reward buttons never break.</summary>
        public bool CanShowRewarded() => Pass();

        /// <summary>Rewarded interstitial: kept under remove-ads unless RemoveAdsBlocks lists it; blocked for an ads-off app version.</summary>
        public bool CanShowRewardedInterstitial()
        {
            if (IsRemoved(SketchRemoveAdsScope.RewardedInterstitial)) return Block("ads_removed");
            if (IsAdsOffForVersion) return Block("ads_off_version");
            return Pass();
        }

        public bool CanShowNative()
        {
            if (IsRemoved(SketchRemoveAdsScope.Native)) return Block("ads_removed");
            if (IsAdsOffForVersion) return Block("ads_off_version");
            return Pass();
        }

        public bool CanShowBanner(double now)
        {
            if (IsRemoved(SketchRemoveAdsScope.Banner)) return Block("ads_removed");
            if (IsAdsOffForVersion) return Block("ads_off_version");
            if (now - _firstOpen < Settings.BannerStartDelaySeconds) return Block("banner_start_delay");
            return Pass();
        }

        public bool CanShowInterstitial(string placement, double now, bool skipCooldown = false)
        {
            if (IsRemoved(SketchRemoveAdsScope.Interstitial)) return Block("ads_removed");
            if (IsAdsOffForVersion) return Block("ads_off_version");
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
            if (IsRemoved(SketchRemoveAdsScope.AppOpen)) return Block("ads_removed");
            if (IsAdsOffForVersion) return Block("ads_off_version");
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
