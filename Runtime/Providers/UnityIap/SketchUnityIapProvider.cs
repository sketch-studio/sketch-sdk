#if SKETCHSDK_UNITY_IAP
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Sketch.Sdk
{
    /// <summary>
    /// ISketchStoreProvider on Unity IAP 5.5 (PurchasingController). Pending orders are reported to SketchStoreService and
    /// confirmed only when it calls Confirm after the game's durable grant; unconfirmed orders are re-delivered on the next
    /// launch (ProcessPendingOrdersOnPurchasesFetched). Finished durable purchases from FetchPurchases / RestoreTransactions
    /// are reported as restore orders so a reinstall gets them back.
    /// </summary>
    public sealed class SketchUnityIapProvider : ISketchStoreProvider, IDisposable
    {
        readonly Dictionary<string, Product> _products = new Dictionary<string, Product>(StringComparer.Ordinal);
        readonly Dictionary<string, PendingOrder> _pending = new Dictionary<string, PendingOrder>(StringComparer.Ordinal);
        readonly HashSet<string> _owned = new HashSet<string>(StringComparer.Ordinal);
        PurchasingController _controller;
        SketchIapCatalog _catalog;
        string _storeName;
        Action<bool> _onInitialized;
        Action<bool, string> _onRestored;
        bool _disposed;

        public bool IsReady { get; private set; }

        public event Action<SketchStoreOrder> OrderReceived;
        public event Action<string, SketchIapResultStatus, string> PurchaseFailed;
        public event Action<string> PurchaseDeferred;

        public void Initialize(SketchIapCatalog catalog, Action<bool> onInitialized)
        {
            if (_controller != null) { SketchLog.Warn("Unity IAP provider is already initialized."); onInitialized?.Invoke(IsReady); return; }
            _catalog = catalog;
            _onInitialized = onInitialized;
            if (catalog == null) { FinishInitialize(false, "catalog missing"); return; }
            try
            {
                _storeName = UnityIAPServices.GetDefaultStore();
                if (string.IsNullOrWhiteSpace(_storeName)) { FinishInitialize(false, "no default store"); return; }
                _controller = new PurchasingController(_storeName);
                Subscribe(_controller);
                _ = ConnectAndFetchAsync(_controller, _storeName, catalog);
            }
            catch (Exception e) { FinishInitialize(false, e.Message); }
        }

        public void Purchase(string productId)
        {
            if (_disposed || !IsReady || _controller == null) { RaiseFailed(productId, SketchIapResultStatus.Unavailable, "store_not_ready"); return; }
            if (!_products.TryGetValue(productId ?? string.Empty, out Product product) || !product.availableToPurchase)
            {
                RaiseFailed(productId, SketchIapResultStatus.Unavailable, "product_unavailable");
                return;
            }
            if (_controller.GetConnectionState(_storeName) != ConnectionState.Connected)
            {
                RaiseFailed(productId, SketchIapResultStatus.Unavailable, "store_disconnected");
                return;
            }
            try { _controller.PurchaseProduct(product); }
            catch (Exception e) { RaiseFailed(productId, SketchIapResultStatus.Failed, "purchase_exception: " + e.Message); }
        }

        public bool Confirm(SketchStoreOrder order)
        {
            if (_disposed || _controller == null || order == null || string.IsNullOrEmpty(order.TransactionId)
                || !_pending.TryGetValue(order.TransactionId, out PendingOrder pending)) return false;
            try
            {
                _controller.ConfirmPurchase(pending);   // OnPurchaseConfirmed removes it from _pending
                return true;
            }
            catch (Exception e)
            {
                SketchLog.Warn($"Unity IAP confirm failed for {order.TransactionId}; it stays pending: {e.Message}");
                return false;
            }
        }

        public void Restore(Action<bool, string> onDone)
        {
            if (_disposed || _controller == null || !IsReady) { onDone?.Invoke(false, "store_not_ready"); return; }
            _onRestored = onDone;
            try
            {
                _controller.RestoreTransactions((store, ok, error) =>
                {
                    if (_disposed || !string.Equals(store, _storeName, StringComparison.Ordinal)) return;
                    _controller?.FetchPurchases(_storeName);   // durable history arrives through OnPurchasesFetched
                    Action<bool, string> callback = _onRestored;
                    _onRestored = null;
                    callback?.Invoke(ok, error);
                }, _storeName);
            }
            catch (Exception e)
            {
                _onRestored = null;
                onDone?.Invoke(false, e.Message);
            }
        }

        public bool IsOwned(string productId) => !string.IsNullOrEmpty(productId) && _owned.Contains(productId);

        public string GetLocalizedPrice(string productId)
        {
            if (string.IsNullOrEmpty(productId) || !_products.TryGetValue(productId, out Product product)) return null;
            string price = product.metadata != null ? product.metadata.localizedPriceString : null;
            return string.IsNullOrWhiteSpace(price) ? null : price;
        }

        async Task ConnectAndFetchAsync(PurchasingController controller, string storeName, SketchIapCatalog catalog)
        {
            try
            {
                await controller.Connect(storeName);
                if (_disposed || !ReferenceEquals(controller, _controller)) return;
                var definitions = new List<ProductDefinition>(catalog.Products.Count);
                for (int i = 0; i < catalog.Products.Count; i++)
                {
                    SketchIapProduct product = catalog.Products[i];
                    if (product == null || string.IsNullOrWhiteSpace(product.Id)) continue;
                    definitions.Add(new ProductDefinition(product.Id, product.StoreIdFor(Application.platform), ToUnity(product.Type)));
                }
                controller.ProcessPendingOrdersOnPurchasesFetched(true, storeName);
                controller.FetchProductsWithNoRetries(definitions, storeName);
            }
            catch (Exception e)
            {
                if (!_disposed) FinishInitialize(false, "connect/fetch failed: " + e.Message);
            }
        }

        void OnProductsFetched(string storeName, List<Product> products)
        {
            if (!IsOurs(storeName)) return;
            _products.Clear();
            if (products != null)
            {
                for (int i = 0; i < products.Count; i++)
                {
                    Product product = products[i];
                    if (product == null || string.IsNullOrEmpty(product.uSku) || _catalog == null || !_catalog.TryGet(product.uSku, out _)) continue;
                    _products[product.uSku] = product;
                }
            }
            IsReady = true;
            FinishInitialize(true, null);
            _controller?.FetchPurchases(_storeName);   // unfinished orders + durable history
        }

        void OnProductsFetchFailed(string storeName, ProductFetchFailed failure)
        {
            if (!IsOurs(storeName)) return;
            FinishInitialize(false, "product fetch failed: " + (failure != null ? failure.FailureReason.ToString() : "unknown"));
        }

        void OnPurchasesFetched(string storeName, Orders orders)
        {
            if (!IsOurs(storeName) || orders == null || orders.ConfirmedOrders == null) return;
            for (int i = 0; i < orders.ConfirmedOrders.Count; i++)
            {
                ConfirmedOrder confirmed = orders.ConfirmedOrders[i];
                if (!TrySingleItem(confirmed, out CartItem item)) continue;
                if (item.Product.type != ProductType.NonConsumable && item.Product.type != ProductType.Subscription) continue;
                if (string.IsNullOrWhiteSpace(confirmed.Info?.TransactionID)) continue;
                _owned.Add(item.Product.uSku);
                SketchStoreOrder order = ToOrder(confirmed, item);
                order.IsRestore = true;
                order.NeedsConfirmation = false;
                RaiseOrder(order);
            }
        }

        void OnPurchasesFetchFailed(string storeName, PurchasesFetchFailureDescription failure)
        {
            if (!IsOurs(storeName)) return;
            SketchLog.Warn("Unity IAP purchases fetch failed: " + (failure != null ? failure.FailureReason + ": " + failure.Message : "unknown"));
        }

        void OnPurchasePending(string storeName, PendingOrder pending)
        {
            if (!IsOurs(storeName)) return;
            string transactionId = pending?.Info?.TransactionID;
            if (string.IsNullOrWhiteSpace(transactionId) || !TrySingleItem(pending, out CartItem item))
            {
                SketchLog.Warn("Unity IAP pending order without a transaction id or with several lines; left unconfirmed.");
                return;
            }
            _pending[transactionId] = pending;
            SketchStoreOrder order = ToOrder(pending, item);
            order.IsRestore = item.Product.appleProductIsRestored;
            order.NeedsConfirmation = true;
            RaiseOrder(order);
        }

        void OnPurchaseConfirmed(string storeName, Order order)
        {
            if (!IsOurs(storeName) || order == null) return;
            string transactionId = order.Info?.TransactionID;
            if (order is FailedOrder failed)
            {
                SketchLog.Warn($"Unity IAP confirmation failed for {transactionId}; it stays pending: {failed.FailureReason}: {failed.Details}");
                return;
            }
            if (!string.IsNullOrEmpty(transactionId)) _pending.Remove(transactionId);
            if (TrySingleItem(order, out CartItem item)
                && (item.Product.type == ProductType.NonConsumable || item.Product.type == ProductType.Subscription))
                _owned.Add(item.Product.uSku);
        }

        void OnPurchaseFailed(string storeName, FailedOrder order)
        {
            if (!IsOurs(storeName)) return;
            string productId = TrySingleItem(order, out CartItem item) ? item.Product.uSku : null;
            PurchaseFailureReason reason = order != null ? order.FailureReason : PurchaseFailureReason.Unknown;
            SketchIapResultStatus status = reason == PurchaseFailureReason.UserCancelled ? SketchIapResultStatus.Cancelled
                : reason == PurchaseFailureReason.ProductUnavailable || reason == PurchaseFailureReason.PurchasingUnavailable ? SketchIapResultStatus.Unavailable
                : SketchIapResultStatus.Failed;
            RaiseFailed(productId, status, reason + (order != null && !string.IsNullOrEmpty(order.Details) ? ": " + order.Details : string.Empty));
        }

        void OnPurchaseDeferred(string storeName, DeferredOrder order)
        {
            if (!IsOurs(storeName)) return;
            string productId = TrySingleItem(order, out CartItem item) ? item.Product.uSku : null;
            try { PurchaseDeferred?.Invoke(productId); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void OnStoreDisconnected(string storeName, StoreConnectionFailureDescription failure)
        {
            if (!IsOurs(storeName)) return;
            IsReady = false;
            SketchLog.Warn("Unity IAP store disconnected: " + (failure != null ? failure.message : "unknown"));
            FinishInitialize(false, "disconnected");
        }

        void OnStoreConnected(string storeName)
        {
            if (!IsOurs(storeName) || _products.Count == 0) return;
            IsReady = true;   // reconnected after a disconnect; products were fetched before
        }

        SketchStoreOrder ToOrder(Order order, CartItem item)
        {
            ProductMetadata metadata = item.Product.metadata;
            return new SketchStoreOrder
            {
                ProductId = item.Product.uSku,
                TransactionId = order.Info.TransactionID,
                Quantity = item.Quantity < 1 ? 1 : item.Quantity,
                Price = metadata != null ? (double)metadata.localizedPrice : 0,
                Currency = metadata != null ? metadata.isoCurrencyCode : null,
                Store = _storeName,
                Receipt = order.Info.Receipt,
            };
        }

        static bool TrySingleItem(Order order, out CartItem item)
        {
            item = null;
            IReadOnlyList<CartItem> items = order?.CartOrdered?.Items();
            if (items == null || items.Count != 1 || items[0]?.Product == null || string.IsNullOrEmpty(items[0].Product.uSku)) return false;
            item = items[0];
            return true;
        }

        static ProductType ToUnity(SketchIapProductType type)
        {
            switch (type)
            {
                case SketchIapProductType.NonConsumable: return ProductType.NonConsumable;
                case SketchIapProductType.Subscription: return ProductType.Subscription;
                default: return ProductType.Consumable;
            }
        }

        bool IsOurs(string storeName) => !_disposed && string.Equals(storeName, _storeName, StringComparison.Ordinal);

        void FinishInitialize(bool ok, string error)
        {
            if (!ok) SketchLog.Warn("Unity IAP initialize failed: " + error);
            Action<bool> callback = _onInitialized;
            _onInitialized = null;
            if (callback == null) return;
            try { callback(ok); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void RaiseOrder(SketchStoreOrder order)
        {
            try { OrderReceived?.Invoke(order); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void RaiseFailed(string productId, SketchIapResultStatus status, string message)
        {
            try { PurchaseFailed?.Invoke(productId, status, message); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void Subscribe(PurchasingController controller)
        {
            controller.OnStoreConnected += OnStoreConnected;
            controller.OnStoreDisconnected += OnStoreDisconnected;
            controller.OnProductsFetched += OnProductsFetched;
            controller.OnProductsFetchFailed += OnProductsFetchFailed;
            controller.OnPurchasesFetched += OnPurchasesFetched;
            controller.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
            controller.OnPurchasePending += OnPurchasePending;
            controller.OnPurchaseConfirmed += OnPurchaseConfirmed;
            controller.OnPurchaseFailed += OnPurchaseFailed;
            controller.OnPurchaseDeferred += OnPurchaseDeferred;
        }

        void Unsubscribe(PurchasingController controller)
        {
            controller.OnStoreConnected -= OnStoreConnected;
            controller.OnStoreDisconnected -= OnStoreDisconnected;
            controller.OnProductsFetched -= OnProductsFetched;
            controller.OnProductsFetchFailed -= OnProductsFetchFailed;
            controller.OnPurchasesFetched -= OnPurchasesFetched;
            controller.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
            controller.OnPurchasePending -= OnPurchasePending;
            controller.OnPurchaseConfirmed -= OnPurchaseConfirmed;
            controller.OnPurchaseFailed -= OnPurchaseFailed;
            controller.OnPurchaseDeferred -= OnPurchaseDeferred;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_controller != null) Unsubscribe(_controller);
            _controller = null;
            _catalog = null;
            _onInitialized = null;
            _onRestored = null;
            IsReady = false;
            _products.Clear();
            _pending.Clear();
            _owned.Clear();
        }
    }
}
#endif
