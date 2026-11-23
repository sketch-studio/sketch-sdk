using System;

namespace Sketch.Sdk
{
    /// <summary>Which sinks one analytics call reaches (per-event sink mask).</summary>
    [Flags]
    public enum SketchAnalyticsRoute
    {
        None = 0,
        Server = 1,
        Firebase = 2,
        Debug = 4,
        /// <summary>Any sink that does not declare a route (custom game sinks).</summary>
        Other = 8,
        All = Server | Firebase | Debug | Other,
    }

    /// <summary>A sink that declares its route. Sinks without it count as Other.</summary>
    public interface ISketchRoutedSink
    {
        SketchAnalyticsRoute Route { get; }
    }

    public static class SketchAnalyticsRouting
    {
        /// <summary>
        /// Route of SketchAnalytics.Log: everything except Firebase. Firebase gets revenue only (ad_impression, IAP, reach_rev_*)
        /// so gameplay events never eat the Firebase quota (owner OR-806/808).
        /// </summary>
        public const SketchAnalyticsRoute DefaultEvents = SketchAnalyticsRoute.All & ~SketchAnalyticsRoute.Firebase;

        public static SketchAnalyticsRoute RouteOf(ISketchAnalyticsSink sink) =>
            sink is ISketchRoutedSink routed ? routed.Route : SketchAnalyticsRoute.Other;

        public static bool Accepts(SketchAnalyticsRoute mask, ISketchAnalyticsSink sink) => (mask & RouteOf(sink)) != 0;

        /// <summary>OR-873: a Development Build sends to the dev server only (null = send nothing, never production).</summary>
        public static string ServerUrl(SketchSdkSettings settings, bool developmentBuild)
        {
            if (settings == null) return null;
            string url = developmentBuild ? settings.DevServerUrl : settings.ServerUrl;
            return string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        }

        /// <summary>OR-873: Firebase and other module sinks only in release builds, so test play never reaches live data.</summary>
        public static bool ExternalSinksAllowed(bool developmentBuild) => !developmentBuild;
    }
}
