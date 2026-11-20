namespace Sketch.Sdk
{
    public enum SketchFakeStoreOutcome
    {
        Success,
        Fail,
        Cancel,
        /// <summary>Reports Deferred first, then delivers the paid order one dispatch later.</summary>
        DeferredThenSuccess,
    }
}
