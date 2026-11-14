using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>SDK log helper. Info logs only in Editor / development builds when VerboseLogs is on.</summary>
    public static class SketchLog
    {
        public static bool Verbose = true;

        public static void Info(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Verbose) Debug.Log("[SketchSdk] " + message);
#endif
        }

        public static void Warn(string message) => Debug.LogWarning("[SketchSdk] " + message);
        public static void Error(string message) => Debug.LogError("[SketchSdk] " + message);
    }
}
