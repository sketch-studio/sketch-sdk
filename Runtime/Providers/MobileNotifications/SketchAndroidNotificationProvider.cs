#if SKETCHSDK_NOTIFICATIONS && UNITY_ANDROID
using System;
using Unity.Notifications.Android;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Android local notifications through com.unity.mobile.notifications: one channel, POST_NOTIFICATIONS through
    /// PermissionRequest (Android 13+; the package adds the manifest permission), schedule by local fire time, cancel all.
    /// The permission request is polled from the SDK PlayerLoop tick only while it is pending.
    /// Written against the documented 2.x API (package not on disk when authored): see the report's UNVERIFIED list.
    /// </summary>
    public sealed class SketchAndroidNotificationProvider : ISketchNotificationProvider
    {
        public const string CHANNEL_ID = "sketch_reengage";
        const float NOT_STARTED_TIMEOUT_SECONDS = 5f;

        readonly string _channelName;
        readonly string _channelDescription;
        readonly Action _poll;
        bool _initialized;
        PermissionRequest _request;
        Action<bool> _pending;
        float _requestStartedAt;

        public SketchAndroidNotificationProvider(string channelName = "Reminders", string channelDescription = "Game reminders")
        {
            _channelName = channelName;
            _channelDescription = channelDescription;
            _poll = Poll;
        }

        public bool HasPermission
        {
            get
            {
                EnsureInitialized();
                return AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.Allowed;
            }
        }

        public void RequestPermission(Action<bool> done)
        {
            EnsureInitialized();
            if (_request != null) { _pending += done; return; }
            _pending = done;
            _requestStartedAt = Time.realtimeSinceStartup;
            _request = new PermissionRequest();
            SketchSdkLoop.Update += _poll;
        }

        public void Schedule(SketchNotificationSpec spec, DateTime fireTimeLocal)
        {
            if (spec == null) return;
            EnsureInitialized();
            var notification = new AndroidNotification
            {
                Title = spec.Title,
                Text = spec.Body,
                FireTime = fireTimeLocal,
            };
            if (!string.IsNullOrEmpty(spec.SmallIcon)) notification.SmallIcon = spec.SmallIcon;
            if (!string.IsNullOrEmpty(spec.LargeIcon)) notification.LargeIcon = spec.LargeIcon;
            AndroidNotificationCenter.SendNotification(notification, CHANNEL_ID);
        }

        public void CancelAll()
        {
            EnsureInitialized();
            AndroidNotificationCenter.CancelAllScheduledNotifications();
            AndroidNotificationCenter.CancelAllDisplayedNotifications();
        }

        void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            AndroidNotificationCenter.Initialize();
            AndroidNotificationCenter.RegisterNotificationChannel(
                new AndroidNotificationChannel(CHANNEL_ID, _channelName, _channelDescription, Importance.Default));
        }

        void Poll()
        {
            if (_request == null) { SketchSdkLoop.Update -= _poll; return; }
            PermissionStatus status = _request.Status;
            if (status == PermissionStatus.RequestPending) return;
            // NotRequested only lasts until the OS dialog opens; give up if it never does.
            if (status == PermissionStatus.NotRequested && Time.realtimeSinceStartup - _requestStartedAt < NOT_STARTED_TIMEOUT_SECONDS) return;
            SketchSdkLoop.Update -= _poll;
            _request = null;
            Action<bool> done = _pending;
            _pending = null;
            done?.Invoke(status == PermissionStatus.Allowed);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
#if !UNITY_EDITOR
            SketchNotifications.Provider = new SketchAndroidNotificationProvider();
#endif
        }
    }
}
#endif
