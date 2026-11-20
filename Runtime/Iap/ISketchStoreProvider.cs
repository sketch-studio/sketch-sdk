using System;

namespace Sketch.Sdk
{
    /// <summary>
    /// Store mechanism only (Unity IAP, Editor fake). It never grants: it reports paid orders through OrderReceived and
    /// finishes them only when SketchStoreService calls Confirm after the game's durable grant. Events fire on the main thread.
    /// </summary>
    public interface ISketchStoreProvider
    {
        bool IsReady { get; }

        /// <summary>Paid order (new purchase, unfinished order re-delivered on start, or restored durable purchase).</summary>
        event Action<SketchStoreOrder> OrderReceived;
        /// <summary>(productId, status Failed / Cancelled / Unavailable, message).</summary>
        event Action<string, SketchIapResultStatus, string> PurchaseFailed;
        /// <summary>The payment awaits approval; the order arrives later through OrderReceived.</summary>
        event Action<string> PurchaseDeferred;

        /// <summary>Connect and fetch the catalog products. onInitialized fires once.</summary>
        void Initialize(SketchIapCatalog catalog, Action<bool> onInitialized);
        void Purchase(string productId);
        /// <summary>Finish an order after the durable grant. Returns false when the order is unknown or already finished.</summary>
        bool Confirm(SketchStoreOrder order);
        /// <summary>Ask the store for finished durable purchases; they arrive as OrderReceived with IsRestore. onDone(success, error).</summary>
        void Restore(Action<bool, string> onDone);
        /// <summary>Store-side ownership of a durable product, as last fetched.</summary>
        bool IsOwned(string productId);
        /// <summary>Localized price string, or null when the store has none.</summary>
        string GetLocalizedPrice(string productId);
    }
}
