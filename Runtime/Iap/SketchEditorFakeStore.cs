using System;
using System.Collections.Generic;

namespace Sketch.Sdk
{
    /// <summary>
    /// Editor / test store: configurable outcome, prices from the catalog fallback price, unfinished orders re-delivered
    /// on Initialize like a real store, durable ownership kept for Restore. Callbacks arrive through the dispatcher
    /// (next frame by default; pass a => a() for synchronous tests).
    /// </summary>
    public sealed class SketchEditorFakeStore : ISketchStoreProvider
    {
        readonly Action<Action> _dispatch;
        readonly Dictionary<string, SketchStoreOrder> _unfinished = new Dictionary<string, SketchStoreOrder>(StringComparer.Ordinal);
        readonly Dictionary<string, SketchStoreOrder> _history = new Dictionary<string, SketchStoreOrder>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _owned = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly List<SketchStoreOrder> _buffer = new List<SketchStoreOrder>();
        SketchIapCatalog _catalog;
        int _nextTransaction;

        public SketchFakeStoreOutcome Outcome { get; set; } = SketchFakeStoreOutcome.Success;
        public bool SimulateInitFailure { get; set; }
        /// <summary>No localized prices (callers then see the catalog fallback through SketchStore.Price).</summary>
        public bool SimulatePricesUnavailable { get; set; }
        public int ConfirmCount { get; private set; }
        public int UnfinishedCount => _unfinished.Count;
        public bool IsReady { get; private set; }

        public event Action<SketchStoreOrder> OrderReceived;
        public event Action<string, SketchIapResultStatus, string> PurchaseFailed;
        public event Action<string> PurchaseDeferred;

        public SketchEditorFakeStore(Action<Action> dispatch = null) => _dispatch = dispatch ?? SketchSdkLoop.Post;

        public void Initialize(SketchIapCatalog catalog, Action<bool> onInitialized)
        {
            _catalog = catalog;
            _dispatch(() =>
            {
                IsReady = !SimulateInitFailure && _catalog != null;
                SketchLog.Info("Editor fake IAP store " + (IsReady ? "ready (outcome " + Outcome + ")." : "failed to initialize (simulated)."));
                onInitialized?.Invoke(IsReady);
                if (IsReady) RedeliverUnfinished();
            });
        }

        public void Purchase(string productId)
        {
            _dispatch(() =>
            {
                if (!IsReady || _catalog == null || !_catalog.TryGet(productId, out SketchIapProduct product))
                {
                    PurchaseFailed?.Invoke(productId, SketchIapResultStatus.Unavailable, "fake_store_unavailable");
                    return;
                }
                if (product.IsDurable && _owned.ContainsKey(product.Id))
                {
                    PurchaseFailed?.Invoke(productId, SketchIapResultStatus.Failed, "fake_already_owned");
                    return;
                }
                switch (Outcome)
                {
                    case SketchFakeStoreOutcome.Fail:
                        PurchaseFailed?.Invoke(productId, SketchIapResultStatus.Failed, "fake_payment_declined");
                        return;
                    case SketchFakeStoreOutcome.Cancel:
                        PurchaseFailed?.Invoke(productId, SketchIapResultStatus.Cancelled, "fake_user_cancelled");
                        return;
                    case SketchFakeStoreOutcome.DeferredThenSuccess:
                        PurchaseDeferred?.Invoke(productId);
                        _dispatch(() => Deliver(NewOrder(product)));
                        return;
                    default:
                        Deliver(NewOrder(product));
                        return;
                }
            });
        }

        public bool Confirm(SketchStoreOrder order)
        {
            if (order == null || string.IsNullOrEmpty(order.TransactionId) || !_unfinished.Remove(order.TransactionId)) return false;
            ConfirmCount++;
            if (_catalog != null && _catalog.TryGet(order.ProductId, out SketchIapProduct product) && product.IsDurable)
                _owned[product.Id] = order.TransactionId;
            return true;
        }

        public void Restore(Action<bool, string> onDone)
        {
            _dispatch(() =>
            {
                if (!IsReady) { onDone?.Invoke(false, "fake_store_not_ready"); return; }
                _buffer.Clear();
                foreach (KeyValuePair<string, string> owned in _owned)
                {
                    if (!_history.TryGetValue(owned.Value, out SketchStoreOrder original)) continue;
                    SketchStoreOrder restored = Copy(original);
                    restored.IsRestore = true;
                    restored.NeedsConfirmation = false;
                    _buffer.Add(restored);
                }
                for (int i = 0; i < _buffer.Count; i++) OrderReceived?.Invoke(_buffer[i]);
                _buffer.Clear();
                onDone?.Invoke(true, null);
            });
        }

        public bool IsOwned(string productId) => !string.IsNullOrEmpty(productId) && _owned.ContainsKey(productId);

        public string GetLocalizedPrice(string productId)
        {
            if (SimulatePricesUnavailable || _catalog == null || !_catalog.TryGet(productId, out SketchIapProduct product)) return null;
            return string.IsNullOrEmpty(product.FallbackPrice) ? null : product.FallbackPrice;
        }

        /// <summary>Like a relaunch: every paid order that was never confirmed is delivered again.</summary>
        public void RedeliverUnfinished()
        {
            _buffer.Clear();
            _buffer.AddRange(_unfinished.Values);
            for (int i = 0; i < _buffer.Count; i++) OrderReceived?.Invoke(_buffer[i]);
            _buffer.Clear();
        }

        /// <summary>Store glitch: delivers an already seen transaction again (must not grant twice).</summary>
        public void SimulateRedelivery(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId) || !_history.TryGetValue(transactionId, out SketchStoreOrder original)) return;
            OrderReceived?.Invoke(Copy(original));
        }

        SketchStoreOrder NewOrder(SketchIapProduct product)
        {
            _nextTransaction++;
            string transactionId = "fake-" + _nextTransaction.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var order = new SketchStoreOrder
            {
                ProductId = product.Id,
                TransactionId = transactionId,
                Quantity = 1,
                Price = 0,
                Currency = "USD",
                Store = "EditorFake",
                Receipt = "{\"Store\":\"EditorFake\",\"TransactionID\":\"" + transactionId + "\"}",
            };
            _history[transactionId] = order;
            _unfinished[transactionId] = order;
            return order;
        }

        void Deliver(SketchStoreOrder order) => OrderReceived?.Invoke(order);

        static SketchStoreOrder Copy(SketchStoreOrder order) => new SketchStoreOrder
        {
            ProductId = order.ProductId,
            TransactionId = order.TransactionId,
            Quantity = order.Quantity,
            Price = order.Price,
            Currency = order.Currency,
            Store = order.Store,
            Receipt = order.Receipt,
            IsRestore = order.IsRestore,
            NeedsConfirmation = order.NeedsConfirmation,
        };
    }
}
