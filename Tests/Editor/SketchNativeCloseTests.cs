using System.Collections.Generic;
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

        [TearDown]
        public void TearDown() => SketchNativeClose.RestoreDefaults();

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

        // OR-879: the close sits on the RIGHT by default (no 3:1 alternation unless a game opts in).
        [Test]
        public void Default_CloseAlwaysOnTheRight()
        {
            SketchNativeClose.RestoreDefaults();
            Assert.IsFalse(SketchNativeClose.MoveEnabled);
            for (int i = 0; i < 8; i++) Assert.IsFalse(SketchNativeClose.NextOnLeft());
        }

        // OR-879: countdown default 3 s, tunable by remote key sketch_native_close_delay, clamped to 0..MAX.
        [TestCase(3f, 3), TestCase(0f, 0), TestCase(-2f, 0), TestCase(1.4f, 1), TestCase(2.6f, 3), TestCase(99f, SketchNativeClose.MAX_COUNTDOWN_SECONDS),
         TestCase(float.NaN, SketchNativeClose.DEFAULT_COUNTDOWN_SECONDS), TestCase(float.PositiveInfinity, SketchNativeClose.DEFAULT_COUNTDOWN_SECONDS)]
        public void ResolveCountdown_ClampsAndRounds(float seconds, int expected)
        {
            Assert.AreEqual(expected, SketchNativeClose.ResolveCountdown(seconds));
        }

        [Test]
        public void Countdown_ReadsRemoteKey_FallsBackToSettingsDefault()
        {
            var values = new SketchConfigValues();
            Assert.AreEqual(3, SketchNativeClose.CountdownFrom(values, 3f), "no key: settings default");
            Assert.AreEqual(0, SketchNativeClose.CountdownFrom(values, 0f), "game default 0 s");
            values.SetRemote(new Dictionary<string, object> { { SketchAdsRemoteKeys.NATIVE_CLOSE_DELAY, 0.0 } }, "c");
            Assert.AreEqual(0, SketchNativeClose.CountdownFrom(values, 3f), "remote 0 wins");
            values.SetRemote(new Dictionary<string, object> { { SketchAdsRemoteKeys.NATIVE_CLOSE_DELAY, 5.0 } }, "c");
            Assert.AreEqual(5, SketchNativeClose.CountdownFrom(values, 0f), "remote 5 wins over a 0 default");
            values.SetRemote(new Dictionary<string, object> { { SketchAdsRemoteKeys.NATIVE_CLOSE_DELAY, -4.0 } }, "c");
            Assert.AreEqual(0, SketchNativeClose.CountdownFrom(values, 3f), "negative remote clamps to 0");
        }

        [Test]
        public void SdkKeys_DeclareNativeCloseDelay_WithSettingsDefault()
        {
            var settings = ScriptableObject.CreateInstance<SketchSdkSettings>();
            try
            {
                Assert.AreEqual(3f, settings.NativeCloseDelaySeconds, "SDK default 3 s");
                settings.NativeCloseDelaySeconds = 0f;
                var key = SketchRemoteConfig.SdkKeys(settings).Find(k => k.Key == SketchAdsRemoteKeys.NATIVE_CLOSE_DELAY);
                Assert.NotNull(key, "declared as an SDK remote key");
                Assert.AreEqual(SketchRemoteValueType.Float, key.Type);
                Assert.AreEqual("0", key.DefaultValue);
                Assert.AreEqual("sketch_native_close_delay", SketchAdsRemoteKeys.NATIVE_CLOSE_DELAY);
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void Countdown_IsThreeSeconds()
        {
            Assert.AreEqual(3, SketchNativeClose.COUNTDOWN_SECONDS);
            Assert.AreEqual(3, SketchNativeClose.DEFAULT_COUNTDOWN_SECONDS);
        }
    }
}
