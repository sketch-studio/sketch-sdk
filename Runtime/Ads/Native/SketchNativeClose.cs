using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Close control of the native in-rect (same on device and in the Editor fake): a 26 dp round countdown close,
    /// 6 dp inside the top-right corner of the native panel, countdown (default 3 s, remote key sketch_native_close_delay,
    /// settings NativeCloseDelaySeconds), then only the user closes the ad.
    /// The close stays on the right; a game may opt in to the 3 right : 1 left alternation with MoveEnabled.
    /// </summary>
    public static class SketchNativeClose
    {
        public const int DEFAULT_COUNTDOWN_SECONDS = 3;
        public const int MAX_COUNTDOWN_SECONDS = 30;
        /// <summary>SDK default countdown; the live value is <see cref="CountdownSeconds"/>.</summary>
        public const int COUNTDOWN_SECONDS = DEFAULT_COUNTDOWN_SECONDS;
        public const float SIZE_DP = 26f;
        public const float MARGIN_DP = 6f;
        // Typical phone width in dp; the Editor derives a device-like density from the Game View width with it.
        public const float REFERENCE_SHORT_SIDE_DP = 411f;

        // OR-879: the close is on the right unless a game opts in to the alternation.
        public static bool MoveEnabled { get; set; }
        public static bool CountdownEnabled { get; set; } = true;

        static int _shows;

        public static int ShowCount => _shows;

        public static void ResetShows() => _shows = 0;

        // Play start (also with domain reload off): a test or a previous run never leaks a left-side close.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void RestoreDefaults()
        {
            _shows = 0;
            MoveEnabled = false;
            CountdownEnabled = true;
        }

        /// <summary>Live countdown in whole seconds: remote key over the settings default (0 = close at once).</summary>
        public static int CountdownSeconds
        {
            get
            {
                SketchSdkSettings settings = SketchSdkSettings.Load();
                return CountdownFrom(SketchRemoteConfig.Store, settings != null ? settings.NativeCloseDelaySeconds : DEFAULT_COUNTDOWN_SECONDS);
            }
        }

        public static int CountdownFrom(SketchConfigValues values, float settingsDefault)
        {
            float seconds = values != null && values.Has(SketchAdsRemoteKeys.NATIVE_CLOSE_DELAY)
                ? values.GetFloat(SketchAdsRemoteKeys.NATIVE_CLOSE_DELAY, settingsDefault)
                : settingsDefault;
            return ResolveCountdown(seconds);
        }

        /// <summary>Whole seconds in 0..MAX; NaN / infinity fall back to the SDK default.</summary>
        public static int ResolveCountdown(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) return DEFAULT_COUNTDOWN_SECONDS;
            return Mathf.RoundToInt(Mathf.Clamp(seconds, 0f, MAX_COUNTDOWN_SECONDS));
        }

        /// <summary>Call once per native that is really shown; true = put the close on the top-left this time.</summary>
        public static bool NextOnLeft()
        {
            if (!MoveEnabled) return false;
            _shows++;
            return _shows % 4 == 0;
        }

        /// <summary>Close rect in Unity screen pixels (bottom-left origin) inside the native panel rect.</summary>
        public static Rect CloseRect(Rect panelPixels, float density, bool onLeft)
        {
            float d = Mathf.Max(0.0001f, density);
            float size = Mathf.Round(SIZE_DP * d);
            float margin = Mathf.Round(MARGIN_DP * d);
            float x = onLeft ? panelPixels.xMin + margin : panelPixels.xMax - margin - size;
            return new Rect(x, panelPixels.yMax - margin - size, size, size);
        }

        public static float EditorDensity(float screenWidth, float screenHeight) =>
            Mathf.Max(0.5f, Mathf.Min(screenWidth, screenHeight) / REFERENCE_SHORT_SIDE_DP);
    }
}
