using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// The ads API a game calls (Bounce-style). Pacing, remove-ads, app-open on resume, placement logging and
    /// audio pause are handled here; the provider (AdMob or the Editor fake) only shows ads.
    /// <code>
    /// SketchAds.ShowInterstitial("level_end", () => LoadNextLevel());
    /// SketchAds.ShowRewarded("double_coins", granted => { if (granted) coins *= 2; });
    /// </code>
    /// </summary>
    public static class SketchAds
    {
        static SketchAdsPolicy _policy;
        static ISketchAdsRemovedStore _adsRemovedStore;
        static readonly Dictionary<string, string> CurrentPlacement = new Dictionary<string, string>();
        static bool _bannerWanted;
        static bool _bannerShown;
        static double _focusLostAt = -1;
        static int _appOpenSuppress;
        static bool _audioPausedByAds;
        static bool _wasAudioPaused;

        /// <summary>Pause AudioListener while a fullscreen ad shows (iOS keeps game audio running otherwise).</summary>
        public static bool PauseAudioDuringAds { get; set; } = true;
        /// <summary>The live pacing rules (tweak at runtime or through remote config).</summary>
        public static SketchAdsPolicy Policy => _policy ??= CreatePolicy(new SketchAdsPolicySettings());
        /// <summary>Raised on the main thread when a fullscreen ad opens (true) or closes (false). Pause gameplay here.</summary>
        public static event Action<bool> FullscreenAdChanged;
        /// <summary>Raised when a rewarded ad finished: (placement, granted).</summary>
        public static event Action<string, bool> RewardedFinished;
        /// <summary>Raised when a rewarded interstitial finished: (placement, granted).</summary>
        public static event Action<string, bool> RewardedInterstitialFinished;

        public static bool IsInitialized => SketchSdk.IsInitialized;

        // ---------------- remove ads ----------------

        /// <summary>Remove-ads purchased: no banner, interstitial, app open or native. Rewarded and rewarded interstitial keep working.</summary>
        public static bool IsAdsRemoved => Policy.AdsRemoved;

        /// <summary>Default remove-ads store (SDK 1.2 key), kept for backward compatibility with existing saves.</summary>
        sealed class PlayerPrefsAdsRemovedStore : ISketchAdsRemovedStore
        {
            const string KEY = "sketchsdk.remove_ads";
            public bool Load() => PlayerPrefs.GetInt(KEY, 0) == 1;
            public void Save(bool removed)
            {
                PlayerPrefs.SetInt(KEY, removed ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        static ISketchAdsRemovedStore AdsRemovedStore => _adsRemovedStore ??= new PlayerPrefsAdsRemovedStore();

        /// <summary>
        /// Persist the remove-ads flag in the game's own save (e.g. a DataBlock) instead of the PlayerPrefs default.
        /// Call after the game data loaded; the current flag is re-read from the store at once. null restores the default.
        /// </summary>
        public static void UseAdsRemovedStore(ISketchAdsRemovedStore store)
        {
            _adsRemovedStore = store ?? new PlayerPrefsAdsRemovedStore();
            if (_policy != null) _policy.AdsRemoved = _adsRemovedStore.Load();
        }

        public static void SetAdsRemoved(bool removed)
        {
            Policy.AdsRemoved = removed;
            AdsRemovedStore.Save(removed);
            if (removed)
            {
                if (_bannerShown) SketchSdk.HideBanner();
                _bannerShown = false;
                if (SketchSdk.IsNativeVisible) SketchSdk.HideNative();
            }
            SketchAnalytics.Log("ads_removed", "removed", removed);
        }

        // ---------------- interstitial ----------------

        public static bool IsInterstitialReady => SketchSdk.IsReady(SketchAdFormat.Interstitial);

        /// <summary>True when pacing allows an interstitial for this placement now and one is loaded.</summary>
        public static bool CanShowInterstitial(string placement, bool skipCooldown = false) =>
            IsInterstitialReady && Policy.CanShowInterstitial(placement, Now, skipCooldown);

        /// <summary>
        /// Shows an interstitial when pacing allows. onClosed ALWAYS fires exactly once (also when nothing showed),
        /// so the game flow can continue in one place. Returns true when an ad started.
        /// </summary>
        public static bool ShowInterstitial(string placement, Action onClosed = null, bool skipCooldown = false)
        {
            placement = Normalize(placement);
            bool done = false;
            void Close() { if (done) return; done = true; Safe(onClosed); }

            if (!Policy.CanShowInterstitial(placement, Now, skipCooldown))
            {
                SketchLog.Info($"Interstitial '{placement}' blocked: {Policy.LastBlockReason}");
                Close();
                return false;
            }
            if (!IsInterstitialReady)
            {
                LogNotReady("interstitial", placement);
                Close();
                return false;
            }
            CurrentPlacement["interstitial"] = placement;
            BeginFullscreen();
            bool started = SketchSdk.ShowInterstitial(() =>
            {
                Policy.OnFullscreenClosed(Now);
                EndFullscreen();
                Close();
            });
            if (!started)
            {
                EndFullscreen();
                LogNotReady("interstitial", placement);
                Close();
            }
            return started;
        }

        /// <summary>Restart the interstitial cooldown without showing one (e.g. the player just bought something).</summary>
        public static void ResetInterstitialCooldown() => Policy.ResetInterstitialCooldown(Now);

        // ---------------- rewarded ----------------

        public static bool IsRewardedReady => SketchSdk.IsReady(SketchAdFormat.Rewarded);

        /// <summary>
        /// Shows a rewarded ad. onResult fires exactly once: true only when the player earned the reward,
        /// false when no ad was ready or the player closed early. Returns true when an ad started.
        /// </summary>
        public static bool ShowRewarded(string placement, Action<bool> onResult)
        {
            placement = Normalize(placement);
            bool done = false;
            void Result(bool granted)
            {
                if (done) return;
                done = true;
                SketchAnalytics.Log(granted ? "ad_reward_granted" : "ad_reward_skipped", "format", "rewarded", "placement", placement);
                try { RewardedFinished?.Invoke(placement, granted); } catch (Exception e) { Debug.LogException(e); }
                if (onResult == null) return;
                try { onResult(granted); } catch (Exception e) { Debug.LogException(e); }
            }

            if (!IsRewardedReady)
            {
                LogNotReady("rewarded", placement);
                Result(false);
                return false;
            }
            CurrentPlacement["rewarded"] = placement;
            BeginFullscreen();
            bool started = SketchSdk.ShowRewarded(granted =>
            {
                Policy.OnFullscreenClosed(Now);
                EndFullscreen();
                Result(granted);
            });
            if (!started)
            {
                EndFullscreen();
                LogNotReady("rewarded", placement);
                Result(false);
            }
            return started;
        }

        // ---------------- rewarded interstitial ----------------

        /// <summary>False when the provider has no rewarded-interstitial capability, no ad is loaded or this app version has ads off.</summary>
        public static bool IsRewardedInterstitialReady => Policy.CanShowRewardedInterstitial() && SketchSdk.IsReady(SketchAdFormat.RewardedInterstitial);

        /// <summary>
        /// Shows a rewarded interstitial (Google requires an intro screen with an opt-out BEFORE calling this; the game owns it).
        /// onResult fires exactly once: true only when the reward was earned, false when no ad was ready, the provider
        /// has no rewarded-interstitial support or the player closed early. Keeps working under remove-ads, like rewarded.
        /// Returns true when an ad started.
        /// </summary>
        public static bool ShowRewardedInterstitial(string placement, Action<bool> onResult)
        {
            placement = Normalize(placement);
            bool done = false;
            void Result(bool granted)
            {
                if (done) return;
                done = true;
                SketchAnalytics.Log(granted ? "ad_reward_granted" : "ad_reward_skipped", "format", "rewarded_interstitial", "placement", placement);
                try { RewardedInterstitialFinished?.Invoke(placement, granted); } catch (Exception e) { Debug.LogException(e); }
                if (onResult == null) return;
                try { onResult(granted); } catch (Exception e) { Debug.LogException(e); }
            }

            if (!Policy.CanShowRewardedInterstitial())
            {
                SketchLog.Info($"Rewarded interstitial '{placement}' blocked: {Policy.LastBlockReason}");
                Result(false);
                return false;
            }
            if (!IsRewardedInterstitialReady)
            {
                LogNotReady("rewarded_interstitial", placement);
                Result(false);
                return false;
            }
            CurrentPlacement["rewarded_interstitial"] = placement;
            BeginFullscreen();
            bool started = SketchSdk.ShowRewardedInterstitial(granted =>
            {
                Policy.OnFullscreenClosed(Now);
                EndFullscreen();
                Result(granted);
            });
            if (!started)
            {
                EndFullscreen();
                LogNotReady("rewarded_interstitial", placement);
                Result(false);
            }
            return started;
        }

        // ---------------- app open ----------------

        public static bool IsAppOpenReady => SketchSdk.IsReady(SketchAdFormat.AppOpen);

        /// <summary>Skip the next automatic app-open on resume (call before opening a store page, share sheet, purchase...).</summary>
        public static void SuppressNextAppOpen() => _appOpenSuppress = 1;

        /// <summary>Show an app-open ad now when pacing allows (cold start: call from the splash). onClosed always fires.</summary>
        public static bool ShowAppOpen(string placement, Action onClosed = null, bool coldStart = false)
        {
            placement = Normalize(placement);
            bool done = false;
            void Close() { if (done) return; done = true; Safe(onClosed); }
            if (!Policy.CanShowAppOpen(Now, coldStart, coldStart ? 0 : double.MaxValue) || !IsAppOpenReady)
            {
                if (Policy.LastBlockReason.Length > 0) SketchLog.Info($"App-open '{placement}' blocked: {Policy.LastBlockReason}");
                Close();
                return false;
            }
            CurrentPlacement["app_open"] = placement;
            BeginFullscreen();
            bool started = SketchSdk.ShowAppOpen(() =>
            {
                Policy.OnFullscreenClosed(Now);
                EndFullscreen();
                Close();
            });
            if (!started) { EndFullscreen(); Close(); }
            return started;
        }

        /// <summary>
        /// Splash helper: waits up to maxWaitSeconds for the SDK and an app-open ad, shows it when AppOpenOnColdStart is on,
        /// then calls onDone exactly once.
        /// </summary>
        public static void ShowColdStartAppOpen(float maxWaitSeconds, Action onDone)
        {
            bool done = false;
            double deadline = Time.realtimeSinceStartupAsDouble + Mathf.Max(0f, maxWaitSeconds);
            void Finish() { if (done) return; done = true; SketchSdkLoop.Update -= Wait; Safe(onDone); }
            void Wait()
            {
                if (done) return;
                // Leave at once when pacing would block anyway (disabled, ads removed, start delay...).
                if (!Policy.CanShowAppOpen(Now, true, 0)) { Finish(); return; }
                if (SketchSdk.IsInitialized && IsAppOpenReady)
                {
                    SketchSdkLoop.Update -= Wait;
                    ShowAppOpen("cold_start", Finish, coldStart: true);
                    return;
                }
                if (Time.realtimeSinceStartupAsDouble >= deadline) Finish();
            }
            SketchSdkLoop.Update += Wait;
        }

        // ---------------- banner ----------------

        /// <summary>Request the banner (sticky). It appears as soon as it is loaded and pacing allows; stays until HideBanner.</summary>
        public static void ShowBanner()
        {
            _bannerWanted = true;
            UpdateBanner();
        }

        public static void HideBanner()
        {
            _bannerWanted = false;
            UpdateBanner();
        }

        public static bool IsBannerWanted => _bannerWanted;
        /// <summary>Current banner height in screen pixels (0 when hidden). Use it to lift bottom UI.</summary>
        public static float BannerHeightPixels => SketchSdk.BannerHeightPixels;
        /// <summary>Raised on the main thread when the banner height changes.</summary>
        public static event Action<float> BannerHeightChanged
        {
            add => SketchSdk.BannerLayoutChanged += value;
            remove => SketchSdk.BannerLayoutChanged -= value;
        }

        static void UpdateBanner()
        {
            if (!SketchSdk.IsInitialized) return;
            bool want = _bannerWanted && Policy.CanShowBanner(Now) && SketchSdk.IsReady(SketchAdFormat.Banner);
            if (want == _bannerShown) return;
            _bannerShown = want;
            if (want) SketchSdk.ShowBanner();
            else SketchSdk.HideBanner();
        }

        // ---------------- native ----------------

        public static bool IsNativeReady => Policy.CanShowNative() && SketchSdk.IsNativeReady;

        /// <summary>Native ad drawn in a screen rect (e.g. beside a popup). Returns false when not ready / ads removed / ads-off version.</summary>
        public static bool ShowNativeInRect(string placement, SketchNativeRequest request, SketchNativeCallbacks callbacks = null)
        {
            if (!Policy.CanShowNative()) return false;
            CurrentPlacement["native"] = Normalize(placement);
            bool ok = SketchSdk.ShowNativeInRect(request, callbacks);
            if (!ok) LogNotReady("native", Normalize(placement));
            return ok;
        }

        public static void HideNative() => SketchSdk.HideNative();

        // ---------------- internals ----------------

        static SketchAdsPolicy CreatePolicy(SketchAdsPolicySettings settings) =>
            new SketchAdsPolicy(settings ?? new SketchAdsPolicySettings(), SketchIdentity.FirstOpenUnix > 0 ? SketchIdentity.FirstOpenUnix : SketchIdentity.NowUnix)
            {
                Session = Math.Max(1, SketchIdentity.Session),
                AdsRemoved = AdsRemovedStore.Load(),   // a paying player stays ad-free even before the SDK starts
                AppVersion = Application.version,   // matched against the remote sketch_ads_off_version list
            };

        internal static void Configure(SketchAdsPolicySettings settings)
        {
            _policy = CreatePolicy(settings);
            SketchSdkLoop.Update -= Tick;
            SketchSdkLoop.Update += Tick;
            Application.focusChanged -= OnFocusChanged;
            Application.focusChanged += OnFocusChanged;
        }

        /// <summary>Placement captured at show time, used by provider telemetry (paid events, impressions).</summary>
        internal static string PlacementFor(string format) => format != null && CurrentPlacement.TryGetValue(format, out string p) ? p : format ?? "unknown";

        static double _nextBannerCheck;
        static bool _adTookFocus;
        static double _focusBackAt = -1;
        const double STUCK_AD_SECONDS = 6.0;

        static void Tick()
        {
            double t = Time.realtimeSinceStartupAsDouble;
            CheckStuckFullscreen(t);
            if (t < _nextBannerCheck) return;
            _nextBannerCheck = t + 0.5;
            UpdateBanner();
        }

        /// <summary>
        /// Android: a fullscreen ad takes focus while it shows. When the game has had focus back for a few seconds and the
        /// provider still has not reported "closed" (activity recreated, adapter bug), complete the ad so the game, audio and
        /// later ads are not locked for the rest of the session.
        /// </summary>
        static void CheckStuckFullscreen(double t)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_fullscreenDepth == 0 || !_adTookFocus || _focusBackAt < 0 || !Application.isFocused) return;
            if (t - _focusBackAt < STUCK_AD_SECONDS) return;
            _focusBackAt = -1;
            _adTookFocus = false;
            SketchAnalytics.Log("ad_close_watchdog");
            SketchSdk.ForceCloseFullscreen();
#endif
        }

        static void OnFocusChanged(bool focused)
        {
            if (_fullscreenDepth > 0)
            {
                if (!focused) { _adTookFocus = true; _focusBackAt = -1; }
                else if (_adTookFocus) _focusBackAt = Time.realtimeSinceStartupAsDouble;
                return;
            }
            if (!focused)
            {
                _focusLostAt = Time.realtimeSinceStartupAsDouble;
                return;
            }
            if (_focusLostAt < 0) return;
            double background = Time.realtimeSinceStartupAsDouble - _focusLostAt;
            _focusLostAt = -1;
            if (_appOpenSuppress > 0) { _appOpenSuppress = 0; return; }
            if (!SketchSdk.IsInitialized || SketchSdk.FullscreenAdShowing || _fullscreenDepth > 0) return;
            // Also covers fullscreen ads shown through the low-level SketchSdk API (they bypass the policy bookkeeping).
            if (SketchSdk.SecondsSinceFullscreenClosed < Policy.Settings.AppOpenAfterFullscreenSeconds) return;
            if (!Policy.CanShowAppOpen(Now, false, background)) return;
            if (!IsAppOpenReady) return;
            // Show on the next frame: some devices deliver focus before the surface is ready.
            SketchSdkLoop.Post(() => ShowAppOpen("resume"));
        }

        static int _fullscreenDepth;
        static void BeginFullscreen()
        {
            _fullscreenDepth++;
            if (_fullscreenDepth != 1) return;
            _adTookFocus = false;
            _focusBackAt = -1;
            if (PauseAudioDuringAds)
            {
                _wasAudioPaused = AudioListener.pause;
                AudioListener.pause = true;
                _audioPausedByAds = true;
            }
            try { FullscreenAdChanged?.Invoke(true); } catch (Exception e) { Debug.LogException(e); }
        }

        static void EndFullscreen()
        {
            if (_fullscreenDepth == 0) return;
            _fullscreenDepth--;
            if (_fullscreenDepth != 0) return;
            if (_audioPausedByAds)
            {
                AudioListener.pause = _wasAudioPaused;
                _audioPausedByAds = false;
            }
            _focusLostAt = -1;   // the ad itself took focus; never treat its close as a resume
            try { FullscreenAdChanged?.Invoke(false); } catch (Exception e) { Debug.LogException(e); }
        }

        static void LogNotReady(string format, string placement)
        {
            SketchLog.Info($"{format} '{placement}' not ready.");
            SketchAnalytics.Log("ad_not_ready", "format", format, "placement", placement);
        }

        static double Now => SketchIdentity.NowUnix;
        static string Normalize(string placement) => string.IsNullOrWhiteSpace(placement) ? "default" : placement.Trim();

        static void Safe(Action action)
        {
            if (action == null) return;
            try { action(); } catch (Exception e) { Debug.LogException(e); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Application.focusChanged -= OnFocusChanged;
            _policy = null;
            _adsRemovedStore = null;
            CurrentPlacement.Clear();
            _bannerWanted = false;
            _bannerShown = false;
            _focusLostAt = -1;
            _appOpenSuppress = 0;
            _fullscreenDepth = 0;
            _audioPausedByAds = false;
            _nextBannerCheck = 0;
            _adTookFocus = false;
            _focusBackAt = -1;
            FullscreenAdChanged = null;
            RewardedFinished = null;
            RewardedInterstitialFinished = null;
            PauseAudioDuringAds = true;
        }
    }
}
