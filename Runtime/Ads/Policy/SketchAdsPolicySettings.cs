using System;
using System.Collections.Generic;

namespace Sketch.Sdk
{
    /// <summary>Per-placement interstitial override (name = placement string passed to ShowInterstitial).</summary>
    [Serializable]
    public sealed class SketchPlacementRule
    {
        public string Placement = "";
        public bool Enabled = true;
        /// <summary>Session number (1 = first session) from which this placement may show.</summary>
        public int MinSession = 1;
        /// <summary>Cooldown for this placement; negative = use the global cooldown.</summary>
        public float CooldownSeconds = -1f;
    }

    /// <summary>Tunable ad pacing numbers (Bounce defaults). Values come from SketchSdkSettings and may be overridden by remote config.</summary>
    [Serializable]
    public sealed class SketchAdsPolicySettings
    {
        /// <summary>No interstitial until this many seconds after the FIRST app open (install).</summary>
        public float InterstitialStartDelaySeconds = 30f;
        /// <summary>Minimum seconds between an interstitial and the previous fullscreen ad (interstitial, rewarded or app open).</summary>
        public float InterstitialCooldownSeconds = 30f;
        public bool ResetInterstitialCooldownOnRewarded = true;
        /// <summary>Session number from which interstitials may show (1 = first session).</summary>
        public int InterstitialMinSession = 1;
        /// <summary>Comma-separated app versions (exact, trimmed) that show no banner, interstitial, app open, native or rewarded interstitial. Rewarded keeps working. Empty = off.</summary>
        public string AdsOffVersions = "";
        /// <summary>Formats a remove-ads purchase blocks. Rewarded is never blocked. Default = SDK 1.4.1 behaviour.</summary>
        public SketchRemoveAdsScope RemoveAdsBlocks = SketchRemoveAdsScope.Default;
        public List<SketchPlacementRule> Placements = new List<SketchPlacementRule>();
        /// <summary>Banner stays hidden until this many seconds after the first app open.</summary>
        public float BannerStartDelaySeconds;
        /// <summary>Show an app-open ad when the app returns from background.</summary>
        public bool AppOpenOnResume = true;
        /// <summary>Show an app-open ad once on cold start when it loads in time.</summary>
        public bool AppOpenOnColdStart = true;
        /// <summary>No app-open ad until this many seconds after the first app open.</summary>
        public float AppOpenStartDelaySeconds;
        /// <summary>Eligible opens between app-open ads. One retains the default behavior.</summary>
        public int AppOpenEvery = 1;
        /// <summary>Seconds to wait after an eligible cold start or resume.</summary>
        public float AppOpenPresentationDelaySeconds;
        /// <summary>Background time needed before resume shows an app-open ad.</summary>
        public float AppOpenMinBackgroundSeconds = 3f;
        /// <summary>No app-open ad within this many seconds after any fullscreen ad (the resume is usually the ad closing).</summary>
        public float AppOpenAfterFullscreenSeconds = 30f;

        public SketchPlacementRule FindPlacement(string placement)
        {
            if (Placements == null || string.IsNullOrEmpty(placement)) return null;
            for (int i = 0; i < Placements.Count; i++)
                if (Placements[i] != null && string.Equals(Placements[i].Placement, placement, StringComparison.Ordinal)) return Placements[i];
            return null;
        }
    }
}
