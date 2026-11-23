using System;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Sketch.Sdk
{
    sealed class SketchLevelPlayProvider : ISketchAdsProvider, ISketchBannerAdsProvider, ISketchBannerLayoutEvents
    {
        readonly SketchLevelPlaySettings _settings;
        readonly ISketchAdsTelemetry _telemetry;
        LevelPlayInterstitialAd _interstitial;
        LevelPlayRewardedAd _rewarded;
        LevelPlayBannerAd _banner;
        Action<bool> _initialized;
        Action _interstitialClosed;
        Action<bool> _rewardedClosed;
        bool _rewardEarned;
        bool _rewardDismissed;
        string _loadedRewardAuction;
        string _showingRewardAuction;
        bool _bannerRequested;
        bool _bannerLoaded;
        bool _bannerDisplayed;
        double _rewardDeadline;

        SketchLevelPlayProvider(SketchLevelPlaySettings settings, ISketchAdsTelemetry telemetry)
        {
            _settings = settings;
            _telemetry = telemetry;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
            SketchSdkModules.RegisterAdsProvider(SketchAdsBackend.LevelPlay, (settings, telemetry) =>
            {
#if UNITY_IOS
                var platform = settings.LevelPlayIos;
#else
                var platform = settings.LevelPlayAndroid;
#endif
                return platform == null || string.IsNullOrWhiteSpace(platform.AppKey)
                    ? null : new SketchLevelPlayProvider(platform, telemetry);
            });
        }

        public void Initialize(Action<bool> onInitialized)
        {
            _initialized = onInitialized;
            LevelPlay.OnInitSuccess += InitSucceeded;
            LevelPlay.OnInitFailed += InitFailed;
            try { LevelPlay.Init(_settings.AppKey.Trim()); }
            catch { UnsubscribeInitialization(); _initialized = null; throw; }
        }

        void InitSucceeded(LevelPlayConfiguration configuration)
        {
            UnsubscribeInitialization();
            Action<bool> callback = _initialized;
            _initialized = null;
            try
            {
                PrepareInterstitial();
                PrepareRewarded();
                PrepareBanner();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                callback?.Invoke(false);
                return;
            }
            callback?.Invoke(true);
        }

        void InitFailed(LevelPlayInitError error)
        {
            UnsubscribeInitialization();
            Action<bool> callback = _initialized;
            _initialized = null;
            Debug.LogWarning("[Sketch SDK] LevelPlay initialization failed: " + error);
            callback?.Invoke(false);
        }

        void UnsubscribeInitialization()
        {
            LevelPlay.OnInitSuccess -= InitSucceeded;
            LevelPlay.OnInitFailed -= InitFailed;
        }

        void PrepareInterstitial()
        {
            if (string.IsNullOrWhiteSpace(_settings.Interstitial)) return;
            _interstitial = new LevelPlayInterstitialAd(_settings.Interstitial.Trim());
            _interstitial.OnAdClosed += _ => FinishInterstitial();
            _interstitial.OnAdDisplayFailed += (_, __) => FinishInterstitial();
            _interstitial.OnAdLoadFailed += _ => Retry(_interstitial.LoadAd);
            _interstitial.OnAdImpressionDataReady += impression => TrackRevenue("interstitial", impression);
            _interstitial.LoadAd();
        }

        void PrepareRewarded()
        {
            if (string.IsNullOrWhiteSpace(_settings.Rewarded)) return;
            _rewarded = new LevelPlayRewardedAd(_settings.Rewarded.Trim());
            _rewarded.OnAdLoaded += info => _loadedRewardAuction = info?.AuctionId;
            _rewarded.OnAdRewarded += (info, _) =>
            {
                if (!MatchesReward(info)) return;
                _rewardEarned = true;
                if (_rewardDismissed) FinishRewarded();
            };
            _rewarded.OnAdClosed += info =>
            {
                if (!MatchesReward(info) || _rewardDismissed) return;
                _rewardDismissed = true;
                if (_rewardEarned) FinishRewarded();
                else
                {
                    _rewardDeadline = Time.realtimeSinceStartupAsDouble + 2;
                    SketchSdkLoop.Update += AwaitReward;
                }
            };
            _rewarded.OnAdDisplayFailed += (info, _) =>
            {
                if (!MatchesReward(info)) return;
                _rewardEarned = false;
                FinishRewarded();
            };
            _rewarded.OnAdLoadFailed += _ => Retry(_rewarded.LoadAd);
            _rewarded.OnAdImpressionDataReady += impression => TrackRevenue("rewarded", impression);
            _rewarded.LoadAd();
        }

        void PrepareBanner()
        {
            if (string.IsNullOrWhiteSpace(_settings.Banner)) return;
            var config = new LevelPlayBannerAd.Config.Builder().SetDisplayOnLoad(false).Build();
            _banner = new LevelPlayBannerAd(_settings.Banner.Trim(), config);
            _banner.OnAdLoaded += _ =>
            {
                _bannerLoaded = true;
                if (_bannerRequested) _banner.ShowAd();
            };
            _banner.OnAdDisplayed += _ =>
            {
                _bannerDisplayed = _bannerRequested;
                BannerLayoutChanged?.Invoke(BannerHeightPixels);
            };
            _banner.OnAdDisplayFailed += (_, __) =>
            {
                _bannerLoaded = _bannerDisplayed = false;
                BannerLayoutChanged?.Invoke(0);
                Retry(_banner.LoadAd);
            };
            _banner.OnAdLoadFailed += _ => Retry(_banner.LoadAd);
            _banner.OnAdImpressionDataReady += impression => TrackRevenue("banner", impression);
            _banner.LoadAd();
        }

        public bool IsReady(SketchAdFormat format)
        {
            switch (format)
            {
                case SketchAdFormat.Interstitial: return _interstitialClosed == null && (_interstitial?.IsAdReady() ?? false);
                case SketchAdFormat.Rewarded: return _rewardedClosed == null && (_rewarded?.IsAdReady() ?? false);
                case SketchAdFormat.Banner: return IsBannerReady;
                default: return false;
            }
        }

        public void ShowInterstitial(Action onClosed)
        {
            if (!IsReady(SketchAdFormat.Interstitial)) { onClosed?.Invoke(); return; }
            _interstitialClosed = onClosed ?? NoOp;
            try { _interstitial.ShowAd(); }
            catch { FinishInterstitial(); }
        }

        public void ShowRewarded(Action<bool> onComplete)
        {
            if (!IsReady(SketchAdFormat.Rewarded)) { onComplete?.Invoke(false); return; }
            _rewardEarned = _rewardDismissed = false;
            _showingRewardAuction = _loadedRewardAuction;
            _loadedRewardAuction = null;
            _rewardedClosed = onComplete ?? IgnoreReward;
            try { _rewarded.ShowAd(); }
            catch { FinishRewarded(); }
        }

        public void ShowAppOpen(Action onClosed) => onClosed?.Invoke();

        static void NoOp() { }
        static void IgnoreReward(bool earned) { }

        bool MatchesReward(LevelPlayAdInfo info)
        {
            if (_rewardedClosed == null) return false;
            string callbackAuction = info?.AuctionId;
            return string.IsNullOrEmpty(_showingRewardAuction) ||
                string.IsNullOrEmpty(callbackAuction) ||
                string.Equals(_showingRewardAuction, callbackAuction, StringComparison.Ordinal);
        }

        void FinishInterstitial()
        {
            Action callback = _interstitialClosed;
            if (callback == null) return;
            _interstitialClosed = null;
            SketchSdkLoop.Post(_interstitial.LoadAd);
            callback();
        }

        void AwaitReward()
        {
            if (Time.realtimeSinceStartupAsDouble >= _rewardDeadline) FinishRewarded();
        }

        void FinishRewarded()
        {
            Action<bool> callback = _rewardedClosed;
            if (callback == null) return;
            _rewardedClosed = null;
            SketchSdkLoop.Update -= AwaitReward;
            SketchSdkLoop.Post(_rewarded.LoadAd);
            callback(_rewardEarned);
        }

        public bool IsBannerReady => _bannerLoaded;
        public float BannerHeightPixels => _bannerDisplayed
            ? _banner.GetAdSize().Height * (Screen.dpi > 0 ? Screen.dpi / 160f : 1f) : 0f;
        public event Action<float> BannerLayoutChanged;

        public void ShowBanner()
        {
            _bannerRequested = true;
            if (_bannerLoaded) _banner.ShowAd();
        }

        public void HideBanner()
        {
            _bannerRequested = _bannerDisplayed = false;
            _banner?.HideAd();
            BannerLayoutChanged?.Invoke(0);
        }

        static void Retry(Action load)
        {
            double retryAt = Time.realtimeSinceStartupAsDouble + 5;
            void Tick()
            {
                if (Time.realtimeSinceStartupAsDouble < retryAt) return;
                SketchSdkLoop.Update -= Tick;
                load();
            }
            SketchSdkLoop.Update += Tick;
        }

        void TrackRevenue(string format, LevelPlayImpressionData impression)
        {
            if (_telemetry == null || impression?.Revenue == null) return;
            _telemetry.TrackRevenue(new SketchAdRevenue
            {
                Format = format,
                Placement = _telemetry.PlacementFor(format),
                Network = "levelplay",
                AdSource = impression.AdNetwork,
                Currency = "USD",
                ValueMicros = (long)Math.Round(impression.Revenue.Value * 1000000d),
                Precision = impression.Precision,
                AdUnitId = impression.MediationAdUnitId,
                ResponseId = impression.AuctionId
            });
        }
    }
}
