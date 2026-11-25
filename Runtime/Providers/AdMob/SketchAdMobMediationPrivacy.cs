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
        public static void ApplyBeforeInitialize(SketchAdMobConfig config)
        {
            if (config == null) return;
            foreach (SketchMediationPrivacyCall call in SketchMediationPrivacy.Plan(config.AgeSignalActive, config.RestrictDataSale, config.NonPersonalized))
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
