using System;
using Unity.Advertisement.IosSupport;
using UnityEngine;

namespace Sketch.Sdk
{
    static class SketchTrackingAuthorization
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => SketchSdkModules.RequestTrackingAuthorization = Request;

        static void Request(Action completed)
        {
            if (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() !=
                ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                completed();
                return;
            }

            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            bool finished = false;
            bool requested = false;
            void Finish()
            {
                if (finished) return;
                finished = true;
                SketchSdkLoop.Update -= Tick;
                completed();
            }
            void Tick()
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline) { Finish(); return; }
                if (requested || !Application.isFocused) return;
                requested = true;
                try
                {
                    ATTrackingStatusBinding.RequestAuthorizationTracking(_ => SketchSdkLoop.Run(Finish));
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    Finish();
                }
            }
            SketchSdkLoop.Update += Tick;
        }
    }
}
