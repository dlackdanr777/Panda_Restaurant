using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Conditional shop sampling, without replacement, independent of paid draws and fairy RNG.</summary>
public static class GachaExchangeDisplaySelector
{
    public static IReadOnlyList<GachaExchangeProduct> SelectMixed(IReadOnlyList<GachaExchangeProduct> products, System.Random random)
    {
        if (random == null) throw new ArgumentNullException(nameof(random));
        var remaining = products.Where(x => x.Price > 0 && x.Quantity > 0 &&
            (x.Category == GachaExchangeCategory.Tickets || x.Data != null) &&
            (x.Category != GachaExchangeCategory.Staff || GachaEconomyService.CanPurchaseStaff(x.Data.Rank)))
            .GroupBy(x => x.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        var result = new List<GachaExchangeProduct>();
        // Include each available kind, then fill from the same pool without duplicate products.
        foreach (var category in new[] { GachaExchangeCategory.Tickets, GachaExchangeCategory.Staff, GachaExchangeCategory.Items })
        {
            var candidates = remaining.Where(x => x.Category == category).ToArray();
            if (candidates.Length == 0) continue;
            var chosen = category == GachaExchangeCategory.Tickets ? candidates[random.Next(candidates.Length)] : Select(candidates, category, random, 1)[0];
            result.Add(chosen); remaining.Remove(chosen);
        }
        while (result.Count < 6 && remaining.Count > 0)
        {
            var category = remaining[random.Next(remaining.Count)].Category;
            var candidates = remaining.Where(x => x.Category == category).ToArray();
            var chosen = category == GachaExchangeCategory.Tickets ? candidates[random.Next(candidates.Length)] : Select(candidates, category, random, 1)[0];
            result.Add(chosen); remaining.Remove(chosen);
        }
        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1); var swap = result[i]; result[i] = result[j]; result[j] = swap;
        }
        return result;
    }
    public static IReadOnlyList<GachaExchangeProduct> Select(IReadOnlyList<GachaExchangeProduct> products,
        GachaExchangeCategory category, System.Random random, int slots = 6)
    {
        if (random == null) throw new ArgumentNullException(nameof(random));
        if (category != GachaExchangeCategory.Staff && category != GachaExchangeCategory.Items)
            throw new ArgumentOutOfRangeException(nameof(category));
        var remaining = products.Where(x => x.Category == category && x.Data != null && x.Price > 0 && x.Quantity > 0 &&
            (category != GachaExchangeCategory.Staff || GachaEconomyService.CanPurchaseStaff(x.Data.Rank)))
            .GroupBy(x => x.Id, StringComparer.Ordinal).Select(x => x.First()).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        var result = new List<GachaExchangeProduct>();
        while (result.Count < slots && remaining.Count > 0)
        {
            var groups = remaining.GroupBy(x => Group(category, x.Data.Rank)).OrderBy(x => x.Key)
                .Select(x => new { Items = x.ToArray(), Weight = Weight(category, x.Key) }).Where(x => x.Weight > 0).ToArray();
            if (groups.Length == 0) break;
            double roll = random.NextDouble() * groups.Sum(x => x.Weight);
            var chosen = groups[groups.Length - 1];
            foreach (var group in groups) { roll -= group.Weight; if (roll < 0) { chosen = group; break; } }
            var product = chosen.Items[random.Next(chosen.Items.Length)];
            result.Add(product); remaining.Remove(product);
        }
        return result.OrderByDescending(x => x.Data.Rank).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
    }
    private static Rank Group(GachaExchangeCategory category, Rank rank)
        => category == GachaExchangeCategory.Staff && (rank == Rank.Normal1 || rank == Rank.Normal2) ? Rank.Normal2 : rank;
    public static double Weight(GachaExchangeCategory category, Rank rank)
        => category == GachaExchangeCategory.Staff ? StaffGachaRandomSelector.GetGradeWeight(rank) : Utility.GetGachaItemRankRange(rank);
}
