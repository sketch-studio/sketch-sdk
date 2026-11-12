namespace Sketch.Sdk
{
    public enum SketchNativeCloseReason
    {
        User = 0,
        Teardown,
        SdkClosed,
        PopupConflict,
        LayoutChanged,
        LoadFailed,
        AdsRemoved,
        GuardWindow,
    }
}
