using System;

namespace Sketch.Sdk
{
    /// <summary>Runtime values mapped from a game-owned config asset or bootstrap.</summary>
    public sealed class SketchAdMobConfig
    {
        public string RewardedId { get; set; }
        public string InterstitialId { get; set; }
        public string BannerId { get; set; }
        public string AppOpenId { get; set; }
        public string NativeId { get; set; }
        public string[] TestDeviceIds { get; set; } = Array.Empty<string>();
        public SketchAdMobTag ChildDirected { get; set; }
        public SketchAdMobTag UnderAgeOfConsent { get; set; }
        public SketchAdMobContentRating MaxContentRating { get; set; } = SketchAdMobContentRating.General;
        public bool NonPersonalized { get; set; } = true;
        public string NonPersonalizedExtraKey { get; set; } = "npa";
        public string NonPersonalizedExtraValue { get; set; } = "1";
        public float NativeAttachTimeoutSeconds { get; set; }

        public static bool IsUsableId(string value) => !string.IsNullOrWhiteSpace(value);

        public static bool HasAnyAdUnit(SketchAdMobConfig config) => config != null &&
            (IsUsableId(config.RewardedId) || IsUsableId(config.InterstitialId) || IsUsableId(config.BannerId) ||
             IsUsableId(config.AppOpenId) || IsUsableId(config.NativeId));
    }
}
