#if SKETCHSDK_ADMOB
using System;
using System.Reflection;

namespace Sketch.Sdk
{
    /// <summary>
    /// Privacy calls that AdMob mediation does not forward on its own. Age tags (TFCD/TFUA) and TCF/UMP consent reach
    /// Liftoff through the adapter (7.6.3.1+ TFUA, 7.7.4.2+ COPPA, 7.4.1+ TCF); the US-state "do not sell" flag does not,
    /// so minors get LiftoffMonetize.SetCCPAStatus(false) before MobileAds.Initialize. Adapters are optional packages and
    /// are reached by reflection (link.xml keeps them through stripping).
    /// </summary>
    static class SketchAdMobMediationPrivacy
    {
        const string LIFTOFF_TYPE = "GoogleMobileAds.Mediation.LiftoffMonetize.Api.LiftoffMonetize, GoogleMobileAds.Mediation.LiftoffMonetize.Api";

        public static void ApplyBeforeInitialize(SketchAdMobConfig config)
        {
            if (config == null || !config.RestrictDataSale) return;
            Invoke(LIFTOFF_TYPE, "SetCCPAStatus", false);
        }

        static void Invoke(string typeName, string method, bool value)
        {
            Type type = Type.GetType(typeName, false);
            if (type == null) return;
            try
            {
                MethodInfo info = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
                if (info == null) { SketchLog.Warn($"{type.Name}.{method}(bool) not found; mediation privacy flag not set."); return; }
                info.Invoke(null, new object[] { value });
                SketchLog.Info($"Mediation privacy: {type.Name}.{method}({value})");
            }
            catch (Exception e)
            {
                Exception inner = e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;
                SketchLog.Warn($"{type.Name}.{method} failed: {inner.Message}");
            }
        }
    }
}
#endif
