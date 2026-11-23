using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// SDK bootstrap. With SketchSdkSettings.AutoInitialize (default) it runs before the first scene; otherwise call Initialize().
    /// Order: identity -> analytics sinks -> remote config (cache, then server) -> consent (UMP) -> ads provider.
    /// Each step is isolated: a failing step never blocks ads or the ready callback.
    /// </summary>
    public static class SketchSdkApp
    {
        static bool _started;
        static readonly List<Action> ReadyCallbacks = new List<Action>();
        static SketchAdsPolicySettings _basePacing;

        public static bool IsStarted => _started;
        /// <summary>True once consent and ads initialization finished (success or failure).</summary>
        public static bool IsReady { get; private set; }
        public static SketchSdkSettings Settings { get; private set; }

        /// <summary>Run when the SDK is ready (consent asked, ads initializing/initialized). Fires at once when already ready.</summary>
        public static void WhenReady(Action action)
        {
            if (action == null) return;
            if (IsReady) { action(); return; }
            ReadyCallbacks.Add(action);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInitialize()
        {
            SketchSdkSettings settings = SketchSdkSettings.Load();
            if (settings != null && !settings.AutoInitialize) return;
            Initialize(settings);
        }

        /// <summary>Start the SDK. Safe to call more than once; only the first call counts.</summary>
        public static void Initialize(SketchSdkSettings settings = null, Action onReady = null)
        {
            if (onReady != null) WhenReady(onReady);
            if (_started) return;
            _started = true;

            settings = settings != null ? settings : SketchSdkSettings.Load();
            if (settings == null)
            {
                SketchLog.Warn("No SketchSdkSettings found (menu Sketch SDK > Settings creates it). Running with defaults: no server, Editor fake ads.");
                settings = ScriptableObject.CreateInstance<SketchSdkSettings>();
            }
            Settings = settings;
            SketchLog.Verbose = settings.VerboseLogs;

            Step("identity", SketchIdentity.Begin);
            SketchServerClient server = null;
            Step("analytics", () => server = StartAnalytics(settings));
            Step("remote config", () => StartRemoteConfig(settings, server));
            Step("ads policy", () =>
            {
                _basePacing = settings.Pacing ?? new SketchAdsPolicySettings();
                SketchAds.Configure(SketchAdsRemoteKeys.Apply(_basePacing, SketchRemoteConfig.Store));
                SketchRemoteConfig.Updated += ApplyRemotePacing;
                ApplyRemoteLogEnable();
            });
            Step("session event", () => SketchAnalytics.Log("sketch_session_start", "session", SketchIdentity.Session, "app_version", SketchAppVersion.Code, "version_name", Application.version, "sdk_version", SketchSdkVersion.VERSION));

            ISketchConsentProvider consent = null;
            Step("consent provider", () => consent = CreateConsent(settings));
            var request = new SketchConsentRequest
            {
                DebugGeography = settings.ConsentDebugGeography,
                TestDeviceIds = settings.TestDeviceIds != null ? settings.TestDeviceIds.ToArray() : Array.Empty<string>(),
                TagForUnderAgeOfConsent = settings.UnderAgeOfConsent == SketchAdMobTag.True,
            };
            // Every consent outcome converges on ONE ads start (Bounce Thunder_AdmobManager rule).
            SketchConsent.Gather(consent, request, () => StartAds(settings));
        }

        static bool _adsStarted;
        static bool _consentRetryHooked;
        static Action<bool> _focusRetry;

        static void StartAds(SketchSdkSettings settings)
        {
            if (_adsStarted) return;
            if (!SketchConsent.CanRequestAds)
            {
                // Google UMP: do not request ads before consent allows it. The game still starts; ads start after the
                // privacy form is answered (SketchConsent.ShowPrivacyOptions) or when consent is gathered again on resume.
                SketchLog.Warn("Consent does not allow ad requests yet; ads start once consent is given.");
                SketchAnalytics.Log("sketch_ads_waiting_consent");
                HookConsentRetry(settings);
                MarkReady();
                return;
            }
            _adsStarted = true;
            ISketchAdsProvider provider = null;
            Step("ads provider", () => provider = CreateAdsProvider(settings));
            if (provider == null)
            {
                SketchLog.Warn("No ads provider: install Google Mobile Ads (menu Sketch SDK > Settings) and fill the AdMob ids.");
                MarkReady();
                return;
            }
            try
            {
                SketchSdk.Initialize(provider, ok =>
                {
                    SketchLog.Info("Ads initialized: " + ok);
                    SketchAnalytics.Log("sketch_ads_init", "success", ok);
                    MarkReady();
                });
            }
            catch (Exception e)
            {
                SketchLog.Warn("Ads initialization threw: " + e.Message);
                MarkReady();
            }
            // Never hang a splash on a provider that does not answer.
            double deadline = Time.realtimeSinceStartupAsDouble + 10.0;
            void Watchdog()
            {
                if (IsReady) { SketchSdkLoop.Update -= Watchdog; return; }
                if (Time.realtimeSinceStartupAsDouble < deadline) return;
                SketchSdkLoop.Update -= Watchdog;
                SketchLog.Warn("Ads initialization did not answer within 10 s; continuing.");
                MarkReady();
            }
            SketchSdkLoop.Update += Watchdog;
        }

        static void HookConsentRetry(SketchSdkSettings settings)
        {
            if (_consentRetryHooked) return;
            _consentRetryHooked = true;
            SketchConsent.Changed += () => { if (SketchConsent.CanRequestAds && Settings != null) StartAds(Settings); };
            _focusRetry = focused =>
            {
                if (!focused || _adsStarted || !_started || Settings == null) return;
                SketchConsent.Regather(() => { if (Settings != null) StartAds(Settings); });
            };
            Application.focusChanged += _focusRetry;
        }

        static ISketchAdsProvider CreateAdsProvider(SketchSdkSettings settings)
        {
            var telemetry = new SketchAdsTelemetryRouter();
#if UNITY_EDITOR
            return new SketchEditorFakeProvider();
#else
            if (SketchSdkModules.AdsProviderFactory == null) return null;
            return SketchSdkModules.AdsProviderFactory(settings, telemetry);
#endif
        }

        static ISketchConsentProvider CreateConsent(SketchSdkSettings settings)
        {
#if UNITY_EDITOR
            return null;
#else
            if (!settings.RequestConsent || SketchSdkModules.ConsentProviderFactory == null) return null;
            return SketchSdkModules.ConsentProviderFactory(settings);
#endif
        }

        static SketchServerClient StartAnalytics(SketchSdkSettings settings)
        {
            SketchAnalytics.RevenueMilestonesUsd = settings.RevenueMilestonesUsd;
            SketchServerClient server = null;
#if UNITY_EDITOR
            // OR-818 (owner): the Editor never sends analytics, revenue or IAP to the server or Firebase (no live-data noise).
            // Every event is printed locally instead so the call sites can still be checked; test real logging on a device build.
            SketchAnalytics.AddSink(new SketchDebugLogSink());
            SketchLog.Info("Sketch analytics: Editor mode, events are logged to the Console only (no server / Firebase).");
#else
            // OR-873: Development Builds are test builds: dev server only, no Firebase, never production data.
            bool devBuild = Debug.isDebugBuild;
            if (settings.VerboseLogs || devBuild) SketchAnalytics.AddSink(new SketchDebugLogSink());
            string serverUrl = SketchAnalyticsRouting.ServerUrl(settings, devBuild);
            if (settings.HasServerCredentials && serverUrl != null)
            {
                server = new SketchServerClient(serverUrl, settings.ServerAppId, settings.ServerApiKey, settings.ServerFlushIntervalSeconds);
                server.Start();
                SketchAnalytics.AddSink(server);
                if (devBuild) SketchLog.Info("Sketch analytics: Development Build -> " + serverUrl + " (Firebase off).");
            }
            else if (settings.ServerEnabled)
            {
                SketchLog.Warn("Sketch Server is enabled but URL / App Id / API key is missing; server analytics off.");
            }
            if (SketchAnalyticsRouting.ExternalSinksAllowed(devBuild))
            foreach (var factory in SketchSdkModules.AnalyticsSinkFactories)
            {
                try
                {
                    ISketchAnalyticsSink sink = factory(settings);
                    if (sink != null) SketchAnalytics.AddSink(sink);
                }
                catch (Exception e) { SketchLog.Warn("Analytics module failed: " + e.Message); }
            }
#endif
            return server;
        }

        static void StartRemoteConfig(SketchSdkSettings settings, SketchServerClient server)
        {
            SketchRemoteConfig.ApplyDefaults(settings);
            SketchRemoteConfig.LoadCache();
            if (server == null || !settings.ServerRemoteConfig)
            {
                SketchRemoteConfig.MarkReady();
                return;
            }
            float timeout = Mathf.Clamp(settings.RemoteConfigTimeoutSeconds, 1f, 30f);
            server.FetchRemoteConfig(timeout, ok =>
            {
                SketchAnalytics.Log("sketch_remote_config", "fetched", ok, "config_id", SketchRemoteConfig.ConfigId);
                SketchRemoteConfig.MarkReady();
            });
        }

        // Bounce RemoteConfig.logEnable (RemoteConfig.cs:39, checked in BounceSdk.LogAds / LogEvent): remote kill switch for events.
        static void ApplyRemoteLogEnable() =>
            SketchAnalytics.EventsEnabled = SketchRemoteConfig.GetBool(SketchAnalyticsSchema.REMOTE_LOG_ENABLE, true);

        static void ApplyRemotePacing()
        {
            ApplyRemoteLogEnable();
            if (_basePacing == null) return;
            SketchAdsPolicy policy = SketchAds.Policy;
            policy.Settings = SketchAdsRemoteKeys.Apply(_basePacing, SketchRemoteConfig.Store);
        }

        static void MarkReady()
        {
            if (IsReady) return;
            IsReady = true;
            Action[] callbacks = ReadyCallbacks.ToArray();
            ReadyCallbacks.Clear();
            foreach (Action callback in callbacks)
            {
                try { callback(); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        static void Step(string name, Action action)
        {
            try { action(); }
            catch (Exception e) { SketchLog.Error($"Init step '{name}' failed: {e}"); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SketchRemoteConfig.Updated -= ApplyRemotePacing;
            _started = false;
            _adsStarted = false;
            _consentRetryHooked = false;
            if (_focusRetry != null) Application.focusChanged -= _focusRetry;
            _focusRetry = null;
            IsReady = false;
            Settings = null;
            _basePacing = null;
            ReadyCallbacks.Clear();
        }
    }
}
