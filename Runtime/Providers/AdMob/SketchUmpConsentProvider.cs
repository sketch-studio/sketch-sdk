#if SKETCHSDK_ADMOB
using System;
using System.Collections.Generic;
using GoogleMobileAds.Ump.Api;

namespace Sketch.Sdk
{
    /// <summary>
    /// Google User Messaging Platform: Update -> LoadAndShowConsentFormIfRequired. onDone fires once whatever happens,
    /// so ads always initialize (Google serves limited / non-personalized ads where consent is missing).
    /// </summary>
    public sealed class SketchUmpConsentProvider : ISketchConsentProvider
    {
        public bool CanRequestAds
        {
            get { try { return ConsentInformation.CanRequestAds(); } catch { return true; } }
        }

        public bool IsPrivacyOptionsRequired
        {
            get
            {
                try { return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required; }
                catch { return false; }
            }
        }

        public void Gather(SketchConsentRequest request, Action onDone)
        {
            bool done = false;
            void Done(string error)
            {
                SketchSdkLoop.Run(() =>
                {
                    if (done) return;
                    done = true;
                    if (!string.IsNullOrEmpty(error)) SketchLog.Warn("UMP: " + error);
                    onDone?.Invoke();
                });
            }

            var parameters = new ConsentRequestParameters
            {
                TagForUnderAgeOfConsent = request != null && request.TagForUnderAgeOfConsent,
            };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (request != null && request.DebugGeography != SketchConsentDebugGeography.Disabled)
            {
                parameters.ConsentDebugSettings = new ConsentDebugSettings
                {
                    DebugGeography = request.DebugGeography == SketchConsentDebugGeography.EEA ? DebugGeography.EEA : DebugGeography.NotEEA,
                    TestDeviceHashedIds = new List<string>(request.TestDeviceIds ?? Array.Empty<string>()),
                };
            }
#endif
            try
            {
                ConsentInformation.Update(parameters, updateError =>
                {
                    if (updateError != null)
                    {
                        // Keep the previous session's consent; ads still start.
                        Done("consent info update failed: " + updateError.Message);
                        return;
                    }
                    SketchSdkLoop.Run(() =>
                    {
                        try
                        {
                            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                                Done(formError != null ? "consent form failed: " + formError.Message : null));
                        }
                        catch (Exception e) { Done("consent form threw: " + e.Message); }
                    });
                });
            }
            catch (Exception e)
            {
                Done("consent update threw: " + e.Message);
            }
        }

        public void ShowPrivacyOptions(Action<string> onClosed)
        {
            ConsentForm.ShowPrivacyOptionsForm(error => SketchSdkLoop.Run(() => onClosed?.Invoke(error?.Message)));
        }

        public void Reset() => ConsentInformation.Reset();
    }
}
#endif
