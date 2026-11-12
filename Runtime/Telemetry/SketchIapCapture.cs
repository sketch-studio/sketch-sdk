namespace Sketch.Sdk
{
    /// <summary>Receipt is forwarded to the server validator; this SDK never grants entitlements.</summary>
    public sealed class SketchIapCapture
    {
        public string ProductId { get; set; }
        public string Platform { get; set; }
        public string OrderId { get; set; }
        public string Receipt { get; set; }
        public double PriceUsd { get; set; }
        public string Currency { get; set; }
    }
}
