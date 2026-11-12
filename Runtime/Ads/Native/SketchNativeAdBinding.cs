namespace Sketch.Sdk
{
    /// <summary>Binds native callbacks to the ad instance and request generation that created them.</summary>
    public sealed class SketchNativeAdBinding
    {
        readonly object _adIdentity;
        readonly int _generation;

        public SketchNativeAdBinding(object adIdentity, int generation)
        {
            _adIdentity = adIdentity;
            _generation = generation;
        }

        public bool Matches(object eventAd, object activeAd, int activeGeneration) =>
            _generation == activeGeneration && ReferenceEquals(_adIdentity, eventAd) && ReferenceEquals(eventAd, activeAd);
    }
}
