using System;
using System.Collections.Generic;

/// <summary>Read-only leading material comparison. This does not accept or fulfil an order.</summary>
public static class PopupStoreOrderMatch
{
    public static int CountPrefix(IReadOnlyList<PopupStoreMaterialEntry> order, IReadOnlyList<PopupStoreMaterialEntry> counter)
    {
        if (order == null || counter == null) return 0;
        int count = Math.Min(order.Count, counter.Count);
        int matched = 0;
        for (; matched < count; matched++)
        {
            var expected = order[matched];
            var actual = counter[matched];
            if (expected == null || actual == null || expected.Kind != actual.Kind
                || !string.Equals(expected.Id, actual.Id, StringComparison.Ordinal)) break;
        }
        return matched;
    }
}
