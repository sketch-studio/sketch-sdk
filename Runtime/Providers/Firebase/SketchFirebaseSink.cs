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
    public sealed class SketchFirebaseSink : ISketchAnalyticsSink, ISketchRoutedSink
    {
        const int MAX_BUFFER = 200;
        readonly List<Action> _pending = new List<Action>();
        readonly bool _logAdImpression;
        bool _ready;
        bool _failed;

        /// <summary>Firebase gets revenue only (ad_impression, iap_success, reach_rev_*): owner OR-806/808.</summary>
        public SketchAnalyticsRoute Route => SketchAnalyticsRoute.Firebase;

        public SketchFirebaseSink(bool logAdImpression)
        {
            _logAdImpression = logAdImpression;
            SketchFirebaseDependencies.Once(FirebaseApp.CheckAndFixDependenciesAsync).ContinueWith(task =>
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
            ApplyAgeConsent();
            SketchAudience.Changed -= ApplyAgeConsent;
            SketchAudience.Changed += ApplyAgeConsent;
            SketchSdkApp.WhenReady(ApplyAgeConsent);
            foreach (Action action in _pending)
            {
                try { action(); } catch (Exception e) { SketchLog.Warn("Firebase log failed: " + e.Message); }
            }
            _pending.Clear();
        }

        /// <summary>
        /// Consent Mode v2 for minors (and an unknown age under Require Age Signal): ad_user_data and ad_personalization are
        /// denied so Analytics never feeds ad personalization. Adults keep the defaults (UMP / TCF decide in the EEA).
        /// </summary>
        static void ApplyAgeConsent()
        {
            SketchSdkSettings settings = SketchSdkApp.Settings;
            if (settings == null || !settings.RequireAgeSignal) return;
            bool adult = SketchAudience.Signals(settings).AdPersonalizationAllowed;
            if (adult)
            {
                // Re-grant only after consent is gathered and only where no consent regulation applies (no privacy
                // options form); in the EEA/UK/CH the TCF string from UMP stays the authority and the denial is kept.
                if (!SketchConsent.IsGathered || SketchConsent.IsPrivacyOptionsRequired) return;
            }
            ConsentStatus status = adult ? ConsentStatus.Granted : ConsentStatus.Denied;
            try
            {
                FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
                {
                    { ConsentType.AdUserData, status },
                    { ConsentType.AdPersonalization, status },
                });
                FirebaseAnalytics.SetUserProperty(FirebaseAnalytics.UserPropertyAllowAdPersonalizationSignals, adult ? "true" : "false");
                SketchLog.Info("Firebase ad consent for age: " + status);
            }
            catch (Exception e) { SketchLog.Warn("Firebase age consent failed: " + e.Message); }
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
            var pairs = new List<KeyValuePair<string, object>>(8);
            SketchAnalyticsSchema.AdImpressionParams(revenue, pairs);
            var parameters = new Parameter[pairs.Count];
            for (int i = 0; i < pairs.Count; i++)
                parameters[i] = pairs[i].Value is double d ? new Parameter(pairs[i].Key, d) : new Parameter(pairs[i].Key, (string)pairs[i].Value);
            Run(() => FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventAdImpression, parameters));
        }

        public void LogPurchase(SketchPurchase purchase)
        {
            // Bounce logs iap_success to Firebase after validation (BounceSdk.cs LogIAPEvent); the store revenue itself is the
            // automatic in_app_purchase event, so iap_success carries value/currency without being a GA4 revenue event.
            if (purchase == null) return;
            LogEvent("iap_success", SketchAnalyticsSchema.PurchaseParams(purchase));
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
