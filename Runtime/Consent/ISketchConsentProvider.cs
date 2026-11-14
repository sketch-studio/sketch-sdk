using System;

namespace Sketch.Sdk
{
    public sealed class SketchConsentRequest
    {
        public SketchConsentDebugGeography DebugGeography { get; set; }
        public string[] TestDeviceIds { get; set; } = Array.Empty<string>();
        public bool TagForUnderAgeOfConsent { get; set; }
    }

    /// <summary>Consent backend (Google UMP in the AdMob module). Callbacks on the main thread, exactly once.</summary>
    public interface ISketchConsentProvider
    {
        /// <summary>Refresh consent info and show the form when required. onDone always fires, success or not.</summary>
        void Gather(SketchConsentRequest request, Action onDone);
        bool CanRequestAds { get; }
        bool IsPrivacyOptionsRequired { get; }
        void ShowPrivacyOptions(Action<string> onClosed);
        void Reset();
    }
}
