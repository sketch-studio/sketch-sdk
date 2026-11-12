using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>Provider-side rectangle clamping and AdMob coordinate conversion.</summary>
    public static class SketchNativeInRect
    {
        public static Rect ClampToBanner(Rect pixels, float screenHeight, float bannerHeightPixels)
        {
            float banner = Mathf.Max(0f, bannerHeightPixels);
            float top = Mathf.Max(banner, screenHeight);
            float yMin = Mathf.Clamp(pixels.yMin, banner, top);
            float yMax = Mathf.Clamp(pixels.yMax, yMin, top);
            if (pixels.width <= 0f || yMax - yMin <= 0f) return new Rect(pixels.xMin, yMin, 0f, 0f);
            return Rect.MinMaxRect(pixels.xMin, yMin, pixels.xMax, yMax);
        }

        // AdMob's custom template uses top-left dp offsets; Android sizes are pixel LayoutParams, iOS sizes are points.
        public static void ToAdMob(Rect clamped, float screenHeight, float deviceScale, bool sizeInPixels, out int xDp, out int yDp, out int width, out int height)
        {
            float scale = Mathf.Max(0.0001f, deviceScale);
            xDp = Mathf.RoundToInt(clamped.xMin / scale);
            yDp = Mathf.RoundToInt((screenHeight - clamped.yMax) / scale);
            float sizeScale = sizeInPixels ? 1f : 1f / scale;
            width = Mathf.Max(1, Mathf.RoundToInt(clamped.width * sizeScale));
            height = Mathf.Max(1, Mathf.RoundToInt(clamped.height * sizeScale));
        }
    }
}
