using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Optional assemblies (AdMob, Firebase...) register their factories here from
    /// [RuntimeInitializeOnLoadMethod(AfterAssembliesLoaded)], before SketchSdkApp boots at BeforeSceneLoad.
    /// </summary>
    public static class SketchSdkModules
    {
        public static Func<SketchSdkSettings, ISketchAdsTelemetry, ISketchAdsProvider> AdsProviderFactory { get; set; }
        static readonly Dictionary<SketchAdsBackend, Func<SketchSdkSettings, ISketchAdsTelemetry, ISketchAdsProvider>>
            AdsFactories = new Dictionary<SketchAdsBackend, Func<SketchSdkSettings, ISketchAdsTelemetry, ISketchAdsProvider>>();

        public static void RegisterAdsProvider(
            SketchAdsBackend backend, Func<SketchSdkSettings, ISketchAdsTelemetry, ISketchAdsProvider> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (backend == SketchAdsBackend.AdMob)
            {
                AdsProviderFactory = factory;
                return;
            }
            AdsFactories[backend] = factory;
        }

        internal static ISketchAdsProvider CreateAdsProvider(SketchSdkSettings settings, ISketchAdsTelemetry telemetry)
        {
            if (settings.AdsBackend == SketchAdsBackend.AdMob) return AdsProviderFactory?.Invoke(settings, telemetry);
            return AdsFactories.TryGetValue(settings.AdsBackend, out var factory) ? factory(settings, telemetry) : null;
        }

        public static Func<SketchSdkSettings, ISketchConsentProvider> ConsentProviderFactory { get; set; }
        public static Func<SketchSdkSettings, ISketchConsentProvider> AdMobConsentProviderFactory { get; set; }
        public static Action<Action> RequestTrackingAuthorization { get; set; }
        static readonly List<Func<SketchSdkSettings, ISketchAnalyticsSink>> SinkFactories = new List<Func<SketchSdkSettings, ISketchAnalyticsSink>>();

        public static IReadOnlyList<Func<SketchSdkSettings, ISketchAnalyticsSink>> AnalyticsSinkFactories => SinkFactories;

        public static void AddAnalyticsSink(Func<SketchSdkSettings, ISketchAnalyticsSink> factory)
        {
            if (factory != null && !SinkFactories.Contains(factory)) SinkFactories.Add(factory);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            AdsProviderFactory = null;
            AdsFactories.Clear();
            ConsentProviderFactory = null;
            AdMobConsentProviderFactory = null;
            RequestTrackingAuthorization = null;
            SinkFactories.Clear();
        }
    }
}
