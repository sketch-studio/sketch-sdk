using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Close control of the native in-rect (same on device and in the Editor fake): a 26 dp round countdown close,
    /// 6 dp inside the top corner of the native panel, 3 s countdown, then only the user closes the ad.
    /// Position alternates 3 right : 1 left (every 4th show on the left) when MoveEnabled.
    /// </summary>
    public static class SketchNativeClose
    {
        public const int COUNTDOWN_SECONDS = 3;
        public const float SIZE_DP = 26f;
        public const float MARGIN_DP = 6f;
        // Typical phone width in dp; the Editor derives a device-like density from the Game View width with it.
        public const float REFERENCE_SHORT_SIDE_DP = 411f;

        public static bool MoveEnabled { get; set; } = true;
        public static bool CountdownEnabled { get; set; } = true;

        static int _shows;

        public static int ShowCount => _shows;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetShows() => _shows = 0;

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
