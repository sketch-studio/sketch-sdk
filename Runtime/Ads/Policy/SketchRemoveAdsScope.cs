using System;

namespace Sketch.Sdk
{
    /// <summary>
    /// Formats a remove-ads purchase blocks (SketchAdsPolicySettings.RemoveAdsBlocks). Rewarded is never blocked.
    /// Default keeps the behaviour of SDK 1.4.1 and earlier: interstitial, app open, banner and native off, RI kept.
    /// </summary>
    [Flags]
    public enum SketchRemoveAdsScope
    {
        None = 0,
        Interstitial = 1,
        AppOpen = 2,
        Banner = 4,
        Native = 8,
        RewardedInterstitial = 16,
        Default = Interstitial | AppOpen | Banner | Native,
        All = Interstitial | AppOpen | Banner | Native | RewardedInterstitial,
    }
}
