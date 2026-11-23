using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Version reported to the Sketch server (OR-820): the bundle version code (1, 2, 3...), not the version name ("1.3")
    /// and not the SDK version, so the dashboard From/To Version filter works with plain integers.
    /// </summary>
    public static class SketchAppVersion
    {
        static string _code;

        public static string Code => _code ??= Resolve(BakedCode(), Application.version);

        /// <summary>Version code if known (>0), else the version name as a fallback.</summary>
        public static string Resolve(int versionCode, string versionName) =>
            versionCode > 0 ? versionCode.ToString(System.Globalization.CultureInfo.InvariantCulture) : (versionName ?? string.Empty);

        static int BakedCode()
        {
#if UNITY_EDITOR
#if UNITY_IOS
            int.TryParse(UnityEditor.PlayerSettings.iOS.buildNumber, out int ios);
            return ios;
#else
            return UnityEditor.PlayerSettings.Android.bundleVersionCode;
#endif
#else
            SketchSdkSettings settings = SketchSdkSettings.Load();
            return settings != null ? settings.BuildVersionCode : 0;
#endif
        }
    }
}
