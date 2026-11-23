#if SKETCHSDK_ADMOB
using System;
using GoogleMobileAds.Api;
using UnityEngine;
using Sketch.Sdk;

namespace Sketch.Sdk
{
    public sealed class SketchSdkAdMobProvider : ISketchAdsProvider, ISketchBannerAdsProvider, ISketchNativeAdsProvider, ISketchAdCloseCapability,
        ISketchRewardedInterstitialProvider
    {
        readonly SketchAdMobConfig _config;
        readonly ISketchAdsTelemetry _telemetry;
        RewardedAd _rewarded;
        RewardedInterstitialAd _rewardedInterstitial;
        InterstitialAd _interstitial;
        AppOpenAd _appOpen;
        BannerView _banner;
        NativeOverlayAd _native;
        int _nativeAdGeneration;
        bool _nativeLoading;
        bool _nativeShown;
        double _nativeLoadedAt;
        double _nextNativeLoadAt;
        bool _appOpenLoading;
        double _appOpenLoadedAt;
        double _nextAppOpenLoadAt;
        const double APP_OPEN_MAX_AGE = 4 * 3600.0;
        const double NATIVE_MAX_AGE = 3600.0;
        const double RELOAD_BACKOFF = 30.0;
        const double NATIVE_RELOAD_AFTER_SHOWN = 2.0;   // a shown native is spent; the next popup should have a fresh one almost at once (failures and expiry still back off RELOAD_BACKOFF)
        bool _initStarted;
        bool _initialized;
        bool _rewardEarned;
        Action<bool> _rewardCallback;
        SketchNativeCallbacks _nativeCallbacks;
        bool _bannerRequested;
        bool _bannerLoaded;
        float _bannerHeightPixels;
        int _bannerGeneration;
        float _lastPublishedBannerHeight;

        public SketchSdkAdMobProvider(SketchAdMobConfig config, ISketchAdsTelemetry telemetry = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _telemetry = telemetry;
            SketchSdk.NativeAttachTimeoutSeconds = config.NativeAttachTimeoutSeconds;
            SketchNativeLayout.DeviceScaleProvider = () => MobileAds.Utils.GetDeviceScale();
        }

        // Android: Sketch custom native layout (SketchAdmobNative.java) draws its own countdown close; elsewhere the GMA template has none.
        public bool SupportsInAdClose => SketchAndroidNative.Supported;
        public bool IsBannerReady => _banner != null;
        public bool IsNativeReady => IsReady(SketchAdFormat.Native);
        public event Action<float> BannerLayoutChanged;

        public float BannerHeightPixels
        {
            get
            {
                if (!_initialized || !_bannerRequested || !_bannerLoaded || float.IsNaN(_bannerHeightPixels) ||
                    float.IsInfinity(_bannerHeightPixels) || _bannerHeightPixels <= 0f) return 0f;
                return _bannerHeightPixels;
            }
        }

        public void Initialize(Action<bool> onInitialized)
        {
            if (_config == null)
            {
                Debug.LogError("[SketchAds] Missing SketchAdMobConfig.");
                onInitialized?.Invoke(false);
                return;
            }

            if (_initialized) { onInitialized?.Invoke(true); return; }
            if (_initStarted) return;

            if (!SketchAdMobConfig.HasAnyAdUnit(_config))
            {
                DevWarn("SketchAdMobConfig ids are blank; ads are not loaded.");
                onInitialized?.Invoke(false);
                return;
            }

            _initStarted = true;
            // Every GMA ad event (closed, reward, paid, load) is raised on the Unity main thread, so callbacks may touch Unity APIs.
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.SetiOSAppPauseOnBackground(true);
            ApplyRequestConfiguration();
            Debug.Log("[SketchAds] Starting Google Mobile Ads initialization.");
            MobileAds.Initialize(status =>
            {
                _initialized = true;
                Debug.Log("[SketchAds] Google Mobile Ads initialized.");
                if (status != null)
                {
                    foreach (var adapter in status.getAdapterStatusMap())
                        Debug.Log($"[SketchAds] Adapter {adapter.Key}: {adapter.Value.InitializationState}, {adapter.Value.Description}");
                }

                LoadConfiguredAds();
                SketchSdkLoop.Update -= TickReloads;
                SketchSdkLoop.Update += TickReloads;
                onInitialized?.Invoke(true);
            });
        }

        static void DevWarn(string msg)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[SketchAds] " + msg);
#endif
        }

        static TagForChildDirectedTreatment ToTfcd(SketchAdMobTag value) => value == SketchAdMobTag.True ? TagForChildDirectedTreatment.True
            : value == SketchAdMobTag.False ? TagForChildDirectedTreatment.False : TagForChildDirectedTreatment.Unspecified;
        static TagForUnderAgeOfConsent ToTfua(SketchAdMobTag value) => value == SketchAdMobTag.True ? TagForUnderAgeOfConsent.True
            : value == SketchAdMobTag.False ? TagForUnderAgeOfConsent.False : TagForUnderAgeOfConsent.Unspecified;
        static MaxAdContentRating ToRating(SketchAdMobContentRating value) => value == SketchAdMobContentRating.ParentalGuidance ? MaxAdContentRating.PG
            : value == SketchAdMobContentRating.Teen ? MaxAdContentRating.T : value == SketchAdMobContentRating.MatureAudience ? MaxAdContentRating.MA : MaxAdContentRating.G;

