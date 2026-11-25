namespace Sketch.Sdk
{
    public sealed class SketchAdTelemetryEvent
    {
        public string Name { get; set; }
        public string Format { get; set; }
        public string Placement { get; set; }
        public string AdUnitId { get; set; }
        public string AdSource { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public string ResponseId { get; set; }
        /// <summary>Compact mediation waterfall: "Source:code/latencyms;...".</summary>
        public string Adapters { get; set; }
    }
}
