using System.Reflection;
using NUnit.Framework;
using Sketch.Sdk.Splash;
using UnityEngine;
using UnityEngine.UI;

namespace Sketch.Sdk.Tests
{
    public sealed class SketchSplashLookTests
    {
        [Test]
        public void Hook_Defaults_KeepThePreviousLook()
        {
            var hook = ScriptableObject.CreateInstance<SketchSplashHook>();
            try
            {
                Assert.IsNull(hook.BarBackgroundSprite);
                Assert.IsNull(hook.BarFillSprite);
                Assert.IsNull(hook.BarLabelPrefab);
                Assert.AreEqual(new Color(0f, 0f, 0f, 0.45f), hook.BarBackgroundColor);
                Assert.AreEqual(new Vector2(720f, 40f), hook.BarSize);
                Assert.AreEqual(0.14f, hook.BarAnchorY, 1e-5f);
                Assert.AreEqual(4f, hook.BarFillInset, 1e-5f);
                Assert.AreEqual(12f, hook.BarLabelGap, 1e-5f);
                Assert.AreEqual(80f, hook.BarLabelHeight, 1e-5f);
                Assert.AreEqual(new Vector2(600f, 600f), hook.BrandSize);
                Assert.AreEqual(0.56f, hook.BrandAnchorY, 1e-5f);
                Assert.IsTrue(hook.BackgroundEnvelope);
            }
            finally { Object.DestroyImmediate(hook); }
        }

        [Test]
        public void FilledRect_Horizontal_LeftAndRightOrigin()
        {
            var r = new Rect(-360f, -28f, 720f, 56f);
            Rect left = SketchSlicedFill.FilledRect(r, 0.15f, true, 0);
            Assert.AreEqual(-360f, left.xMin, 1e-3f);
            Assert.AreEqual(108f, left.width, 1e-3f);
            Assert.AreEqual(56f, left.height, 1e-3f);
            Rect right = SketchSlicedFill.FilledRect(r, 0.25f, true, 1);
            Assert.AreEqual(360f, right.xMax, 1e-3f);
            Assert.AreEqual(180f, right.width, 1e-3f);
            Rect clamped = SketchSlicedFill.FilledRect(r, 2f, true, 0);
            Assert.AreEqual(720f, clamped.width, 1e-3f);
        }

        [Test]
        public void FitBorders_KeepsCapsWhenWide_ScalesEvenlyWhenShort()
        {
            var border = new Vector4(20f, 0f, 20f, 0f); // L, B, R, T in canvas units
            Assert.AreEqual(border, SketchSlicedFill.FitBorders(border, 108f, 56f));
            Vector4 squeezed = SketchSlicedFill.FitBorders(border, 20f, 56f);
            Assert.AreEqual(10f, squeezed.x, 1e-4f);
            Assert.AreEqual(10f, squeezed.z, 1e-4f);
            Assert.AreEqual(0f, squeezed.y, 1e-4f);
        }

        [Test]
        public void SlicedFillImage_LowFill_KeepsFullCaps_ZeroFillDrawsNothing()
        {
            var tex = new Texture2D(320, 112, TextureFormat.RGBA32, false);
            var go = new GameObject("fill", typeof(RectTransform));
            try
            {
                // 320x112 px at ppu 200 with 40 px L/R caps = 160x56 canvas units (reference ppu 100), caps 20 units.
                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 320, 112), new Vector2(0.5f, 0.5f), 200f, 0, SpriteMeshType.FullRect, new Vector4(40f, 0f, 40f, 0f));
                var image = go.AddComponent<SketchSlicedFillImage>();
                image.sprite = sprite;
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = 0;
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(720f, 56f);

                image.fillAmount = 0f;
                Assert.AreEqual(0, Populate(image).currentVertCount);

                image.fillAmount = 0.15f;
                VertexHelper vh = Populate(image);
                Assert.AreEqual(3 * 4, vh.currentVertCount); // 3 columns x 1 row: T/B borders are 0, so empty rows are skipped
                float minX = float.MaxValue, maxX = float.MinValue, capEdge = float.MaxValue;
                var v = new UIVertex();
                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    vh.PopulateUIVertex(ref v, i);
                    minX = Mathf.Min(minX, v.position.x);
                    maxX = Mathf.Max(maxX, v.position.x);
                    if (v.position.x > -360f + 1e-3f) capEdge = Mathf.Min(capEdge, v.position.x);
                }
                Assert.AreEqual(-360f, minX, 1e-3f);
                Assert.AreEqual(-360f + 108f, maxX, 1e-3f);
                Assert.AreEqual(-360f + 20f, capEdge, 1e-3f, "left cap keeps its full width at 15%");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(tex);
            }
        }

        static VertexHelper Populate(Graphic graphic)
        {
            var vh = new VertexHelper();
            MethodInfo m = typeof(Graphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null);
            m.Invoke(graphic, new object[] { vh });
            return vh;
        }
    }
}