        bool _npa = true;
        void ApplyRequestConfiguration()
        {
            _npa = _config.NonPersonalized;
            var requestConfiguration = new RequestConfiguration
            {
                TagForChildDirectedTreatment = ToTfcd(_config.ChildDirected),
                TagForUnderAgeOfConsent = ToTfua(_config.UnderAgeOfConsent),
                MaxAdContentRating = ToRating(_config.MaxContentRating),
                TestDeviceIds = new System.Collections.Generic.List<string>(_config.TestDeviceIds ?? Array.Empty<string>()),
            };
            MobileAds.SetRequestConfiguration(requestConfiguration);
        }

        AdRequest NewRequest()
        {
            var request = new AdRequest();
            if (_npa && !string.IsNullOrWhiteSpace(_config.NonPersonalizedExtraKey))
                request.Extras[_config.NonPersonalizedExtraKey] = _config.NonPersonalizedExtraValue ?? "1";
            return request;
        }
        static double Now => Time.realtimeSinceStartupAsDouble;

        public bool IsReady(SketchAdFormat format)
        {
            try
            {
                switch (format)
                {
                    case SketchAdFormat.Rewarded: return _rewarded != null && _rewarded.CanShowAd();
                    case SketchAdFormat.RewardedInterstitial: return _rewardedInterstitial != null && _rewardedInterstitial.CanShowAd();
                    case SketchAdFormat.Interstitial: return _interstitial != null && _interstitial.CanShowAd();
                    case SketchAdFormat.AppOpen:
                        if (_appOpen != null && Now - _appOpenLoadedAt > APP_OPEN_MAX_AGE)
                        {
                            // Stale (older than 4 h): drop it and reload, never show it.
                            _appOpen.Destroy();
                            _appOpen = null;
                            _nextAppOpenLoadAt = 0;
                        }
                        if (_appOpen == null) TryReloadAppOpen();
                        return _appOpen != null && _appOpen.CanShowAd();
                    case SketchAdFormat.Banner: return _banner != null;
                    case SketchAdFormat.Native:
#if UNITY_ANDROID && !UNITY_EDITOR
                        if (SketchAndroidNative.IsReady) return true;
                        TryReloadNative();
                        return false;
#endif
                        if (_native != null && !_nativeShown && Now - _nativeLoadedAt > NATIVE_MAX_AGE)
                            DestroyNative(0);
                        if (_native == null) TryReloadNative();
                        return _native != null && (_nativeShown || Now - _nativeLoadedAt <= NATIVE_MAX_AGE);
                    default: return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SketchAds] IsReady({format}) failed: {e.Message}");
                return false;
            }
        }

        public void ShowRewarded(Action<bool> onComplete)
        {
            if (!IsReady(SketchAdFormat.Rewarded)) { onComplete?.Invoke(false); return; }
            _rewardCallback = onComplete;
            _rewardEarned = false;
            _rewardClosePending = false;
            var ad = _rewarded;
            _rewarded = null;
            _showingRewarded = ad;
            try
            {
                ad.OnAdFullScreenContentClosed += CompleteRewarded;
                ad.OnAdFullScreenContentFailed += _ => CompleteRewarded();
                SketchSdk.ReportFullscreen(true);
                ad.Show(_ =>
                {
                    _rewardEarned = true;
                    if (_rewardClosePending) FinishRewarded();
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SketchAds] Rewarded show failed: {e.Message}");
                CompleteRewarded();
            }
        }

        public void ShowRewardedInterstitial(Action<bool> onComplete)
        {
            if (!IsReady(SketchAdFormat.RewardedInterstitial)) { onComplete?.Invoke(false); return; }
            _riCallback = onComplete;
            _riEarned = false;
            _riClosePending = false;
            var ad = _rewardedInterstitial;
            _rewardedInterstitial = null;
            _showingRewardedInterstitial = ad;
            try
            {
                ad.OnAdFullScreenContentClosed += CompleteRewardedInterstitial;
                ad.OnAdFullScreenContentFailed += _ => CompleteRewardedInterstitial();
                SketchSdk.ReportFullscreen(true);
                ad.Show(_ =>
                {
                    _riEarned = true;
                    if (_riClosePending) FinishRewardedInterstitial();
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SketchAds] Rewarded interstitial show failed: {e.Message}");
                CompleteRewardedInterstitial();
            }
        }

        public void ShowInterstitial(Action onClosed)
        {
            if (!IsReady(SketchAdFormat.Interstitial)) { onClosed?.Invoke(); return; }
            var ad = _interstitial;
            _interstitial = null;
            bool done = false;
            // Per-show completion: closed and failed (or a throw) can all arrive; only the first counts, then the spent ad is destroyed.
            void Complete()
            {
                if (done) return;
                done = true;
                SketchSdk.ReportFullscreen(false);
                try { onClosed?.Invoke(); }
                finally
                {
                    SafeDestroy(ad);
                    LoadInterstitial();
                }
            }
            try
            {
                ad.OnAdFullScreenContentClosed += Complete;
                ad.OnAdFullScreenContentFailed += _ => Complete();
                SketchSdk.ReportFullscreen(true);
                ad.Show();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SketchAds] Interstitial show failed: {e.Message}");
                Complete();
            }
        }

        public void ShowAppOpen(Action onClosed)
        {
            if (!IsReady(SketchAdFormat.AppOpen)) { onClosed?.Invoke(); return; }
            var ad = _appOpen;
            _appOpen = null;
            bool done = false;
            void Complete()
            {
                if (done) return;
                done = true;
                SketchSdk.ReportFullscreen(false);
                try { onClosed?.Invoke(); }
                finally
                {
                    SafeDestroy(ad);
                    _nextAppOpenLoadAt = 0;
                    if (SketchAdMobConfig.IsUsableId(_config.AppOpenId)) LoadAppOpen();
                }
            }
            try
            {
                ad.OnAdFullScreenContentClosed += Complete;
                ad.OnAdFullScreenContentFailed += _ => Complete();
                SketchSdk.ReportFullscreen(true);
                ad.Show();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SketchAds] App-open show failed: {e.Message}");
                Complete();
            }
        }

