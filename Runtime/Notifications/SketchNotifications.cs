using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Local re-engagement notifications. The game configures the set once and asks for permission at a happy moment
    /// (e.g. after the first win, never at boot). The SDK schedules the set when the app loses focus and cancels all
    /// (scheduled + shown) when it comes back, so a player who keeps playing never sees one.
    /// Remote kill switch: <see cref="SketchNotificationsRemoteKeys.ENABLED"/>. No provider (Editor fake aside) = no-op.
    /// <code>
    /// SketchNotifications.Configure(new[] { new SketchNotificationSpec("d1", "Your tower is back!", "Smash it again.", TimeSpan.FromHours(24), "icon_0") });
    /// // after the first win:
    /// SketchNotifications.RequestPermissionOnce(granted => { });
    /// </code>
    /// </summary>
    public static class SketchNotifications
    {
        const string ASKED_KEY = "sketchsdk.notif_permission_asked";

        static readonly List<SketchNotificationSpec> Specs = new List<SketchNotificationSpec>();
        static readonly List<SketchScheduledNotification> Buffer = new List<SketchScheduledNotification>();
        static ISketchNotificationProvider _provider;
        static ISketchKeyValueStore _store;
        static bool _hooked;
        static bool _requesting;
        static DateTime _lastActiveLocal;

        /// <summary>Local clock (injectable for tests / debug).</summary>
        public static Func<DateTime> Clock { get; set; } = DefaultClock;

        /// <summary>Quiet hours rule (default 22:00-08:00).</summary>
        public static SketchNotificationSchedule Schedule { get; set; } = new SketchNotificationSchedule();

        public static ISketchKeyValueStore Store
        {
            get => _store ?? (_store = new SketchPlayerPrefsStore());
            set => _store = value;
        }

        /// <summary>Registered by the MobileNotifications module on device; the Editor uses a logging fake.</summary>
        public static ISketchNotificationProvider Provider
        {
            get
            {
#if UNITY_EDITOR
                if (_provider == null) _provider = new SketchEditorNotificationProvider();
#endif
                return _provider;
            }
            set => _provider = value;
        }

        public static IReadOnlyList<SketchNotificationSpec> Configured => Specs;
        public static bool IsEnabled => SketchNotificationsRemoteKeys.IsEnabled(SketchRemoteConfig.Store);
        public static bool HasPermission => SafeHasPermission(Provider);
        public static bool PermissionAsked => Store.GetInt(ASKED_KEY, 0) == 1;

        /// <summary>Sets the re-engagement set (replaces the previous one) and starts the pause/resume hooks.</summary>
        public static void Configure(IReadOnlyList<SketchNotificationSpec> specs)
        {
            Specs.Clear();
            if (specs != null)
                for (int i = 0; i < specs.Count; i++)
                    if (specs[i] != null) Specs.Add(specs[i]);
            Hook();
            _lastActiveLocal = Now();
            CancelAll();   // the app is open: nothing should fire now
        }

        /// <summary>
        /// Asks for the notification permission once per install. done(granted) fires once: immediately when already
        /// granted (Android below 13), already asked before, or no provider; otherwise after the OS dialog.
        /// </summary>
        public static void RequestPermissionOnce(Action<bool> done)
        {
            ISketchNotificationProvider provider = Provider;
            if (provider == null) { done?.Invoke(false); return; }
            if (SafeHasPermission(provider)) { done?.Invoke(true); return; }
            if (PermissionAsked || _requesting) { done?.Invoke(false); return; }

            ISketchKeyValueStore store = Store;
            store.SetInt(ASKED_KEY, 1);
            store.Save();
            _requesting = true;
            SketchAds.SuppressNextAppOpen();   // the OS dialog takes focus; do not answer it with an app open ad
            try
            {
                provider.RequestPermission(granted => SketchSdkLoop.Run(() =>
                {
                    _requesting = false;
                    SketchAnalytics.Log("notif_permission", "granted", granted ? 1 : 0);
                    done?.Invoke(granted);
                }));
            }
            catch (Exception e)
            {
                _requesting = false;
                SketchLog.Warn("Notification permission request failed: " + e.Message);
                done?.Invoke(false);
            }
        }

        /// <summary>Schedules the configured set now (normally automatic when the app loses focus). Returns the count.</summary>
        public static int ScheduleReengagement()
        {
            ISketchNotificationProvider provider = Provider;
            if (provider == null) return 0;
            SafeCancel(provider);
            if (Specs.Count == 0 || !SafeHasPermission(provider)) return 0;
            DateTime now = Now();
            int count = Schedule.Build(now, _lastActiveLocal == default ? now : _lastActiveLocal, Specs, IsEnabled, Buffer);
            for (int i = 0; i < count; i++)
            {
                try { provider.Schedule(Buffer[i].Spec, Buffer[i].FireTime); }
                catch (Exception e) { SketchLog.Warn("Notification schedule failed: " + e.Message); }
            }
            Buffer.Clear();
            return count;
        }

        /// <summary>Cancels scheduled notifications and clears shown ones (automatic on resume).</summary>
        public static void CancelAll()
        {
            ISketchNotificationProvider provider = Provider;
            if (provider != null) SafeCancel(provider);
        }

        static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            Application.focusChanged -= OnFocusChanged;
            Application.focusChanged += OnFocusChanged;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        static void OnFocusChanged(bool focused)
        {
            if (focused)
            {
                _lastActiveLocal = Now();
                CancelAll();
                return;
            }
            _lastActiveLocal = Now();
            ScheduleReengagement();
        }

        static void OnQuitting()
        {
            _lastActiveLocal = Now();
            ScheduleReengagement();
        }

        static DateTime Now() => (Clock ?? DefaultClock)();
        static DateTime DefaultClock() => DateTime.Now;

        static bool SafeHasPermission(ISketchNotificationProvider provider)
        {
            if (provider == null) return false;
            try { return provider.HasPermission; }
            catch (Exception e) { SketchLog.Warn("Notification permission check failed: " + e.Message); return false; }
        }

        static void SafeCancel(ISketchNotificationProvider provider)
        {
            try { provider.CancelAll(); }
            catch (Exception e) { SketchLog.Warn("Notification cancel failed: " + e.Message); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Application.focusChanged -= OnFocusChanged;
            Application.quitting -= OnQuitting;
            _hooked = false;
            _requesting = false;
            _provider = null;
            _store = null;
            _lastActiveLocal = default;
            Clock = DefaultClock;
            Schedule = new SketchNotificationSchedule();
            Specs.Clear();
            Buffer.Clear();
        }
    }
}
