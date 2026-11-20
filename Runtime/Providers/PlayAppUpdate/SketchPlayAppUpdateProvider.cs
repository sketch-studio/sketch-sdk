#if SKETCHSDK_PLAY_APPUPDATE && UNITY_ANDROID
using System;
using Google.Play.AppUpdate;
using Google.Play.Common;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Google Play In-App Updates (com.google.play.appupdate): GetAppUpdateInfo -> StartUpdate(flexible | immediate)
    /// -> CompleteUpdate. The running AppUpdateRequest is polled from the SDK PlayerLoop tick only while a flow runs
    /// (one cached delegate, no per-frame allocation). Written against the documented public API (package not on disk
    /// when authored): see docs/reports/2026-11-21-sdk-retention-modules.md, UNVERIFIED list.
    /// </summary>
    public sealed class SketchPlayAppUpdateProvider : ISketchAppUpdateProvider
    {
        AppUpdateManager _manager;
        AppUpdateInfo _info;
        AppUpdateRequest _request;
        Action<SketchAppUpdateResult> _startDone;
        readonly Action _poll;
        int _versionCode = -1;

        public SketchPlayAppUpdateProvider() { _poll = Poll; }

        AppUpdateManager Manager => _manager ?? (_manager = new AppUpdateManager());

        public int CurrentVersionCode
        {
            get
            {
                if (_versionCode < 0) _versionCode = ReadVersionCode();
                return _versionCode;
            }
        }

        public void GetInfo(Action<bool, SketchAppUpdateInfo> done)
        {
            PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode> operation;
            try { operation = Manager.GetAppUpdateInfo(); }
            catch (Exception e)
            {
                SketchLog.Warn("Play update info failed: " + e.Message);
                done?.Invoke(false, default);
                return;
            }
            operation.Completed += op => SketchSdkLoop.Run(() =>
            {
                if (!op.IsSuccessful)
                {
                    SketchLog.Warn("Play update info error: " + op.Error);
                    done?.Invoke(false, default);
                    return;
                }
                _info = op.GetResult();
                UpdateAvailability availability = _info.UpdateAvailability;
                var result = new SketchAppUpdateInfo
                {
                    // An immediate update interrupted earlier must be resumed (developer-triggered in progress).
                    UpdateAvailable = availability == UpdateAvailability.UpdateAvailable
                                      || availability == UpdateAvailability.DeveloperTriggeredUpdateInProgress,
                    AvailableVersionCode = _info.AvailableVersionCode,
                    FlexibleAllowed = _info.IsUpdateTypeAllowed(AppUpdateOptions.FlexibleAppUpdateOptions()),
                    ImmediateAllowed = _info.IsUpdateTypeAllowed(AppUpdateOptions.ImmediateAppUpdateOptions()),
                };
                done?.Invoke(true, result);
            });
        }

        public void StartUpdate(SketchAppUpdateDecision type, Action<SketchAppUpdateResult> done)
        {
            if (_info == null || _request != null || type == SketchAppUpdateDecision.None)
            {
                done?.Invoke(SketchAppUpdateResult.Failed);
                return;
            }
            AppUpdateOptions options = type == SketchAppUpdateDecision.Immediate
                ? AppUpdateOptions.ImmediateAppUpdateOptions()
                : AppUpdateOptions.FlexibleAppUpdateOptions();
            try { _request = Manager.StartUpdate(_info, options); }
            catch (Exception e)
            {
                SketchLog.Warn("Play update start failed: " + e.Message);
                done?.Invoke(SketchAppUpdateResult.Failed);
                return;
            }
            _startDone = done;
            SketchSdkLoop.Update += _poll;
        }

        public void CompleteUpdate(Action<bool> done)
        {
            PlayAsyncOperation<VoidResult, AppUpdateErrorCode> operation;
            try { operation = Manager.CompleteUpdate(); }
            catch (Exception e)
            {
                SketchLog.Warn("Play update complete failed: " + e.Message);
                done?.Invoke(false);
                return;
            }
            // On success the app restarts and this never runs.
            operation.Completed += op => SketchSdkLoop.Run(() => done?.Invoke(op.IsSuccessful));
        }

        void Poll()
        {
            if (_request == null || !_request.IsDone) return;
            AppUpdateStatus status = _request.Status;
            SketchSdkLoop.Update -= _poll;
            _request = null;
            Action<SketchAppUpdateResult> done = _startDone;
            _startDone = null;
            SketchAppUpdateResult result = status == AppUpdateStatus.Downloaded ? SketchAppUpdateResult.Downloaded
                : status == AppUpdateStatus.Canceled ? SketchAppUpdateResult.Canceled
                : SketchAppUpdateResult.Failed;
            done?.Invoke(result);
        }

        static int ReadVersionCode()
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
                using (var info = packageManager.Call<AndroidJavaObject>("getPackageInfo", activity.Call<string>("getPackageName"), 0))
                    return info.Get<int>("versionCode");
            }
            catch (Exception e)
            {
                SketchLog.Warn("versionCode read failed: " + e.Message);
                return 0;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
#if !UNITY_EDITOR
            SketchAppUpdate.Provider = new SketchPlayAppUpdateProvider();
#endif
        }
    }
}
#endif
