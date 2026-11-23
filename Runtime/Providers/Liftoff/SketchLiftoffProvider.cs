using System;
using UnityEngine;
using VungleAds;

namespace Sketch.Sdk
{
    sealed class SketchLiftoffProvider : ISketchAdsProvider, ISketchBannerAdsProvider, ISketchBannerLayoutEvents
    {
        readonly SketchAdUnitIds _ids;
        VungleInterstitial _interstitial;
        VungleRewarded _rewarded;
        VungleBannerView _banner;
        bool _interstitialShowing;
        bool _rewardedShowing;
        bool _bannerLoaded;
        bool _bannerRequested;
        int _bannerHeight;

        SketchLiftoffProvider(SketchAdUnitIds ids) => _ids = ids;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
            SketchSdkModules.RegisterAdsProvider(SketchAdsBackend.Liftoff, (settings, _) =>
            {
#if UNITY_IOS
                var ids = settings.LiftoffIos;
#else
                var ids = settings.LiftoffAndroid;
#endif
                return ids == null || string.IsNullOrWhiteSpace(ids.AppId) ? null : new SketchLiftoffProvider(ids);
            });
        }

        public void Initialize(Action<bool> onInitialized)
        {
            bool completed = false;
            void RemoveListeners()
            {
                VungleSdk.onInitializeSuccessEvent -= Succeeded;
                VungleSdk.onInitializeFailedEvent -= Failed;
            }
            void Complete(bool success)
            {
                if (completed) return;
                completed = true;
                RemoveListeners();
                if (success)
                {
                    try { LoadInterstitial(); LoadRewarded(); LoadBanner(); }
                    catch (Exception error)
                    {
                        Debug.LogException(error);
                        success = false;
                    }
                }
                onInitialized?.Invoke(success);
            }
            void Succeeded() => SketchSdkLoop.Run(() => Complete(true));
            void Failed(string error) => SketchSdkLoop.Run(() =>
            {
                Debug.LogWarning("[Sketch SDK] Liftoff initialization failed: " + error);
                Complete(false);
            });
            VungleSdk.onInitializeSuccessEvent += Succeeded;
            VungleSdk.onInitializeFailedEvent += Failed;
            try { VungleSdk.Init(_ids.AppId.Trim()); }
            catch { RemoveListeners(); throw; }
        }

        void LoadInterstitial()
        {
            if (string.IsNullOrWhiteSpace(_ids.Interstitial)) return;
            var ad = new VungleInterstitial(_ids.Interstitial.Trim());
            _interstitial = ad;
            ad.onLoadFailed = _ => SketchSdkLoop.Run(() =>
            {
                if (_interstitial != ad) return;
                _interstitial = null;
                Retry(LoadInterstitial);
            });
            ad.Load();
        }

        void LoadRewarded()
        {
            if (string.IsNullOrWhiteSpace(_ids.Rewarded)) return;
            var ad = new VungleRewarded(_ids.Rewarded.Trim());
            _rewarded = ad;
            ad.onLoadFailed = _ => SketchSdkLoop.Run(() =>
            {
                if (_rewarded != ad) return;
                _rewarded = null;
                Retry(LoadRewarded);
            });
            ad.Load();
        }

        public bool IsReady(SketchAdFormat format)
        {
            switch (format)
            {
                case SketchAdFormat.Interstitial: return !_interstitialShowing && (_interstitial?.CanPlay() ?? false);
                case SketchAdFormat.Rewarded: return !_rewardedShowing && (_rewarded?.CanPlay() ?? false);
                case SketchAdFormat.Banner: return IsBannerReady;
                default: return false;
            }
        }

