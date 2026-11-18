using System;
using System.Collections.Generic;

namespace Sketch.Sdk
{
    public sealed class SketchCrossPromoItem
    {
        public string Id { get; }
        public string ImageUrl { get; }
        public string VideoUrl { get; }
        public string StoreUrl { get; }
        public bool Enabled { get; }

        public SketchCrossPromoItem(string id, string storeUrl, string imageUrl = null, string videoUrl = null, bool enabled = true)
        {
            Id = Normalize(id);
            StoreUrl = Normalize(storeUrl);
            ImageUrl = Normalize(imageUrl);
            VideoUrl = Normalize(videoUrl);
            Enabled = enabled && Id.Length > 0 && Uri.TryCreate(StoreUrl, UriKind.Absolute, out _);
        }

        static string Normalize(string value) => (value ?? string.Empty).Trim();
    }

    public interface ISketchCrossPromoCatalog
    {
        IReadOnlyList<SketchCrossPromoItem> Items { get; }
    }

    public interface ISketchCrossPromoState
    {
        int OpenCount { get; }
        int SessionCount { get; }
        bool Disabled { get; }
        void Save(int openCount, int sessionCount, bool disabled);
    }

    /// <summary>Provider-neutral cross-promotion rotation. Persistence and presentation stay game-owned.</summary>
    public sealed class SketchCrossPromo
    {
        readonly ISketchCrossPromoCatalog _catalog;
        readonly ISketchCrossPromoState _state;
        readonly int _opensPerRotation;
        readonly int _sessionsPerRotation;
        int _index = -1;

        public SketchCrossPromo(ISketchCrossPromoCatalog catalog, ISketchCrossPromoState state, int opensPerRotation = 0, int sessionsPerRotation = 0)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _opensPerRotation = Math.Max(0, opensPerRotation);
            _sessionsPerRotation = Math.Max(0, sessionsPerRotation);
        }

        public SketchCrossPromoItem Current
        {
            get
            {
                if (_state.Disabled || !ShouldRotate()) return null;
                var items = EnabledItems();
                if (items.Count == 0) return null;
                if (_index < 0 || _index >= items.Count) _index = 0;
                return items[_index];
            }
        }

        public bool RecordOpen()
        {
            var next = _state.OpenCount + 1;
            _state.Save(next, _state.SessionCount, _state.Disabled);
            return ShouldRotate();
        }

        public bool RecordSession()
        {
            var next = _state.SessionCount + 1;
            _state.Save(_state.OpenCount, next, _state.Disabled);
            return ShouldRotate();
        }

        public bool RecordClick()
        {
            var item = Current;
            if (item == null) return false;
            SketchAnalytics.Log("cross_promo_click", "promo_id", item.Id);
            Advance(item);
            return true;
        }

        public void RecordImpression()
        {
            var item = Current;
            if (item != null) SketchAnalytics.Log("cross_promo_impression", "promo_id", item.Id);
        }

        void Advance(SketchCrossPromoItem item)
        {
            var items = EnabledItems();
            var found = items.IndexOf(item);
            _index = found < 0 ? 0 : (found + 1) % Math.Max(1, items.Count);
        }

        bool ShouldRotate() => (_opensPerRotation == 0 || _state.OpenCount >= _opensPerRotation)
            && (_sessionsPerRotation == 0 || _state.SessionCount >= _sessionsPerRotation);

        List<SketchCrossPromoItem> EnabledItems()
        {
            var result = new List<SketchCrossPromoItem>();
            if (_catalog.Items == null) return result;
            foreach (var item in _catalog.Items) if (item != null && item.Enabled) result.Add(item);
            return result;
        }
    }
}
