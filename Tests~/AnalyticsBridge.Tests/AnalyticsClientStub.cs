namespace Sketch.Analytics
{
    public sealed class AnalyticsClient
    {
        public int EventCount { get; private set; }
        public int RevenueCount { get; private set; }
        public int IapCount { get; private set; }
        public string LastEvent { get; private set; }
        public string LastPlacement { get; private set; }
        public System.Collections.Generic.IDictionary<string, object> LastProperties { get; private set; }
        public AdRevenueCapture LastRevenue { get; private set; }
        public IapCapture LastIap { get; private set; }
        public bool Track(string eventName, System.Collections.Generic.IDictionary<string, object> properties = null, string placement = null) { EventCount++; LastEvent = eventName; LastProperties = properties; LastPlacement = placement; return true; }
        public bool TrackAdRevenue(AdRevenueCapture capture) { RevenueCount++; LastRevenue = capture; Track("ad_revenue", new System.Collections.Generic.Dictionary<string, object> { ["revenueUsd"] = capture.RevenueUsd }, capture.Placement); return true; }
        public object CaptureIap(IapCapture capture) { IapCount++; LastIap = capture; return new object(); }
    }
}
