/// <summary>Explicit owner of a popup-local Editor preview. No Player implementation or global supply override.</summary>
internal interface IPopupStorePhasePreviewSupply
{
    UIPopupStore Popup { get; }
    bool CanSupply { get; }
    bool CanCleanup { get; }
    float EntryInterval { get; }
    bool TryCreateOrder(int slotIndex, out PopupStoreOrder order);
    bool OwnsOrder(PopupStoreOrder order);
}
