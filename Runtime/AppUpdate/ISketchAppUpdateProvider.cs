using System;

namespace Sketch.Sdk
{
    /// <summary>Store in-app update backend (Google Play In-App Updates, Editor fake). Callbacks on the main thread.</summary>
    public interface ISketchAppUpdateProvider
    {
        /// <summary>Installed Android versionCode; 0 when unknown.</summary>
        int CurrentVersionCode { get; }

        /// <summary>done(ok, info): ok false when the store could not be queried.</summary>
        void GetInfo(Action<bool, SketchAppUpdateInfo> done);

        /// <summary>Starts the flow for the last queried info. done is invoked once when it ends.</summary>
        void StartUpdate(SketchAppUpdateDecision type, Action<SketchAppUpdateResult> done);

        /// <summary>Installs a downloaded flexible update (the app restarts). done(false) when it failed.</summary>
        void CompleteUpdate(Action<bool> done);
    }
}
