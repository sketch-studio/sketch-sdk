#if SKETCHSDK_UNITY_IAP
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>Registers the Unity IAP store provider with SketchStoreProviders before the first scene loads.</summary>
    static class SketchUnityIapModule
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => SketchStoreProviders.Factory = () => new SketchUnityIapProvider();
    }
}
#endif
