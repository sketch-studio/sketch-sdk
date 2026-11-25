using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sketch.Sdk
{
    public enum SketchAdapterPrivacyStatus { Ok, TooOld, NotReviewed }

    public readonly struct SketchMediationAdapter
    {
        public readonly string Artifact;
        public readonly string Version;
        public SketchMediationAdapter(string artifact, string version)
        {
            Artifact = artifact;
            Version = version;
        }
    }

    public readonly struct SketchMediationAdapterReview
    {
        public readonly string Artifact, Network, Version, MinVersion, Reason;
        public readonly SketchAdapterPrivacyStatus Status;
        public SketchMediationAdapterReview(string artifact, string network, string version, string minVersion, SketchAdapterPrivacyStatus status, string reason)
        {
            Artifact = artifact;
            Network = network;
            Version = version;
            MinVersion = minVersion;
            Status = status;
            Reason = reason;
        }
    }

    /// <summary>
    /// Which installed AdMob mediation adapters (Android, from EDM Dependencies.xml) forward the privacy signals the SDK
    /// relies on. Minimum versions from the official Google adapter changelogs (2026-11): the newest of the TCF (GDPR),
    /// TFUA and COPPA / age-treatment forwarding entries. Add a network here after reviewing its AdMob page.
    /// </summary>
    public static class SketchMediationAdapters
    {
        static readonly Dictionary<string, (string network, string min, string why)> Reviewed = new Dictionary<string, (string, string, string)>
        {
            ["vungle"] = ("Liftoff", "7.7.4.2", "7.4.1 TCF, 7.6.3.1 TFUA, 7.7.4.2 COPPA/age treatment"),
            ["unity"] = ("Unity Ads", "4.19.0.1", "4.6.0.0 COPPA, 4.16.6.0 TFUA, 4.19.0.1 TCF"),
            ["inmobi"] = ("InMobi", "11.3.0.1", "10.7.5.0 TCF, 11.1.0.1 TFUA, 11.3.0.1 COPPA/age treatment"),
            ["bigo"] = ("BIGO Ads", "5.9.0.1", "5.5.2.0 TCF, 5.7.0.0 TFUA, 5.9.0.1 COPPA/age treatment"),
        };

        static readonly Regex Spec = new Regex(@"com\.google\.ads\.mediation:([A-Za-z0-9_.\-]+):\[?([0-9][0-9A-Za-z.\-]*)\]?", RegexOptions.Compiled);

        public static List<SketchMediationAdapter> ParseDependencies(string xml)
        {
            var list = new List<SketchMediationAdapter>();
            if (string.IsNullOrEmpty(xml)) return list;
            foreach (Match m in Spec.Matches(xml)) list.Add(new SketchMediationAdapter(m.Groups[1].Value, m.Groups[2].Value));
            return list;
        }

        public static List<SketchMediationAdapterReview> Review(IEnumerable<SketchMediationAdapter> adapters)
        {
            var rows = new List<SketchMediationAdapterReview>();
            var seen = new HashSet<string>();
            foreach (SketchMediationAdapter a in adapters)
            {
                if (!seen.Add(a.Artifact)) continue;
                if (!Reviewed.TryGetValue(a.Artifact, out var info))
                {
                    rows.Add(new SketchMediationAdapterReview(a.Artifact, a.Artifact, a.Version, null, SketchAdapterPrivacyStatus.NotReviewed,
                        "Privacy forwarding not reviewed by Sketch SDK: check its AdMob mediation page (GDPR, TFUA, COPPA, US states) before production."));
                    continue;
                }
                bool ok = CompareVersions(a.Version, info.min) >= 0;
                rows.Add(new SketchMediationAdapterReview(a.Artifact, info.network, a.Version, info.min,
                    ok ? SketchAdapterPrivacyStatus.Ok : SketchAdapterPrivacyStatus.TooOld,
                    ok ? info.why : $"Update to {info.min}+ ({info.why}); older adapters drop these signals."));
            }
            return rows;
        }

        /// <summary>Numeric dotted-version comparison; non-numeric parts compare as 0.</summary>
        public static int CompareVersions(string a, string b)
        {
            string[] x = (a ?? "").Split('.', '-'), y = (b ?? "").Split('.', '-');
            for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
            {
                int p = i < x.Length && int.TryParse(x[i], out int px) ? px : 0;
                int q = i < y.Length && int.TryParse(y[i], out int qy) ? qy : 0;
                if (p != q) return p.CompareTo(q);
            }
            return 0;
        }
    }
}
