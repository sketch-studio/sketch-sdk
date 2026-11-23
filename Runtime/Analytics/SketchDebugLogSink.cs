using System.Collections.Generic;
using System.Text;

namespace Sketch.Sdk
{
    /// <summary>Prints analytics to the Console (Editor / development builds with VerboseLogs).</summary>
    public sealed class SketchDebugLogSink : ISketchAnalyticsSink, ISketchRoutedSink
    {
        public SketchAnalyticsRoute Route => SketchAnalyticsRoute.Debug;

        // OR-818: in the Editor every event is always printed (the Editor never sends to server/Firebase), so the owner sees
        // that a call site fired; in development builds it follows VerboseLogs.
        static void Print(string message)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log("[SketchSdk][Editor only, not sent] " + message);
#else
            SketchLog.Info(message);
#endif
        }

        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            var b = new StringBuilder("[Analytics] ").Append(eventName);
            if (parameters != null && parameters.Count > 0)
            {
                b.Append(' ');
                SketchJson.Write(b, parameters);
            }
            Print(b.ToString());
        }

        public void LogAdRevenue(SketchAdRevenue revenue) =>
            Print($"[Analytics] ad revenue {revenue.Format}/{revenue.Placement}: {revenue.ValueMicros / 1_000_000d:0.######} {revenue.Currency} ({revenue.Precision})");

        public void LogPurchase(SketchPurchase purchase) =>
            Print($"[Analytics] purchase {purchase.ProductId}: {purchase.Price} {purchase.Currency} ({purchase.Store})");
    }
}