        public void ShowBanner()
        {
            try { _bannerRequested = true; _banner?.Show(); }
            catch (Exception e) { _bannerRequested = false; Debug.LogWarning($"[SketchAds] Banner show failed: {e.Message}"); }
            PublishBannerLayout();
        }

        public void HideBanner()
        {
            _bannerRequested = false;
            try { _banner?.Hide(); }
            catch (Exception e) { Debug.LogWarning($"[SketchAds] Banner hide failed: {e.Message}"); }
            PublishBannerLayout();
        }

        public bool ShowNative(SketchNativeLayout layout)
        {
            if (!IsReady(SketchAdFormat.Native)) return false;
            try
            {
                var style = new NativeTemplateStyle { TemplateId = NativeTemplateId.Medium };
                float sizeScale = 1f;
#if UNITY_ANDROID && !UNITY_EDITOR
                // GMA Unity 11.6.0's Android custom template assigns AdSize.getWidth/Height
                // directly to pixel LayoutParams; only its x/y offsets are converted from dp.
                sizeScale = SketchNativeLayout.DeviceScale;
#endif
                var size = new AdSize(Mathf.Max(1, Mathf.RoundToInt(layout.WidthDp * sizeScale)), Mathf.Max(1, Mathf.RoundToInt(layout.HeightDp * sizeScale)));
                if (layout.HasRect) _native.RenderTemplate(style, size, Mathf.RoundToInt(layout.XDp), Mathf.RoundToInt(layout.YDp));   // native half beside a popup: exact rect
                else _native.RenderTemplate(style, size, layout.Top ? AdPosition.Top : AdPosition.Bottom);
                _native.Show();
                _nativeShown = true;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SketchAds] Native show failed: {e.Message}");
                DestroyNative(RELOAD_BACKOFF);
                return false;
            }
        }

        public bool ShowNativeInRect(SketchNativeRequest request, SketchNativeCallbacks callbacks)
        {
            if (!IsReady(SketchAdFormat.Native)) return false;
            // OR-157: one shared helper clamps the rect above the banner and converts it, so the Editor fake draws exactly this rect.
            Rect clamped = SketchNativeInRect.ClampToBanner(request.ScreenRect, Screen.height, BannerHeightPixels);
            if (clamped.width <= 0f || clamped.height <= 0f) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
            // Sketch custom native layout (KT ThunderAdmobNative parity): pixel rect, own window, round countdown close.
            HookJavaNative();
            _nativeCallbacks = callbacks;
            if (!SketchAndroidNative.ShowInRect(clamped, request.Placement))
            {
                _nativeCallbacks = null;
                return false;
            }
            _nativeShown = true;
            return true;
#endif
            bool sizeInPixels = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            sizeInPixels = true;   // GMA Unity Android custom template: AdSize width/height are pixel LayoutParams, x/y are dp
#endif
            SketchNativeInRect.ToAdMob(clamped, Screen.height, SketchNativeLayout.DeviceScale, sizeInPixels, out int xDp, out int yDp, out int w, out int h);
            try
            {
                var style = new NativeTemplateStyle { TemplateId = NativeTemplateId.Medium };
                var size = new AdSize(w, h);
                _nativeCallbacks = callbacks;
                _native.RenderTemplate(style, size, xDp, yDp);
                _native.Show();
                _nativeShown = true;
                callbacks?.Shown();
                return true;
            }
            catch (Exception e)
            {
                _nativeCallbacks = null;
                Debug.LogWarning($"[SketchAds] Native in-rect show failed: {e.Message}");
                DestroyNative(RELOAD_BACKOFF);
                throw;
            }
        }

        public void HideNative()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            {
                bool androidWasShown = _nativeShown;
                SketchNativeCallbacks androidCallbacks = _nativeCallbacks;
                _nativeCallbacks = null;
                _nativeShown = false;
                SketchAndroidNative.Hide();
                if (androidWasShown) androidCallbacks?.Closed(SketchNativeCloseReason.SdkClosed);
                return;
            }
