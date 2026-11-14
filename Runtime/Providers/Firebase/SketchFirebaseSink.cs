#if SKETCHSDK_FIREBASE
using System;
using System.Collections.Generic;
using System.Globalization;
using Firebase;
using Firebase.Analytics;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Optional Firebase Analytics sink, active when the Firebase Analytics package is installed (define SKETCHSDK_FIREBASE).
    /// Events logged before Firebase is ready are buffered (max 200) and sent once dependencies resolve.
    /// Needs google-services.json / GoogleService-Info.plist in Assets (from the Firebase console).
    /// </summary>
    public sealed class SketchFirebaseSink : ISketchAnalyticsSink
    {
        const int MAX_BUFFER = 200;
        readonly List<Action> _pending = new List<Action>();
        readonly bool _logAdImpression;
        bool _ready;
        bool _failed;

        public SketchFirebaseSink(bool logAdImpression)
        {
            _logAdImpression = logAdImpression;
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
            {
                DependencyStatus status = task.IsFaulted || task.IsCanceled ? DependencyStatus.UnavailableOther : task.Result;
                SketchSdkLoop.Post(() => OnDependencies(status));
            });
        }

        void OnDependencies(DependencyStatus status)
        {
            if (status != DependencyStatus.Available)
            {
                _failed = true;
                _pending.Clear();
                SketchLog.Warn("Firebase unavailable: " + status + " (google-services.json missing?). Firebase analytics off.");
                return;
            }
            _ready = true;
            // Collection state is the game's decision (consent, kids apps); the SDK never turns it on.
            SketchLog.Info("Firebase Analytics ready.");
            foreach (Action action in _pending)
            {
                try { action(); } catch (Exception e) { SketchLog.Warn("Firebase log failed: " + e.Message); }
            }
            _pending.Clear();
        }

        void Run(Action action)
        {
            if (_failed || !SketchAnalytics.Enabled) return;
            if (_ready) { action(); return; }
            if (_pending.Count < MAX_BUFFER) _pending.Add(action);
        }

        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            string name = Trim(eventName, 40);
            Parameter[] firebaseParams = Convert(parameters);
            Run(() => FirebaseAnalytics.LogEvent(name, firebaseParams));
        }

        public void LogAdRevenue(SketchAdRevenue revenue)
        {
            if (!_logAdImpression || revenue == null) return;
            double value = revenue.ValueMicros / 1_000_000d;
            string currency = string.IsNullOrEmpty(revenue.Currency) ? "USD" : revenue.Currency;
            var parameters = new[]
            {
                new Parameter(FirebaseAnalytics.ParameterAdPlatform, "AdMob"),
                new Parameter(FirebaseAnalytics.ParameterAdSource, Trim(revenue.AdSource ?? "AdMob", 100)),
                new Parameter(FirebaseAnalytics.ParameterAdFormat, Trim(revenue.Format ?? string.Empty, 100)),
                new Parameter(FirebaseAnalytics.ParameterAdUnitName, Trim(revenue.AdUnitId ?? string.Empty, 100)),
                new Parameter(FirebaseAnalytics.ParameterCurrency, currency),
                new Parameter(FirebaseAnalytics.ParameterValue, value),
            };
            Run(() => FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventAdImpression, parameters));
        }

        public void LogPurchase(SketchPurchase purchase)
        {
            // Firebase collects in_app_purchase automatically from the store; log the funnel event only.
            LogEvent("iap_success", new Dictionary<string, object>
            {
                ["product_id"] = purchase.ProductId ?? string.Empty,
                ["value"] = purchase.Price,
                ["currency"] = purchase.Currency ?? string.Empty,
            });
        }

        static Parameter[] Convert(IReadOnlyDictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0) return Array.Empty<Parameter>();
            var result = new List<Parameter>(Math.Min(parameters.Count, 25));
            foreach (var pair in parameters)
            {
                if (result.Count >= 25) break;   // Firebase limit per event
                string key = Trim(pair.Key, 40);
                switch (pair.Value)
                {
                    case null: break;
                    case bool b: result.Add(new Parameter(key, b ? 1L : 0L)); break;
                    case int i: result.Add(new Parameter(key, i)); break;
                    case long l: result.Add(new Parameter(key, l)); break;
                    case short s: result.Add(new Parameter(key, s)); break;
                    case byte by: result.Add(new Parameter(key, by)); break;
                    case float f: result.Add(new Parameter(key, (double)f)); break;
                    case double d: result.Add(new Parameter(key, d)); break;
                    case decimal m: result.Add(new Parameter(key, (double)m)); break;
                    default: result.Add(new Parameter(key, Trim(System.Convert.ToString(pair.Value, CultureInfo.InvariantCulture), 100))); break;
                }
            }
            return result.ToArray();
        }

        static string Trim(string value, int max) => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value.Substring(0, max);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() =>
            SketchSdkModules.AddAnalyticsSink(settings => settings.FirebaseAnalytics ? new SketchFirebaseSink(settings.FirebaseLogAdImpression) : null);
    }
}
#endif
