#if SKETCHSDK_ADMOB
using System;
using System.Reflection;

namespace Sketch.Sdk
{
    /// <summary>
    /// Runs SketchMediationPrivacy.Plan on the installed AdMob mediation adapters before MobileAds.Initialize. Adapters
    /// are optional packages reached by reflection (link.xml keeps them through stripping); a missing adapter is skipped,
    /// an installed adapter whose call fails is logged as an error and reported to analytics.
    /// </summary>
    static class SketchAdMobMediationPrivacy
    {
        /// <summary>Run before MobileAds.Initialize and again whenever consent changes (privacy options form).</summary>
        public static void Apply(SketchAdMobConfig config)
        {
            if (config == null) return;
            bool? gpp = SketchGppStore.UsOptOut();
            if (gpp.HasValue) SketchLog.Info("GPP US opt-out: " + gpp.Value);
            foreach (SketchMediationPrivacyCall call in SketchMediationPrivacy.Plan(config.AgeSignalActive, config.RestrictDataSale, config.NonPersonalized, gpp))
                Invoke(call);
        }

        static void Invoke(SketchMediationPrivacyCall call)
        {
            Type type = Type.GetType(call.TypeName, false);
            if (type == null) return;
            var types = new Type[call.Args.Length];
            for (int i = 0; i < types.Length; i++) types[i] = call.Args[i].GetType();
            try
            {
                MethodInfo info = type.GetMethod(call.Method, BindingFlags.Public | BindingFlags.Static, null, types, null);
                if (info == null) throw new MissingMethodException(type.FullName, call.Method);
                info.Invoke(null, call.Args);
                SketchLog.Info("Mediation privacy: " + call);
            }
            catch (Exception e)
            {
                Exception inner = e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;
                UnityEngine.Debug.LogError($"[SketchAds] Mediation privacy call failed: {call} -> {inner.GetType().Name}: {inner.Message}");
                SketchAnalytics.Log("mediation_privacy_failed", "network", call.Network, "method", call.Method);
            }
        }
    }
}
#endif
