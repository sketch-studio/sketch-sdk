#if SKETCHSDK_NOTIFICATIONS && UNITY_IOS
using System;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// iOS placeholder: everything is a no-op and permission is reported as not granted.
    /// TODO(iOS): Unity.Notifications.iOS AuthorizationRequest (Alert | Badge | Sound) for RequestPermission,
    /// iOSNotificationCenter.ScheduleNotification with an iOSNotificationTimeIntervalTrigger / calendar trigger for Schedule,
    /// RemoveAllScheduledNotifications + RemoveAllDeliveredNotifications for CancelAll.
    /// </summary>
    public sealed class SketchIosNotificationProvider : ISketchNotificationProvider
    {
        public bool HasPermission => false;
        public void RequestPermission(Action<bool> done) => done?.Invoke(false);
        public void Schedule(SketchNotificationSpec spec, DateTime fireTimeLocal) { }
        public void CancelAll() { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
#if !UNITY_EDITOR
            SketchNotifications.Provider = new SketchIosNotificationProvider();
#endif
        }
    }
}
#endif
