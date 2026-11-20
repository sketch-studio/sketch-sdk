using System;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>One catalog entry. Id is the game-side id; the store ids default to it when left empty.</summary>
    [Serializable]
    public sealed class SketchIapProduct
    {
        [SerializeField] string _id;
        [SerializeField] SketchIapProductType _type;
        [SerializeField] string _googlePlayId;
        [SerializeField] string _appleId;
        [Tooltip("Free tag the game reads in its fulfillment callback (e.g. \"gems:500\", \"bundle_starter\").")]
        [SerializeField] string _payoutTag;
        [Tooltip("Shown when the store has no localized price (offline, Editor).")]
        [SerializeField] string _fallbackPrice;
        [SerializeField] bool _removesAds;

        public SketchIapProduct() { }

        public SketchIapProduct(string id, SketchIapProductType type, string fallbackPrice = null, string payoutTag = null,
            bool removesAds = false, string googlePlayId = null, string appleId = null)
        {
            _id = id;
            _type = type;
            _fallbackPrice = fallbackPrice;
            _payoutTag = payoutTag;
            _removesAds = removesAds;
            _googlePlayId = googlePlayId;
            _appleId = appleId;
        }

        public string Id => _id;
        public SketchIapProductType Type => _type;
        public string GooglePlayId => string.IsNullOrWhiteSpace(_googlePlayId) ? _id : _googlePlayId;
        public string AppleId => string.IsNullOrWhiteSpace(_appleId) ? _id : _appleId;
        public string PayoutTag => _payoutTag ?? string.Empty;
        public string FallbackPrice => _fallbackPrice ?? string.Empty;
        public bool RemovesAds => _removesAds;
        /// <summary>Non-consumables and subscriptions are owned once bought and come back on restore.</summary>
        public bool IsDurable => _type != SketchIapProductType.Consumable;

        public string StoreIdFor(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.IPhonePlayer:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.tvOS:
                    return AppleId;
                case RuntimePlatform.Android:
                    return GooglePlayId;
                default:
                    return _id;
            }
        }
    }
}
