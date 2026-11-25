#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>Development builds only: hold four fingers for two seconds to open AdMob Ad Inspector. Compiled out of release builds.</summary>
    sealed class SketchAdInspectorGesture : MonoBehaviour
    {
        readonly SketchAdInspectorGestureDetector _detector = new SketchAdInspectorGestureDetector();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var host = new GameObject("SketchAdInspectorGesture") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<SketchAdInspectorGesture>();
            Debug.Log("[SketchAds] Development build: hold 4 fingers for 2 s to open Ad Inspector.");
        }

        void Update()
        {
            if (!_detector.Step(Input.touchCount, Time.unscaledTimeAsDouble)) return;
            Debug.Log("[SketchAds] Opening Ad Inspector.");
            SketchAds.OpenAdInspector(error =>
            {
                if (error != null) Debug.LogWarning("[SketchAds] Ad Inspector not opened: " + error);
            });
        }
    }
}
#endif
