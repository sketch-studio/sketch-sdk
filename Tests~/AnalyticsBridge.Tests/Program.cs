using Sketch.Sdk;
using Sketch.Analytics;
using Sketch.Sdk.Integrations;

int assertions = 0;
var client = new AnalyticsClient();
var bridge = new SketchAnalyticsBridge(client);
var placement = bridge.PlacementFor("rewarded");
Check(placement == "rewarded", "format placement is stable and generic");
bridge.Track(new SketchAdTelemetryEvent { Name = "ad_impression", Format = "rewarded", Placement = placement, AdUnitId = "unit", AdSource = "network" });
Check(client.EventCount == 1 && client.LastEvent == "ad_impression", "lifecycle event forwards once");
int eventsBeforeUsd = client.EventCount;
bridge.TrackRevenue(new SketchAdRevenue { Format = "banner", Placement = "banner", Network = "admob", AdSource = "source-network", ResponseId = "response", Currency = "USD", ValueMicros = 1250000, Precision = "Estimated", AdUnitId = "unit" });
Check(client.RevenueCount == 0 && client.EventCount == eventsBeforeUsd + 1 && client.LastEvent == "ad_revenue", "USD revenue uses one generic event, without a second revenue API write");
Check(client.LastPlacement == "banner", "USD event sets the AnalyticsClient top-level placement");
Check(Math.Abs((double)client.LastProperties["revenueUsd"] - 1.25) < 0.000001 && client.LastProperties["rawValueMicros"].Equals(1250000L), "USD dollars and raw micros are both preserved");
Check(client.LastProperties["network"].Equals("source-network") && client.LastProperties["mediation"].Equals("admob") && client.LastProperties["responseId"].Equals("response") && client.LastProperties["adSource"].Equals("source-network"), "USD source network, mediation, and response metadata map in one event");
int eventsBeforeNonUsd = client.EventCount;
bridge.TrackRevenue(new SketchAdRevenue { Format = "native", Placement = "native", Network = "admob", AdSource = "source-network", Currency = "JPY", ValueMicros = 1250000 });
Check(client.RevenueCount == 0 && client.EventCount == eventsBeforeNonUsd + 1 && client.LastEvent == "ad_revenue_raw", "non-USD produces one non-revenue-taxonomy event");
Check(client.LastProperties["valueMicros"].Equals(1250000L) && client.LastProperties["currency"].Equals("JPY") && client.LastProperties["network"].Equals("source-network") && client.LastProperties["mediation"].Equals("admob"), "non-USD preserves raw amount and source semantics without USD conversion");
bridge.CaptureIap(new SketchIapCapture { ProductId = "product", Platform = "android", OrderId = "order", Receipt = "receipt", PriceUsd = 2.5, Currency = "USD" });
Check(client.IapCount == 1 && client.LastIap.Receipt == "receipt", "IAP is forwarded to capture endpoint without entitlement behavior");
Console.WriteLine($"PASS: {assertions} analytics bridge assertions");
void Check(bool condition, string message) { assertions++; if (!condition) throw new System.Exception(message); }
