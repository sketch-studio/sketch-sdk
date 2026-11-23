using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Sketch.Sdk.Editor
{
    /// <summary>OR-820: writes the bundle version code into SketchSdkSettings before every player build (runtime cannot read PlayerSettings).</summary>
    public sealed class SketchBuildVersionBaker : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            SketchSdkSettings settings = SketchSdkSettings.Load();
            if (settings == null) return;
            int code = CodeFor(report.summary.platform);
            if (settings.BuildVersionCode == code) return;
            settings.BuildVersionCode = code;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        public static int CodeFor(BuildTarget target)
        {
            if (target == BuildTarget.iOS) return int.TryParse(PlayerSettings.iOS.buildNumber, out int ios) ? ios : 0;
            return PlayerSettings.Android.bundleVersionCode;
        }
    }
}
