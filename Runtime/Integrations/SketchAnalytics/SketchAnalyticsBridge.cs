#if SKETCHSDK_ANALYTICS
using System;
using System.Collections.Generic;
using Sketch.Analytics;

namespace Sketch.Sdk.Integrations
{
    /// <summary>Maps provider-neutral ad and store telemetry to an injected Sketch Analytics client.</summary>
    public sealed class SketchAnalyticsBridge : ISketchAdsTelemetry, ISketchIapTelemetry
    {
        readonly AnalyticsClient _client;

        public SketchAnalyticsBridge(AnalyticsClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public string PlacementFor(string format) => SketchAnalyticsPlacement.ForFormat(format);

        public void Track(SketchAdTelemetryEvent adEvent)
        {
            if (adEvent == null || string.IsNullOrWhiteSpace(adEvent.Name)) return;
            string placement = string.IsNullOrWhiteSpace(adEvent.Placement) ? PlacementFor(adEvent.Format) : adEvent.Placement;
            var properties = new Dictionary<string, object>
            {
                ["format"] = adEvent.Format ?? string.Empty,
                ["placement"] = placement,
                ["adUnitId"] = adEvent.AdUnitId ?? string.Empty,
                ["adSource"] = adEvent.AdSource ?? string.Empty,
                ["code"] = adEvent.Code ?? string.Empty,
                ["message"] = adEvent.Message ?? string.Empty,
            };
            _client.Track(adEvent.Name, properties, placement);
        }

        public void TrackRevenue(SketchAdRevenue revenue)
        {
            if (revenue == null) return;
            string currency = string.IsNullOrWhiteSpace(revenue.Currency) ? string.Empty : revenue.Currency.Trim().ToUpperInvariant();
            string placement = string.IsNullOrWhiteSpace(revenue.Placement) ? PlacementFor(revenue.Format) : revenue.Placement;
            if (string.Equals(currency, "USD", StringComparison.Ordinal))
            {
                _client.Track("ad_revenue", new Dictionary<string, object>
                {
                    ["format"] = revenue.Format ?? "unknown",
                    ["placement"] = placement,
                    ["network"] = revenue.AdSource ?? string.Empty,
                    ["mediation"] = revenue.Network ?? string.Empty,
                    ["adUnitId"] = revenue.AdUnitId ?? string.Empty,
                    ["revenueUsd"] = revenue.ValueMicros / 1000000d,
                    ["currency"] = currency,
                    ["precision"] = revenue.Precision ?? string.Empty,
                    ["rawValueMicros"] = revenue.ValueMicros,
                    ["responseId"] = revenue.ResponseId ?? string.Empty,
                    ["adSource"] = revenue.AdSource ?? string.Empty,
                }, placement);
                return;
            }

            _client.Track("ad_revenue_raw", new Dictionary<string, object>
            {
                ["format"] = revenue.Format ?? "unknown",
                ["placement"] = placement,
                ["network"] = revenue.AdSource ?? string.Empty,
                ["mediation"] = revenue.Network ?? string.Empty,
                ["adUnitId"] = revenue.AdUnitId ?? string.Empty,
                ["valueMicros"] = revenue.ValueMicros,
                ["currency"] = currency,
                ["precision"] = revenue.Precision ?? string.Empty,
                ["responseId"] = revenue.ResponseId ?? string.Empty,
            }, placement);
        }

        public void CaptureIap(SketchIapCapture purchase)
        {
            if (purchase == null) return;
            _client.CaptureIap(new IapCapture
            {
                ProductId = purchase.ProductId ?? string.Empty,
                Platform = purchase.Platform ?? string.Empty,
                OrderId = purchase.OrderId ?? string.Empty,
                Receipt = purchase.Receipt ?? string.Empty,
                PriceUsd = purchase.PriceUsd,
                Currency = purchase.Currency ?? string.Empty,
            });
        }
    }
}
#endif
