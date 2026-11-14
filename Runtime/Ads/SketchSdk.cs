using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sketch.Sdk
{
    public static class SketchSdk
    {
        static ISketchAdsProvider _provider;
        static ISketchBannerLayoutEvents _bannerLayoutProvider;
        static Action<float> _bannerLayoutHandler;
        static float _bannerHeightPixels;
        static bool _bannerRequested;
        static readonly List<Action<bool>> InitializationCallbacks = new List<Action<bool>>();
        static bool _initialized;
        static bool _initializing;
        static int _providerGeneration;
        static bool _fullscreenReserved;
        static bool _nativeStarting;
        static bool _nativeVisible;
        static int _nativeGeneration;
        static double _nativeStartedAt;
        static float _activeNativeAttachTimeoutSeconds;
        static double _fullscreenClosedAt = double.NegativeInfinity;
        static SketchNativeCallbacks _nativeCallbacks;
        static SketchNativeCloseReason? _forcedNativeCloseReason;

        public static bool IsInitialized => _initialized;
        public static ISketchAdsProvider Provider => _provider;
        public static float NativeAttachTimeoutSeconds { get; set; }
        public static bool FullscreenAdShowing { get; private set; }
        public static double SecondsSinceFullscreenClosed => Time.realtimeSinceStartupAsDouble - _fullscreenClosedAt;

        public static void Initialize(ISketchAdsProvider provider, Action<bool> onInitialized = null)
        {
            if (provider != null && !ReferenceEquals(_provider, provider) &&
                (_fullscreenReserved || FullscreenAdShowing || _nativeStarting || _nativeVisible))
            {
                onInitialized?.Invoke(false);
                return;
            }
            if (provider != null && !ReferenceEquals(_provider, provider))
            {
                ClearBannerLayoutProvider();
                _provider = provider;
                _providerGeneration++;
                AttachBannerLayoutProvider(provider, _providerGeneration);
                _initialized = false;
                bool replacedPendingInitialization = _initializing;
                _initializing = false;
                if (replacedPendingInitialization) CompleteInitialization(false);
            }
            if (_initialized) { onInitialized?.Invoke(true); return; }
            if (onInitialized != null) InitializationCallbacks.Add(onInitialized);
            if (_provider == null) { CompleteInitialization(false); return; }
            if (_initializing) return;

            _initializing = true;
            int generation = _providerGeneration;
            ISketchAdsProvider currentProvider = _provider;
            try
            {
                currentProvider.Initialize(ok =>
                {
                    if (generation != _providerGeneration || !ReferenceEquals(currentProvider, _provider) || !_initializing) return;
                    _initializing = false;
                    _initialized = ok;
                    if (!ok) PublishBannerHeight(0f);
                    else RefreshBannerHeight(currentProvider, generation);
                    CompleteInitialization(ok);
                });
            }
            catch (Exception e)
            {
                if (generation != _providerGeneration || !_initializing) throw;
                _initializing = false;
                _initialized = false;
                Debug.LogWarning("[SketchAds] Provider initialization failed: " + e.Message);
                CompleteInitialization(false);
            }
        }

        static void CompleteInitialization(bool success)
        {
            if (InitializationCallbacks.Count == 0) return;
            Action<bool>[] callbacks = InitializationCallbacks.ToArray();
            InitializationCallbacks.Clear();
            for (int i = 0; i < callbacks.Length; i++) callbacks[i]?.Invoke(success);
        }

        public static bool IsReady(SketchAdFormat format)
        {
            if (!_initialized || _provider == null) return false;
            if (format == SketchAdFormat.Banner)
                return _provider is ISketchBannerAdsProvider banner && banner.IsBannerReady;
            if (format == SketchAdFormat.Native)
                return _provider is ISketchNativeAdsProvider native && native.IsNativeReady;
            return _provider.IsReady(format);
        }

        static bool TryReserveFullscreen()
        {
            if (_fullscreenReserved || FullscreenAdShowing) return false;
            _fullscreenReserved = true;
            return true;
        }

        static void ReleaseFullscreen()
        {
            _fullscreenReserved = false;
        }

        // Completion of the fullscreen ad in progress; used by the watchdog when a provider never reports "closed".
        static Action _abortFullscreen;

        /// <summary>Completes the fullscreen ad in progress as closed (rewarded: not granted). Returns false when none is pending.</summary>
        internal static bool ForceCloseFullscreen()
        {
            Action abort = _abortFullscreen;
            if (abort == null) return false;
            Debug.LogWarning("[SketchAds] Fullscreen ad never reported closed; completing it (watchdog).");
            abort();
            FullscreenAdShowing = false;
            _fullscreenClosedAt = Time.realtimeSinceStartupAsDouble;
            return true;
        }

        static bool TryShowFullscreen(Action<Action> show, Action onClosed)
        {
            bool completed = false;
            if (!TryReserveFullscreen()) return false;
            void Complete()
            {
                if (completed) return;
                completed = true;
                _abortFullscreen = null;
                ReleaseFullscreen();
                onClosed?.Invoke();
            }
            _abortFullscreen = Complete;
            try
            {
                show(Complete);
                return true;
            }
            catch (Exception e)
            {
                if (completed) throw;
                completed = true;
                _abortFullscreen = null;
                ReleaseFullscreen();
                Debug.LogWarning("[SketchAds] Fullscreen show failed: " + e.Message);
                return false;
            }
        }

        public static bool ShowRewarded(Action<bool> onComplete)
        {
            if (!IsReady(SketchAdFormat.Rewarded) || !TryReserveFullscreen()) return false;
            bool completed = false;
            void Complete(bool ok)
            {
                if (completed) return;
                completed = true;
                _abortFullscreen = null;
                ReleaseFullscreen();
                onComplete?.Invoke(ok);
            }
            _abortFullscreen = () => Complete(false);
            try
            {
                _provider.ShowRewarded(Complete);
                return true;
            }
            catch (Exception e)
            {
                if (completed) throw;
                completed = true;
                _abortFullscreen = null;
                ReleaseFullscreen();
                Debug.LogWarning("[SketchAds] Rewarded show failed: " + e.Message);
                return false;
            }
        }

        public static bool ShowInterstitial(Action onClosed = null)
        {
            if (!IsReady(SketchAdFormat.Interstitial)) return false;
            return TryShowFullscreen(callback => _provider.ShowInterstitial(callback), onClosed);
        }

        public static bool ShowAppOpen(Action onClosed = null)
        {
            if (!IsReady(SketchAdFormat.AppOpen)) return false;
            return TryShowFullscreen(callback => _provider.ShowAppOpen(callback), onClosed);
        }

        public static void ShowBanner()
        {
            if (!IsReady(SketchAdFormat.Banner) || !(_provider is ISketchBannerAdsProvider banner)) return;
            _bannerRequested = true;
            try { banner.ShowBanner(); }
            catch (Exception e) { _bannerRequested = false; Debug.LogWarning("[SketchAds] Banner show failed: " + e.Message); }
            RefreshBannerHeight(_provider, _providerGeneration);
        }

        public static void HideBanner()
        {
            _bannerRequested = false;
            try { (_provider as ISketchBannerAdsProvider)?.HideBanner(); }
            catch (Exception e) { Debug.LogWarning("[SketchAds] Banner hide failed: " + e.Message); }
            PublishBannerHeight(0f);
        }

        public static bool ShowNativeInRect(SketchNativeRequest request, SketchNativeCallbacks callbacks = null)
        {
            if (!IsNativeReady || _fullscreenReserved || FullscreenAdShowing || _nativeStarting || _nativeVisible ||
                request.ScreenRect.width <= 0f || request.ScreenRect.height <= 0f) return false;
            ISketchNativeAdsProvider nativeProvider = (ISketchNativeAdsProvider)_provider;
            if (request.AttachTimeoutSeconds < 0f) request.AttachTimeoutSeconds = NativeAttachTimeoutSeconds;
            request.CloseInsideAd &= _provider is ISketchAdCloseCapability closeCapability && closeCapability.SupportsInAdClose;

            int generation = ++_nativeGeneration;
            _nativeStarting = true;
            _nativeStartedAt = Time.realtimeSinceStartupAsDouble;
            _activeNativeAttachTimeoutSeconds = request.AttachTimeoutSeconds;
            _nativeCallbacks = callbacks;
            _forcedNativeCloseReason = null;
            var callbackGate = new SketchNativeCallbacks.Gate(
                () => NativeShown(generation),
                () => NativeClicked(generation),
                reason => NativeClosed(generation, reason));
            bool accepted;
            try { accepted = nativeProvider.ShowNativeInRect(request, callbackGate.ProviderCallbacks); }
            catch (Exception e)
            {
                callbackGate.Reject();
                if (generation == _nativeGeneration) ClearNativeState();
                Debug.LogWarning("[SketchAds] Native in-rect show failed: " + e.Message);
                return false;
            }
            if (!accepted)
            {
                callbackGate.Reject();
                if (generation == _nativeGeneration) ClearNativeState();
                return false;
            }
            callbackGate.Accept();
            if (_nativeStarting)
            {
                Application.onBeforeRender -= TickNativeAttach;
                Application.onBeforeRender += TickNativeAttach;
            }
            return true;
        }

        public static void HideNative(SketchNativeCloseReason reason = SketchNativeCloseReason.Teardown)
        {
            if (!_nativeStarting && !_nativeVisible) return;
            int generation = _nativeGeneration;
            _forcedNativeCloseReason = reason;
            try { (_provider as ISketchNativeAdsProvider)?.HideNative(); }
            catch (Exception e) { Debug.LogWarning("[SketchAds] Native hide failed: " + e.Message); }
            NativeClosed(generation, reason);
        }

        public static bool IsNativeReady => IsReady(SketchAdFormat.Native);
        public static bool IsNativeVisible => _nativeVisible;
        /// <summary>Raised on the Unity main thread when visible banner occupancy changes.</summary>
        public static event Action<float> BannerLayoutChanged;
        /// <summary>Current finite positive banner height in pixels, or zero when hidden, unready, or invalid.</summary>
        public static float BannerHeightPixels
        {
            get
            {
                if (!_bannerRequested || !IsReady(SketchAdFormat.Banner) || !(_provider is ISketchBannerAdsProvider banner)) return 0f;
                if (_provider is ISketchBannerLayoutEvents) return _bannerHeightPixels;
                return NormalizeBannerHeight(banner.BannerHeightPixels);
            }
        }

        static void AttachBannerLayoutProvider(ISketchAdsProvider provider, int generation)
        {
            if (!(provider is ISketchBannerLayoutEvents layoutEvents)) return;
            _bannerLayoutProvider = layoutEvents;
            _bannerLayoutHandler = _ => RefreshBannerHeight(provider, generation);
            _bannerLayoutProvider.BannerLayoutChanged += _bannerLayoutHandler;
        }

        static void ClearBannerLayoutProvider()
        {
            _bannerRequested = false;
            try { (_provider as ISketchBannerAdsProvider)?.HideBanner(); }
            catch (Exception e) { Debug.LogWarning("[SketchAds] Banner reset failed: " + e.Message); }
            if (_bannerLayoutProvider != null && _bannerLayoutHandler != null)
                _bannerLayoutProvider.BannerLayoutChanged -= _bannerLayoutHandler;
            _bannerLayoutProvider = null;
            _bannerLayoutHandler = null;
            PublishBannerHeight(0f);
        }

        static void RefreshBannerHeight(ISketchAdsProvider provider, int generation)
        {
            if (generation != _providerGeneration || !ReferenceEquals(provider, _provider)) return;
            if (!_bannerRequested || !IsReady(SketchAdFormat.Banner) || !(provider is ISketchBannerAdsProvider banner))
            {
                PublishBannerHeight(0f);
                return;
            }

            PublishBannerHeight(NormalizeBannerHeight(banner.BannerHeightPixels));
        }

        static float NormalizeBannerHeight(float height)
        {
            return float.IsNaN(height) || float.IsInfinity(height) || height <= 0f ? 0f : height;
        }

        static void PublishBannerHeight(float height)
        {
            height = NormalizeBannerHeight(height);
            if (_bannerHeightPixels == height) return;
            _bannerHeightPixels = height;
            Action<float> listeners = BannerLayoutChanged;
            if (listeners == null) return;
            Delegate[] subscribers = listeners.GetInvocationList();
            int generation = _providerGeneration;
            ISketchAdsProvider provider = _provider;
            for (int i = 0; i < subscribers.Length; i++)
            {
                try { ((Action<float>)subscribers[i])(height); }
                catch (Exception e)
                {
                    try { Debug.LogWarning("[SketchAds] Banner layout listener failed: " + e.Message); }
                    catch { }
                }
                if (generation != _providerGeneration || !ReferenceEquals(provider, _provider) || _bannerHeightPixels != height) return;
            }
        }

        static void NativeShown(int generation)
        {
            if (generation != _nativeGeneration || (!_nativeStarting && !_nativeVisible) || _nativeVisible) return;
            _nativeStarting = false;
            _nativeVisible = true;
            Application.onBeforeRender -= TickNativeAttach;
            _nativeCallbacks?.Shown();
        }

        static void NativeClicked(int generation)
        {
            if (generation == _nativeGeneration && (_nativeStarting || _nativeVisible)) _nativeCallbacks?.Clicked();
        }

        static void NativeClosed(int generation, SketchNativeCloseReason reason)
        {
            if (generation != _nativeGeneration || (!_nativeStarting && !_nativeVisible)) return;
            SketchNativeCallbacks callback = _nativeCallbacks;
            SketchNativeCloseReason finalReason = _forcedNativeCloseReason ?? reason;
            ClearNativeState();
            callback?.Closed(finalReason);
        }

        static void TickNativeAttach()
        {
            if (!_nativeStarting) { Application.onBeforeRender -= TickNativeAttach; return; }
            if (_activeNativeAttachTimeoutSeconds > 0f && Time.realtimeSinceStartupAsDouble - _nativeStartedAt >= _activeNativeAttachTimeoutSeconds)
                HideNative(SketchNativeCloseReason.LoadFailed);
        }

        static void ClearNativeState()
        {
            Application.onBeforeRender -= TickNativeAttach;
            _nativeStarting = false;
            _nativeVisible = false;
            _activeNativeAttachTimeoutSeconds = 0f;
            _nativeCallbacks = null;
            _forcedNativeCloseReason = null;
        }

        public static void ReportFullscreen(bool open)
        {
            FullscreenAdShowing = open;
            if (!open) _fullscreenClosedAt = Time.realtimeSinceStartupAsDouble;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ClearBannerLayoutProvider();
            BannerLayoutChanged = null;
            FullscreenAdShowing = false;
            _fullscreenReserved = false;
            _abortFullscreen = null;
            _fullscreenClosedAt = double.NegativeInfinity;
            NativeAttachTimeoutSeconds = 0f;
            ClearNativeState();
            _nativeGeneration++;
            _providerGeneration++;
            _provider = null;
            _initialized = false;
            _initializing = false;
            InitializationCallbacks.Clear();
        }
    }
}
