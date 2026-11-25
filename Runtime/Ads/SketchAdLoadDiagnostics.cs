using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sketch.Sdk
{
    /// <summary>One mediation source tried for a request (AdMob ResponseInfo.GetAdapterResponses entry).</summary>
    public sealed class SketchAdapterAttempt
    {
        public const int NO_ERROR = -1;
        public string AdSourceName;
        public string AdSourceId;
        public string AdapterClass;
        public long LatencyMillis;
        public int ErrorCode = NO_ERROR;
        public string ErrorMessage;
    }

    /// <summary>Provider-neutral copy of a failed ad load (LoadAdError + ResponseInfo).</summary>
    public sealed class SketchAdLoadFailure
    {
        public string Format;
        public string AdUnitId;
        public int Code;
        public string Domain;
        public string Message;
        public string Cause;
        public string ResponseId;
        public string MediationAdapterClass;
        public List<SketchAdapterAttempt> Adapters;
        /// <summary>Already compact adapter list (Java native payload); used when Adapters is empty.</summary>
        public string AdapterSummaryRaw;
    }

    /// <summary>
    /// Explains why an ad request returned nothing: Google error constant, a hint, every mediation source tried,
    /// and a per-format load/fail tally so the in-app numbers can be compared with AdMob "requests / matched requests".
    /// Runs only on load callbacks (never per frame).
    /// </summary>
    public static class SketchAdLoadDiagnostics
    {
        sealed class Stats
        {
            public int Loaded;
            public int Failed;
            public readonly SortedDictionary<int, int> Codes = new SortedDictionary<int, int>();
        }

        static readonly Dictionary<string, Stats> PerFormat = new Dictionary<string, Stats>(StringComparer.Ordinal);

        public static void ResetStats() => PerFormat.Clear();

        static Stats For(string format)
        {
            format = format ?? string.Empty;
            if (!PerFormat.TryGetValue(format, out var stats)) PerFormat[format] = stats = new Stats();
            return stats;
        }

        public static void RecordLoaded(string format) => For(format).Loaded++;

        public static void RecordFailure(string format, int code)
        {
            Stats stats = For(format);
            stats.Failed++;
            stats.Codes.TryGetValue(code, out int count);
            stats.Codes[code] = count + 1;
        }

        public static string Summary(string format)
        {
            if (!PerFormat.TryGetValue(format ?? string.Empty, out var stats) || stats.Loaded + stats.Failed == 0)
                return format + " attempts=0";
            int attempts = stats.Loaded + stats.Failed;
            var sb = new StringBuilder();
            sb.Append(format).Append(" attempts=").Append(attempts).Append(" loaded=").Append(stats.Loaded)
              .Append(" failed=").Append(stats.Failed).Append(" match=")
              .Append((100.0 * stats.Loaded / attempts).ToString("0.0", CultureInfo.InvariantCulture)).Append("% [");
            bool first = true;
            foreach (var pair in stats.Codes)
            {
                if (!first) sb.Append(", ");
                first = false;
                sb.Append("code").Append(pair.Key).Append(" x").Append(pair.Value);
            }
            return sb.Append(']').ToString();
        }

        // Google Mobile Ads error codes: Android com.google.android.gms.ads.AdRequest.ERROR_CODE_*, iOS GADErrorCode.
        public static string ReasonFor(int code, bool ios)
        {
            if (ios)
            {
                switch (code)
                {
                    case 0: return "INVALID_REQUEST";
                    case 1: return "NO_FILL";
                    case 2: return "NETWORK_ERROR";
                    case 3: return "SERVER_ERROR";
                    case 5: return "TIMEOUT";
                    case 7: return "MEDIATION_DATA_ERROR";
                    case 8: return "MEDIATION_ADAPTER_ERROR";
                    case 9: return "MEDIATION_NO_FILL";
                    case 11: return "INTERNAL_ERROR";
                    case 13: return "RECEIVED_INVALID_RESPONSE";
                    case 20: return "APP_ID_MISSING";
                    default: return "CODE_" + code.ToString(CultureInfo.InvariantCulture);
                }
            }
            switch (code)
            {
                case 0: return "INTERNAL_ERROR";
                case 1: return "INVALID_REQUEST";
                case 2: return "NETWORK_ERROR";
                case 3: return "NO_FILL";
                case 8: return "APP_ID_MISSING";
                case 9: return "MEDIATION_NO_FILL";
                case 10: return "REQUEST_ID_MISMATCH";
                case 11: return "INVALID_AD_STRING";
                default: return "CODE_" + code.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static string HintFor(string reason)
        {
            switch (reason)
            {
                case "NO_FILL":
                    return "no ad returned: check AdMob ad serving limit (Apps / Home banner, not Policy Center), new or low-traffic ad unit, max content rating G, " +
                           "test device not registered; open Ad Inspector for the waterfall";
                case "MEDIATION_NO_FILL": return "every mediation source returned no fill: read the adapter list";
                case "INVALID_REQUEST": return "ad unit id wrong, from another app, or format mismatch; App ID must match the ad unit account";
                case "INTERNAL_ERROR": return "server rejected or failed: app not approved / not linked to store, or invalid response";
                case "NETWORK_ERROR": return "no or unstable connection";
                case "APP_ID_MISSING": return "AdMob App ID missing in manifest (GoogleMobileAdsSettings)";
                case "TIMEOUT": return "request timed out";
                default: return "see message";
            }
        }

        public static string Describe(SketchAdLoadFailure failure, bool ios)
        {
            if (failure == null) return "no failure info";
            string reason = ReasonFor(failure.Code, ios);
            var sb = new StringBuilder(256);
            sb.Append("format=").Append(failure.Format)
              .Append(" unit=").Append(failure.AdUnitId)
              .Append(" code=").Append(failure.Code).Append(' ').Append(reason)
              .Append(" domain=").Append(failure.Domain)
              .Append(" message=\"").Append(failure.Message).Append('"');
            if (!string.IsNullOrEmpty(failure.Cause)) sb.Append(" cause=\"").Append(failure.Cause).Append('"');
            sb.Append(" response_id=").Append(string.IsNullOrEmpty(failure.ResponseId) ? "none" : failure.ResponseId);
            if (!string.IsNullOrEmpty(failure.MediationAdapterClass)) sb.Append(" mediation_adapter=").Append(failure.MediationAdapterClass);
            if (failure.Adapters != null && failure.Adapters.Count > 0)
            {
                sb.Append(" adapters=[");
                for (int i = 0; i < failure.Adapters.Count; i++)
                {
                    SketchAdapterAttempt a = failure.Adapters[i];
                    if (a == null) continue;
                    if (i > 0) sb.Append(" | ");
                    sb.Append(a.AdSourceName).Append(" (").Append(a.AdapterClass);
                    if (!string.IsNullOrEmpty(a.AdSourceId)) sb.Append(", source_id=").Append(a.AdSourceId);
                    sb.Append(", ").Append(a.LatencyMillis).Append("ms, ");
                    if (a.ErrorCode == SketchAdapterAttempt.NO_ERROR) sb.Append("ok");
                    else sb.Append("error ").Append(a.ErrorCode).Append(" \"").Append(a.ErrorMessage).Append('"');
                    sb.Append(')');
                }
                sb.Append(']');
            }
            else if (!string.IsNullOrEmpty(failure.AdapterSummaryRaw)) sb.Append(" adapters=[").Append(failure.AdapterSummaryRaw).Append(']');
            else sb.Append(" adapters=none");
            sb.Append(" hint=").Append(HintFor(reason));
            return sb.ToString();
        }

        /// <summary>"Source:code/latencyms;..." capped to maxLength (analytics parameter limits).</summary>
        public static string AdapterSummary(SketchAdLoadFailure failure, int maxLength)
        {
            if (failure == null) return string.Empty;
            string text;
            if (failure.Adapters != null && failure.Adapters.Count > 0)
            {
                var sb = new StringBuilder(64);
                foreach (SketchAdapterAttempt a in failure.Adapters)
                {
                    if (a == null) continue;
                    if (sb.Length > 0) sb.Append(';');
                    sb.Append(a.AdSourceName).Append(':')
                      .Append(a.ErrorCode == SketchAdapterAttempt.NO_ERROR ? "ok" : a.ErrorCode.ToString(CultureInfo.InvariantCulture))
                      .Append('/').Append(a.LatencyMillis).Append("ms");
                }
                text = sb.ToString();
            }
            else text = failure.AdapterSummaryRaw ?? string.Empty;
            return maxLength > 0 && text.Length > maxLength ? text.Substring(0, maxLength) : text;
        }

        /// <summary>Java native payload: code|message[|domain|responseId|adapterSummary].</summary>
        public static SketchAdLoadFailure ParseJavaNativeFailure(string adUnitId, string payload)
        {
            string[] parts = (payload ?? string.Empty).Split(new[] { '|' }, 5);
            int.TryParse(parts.Length > 0 ? parts[0] : null, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code);
            string Part(int i) => parts.Length > i && !string.IsNullOrEmpty(parts[i]) ? parts[i] : null;
            return new SketchAdLoadFailure
            {
                Format = "native",
                AdUnitId = adUnitId,
                Code = code,
                Message = Part(1),
                Domain = Part(2),
                ResponseId = Part(3),
                AdapterSummaryRaw = Part(4),
            };
        }
    }
}
