namespace Sketch.Sdk
{
    /// <summary>A paid order reported by a store provider. The service grants it, then asks the provider to confirm it.</summary>
    public sealed class SketchStoreOrder
    {
        public string ProductId { get; set; }
        public string TransactionId { get; set; }
        public int Quantity { get; set; } = 1;
        /// <summary>Localized price paid (0 = unknown) in Currency.</summary>
        public double Price { get; set; }
        public string Currency { get; set; }
        public string Store { get; set; }
        public string Receipt { get; set; }
        /// <summary>A previously finished durable purchase found again (restore / purchase history), not a new payment.</summary>
        public bool IsRestore { get; set; }
        /// <summary>False when the store already finished the order (restored history); the service then never calls Confirm.</summary>
        public bool NeedsConfirmation { get; set; } = true;
    }
}
