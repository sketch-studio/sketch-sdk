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
        static bool _useLegacyTokens;
        static Action<bool> _completion;

        public static event Action<string> RegistrationReceived;
        public static event Action<string> TokenReceived;
        public static event Action<FirebaseMessage> MessageReceived;

        /// <summary>Starts Firebase Messaging after the game has completed its notification permission flow.</summary>
        public static void Start(Action<bool> onReady = null) => Start(false, onReady);

        /// <summary>Starts the older FCM registration-token flow for an existing token-based backend.</summary>
        public static void StartWithLegacyTokens(Action<bool> onReady = null) => Start(true, onReady);

        static void Start(bool useLegacyTokens, Action<bool> onReady)
        {
            if ((_started || _starting) && _useLegacyTokens != useLegacyTokens)
            {
                onReady?.Invoke(false);
                return;
            }
            if (_started) { onReady?.Invoke(true); return; }
            _completion += onReady;
            if (_starting) return;
            _starting = true;
            _useLegacyTokens = useLegacyTokens;
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
                    SetRegistrationListener(true);
                    FirebaseMessaging.MessageReceived += ForwardMessage;
                    _started = true;
                }
                catch (Exception error)
                {
                    SetRegistrationListener(false);
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

        static void SetRegistrationListener(bool added)
        {
            if (_useLegacyTokens)
            {
#pragma warning disable CS0618
                if (added) FirebaseMessaging.TokenReceived += ForwardToken;
                else FirebaseMessaging.TokenReceived -= ForwardToken;
#pragma warning restore CS0618
            }
            else if (added) FirebaseMessaging.RegistrationReceived += ForwardRegistration;
            else FirebaseMessaging.RegistrationReceived -= ForwardRegistration;
        }

        static void ForwardToken(object sender, TokenReceivedEventArgs args)
        {
            string token = args.Token;
            SketchSdkLoop.Run(() => TokenReceived?.Invoke(token));
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
                SetRegistrationListener(false);
                FirebaseMessaging.MessageReceived -= ForwardMessage;
            }
            _started = _starting = _useLegacyTokens = false;
            _completion = null;
            RegistrationReceived = null;
            TokenReceived = null;
            MessageReceived = null;
        }
    }
}
