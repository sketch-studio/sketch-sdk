using System.Collections;

namespace Sketch.Sdk.Splash
{
    /// <summary>
    /// Game work the splash waits for (asset warm-up, save load...). Implement it on a ScriptableObject or prefab component
    /// listed in SketchSplashHook, or register an instance with SketchSplashRegistry. Abandoned when the splash hard cap hits.
    /// </summary>
    public interface ISketchSplashPreload
    {
        IEnumerator Preload();
    }
}
