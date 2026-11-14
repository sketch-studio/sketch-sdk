using System;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Game-facing consent API. The SDK gathers consent on launch before ads; games only need the privacy button:
    /// <code>privacyButton.SetActive(SketchConsent.IsPrivacyOptionsRequired); ... SketchConsent.ShowPrivacyOptions();</code>
    /// </summary>
    public static class SketchConsent
    {
        static ISketchConsentProvider _provider;
        static SketchConsentRequest _request;
        static bool _regathering;

        /// <summary>Raised on the main thread after the privacy options form closes or consent was gathered again.</summary>
        public static event Action Changed;

        public static bool IsGathered { get; private set; }
        /// <summary>True when ads may be requested (no provider = consent not managed by the SDK = true).</summary>
        public static bool CanRequestAds => _provider == null || _provider.CanRequestAds;
        /// <summary>Show a "Privacy settings" button in Settings when true (required by Google for EEA/UK users).</summary>
        public static bool IsPrivacyOptionsRequired => _provider != null && _provider.IsPrivacyOptionsRequired;

        public static void ShowPrivacyOptions(Action onClosed = null)
        {
            if (_provider == null) { onClosed?.Invoke(); return; }
            try
            {
                _provider.ShowPrivacyOptions(error =>
                {
                    if (!string.IsNullOrEmpty(error)) SketchLog.Warn("Privacy options: " + error);
                    SketchAnalytics.Log("consent_privacy_options", "error", error ?? string.Empty);
                    RaiseChanged();
                    onClosed?.Invoke();
                });
            }
            catch (Exception e)
            {
                SketchLog.Warn("Privacy options failed: " + e.Message);
                onClosed?.Invoke();
            }
        }

        /// <summary>Testing only: forget the stored consent so the form shows again on next launch.</summary>
        public static void ResetForTesting()
        {
            try { _provider?.Reset(); } catch (Exception e) { SketchLog.Warn("Consent reset failed: " + e.Message); }
        }

        /// <summary>Ask again (e.g. on resume after the form failed offline). onDone fires once.</summary>
        internal static void Regather(Action onDone)
        {
            if (_provider == null || _regathering) return;
            _regathering = true;
            bool done = false;
            void Finish()
            {
                if (done) return;
                done = true;
                _regathering = false;
                RaiseChanged();
                onDone?.Invoke();
            }
            try { _provider.Gather(_request ?? new SketchConsentRequest(), () => SketchSdkLoop.Run(Finish)); }
            catch (Exception e) { SketchLog.Warn("Consent regather failed: " + e.Message); Finish(); }
        }

        static void RaiseChanged()
        {
            try { Changed?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }

        internal static void Gather(ISketchConsentProvider provider, SketchConsentRequest request, Action onDone)
        {
            _provider = provider;
            _request = request;
            if (provider == null)
            {
                IsGathered = true;
                onDone?.Invoke();
                return;
            }
            bool done = false;
            void Finish()
            {
                if (done) return;
                done = true;
                IsGathered = true;
                SketchLog.Info($"Consent gathered: canRequestAds={CanRequestAds}, privacyOptionsRequired={IsPrivacyOptionsRequired}");
                SketchAnalytics.Log("consent_result", "can_request_ads", CanRequestAds, "privacy_options_required", IsPrivacyOptionsRequired);
                onDone?.Invoke();
            }
            try { provider.Gather(request, () => SketchSdkLoop.Run(Finish)); }
            catch (Exception e)
            {
                SketchLog.Warn("Consent gather failed, continuing to ads: " + e.Message);
                Finish();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _provider = null;
            _request = null;
            _regathering = false;
            Changed = null;
            IsGathered = false;
        }
    }
}
