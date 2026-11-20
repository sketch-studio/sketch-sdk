using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Sketch.Sdk.Tests
{
    public sealed class SketchStoreTests
    {
        const string GEMS = "gems_100";
        const string NO_ADS = "remove_ads";
        const string VIP = "vip_pack";

        sealed class RecordingTelemetry : ISketchStoreTelemetry
        {
            public readonly List<string> Started = new List<string>();
            public readonly List<SketchPurchase> Succeeded = new List<SketchPurchase>();
            public readonly List<string> Failed = new List<string>();
            public void PurchaseStarted(string productId, string placement) => Started.Add(productId);
            public void PurchaseSucceeded(SketchPurchase purchase) => Succeeded.Add(purchase);
            public void PurchaseFailed(string productId, string reason, string placement) => Failed.Add(reason);
        }

        sealed class MemoryAdsRemovedStore : ISketchAdsRemovedStore
        {
            public bool Value;
            public int Saves;
            public bool Load() => Value;
            public void Save(bool removed) { Value = removed; Saves++; }
        }

        SketchIapCatalog _catalog;
        SketchEditorFakeStore _store;
        SketchMemoryEntitlementStore _entitlements;
        RecordingTelemetry _telemetry;
        SketchStoreService _service;
        List<SketchIapGrant> _grants;
        List<bool> _adsRemovedCalls;
        bool _fulfillResult;

        [SetUp]
        public void SetUp()
        {
            _catalog = SketchIapCatalog.Create(new[]
            {
                new SketchIapProduct(GEMS, SketchIapProductType.Consumable, "$0.99", "gems:100"),
                new SketchIapProduct(NO_ADS, SketchIapProductType.NonConsumable, "$2.99", removesAds: true),
                new SketchIapProduct(VIP, SketchIapProductType.NonConsumable, "$4.99"),
            });
            _store = new SketchEditorFakeStore(action => action());
            _entitlements = new SketchMemoryEntitlementStore();
            _telemetry = new RecordingTelemetry();
            _grants = new List<SketchIapGrant>();
            _adsRemovedCalls = new List<bool>();
            _fulfillResult = true;
            _service = CreateService(_entitlements);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            if (_catalog != null) Object.DestroyImmediate(_catalog);
        }

        SketchStoreService CreateService(ISketchEntitlementStore entitlements)
        {
            var service = new SketchStoreService(_catalog, _store, entitlements, grant =>
            {
                if (!_fulfillResult) return false;
                _grants.Add(grant);
                return true;
            }, _telemetry, removed => _adsRemovedCalls.Add(removed));
            bool ready = false;
            service.Initialize(ok => ready = ok);
            Assert.IsTrue(ready, "fake store must initialize synchronously");
            return service;
        }

        List<SketchIapResult> Buy(string productId)
        {
            var results = new List<SketchIapResult>();
            _service.Purchase(productId, results.Add, "shop");
            return results;
        }

        [Test]
        public void Purchase_Consumable_GrantsExactlyOnceAndConfirms()
        {
            List<SketchIapResult> results = Buy(GEMS);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(SketchIapResultStatus.Success, results[0].Status);
            Assert.AreEqual(1, _grants.Count);
            Assert.AreEqual(GEMS, _grants[0].Product.Id);
            Assert.AreEqual(1, _store.ConfirmCount);
            Assert.AreEqual(0, _store.UnfinishedCount);
            Assert.AreEqual(1, _telemetry.Started.Count);
            Assert.AreEqual(1, _telemetry.Succeeded.Count);
            Assert.AreEqual("shop", _telemetry.Succeeded[0].Placement);
        }

        [Test]
        public void RedeliveredTransaction_IsNotGrantedTwice()
        {
            List<SketchIapResult> results = Buy(GEMS);
            string transactionId = results[0].TransactionId;

            _store.SimulateRedelivery(transactionId);
            _store.SimulateRedelivery(transactionId);

            Assert.AreEqual(1, _grants.Count);
            Assert.AreEqual(1, _telemetry.Succeeded.Count);
        }

        [Test]
        public void FulfillmentFalse_LeavesOrderUnconfirmed_RedeliveryGrantsOnce()
        {
            _fulfillResult = false;
            List<SketchIapResult> results = Buy(GEMS);

            Assert.AreEqual(SketchIapResultStatus.Failed, results[0].Status);
            Assert.AreEqual(0, _grants.Count);
            Assert.AreEqual(0, _store.ConfirmCount);
            Assert.AreEqual(1, _store.UnfinishedCount);
            Assert.AreEqual(1, _service.UnfulfilledCount);

            _fulfillResult = true;
            _store.RedeliverUnfinished();
            _store.RedeliverUnfinished();

            Assert.AreEqual(1, _grants.Count);
            Assert.AreEqual(1, _store.ConfirmCount);
            Assert.AreEqual(0, _store.UnfinishedCount);
            Assert.AreEqual(0, _service.UnfulfilledCount);
        }

        [Test]
        public void RetryPending_GrantsFailedFulfillmentOnce()
        {
            _fulfillResult = false;
            Buy(GEMS);
            _fulfillResult = true;

            _service.RetryPending();
            _service.RetryPending();

            Assert.AreEqual(1, _grants.Count);
            Assert.AreEqual(1, _store.ConfirmCount);
        }

        [Test]
        public void Restore_RegrantsLostNonConsumableOnce()
        {
            Buy(VIP);
            Assert.AreEqual(1, _grants.Count);
            Assert.IsTrue(_service.IsOwned(VIP));

            // Reinstall: the local save is gone, the store still owns the product.
            _service.Dispose();
            _service = CreateService(new SketchMemoryEntitlementStore());
            Assert.IsFalse(_service.IsOwned(VIP));

            bool restored = false;
            _service.Restore(ok => restored = ok);
            _service.Restore(_ => { });

            Assert.IsTrue(restored);
            Assert.AreEqual(2, _grants.Count);
            Assert.IsTrue(_grants[1].IsRestore);
            Assert.IsTrue(_service.IsOwned(VIP));
            Assert.AreEqual(1, _telemetry.Succeeded.Count, "a restore is not new revenue");
        }

        [Test]
        public void Restore_DoesNotRegrantProductStillOwnedLocally()
        {
            Buy(VIP);
            _service.Restore();
            Assert.AreEqual(1, _grants.Count);
        }

        [Test]
        public void Purchase_OwnedNonConsumable_ReturnsAlreadyOwnedWithoutCharging()
        {
            Buy(VIP);
            List<SketchIapResult> second = Buy(VIP);

            Assert.AreEqual(SketchIapResultStatus.AlreadyOwned, second[0].Status);
            Assert.AreEqual(1, _grants.Count);
            Assert.AreEqual(1, _telemetry.Started.Count);
        }

        [Test]
        public void RemoveAdsProduct_SetsAdsRemoved()
        {
            Buy(NO_ADS);
            CollectionAssert.AreEqual(new[] { true }, _adsRemovedCalls);
        }

        [Test]
        public void RemoveAdsOwnedInSave_IsReappliedOnInitialize()
        {
            var entitlements = new SketchMemoryEntitlementStore();
            entitlements.SetOwned(NO_ADS, true);
            _service.Dispose();
            _adsRemovedCalls.Clear();

            _service = CreateService(entitlements);

            CollectionAssert.AreEqual(new[] { true }, _adsRemovedCalls);
        }

        [Test]
        public void DeferredThenSuccess_ReportsDeferredAndGrantsOnce()
        {
            var granted = new List<string>();
            _service.Granted += (product, transactionId, isRestore) => granted.Add(transactionId);
            _store.Outcome = SketchFakeStoreOutcome.DeferredThenSuccess;

            List<SketchIapResult> results = Buy(GEMS);

            Assert.AreEqual(1, results.Count, "the callback fires once");
            Assert.AreEqual(SketchIapResultStatus.Deferred, results[0].Status);
            Assert.AreEqual(1, _grants.Count);
            Assert.AreEqual(1, granted.Count);
            Assert.AreEqual(1, _store.ConfirmCount);
        }

        [TestCase(SketchFakeStoreOutcome.Fail, SketchIapResultStatus.Failed)]
        [TestCase(SketchFakeStoreOutcome.Cancel, SketchIapResultStatus.Cancelled)]
        public void FailedOrCancelledPurchase_GrantsNothing(SketchFakeStoreOutcome outcome, SketchIapResultStatus expected)
        {
            _store.Outcome = outcome;
            List<SketchIapResult> results = Buy(GEMS);

            Assert.AreEqual(expected, results[0].Status);
            Assert.AreEqual(0, _grants.Count);
            Assert.AreEqual(1, _telemetry.Failed.Count);
            Assert.IsFalse(_service.IsPurchasing);
        }

        [Test]
        public void Purchase_WhileAnotherIsPending_ReturnsBusy()
        {
            var queue = new List<System.Action>();
            _service.Dispose();
            _store = new SketchEditorFakeStore(queue.Add);
            var service = new SketchStoreService(_catalog, _store, _entitlements, g => { _grants.Add(g); return true; });
            service.Initialize();
            queue[0]();
            queue.Clear();
            _service = service;

            List<SketchIapResult> first = Buy(GEMS);
            List<SketchIapResult> second = Buy(GEMS);
            queue[0]();

            Assert.AreEqual(SketchIapResultStatus.Busy, second[0].Status);
            Assert.AreEqual(SketchIapResultStatus.Success, first[0].Status);
            Assert.AreEqual(1, _grants.Count);
        }

        [Test]
        public void Price_UsesLocalizedPriceElseCatalogFallback()
        {
            Assert.AreEqual("$0.99", _service.Price(GEMS));
            _store.SimulatePricesUnavailable = true;
            Assert.AreEqual("$0.99", _service.Price(GEMS), "fallback when the store has no localized price");
            Assert.AreEqual(string.Empty, _service.Price("unknown"));
        }

        [Test]
        public void Catalog_Validate_ReportsDuplicateIds()
        {
            SketchIapCatalog duplicate = SketchIapCatalog.Create(new[]
            {
                new SketchIapProduct("a", SketchIapProductType.Consumable),
                new SketchIapProduct("a", SketchIapProductType.NonConsumable),
            });
            try
            {
                var errors = new List<string>();
                Assert.IsFalse(duplicate.Validate(errors));
                Assert.AreEqual(1, errors.Count);
                Assert.IsTrue(_catalog.Validate());
            }
            finally { Object.DestroyImmediate(duplicate); }
        }

        [Test]
        public void GrantPolicy_ConsumableAlreadyGranted_ConfirmsOnly()
        {
            var entitlements = new SketchMemoryEntitlementStore();
            entitlements.MarkTransactionGranted("t1", GEMS);
            _catalog.TryGet(GEMS, out SketchIapProduct gems);
            var order = new SketchStoreOrder { ProductId = GEMS, TransactionId = "t1" };
            Assert.AreEqual(SketchStoreGrantDecision.ConfirmOnly, SketchStoreGrantPolicy.Decide(gems, order, entitlements));
            order.TransactionId = "";
            Assert.AreEqual(SketchStoreGrantDecision.Reject, SketchStoreGrantPolicy.Decide(gems, order, entitlements));
        }

        [Test]
        public void SketchAds_UseAdsRemovedStore_ReadsAndWritesInjectedStore()
        {
            var store = new MemoryAdsRemovedStore { Value = true };
            try
            {
                SketchAds.UseAdsRemovedStore(store);
                Assert.IsTrue(SketchAds.IsAdsRemoved);
                SketchAds.SetAdsRemoved(false);
                Assert.IsFalse(store.Value);
                Assert.AreEqual(1, store.Saves);
                Assert.IsFalse(SketchAds.IsAdsRemoved);
            }
            finally { SketchAds.UseAdsRemovedStore(null); }
        }
    }
}
