using UnityEngine;

namespace Sketch.Sdk.Splash
{
    /// <summary>Pure layout maths for a 9-slice sprite drawn over the filled part of a bar.</summary>
    public static class SketchSlicedFill
    {
        /// <summary>The part of <paramref name="rect"/> covered by <paramref name="fill"/> (0..1). Origin 1 = right/top.</summary>
        public static Rect FilledRect(Rect rect, float fill, bool horizontal, int origin)
        {
            float f = Mathf.Clamp01(fill);
            if (horizontal)
            {
                float w = rect.width * f;
                if (origin == 1) rect.xMin = rect.xMax - w; else rect.width = w;
            }
            else
            {
                float h = rect.height * f;
                if (origin == 1) rect.yMin = rect.yMax - h; else rect.height = h;
            }
            return rect;
        }

        /// <summary>Keeps the caps (L, B, R, T) at full size when they fit; scales a pair evenly when the part is shorter.</summary>
        public static Vector4 FitBorders(Vector4 border, float width, float height)
        {
            float bx = border.x + border.z, by = border.y + border.w;
            if (bx > width && bx > 0f) { float k = width / bx; border.x *= k; border.z *= k; }
            if (by > height && by > 0f) { float k = height / by; border.y *= k; border.w *= k; }
            return border;
        }
    }
}