#endif
            if (_native == null) { _nativeCallbacks = null; return; }
            bool wasShown = _nativeShown;
            SketchNativeCallbacks callbacks = _nativeCallbacks;
            _nativeCallbacks = null;
            _nativeShown = false;
            try { _native.Hide(); }
            catch (Exception e) { Debug.LogWarning($"[SketchAds] Native hide failed: {e.Message}"); }
            // A shown native is spent: destroy and reload after the back-off.
            if (wasShown) DestroyNative(NATIVE_RELOAD_AFTER_SHOWN);
            if (wasShown) callbacks?.Closed(SketchNativeCloseReason.SdkClosed);
        }

        void DestroyNative(double backoff)
        {
            _nativeAdGeneration++;
            var ad = _native;
            _native = null;
            _nativeCallbacks = null;
            _nativeShown = false;
            _nextNativeLoadAt = Now + backoff;
            try { ad?.Destroy(); }
            catch (Exception e) { Debug.LogWarning($"[SketchAds] Native destroy failed: {e.Message}"); }
        }

        void TryReloadNative()
        {
            if (!_initialized || _nativeLoading || Now < _nextNativeLoadAt) return;
            if (!SketchAdMobConfig.IsUsableId(_config.NativeId)) return;
            LoadNative();
        }

        void TryReloadAppOpen()
        {
            if (!_initialized || _appOpenLoading || Now < _nextAppOpenLoadAt) return;
            if (!SketchAdMobConfig.IsUsableId(_config.AppOpenId)) return;
            LoadAppOpen();
        }

        void LoadNative()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Java owns loading and its own retry back-off (10/20/40/60 s); C# only re-kicks it now and then.
            HookJavaNative();
            _nextNativeLoadAt = Now + RELOAD_BACKOFF;
            SketchAndroidNative.Load(_config.NativeId);
            return;
