#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Sketch.Sdk
{
    static class SketchTrackingUsagePostBuild
    {
        [PostProcessBuild]
        static void WriteUsageDescription(BuildTarget target, string outputPath)
        {
            if (target != BuildTarget.iOS) return;
            SketchSdkSettings settings = SketchSdkSettings.Load();
            string description = settings != null ? settings.IosTrackingUsageDescription : null;
            if (string.IsNullOrWhiteSpace(description)) return;

            string path = Path.Combine(outputPath, "Info.plist");
            var document = new PlistDocument();
            document.ReadFromFile(path);
            document.root.SetString("NSUserTrackingUsageDescription", description.Trim());
            document.WriteToFile(path);
        }
    }
}
#endif
