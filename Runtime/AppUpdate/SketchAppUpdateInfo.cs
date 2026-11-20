namespace Sketch.Sdk
{
    /// <summary>Store answer about an available update (provider-neutral).</summary>
    public struct SketchAppUpdateInfo
    {
        public bool UpdateAvailable;
        public int AvailableVersionCode;
        public bool FlexibleAllowed;
        public bool ImmediateAllowed;
    }
}
