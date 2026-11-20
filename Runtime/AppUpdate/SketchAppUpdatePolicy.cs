namespace Sketch.Sdk
{
    /// <summary>Pure rule: which update flow to start. No IO.</summary>
    public static class SketchAppUpdatePolicy
    {
        /// <summary>Remote key: installs below this Android versionCode get the immediate (blocking) update. 0 or missing = off.</summary>
        public const string FORCE_MIN_VERSION_CODE_KEY = "sketch_force_update_min_version_code";

        /// <summary>True when a known version code is below a configured minimum.</summary>
        public static bool IsForced(int currentVersionCode, int forceMinVersionCode) =>
            forceMinVersionCode > 0 && currentVersionCode > 0 && currentVersionCode < forceMinVersionCode;

        /// <summary>
        /// No update -> None. Forced and immediate allowed -> Immediate. Otherwise Flexible when allowed
        /// (a forced install whose store does not allow immediate still gets the flexible prompt), else None.
        /// </summary>
        public static SketchAppUpdateDecision Decide(SketchAppUpdateInfo info, int currentVersionCode, int forceMinVersionCode)
        {
            if (!info.UpdateAvailable) return SketchAppUpdateDecision.None;
            if (IsForced(currentVersionCode, forceMinVersionCode) && info.ImmediateAllowed) return SketchAppUpdateDecision.Immediate;
            return info.FlexibleAllowed ? SketchAppUpdateDecision.Flexible : SketchAppUpdateDecision.None;
        }
    }
}
