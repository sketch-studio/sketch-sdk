using System;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Store provider selection. SketchSdk.UnityIap registers Factory from [RuntimeInitializeOnLoadMethod(AfterAssembliesLoaded)];
    /// the Editor uses SketchEditorFakeStore unless UseRealStoreInEditor is set.
    /// </summary>
    public static class SketchStoreProviders
    {
        public static Func<ISketchStoreProvider> Factory { get; set; }
        /// <summary>Editor only: use the registered real provider (Unity IAP fake store) instead of SketchEditorFakeStore.</summary>
        public static bool UseRealStoreInEditor { get; set; }

        public static ISketchStoreProvider Create()
        {
#if UNITY_EDITOR
            if (!UseRealStoreInEditor || Factory == null) return new SketchEditorFakeStore();
#endif
            return Factory?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Factory = null;
            UseRealStoreInEditor = false;
        }
    }
}
