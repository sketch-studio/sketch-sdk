namespace Sketch.Sdk
{
    /// <summary>Purchase funnel boundary used by SketchStoreService (default: SketchIapStoreTelemetry -> SketchIap).</summary>
    public interface ISketchStoreTelemetry
    {
        void PurchaseStarted(string productId, string placement);
        /// <summary>A new paid purchase was granted (never called for restores or re-deliveries already granted).</summary>
        void PurchaseSucceeded(SketchPurchase purchase);
        void PurchaseFailed(string productId, string reason, string placement);
    }
}
