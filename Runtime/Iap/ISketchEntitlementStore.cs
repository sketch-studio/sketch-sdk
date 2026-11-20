namespace Sketch.Sdk
{
    /// <summary>
    /// Durable record of granted transactions and owned durable products. The game implements it on its own save
    /// (DataBlock) and must persist each write together with the fulfillment grant (same save flush), so a crash can
    /// never leave a granted item without its transaction record. SketchMemoryEntitlementStore is for tests only.
    /// </summary>
    public interface ISketchEntitlementStore
    {
        bool IsTransactionGranted(string transactionId);
        void MarkTransactionGranted(string transactionId, string productId);
        bool IsOwned(string productId);
        void SetOwned(string productId, bool owned);
    }
}
