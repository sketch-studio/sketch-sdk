using System;
using UnityEngine;

namespace Sketch.Sdk
{
    sealed class SketchMaxProvider : ISketchAdsProvider, ISketchBannerAdsProvider, ISketchBannerLayoutEvents
    {
        readonly SketchMaxSettings _ids;
        readonly ISketchAdsTelemetry _telemetry;
        string _viewId;
        bool _viewCreated;
        bool _viewLoaded;
        bool _viewRequested;
        float _viewHeight;
        Fullscreen _interstitial;
        Fullscreen _rewarded;
        Fullscreen _appOpen;

        SketchMaxProvider(SketchMaxSettings ids, ISketchAdsTelemetry telemetry)
        {
            _ids = ids;
            _telemetry = telemetry;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
            SketchSdkModules.RegisterAdsProvider(SketchAdsBackend.Max, (settings, telemetry) =>
            {
#if UNITY_IOS
                var ids = settings.MaxIos;
#else
                var ids = settings.MaxAndroid;
#endif
                return ids == null ? null : new SketchMaxProvider(ids, telemetry);
            });
        }

        public void Initialize(Action<bool> onInitialized)
        {
            if (MaxSdk.IsInitialized()) { Prepare(); onInitialized?.Invoke(true); return; }
            void Initialized(MaxSdk.SdkConfiguration configuration)
            {
                MaxSdkCallbacks.OnSdkInitializedEvent -= Initialized;
                try { Prepare(); }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    onInitialized?.Invoke(false);
                    return;
                }
                onInitialized?.Invoke(true);
            }
            MaxSdkCallbacks.OnSdkInitializedEvent += Initialized;
            try { MaxSdk.InitializeSdk(); }
            catch { MaxSdkCallbacks.OnSdkInitializedEvent -= Initialized; throw; }
        }

        void Prepare()
        {
            _viewId = (_ids.UseMrec ? _ids.Mrec : _ids.Banner)?.Trim();
            if (!string.IsNullOrEmpty(_viewId))
            {
                if (_ids.UseMrec)
                {
                    MaxSdkCallbacks.MRec.OnAdLoadedEvent += ViewLoaded;
                    MaxSdkCallbacks.MRec.OnAdRevenuePaidEvent += ViewPaid;
                }
                else
                {
                    MaxSdkCallbacks.Banner.OnAdLoadedEvent += ViewLoaded;
                    MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += ViewPaid;
                }
            }
            if (!string.IsNullOrWhiteSpace(_ids.Interstitial))
            {
                var slot = _interstitial = new Fullscreen(_ids.Interstitial, "interstitial", _telemetry,
                    MaxSdk.LoadInterstitial, MaxSdk.IsInterstitialReady, id => MaxSdk.ShowInterstitial(id));
                MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += slot.Loaded;
                MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += slot.LoadFailed;
                MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += slot.Hidden;
                MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += slot.DisplayFailed;
                MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += slot.Paid;
                slot.Load();
            }
            if (!string.IsNullOrWhiteSpace(_ids.Rewarded))
            {
                var slot = _rewarded = new Fullscreen(_ids.Rewarded, "rewarded", _telemetry,
                    MaxSdk.LoadRewardedAd, MaxSdk.IsRewardedAdReady, id => MaxSdk.ShowRewardedAd(id));
                MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += slot.Loaded;
                MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += slot.LoadFailed;
                MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += slot.Hidden;
                MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += slot.DisplayFailed;
                MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += slot.Paid;
                MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += slot.Rewarded;
                slot.Load();
            }
            if (!string.IsNullOrWhiteSpace(_ids.AppOpen))
            {
                var slot = _appOpen = new Fullscreen(_ids.AppOpen, "app_open", _telemetry,
                    MaxSdk.LoadAppOpenAd, MaxSdk.IsAppOpenAdReady, id => MaxSdk.ShowAppOpenAd(id));
                MaxSdkCallbacks.AppOpen.OnAdLoadedEvent += slot.Loaded;
                MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent += slot.LoadFailed;
                MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += slot.Hidden;
                MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent += slot.DisplayFailed;
                MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent += slot.Paid;
                slot.Load();
            }
        }

        public bool IsBannerReady => _viewLoaded;
        public float BannerHeightPixels => _viewHeight;
        public event Action<float> BannerLayoutChanged;

        public void ShowBanner()
        {
            if (string.IsNullOrEmpty(_viewId)) return;
            _viewRequested = true;
            if (!_viewCreated)
            {
                _viewCreated = true;
                var config = new MaxSdk.AdViewConfiguration(MaxSdk.AdViewPosition.BottomCenter);
                if (_ids.UseMrec) MaxSdk.CreateMRec(_viewId, config);
                else MaxSdk.CreateBanner(_viewId, config);
            }
            if (_ids.UseMrec) MaxSdk.ShowMRec(_viewId);
            else MaxSdk.ShowBanner(_viewId);
            if (_viewLoaded) PublishViewHeight();
        }

