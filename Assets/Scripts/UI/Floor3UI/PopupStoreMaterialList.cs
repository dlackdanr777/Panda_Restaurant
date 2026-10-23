using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One independent horizontal material browser. Selection only fills the local sales counter.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreMaterialList : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
{
    [SerializeField] private PopupStoreMaterialCatalog _catalog;
    [SerializeField] private PopupStoreSalesCounter _counter;
    [SerializeField] private PopupStoreMaterialKind _kind;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _content;
    [SerializeField] private Image _itemTemplate;
    [SerializeField] private Button _previousButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Vector2 _iconSize = new Vector2(126, 144);
    [SerializeField, Min(0)] private float _spacing = 18;
    [SerializeField, Min(0)] private float _padding = 10;
    [SerializeField, Min(1)] private float _pageDistance = 440;

    private readonly List<Image> _slots = new List<Image>();
    private ScrollRect _boundScrollRect;
    private Button _boundPrevious, _boundNext;
    private Image _builtTemplate;
    private RectTransform _builtContent;
    private float _savedPosition;
    private bool _layingOut;
    private string _lastError;
    private uint _inputVersion;

    public bool IsReady { get; private set; }
    public int MaterialCount { get; private set; }
    internal uint InputVersion => _inputVersion;

    public bool Refresh()
    {
        CancelPendingClick();
        Unbind();
        IsReady = false;
        MaterialCount = 0;
        if (!ValidateReferences(out string error)) return Reject(error);
        if (!_catalog.TryGetMaterials(_kind, out var materials, out error)) return Reject(error);

        if (_builtTemplate != _itemTemplate || _builtContent != _content)
        {
            ReleaseSlots();
            _builtTemplate = _itemTemplate;
            _builtContent = _content;
            _savedPosition = 0;
        }
        _itemTemplate.gameObject.SetActive(false);
        for (int i = 0; i < materials.Length; i++)
        {
            if (i == _slots.Count)
                _slots.Add(Instantiate(_itemTemplate, _content, false));
            var slot = _slots[i];
            slot.name = materials[i].Id;
            slot.sprite = materials[i].Sprite;
            slot.preserveAspect = true;
            slot.raycastTarget = true;
            var input = slot.GetComponent<PopupStoreMaterialItem>();
            if (input == null) input = slot.gameObject.AddComponent<PopupStoreMaterialItem>();
            input.Bind(this, materials[i]);
            slot.gameObject.SetActive(true);
        }
        for (int i = materials.Length; i < _slots.Count; i++) _slots[i].gameObject.SetActive(false);
        MaterialCount = materials.Length;
        _scrollRect.horizontal = true;
        _scrollRect.vertical = false;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.StopMovement();
        IsReady = true;
        _lastError = null;
        ApplyLayout();
        Bind();
        return true;
    }

    private bool ValidateReferences(out string error)
    {
        error = null;
        if (_catalog == null) error = "A material catalog is required.";
        else if (_counter == null || GetComponentInParent<UIPopupStore>(true) == null
            || _counter.GetComponentInParent<UIPopupStore>(true) != GetComponentInParent<UIPopupStore>(true))
            error = "The sales counter must belong to the same popup store.";
        else if (!Owned(_scrollRect) || _scrollRect.gameObject != gameObject
            || _scrollRect.viewport == null || !Owned(_scrollRect.viewport)
            || !Owned(_content) || _scrollRect.content != _content || _content.parent != _scrollRect.viewport
            || !_scrollRect.viewport.IsChildOf(_scrollRect.transform))
            error = "ScrollRect, viewport and content must belong to this material panel.";
        else if (!Owned(_itemTemplate) || _itemTemplate.transform.parent != _content
            || _itemTemplate.GetComponentInChildren<Selectable>(true) != null)
            error = "The item template must be an Image without Selectable components directly inside this content.";
        else if (!Owned(_previousButton) || !Owned(_nextButton) || _previousButton == _nextButton)
            error = "Two distinct navigation buttons must belong to this material panel.";
        else if (!Positive(_iconSize.x) || !Positive(_iconSize.y) || !NonNegative(_spacing)
            || !NonNegative(_padding) || !Positive(_pageDistance))
            error = "Icon size and page distance must be positive; spacing and padding must be non-negative.";
        return error == null;
    }

    private bool Owned(Component component)
        => component != null && component.GetComponentInParent<PopupStoreMaterialList>(true) == this;
    private static bool Positive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool NonNegative(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);

    private bool Reject(string error)
    {
        foreach (var slot in _slots) if (slot != null) slot.gameObject.SetActive(false);
        if (Owned(_scrollRect)) { _scrollRect.StopMovement(); _scrollRect.enabled = false; }
        if (Owned(_previousButton)) _previousButton.interactable = false;
        if (Owned(_nextButton)) _nextButton.interactable = false;
        if (_lastError != error) Debug.LogError("PopupStoreMaterialList: " + error, this);
        _lastError = error;
        return false;
    }

    private void ApplyLayout()
    {
        if (!IsReady || _layingOut) return;
        _layingOut = true;
        try
        {
            float viewportWidth = Mathf.Max(0, _scrollRect.viewport.rect.width);
            float itemsWidth = _padding * 2 + MaterialCount * _iconSize.x + Mathf.Max(0, MaterialCount - 1) * _spacing;
            _content.anchorMin = _content.anchorMax = new Vector2(0, .5f);
            _content.pivot = new Vector2(0, .5f);
            _content.sizeDelta = new Vector2(Mathf.Max(viewportWidth, itemsWidth), Mathf.Max(_iconSize.y, _scrollRect.viewport.rect.height));
            for (int i = 0; i < MaterialCount; i++)
            {
                var rect = _slots[i].rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = _iconSize;
                rect.anchoredPosition = new Vector2(_padding + _iconSize.x * .5f + i * (_iconSize.x + _spacing), 0);
            }
            bool canScroll = itemsWidth > viewportWidth + .5f && viewportWidth > 0;
            _scrollRect.enabled = canScroll;
            _savedPosition = canScroll ? Mathf.Clamp01(_savedPosition) : 0;
            _scrollRect.StopMovement();
            _content.anchoredPosition = new Vector2(-Mathf.Max(0, itemsWidth - viewportWidth) * _savedPosition, 0);
            UpdateButtons();
        }
        finally { _layingOut = false; }
    }

    private void PreviousPage() => MovePage(-1);
    private void NextPage() => MovePage(1);

    private void MovePage(int direction)
    {
        CancelPendingClick();
        if (!IsReady || !isActiveAndEnabled || !_scrollRect.enabled) return;
        float range = _content.rect.width - _scrollRect.viewport.rect.width;
        if (range <= .5f) return;
        _scrollRect.StopMovement();
        _scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(_scrollRect.horizontalNormalizedPosition + direction * _pageDistance / range);
        _savedPosition = _scrollRect.horizontalNormalizedPosition;
        UpdateButtons();
    }

    private void OnScrolled(Vector2 position)
    {
        if (!IsReady || _layingOut) return;
        CancelPendingClick();
        _savedPosition = Mathf.Clamp01(position.x);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool scrollable = IsReady && _scrollRect.enabled && _content.rect.width > _scrollRect.viewport.rect.width + .5f;
        float position = scrollable ? Mathf.Clamp01(_scrollRect.horizontalNormalizedPosition) : 0;
        _previousButton.interactable = scrollable && position > .0001f;
        _nextButton.interactable = scrollable && position < .9999f;
    }

    private void Bind()
    {
        _boundScrollRect = _scrollRect;
        _boundPrevious = _previousButton;
        _boundNext = _nextButton;
        _boundScrollRect.onValueChanged.AddListener(OnScrolled);
        _boundPrevious.onClick.AddListener(PreviousPage);
        _boundNext.onClick.AddListener(NextPage);
    }

    private void Unbind()
    {
        if (_boundScrollRect != null)
        {
            _savedPosition = Mathf.Clamp01(_boundScrollRect.horizontalNormalizedPosition);
            _boundScrollRect.StopMovement();
            _boundScrollRect.onValueChanged.RemoveListener(OnScrolled);
        }
        if (_boundPrevious != null) _boundPrevious.onClick.RemoveListener(PreviousPage);
        if (_boundNext != null) _boundNext.onClick.RemoveListener(NextPage);
        _boundScrollRect = null;
        _boundPrevious = _boundNext = null;
    }

    private void ReleaseSlots()
    {
        foreach (var slot in _slots)
        {
            if (slot == null) continue;
            slot.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(slot.gameObject);
            else DestroyImmediate(slot.gameObject);
        }
        _slots.Clear();
    }

    internal bool TrySelect(PopupStoreMaterialItem item, uint inputVersion)
    {
        if (!IsReady || !isActiveAndEnabled || inputVersion != _inputVersion
            || item == null || item.Owner != this || !item.isActiveAndEnabled
            || item.Material == null || item.Material.Kind != _kind
            || !_slots.Contains(item.GetComponent<Image>()) || _counter == null) return false;
        return _counter.TryAdd(item.Material);
    }

    private void CancelPendingClick()
    {
        unchecked { _inputVersion++; }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        CancelPendingClick();
        if (eventData != null) eventData.eligibleForClick = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Keeping a parent drag handler active also cancels icon clicks when a short
        // list disables its ScrollRect. The enabled ScrollRect alone performs movement.
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (eventData == null || eventData.scrollDelta.sqrMagnitude == 0) return;
        CancelPendingClick();
        eventData.eligibleForClick = false;
    }

    private void OnEnable() => Refresh();
    private void OnDisable() { CancelPendingClick(); Unbind(); IsReady = false; }
    private void OnDestroy() => Unbind();
    private void OnRectTransformDimensionsChange() => ApplyLayout();
}
