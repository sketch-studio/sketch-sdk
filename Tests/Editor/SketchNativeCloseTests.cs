using NUnit.Framework;
using UnityEngine;

namespace Sketch.Sdk.Tests
{
    // Native close control shared by the Android layout and the Editor fake (KT ThunderAdmobNative parity).
    public sealed class SketchNativeCloseTests
    {
        [SetUp]
        public void SetUp()
        {
            SketchNativeClose.ResetShows();
            SketchNativeClose.MoveEnabled = true;
        }

        [Test]
        public void Alternates_ThreeRight_OneLeft()
        {
            var sides = new bool[8];
            for (int i = 0; i < sides.Length; i++) sides[i] = SketchNativeClose.NextOnLeft();
            CollectionAssert.AreEqual(new[] { false, false, false, true, false, false, false, true }, sides);
        }

        [Test]
        public void MoveDisabled_AlwaysRight()
        {
            SketchNativeClose.MoveEnabled = false;
            for (int i = 0; i < 8; i++) Assert.IsFalse(SketchNativeClose.NextOnLeft());
        }

        [Test]
        public void CloseRect_Is26dpWith6dpMargin_InTheTopCorner()
        {
            var panel = new Rect(0f, 1200f, 1080f, 864f);
            const float density = 2.625f;
            Rect right = SketchNativeClose.CloseRect(panel, density, false);
            Assert.AreEqual(Mathf.Round(26f * density), right.width, 0.01f);
            Assert.AreEqual(right.width, right.height, 0.01f);
            float margin = Mathf.Round(6f * density);
            Assert.AreEqual(panel.xMax - margin, right.xMax, 0.01f, "6 dp from the right edge");
            Assert.AreEqual(panel.yMax - margin, right.yMax, 0.01f, "6 dp from the top edge");
            Rect left = SketchNativeClose.CloseRect(panel, density, true);
            Assert.AreEqual(panel.xMin + margin, left.xMin, 0.01f, "mirrored on the left");
            Assert.AreEqual(right.yMin, left.yMin, 0.01f);
        }

        [Test]
        public void Countdown_IsThreeSeconds()
        {
            Assert.AreEqual(3, SketchNativeClose.COUNTDOWN_SECONDS);
        }
    }
}
