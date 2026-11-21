using System;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// JNI bridge to the Sketch custom native layout (Plugins/Android/SketchAdmobNative.java). Android player only;
    /// every member is a safe no-op elsewhere. Java calls back through UnitySendMessage on the receiver object.
    /// </summary>
    public sealed class SketchAndroidNative : MonoBehaviour
    {
        const string JAVA_CLASS = "com.sketch.sdk.ads.SketchAdmobNative";
        const string RECEIVER = "[SketchAndroidNative]";

        static SketchAndroidNative _instance;

        public static event Action Loaded;
        public static event Action<string> LoadFailed;   // "code|message"
        public static event Action Shown;
        public static event Action UserClosed;
        public static event Action Clicked;
        public static event Action<string> Paid;         // "micros|currency|precision|responseId|source|placement"

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            Loaded = null; LoadFailed = null; Shown = null; UserClosed = null; Clicked = null; Paid = null;
        }

        public static bool Supported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        static void EnsureReceiver()
        {
            if (_instance != null) return;
            var go = new GameObject(RECEIVER);
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SketchAndroidNative>();
            Call("setReceiver", RECEIVER);
        }

        public static void Load(string adUnitId)
        {
            if (!Supported) return;
            EnsureReceiver();
            Call("load", adUnitId);
        }

        public static bool IsReady => Supported && CallBool("isReady");
        public static bool IsShowing => Supported && CallBool("isShowing");

        /// <summary>screenPixels: Unity screen rect (bottom-left origin); converted to Android top-left pixels here.</summary>
        public static bool ShowInRect(Rect screenPixels, string placement)
        {
            if (!Supported) return false;
            EnsureReceiver();
            int x = Mathf.RoundToInt(screenPixels.xMin);
            int y = Mathf.RoundToInt(Screen.height - screenPixels.yMax);
            int w = Mathf.Max(1, Mathf.RoundToInt(screenPixels.width));
            int h = Mathf.Max(1, Mathf.RoundToInt(screenPixels.height));
            return CallBool("showInRect", x, y, w, h, placement ?? string.Empty);
        }

        public static void StartCloseCountdown(int seconds, bool showCountdown, bool closeOnLeft)
        {
            if (Supported) Call("startCloseCountdown", seconds, showCountdown, closeOnLeft);
        }

        public static void ReleasePlayback()
        {
            if (Supported) Call("releasePlayback");
        }

        public static void Hide()
        {
            if (Supported) Call("hide");
        }

        public static string Status => Supported ? CallString("status") : "unsupported";

        // ---- Java -> Unity (names must match SketchAdmobNative.java send(...)) ----
        public void OnNativeLoaded(string _) => Loaded?.Invoke();
        public void OnNativeLoadFailed(string payload) => LoadFailed?.Invoke(payload);
        public void OnNativeShown(string _) => Shown?.Invoke();
        public void OnNativeClosed(string _) => UserClosed?.Invoke();
        public void OnNativeClicked(string _) => Clicked?.Invoke();
        public void OnNativePaid(string payload) => Paid?.Invoke(payload);

        static void Call(string method, params object[] args)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var java = new AndroidJavaClass(JAVA_CLASS)) java.CallStatic(method, args); }
            catch (Exception e) { Debug.LogWarning($"[SketchAds] Native java {method} failed: {e.Message}"); }
#endif
        }

        static bool CallBool(string method, params object[] args)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var java = new AndroidJavaClass(JAVA_CLASS)) return java.CallStatic<bool>(method, args); }
            catch (Exception e) { Debug.LogWarning($"[SketchAds] Native java {method} failed: {e.Message}"); }
#endif
            return false;
        }

        static string CallString(string method)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var java = new AndroidJavaClass(JAVA_CLASS)) return java.CallStatic<string>(method); }
            catch (Exception e) { return "error: " + e.Message; }
#else
            return string.Empty;
#endif
        }
    }
}
