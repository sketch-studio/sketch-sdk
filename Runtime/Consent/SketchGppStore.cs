using System;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Reads the IAB GPP keys a CMP (Google UMP) writes on the device: IABGPP_HDR_GppString and IABGPP_GppSID in the
    /// default SharedPreferences on Android and NSUserDefaults (Unity PlayerPrefs) on iOS. Override Reader in tests or
    /// for another CMP storage.
    /// </summary>
    public static class SketchGppStore
    {
        public const string GPP_STRING_KEY = "IABGPP_HDR_GppString";
        public const string GPP_SID_KEY = "IABGPP_GppSID";

        /// <summary>Returns (gppString, sectionIds); nulls when nothing is stored.</summary>
        public static Func<(string gpp, string sid)> Reader = ReadPlatform;

        /// <summary>US opt-out from the stored GPP signal (see SketchGpp.UsOptOut); null when no US section applies.</summary>
        public static bool? UsOptOut()
        {
            try
            {
                (string gpp, string sid) = Reader();
                return SketchGpp.UsOptOut(gpp, sid);
            }
            catch (Exception e)
            {
                SketchLog.Warn("GPP read failed: " + e.Message);
                return null;
            }
        }

        static (string, string) ReadPlatform()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var manager = new AndroidJavaClass("android.preference.PreferenceManager"))
            using (var prefs = manager.CallStatic<AndroidJavaObject>("getDefaultSharedPreferences", activity))
            {
                return (ReadAny(prefs, GPP_STRING_KEY), ReadAny(prefs, GPP_SID_KEY));
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // Unity's PlayerPrefs is NSUserDefaults.standardUserDefaults on iOS, where CMPs store the IABGPP_ keys.
            return (ReadDefaults(GPP_STRING_KEY), ReadDefaults(GPP_SID_KEY));
#else
            return (null, null);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>The value as text whatever type the CMP stored (String or Integer).</summary>
        static string ReadAny(AndroidJavaObject prefs, string key)
        {
            using (var all = prefs.Call<AndroidJavaObject>("getAll"))
            using (var value = all.Call<AndroidJavaObject>("get", key))
            {
                return value == null ? null : value.Call<string>("toString");
            }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        static string ReadDefaults(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return null;
            string text = PlayerPrefs.GetString(key, string.Empty);
            if (!string.IsNullOrEmpty(text)) return text;
            int number = PlayerPrefs.GetInt(key, int.MinValue);
            return number == int.MinValue ? null : number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Reader = ReadPlatform;
    }
}
