namespace Sketch.Sdk
{
    public sealed class SketchAdRevenue
    {
        public string Format { get; set; }
        public string Placement { get; set; }
        public string Network { get; set; }
        public string Currency { get; set; }
        public string Precision { get; set; }
        public long ValueMicros { get; set; }
        public string AdUnitId { get; set; }
        public string ResponseId { get; set; }
        public string AdSource { get; set; }
    }
}
