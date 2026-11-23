#if SKETCHSDK_ADMOB
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>Registers the AdMob provider and UMP consent with the SDK bootstrap.</summary>
    static class SketchAdMobModule
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
            SketchSdkModules.AdsProviderFactory = (settings, telemetry) =>
            {
                SketchAdMobConfig config = settings.BuildAdMobConfig();
                if (!SketchAdMobConfig.HasAnyAdUnit(config))
                {
                    SketchLog.Warn($"No AdMob ad unit ids for {Application.platform}; fill them in Sketch SDK > Settings.");
                    return null;
                }
                if (settings.UseTestAds) SketchLog.Info("AdMob test ad units in use (development build).");
                return new SketchSdkAdMobProvider(config, telemetry);
            };
            SketchSdkModules.AdMobConsentProviderFactory = _ => new SketchUmpConsentProvider();
        }
    }
}
#endif
