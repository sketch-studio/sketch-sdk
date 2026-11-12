#if SKETCHSDK_ANALYTICS
namespace Sketch.Sdk.Integrations
{
    /// <summary>Stable format-level names for analytics; game placements remain consumer-owned.</summary>
    public static class SketchAnalyticsPlacement
    {
        public static string ForFormat(string format)
        {
            if (string.IsNullOrWhiteSpace(format)) return "unknown";
            switch (format.Trim().ToLowerInvariant())
            {
                case "rewarded": return "rewarded";
                case "interstitial": return "interstitial";
                case "app_open": return "app_open";
                case "banner": return "banner";
                case "native": return "native";
                default: return "unknown";
            }
        }
    }
}
#endif
