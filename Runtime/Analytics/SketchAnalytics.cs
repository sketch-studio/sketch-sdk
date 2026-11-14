using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Game-facing analytics: one call fans out to every registered sink (Sketch Server, Firebase, ...).
    /// <code>SketchAnalytics.Log("level_complete", "level", 3, "time", 41.5f);</code>
    /// </summary>
    public static class SketchAnalytics
    {
        const string LTV_KEY = "sketchsdk.ltv_usd";
        static readonly List<ISketchAnalyticsSink> Sinks = new List<ISketchAnalyticsSink>();
        static readonly List<float> Crossed = new List<float>();

        /// <summary>When false every call is dropped (e.g. the player opted out).</summary>
        public static bool Enabled { get; set; } = true;
        /// <summary>Lifetime revenue thresholds in USD (from settings).</summary>
        public static IList<float> RevenueMilestonesUsd { get; set; }
        /// <summary>Raised after every event, for debug panels and tests.</summary>
        public static event Action<string, IReadOnlyDictionary<string, object>> EventLogged;

        public static IReadOnlyList<ISketchAnalyticsSink> RegisteredSinks => Sinks;

        public static void AddSink(ISketchAnalyticsSink sink)
        {
            if (sink != null && !Sinks.Contains(sink)) Sinks.Add(sink);
        }

        public static void RemoveSink(ISketchAnalyticsSink sink) => Sinks.Remove(sink);

        /// <summary>Log an event with key/value pairs: Log("shop_open", "tab", "gems").</summary>
        public static void Log(string eventName, params object[] keyValues)
        {
            Dictionary<string, object> parameters = null;
            if (keyValues != null && keyValues.Length > 0)
            {
                parameters = new Dictionary<string, object>(keyValues.Length / 2, StringComparer.Ordinal);
                for (int i = 0; i + 1 < keyValues.Length; i += 2)
                {
                    string key = keyValues[i] as string ?? Convert.ToString(keyValues[i], CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(key)) parameters[SanitizeName(key)] = keyValues[i + 1];
                }
                if (keyValues.Length % 2 != 0) SketchLog.Warn($"Log({eventName}): odd key/value count, last key ignored.");
            }
            Log(eventName, parameters);
        }

        public static void Log(string eventName, IDictionary<string, object> parameters)
        {
            if (!Enabled) return;
            string name = SanitizeName(eventName);
            if (string.IsNullOrEmpty(name)) { SketchLog.Warn("Analytics event without a name dropped."); return; }
            IReadOnlyDictionary<string, object> safe = parameters == null
                ? (IReadOnlyDictionary<string, object>)EmptyParams
                : new Dictionary<string, object>(parameters, StringComparer.Ordinal);
            SketchSdkLoop.Run(() =>
            {
                for (int i = 0; i < Sinks.Count; i++)
                {
                    try { Sinks[i].LogEvent(name, safe); }
                    catch (Exception e) { SketchLog.Warn($"Sink {Sinks[i].GetType().Name} failed on {name}: {e.Message}"); }
                }
                try { EventLogged?.Invoke(name, safe); } catch (Exception e) { Debug.LogException(e); }
            });
        }

        /// <summary>Ad paid event (AdMob OnAdPaid). Called by the SDK; games normally do not call this.</summary>
        public static void LogAdRevenue(SketchAdRevenue revenue)
        {
            if (!Enabled || revenue == null) return;
            SketchSdkLoop.Run(() =>
            {
                for (int i = 0; i < Sinks.Count; i++)
                {
                    try { Sinks[i].LogAdRevenue(revenue); }
                    catch (Exception e) { SketchLog.Warn($"Sink {Sinks[i].GetType().Name} ad revenue failed: {e.Message}"); }
                }
                double usd = RevenueUsd(revenue);
                if (usd > 0) AddLifetimeRevenue(usd);
            });
        }

        /// <summary>A completed purchase (after the store confirmed it). Sends iap_success + the revenue row.</summary>
        public static void LogPurchase(SketchPurchase purchase)
        {
            if (!Enabled || purchase == null) return;
            SketchSdkLoop.Run(() =>
            {
                for (int i = 0; i < Sinks.Count; i++)
                {
                    try { Sinks[i].LogPurchase(purchase); }
                    catch (Exception e) { SketchLog.Warn($"Sink {Sinks[i].GetType().Name} purchase failed: {e.Message}"); }
                }
                double usd = purchase.PriceUsd > 0 ? purchase.PriceUsd
                    : string.Equals(purchase.Currency, "USD", StringComparison.OrdinalIgnoreCase) ? purchase.Price : 0;
                if (usd > 0) AddLifetimeRevenue(usd);
            });
        }

        /// <summary>USD value of a paid event, or 0 when the currency is not USD (no invented conversion).</summary>
        public static double RevenueUsd(SketchAdRevenue revenue)
        {
            if (revenue == null || revenue.ValueMicros <= 0) return 0;
            string currency = string.IsNullOrEmpty(revenue.Currency) ? "USD" : revenue.Currency;
            return string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ? revenue.ValueMicros / 1_000_000d : 0;
        }

        public static double LifetimeRevenueUsd => double.TryParse(PlayerPrefs.GetString(LTV_KEY, "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;

        static void AddLifetimeRevenue(double usd)
        {
            double before = LifetimeRevenueUsd;
            double after = before + usd;
            PlayerPrefs.SetString(LTV_KEY, after.ToString("R", CultureInfo.InvariantCulture));
            SketchRevenueMilestones.Crossed(before, after, RevenueMilestonesUsd, Crossed);
            for (int i = 0; i < Crossed.Count; i++)
                Log(SketchRevenueMilestones.EventName(Crossed[i]), "value", (double)(decimal)Crossed[i], "currency", "USD");
        }

        /// <summary>Server rule: letters, digits and '_', starting with a letter, max 80 chars.</summary>
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var b = new StringBuilder(Math.Min(name.Length, 80));
            for (int i = 0; i < name.Length && b.Length < 80; i++)
            {
                char c = name[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) c = '_';
                if (b.Length == 0 && !((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))) { b.Append('e'); if (b.Length >= 80) break; }
                b.Append(c);
            }
            return b.ToString();
        }

        static readonly Dictionary<string, object> EmptyParams = new Dictionary<string, object>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Sinks.Clear();
            EventLogged = null;
            Enabled = true;
            RevenueMilestonesUsd = null;
        }
    }
}
