using NUnit.Framework;

namespace Sketch.Sdk.Tests
{
    // OR-820: the server/dashboard version is the bundle version code (1, 2, 3...).
    public sealed class SketchAppVersionTests
    {
        [Test] public void Resolve_PrefersVersionCode() => Assert.AreEqual("3", SketchAppVersion.Resolve(3, "1.3"));
        [Test] public void Resolve_FallsBackToName_WhenCodeUnknown() => Assert.AreEqual("1.3", SketchAppVersion.Resolve(0, "1.3"));
        [Test] public void Code_InEditor_IsAndroidBundleVersionCode() =>
            Assert.AreEqual(UnityEditor.PlayerSettings.Android.bundleVersionCode.ToString(), SketchAppVersion.Code);
    }
}
