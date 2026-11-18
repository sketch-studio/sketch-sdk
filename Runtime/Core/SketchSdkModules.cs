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
        public static Func<SketchSdkSettings, ISketchConsentProvider> ConsentProviderFactory { get; set; }
        public static Func<SketchSdkSettings, ISketchNotificationsProvider> NotificationsProviderFactory { get; set; }
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
            ConsentProviderFactory = null;
            NotificationsProviderFactory = null;
            SinkFactories.Clear();
        }
    }
}
