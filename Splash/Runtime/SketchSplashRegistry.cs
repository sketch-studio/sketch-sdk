using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sketch.Sdk.Splash
{
    /// <summary>
    /// Code-side splash extensions. Register from [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] so they exist
    /// before the splash starts. Ready checks are polled every frame and must not allocate.
    /// </summary>
    public static class SketchSplashRegistry
    {
        static readonly List<ISketchSplashPreload> Preloads = new List<ISketchSplashPreload>();
        static readonly List<Func<bool>> ReadyChecks = new List<Func<bool>>();

        internal static IReadOnlyList<ISketchSplashPreload> RegisteredPreloads => Preloads;
        internal static IReadOnlyList<Func<bool>> RegisteredReadyChecks => ReadyChecks;

        public static void Register(ISketchSplashPreload preload)
        {
            if (preload != null && !Preloads.Contains(preload)) Preloads.Add(preload);
        }

        public static void RegisterReadyCheck(Func<bool> isReady)
        {
            if (isReady != null && !ReadyChecks.Contains(isReady)) ReadyChecks.Add(isReady);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Preloads.Clear();
            ReadyChecks.Clear();
        }
    }
}
