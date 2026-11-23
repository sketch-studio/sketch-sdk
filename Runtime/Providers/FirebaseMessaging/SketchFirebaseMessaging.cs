using System;
using Firebase;
using Firebase.Messaging;
using UnityEngine;

namespace Sketch.Sdk
{
    public static class SketchFirebaseMessaging
    {
        static bool _starting;
        static bool _started;
        static Action<bool> _completion;

        public static event Action<string> RegistrationReceived;
        public static event Action<FirebaseMessage> MessageReceived;

        /// <summary>Starts Firebase Messaging after the game has completed its notification permission flow.</summary>
        public static void Start(Action<bool> onReady = null)
        {
            if (_started) { onReady?.Invoke(true); return; }
            _completion += onReady;
            if (_starting) return;
            _starting = true;
            try
            {
                SketchFirebaseDependencies.Once(FirebaseApp.CheckAndFixDependenciesAsync).ContinueWith(task =>
                {
                    bool available = !task.IsCanceled && !task.IsFaulted && task.Result == DependencyStatus.Available;
                    SketchSdkLoop.Post(() => FinishStart(available));
                });
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                FinishStart(false);
            }
        }

        static void FinishStart(bool available)
        {
            if (available)
            {
                try
                {
                    _ = FirebaseApp.DefaultInstance;
                    FirebaseMessaging.RegistrationReceived += ForwardRegistration;
                    FirebaseMessaging.MessageReceived += ForwardMessage;
                    _started = true;
                }
                catch (Exception error)
                {
                    FirebaseMessaging.RegistrationReceived -= ForwardRegistration;
                    FirebaseMessaging.MessageReceived -= ForwardMessage;
                    Debug.LogException(error);
                }
            }
            _starting = false;
            Action<bool> callbacks = _completion;
            _completion = null;
            if (callbacks == null) return;
            foreach (Action<bool> callback in callbacks.GetInvocationList())
            {
                try { callback(_started); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }

        static void ForwardRegistration(object sender, RegistrationReceivedEventArgs args)
        {
            string installationId = args.InstallationId;
            SketchSdkLoop.Run(() => RegistrationReceived?.Invoke(installationId));
        }

        static void ForwardMessage(object sender, MessageReceivedEventArgs args)
        {
            FirebaseMessage message = args.Message;
            SketchSdkLoop.Run(() => MessageReceived?.Invoke(message));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            if (_started)
            {
                FirebaseMessaging.RegistrationReceived -= ForwardRegistration;
                FirebaseMessaging.MessageReceived -= ForwardMessage;
            }
            _started = _starting = false;
            _completion = null;
            RegistrationReceived = null;
            MessageReceived = null;
        }
    }
}
