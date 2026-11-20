namespace Sketch.Sdk
{
    /// <summary>Default telemetry: the existing SketchIap funnel (iap_start / iap_success + revenue / iap_failed), plus an optional receipt capture.</summary>
    public sealed class SketchIapStoreTelemetry : ISketchStoreTelemetry
    {
        readonly ISketchIapTelemetry _capture;

        /// <param name="capture">Optional receipt forwarder (server validation). null = funnel only.</param>
        public SketchIapStoreTelemetry(ISketchIapTelemetry capture = null) => _capture = capture;

        public void PurchaseStarted(string productId, string placement) => SketchIap.ReportPurchaseStart(productId, placement);

        public void PurchaseSucceeded(SketchPurchase purchase)
        {
            SketchIap.ReportPurchase(purchase);
            if (_capture == null || purchase == null) return;
            _capture.CaptureIap(new SketchIapCapture
            {
                ProductId = purchase.ProductId,
                Platform = purchase.Store,
                OrderId = purchase.TransactionId,
                Receipt = purchase.Receipt,
                PriceUsd = purchase.PriceUsd,
                Currency = purchase.Currency,
            });
        }

        public void PurchaseFailed(string productId, string reason, string placement) => SketchIap.ReportPurchaseFailed(productId, reason, placement);
    }
}
