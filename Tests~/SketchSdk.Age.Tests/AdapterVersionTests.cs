using Sketch.Sdk;

static class AdapterVersionTests
{
    public static int Run()
    {
        int n = 0;
        void Check(bool condition, string message)
        {
            n++;
            if (!condition) throw new Exception("FAIL: " + message);
        }

        const string xml = "<dependencies><androidPackages>" +
            "<androidPackage spec=\"com.google.ads.mediation:vungle:7.7.9.0\"></androidPackage>" +
            "<androidPackage spec=\"com.google.ads.mediation:unity:4.16.6.0\"/>" +
            "<androidPackage spec=\"com.unity3d.ads:unity-ads:4.16.6\"/>" +
            "<androidPackage spec=\"com.google.ads.mediation:inmobi:[11.5.1.0]\"/>" +
            "<androidPackage spec=\"com.google.ads.mediation:mintegral:16.9.71.0\"/>" +
            "</androidPackages></dependencies>";
        var adapters = SketchMediationAdapters.ParseDependencies(xml);
        Check(adapters.Count == 4, "only com.google.ads.mediation artifacts are adapters");

        var rows = SketchMediationAdapters.Review(adapters);
        SketchMediationAdapterReview Row(string artifact) => rows.First(r => r.Artifact == artifact);
        Check(Row("vungle").Status == SketchAdapterPrivacyStatus.Ok && Row("vungle").Network == "Liftoff", "Liftoff 7.7.9.0 >= 7.7.4.2");
        Check(Row("unity").Status == SketchAdapterPrivacyStatus.TooOld && Row("unity").MinVersion == "4.19.0.1", "Unity Ads 4.16.6.0 < 4.19.0.1 (no TCF forwarding)");
        Check(Row("inmobi").Status == SketchAdapterPrivacyStatus.Ok && Row("inmobi").Version == "11.5.1.0", "bracketed version is read");
        Check(Row("mintegral").Status == SketchAdapterPrivacyStatus.NotReviewed, "unknown network is flagged for manual review");

        Check(SketchMediationAdapters.CompareVersions("5.9.0.1", "5.10.0.0") < 0, "numeric, not text, comparison");
        Check(SketchMediationAdapters.CompareVersions("6.2.0.1", "5.9.0.1") > 0, "BIGO 6.2.0.1 is newer");
        Check(SketchMediationAdapters.CompareVersions("7.7.4.2", "7.7.4.2") == 0, "equal");
        Check(SketchMediationAdapters.Review(SketchMediationAdapters.ParseDependencies("<androidPackage spec=\"com.google.ads.mediation:bigo:5.8.0.0\"/>"))[0].Status == SketchAdapterPrivacyStatus.TooOld, "old BIGO adapter does not forward COPPA");
        Check(SketchMediationAdapters.ParseDependencies("<x>").Count == 0 && SketchMediationAdapters.ParseDependencies(null).Count == 0, "no adapters");
        return n;
    }
}
