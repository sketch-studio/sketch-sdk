using System;

namespace Sketch.Sdk
{
    /// <summary>Provider-neutral local notification facade. Platform scheduling and persistence stay in adapters.</summary>
    public static class SketchNotifications
    {
        static ISketchNotificationsProvider _provider;
        public static bool IsInitialized { get; private set; }
        public static bool IsSupported => _provider != null;

        public static void Initialize(ISketchNotificationsProvider provider, Action<bool> onDone = null)
        {
            _provider = provider;
            IsInitialized = false;
            if (_provider == null) { onDone?.Invoke(false); return; }
            try
            {
                _provider.Initialize(ok =>
                {
                    IsInitialized = ok;
                    SketchAnalytics.Log("notifications_init", "success", ok);
                    onDone?.Invoke(ok);
                });
            }
            catch (Exception e)
            {
                SketchLog.Warn("Notifications initialization failed: " + e.Message);
                onDone?.Invoke(false);
            }
        }

        public static bool Schedule(SketchNotificationRequest request)
        {
            if (!IsInitialized || request == null || !request.IsValid) return false;
            try
            {
                bool scheduled = _provider.Schedule(request);
                if (scheduled) SketchAnalytics.Log("notification_scheduled", "notification_id", request.Id);
                return scheduled;
            }
            catch (Exception e) { SketchLog.Warn("Notification schedule failed: " + e.Message); return false; }
        }

        public static bool Cancel(string id)
        {
            if (!IsInitialized || string.IsNullOrWhiteSpace(id)) return false;
            try { return _provider.Cancel(id.Trim()); }
            catch (Exception e) { SketchLog.Warn("Notification cancel failed: " + e.Message); return false; }
        }

        public static void CancelAll()
        {
            if (!IsInitialized) return;
            try { _provider.CancelAll(); }
            catch (Exception e) { SketchLog.Warn("Notification cancel-all failed: " + e.Message); }
        }

        public static void ResetOnLaunch()
        {
            if (!IsInitialized) return;
            try { _provider.ResetOnLaunch(); }
            catch (Exception e) { SketchLog.Warn("Notification reset failed: " + e.Message); }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _provider = null; IsInitialized = false; }
    }
}
