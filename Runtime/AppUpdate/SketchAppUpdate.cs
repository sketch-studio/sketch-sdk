using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Google Play in-app updates. Call <see cref="CheckOnStart"/> once (e.g. from the first scene); it waits for remote
    /// config, then starts a flexible (background) update, or an immediate (blocking) one when the installed versionCode
    /// is below <see cref="SketchAppUpdatePolicy.FORCE_MIN_VERSION_CODE_KEY"/>. A cancelled forced update is offered
    /// again on the next resume. When a flexible download finishes, the handlers given to
    /// <see cref="OnFlexibleDownloaded"/> receive a "complete" action for a small "Restart to update" button.
    /// <code>
    /// SketchAppUpdate.OnFlexibleDownloaded(complete => restartButton.Show(complete));
    /// SketchAppUpdate.CheckOnStart();
    /// </code>
    /// </summary>
    public static class SketchAppUpdate
    {
        static readonly List<Action<Action>> DownloadedHandlers = new List<Action<Action>>();
        static readonly Action CompleteAction = CompleteFlexibleUpdate;
        static ISketchAppUpdateProvider _provider;
        static bool _checking;
        static bool _started;
        static bool _forcePending;
        static bool _hooked;

        /// <summary>Registered by the PlayAppUpdate module on Android; the Editor uses a fake (no update). Null = no-op.</summary>
        public static ISketchAppUpdateProvider Provider
        {
            get
            {
#if UNITY_EDITOR
                if (_provider == null) _provider = new SketchEditorAppUpdateProvider();
#endif
                return _provider;
            }
            set => _provider = value;
        }

        public static bool IsChecking => _checking;
        public static bool IsFlexibleDownloaded { get; private set; }
        public static SketchAppUpdateDecision LastDecision { get; private set; }

        /// <summary>Checks the store once per launch after remote config is ready. Safe to call more than once.</summary>
        public static void CheckOnStart()
        {
            if (_started) return;
            _started = true;
            SketchRemoteConfig.WhenReady(Check);
        }

        /// <summary>
        /// Registers a handler for a finished flexible download; it receives the action that installs and restarts.
        /// Fires at once when the download is already done.
        /// </summary>
        public static void OnFlexibleDownloaded(Action<Action> handler)
        {
            if (handler == null) return;
            if (!DownloadedHandlers.Contains(handler)) DownloadedHandlers.Add(handler);
            if (IsFlexibleDownloaded) Invoke(handler);
        }

        public static void RemoveFlexibleDownloadedHandler(Action<Action> handler) => DownloadedHandlers.Remove(handler);

        /// <summary>Installs the downloaded flexible update (the app restarts).</summary>
        public static void CompleteFlexibleUpdate()
        {
            ISketchAppUpdateProvider provider = Provider;
            if (provider == null || !IsFlexibleDownloaded) return;
            SketchAnalytics.Log("app_update_complete");
            try
            {
                provider.CompleteUpdate(ok =>
                {
                    if (!ok) SketchLog.Warn("In-app update install failed.");
                });
            }
            catch (Exception e) { SketchLog.Warn("In-app update install failed: " + e.Message); }
        }

        static void Check()
        {
            ISketchAppUpdateProvider provider = Provider;
            if (provider == null || _checking) return;
            _checking = true;
            try { provider.GetInfo((ok, info) => SketchSdkLoop.Run(() => OnInfo(provider, ok, info))); }
            catch (Exception e)
            {
                _checking = false;
                SketchLog.Warn("In-app update check failed: " + e.Message);
            }
        }

        static void OnInfo(ISketchAppUpdateProvider provider, bool ok, SketchAppUpdateInfo info)
        {
            if (!ok) { _checking = false; return; }
            int current = SafeVersionCode(provider);
            int forceMin = SketchRemoteConfig.GetInt(SketchAppUpdatePolicy.FORCE_MIN_VERSION_CODE_KEY, 0);
            SketchAppUpdateDecision decision = SketchAppUpdatePolicy.Decide(info, current, forceMin);
            LastDecision = decision;
            if (decision == SketchAppUpdateDecision.None) { _checking = false; return; }

            SketchAnalytics.Log("app_update_start", "type", decision == SketchAppUpdateDecision.Immediate ? "immediate" : "flexible",
                "current", current, "available", info.AvailableVersionCode);
            SketchAds.SuppressNextAppOpen();   // the Play update UI takes focus
            try { provider.StartUpdate(decision, result => SketchSdkLoop.Run(() => OnResult(decision, result))); }
            catch (Exception e)
            {
                _checking = false;
                SketchLog.Warn("In-app update start failed: " + e.Message);
            }
        }

        static void OnResult(SketchAppUpdateDecision decision, SketchAppUpdateResult result)
        {
            _checking = false;
            SketchAnalytics.Log("app_update_result", "result", result.ToString());
            if (decision == SketchAppUpdateDecision.Immediate)
            {
                // A forced update the player backed out of is offered again on the next resume.
                _forcePending = result == SketchAppUpdateResult.Canceled;
                if (_forcePending) Hook();
                return;
            }
            if (result != SketchAppUpdateResult.Downloaded) return;
            IsFlexibleDownloaded = true;
            for (int i = 0; i < DownloadedHandlers.Count; i++) Invoke(DownloadedHandlers[i]);
        }

        static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            Application.focusChanged += OnFocusChanged;
        }

        static void OnFocusChanged(bool focused)
        {
            if (!focused || !_forcePending || _checking) return;
            _forcePending = false;
            Check();
        }

        static int SafeVersionCode(ISketchAppUpdateProvider provider)
        {
            try { return provider.CurrentVersionCode; }
            catch (Exception e) { SketchLog.Warn("versionCode read failed: " + e.Message); return 0; }
        }

        static void Invoke(Action<Action> handler)
        {
            try { handler(CompleteAction); }
            catch (Exception e) { Debug.LogException(e); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Application.focusChanged -= OnFocusChanged;
            _hooked = false;
            _provider = null;
            _checking = false;
            _started = false;
            _forcePending = false;
            IsFlexibleDownloaded = false;
            LastDecision = SketchAppUpdateDecision.None;
            DownloadedHandlers.Clear();
        }
    }
}
