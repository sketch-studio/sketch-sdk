using System;
using System.Globalization;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Native in-app review behind a launch/win gate (<see cref="SketchReviewGate"/>). The game calls
    /// <see cref="RecordWin"/> on each win and <see cref="TryRequest"/> at a happy moment; the SDK decides whether it is
    /// allowed, runs the native flow and falls back to <see cref="SketchRating.OpenStorePage"/> when native fails.
    /// The game's own "Enjoying the game?" popup (Good/Bad flow) stays game-owned; call TryRequest from its Good branch.
    /// <code>
    /// SketchReview.RecordWin();
    /// SketchReview.TryRequest("level_win", shown => { });
    /// </code>
    /// </summary>
    public static class SketchReview
    {
        const string WINS_KEY = "sketchsdk.review_wins";
        const string REQUESTS_KEY = "sketchsdk.review_requests";
        const string LAST_UNIX_KEY = "sketchsdk.review_last_unix";
        const string LAST_VERSION_KEY = "sketchsdk.review_last_version";
        const string NEVER_KEY = "sketchsdk.review_never";

        static ISketchReviewProvider _provider;
        static ISketchKeyValueStore _store;
        static bool _busy;

        public static SketchReviewGateSettings Settings { get; set; } = new SketchReviewGateSettings();

        /// <summary>Unix seconds (injectable for tests / debug).</summary>
        public static Func<double> ClockUnix { get; set; } = DefaultClock;

        /// <summary>Session number source; defaults to SketchIdentity.Session.</summary>
        public static Func<int> SessionSource { get; set; } = DefaultSessions;

        public static ISketchKeyValueStore Store
        {
            get => _store ?? (_store = new SketchPlayerPrefsStore());
            set => _store = value;
        }

        /// <summary>Registered by the PlayReview module on Android; the Editor uses a logging fake. Null = store page.</summary>
        public static ISketchReviewProvider Provider
        {
            get
            {
#if UNITY_EDITOR
                if (_provider == null) _provider = new SketchEditorReviewProvider();
#endif
                return _provider;
            }
            set => _provider = value;
        }

        public static bool IsBusy => _busy;

        public static SketchReviewState State
        {
            get
            {
                ISketchKeyValueStore store = Store;
                double.TryParse(store.GetString(LAST_UNIX_KEY, "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out double last);
                return new SketchReviewState
                {
                    Wins = store.GetInt(WINS_KEY, 0),
                    Requests = store.GetInt(REQUESTS_KEY, 0),
                    LastRequestUnix = last,
                    LastRequestVersion = store.GetString(LAST_VERSION_KEY, string.Empty),
                    NeverAgain = store.GetInt(NEVER_KEY, 0) == 1,
                };
            }
        }

        public static void RecordWin()
        {
            ISketchKeyValueStore store = Store;
            store.SetInt(WINS_KEY, store.GetInt(WINS_KEY, 0) + 1);
            store.Save();
        }

        /// <summary>The player said "never" (e.g. in the game's own rate popup): no more requests on this install.</summary>
        public static void NeverAskAgain()
        {
            ISketchKeyValueStore store = Store;
            store.SetInt(NEVER_KEY, 1);
            store.Save();
        }

        public static bool CanRequest(out string blockReason) =>
            SketchReviewGate.CanRequest(Settings, State, Sessions(), Application.version, Now(), out blockReason);

        /// <summary>
        /// Requests a review when the gate allows it. Returns false (and done(false)) when blocked.
        /// done(true) when the native flow completed or the store page was opened as fallback.
        /// </summary>
        public static bool TryRequest(string moment, Action<bool> done)
        {
            moment = string.IsNullOrEmpty(moment) ? "review" : moment;
            if (_busy) { done?.Invoke(false); return false; }
            if (!CanRequest(out string reason))
            {
                SketchLog.Info($"Review '{moment}' blocked: {reason}.");
                done?.Invoke(false);
                return false;
            }

            MarkRequested();
            SketchAnalytics.Log("review_request", "moment", moment);
            ISketchReviewProvider provider = Provider;
            if (provider == null) { Fallback(moment, done); return true; }

            _busy = true;
            SketchAds.SuppressNextAppOpen();   // the review sheet takes focus
            try
            {
                provider.RequestReview(ok => SketchSdkLoop.Run(() =>
                {
                    _busy = false;
                    if (ok)
                    {
                        SketchAnalytics.Log("review_native_done", "moment", moment);
                        done?.Invoke(true);
                    }
                    else Fallback(moment, done);
                }));
            }
            catch (Exception e)
            {
                _busy = false;
                SketchLog.Warn("Native review failed: " + e.Message);
                Fallback(moment, done);
            }
            return true;
        }

        static void MarkRequested()
        {
            ISketchKeyValueStore store = Store;
            store.SetInt(REQUESTS_KEY, store.GetInt(REQUESTS_KEY, 0) + 1);
            store.SetString(LAST_UNIX_KEY, Now().ToString("R", CultureInfo.InvariantCulture));
            store.SetString(LAST_VERSION_KEY, Application.version ?? string.Empty);
            store.Save();   // saved before the flow: a crash inside it never re-asks in this version
        }

        static void Fallback(string moment, Action<bool> done)
        {
            if (!Settings.StoreFallback) { done?.Invoke(false); return; }
            SketchRating.OpenStorePage(moment);
            done?.Invoke(true);
        }

        static double Now() => (ClockUnix ?? DefaultClock)();
        static int Sessions() => (SessionSource ?? DefaultSessions)();
        static double DefaultClock() => SketchIdentity.NowUnix;
        static int DefaultSessions() => SketchIdentity.Session;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _provider = null;
            _store = null;
            _busy = false;
            Settings = new SketchReviewGateSettings();
            ClockUnix = DefaultClock;
            SessionSource = DefaultSessions;
        }
    }
}
