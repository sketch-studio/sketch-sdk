using System;

namespace Sketch.Sdk
{
    /// <summary>Editor stand-in: no update by default; set <see cref="Simulated"/> to exercise the flows.</summary>
    public sealed class SketchEditorAppUpdateProvider : ISketchAppUpdateProvider
    {
        public SketchAppUpdateInfo Simulated { get; set; }

        public int CurrentVersionCode
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.PlayerSettings.Android.bundleVersionCode;
#else
                return 0;
#endif
            }
        }

        public void GetInfo(Action<bool, SketchAppUpdateInfo> done) => done?.Invoke(true, Simulated);

        public void StartUpdate(SketchAppUpdateDecision type, Action<SketchAppUpdateResult> done)
        {
            SketchLog.Info("[Editor] In-app update started: " + type + ".");
            done?.Invoke(type == SketchAppUpdateDecision.Flexible ? SketchAppUpdateResult.Downloaded : SketchAppUpdateResult.Canceled);
        }

        public void CompleteUpdate(Action<bool> done)
        {
            SketchLog.Info("[Editor] In-app update would install and restart now.");
            done?.Invoke(true);
        }
    }
}
