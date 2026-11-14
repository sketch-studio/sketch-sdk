using System.Collections.Generic;

namespace Sketch.Sdk
{
    /// <summary>Routes provider ad lifecycle and paid events into SketchAnalytics with the placement captured at show time.</summary>
    public sealed class SketchAdsTelemetryRouter : ISketchAdsTelemetry
    {
        /// <summary>Also log ad_loaded / ad_load_failed (useful for fill rate, but frequent).</summary>
        public bool LogLoadEvents { get; set; } = true;

        public string PlacementFor(string format) => SketchAds.PlacementFor(format);

        public void Track(SketchAdTelemetryEvent adEvent)
        {
            if (adEvent == null || string.IsNullOrEmpty(adEvent.Name)) return;
            if (!LogLoadEvents && (adEvent.Name == "ad_loaded" || adEvent.Name == "ad_load_failed")) return;
            var parameters = new Dictionary<string, object>(6)
            {
                ["format"] = adEvent.Format ?? string.Empty,
                ["placement"] = adEvent.Placement ?? string.Empty,
            };
            if (!string.IsNullOrEmpty(adEvent.AdSource)) parameters["ad_source"] = adEvent.AdSource;
            if (!string.IsNullOrEmpty(adEvent.Code)) parameters["code"] = adEvent.Code;
            if (!string.IsNullOrEmpty(adEvent.Message)) parameters["message"] = adEvent.Message;
            SketchAnalytics.Log(adEvent.Name, parameters);
        }

        public void TrackRevenue(SketchAdRevenue revenue) => SketchAnalytics.LogAdRevenue(revenue);
    }
}
