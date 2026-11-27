using NUnit.Framework;
using UnityEngine;

namespace Sketch.Sdk.Tests
{
    // 1.5.14: portrait top band (loading-screen native) and moving the native that is on screen.
    public sealed class SketchNativeTopBandMoveTests
    {
        [Test]
        public void TopBand_1080x2400_Bar534_Pad50()
        {
            Rect band = SketchNativeFit.TopBand(new Rect(0f, 0f, 1080f, 2400f), 2400f, 534f, 50f);
            Assert.That(band, Is.EqualTo(new Rect(50f, 584f, 980f, 1766f)));
        }

        [Test]
        public void TopBand_RespectsSafeAreaInsets()
        {
            Rect band = SketchNativeFit.TopBand(new Rect(0f, 60f, 1080f, 2240f), 2400f, 534f, 50f);
            Assert.That(band.xMin, Is.EqualTo(50f));
            Assert.That(band.xMax, Is.EqualTo(1030f));
            Assert.That(band.yMax, Is.EqualTo(2250f), "safe top 2300 minus the padding");
            Assert.That(band.yMin, Is.EqualTo(584f));
        }

        [Test]
        public void TopBand_BarBelowTheSafeArea_StartsAtTheSafeBottom()
        {
            Rect band = SketchNativeFit.TopBand(new Rect(0f, 100f, 1080f, 2200f), 2400f, 40f, 50f);
            Assert.That(band.yMin, Is.EqualTo(150f));
        }

        [Test]
        public void TopBand_TooShort_ReturnsZero()
        {
            Rect band = SketchNativeFit.TopBand(new Rect(0f, 0f, 1080f, 2400f), 2400f, 2000f, 50f);
            Assert.That(band.width, Is.EqualTo(0f));
            Assert.That(band.height, Is.EqualTo(0f));
        }

        [Test]
        public void TopBand_NegativePadding_IsTreatedAsZero()
        {
            Rect band = SketchNativeFit.TopBand(new Rect(0f, 0f, 1080f, 1920f), 1920f, 500f, -10f);
            Assert.That(band, Is.EqualTo(new Rect(0f, 500f, 1080f, 1420f)));
        }

        [Test]
        public void FakeProvider_Move_NoNativeShown_ReturnsFalse()
        {
            var provider = new SketchEditorFakeProvider();
            Assert.That(provider.MoveNative(new Rect(0f, 500f, 400f, 400f)), Is.False);
        }

        [Test]
        public void FakeProvider_IsAMoveCapability()
        {
            Assert.That(new SketchEditorFakeProvider(), Is.InstanceOf<ISketchNativeMoveCapability>());
        }

        [Test]
        public void SdkMove_NoNativeUp_ReturnsFalse()
        {
            Assert.That(SketchSdk.IsNativeVisible, Is.False);
            Assert.That(SketchSdk.MoveNative(new Rect(0f, 500f, 400f, 400f)), Is.False);
        }

        [Test]
        public void SdkMove_EmptyRect_ReturnsFalse()
        {
            Assert.That(SketchSdk.MoveNative(new Rect(0f, 0f, 0f, 400f)), Is.False);
        }
    }
}
