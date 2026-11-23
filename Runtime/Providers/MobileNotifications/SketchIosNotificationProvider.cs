#if SKETCHSDK_NOTIFICATIONS && UNITY_IOS
using System;
using Unity.Notifications.iOS;
using UnityEngine;

namespace Sketch.Sdk
{
    public sealed class SketchIosNotificationProvider : ISketchNotificationProvider
    {
        public bool HasPermission
        {
            get
            {
                var status = iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus;
                return status == AuthorizationStatus.Authorized || status == AuthorizationStatus.Provisional;
            }
        }

        public void RequestPermission(Action<bool> done)
        {
            var request = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge |
                AuthorizationOption.Sound, false);
            void Poll()
            {
                if (!request.IsFinished) return;
                SketchSdkLoop.Update -= Poll;
                bool granted = request.Granted;
                request.Dispose();
                done?.Invoke(granted);
            }
            SketchSdkLoop.Update += Poll;
        }

        public void Schedule(SketchNotificationSpec spec, DateTime fireTimeLocal)
        {
            if (spec == null) return;
            double seconds = Math.Max(1, (fireTimeLocal - DateTime.Now).TotalSeconds);
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = spec.Id,
                Title = spec.Title,
                Body = spec.Body,
                ShowInForeground = false,
                Trigger = new iOSNotificationTimeIntervalTrigger
                {
                    TimeInterval = TimeSpan.FromSeconds(seconds),
                    Repeats = false
                }
            });
        }

        public void CancelAll()
        {
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
        }

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
