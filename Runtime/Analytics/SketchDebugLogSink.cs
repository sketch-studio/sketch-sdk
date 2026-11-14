using System.Collections.Generic;
using System.Text;

namespace Sketch.Sdk
{
    /// <summary>Prints analytics to the Console (Editor / development builds with VerboseLogs).</summary>
    public sealed class SketchDebugLogSink : ISketchAnalyticsSink
    {
        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            var b = new StringBuilder("[Analytics] ").Append(eventName);
            if (parameters != null && parameters.Count > 0)
            {
                b.Append(' ');
                SketchJson.Write(b, parameters);
            }
            SketchLog.Info(b.ToString());
        }

        public void LogAdRevenue(SketchAdRevenue revenue) =>
            SketchLog.Info($"[Analytics] ad revenue {revenue.Format}/{revenue.Placement}: {revenue.ValueMicros / 1_000_000d:0.######} {revenue.Currency} ({revenue.Precision})");

        public void LogPurchase(SketchPurchase purchase) =>
            SketchLog.Info($"[Analytics] purchase {purchase.ProductId}: {purchase.Price} {purchase.Currency} ({purchase.Store})");
    }
}
