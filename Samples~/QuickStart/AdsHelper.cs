using System;
using Sketch.Sdk;

namespace Sketch.Sdk.Samples
{
    /// <summary>
    /// Example game-side adapter: the ONLY game file that calls the SDK. Gameplay calls AdsHelper; the decision
    /// of WHEN to show (level end, popup close...) stays in the game. Copy it into your game and edit placements.
    /// </summary>
    public static class AdsHelper
    {
        // Placement names show up in analytics and in the per-placement remote config (sketch_ads_placements).
        public const string LEVEL_END = "level_end";
        public const string DOUBLE_REWARD = "double_reward";
        public const string REVIVE = "revive";

        public static bool IsRewardedReady => SketchAds.IsRewardedReady;

        /// <summary>Interstitial between levels; next always runs once (ad or not).</summary>
        public static void LevelEnd(Action next) => SketchAds.ShowInterstitial(LEVEL_END, next);

        /// <summary>Rewarded: onReward runs only when the player earned it; onFail for "no ad / closed early" (show a toast).</summary>
        public static void ShowRewarded(string placement, Action onReward, Action onFail = null) =>
            SketchAds.ShowRewarded(placement, granted =>
            {
                if (granted) onReward?.Invoke();
                else onFail?.Invoke();
            });

        public static void ShowBanner() => SketchAds.ShowBanner();
        public static void HideBanner() => SketchAds.HideBanner();

        /// <summary>Call after the "Remove Ads" purchase is confirmed (and on restore).</summary>
        public static void RemoveAds() => SketchAds.SetAdsRemoved(true);
    }
}