        public void ShowInterstitial(Action onClosed)
        {
            if (!IsReady(SketchAdFormat.Interstitial)) { onClosed?.Invoke(); return; }
            var ad = _interstitial;
            _interstitialShowing = true;
            void Finish()
            {
                if (_interstitial != ad) return;
                _interstitial = null;
                _interstitialShowing = false;
                SketchSdkLoop.Post(LoadInterstitial);
                onClosed?.Invoke();
            }
            ad.onDidClose = () => SketchSdkLoop.Run(Finish);
            ad.onPresentFailed = _ => SketchSdkLoop.Run(Finish);
            try { ad.Show(); }
            catch (Exception error) { Debug.LogException(error); Finish(); }
        }

        public void ShowRewarded(Action<bool> onComplete)
        {
            if (!IsReady(SketchAdFormat.Rewarded)) { onComplete?.Invoke(false); return; }
            var ad = _rewarded;
            _rewardedShowing = true;
            bool earned = false;
            void Finish(bool success)
            {
                if (_rewarded != ad) return;
                _rewarded = null;
                _rewardedShowing = false;
                SketchSdkLoop.Post(LoadRewarded);
                onComplete?.Invoke(success);
            }
            ad.onDidRewardUser = () => SketchSdkLoop.Run(() => earned = true);
            ad.onDidClose = () => SketchSdkLoop.Run(() => Finish(earned));
            ad.onPresentFailed = _ => SketchSdkLoop.Run(() => Finish(false));
            try { ad.Show(); }
            catch (Exception error) { Debug.LogException(error); Finish(false); }
        }

        public void ShowAppOpen(Action onClosed) => onClosed?.Invoke();

        public bool IsBannerReady => _bannerLoaded;
        public float BannerHeightPixels { get; private set; }
        public event Action<float> BannerLayoutChanged;

        void LoadBanner()
        {
            if (string.IsNullOrWhiteSpace(_ids.Banner)) return;
            var ad = new VungleBannerView(_ids.Banner.Trim(), VungleBannerSize.Banner);
            _banner = ad;
            ad.onLoadSuccess = () => SketchSdkLoop.Run(() =>
            {
                if (_banner != ad) return;
                _bannerLoaded = true;
                if (_bannerRequested) AttachBanner();
            });
            ad.onDidPresent = () => SketchSdkLoop.Run(() =>
            {
                if (_banner == ad && _bannerRequested) SetBannerHeight(_bannerHeight);
            });
            void Reload()
            {
                if (_banner != ad) return;
                _banner = null;
                _bannerLoaded = false;
                ad.Destroy();
                SetBannerHeight(0);
                Retry(LoadBanner);
            }
            ad.onLoadFailed = _ => SketchSdkLoop.Run(Reload);
            ad.onPresentFailed = _ => SketchSdkLoop.Run(Reload);
            ad.Load();
        }

        public void ShowBanner()
        {
            if (_bannerRequested) return;
            _bannerRequested = true;
            if (_bannerLoaded) AttachBanner();
        }

        public void HideBanner()
        {
            if (!_bannerRequested) return;
            _bannerRequested = false;
            _banner?.Detach();
            SetBannerHeight(0);
        }

        void AttachBanner()
        {
            Rect safe = Screen.safeArea;
            float scale = Screen.dpi > 0 ? Screen.dpi / 160f : 1f;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var resources = activity.Call<AndroidJavaObject>("getResources"))
            using (var metrics = resources.Call<AndroidJavaObject>("getDisplayMetrics"))
                scale = metrics.Get<float>("density");
#endif
            int width = Mathf.RoundToInt(Mathf.Min(320f * scale, safe.width));
            _bannerHeight = Mathf.RoundToInt(50f * scale);
            _banner.Attach(Mathf.RoundToInt(safe.center.x - width * 0.5f),
                Mathf.RoundToInt(Screen.height - safe.yMin) - _bannerHeight, width, _bannerHeight);
        }

        void SetBannerHeight(float height)
        {
            if (Mathf.Approximately(BannerHeightPixels, height)) return;
            BannerHeightPixels = height;
            BannerLayoutChanged?.Invoke(height);
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
    }
}