        public void HideBanner()
        {
            _viewRequested = false;
            if (_viewCreated)
            {
                if (_ids.UseMrec) MaxSdk.HideMRec(_viewId);
                else MaxSdk.HideBanner(_viewId);
            }
            PublishViewHeight();
        }

        void ViewLoaded(string id, MaxSdk.AdInfo info)
        {
            if (id != _viewId) return;
            _viewLoaded = true;
            PublishViewHeight();
        }

        void PublishViewHeight()
        {
            float height = 0;
            if (_viewLoaded && _viewRequested)
            {
                Rect layout = _ids.UseMrec ? MaxSdk.GetMRecLayout(_viewId) : MaxSdk.GetBannerLayout(_viewId);
                height = Mathf.Max(0, layout.height * MaxSdkUtils.GetScreenDensity());
            }
            if (Mathf.Approximately(_viewHeight, height)) return;
            _viewHeight = height;
            BannerLayoutChanged?.Invoke(height);
        }

        void ViewPaid(string id, MaxSdk.AdInfo info)
        {
            if (id != _viewId) return;
            TrackRevenue(_telemetry, _ids.UseMrec ? "mrec" : "banner", id, info);
        }

        static void TrackRevenue(ISketchAdsTelemetry telemetry, string format, string id, MaxSdk.AdInfo info)
        {
            if (info.Revenue < 0 || telemetry == null) return;
            telemetry.TrackRevenue(new SketchAdRevenue
            {
                Format = format,
                Placement = telemetry.PlacementFor(format),
                Network = "max",
                AdSource = info.NetworkName,
                Currency = "USD",
                ValueMicros = (long)Math.Round(info.Revenue * 1000000d),
                Precision = info.RevenuePrecision,
                AdUnitId = id
            });
        }

        public bool IsReady(SketchAdFormat format)
        {
            switch (format)
            {
                case SketchAdFormat.Interstitial: return _interstitial?.Ready ?? false;
                case SketchAdFormat.Rewarded: return _rewarded?.Ready ?? false;
                case SketchAdFormat.AppOpen: return _appOpen?.Ready ?? false;
                case SketchAdFormat.Banner: return IsBannerReady;
                default: return false;
            }
        }

        public void ShowInterstitial(Action onClosed)
        {
            if (_interstitial == null) onClosed?.Invoke();
            else _interstitial.Show(_ => onClosed?.Invoke());
        }

        public void ShowRewarded(Action<bool> onComplete)
        {
            if (_rewarded == null) onComplete?.Invoke(false);
            else _rewarded.Show(onComplete ?? (_ => { }));
        }

        public void ShowAppOpen(Action onClosed)
        {
            if (_appOpen == null) onClosed?.Invoke();
            else _appOpen.Show(_ => onClosed?.Invoke());
        }

        sealed class Fullscreen
        {
            readonly string _id;
            readonly string _format;
            readonly ISketchAdsTelemetry _telemetry;
            readonly Action<string> _load;
            readonly Func<string, bool> _ready;
            readonly Action<string> _show;
            Action<bool> _completion;
            bool _earned;
            int _failures;
            double _retryAt;

            public Fullscreen(string id, string format, ISketchAdsTelemetry telemetry, Action<string> load,
                Func<string, bool> ready, Action<string> show)
            {
                _id = id.Trim();
                _format = format;
                _telemetry = telemetry;
                _load = load;
                _ready = ready;
                _show = show;
            }

            public bool Ready => _completion == null && _ready(_id);
            public void Load() => _load(_id);

            public void Show(Action<bool> completion)
            {
                if (!Ready) { completion(false); return; }
                _earned = false;
                _completion = completion;
                try { _show(_id); }
                catch (Exception error) { Debug.LogException(error); Finish(false); }
            }

            public void Loaded(string id, MaxSdk.AdInfo info)
            {
                if (id != _id) return;
                _failures = 0;
                SketchSdkLoop.Update -= Retry;
            }

            public void LoadFailed(string id, MaxSdk.ErrorInfo error)
            {
                if (id != _id) return;
                _failures = Math.Min(_failures + 1, 6);
                _retryAt = Time.realtimeSinceStartupAsDouble + (1 << _failures);
                SketchSdkLoop.Update += Retry;
            }

            void Retry()
            {
                if (Time.realtimeSinceStartupAsDouble < _retryAt) return;
                SketchSdkLoop.Update -= Retry;
                Load();
            }

            public void Hidden(string id, MaxSdk.AdInfo info) { if (id == _id) Finish(_earned); }
            public void DisplayFailed(string id, MaxSdk.ErrorInfo error, MaxSdk.AdInfo info) { if (id == _id) Finish(false); }
            public void Rewarded(string id, MaxSdk.Reward reward, MaxSdk.AdInfo info)
            {
                if (id == _id && _completion != null) _earned = true;
            }

            void Finish(bool success)
            {
                var callback = _completion;
                if (callback == null) return;
                _completion = null;
                SketchSdkLoop.Post(Load);
                callback(success);
            }

            public void Paid(string id, MaxSdk.AdInfo info)
            {
                if (id == _id) TrackRevenue(_telemetry, _format, id, info);
            }
        }
    }
}
