using System;

namespace Sketch.Sdk
{
    public enum SketchBannerPosition { Bottom, Top, BottomLeft, BottomRight, TopLeft, TopRight, Center }
    public enum SketchBannerSize { Adaptive, Standard, MediumRectangle, IabBanner, Leaderboard }
    public enum SketchBannerOrientation { Current, Portrait, Landscape }

    [Serializable]
    public sealed class SketchAdMobBannerSlot
    {
        public string Name = "";
        public string AndroidId = "";
        public string IosId = "";
        public SketchBannerPosition Position = SketchBannerPosition.Bottom;
        public SketchBannerSize Size = SketchBannerSize.Adaptive;
        public SketchBannerOrientation Orientation;
        public int AdaptiveWidth;
    }

    public interface ISketchNamedBannerProvider
    {
        bool SetBannerRequested(string name, bool requested);
        string GetBannerName(int index);
        bool PreloadBanner(string name);
        void PreloadAllBanners();
        void SetAllBannersRequested(bool requested);
        bool ReleaseBanner(string name);
        void ReleaseAllBanners();
    }

    /// <summary>Runtime values mapped from a game-owned config asset or bootstrap.</summary>
    public sealed class SketchAdMobConfig
    {
        public string RewardedId { get; set; }
        public string InterstitialId { get; set; }
        public string BannerId { get; set; }
        public SketchAdMobBannerSlot[] BannerSlots { get; set; } = Array.Empty<SketchAdMobBannerSlot>();
        public bool UseTestBannerSlots { get; set; }
        public string AppOpenId { get; set; }
        public string NativeId { get; set; }
        public string RewardedInterstitialId { get; set; }
        public string[] TestDeviceIds { get; set; } = Array.Empty<string>();
        public SketchAdMobTag ChildDirected { get; set; }
        public SketchAdMobTag UnderAgeOfConsent { get; set; }
        public SketchAdMobContentRating MaxContentRating { get; set; } = SketchAdMobContentRating.General;
        public bool NonPersonalized { get; set; } = true;
        /// <summary>Opt out of data sale on mediation networks that need an explicit call (US-state privacy, minors).</summary>
        public bool RestrictDataSale { get; set; }
        /// <summary>Tags came from the player's age bracket (Require Age Signal); mixed-audience network signals apply.</summary>
        public bool AgeSignalActive { get; set; }
        public string NonPersonalizedExtraKey { get; set; } = "npa";
        public string NonPersonalizedExtraValue { get; set; } = "1";
        public float NativeAttachTimeoutSeconds { get; set; }

        public static bool IsUsableId(string value) => !string.IsNullOrWhiteSpace(value);

        public static bool HasAnyAdUnit(SketchAdMobConfig config) => config != null &&
            (IsUsableId(config.RewardedId) || IsUsableId(config.InterstitialId) || IsUsableId(config.BannerId) ||
             IsUsableId(config.AppOpenId) || IsUsableId(config.NativeId) || IsUsableId(config.RewardedInterstitialId) ||
             (config.BannerSlots != null && config.BannerSlots.Length > 0));
    }
}
