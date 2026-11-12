using System;
using System.Collections.Generic;

/// <summary>Editor proposal v0 only. Uses catalog order and never changes the authored twenty-order supply.</summary>
public sealed class PopupStorePhasePreviewOrderSource
{
    private readonly PopupStoreMaterialCatalog _catalog;
    private readonly Func<int, int> _next;
    private readonly string[] _customers;
    private readonly Dictionary<string, Binding> _bindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
    public int CreatedCount => _bindings.Count;

    public PopupStorePhasePreviewOrderSource(PopupStoreMaterialCatalog catalog, string[] customers, Func<int, int> next)
    { _catalog = catalog; _customers = customers == null ? null : (string[])customers.Clone(); _next = next; }

    public static int PlushCount(PopupStoreGradeTier grade, int roll)
    {
        if (roll < 0 || roll >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        switch (grade)
        {
            case PopupStoreGradeTier.C: return 1;
            case PopupStoreGradeTier.B: return roll < 70 ? 1 : 2;
            case PopupStoreGradeTier.A: return roll < 40 ? 1 : 2;
            case PopupStoreGradeTier.S: return roll < 70 ? 2 : 3;
            case PopupStoreGradeTier.SS: return roll < 50 ? 2 : 3;
            case PopupStoreGradeTier.SSS: return roll < 20 ? 2 : 3;
            default: throw new ArgumentOutOfRangeException(nameof(grade));
        }
    }

    public bool TryCreate(PopupStorePhaseSession session, int slot, out PopupStoreOrder order)
    {
        order = null;
        if (session == null || !session.CanAcceptPacking || string.IsNullOrEmpty(session.PhaseRunId)
            || _catalog == null || _next == null || _customers == null || _customers.Length != 3 || slot < 0 || slot >= 3
            || !_catalog.TryGetMaterials(PopupStoreMaterialKind.Box, out var boxes, out _)
            || !_catalog.TryGetMaterials(PopupStoreMaterialKind.Ribbon, out var ribbons, out _)) return false;
        try
        {
            int count = PlushCount(session.CurrentPhase.Grade, Next(100));
            var plush = new string[count];
            for (int i = 0; i < count; i++) plush[i] = session.CurrentPhase.AllowedPlushIds[Next(session.CurrentPhase.AllowedPlushIds.Count)];
            if (!PopupStoreOrder.TryCreateEditorOrder(_customers[slot], plush, boxes[Next(boxes.Length)].Id,
                ribbons[Next(ribbons.Length)].Id, _catalog, out order)) return false;
            _bindings.Add(order.RuntimeId, new Binding(order, session.BusinessSessionId, session.PhaseRunId, slot));
            return true;
        }
        catch { order = null; return false; }
    }

    private int Next(int max)
    {
        int value = _next(max);
        if (value < 0 || value >= max) throw new InvalidOperationException("Preview order random source is out of range.");
        return value;
    }

    public bool Owns(PopupStorePhaseSession session, PopupStoreOrder order)
        => session != null && order != null && _bindings.TryGetValue(order.RuntimeId, out var b)
            && ReferenceEquals(b.Order, order) && b.Business == session.BusinessSessionId && b.Phase == session.PhaseRunId;

    public bool OwnsReceipt(PopupStorePhaseSession session, PopupStorePackingReceipt receipt)
        => session != null && receipt != null && _bindings.TryGetValue(receipt.OrderId, out var b)
            && b.Business == session.BusinessSessionId && b.Phase == session.PhaseRunId && b.Slot == receipt.SlotIndex
            && b.Order.CustomerId == receipt.CustomerId && b.Order.IsPacked && ReferenceEquals(b.Order.PackingReceipt, receipt);

    private sealed class Binding
    {
        internal readonly PopupStoreOrder Order;
        internal readonly string Business, Phase;
        internal readonly int Slot;
        internal Binding(PopupStoreOrder order, string business, string phase, int slot)
        { Order = order; Business = business; Phase = phase; Slot = slot; }
    }
}
