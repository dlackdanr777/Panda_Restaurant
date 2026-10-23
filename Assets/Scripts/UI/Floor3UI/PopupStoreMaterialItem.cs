using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>A catalog-bound click target. Scrolling never adds a material.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreMaterialItem : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    public PopupStoreMaterialList Owner { get; private set; }
    public PopupStoreMaterialEntry Material { get; private set; }

    private bool _pressed;
    private int _pointerId;
    private Vector2 _pressPosition;
    private uint _inputVersion;

    public void Bind(PopupStoreMaterialList owner, PopupStoreMaterialEntry material)
    {
        _pressed = false;
        Owner = owner;
        Material = material;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressed = false;
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left
            || !isActiveAndEnabled || Owner == null || !Owner.IsReady || !Owner.isActiveAndEnabled
            || Material == null || eventData.scrollDelta.sqrMagnitude != 0) return;
        _pressed = true;
        _pointerId = eventData.pointerId;
        _pressPosition = eventData.position;
        _inputVersion = Owner.InputVersion;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        bool pressed = _pressed;
        _pressed = false;
        if (!pressed || eventData == null || !eventData.eligibleForClick || eventData.dragging
            || eventData.button != PointerEventData.InputButton.Left || eventData.pointerId != _pointerId
            || eventData.scrollDelta.sqrMagnitude != 0 || !isActiveAndEnabled || Owner == null) return;

        // The native input module handles drags. This also rejects a release-position
        // jump in the same frame, before the input module gets to ProcessDrag.
        float threshold = EventSystem.current != null ? Mathf.Max(1, EventSystem.current.pixelDragThreshold) : 10;
        if ((eventData.position - _pressPosition).sqrMagnitude >= threshold * threshold) return;
        Owner.TrySelect(this, _inputVersion);
    }

    private void OnDisable() => _pressed = false;
}
