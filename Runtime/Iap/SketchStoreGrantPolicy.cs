namespace Sketch.Sdk
{
    /// <summary>Pure grant rules: no double grant on re-delivery, durable products re-granted only when the local save lost them.</summary>
    public static class SketchStoreGrantPolicy
    {
        public static SketchStoreGrantDecision Decide(SketchIapProduct product, SketchStoreOrder order, ISketchEntitlementStore entitlements)
        {
            if (product == null || order == null || entitlements == null || string.IsNullOrWhiteSpace(order.TransactionId))
                return SketchStoreGrantDecision.Reject;

            if (product.IsDurable)
            {
                if (!entitlements.IsOwned(product.Id)) return SketchStoreGrantDecision.Grant;
                return order.NeedsConfirmation ? SketchStoreGrantDecision.ConfirmOnly : SketchStoreGrantDecision.Ignore;
            }

            // Consumables are never "restored": finished consumable history carries nothing to grant.
            if (order.IsRestore && !order.NeedsConfirmation) return SketchStoreGrantDecision.Ignore;
            if (entitlements.IsTransactionGranted(order.TransactionId))
                return order.NeedsConfirmation ? SketchStoreGrantDecision.ConfirmOnly : SketchStoreGrantDecision.Ignore;
            return SketchStoreGrantDecision.Grant;
        }
    }
}
