using System;
using System.Collections.Generic;

namespace Sketch.Sdk
{
    /// <summary>One static privacy call on an optional mediation adapter's Unity API (reached by reflection).</summary>
    public sealed class SketchMediationPrivacyCall
    {
        public readonly string Network;
        /// <summary>Assembly-qualified type name of the adapter's public API class.</summary>
        public readonly string TypeName;
        public readonly string Method;
        public readonly object[] Args;

        public SketchMediationPrivacyCall(string network, string typeName, string method, params object[] args)
        {
            Network = network;
            TypeName = typeName;
            Method = method;
            Args = args ?? Array.Empty<object>();
        }

        public override string ToString() => $"{Network}: {Method}({string.Join(", ", Array.ConvertAll(Args, a => a is string s ? "\"" + s + "\"" : Convert.ToString(a)))})";
    }

    /// <summary>
    /// Privacy signals that AdMob mediation adapters do NOT forward, as data. What adapters already forward
    /// (official Google adapter pages, 2026-11): GDPR via the UMP TCF/AC strings (Liftoff 7.4.1+, Unity Ads 4.19.0.1+,
    /// InMobi 10.7.5.0+, BIGO 5.5.2.0+), TFUA (Liftoff 7.6.3.1+, Unity Ads 4.16.6.0+, InMobi 11.1.0.1+, BIGO 5.7.0.0+),
    /// COPPA / age treatment (Liftoff 7.7.4.2+, Unity Ads 4.6.0.0+, InMobi 11.3.0.1+, BIGO 5.9.0.1+), InMobi also reads
    /// the IAB US Privacy string. US-state "do not sell" needs an explicit call on Liftoff, Unity Ads and BIGO, and Unity
    /// Ads Mixed Audience needs user.nonbehavioral per user. Add a network by adding a rule here; missing adapters are
    /// skipped at run time.
    /// </summary>
    public static class SketchMediationPrivacy
    {
        public const string LIFTOFF_TYPE = "GoogleMobileAds.Mediation.LiftoffMonetize.Api.LiftoffMonetize, GoogleMobileAds.Mediation.LiftoffMonetize.Api";
        public const string UNITY_ADS_TYPE = "GoogleMobileAds.Mediation.UnityAds.Api.UnityAds, GoogleMobileAds.Mediation.UnityAds.Api";
        public const string BIGO_TYPE = "GoogleMobileAds.Mediation.Bigo.Api.Bigo, GoogleMobileAds.Mediation.Bigo.Api";

        /// <summary>Calls to make before MobileAds.Initialize.</summary>
        /// <param name="ageSignalActive">Require Age Signal is on and the bracket decided the signals.</param>
        /// <param name="restrictSale">Opt out of data sale / sharing (minors, unknown age).</param>
        /// <param name="nonBehavioral">Contextual ads only (minors, unknown age).</param>
        /// <param name="gppUsOptOut">SketchGpp.UsOptOut of the stored UMP signal: true = US opt-out of sale/sharing/targeting.</param>
        public static IReadOnlyList<SketchMediationPrivacyCall> Plan(bool ageSignalActive, bool restrictSale, bool nonBehavioral, bool? gppUsOptOut = null)
        {
            if (gppUsOptOut == true)
            {
                restrictSale = true;
                nonBehavioral = true;
            }
            var calls = new List<SketchMediationPrivacyCall>(4);
            if (restrictSale)
            {
                calls.Add(new SketchMediationPrivacyCall("Liftoff", LIFTOFF_TYPE, "SetCCPAStatus", false));
                calls.Add(new SketchMediationPrivacyCall("UnityAds", UNITY_ADS_TYPE, "SetConsentMetaData", "privacy.consent", false));
                calls.Add(new SketchMediationPrivacyCall("BIGO", BIGO_TYPE, "SetCcpaConsent", false));
            }
            if (ageSignalActive)
                calls.Add(new SketchMediationPrivacyCall("UnityAds", UNITY_ADS_TYPE, "SetConsentMetaData", "user.nonbehavioral", nonBehavioral));
            return calls;
        }
    }
}
