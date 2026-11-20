namespace Sketch.Sdk
{
    /// <summary>How a started update flow ended. A successful immediate update restarts the app and never reports.</summary>
    public enum SketchAppUpdateResult
    {
        /// <summary>Flexible update downloaded; call CompleteUpdate to install and restart.</summary>
        Downloaded = 0,
        Canceled = 1,
        Failed = 2,
    }
}
