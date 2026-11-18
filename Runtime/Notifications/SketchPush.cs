using System;

namespace Sketch.Sdk
{
    /// <summary>Provider-neutral remote push contract. FCM/APNs adapters own credentials and platform IO.</summary>
    public static class SketchPush
    {
        static ISketchPushProvider _provider;
        public static bool IsInitialized { get; private set; }
        public static string Token { get; private set; }

        public static void Initialize(ISketchPushProvider provider, Action<bool> onDone = null)
        {
            _provider = provider;
            IsInitialized = false;
            Token = string.Empty;
            if (_provider == null) { onDone?.Invoke(false); return; }
            try
            {
                _provider.Initialize(ok =>
                {
                    IsInitialized = ok;
                    SketchAnalytics.Log("push_init", "success", ok);
                    onDone?.Invoke(ok);
                });
            }
            catch (Exception e) { SketchLog.Warn("Push initialization failed: " + e.Message); onDone?.Invoke(false); }
        }

        public static void RequestPermission(Action<bool> onGranted)
        {
            if (!IsInitialized) { onGranted?.Invoke(false); return; }
            try { _provider.RequestPermission(onGranted); }
            catch (Exception e) { SketchLog.Warn("Push permission failed: " + e.Message); onGranted?.Invoke(false); }
        }

        public static void RefreshToken(Action<string> onToken)
        {
            if (!IsInitialized) { onToken?.Invoke(string.Empty); return; }
            try
            {
                _provider.GetToken(token =>
                {
                    Token = token ?? string.Empty;
                    if (Token.Length > 0) SketchAnalytics.Log("push_token_ready");
                    onToken?.Invoke(Token);
                });
            }
            catch (Exception e) { SketchLog.Warn("Push token failed: " + e.Message); onToken?.Invoke(string.Empty); }
        }

        public static void DeleteToken(Action<bool> onDeleted)
        {
            if (!IsInitialized) { onDeleted?.Invoke(false); return; }
            try { _provider.DeleteToken(ok => { if (ok) Token = string.Empty; onDeleted?.Invoke(ok); }); }
            catch (Exception e) { SketchLog.Warn("Push token deletion failed: " + e.Message); onDeleted?.Invoke(false); }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _provider = null; IsInitialized = false; Token = string.Empty; }
    }
}
