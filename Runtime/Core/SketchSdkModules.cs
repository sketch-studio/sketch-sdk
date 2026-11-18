using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sketch.Sdk
{
    public interface ISketchNotificationsProvider
    {
        void Initialize(Action<bool> onInitialized);
        bool Schedule(SketchNotificationRequest request);
        bool Cancel(string id);
        void CancelAll();
        void ResetOnLaunch();
    }

    public interface ISketchPushProvider
    {
        void Initialize(Action<bool> onInitialized);
        void RequestPermission(Action<bool> onGranted);
        void GetToken(Action<string> onToken);
        void DeleteToken(Action<bool> onDeleted);
    }

    /// <summary>
    /// Optional assemblies (AdMob, Firebase...) register their factories here from
    /// [RuntimeInitializeOnLoadMethod(AfterAssembliesLoaded)], before SketchSdkApp boots at BeforeSceneLoad.
    /// </summary>
    public static class SketchSdkModules
    {
        public static Func<SketchSdkSettings, ISketchAdsTelemetry, ISketchAdsProvider> AdsProviderFactory { get; set; }
        public static Func<SketchSdkSettings, ISketchConsentProvider> ConsentProviderFactory { get; set; }
        public static Func<SketchSdkSettings, ISketchNotificationsProvider> NotificationsProviderFactory { get; set; }
        public static Func<SketchSdkSettings, ISketchPushProvider> PushProviderFactory { get; set; }
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
            PushProviderFactory = null;
            SinkFactories.Clear();
        }
    }
}
