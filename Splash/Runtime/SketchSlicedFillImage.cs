using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Sketch.Sdk.Splash
{
    /// <summary>
    /// Stock Image draws a Filled sprite as one stretched quad, so a bordered (9-slice) bar loses its rounded caps.
    /// This Image lays the 9-slice over the filled part only (Filled + Horizontal/Vertical, fillOrigin respected).
    /// Any other type/method, or a sprite without a border, uses the stock mesh.
    /// </summary>
    public sealed class SketchSlicedFillImage : Image
    {
        static readonly float[] Xs = new float[4], Ys = new float[4], Us = new float[4], Vs = new float[4];

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            Sprite s = overrideSprite;
            bool horizontal = fillMethod == FillMethod.Horizontal;
            bool sliced = type == Type.Filled && s != null && s.border != Vector4.zero && (horizontal || fillMethod == FillMethod.Vertical);
            if (!sliced) { base.OnPopulateMesh(vh); return; }

            vh.Clear();
            if (fillAmount <= 0f) return;

            Rect r = SketchSlicedFill.FilledRect(GetPixelAdjustedRect(), fillAmount, horizontal, fillOrigin);
            float ppu = pixelsPerUnit * pixelsPerUnitMultiplier;
            Vector4 b = SketchSlicedFill.FitBorders(ppu > 0f ? s.border / ppu : s.border, r.width, r.height); // L, B, R, T
            Vector4 outer = DataUtility.GetOuterUV(s), inner = DataUtility.GetInnerUV(s);

            Xs[0] = r.xMin; Xs[1] = r.xMin + b.x; Xs[2] = r.xMax - b.z; Xs[3] = r.xMax;
            Ys[0] = r.yMin; Ys[1] = r.yMin + b.y; Ys[2] = r.yMax - b.w; Ys[3] = r.yMax;
            Us[0] = outer.x; Us[1] = inner.x; Us[2] = inner.z; Us[3] = outer.z;
            Vs[0] = outer.y; Vs[1] = inner.y; Vs[2] = inner.w; Vs[3] = outer.w;

            Color32 c = color;
            for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
            {
                if (!fillCenter && x == 1 && y == 1) continue;
                if (Xs[x + 1] - Xs[x] <= 0f || Ys[y + 1] - Ys[y] <= 0f) continue;
                int i = vh.currentVertCount;
                vh.AddVert(new Vector3(Xs[x], Ys[y]), c, new Vector2(Us[x], Vs[y]));
                vh.AddVert(new Vector3(Xs[x], Ys[y + 1]), c, new Vector2(Us[x], Vs[y + 1]));
                vh.AddVert(new Vector3(Xs[x + 1], Ys[y + 1]), c, new Vector2(Us[x + 1], Vs[y + 1]));
                vh.AddVert(new Vector3(Xs[x + 1], Ys[y]), c, new Vector2(Us[x + 1], Vs[y]));
                vh.AddTriangle(i, i + 1, i + 2);
                vh.AddTriangle(i + 2, i + 3, i);
            }
        }
    }
}