#endif
            if (_nativeLoading) return;
            _nativeLoading = true;
            _nextNativeLoadAt = Now + RELOAD_BACKOFF;
            try
            {
                NativeOverlayAd.Load(_config.NativeId, NewRequest(), new NativeAdOptions(), (ad, error) =>
                {
                    _nativeLoading = false;
                    if (error != null || ad == null) { Debug.LogWarning($"[SketchAds] Native load failed: {error}"); LogLoadFailed("native", _config.NativeId, error); return; }
                    NativeOverlayAd previousAd = _native;
                    int generation = ++_nativeAdGeneration;
                    _native = ad;
                    try { previousAd?.Destroy(); } catch { }
                    HookPaid(ad, "native");
                    HookLife(ad, "native", _config.NativeId, new SketchNativeAdBinding(ad, generation));
                    LogLoaded("native", _config.NativeId, SafeInfo(() => ad.GetResponseInfo()));
                    _nativeShown = false;
                    _nativeLoadedAt = Now;
                    Debug.Log("[SketchAds] Native ready.");
                });
            }
            catch (Exception e)
            {
                _nativeLoading = false;
                Debug.LogWarning($"[SketchAds] Native load threw: {e.Message}");
            }
        }

        // Preserve exact AdMob micros and currency; a consumer analytics adapter owns server conversion.
        // AdMob raises OnAdPaid on a background thread; the analytics post only works with Unity APIs, so every paid event is moved to the main thread first (it silently failed before).
        void HookPaid(RewardedAd ad, string format) => ad.OnAdPaid += value => OnMainThread(() => ReportPaid(format, value, _config.RewardedId, SafeInfo(() => ad.GetResponseInfo())));
        void HookPaid(RewardedInterstitialAd ad, string format) => ad.OnAdPaid += value => OnMainThread(() => ReportPaid(format, value, _config.RewardedInterstitialId, SafeInfo(() => ad.GetResponseInfo())));
        void HookPaid(InterstitialAd ad, string format) => ad.OnAdPaid += value => OnMainThread(() => ReportPaid(format, value, _config.InterstitialId, SafeInfo(() => ad.GetResponseInfo())));
        void HookPaid(AppOpenAd ad, string format) => ad.OnAdPaid += value => OnMainThread(() => ReportPaid(format, value, _config.AppOpenId, SafeInfo(() => ad.GetResponseInfo())));
        void HookPaid(NativeOverlayAd ad, string format) => ad.OnAdPaid += value => OnMainThread(() => ReportPaid(format, value, _config.NativeId, SafeInfo(() => ad.GetResponseInfo())));

        static void OnMainThread(Action action) => GoogleMobileAds.Common.MobileAdsEventExecutor.ExecuteInUpdate(action);

        // ---- lifecycle logging for EVERY AdMob format: load ok / load failed / impression / click / opened / closed / show failed (revenue is ReportPaid above) ----
        // One analytics event each: format + placement + ad_unit_id (+ source / code / message), through the consumer-owned telemetry interface.
        void LogAd(string eventName, string format, string adUnitId, string key1 = null, object value1 = null, string key2 = null, object value2 = null)
        {
            OnMainThread(() =>
            {
                try
                {
                    string source = key1 == "ad_source" ? Convert.ToString(value1) : key2 == "ad_source" ? Convert.ToString(value2) : null;
                    string code = key1 == "code" ? Convert.ToString(value1) : key2 == "code" ? Convert.ToString(value2) : null;
                    string message = key1 == "message" ? Convert.ToString(value1) : key2 == "message" ? Convert.ToString(value2) : null;
                    _telemetry?.Track(new SketchAdTelemetryEvent
                    {
                        Name = eventName,
                        Format = format,
                        Placement = _telemetry?.PlacementFor(format) ?? format,
                        AdUnitId = adUnitId ?? string.Empty,
                        AdSource = source,
                        Code = code,
                        Message = message,
                    });
                }
                catch (Exception e) { Debug.LogWarning("[SketchAds] Ad telemetry failed: " + e.Message); }
            });
        }
        static string ErrCode(AdError e) { try { return e == null ? string.Empty : e.GetCode().ToString(); } catch { return string.Empty; } }
        static string ErrMsg(AdError e) { try { string m = e == null ? string.Empty : e.GetMessage() ?? string.Empty; return m.Length > 120 ? m.Substring(0, 120) : m; } catch { return string.Empty; } }
        static string SourceOf(ResponseInfo info) { try { return info?.GetLoadedAdapterResponseInfo()?.AdSourceName ?? string.Empty; } catch { return string.Empty; } }

        void LogLoaded(string format, string unit, ResponseInfo info) => LogAd("ad_loaded", format, unit, "ad_source", SourceOf(info));
        void LogLoadFailed(string format, string unit, AdError error) => LogAd("ad_load_failed", format, unit, "code", ErrCode(error), "message", ErrMsg(error));

        void HookLife(RewardedAd ad, string format, string unit)
        {
            ad.OnAdImpressionRecorded += () => LogAd("ad_impression", format, unit);
            ad.OnAdClicked += () => LogAd("ad_clicked", format, unit);
            ad.OnAdFullScreenContentOpened += () => LogAd("ad_opened", format, unit);
            ad.OnAdFullScreenContentClosed += () => LogAd("ad_closed", format, unit);
            ad.OnAdFullScreenContentFailed += e => LogAd("ad_show_failed", format, unit, "code", ErrCode(e), "message", ErrMsg(e));
        }

        void HookLife(RewardedInterstitialAd ad, string format, string unit)
        {
            ad.OnAdImpressionRecorded += () => LogAd("ad_impression", format, unit);
            ad.OnAdClicked += () => LogAd("ad_clicked", format, unit);
            ad.OnAdFullScreenContentOpened += () => LogAd("ad_opened", format, unit);
            ad.OnAdFullScreenContentClosed += () => LogAd("ad_closed", format, unit);
            ad.OnAdFullScreenContentFailed += e => LogAd("ad_show_failed", format, unit, "code", ErrCode(e), "message", ErrMsg(e));
        }

        void HookLife(InterstitialAd ad, string format, string unit)
        {
            ad.OnAdImpressionRecorded += () => LogAd("ad_impression", format, unit);
            ad.OnAdClicked += () => LogAd("ad_clicked", format, unit);
            ad.OnAdFullScreenContentOpened += () => LogAd("ad_opened", format, unit);
            ad.OnAdFullScreenContentClosed += () => LogAd("ad_closed", format, unit);
            ad.OnAdFullScreenContentFailed += e => LogAd("ad_show_failed", format, unit, "code", ErrCode(e), "message", ErrMsg(e));
        }

        void HookLife(AppOpenAd ad, string format, string unit)
        {
            ad.OnAdImpressionRecorded += () => LogAd("ad_impression", format, unit);
            ad.OnAdClicked += () => LogAd("ad_clicked", format, unit);
            ad.OnAdFullScreenContentOpened += () => LogAd("ad_opened", format, unit);
            ad.OnAdFullScreenContentClosed += () => LogAd("ad_closed", format, unit);
            ad.OnAdFullScreenContentFailed += e => LogAd("ad_show_failed", format, unit, "code", ErrCode(e), "message", ErrMsg(e));
        }

        void HookLife(NativeOverlayAd ad, string format, string unit, SketchNativeAdBinding binding)
        {
            bool IsCurrent() => binding.Matches(ad, _native, _nativeAdGeneration);
            ad.OnAdImpressionRecorded += () => { if (IsCurrent()) LogAd("ad_impression", format, unit); };
            ad.OnAdClicked += () => { if (IsCurrent()) { LogAd("ad_clicked", format, unit); _nativeCallbacks?.Clicked(); } };
            ad.OnAdFullScreenContentOpened += () => { if (IsCurrent()) LogAd("ad_opened", format, unit); };
            ad.OnAdFullScreenContentClosed += () =>
            {
                if (!IsCurrent()) return;
                LogAd("ad_closed", format, unit);
                SketchNativeCallbacks callbacks = _nativeCallbacks;
                _nativeCallbacks = null;
                _nativeShown = false;
                callbacks?.Closed(SketchNativeCloseReason.SdkClosed);
            };
        }

        static ResponseInfo SafeInfo(Func<ResponseInfo> get)
        {
            try { return get(); } catch { return null; }
        }

        bool _javaNativeHooked;

        void HookJavaNative()
        {
            if (_javaNativeHooked) return;
            _javaNativeHooked = true;
            SketchAndroidNative.Loaded += () => LogAd("ad_loaded", "native", _config.NativeId);
            SketchAndroidNative.LoadFailed += payload => LogAd("ad_load_failed", "native", _config.NativeId, "message", payload);
            SketchAndroidNative.Clicked += () => { LogAd("ad_clicked", "native", _config.NativeId); _nativeCallbacks?.Clicked(); };
            SketchAndroidNative.Shown += () =>
            {
                LogAd("ad_impression", "native", _config.NativeId);
                _nativeCallbacks?.Shown();
                SketchAndroidNative.ReleasePlayback();
                SketchAndroidNative.StartCloseCountdown(SketchNativeClose.COUNTDOWN_SECONDS, SketchNativeClose.CountdownEnabled,
                    SketchNativeClose.NextOnLeft());
            };
            SketchAndroidNative.UserClosed += () =>
            {
                LogAd("ad_closed", "native", _config.NativeId);
                SketchNativeCallbacks callbacks = _nativeCallbacks;
                _nativeCallbacks = null;
                _nativeShown = false;
                callbacks?.Closed(SketchNativeCloseReason.User);
            };
            SketchAndroidNative.Paid += ReportJavaNativePaid;
        }

        // Java payload: micros|currency|precision(0 unknown,1 estimated,2 publisher,3 precise)|responseId|source|placement
        void ReportJavaNativePaid(string payload)
        {
            try
            {
                string[] parts = (payload ?? string.Empty).Split('|');
                if (parts.Length < 3 || !long.TryParse(parts[0], out long micros)) return;
                string currency = string.IsNullOrEmpty(parts[1]) ? "USD" : parts[1];
                int.TryParse(parts[2], out int precision);
                string[] precisions = { "Unknown", "Estimated", "PublisherProvided", "Precise" };
                _telemetry?.TrackRevenue(new SketchAdRevenue
                {
                    Format = "native",
                    Placement = _telemetry?.PlacementFor("native") ?? "native",
                    Network = "admob",
                    Currency = currency,
                    Precision = precision >= 0 && precision < precisions.Length ? precisions[precision] : precision.ToString(),
                    ValueMicros = micros,
                    AdUnitId = _config.NativeId,
                    ResponseId = parts.Length > 3 ? parts[3] : null,
                    AdSource = parts.Length > 4 ? parts[4] : null,
                });
            }
            catch (Exception e) { Debug.LogWarning("[SketchAds] Native paid telemetry failed: " + e.Message); }
        }

        void ReportPaid(string format, AdValue value, string adUnitId, ResponseInfo info)
        {
            try
            {
                if (value == null) return;
                string currency = string.IsNullOrEmpty(value.CurrencyCode) ? "USD" : value.CurrencyCode;
                string responseId = null, adSource = null;
                try { responseId = info?.GetResponseId(); adSource = info?.GetLoadedAdapterResponseInfo()?.AdSourceName; } catch { }
                _telemetry?.TrackRevenue(new SketchAdRevenue
                {
                    Format = format,
                    Placement = _telemetry?.PlacementFor(format) ?? format,
                    Network = "admob",
                    Currency = currency,
                    Precision = value.Precision.ToString(),
                    ValueMicros = value.Value,
                    AdUnitId = adUnitId,
                    ResponseId = responseId,
                    AdSource = adSource,
                });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[SketchAds] Paid {format}: {value.Value / 1000000d:0.000000} {currency} ({value.Precision})");
#endif
            }
            catch (Exception e) { Debug.LogWarning("[SketchAds] Paid telemetry failed: " + e.Message); }
        }
        void LoadConfiguredAds()
        {
            if (SketchAdMobConfig.IsUsableId(_config.RewardedId)) LoadRewarded();
            if (SketchAdMobConfig.IsUsableId(_config.InterstitialId)) LoadInterstitial();
            if (SketchAdMobConfig.IsUsableId(_config.AppOpenId)) LoadAppOpen();
            if (SketchAdMobConfig.IsUsableId(_config.BannerId)) LoadBanner();
            if (SketchAdMobConfig.IsUsableId(_config.NativeId)) LoadNative();
            if (SketchAdMobConfig.IsUsableId(_config.RewardedInterstitialId)) LoadRewardedInterstitial();
        }

        // ---- reload with linear back-off (5 s x attempt, max 60 s) so a failed load never leaves a format dead ----
        bool _rewardedLoading, _interstitialLoading;
        int _rewardedAttempts, _interstitialAttempts, _bannerAttempts;
        double _rewardedRetryAt = double.MaxValue, _interstitialRetryAt = double.MaxValue, _bannerRetryAt = double.MaxValue;
        static double Backoff(int attempts) => Math.Min(60.0, 5.0 * Math.Max(1, attempts));

        void TickReloads()
        {
            double now = Now;
            if (_rewarded == null && !_rewardedLoading && now >= _rewardedRetryAt) LoadRewarded();
            if (_interstitial == null && !_interstitialLoading && now >= _interstitialRetryAt) LoadInterstitial();
            if (_rewardedInterstitial == null && !_riLoading && now >= _riRetryAt) LoadRewardedInterstitial();
            if (_banner != null && !_bannerLoaded && now >= _bannerRetryAt)
            {
                _bannerRetryAt = double.MaxValue;
                try { _banner.LoadAd(NewRequest()); } catch (Exception e) { Debug.LogWarning($"[SketchAds] Banner reload threw: {e.Message}"); }
            }
        }

        void LoadRewarded()
        {
            if (_rewardedLoading) return;
            _rewardedLoading = true;
            _rewardedRetryAt = double.MaxValue;
            _rewarded?.Destroy();
            _rewarded = null;
            RewardedAd.Load(_config.RewardedId, NewRequest(), (ad, error) =>
            {
                _rewardedLoading = false;
                if (error != null || ad == null)
                {
                    _rewardedAttempts++;
                    _rewardedRetryAt = Now + Backoff(_rewardedAttempts);
                    Debug.LogWarning($"[SketchAds] Rewarded load failed (retry in {Backoff(_rewardedAttempts)}s): {error}");
                    LogLoadFailed("rewarded", _config.RewardedId, error);
                    return;
                }
                _rewardedAttempts = 0;
                _rewarded = ad;
                HookPaid(ad, "rewarded");
                HookLife(ad, "rewarded", _config.RewardedId);
                LogLoaded("rewarded", _config.RewardedId, SafeInfo(() => ad.GetResponseInfo()));
                Debug.Log("[SketchAds] Rewarded ready.");
            });
        }

        void LoadInterstitial()
        {
            if (_interstitialLoading) return;
            _interstitialLoading = true;
            _interstitialRetryAt = double.MaxValue;
            _interstitial?.Destroy();
            _interstitial = null;
            InterstitialAd.Load(_config.InterstitialId, NewRequest(), (ad, error) =>
            {
                _interstitialLoading = false;
                if (error != null || ad == null)
                {
                    _interstitialAttempts++;
                    _interstitialRetryAt = Now + Backoff(_interstitialAttempts);
                    Debug.LogWarning($"[SketchAds] Interstitial load failed (retry in {Backoff(_interstitialAttempts)}s): {error}");
                    LogLoadFailed("interstitial", _config.InterstitialId, error);
                    return;
                }
                _interstitialAttempts = 0;
                _interstitial = ad;
                HookPaid(ad, "interstitial");
                HookLife(ad, "interstitial", _config.InterstitialId);
                LogLoaded("interstitial", _config.InterstitialId, SafeInfo(() => ad.GetResponseInfo()));
                Debug.Log("[SketchAds] Interstitial ready.");
            });
        }

        // Same linear back-off as rewarded / interstitial.
        bool _riLoading;
        int _riAttempts;
        double _riRetryAt = double.MaxValue;

        void LoadRewardedInterstitial()
        {
            if (_riLoading) return;
            _riLoading = true;
            _riRetryAt = double.MaxValue;
            _rewardedInterstitial?.Destroy();
            _rewardedInterstitial = null;
            RewardedInterstitialAd.Load(_config.RewardedInterstitialId, NewRequest(), (ad, error) =>
            {
                _riLoading = false;
                if (error != null || ad == null)
                {
                    _riAttempts++;
                    _riRetryAt = Now + Backoff(_riAttempts);
                    Debug.LogWarning($"[SketchAds] Rewarded interstitial load failed (retry in {Backoff(_riAttempts)}s): {error}");
                    LogLoadFailed("rewarded_interstitial", _config.RewardedInterstitialId, error);
                    return;
                }
                _riAttempts = 0;
                _rewardedInterstitial = ad;
                HookPaid(ad, "rewarded_interstitial");
                HookLife(ad, "rewarded_interstitial", _config.RewardedInterstitialId);
                LogLoaded("rewarded_interstitial", _config.RewardedInterstitialId, SafeInfo(() => ad.GetResponseInfo()));
                Debug.Log("[SketchAds] Rewarded interstitial ready.");
            });
        }

        void LoadAppOpen()
        {
            if (_appOpenLoading) return;
            _appOpenLoading = true;
            _nextAppOpenLoadAt = Now + RELOAD_BACKOFF;
            try
            {
                _appOpen?.Destroy();
                _appOpen = null;
                AppOpenAd.Load(_config.AppOpenId, NewRequest(), (ad, error) =>
                {
                    _appOpenLoading = false;
                    if (error != null || ad == null) { Debug.LogWarning($"[SketchAds] App-open load failed: {error}"); LogLoadFailed("app_open", _config.AppOpenId, error); return; }
                    _appOpen = ad;
                    HookPaid(ad, "app_open");
                    HookLife(ad, "app_open", _config.AppOpenId);
                    LogLoaded("app_open", _config.AppOpenId, SafeInfo(() => ad.GetResponseInfo()));
                    _appOpenLoadedAt = Now;
                    Debug.Log("[SketchAds] App-open ready.");
                });
            }
            catch (Exception e)
            {
                _appOpenLoading = false;
                Debug.LogWarning($"[SketchAds] App-open load threw: {e.Message}");
            }
        }

        void LoadBanner()
        {
            int generation = ++_bannerGeneration;
            BannerView previous = _banner;
            _banner = null;
            _bannerLoaded = false;
            _bannerHeightPixels = 0f;
            previous?.Destroy();
            var banner = new BannerView(_config.BannerId, AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth), AdPosition.Bottom);
            _banner = banner;
            bool IsCurrent() => generation == _bannerGeneration && ReferenceEquals(banner, _banner);
            banner.OnAdPaid += value => OnMainThread(() =>
            {
                if (IsCurrent()) ReportPaid("banner", value, _config.BannerId, SafeInfo(() => banner.GetResponseInfo()));
            });
            banner.OnBannerAdLoaded += () => OnMainThread(() =>
            {
                if (!IsCurrent()) return;
                _bannerLoaded = true;
                _bannerAttempts = 0;
                _bannerRetryAt = double.MaxValue;
                _bannerHeightPixels = banner.GetHeightInPixels();
                PublishBannerLayout();
                LogLoaded("banner", _config.BannerId, SafeInfo(() => banner.GetResponseInfo()));
            });
            banner.OnBannerAdLoadFailed += error => OnMainThread(() =>
            {
                if (!IsCurrent()) return;
                _bannerLoaded = false;
                _bannerHeightPixels = 0f;
                _bannerAttempts++;
                _bannerRetryAt = Now + Backoff(_bannerAttempts);
                PublishBannerLayout();
                LogLoadFailed("banner", _config.BannerId, error);
            });
            banner.OnAdImpressionRecorded += () => OnMainThread(() => { if (IsCurrent()) LogAd("ad_impression", "banner", _config.BannerId); });
            banner.OnAdClicked += () => OnMainThread(() => { if (IsCurrent()) LogAd("ad_clicked", "banner", _config.BannerId); });
            banner.OnAdFullScreenContentOpened += () => OnMainThread(() => { if (IsCurrent()) LogAd("ad_opened", "banner", _config.BannerId); });
            banner.OnAdFullScreenContentClosed += () => OnMainThread(() => { if (IsCurrent()) LogAd("ad_closed", "banner", _config.BannerId); });
            banner.LoadAd(NewRequest());
            banner.Hide();
            PublishBannerLayout();
        }

        void PublishBannerLayout()
        {
            float height = BannerHeightPixels;
            if (_lastPublishedBannerHeight == height) return;
            _lastPublishedBannerHeight = height;
            BannerLayoutChanged?.Invoke(height);
        }

        // Some networks deliver "closed" before "earned reward": wait briefly for a late reward before reporting false.
        bool _rewardClosePending;
        double _rewardCloseDeadline;
        const double LATE_REWARD_GRACE = 1.0;

        void CompleteRewarded()
        {
            if (_rewardCallback == null || _rewardClosePending) return;
            if (_rewardEarned) { FinishRewarded(); return; }
            _rewardClosePending = true;
            _rewardCloseDeadline = Now + LATE_REWARD_GRACE;
            SketchSdkLoop.Update -= WaitLateReward;
            SketchSdkLoop.Update += WaitLateReward;
        }

        void WaitLateReward()
        {
            if (!_rewardClosePending) { SketchSdkLoop.Update -= WaitLateReward; return; }
            if (Now >= _rewardCloseDeadline) FinishRewarded();
        }

        void FinishRewarded()
        {
            SketchSdkLoop.Update -= WaitLateReward;
            _rewardClosePending = false;
            SketchSdk.ReportFullscreen(false);
            var callback = _rewardCallback;
            _rewardCallback = null;
            var spent = _showingRewarded;
            _showingRewarded = null;
            try { callback?.Invoke(_rewardEarned); }
            finally
            {
                SafeDestroy(spent);
                LoadRewarded();
            }
        }

        RewardedAd _showingRewarded;

        // Rewarded interstitial: own state so it never shares the rewarded callback; same late-reward grace.
        bool _riEarned;
        bool _riClosePending;
        double _riCloseDeadline;
        Action<bool> _riCallback;
        RewardedInterstitialAd _showingRewardedInterstitial;

        void CompleteRewardedInterstitial()
        {
            if (_riCallback == null || _riClosePending) return;
            if (_riEarned) { FinishRewardedInterstitial(); return; }
            _riClosePending = true;
            _riCloseDeadline = Now + LATE_REWARD_GRACE;
            SketchSdkLoop.Update -= WaitLateRewardedInterstitial;
            SketchSdkLoop.Update += WaitLateRewardedInterstitial;
        }

        void WaitLateRewardedInterstitial()
        {
            if (!_riClosePending) { SketchSdkLoop.Update -= WaitLateRewardedInterstitial; return; }
            if (Now >= _riCloseDeadline) FinishRewardedInterstitial();
        }

        void FinishRewardedInterstitial()
        {
            SketchSdkLoop.Update -= WaitLateRewardedInterstitial;
            _riClosePending = false;
            SketchSdk.ReportFullscreen(false);
            var callback = _riCallback;
            _riCallback = null;
            var spent = _showingRewardedInterstitial;
            _showingRewardedInterstitial = null;
            try { callback?.Invoke(_riEarned); }
            finally
            {
                SafeDestroy(spent);
                LoadRewardedInterstitial();
            }
        }

        static void SafeDestroy(RewardedAd ad) { try { ad?.Destroy(); } catch (Exception e) { Debug.LogWarning("[SketchAds] Destroy failed: " + e.Message); } }
        static void SafeDestroy(RewardedInterstitialAd ad) { try { ad?.Destroy(); } catch (Exception e) { Debug.LogWarning("[SketchAds] Destroy failed: " + e.Message); } }
        static void SafeDestroy(InterstitialAd ad) { try { ad?.Destroy(); } catch (Exception e) { Debug.LogWarning("[SketchAds] Destroy failed: " + e.Message); } }
        static void SafeDestroy(AppOpenAd ad) { try { ad?.Destroy(); } catch (Exception e) { Debug.LogWarning("[SketchAds] Destroy failed: " + e.Message); } }




    }
}
#endif
