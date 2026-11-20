#if SKETCHSDK_PLAY_REVIEW && UNITY_ANDROID
using System;
using Google.Play.Common;
using Google.Play.Review;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Google Play In-App Review (com.google.play.review): RequestReviewFlow -> LaunchReviewFlow. Uses the operations'
    /// Completed events (Play Core raises them on the main thread; they are re-posted through SketchSdkLoop anyway),
    /// so no coroutine or scene object is needed. Play quota may silently show nothing; success never means a rating.
    /// </summary>
    public sealed class SketchPlayReviewProvider : ISketchReviewProvider
    {
        ReviewManager _manager;
        Action<bool> _pending;

        public void RequestReview(Action<bool> done)
        {
            if (_pending != null) { done?.Invoke(false); return; }
            _pending = done ?? (_ => { });
            PlayAsyncOperation<PlayReviewInfo, ReviewErrorCode> request;
            try
            {
                if (_manager == null) _manager = new ReviewManager();
                request = _manager.RequestReviewFlow();
            }
            catch (Exception e)
            {
                SketchLog.Warn("Play review request failed: " + e.Message);
                Finish(false);
                return;
            }
            request.Completed += OnRequestCompleted;
        }

        void OnRequestCompleted(PlayAsyncOperation<PlayReviewInfo, ReviewErrorCode> request)
        {
            SketchSdkLoop.Run(() =>
            {
                if (!request.IsSuccessful)
                {
                    SketchLog.Warn("Play review flow unavailable: " + request.Error);
                    Finish(false);
                    return;
                }
                PlayAsyncOperation<VoidResult, ReviewErrorCode> launch;
                try { launch = _manager.LaunchReviewFlow(request.GetResult()); }
                catch (Exception e)
                {
                    SketchLog.Warn("Play review launch failed: " + e.Message);
                    Finish(false);
                    return;
                }
                launch.Completed += op => SketchSdkLoop.Run(() =>
                {
                    if (!op.IsSuccessful) SketchLog.Warn("Play review launch error: " + op.Error);
                    Finish(op.IsSuccessful);
                });
            });
        }

        void Finish(bool ok)
        {
            Action<bool> done = _pending;
            _pending = null;
            done?.Invoke(ok);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
#if !UNITY_EDITOR
            SketchReview.Provider = new SketchPlayReviewProvider();
#endif
        }
    }
}
#endif
