namespace Sketch.Sdk
{
    /// <summary>
    /// IAP funnel reporting for games that run their own store code (SketchPurchasing calls these for you).
    /// The SDK never grants items: call ReportPurchase only after the store confirmed the transaction.
    /// </summary>
    public static class SketchIap
    {
        /// <summary>Player tapped a buy button. Also stops the store dialog from triggering an app-open ad on return.</summary>
        public static void ReportPurchaseStart(string productId, string placement = null)
        {
            SketchAds.SuppressNextAppOpen();
            // Bounce logs iap_start to the server AND Firebase (BounceSdk.cs LogIAPEvent START -> FirebaseManager.Log("iap_start")).
            SketchAnalytics.LogTo(SketchAnalyticsRoute.All, "iap_start", "product_id", productId ?? string.Empty, "placement", placement ?? string.Empty);
        }

        public static void ReportPurchase(SketchPurchase purchase)
        {
            if (purchase == null) return;
            SketchAds.ResetInterstitialCooldown();   // a paying player should not see an interstitial right away
            SketchAnalytics.LogPurchase(purchase);
        }

        public static void ReportPurchaseFailed(string productId, string reason, string placement = null) =>
            SketchAnalytics.Log("iap_failed", "product_id", productId ?? string.Empty, "reason", reason ?? string.Empty, "placement", placement ?? string.Empty);
    }
}
