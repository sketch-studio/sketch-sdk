namespace Sketch.Sdk
{
    /// <summary>Remote config keys for local notifications. Missing key = enabled.</summary>
    public static class SketchNotificationsRemoteKeys
    {
        /// <summary>Kill switch: false cancels everything and schedules nothing.</summary>
        public const string ENABLED = "sketch_notifications_enabled";

        public static bool IsEnabled(SketchConfigValues values) => values == null || values.GetBool(ENABLED, true);
    }
}
