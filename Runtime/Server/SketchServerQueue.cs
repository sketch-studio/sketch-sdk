using System;
using System.Collections.Generic;

namespace Sketch.Sdk
{
    /// <summary>One pending POST to the Sketch server. Events are batched; other kinds are posted one by one.</summary>
    [Serializable]
    public sealed class SketchServerItem
    {
        public string Kind;   // "event", "session", "ad_revenue", "iap"
        public string Json;   // the JSON object body (an event object for kind "event")
    }

    /// <summary>Pure FIFO queue rules: batching of events, size cap, retry backoff. No IO.</summary>
    public sealed class SketchServerQueue
    {
        public const int MAX_BATCH = 100;
        readonly List<SketchServerItem> _items = new List<SketchServerItem>();
        int _failures;

        public int MaxItems { get; set; } = 2000;
        public int Count => _items.Count;
        public IReadOnlyList<SketchServerItem> Items => _items;
        public int DroppedForCap { get; private set; }
        /// <summary>Head items of the request in flight; the cap never trims them, so Complete(count) removes exactly what was sent.</summary>
        public int Reserved { get; private set; }

        public void Enqueue(SketchServerItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Json)) return;
            _items.Add(item);
            int overflow = _items.Count - Math.Max(MaxItems, Reserved + 1);
            if (overflow > 0)
            {
                // Drop the oldest items that are not part of the request in flight.
                _items.RemoveRange(Reserved, overflow);
                DroppedForCap += overflow;
            }
        }

        /// <summary>Mark the head items as in flight (call before sending them).</summary>
        public void Reserve(int count) => Reserved = Math.Max(0, Math.Min(count, _items.Count));

        public void Load(IEnumerable<SketchServerItem> items)
        {
            _items.Clear();
            if (items != null) foreach (var item in items) Enqueue(item);
        }

        /// <summary>Next request: either up to MAX_BATCH leading events, or the single leading non-event item. Count = items consumed.</summary>
        public bool TryPeekRequest(out string kind, out List<SketchServerItem> items, int maxBatch = MAX_BATCH)
        {
            maxBatch = Math.Max(1, Math.Min(maxBatch, MAX_BATCH));
            kind = null;
            items = null;
            if (_items.Count == 0) return false;
            kind = _items[0].Kind;
            items = new List<SketchServerItem>();
            if (kind != "event") { items.Add(_items[0]); return true; }
            for (int i = 0; i < _items.Count && items.Count < maxBatch && _items[i].Kind == "event"; i++) items.Add(_items[i]);
            return true;
        }

        /// <summary>Remove the items of a finished request (success or a permanent 4xx rejection).</summary>
        public void Complete(int count)
        {
            if (count > 0) _items.RemoveRange(0, Math.Min(count, _items.Count));
            Reserved = 0;
            _failures = 0;
        }

        /// <summary>A retryable failure: returns seconds to wait (1, 2, 4 ... max 60).</summary>
        public double Fail()
        {
            Reserved = 0;
            _failures++;
            return Math.Min(60.0, Math.Pow(2, Math.Min(_failures - 1, 6)));
        }

        /// <summary>HTTP status that means this payload will never be accepted (drop instead of retry): 400, 409, 422.
        /// Everything else (401/403 wrong key, 404/405 wrong URL, 413, 429, 5xx, network) is kept and retried, so fixing the
        /// configuration still delivers the queue.</summary>
        public static bool IsPermanentRejection(long status) => status == 400 || status == 409 || status == 422;

        public static string BuildBatchBody(List<SketchServerItem> events)
        {
            var b = new System.Text.StringBuilder(256 * events.Count + 16);
            b.Append("{\"events\":[");
            for (int i = 0; i < events.Count; i++)
            {
                if (i > 0) b.Append(',');
                b.Append(events[i].Json);
            }
            b.Append("]}");
            return b.ToString();
        }
    }
}
