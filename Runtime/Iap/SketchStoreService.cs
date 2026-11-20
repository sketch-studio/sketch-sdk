using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Purchase orchestration: one purchase at a time, grant order pending -> game fulfillment (durable) -> entitlement
    /// record -> store confirmation, transaction dedup, durable re-grant on restore, remove-ads link, funnel telemetry.
    /// Every dependency is injected; SketchStore is the static facade games call.
    /// </summary>
    public sealed class SketchStoreService : IDisposable
    {
        readonly SketchIapCatalog _catalog;
        readonly ISketchStoreProvider _provider;
        readonly ISketchEntitlementStore _entitlements;
        readonly Func<SketchIapGrant, bool> _fulfill;
        readonly ISketchStoreTelemetry _telemetry;
        readonly Action<bool> _setAdsRemoved;
        readonly Dictionary<string, SketchStoreOrder> _unfulfilled = new Dictionary<string, SketchStoreOrder>(StringComparer.Ordinal);
        readonly List<SketchStoreOrder> _retryBuffer = new List<SketchStoreOrder>();

        string _inflightId;
        string _inflightPlacement;
        Action<SketchIapResult> _inflightCallback;
        bool _initialized;
        bool _initializing;
        bool _disposed;

        /// <summary>(product, transactionId, isRestore) after the game's fulfillment returned true.</summary>
        public event Action<SketchIapProduct, string, bool> Granted;

        public bool IsInitialized => _initialized;
        public bool IsReady => _initialized && !_disposed && _provider.IsReady;
        public bool IsPurchasing => _inflightId != null;
        /// <summary>Paid orders whose fulfillment returned false; they stay unconfirmed until RetryPending or a store re-delivery grants them.</summary>
        public int UnfulfilledCount => _unfulfilled.Count;
        public SketchIapCatalog Catalog => _catalog;

        /// <param name="fulfill">Game grant. Return true only after the grant is durable (saved together with the entitlement store).</param>
        /// <param name="telemetry">null = no funnel events.</param>
        /// <param name="setAdsRemoved">Called with true when a remove-ads product is granted, restored or found owned. null = no-op.</param>
        public SketchStoreService(SketchIapCatalog catalog, ISketchStoreProvider provider, ISketchEntitlementStore entitlements,
            Func<SketchIapGrant, bool> fulfill, ISketchStoreTelemetry telemetry = null, Action<bool> setAdsRemoved = null)
        {
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
            _fulfill = fulfill ?? throw new ArgumentNullException(nameof(fulfill));
            _telemetry = telemetry;
            _setAdsRemoved = setAdsRemoved;
            _provider.OrderReceived += OnOrderReceived;
            _provider.PurchaseFailed += OnPurchaseFailed;
            _provider.PurchaseDeferred += OnPurchaseDeferred;
        }

        public void Initialize(Action<bool> onDone = null)
        {
            if (_disposed) { Safe(onDone, false); return; }
            if (_initialized) { Safe(onDone, true); return; }
            if (_initializing) { SketchLog.Warn("SketchStore is already initializing."); Safe(onDone, false); return; }
            _initializing = true;
            SyncAdsRemoved();   // durable ownership wins at once, before the store answers
            _provider.Initialize(_catalog, ok =>
            {
                _initializing = false;
                _initialized = ok && !_disposed;
                if (!ok) SketchLog.Warn("IAP store failed to initialize; purchases are unavailable.");
                Safe(onDone, _initialized);
            });
        }

        /// <summary>onResult fires exactly once. Deferred means the grant arrives later through Granted.</summary>
        public void Purchase(string productId, Action<SketchIapResult> onResult, string placement = null)
        {
            if (_disposed || !_initialized || !_provider.IsReady)
            {
                Invoke(onResult, new SketchIapResult(productId, SketchIapResultStatus.Unavailable, message: "store_not_ready"));
                return;
            }
            if (!_catalog.TryGet(productId, out SketchIapProduct product))
            {
                Invoke(onResult, new SketchIapResult(productId, SketchIapResultStatus.Unavailable, message: "unknown_product"));
                return;
            }
            if (product.IsDurable && _entitlements.IsOwned(product.Id))
            {
                if (product.RemovesAds) SetAdsRemoved();
                Invoke(onResult, new SketchIapResult(productId, SketchIapResultStatus.AlreadyOwned));
                return;
            }
            if (_inflightId != null)
            {
                Invoke(onResult, new SketchIapResult(productId, SketchIapResultStatus.Busy, message: "purchase_in_progress"));
                return;
            }
            _inflightId = product.Id;
            _inflightPlacement = placement;
            _inflightCallback = onResult;
            try { _telemetry?.PurchaseStarted(product.Id, placement); }
            catch (Exception e) { Debug.LogException(e); }
            try { _provider.Purchase(product.Id); }
            catch (Exception e)
            {
                Debug.LogException(e);
                FailInflight(product.Id, SketchIapResultStatus.Failed, "provider_exception");
            }
        }

        /// <summary>Durable finished purchases come back as grants with isRestore = true (only those the local save lost).</summary>
        public void Restore(Action<bool> onDone = null)
        {
            if (_disposed || !_initialized) { Safe(onDone, false); return; }
            _provider.Restore((ok, error) =>
            {
                if (!ok) SketchLog.Warn("IAP restore failed: " + error);
                Safe(onDone, ok);
            });
        }

        /// <summary>Local durable ownership (non-consumables, subscriptions) from the entitlement store.</summary>
        public bool IsOwned(string productId) => !string.IsNullOrEmpty(productId) && _entitlements.IsOwned(productId);

        /// <summary>Store localized price, else the catalog fallback price, else empty.</summary>
        public string Price(string productId)
        {
            string localized = _disposed ? null : _provider.GetLocalizedPrice(productId);
            if (!string.IsNullOrWhiteSpace(localized)) return localized;
            return _catalog.TryGet(productId, out SketchIapProduct product) ? product.FallbackPrice : string.Empty;
        }

        /// <summary>Try the fulfillment again for paid orders that failed to grant (e.g. after the save became writable).</summary>
        public void RetryPending()
        {
            if (_disposed || _unfulfilled.Count == 0) return;
            _retryBuffer.Clear();
            _retryBuffer.AddRange(_unfulfilled.Values);
            for (int i = 0; i < _retryBuffer.Count; i++) HandleOrder(_retryBuffer[i]);
            _retryBuffer.Clear();
        }

        void OnOrderReceived(SketchStoreOrder order)
        {
            if (_disposed || order == null) return;
            HandleOrder(order);
        }

        void HandleOrder(SketchStoreOrder order)
        {
            if (!_catalog.TryGet(order.ProductId, out SketchIapProduct product))
            {
                SketchLog.Warn($"IAP order for unknown product '{order.ProductId}' left unconfirmed.");
                if (MatchesInflight(order)) FailInflight(order.ProductId, SketchIapResultStatus.Failed, "unknown_product");
                return;
            }
            switch (SketchStoreGrantPolicy.Decide(product, order, _entitlements))
            {
                case SketchStoreGrantDecision.Grant:
                    Grant(product, order);
                    return;
                case SketchStoreGrantDecision.ConfirmOnly:
                    _unfulfilled.Remove(order.TransactionId);
                    if (product.RemovesAds) SetAdsRemoved();
                    if (order.NeedsConfirmation) Confirm(order);
                    if (MatchesInflight(order))
                        CompleteInflight(new SketchIapResult(product.Id, product.IsDurable ? SketchIapResultStatus.AlreadyOwned : SketchIapResultStatus.Success, order.TransactionId));
                    return;
                case SketchStoreGrantDecision.Ignore:
                    if (product.RemovesAds && _entitlements.IsOwned(product.Id)) SetAdsRemoved();
                    return;
                default:
                    SketchLog.Warn($"IAP order for '{order.ProductId}' has no transaction id; left unconfirmed.");
                    if (MatchesInflight(order)) FailInflight(order.ProductId, SketchIapResultStatus.Failed, "invalid_order");
                    return;
            }
        }

        void Grant(SketchIapProduct product, SketchStoreOrder order)
        {
            bool inflight = MatchesInflight(order);
            bool granted = false;
            try { granted = _fulfill(new SketchIapGrant(product, order.TransactionId, order.Quantity, order.IsRestore)); }
            catch (Exception e) { Debug.LogException(e); }
            if (!granted)
            {
                _unfulfilled[order.TransactionId] = order;   // never confirmed: the store re-delivers it, or RetryPending grants it
                SketchLog.Warn($"IAP fulfillment for '{product.Id}' ({order.TransactionId}) failed; order kept unconfirmed.");
                if (inflight) FailInflight(product.Id, SketchIapResultStatus.Failed, "fulfillment_failed");
                return;
            }

            _entitlements.MarkTransactionGranted(order.TransactionId, product.Id);
            if (product.IsDurable) _entitlements.SetOwned(product.Id, true);
            _unfulfilled.Remove(order.TransactionId);
            if (product.RemovesAds) SetAdsRemoved();
            if (order.NeedsConfirmation) Confirm(order);

            if (!order.IsRestore)
            {
                try { _telemetry?.PurchaseSucceeded(ToPurchase(order, inflight ? _inflightPlacement : null)); }
                catch (Exception e) { Debug.LogException(e); }
            }
            try { Granted?.Invoke(product, order.TransactionId, order.IsRestore); }
            catch (Exception e) { Debug.LogException(e); }
            if (inflight) CompleteInflight(new SketchIapResult(product.Id, SketchIapResultStatus.Success, order.TransactionId));
        }

        void Confirm(SketchStoreOrder order)
        {
            try
            {
                if (!_provider.Confirm(order)) SketchLog.Info($"IAP order {order.TransactionId} was already confirmed or is unknown to the store.");
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        void OnPurchaseFailed(string productId, SketchIapResultStatus status, string message)
        {
            if (_disposed) return;
            if (_inflightId == null || (!string.IsNullOrEmpty(productId) && productId != _inflightId))
            {
                SketchLog.Info($"IAP failure for '{productId}' outside the active purchase: {message}");
                return;
            }
            if (status == SketchIapResultStatus.Success || status == SketchIapResultStatus.AlreadyOwned) status = SketchIapResultStatus.Failed;
            FailInflight(_inflightId, status, message);
        }

        void OnPurchaseDeferred(string productId)
        {
            if (_disposed || _inflightId == null || (!string.IsNullOrEmpty(productId) && productId != _inflightId)) return;
            CompleteInflight(new SketchIapResult(_inflightId, SketchIapResultStatus.Deferred, message: "awaiting_approval"));
        }

        bool MatchesInflight(SketchStoreOrder order) =>
            _inflightId != null && !order.IsRestore && string.Equals(_inflightId, order.ProductId, StringComparison.Ordinal);

        void FailInflight(string productId, SketchIapResultStatus status, string reason)
        {
            if (_inflightId == null) return;
            try { _telemetry?.PurchaseFailed(productId, reason, _inflightPlacement); }
            catch (Exception e) { Debug.LogException(e); }
            CompleteInflight(new SketchIapResult(productId, status, message: reason));
        }

        void CompleteInflight(SketchIapResult result)
        {
            Action<SketchIapResult> callback = _inflightCallback;
            _inflightId = null;
            _inflightPlacement = null;
            _inflightCallback = null;
            Invoke(callback, result);
        }

        void SyncAdsRemoved()
        {
            IReadOnlyList<SketchIapProduct> products = _catalog.Products;
            for (int i = 0; i < products.Count; i++)
            {
                SketchIapProduct product = products[i];
                if (product != null && product.RemovesAds && _entitlements.IsOwned(product.Id)) { SetAdsRemoved(); return; }
            }
        }

        void SetAdsRemoved()
        {
            if (_setAdsRemoved == null) return;
            try { _setAdsRemoved(true); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static SketchPurchase ToPurchase(SketchStoreOrder order, string placement) => new SketchPurchase
        {
            ProductId = order.ProductId,
            TransactionId = order.TransactionId,
            Price = order.Price,
            Currency = order.Currency,
            Store = order.Store,
            Receipt = order.Receipt,
            Placement = placement,
        };

        static void Invoke(Action<SketchIapResult> callback, SketchIapResult result)
        {
            if (callback == null) return;
            try { callback(result); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void Safe(Action<bool> callback, bool value)
        {
            if (callback == null) return;
            try { callback(value); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _provider.OrderReceived -= OnOrderReceived;
            _provider.PurchaseFailed -= OnPurchaseFailed;
            _provider.PurchaseDeferred -= OnPurchaseDeferred;
            if (_inflightId != null) CompleteInflight(new SketchIapResult(_inflightId, SketchIapResultStatus.Unavailable, message: "store_shutdown"));
            _unfulfilled.Clear();
            Granted = null;
        }
    }
}
