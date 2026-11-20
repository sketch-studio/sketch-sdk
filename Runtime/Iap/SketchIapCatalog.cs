using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>Every product the game sells. Ids must be unique; Validate lists authoring errors.</summary>
    [CreateAssetMenu(menuName = "Sketch SDK/IAP Catalog", fileName = "SketchIapCatalog")]
    public sealed class SketchIapCatalog : ScriptableObject
    {
        [SerializeField] List<SketchIapProduct> _products = new List<SketchIapProduct>();

        Dictionary<string, SketchIapProduct> _lookup;

        public IReadOnlyList<SketchIapProduct> Products => _products;

        /// <summary>Runtime catalog (tests, code-driven setups). Destroy it with Object.DestroyImmediate when done.</summary>
        public static SketchIapCatalog Create(IEnumerable<SketchIapProduct> products)
        {
            var catalog = CreateInstance<SketchIapCatalog>();
            catalog.hideFlags = HideFlags.DontSave;
            if (products != null) catalog._products.AddRange(products);
            catalog._lookup = null;
            return catalog;
        }

        public bool TryGet(string id, out SketchIapProduct product)
        {
            product = null;
            if (string.IsNullOrEmpty(id)) return false;
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(id, out product);
        }

        /// <summary>Appends authoring errors (null entry, empty id, duplicate id). Returns true when the catalog is valid.</summary>
        public bool Validate(List<string> errors = null)
        {
            errors ??= new List<string>();
            int before = errors.Count;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _products.Count; i++)
            {
                SketchIapProduct product = _products[i];
                if (product == null) { errors.Add($"Entry {i} is empty."); continue; }
                if (string.IsNullOrWhiteSpace(product.Id)) { errors.Add($"Entry {i} has no product id."); continue; }
                if (!seen.Add(product.Id)) errors.Add($"Duplicate product id '{product.Id}' (entry {i}).");
            }
            return errors.Count == before;
        }

        void BuildLookup()
        {
            _lookup = new Dictionary<string, SketchIapProduct>(_products.Count, StringComparer.Ordinal);
            for (int i = 0; i < _products.Count; i++)
            {
                SketchIapProduct product = _products[i];
                if (product == null || string.IsNullOrWhiteSpace(product.Id)) continue;
                if (_lookup.ContainsKey(product.Id)) { SketchLog.Warn($"IAP catalog '{name}': duplicate product id '{product.Id}', first entry kept."); continue; }
                _lookup.Add(product.Id, product);
            }
        }

        void OnEnable() => _lookup = null;
        void OnValidate() => _lookup = null;
    }
}
