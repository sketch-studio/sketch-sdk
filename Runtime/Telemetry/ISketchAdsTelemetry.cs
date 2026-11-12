namespace Sketch.Sdk
{
    /// <summary>Analytics adapter boundary; implemented by the consuming game.</summary>
    public interface ISketchAdsTelemetry
    {
        string PlacementFor(string format);
        void Track(SketchAdTelemetryEvent adEvent);
        void TrackRevenue(SketchAdRevenue revenue);
    }
}
