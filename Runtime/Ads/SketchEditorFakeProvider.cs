using System;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Editor stand-in: every format is ready, rewarded grants (house rule, so reward flows are playable),
    /// callbacks arrive on the next frame like a real SDK. Toggle SimulateNoFill to test the "no ad" paths.
    /// </summary>
    public sealed class SketchEditorFakeProvider : ISketchAdsProvider, ISketchBannerAdsProvider, ISketchNativeAdsProvider, ISketchBannerLayoutEvents
    {
        public static bool SimulateNoFill;
        public static bool SimulateRewardSkipped;

        bool _bannerVisible;
        SketchNativeCallbacks _native;

        public event Action<float> BannerLayoutChanged;

        public void Initialize(Action<bool> onInitialized)
        {
            SketchLog.Info("Editor fake ads provider active (all formats ready, rewarded grants).");
            onInitialized?.Invoke(true);
        }

        public bool IsReady(SketchAdFormat format) => !SimulateNoFill;

        public void ShowRewarded(Action<bool> onComplete)
        {
            SketchLog.Info("[FakeAds] Rewarded shown -> " + (SimulateRewardSkipped ? "skipped" : "granted"));
            Fullscreen(() => onComplete?.Invoke(!SimulateRewardSkipped));
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
            _native = callbacks;
            SketchLog.Info($"[FakeAds] Native in rect {request.ScreenRect}.");
            callbacks?.Shown();
            return true;
        }

        public void HideNative()
        {
            var callbacks = _native;
            _native = null;
            callbacks?.Closed(SketchNativeCloseReason.SdkClosed);
        }
    }
}
