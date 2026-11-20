using System;
using System.Collections.Generic;

namespace Sketch.Sdk
{
    /// <summary>In-memory entitlement store for tests and prototypes. Not durable: never ship it.</summary>
    public sealed class SketchMemoryEntitlementStore : ISketchEntitlementStore
    {
        readonly HashSet<string> _transactions = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _owned = new HashSet<string>(StringComparer.Ordinal);

        public int GrantedTransactionCount => _transactions.Count;

        public bool IsTransactionGranted(string transactionId) => !string.IsNullOrEmpty(transactionId) && _transactions.Contains(transactionId);

        public void MarkTransactionGranted(string transactionId, string productId)
        {
            if (!string.IsNullOrEmpty(transactionId)) _transactions.Add(transactionId);
        }

        public bool IsOwned(string productId) => !string.IsNullOrEmpty(productId) && _owned.Contains(productId);

        public void SetOwned(string productId, bool owned)
        {
            if (string.IsNullOrEmpty(productId)) return;
            if (owned) _owned.Add(productId);
            else _owned.Remove(productId);
        }
    }
}
