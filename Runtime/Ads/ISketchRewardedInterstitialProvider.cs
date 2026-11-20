using System;

namespace Sketch.Sdk
{
    /// <summary>
    /// Optional capability: AdMob "rewarded interstitial" (a rewarded ad shown at a natural break without an opt-in button).
    /// Readiness goes through ISketchAdsProvider.IsReady(SketchAdFormat.RewardedInterstitial).
    /// onComplete fires once: true only when the reward was earned.
    /// </summary>
    public interface ISketchRewardedInterstitialProvider
    {
        void ShowRewardedInterstitial(Action<bool> onComplete);
    }
}
