using System;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Editor stand-in: every format is ready, rewarded grants (house rule, so reward flows are playable),
    /// callbacks arrive on the next frame like a real SDK. Toggle SimulateNoFill to test the "no ad" paths.
    /// </summary>
    public sealed class SketchEditorFakeProvider : ISketchAdsProvider, ISketchBannerAdsProvider, ISketchNativeAdsProvider, ISketchBannerLayoutEvents, ISketchAdCloseCapability,
        ISketchRewardedInterstitialProvider, ISketchNativeMoveCapability
    {
        public static bool SimulateNoFill;
        public static bool SimulateRewardSkipped;
        /// <summary>Rewarded interstitial only: not loaded (IsReady false), so callers fall back to their plain interstitial.</summary>
        public static bool SimulateRewardedInterstitialNoFill;
        /// <summary>Rewarded interstitial only: the ad shows but the player closes it early (no reward).</summary>
        public static bool SimulateRewardedInterstitialSkipped;
        /// <summary>Rewarded only: not loaded (IsReady false), for "rewarded gone" fallback paths.</summary>
        public static bool SimulateRewardedNoFill;
        /// <summary>Interstitial only: not loaded (IsReady false), for "nothing can start" paths.</summary>
        public static bool SimulateInterstitialNoFill;

        bool _bannerVisible;
        SketchNativeCallbacks _native;

        public event Action<float> BannerLayoutChanged;

        public void Initialize(Action<bool> onInitialized)
        {
            SketchLog.Info("Editor fake ads provider active (all formats ready, rewarded grants).");
            onInitialized?.Invoke(true);
        }

        public bool IsReady(SketchAdFormat format) =>
            !SimulateNoFill && !(format == SketchAdFormat.RewardedInterstitial && SimulateRewardedInterstitialNoFill)
            && !(format == SketchAdFormat.Rewarded && SimulateRewardedNoFill)
            && !(format == SketchAdFormat.Interstitial && SimulateInterstitialNoFill);

        public void ShowRewarded(Action<bool> onComplete)
        {
            SketchLog.Info("[FakeAds] Rewarded shown -> " + (SimulateRewardSkipped ? "skipped" : "granted"));
            Fullscreen(() => onComplete?.Invoke(!SimulateRewardSkipped));
        }

        public void ShowRewardedInterstitial(Action<bool> onComplete)
        {
            bool skipped = SimulateRewardSkipped || SimulateRewardedInterstitialSkipped;
            SketchLog.Info("[FakeAds] Rewarded interstitial shown -> " + (skipped ? "skipped" : "granted"));
            Fullscreen(() => onComplete?.Invoke(!skipped));
        }

        public void ShowInterstitial(Action onClosed)
        {
            SketchLog.Info("[FakeAds] Interstitial shown and closed.");
            Fullscreen(onClosed);
        }

        public void ShowAppOpen(Action onClosed)
        {
            SketchLog.Info("[FakeAds] App-open shown and closed.");
            Fullscreen(onClosed);
        }

        static void Fullscreen(Action onClosed)
        {
            SketchSdk.ReportFullscreen(true);
            SketchSdkLoop.Post(() =>
            {
                SketchSdk.ReportFullscreen(false);
                onClosed?.Invoke();
            });
        }

        public bool IsBannerReady => !SimulateNoFill;
        /// <summary>Simulated adaptive banner height (50 dp) so layouts that avoid the banner can be checked in the Editor.</summary>
        public float BannerHeightPixels => _bannerVisible ? Mathf.Round(50f * Mathf.Max(1f, Screen.dpi / 160f)) : 0f;

        public void ShowBanner()
        {
            if (_bannerVisible) return;
            _bannerVisible = true;
            SketchLog.Info($"[FakeAds] Banner shown ({BannerHeightPixels}px).");
            BannerLayoutChanged?.Invoke(BannerHeightPixels);
        }

        public void HideBanner()
        {
            if (!_bannerVisible) return;
            _bannerVisible = false;
            BannerLayoutChanged?.Invoke(0f);
        }

        public bool IsNativeReady => !SimulateNoFill;

        public bool ShowNativeInRect(SketchNativeRequest request, SketchNativeCallbacks callbacks)
        {
            if (SimulateNoFill) return false;
#if UNITY_EDITOR
            if (!SketchEditorNativeView.Enabled) return false;
#endif
            _native = callbacks;
            Rect rect = SketchNativeInRect.ClampToBanner(request.ScreenRect, Screen.height, _bannerVisible ? BannerHeightPixels : 0f);
            SketchLog.Info($"[FakeAds] Native in rect {rect}.");
            LastNativeRect = rect;
#if UNITY_EDITOR
            // Same layout + round countdown close as the Android custom native; only the user closes it.
            SketchEditorNativeView.Show(rect, request.Placement, OnEditorNativeUserClose);
#endif
            callbacks?.Shown();
            return true;
        }

        public bool MoveNative(Rect screenPixels)
        {
            if (_native == null) return false;
            Rect rect = SketchNativeInRect.ClampToBanner(screenPixels, Screen.height, _bannerVisible ? BannerHeightPixels : 0f);
            if (rect.width <= 0f || rect.height <= 0f) return false;
            LastNativeRect = rect;
#if UNITY_EDITOR
            SketchEditorNativeView.Move(rect);
#endif
            return true;
        }

        /// <summary>Last rect the fake native was shown at or moved to (tests).</summary>
        public static Rect LastNativeRect { get; private set; }

        void OnEditorNativeUserClose()
        {
            var callbacks = _native;
            _native = null;
            callbacks?.Closed(SketchNativeCloseReason.User);
        }

        // In-ad close: the fake draws the same countdown close as the Android layout.
        public bool SupportsInAdClose => true;

        public void HideNative()
        {
#if UNITY_EDITOR
            SketchEditorNativeView.Close();
#endif
            var callbacks = _native;
            _native = null;
            callbacks?.Closed(SketchNativeCloseReason.SdkClosed);
        }
    }
}
