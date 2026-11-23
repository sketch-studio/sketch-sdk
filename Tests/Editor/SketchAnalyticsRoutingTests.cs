using NUnit.Framework;

namespace Sketch.Sdk.Tests
{
    // OR-873 (owner): a Development Build is a test build. Its analytics go to the DEV server only and never to
    // Firebase or the production server, so the live dashboard keeps real players only.
    public sealed class SketchAnalyticsRoutingTests
    {
        static SketchSdkSettings Settings()
        {
            var s = UnityEngine.ScriptableObject.CreateInstance<SketchSdkSettings>();
            s.ServerUrl = "https://api.sketchapi.win/api/v1";
            s.DevServerUrl = "https://dev-api.sketchapi.win/api/v1";
            return s;
        }

        [Test]
        public void ReleaseBuild_UsesProductionServer_AndExternalSinks()
        {
            var s = Settings();
            Assert.AreEqual("https://api.sketchapi.win/api/v1", SketchAnalyticsRouting.ServerUrl(s, developmentBuild: false));
            Assert.IsTrue(SketchAnalyticsRouting.ExternalSinksAllowed(developmentBuild: false));
        }

        [Test]
        public void DevelopmentBuild_UsesDevServer_AndNoFirebase()
        {
            var s = Settings();
            Assert.AreEqual("https://dev-api.sketchapi.win/api/v1", SketchAnalyticsRouting.ServerUrl(s, developmentBuild: true));
            Assert.IsFalse(SketchAnalyticsRouting.ExternalSinksAllowed(developmentBuild: true));
        }

        [Test]
        public void DevelopmentBuild_WithoutDevServer_SendsNothing()
        {
            var s = Settings();
            s.DevServerUrl = "";
            Assert.IsNull(SketchAnalyticsRouting.ServerUrl(s, developmentBuild: true), "never falls back to production");
        }
    }
}
