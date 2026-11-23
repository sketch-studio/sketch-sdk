using System;
using System.Collections.Generic;

namespace Sketch.Sdk
{
    /// <summary>
    /// Pure analytics rules shared by every sink: Firebase limits (event name 40, 25 params, param name 40, string value 100,
    /// reserved prefixes) and the revenue parameter mapping (ad_impression, iap_success).
    /// </summary>
    public static class SketchAnalyticsSchema
    {
        public const int MAX_EVENT_NAME = 40;
        public const int MAX_PARAMS = 25;
        public const int MAX_PARAM_NAME = 40;
        public const int MAX_STRING_VALUE = 100;
        /// <summary>Remote-config kill switch for events (Bounce RemoteConfig.logEnable). Revenue is never gated.</summary>
        public const string REMOTE_LOG_ENABLE = "log_enable";

        static readonly string[] ReservedPrefixes = { "firebase_", "google_", "ga_" };

        /// <summary>Letters, digits, '_', starts with a letter, within maxLength, no reserved prefix.</summary>
        public static bool IsValidName(string name, int maxLength)
        {
            if (string.IsNullOrEmpty(name) || name.Length > maxLength) return false;
            char first = name[0];
            if (!((first >= 'a' && first <= 'z') || (first >= 'A' && first <= 'Z'))) return false;
            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) return false;
            }
            for (int i = 0; i < ReservedPrefixes.Length; i++)
                if (name.StartsWith(ReservedPrefixes[i], StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        public static bool IsValidEventName(string name) => IsValidName(name, MAX_EVENT_NAME);

        /// <summary>Adds one line per broken rule to issues (cleared first). True when the event is Firebase-safe.</summary>
        public static bool Validate(string eventName, IReadOnlyDictionary<string, object> parameters, List<string> issues)
        {
            issues.Clear();
            if (!IsValidEventName(eventName)) issues.Add("event name '" + eventName + "' breaks the Firebase rule (a-z0-9_, <= 40, no firebase_/google_/ga_)");
            if (parameters != null)
            {
                if (parameters.Count > MAX_PARAMS) issues.Add(eventName + ": " + parameters.Count + " params > " + MAX_PARAMS);
                foreach (var pair in parameters)
                {
                    if (!IsValidName(pair.Key, MAX_PARAM_NAME)) issues.Add(eventName + ": param name '" + pair.Key + "' invalid");
                    if (pair.Value is string s && s.Length > MAX_STRING_VALUE) issues.Add(eventName + "." + pair.Key + ": string value > " + MAX_STRING_VALUE + " chars");
                }
            }
            return issues.Count == 0;
        }

        /// <summary>
        /// Firebase ad_impression parameters for one paid event (GA4 ad revenue schema): ad_platform, ad_source, ad_format,
        /// ad_unit_name, currency, value (micros / 1e6) + placement. Pure so it is testable without the Firebase package.
        /// </summary>
        public static void AdImpressionParams(SketchAdRevenue revenue, List<KeyValuePair<string, object>> result)
        {
            result.Clear();
            if (revenue == null) return;
            result.Add(new KeyValuePair<string, object>("ad_platform", "AdMob"));
            result.Add(new KeyValuePair<string, object>("ad_source", Trim(string.IsNullOrEmpty(revenue.AdSource) ? "AdMob" : revenue.AdSource)));
            result.Add(new KeyValuePair<string, object>("ad_format", Trim(revenue.Format ?? string.Empty)));
            result.Add(new KeyValuePair<string, object>("ad_unit_name", Trim(revenue.AdUnitId ?? string.Empty)));
            result.Add(new KeyValuePair<string, object>("currency", string.IsNullOrEmpty(revenue.Currency) ? "USD" : revenue.Currency));
            result.Add(new KeyValuePair<string, object>("value", revenue.ValueMicros / 1_000_000d));
            result.Add(new KeyValuePair<string, object>("placement", Trim(revenue.Placement ?? string.Empty)));
        }

        /// <summary>iap_success parameters for a confirmed purchase: product_id, value (local price), currency, value_usd, placement.</summary>
        public static Dictionary<string, object> PurchaseParams(SketchPurchase purchase)
        {
            var p = new Dictionary<string, object>(5, StringComparer.Ordinal)
            {
                ["product_id"] = Trim(purchase?.ProductId ?? string.Empty),
                ["value"] = purchase?.Price ?? 0d,
                ["currency"] = purchase?.Currency ?? string.Empty,
            };
            if (purchase != null && purchase.PriceUsd > 0) p["value_usd"] = purchase.PriceUsd;
            if (!string.IsNullOrEmpty(purchase?.Placement)) p["placement"] = Trim(purchase.Placement);
            return p;
        }

        static string Trim(string value) => value.Length <= MAX_STRING_VALUE ? value : value.Substring(0, MAX_STRING_VALUE);
    }
}
