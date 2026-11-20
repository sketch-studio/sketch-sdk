using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// The IAP API a game calls for every purchase. The game supplies the catalog, a durable entitlement store (its save)
    /// and the fulfillment that grants items; the SDK runs the store, dedup, confirmation, restore, remove-ads and telemetry.
    /// <code>
    /// SketchStore.Init(catalog, myEntitlements, grant => myShop.GrantDurably(grant));
    /// SketchStore.Purchase("gems_500", r => { if (r.IsSuccess) ShowThanks(); }, "shop");
    /// </code>
    /// In the Editor the fake store is used (SketchStoreProviders.UseRealStoreInEditor to opt out).
    /// </summary>
    public static class SketchStore
    {
        static SketchStoreService _service;
        static ISketchStoreProvider _provider;

        /// <summary>(product, transactionId, isRestore) for every grant, including deferred and re-delivered orders.</summary>
        public static event Action<SketchIapProduct, string, bool> Granted;

        public static bool IsInitialized => _service != null && _service.IsInitialized;
        public static bool IsReady => _service != null && _service.IsReady;
        public static bool IsPurchasing => _service != null && _service.IsPurchasing;
        /// <summary>The active provider (cast to SketchEditorFakeStore in the Editor to drive test outcomes).</summary>
        public static ISketchStoreProvider Provider => _provider;
        public static SketchStoreService Service => _service;

        /// <param name="fulfill">Grant the product durably and return true; return false to keep the order unconfirmed for a retry.</param>
        /// <param name="provider">null = Editor fake in the Editor, Unity IAP (SketchSdk.UnityIap) on devices.</param>
        /// <param name="telemetry">null = SketchIap funnel (iap_start / iap_success / iap_failed).</param>
        public static void Init(SketchIapCatalog catalog, ISketchEntitlementStore entitlements, Func<SketchIapGrant, bool> fulfill,
            Action<bool> onReady = null, ISketchStoreProvider provider = null, ISketchStoreTelemetry telemetry = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (entitlements == null) throw new ArgumentNullException(nameof(entitlements));
            if (fulfill == null) throw new ArgumentNullException(nameof(fulfill));
            var errors = new List<string>();
            if (!catalog.Validate(errors)) SketchLog.Warn($"IAP catalog '{catalog.name}': " + string.Join(" ", errors));

            Shutdown();
            _provider = provider ?? SketchStoreProviders.Create();
            if (_provider == null)
            {
                SketchLog.Warn("No IAP store provider: install com.unity.purchasing 5.5+ (assembly SketchSdk.UnityIap). Purchases are unavailable.");
                if (onReady != null) onReady(false);
                return;
            }
            _service = new SketchStoreService(catalog, _provider, entitlements, fulfill, telemetry ?? new SketchIapStoreTelemetry(), ApplyAdsRemoved);
            _service.Granted += RaiseGranted;
            _service.Initialize(onReady);
        }

        public static void Purchase(string productId, Action<SketchIapResult> onResult, string placement = null)
        {
            if (_service == null)
            {
                if (onResult == null) return;
                try { onResult(new SketchIapResult(productId, SketchIapResultStatus.Unavailable, message: "store_not_initialized")); }
                catch (Exception e) { Debug.LogException(e); }
                return;
            }
            _service.Purchase(productId, onResult, placement);
        }

        /// <summary>Restore button (required on iOS). Lost durable products come back through Granted with isRestore = true.</summary>
        public static void Restore(Action<bool> onDone = null)
        {
            if (_service == null) { onDone?.Invoke(false); return; }
            SketchAds.SuppressNextAppOpen();   // the store sign-in sheet must not trigger an app-open ad on return
            _service.Restore(onDone);
        }

        public static bool IsOwned(string productId) => _service != null && _service.IsOwned(productId);

        /// <summary>Localized store price, else the catalog fallback price.</summary>
        public static string Price(string productId) => _service != null ? _service.Price(productId) : string.Empty;

        public static void RetryPending() => _service?.RetryPending();

        public static void Shutdown()
        {
            if (_service != null)
            {
                _service.Granted -= RaiseGranted;
                _service.Dispose();
                _service = null;
            }
            if (_provider is IDisposable disposable)
            {
                try { disposable.Dispose(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            _provider = null;
        }

        static void ApplyAdsRemoved(bool removed)
        {
            if (SketchAds.IsAdsRemoved != removed) SketchAds.SetAdsRemoved(removed);
        }

        static void RaiseGranted(SketchIapProduct product, string transactionId, bool isRestore)
        {
            try { Granted?.Invoke(product, transactionId, isRestore); }
            catch (Exception e) { Debug.LogException(e); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _service = null;
            _provider = null;
            Granted = null;
        }
    }
}
