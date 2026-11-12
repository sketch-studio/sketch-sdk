namespace Sketch.Sdk
{
    /// <summary>Consumer analytics boundary for a verified store transaction.</summary>
    public interface ISketchIapTelemetry
    {
        void CaptureIap(SketchIapCapture purchase);
    }
}
