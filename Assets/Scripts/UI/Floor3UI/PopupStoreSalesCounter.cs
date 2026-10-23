using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Five ordered materials held only by this popup instance. No account or inventory writes.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreSalesCounter : MonoBehaviour
{
    public const int Capacity = 5;
    public enum ChangeReason { Refreshed, Added, Cleared, Unavailable, Packed }

    public event Action<ChangeReason> Changed;

    [SerializeField] private PopupStoreMaterialCatalog _catalog;
    [SerializeField] private Image[] _slotIcons;
    [SerializeField] private Button _clearButton;

    // Intentionally not serialized: closing the view retains this instance, destroying it releases the contents.
    private readonly List<PopupStoreMaterialEntry> _materials = new List<PopupStoreMaterialEntry>(Capacity);
    private ReadOnlyCollection<PopupStoreMaterialEntry> _readOnlyMaterials;
    private Button _boundClearButton;
    private string _lastError;
    private bool _packingLocked;
    private bool _packingConsumedPending;

    public bool IsReady { get; private set; }
    public int Count => _materials.Count;
    public IReadOnlyList<PopupStoreMaterialEntry> Materials
        => _readOnlyMaterials ?? (_readOnlyMaterials = _materials.AsReadOnly());

    public bool Refresh()
    {
        if (_packingLocked) return IsReady;
        Unbind();
        IsReady = false;
        if (!ValidateReferences(out string error))
        {
            if (OwnsClearButton()) _clearButton.interactable = false;
            if (_lastError != error) Debug.LogError("PopupStoreSalesCounter: " + error, this);
            _lastError = error;
            NotifyChanged(ChangeReason.Unavailable);
            return false;
        }
        IsReady = true;
        _lastError = null;
        Render();
        _boundClearButton = _clearButton;
        _boundClearButton.onClick.AddListener(Clear);
        _boundClearButton.interactable = true;
        NotifyChanged(ChangeReason.Refreshed);
        return true;
    }

    public bool TryAdd(PopupStoreMaterialEntry material)
    {
        if (_packingLocked || !isActiveAndEnabled || !IsReady || material == null || _materials.Count >= Capacity)
            return false;
        if (!_catalog.TryGetMaterials(material.Kind, out var approved, out _)) return false;
        bool registered = false;
        foreach (var entry in approved)
            if (ReferenceEquals(entry, material)) { registered = true; break; }
        if (!registered) return false;

        _materials.Add(material);
        Render();
        NotifyChanged(ChangeReason.Added);
        return true;
    }

    public void Clear()
    {
        if (_packingLocked || !isActiveAndEnabled || !IsReady) return;
        _materials.Clear();
        Render();
        NotifyChanged(ChangeReason.Cleared);
    }

    internal bool BeginPackingLock()
    {
        if (_packingLocked || !isActiveAndEnabled || !IsReady) return false;
        _packingLocked = true;
        return true;
    }

    internal void EndPackingLock()
    {
        _packingLocked = false;
        _packingConsumedPending = false;
        if (isActiveAndEnabled && !IsReady) Refresh();
    }

    // The order is committed before either component publishes layout or observer changes.
    internal bool ConsumePackingMaterialsSilently()
    {
        if (!_packingLocked || !isActiveAndEnabled || !IsReady || _materials.Count == 0
            || _packingConsumedPending) return false;
        _materials.Clear();
        _packingConsumedPending = true;
        return true;
    }

    internal void PublishPackingConsumed()
    {
        if (!_packingLocked || !_packingConsumedPending) return;
        _packingConsumedPending = false;
        try { Render(); }
        catch (Exception exception) { Debug.LogException(exception, this); }
        NotifyChanged(ChangeReason.Packed);
    }

    private void NotifyChanged(ChangeReason reason)
    {
        var handlers = Changed;
        if (handlers == null) return;
        foreach (Action<ChangeReason> handler in handlers.GetInvocationList())
            try { handler(reason); }
            catch (Exception exception) { Debug.LogException(exception, this); }
    }

    private bool ValidateReferences(out string error)
    {
        error = null;
        if (_catalog == null) error = "The approved material catalog is required.";
        else if (!_catalog.TryValidate(out error)) return false;
        else if (_slotIcons == null || _slotIcons.Length != Capacity)
            error = "Exactly five authored slot icons are required.";
        else if (!OwnsClearButton()) error = "The clear button must belong to the same popup.";
        if (error != null) return false;

        var seen = new HashSet<Image>();
        foreach (var icon in _slotIcons)
        {
            if (icon == null || icon.GetComponentInParent<PopupStoreSalesCounter>(true) != this || !seen.Add(icon))
            {
                error = "Five distinct material icons must belong to this sales counter.";
                return false;
            }
        }
        return true;
    }

    private bool OwnsClearButton()
    {
        var popup = GetComponentInParent<UIPopupStore>(true);
        return popup != null && _clearButton != null
            && _clearButton.GetComponentInParent<UIPopupStore>(true) == popup;
    }

    private void Render()
    {
        for (int i = 0; i < Capacity; i++)
        {
            var icon = _slotIcons[i];
            bool occupied = i < _materials.Count;
            icon.sprite = occupied ? _materials[i].Sprite : null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.gameObject.SetActive(occupied);
        }
    }

    private void Unbind()
    {
        if (_boundClearButton != null) _boundClearButton.onClick.RemoveListener(Clear);
        _boundClearButton = null;
    }

    private void OnEnable() => Refresh();
    private void OnDisable()
    {
        Unbind();
        IsReady = false;
        NotifyChanged(ChangeReason.Unavailable);
    }
    private void OnDestroy() => Unbind();
}
