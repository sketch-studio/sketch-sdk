using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Sketch.Sdk
{
    /// <summary>
    /// Sketch Server client (contract: sketch-server docs/sdk-contract.md): events batch, sessions, ad revenue, IAP, remote config.
    /// Persistent queue on disk, one request in flight, exponential retry, 4xx payload rejections dropped.
    /// </summary>
    public sealed class SketchServerClient : ISketchAnalyticsSink
    {
        [Serializable]
        sealed class QueueFile { public List<SketchServerItem> Items = new List<SketchServerItem>(); }

        const string QUEUE_FILE = "sketchsdk-queue.json";
        const int MAX_EVENT_PARAM_DEPTH = 4;

        readonly string _baseUrl;
        readonly string _appId;
        readonly string _apiKey;
        readonly float _flushInterval;
        readonly SketchServerQueue _queue = new SketchServerQueue();
        readonly string _queuePath;
        bool _inFlight;
        bool _dirty;
        double _nextFlushAt;
        double _nextSaveAt;
        double _retryAt;
        int _maxBatch = SketchServerQueue.MAX_BATCH;

        public static SketchServerClient Current { get; private set; }
        public int PendingCount => _queue.Count;
        public string BaseUrl => _baseUrl;

        public SketchServerClient(string baseUrl, string appId, string apiKey, float flushIntervalSeconds)
        {
            _baseUrl = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
            _appId = (appId ?? string.Empty).Trim();
            _apiKey = (apiKey ?? string.Empty).Trim();
            _flushInterval = Mathf.Max(2f, flushIntervalSeconds);
            _queuePath = Path.Combine(Application.persistentDataPath, QUEUE_FILE);
        }

        public void Start()
        {
            Current = this;
            LoadQueue();
            SketchSdkLoop.Update += Tick;
            Application.focusChanged += OnFocusChanged;
            Application.quitting += SaveQueue;
            PostSessionStart();
            _nextFlushAt = Now + 1.0;
        }

        static double Now => Time.realtimeSinceStartupAsDouble;

        // ---------------- ISketchAnalyticsSink ----------------

        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            var b = new StringBuilder(256);
            b.Append('{');
            Prop(b, "eventId", Guid.NewGuid().ToString("N"));
            CommonIds(b);
            Prop(b, "eventName", eventName);
            Prop(b, "occurredAt", SketchIdentity.IsoNow());
            Prop(b, "platform", SketchIdentity.Platform);
            Prop(b, "appVersion", Application.version);
            if (!string.IsNullOrEmpty(SketchRemoteConfig.ConfigId)) Prop(b, "configId", SketchRemoteConfig.ConfigId);
            b.Append("\"parameters\":");
            WriteParameters(b, parameters);
            b.Append('}');
            Enqueue("event", b.ToString());
        }

        public void LogAdRevenue(SketchAdRevenue revenue)
        {
            string currency = string.IsNullOrEmpty(revenue.Currency) ? "USD" : revenue.Currency.ToUpperInvariant();
            if (currency.Length != 3) currency = "USD";
            double value = Math.Max(0, revenue.ValueMicros / 1_000_000d);
            // The server stores revenueUsd as is: a non-USD AdMob account must not be counted as USD (VND would be ~25,000x).
            // Send 0 USD with the local value in metadata so it can be converted later.
            double usd = currency == "USD" ? value : 0;
            var b = new StringBuilder(384);
            b.Append('{');
            Prop(b, "revenueId", string.IsNullOrEmpty(revenue.ResponseId) ? Guid.NewGuid().ToString("N") : Identifier(revenue.ResponseId + "_" + revenue.ValueMicros + "_" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            CommonIds(b);
            Prop(b, "occurredAt", SketchIdentity.IsoNow());
            Prop(b, "format", Trunc(revenue.Format ?? "unknown", 32));
            if (!string.IsNullOrEmpty(revenue.Placement)) Prop(b, "placement", Trunc(revenue.Placement, 128));
            Prop(b, "network", Trunc(string.IsNullOrEmpty(revenue.AdSource) ? revenue.Network ?? "admob" : revenue.AdSource, 64));
            Prop(b, "platform", SketchIdentity.Platform);
            Prop(b, "appVersion", Application.version);
            if (!string.IsNullOrEmpty(SketchRemoteConfig.ConfigId)) Prop(b, "configId", SketchRemoteConfig.ConfigId);
            // revenueUsd is the AdMob value in the account currency; currency carries the code so a non-USD account is not mislabeled.
            PropRaw(b, "revenueUsd", usd.ToString("0.##########", CultureInfo.InvariantCulture));
            Prop(b, "currency", currency);
            if (!string.IsNullOrEmpty(revenue.Precision)) Prop(b, "precision", Trunc(revenue.Precision, 32));
            b.Append("\"metadata\":");
            WriteParameters(b, new Dictionary<string, object>
            {
                ["value_micros"] = revenue.ValueMicros,
                ["local_value"] = value,
                ["local_currency"] = currency,
                ["ad_unit_id"] = revenue.AdUnitId ?? string.Empty,
                ["mediation"] = revenue.Network ?? "admob",
            });
            b.Append('}');
            Enqueue("ad_revenue", b.ToString());
        }

        public void LogPurchase(SketchPurchase purchase)
        {
            LogEvent("iap_success", new Dictionary<string, object>
            {
                ["product_id"] = purchase.ProductId ?? string.Empty,
                ["price"] = purchase.Price,
                ["currency"] = purchase.Currency ?? string.Empty,
                ["store"] = purchase.Store ?? string.Empty,
                ["placement"] = purchase.Placement ?? string.Empty,
            });
            string currency = (purchase.Currency ?? string.Empty).Trim().ToUpperInvariant();
            if (currency.Length != 3 || purchase.Price < 0 || string.IsNullOrEmpty(purchase.ProductId)) return;
            double usd = purchase.PriceUsd > 0 ? purchase.PriceUsd : currency == "USD" ? purchase.Price : 0;
            var b = new StringBuilder(512);
            b.Append('{');
            Prop(b, "transactionId", Identifier(string.IsNullOrEmpty(purchase.TransactionId) ? Guid.NewGuid().ToString("N") : purchase.TransactionId));
            CommonIds(b);
            Prop(b, "productId", Identifier(purchase.ProductId));
            Prop(b, "platform", SketchIdentity.Platform);
            PropRaw(b, "amountUsd", usd.ToString("0.######", CultureInfo.InvariantCulture));
            Prop(b, "currency", currency);
            Prop(b, "purchasedAt", SketchIdentity.IsoNow());
            Prop(b, "provider", Trunc(purchase.Store ?? "unknown", 64));
            if (!string.IsNullOrEmpty(purchase.Receipt) && purchase.Receipt.Length <= 32_768) Prop(b, "receipt", purchase.Receipt);
            b.Append("\"metadata\":");
            WriteParameters(b, new Dictionary<string, object>
            {
                ["local_price"] = purchase.Price,
                ["local_currency"] = currency,
                ["usd_known"] = usd > 0,
            });
            b.Append('}');
            Enqueue("iap", b.ToString());
        }

        // ---------------- remote config ----------------

        /// <summary>GET /config?appId=&amp;installationId= ; onDone(true) when the server answered with a config.</summary>
        public void FetchRemoteConfig(float timeoutSeconds, Action<bool> onDone)
        {
            string url = _baseUrl + "/config?appId=" + UnityWebRequest.EscapeURL(_appId) +
                         "&installationId=" + UnityWebRequest.EscapeURL(SketchIdentity.InstallationId);
            UnityWebRequest request;
            try
            {
                request = UnityWebRequest.Get(url);
                request.SetRequestHeader("x-api-key", _apiKey);
                request.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));
            }
            catch (Exception e)
            {
                SketchLog.Warn("Remote config request failed to start: " + e.Message);
                onDone?.Invoke(false);
                return;
            }
            request.SendWebRequest().completed += _ =>
            {
                bool ok = request.result == UnityWebRequest.Result.Success;
                string body = ok ? request.downloadHandler.text : null;
                long code = request.responseCode;
                string error = request.error;
                request.Dispose();
                if (ok) SketchRemoteConfig.ApplyServerResponse(body);
                else SketchLog.Warn($"Remote config fetch failed ({code}): {error}");
                onDone?.Invoke(ok);
            };
        }

        // ---------------- queue / delivery ----------------

        public void Flush() => _nextFlushAt = 0;

        void PostSessionStart()
        {
            var b = new StringBuilder(256);
            b.Append('{');
            Prop(b, "sessionId", SketchIdentity.SessionId);
            CommonIds(b, includeSession: false);
            Prop(b, "startedAt", SketchIdentity.Iso(SketchIdentity.SessionStartedUtc));
            Prop(b, "platform", SketchIdentity.Platform);
            Prop(b, "appVersion", Application.version);
            b.Append("\"metadata\":");
            WriteParameters(b, new Dictionary<string, object>
            {
                ["session_number"] = SketchIdentity.Session,
                ["device_model"] = SystemInfo.deviceModel,
                ["os"] = SystemInfo.operatingSystem,
                ["memory_mb"] = SystemInfo.systemMemorySize,
            });
            b.Append('}');
            Enqueue("session", b.ToString());
        }

        void Enqueue(string kind, string json)
        {
            _queue.Enqueue(new SketchServerItem { Kind = kind, Json = json });
            _dirty = true;
            if (_queue.Count >= 50 && _nextFlushAt > Now + 1) _nextFlushAt = Now + 1;
        }

        void Tick()
        {
            double now = Now;
            if (_dirty && now >= _nextSaveAt) { SaveQueue(); _nextSaveAt = now + 10; }
            if (_inFlight || now < _retryAt || now < _nextFlushAt) return;
            if (!_queue.TryPeekRequest(out string kind, out List<SketchServerItem> items, _maxBatch))
            {
                _nextFlushAt = now + _flushInterval;
                return;
            }
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                _nextFlushAt = now + _flushInterval;
                return;
            }
            Send(kind, items);
        }

        void Send(string kind, List<SketchServerItem> items)
        {
            string path;
            string body;
            switch (kind)
            {
                case "event": path = "/events/batch"; body = SketchServerQueue.BuildBatchBody(items); break;
                case "session": path = "/sessions"; body = items[0].Json; break;
                case "ad_revenue": path = "/revenue/ads"; body = items[0].Json; break;
                case "iap": path = "/revenue/iap"; body = items[0].Json; break;
                default: _queue.Complete(1); _dirty = true; return;
            }

            UnityWebRequest request;
            try
            {
                request = new UnityWebRequest(_baseUrl + path, UnityWebRequest.kHttpVerbPOST)
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = 20,
                };
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-api-key", _apiKey);
            }
            catch (Exception e)
            {
                SketchLog.Warn("Server request failed to start: " + e.Message);
                _retryAt = Now + _queue.Fail();
                return;
            }

            _inFlight = true;
            int count = items.Count;
            _queue.Reserve(count);
            request.SendWebRequest().completed += _ =>
            {
                _inFlight = false;
                long code = request.responseCode;
                bool success = request.result == UnityWebRequest.Result.Success && code >= 200 && code < 300;
                string error = request.error;
                string response = request.downloadHandler?.text;
                request.Dispose();
                if (success)
                {
                    _queue.Complete(count);
                    _dirty = true;
                    SketchLog.Info($"Server {path}: {count} item(s) delivered ({code}).");
                    _nextFlushAt = _queue.Count > 0 ? Now : Now + _flushInterval;
                }
                else if (code == 413 && count > 1)
                {
                    _queue.Fail();
                    _maxBatch = Math.Max(1, count / 2);
                    _nextFlushAt = Now;
                    SketchLog.Info($"Server {path}: payload too large, batch size now {_maxBatch}.");
                }
                else if (SketchServerQueue.IsPermanentRejection(code) || code == 413)
                {
                    SketchLog.Warn($"Server rejected {path} ({code}), dropping {count} item(s): {Trunc(response ?? error, 300)}");
                    _queue.Complete(count);
                    _dirty = true;
                }
                else
                {
                    double wait = _queue.Fail();
                    _retryAt = Now + wait;
                    SketchLog.Info($"Server {path} failed ({code} {error}); retry in {wait:0}s.");
                }
            };
        }

        void OnFocusChanged(bool focused)
        {
            if (focused) return;
            SaveQueue();
            _nextFlushAt = 0;   // try to deliver before the OS suspends us
        }

        void LoadQueue()
        {
            try
            {
                string path = File.Exists(_queuePath) ? _queuePath : File.Exists(_queuePath + ".tmp") ? _queuePath + ".tmp" : null;
                if (path == null) return;
                var file = JsonUtility.FromJson<QueueFile>(File.ReadAllText(path));
                _queue.Load(file?.Items);
                if (_queue.Count > 0) SketchLog.Info($"Server queue restored: {_queue.Count} item(s).");
            }
            catch (Exception e) { SketchLog.Warn("Server queue load failed, starting empty: " + e.Message); }
        }

        void SaveQueue()
        {
            if (!_dirty) return;
            try
            {
                var file = new QueueFile { Items = new List<SketchServerItem>(_queue.Items) };
                string tmp = _queuePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(file));
                if (File.Exists(_queuePath)) File.Replace(tmp, _queuePath, null);
                else File.Move(tmp, _queuePath);
                _dirty = false;
            }
            catch (Exception e) { SketchLog.Warn("Server queue save failed: " + e.Message); }
        }

        // ---------------- JSON helpers ----------------

        void CommonIds(StringBuilder b, bool includeSession = true)
        {
            Prop(b, "appId", _appId);
            Prop(b, "gameId", _appId);
            Prop(b, "installationId", SketchIdentity.InstallationId);
            if (includeSession && !string.IsNullOrEmpty(SketchIdentity.SessionId)) Prop(b, "sessionId", SketchIdentity.SessionId);
        }

        static void Prop(StringBuilder b, string key, string value)
        {
            SketchJson.WriteString(b, key);
            b.Append(':');
            SketchJson.WriteString(b, value ?? string.Empty);
            b.Append(',');
        }

        static void PropRaw(StringBuilder b, string key, string rawJson)
        {
            SketchJson.WriteString(b, key);
            b.Append(':').Append(rawJson).Append(',');
        }

        static void WriteParameters(StringBuilder b, IReadOnlyDictionary<string, object> parameters)
        {
            b.Append('{');
            if (parameters != null)
            {
                bool first = true;
                foreach (var pair in parameters)
                {
                    if (string.IsNullOrEmpty(pair.Key)) continue;
                    if (!first) b.Append(',');
                    first = false;
                    SketchJson.WriteString(b, pair.Key);
                    b.Append(':');
                    SketchJson.Write(b, Simplify(pair.Value, 0));
                }
            }
            b.Append('}');
        }

        static void WriteParameters(StringBuilder b, Dictionary<string, object> parameters) =>
            WriteParameters(b, (IReadOnlyDictionary<string, object>)parameters);

        /// <summary>Unity types (Vector3, enums, objects) become strings; nested containers are kept up to a small depth.</summary>
        static object Simplify(object value, int depth)
        {
            switch (value)
            {
                case null: case string _: case bool _: case double _: case float _: case int _: case long _:
                case short _: case byte _: case uint _: case ulong _: case decimal _:
                    return value;
                case Enum e: return e.ToString();
                case UnityEngine.Object o: return o != null ? o.name : string.Empty;
                case System.Collections.IDictionary dict:
                {
                    // Copy with a depth cap: cycles and deep graphs (a Transform is IEnumerable) never reach the JSON writer.
                    if (depth >= MAX_EVENT_PARAM_DEPTH) return "{...}";
                    var copy = new Dictionary<string, object>();
                    foreach (System.Collections.DictionaryEntry entry in dict)
                        copy[Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty] = Simplify(entry.Value, depth + 1);
                    return copy;
                }
                case System.Collections.IEnumerable list:
                {
                    if (depth >= MAX_EVENT_PARAM_DEPTH) return "[...]";
                    var copy = new List<object>();
                    foreach (object item in list)
                    {
                        if (copy.Count >= 100) break;
                        copy.Add(Simplify(item, depth + 1));
                    }
                    return copy;
                }
                default: return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        static string Trunc(string value, int max) => string.IsNullOrEmpty(value) || value.Length <= max ? value ?? string.Empty : value.Substring(0, max);

        /// <summary>Server identifier rule: ^[A-Za-z0-9][A-Za-z0-9_.:-]*$, max 128.</summary>
        public static string Identifier(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return Guid.NewGuid().ToString("N");
            var b = new StringBuilder(Math.Min(raw.Length, 128));
            foreach (char c in raw)
            {
                if (b.Length >= 128) break;
                bool ok = char.IsLetterOrDigit(c) && c < 128 || c == '_' || c == '.' || c == ':' || c == '-';
                if (b.Length == 0 && !(char.IsLetterOrDigit(c) && c < 128)) continue;
                b.Append(ok ? c : '_');
            }
            return b.Length == 0 ? Guid.NewGuid().ToString("N") : b.ToString();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current?.Stop();
            Current = null;
        }

        public void Stop()
        {
            SketchSdkLoop.Update -= Tick;
            Application.focusChanged -= OnFocusChanged;
            Application.quitting -= SaveQueue;
            SaveQueue();
            if (ReferenceEquals(Current, this)) Current = null;
        }
    }
}
