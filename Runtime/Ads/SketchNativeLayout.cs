namespace Sketch.Sdk
{
    public readonly struct SketchNativeLayout
    {
        public readonly float WidthDp;
        public readonly float HeightDp;
        public readonly bool Top;
        public readonly bool HasRect;
        public readonly float XDp;
        public readonly float YDp;

        public static System.Func<float> DeviceScaleProvider { get; set; }
        public static float DeviceScale
        {
            get
            {
                if (DeviceScaleProvider != null)
                {
                    float scale = DeviceScaleProvider();
                    if (scale > 0f) return scale;
                }
                return UnityEngine.Screen.dpi > 0f ? UnityEngine.Screen.dpi / 160f : 1f;
            }
        }

        public static SketchNativeLayout FromScreenRect(UnityEngine.Rect pixels, float screenHeight) =>
            FromScreenRect(pixels, screenHeight, DeviceScale);

        public static SketchNativeLayout FromScreenRect(UnityEngine.Rect pixels, float screenHeight, float deviceScale)
        {
            float scale = UnityEngine.Mathf.Max(0.0001f, deviceScale);
            return new SketchNativeLayout(pixels.xMin / scale, (screenHeight - pixels.yMax) / scale,
                pixels.width / scale, pixels.height / scale);
        }

        public SketchNativeLayout(float widthDp, float heightDp, bool top)
        {
            WidthDp = widthDp;
            HeightDp = heightDp;
            Top = top;
            HasRect = false;
            XDp = 0f;
            YDp = 0f;
        }

        public SketchNativeLayout(float xDp, float yDp, float widthDp, float heightDp)
        {
            WidthDp = widthDp;
            HeightDp = heightDp;
            Top = false;
            HasRect = true;
            XDp = xDp;
            YDp = yDp;
        }
    }
}
