using System.Collections.Generic;

namespace Sketch.Sdk
{
    /// <summary>One analytics backend (Sketch Server, Firebase, debug log...). Called on the main thread.</summary>
    public interface ISketchAnalyticsSink
    {
        void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters);
        void LogAdRevenue(SketchAdRevenue revenue);
        void LogPurchase(SketchPurchase purchase);
    }
}
