using System.Collections.Generic;

namespace Sketch.Sdk
{
    /// <summary>Routes provider ad lifecycle and paid events into SketchAnalytics with the placement captured at show time.</summary>
    public sealed class SketchAdsTelemetryRouter : ISketchAdsTelemetry
    {
        /// <summary>Also log ad_loaded / ad_load_failed (useful for fill rate, but frequent).</summary>
        public bool LogLoadEvents { get; set; } = true;

        /// <summary>Bounce BounceSdk.cs MaxAdsLogsPerSecond: at most 10 ad lifecycle logs in any 1 s window (all formats together).</summary>
        public const int MAX_AD_LOGS_PER_SECOND = 10;
        readonly double[] _recent = new double[MAX_AD_LOGS_PER_SECOND];
        int _recentHead;
        /// <summary>Clock for the rate limit (seconds); tests inject one.</summary>
        public System.Func<double> Clock { get; set; } = () => UnityEngine.Time.realtimeSinceStartupAsDouble;

        public string PlacementFor(string format) => SketchAds.PlacementFor(format);

        /// <summary>Ring buffer of the last 10 accepted log times: accept when the oldest is at least 1 s old. No allocation.</summary>
        public bool TryAcceptAdLog()
        {
            double now = Clock();
            double oldest = _recent[_recentHead];
            if (oldest > 0 && now - oldest < 1.0) return false;
            _recent[_recentHead] = now <= 0 ? 1e-9 : now;
            _recentHead = (_recentHead + 1) % MAX_AD_LOGS_PER_SECOND;
            return true;
        }

        public void Track(SketchAdTelemetryEvent adEvent)
        {
            if (adEvent == null || string.IsNullOrEmpty(adEvent.Name)) return;
            if (!LogLoadEvents && (adEvent.Name == "ad_loaded" || adEvent.Name == "ad_load_failed")) return;
            if (!TryAcceptAdLog()) return;
            SketchAnalytics.Log(adEvent.Name, BuildParameters(adEvent));
        }

        public static Dictionary<string, object> BuildParameters(SketchAdTelemetryEvent adEvent)
        {
            var parameters = new Dictionary<string, object>(8)
            {
                ["format"] = adEvent.Format ?? string.Empty,
                ["placement"] = adEvent.Placement ?? string.Empty,
            };
            if (!string.IsNullOrEmpty(adEvent.AdSource)) parameters["ad_source"] = adEvent.AdSource;
            if (!string.IsNullOrEmpty(adEvent.Code)) parameters["code"] = adEvent.Code;
            if (!string.IsNullOrEmpty(adEvent.Message)) parameters["message"] = adEvent.Message;
            if (!string.IsNullOrEmpty(adEvent.ResponseId)) parameters["response_id"] = adEvent.ResponseId;
            if (!string.IsNullOrEmpty(adEvent.Adapters)) parameters["adapters"] = adEvent.Adapters;
            return parameters;
        }

        public void TrackRevenue(SketchAdRevenue revenue) => SketchAnalytics.LogAdRevenue(revenue);
    }
}
