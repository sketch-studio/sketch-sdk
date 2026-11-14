namespace Sketch.Sdk
{
    /// <summary>A completed store purchase reported by the game (or SketchPurchasing). The SDK only records it; entitlements stay in the game.</summary>
    public sealed class SketchPurchase
    {
        public string ProductId { get; set; }
        public string TransactionId { get; set; }
        /// <summary>Localized price paid, in Currency.</summary>
        public double Price { get; set; }
        /// <summary>ISO 4217 code of Price, e.g. USD, VND.</summary>
        public string Currency { get; set; }
        /// <summary>Price converted to USD when known (0 = unknown).</summary>
        public double PriceUsd { get; set; }
        /// <summary>GooglePlay, AppleAppStore, Editor...</summary>
        public string Store { get; set; }
        public string Receipt { get; set; }
        public string Placement { get; set; }
    }
}
