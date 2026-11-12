using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Noninteractive dots derived from the existing scroll range and navigation distance.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PopupStoreScrollPageIndicator : MonoBehaviour
{
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private Sprite _activeSprite;
    [SerializeField] private Sprite _inactiveSprite;
    [SerializeField, Min(1)] private float _pageDistance = 440;
    [SerializeField] private Vector2 _dotSize = new Vector2(12, 12);
    [SerializeField, Min(0)] private float _spacing = 7;

    // Keep the instance-owned references through an Editor domain reload; a refreshed view
    // must not create a second set of dots beside its existing children.
    [SerializeField, HideInInspector] private List<Image> _dots = new List<Image>();
    private ScrollRect _boundScroll;
    private RectTransform _lastContent, _lastViewport;
    private float _lastContentWidth = float.NaN, _lastViewportWidth = float.NaN;
    private float _lastDistance, _lastSpacing;
    private Vector2 _lastDotSize;
    private Sprite _lastActive, _lastInactive;

    public int PageCount { get; private set; }
    public int CurrentPage { get; private set; }

    public void Refresh()
    {
        if (isActiveAndEnabled) Bind(); else Unbind();
        var content = _boundScroll != null ? _boundScroll.content : null;
        var viewport = _boundScroll != null ? _boundScroll.viewport : null;
        CacheGeometry(content, viewport);
        _dots.RemoveAll(dot => dot == null || dot.transform.parent != transform);
        float range = content != null && viewport != null ? content.rect.width - viewport.rect.width : 0;
        bool valid = isActiveAndEnabled && viewport != null && viewport.rect.width > 0
            && _activeSprite != null && _inactiveSprite != null
            && Finite(range) && range > 1 && Finite(_pageDistance) && _pageDistance > 0
            && Finite(_spacing) && _spacing >= 0 && Finite(_dotSize.x) && Finite(_dotSize.y)
            && _dotSize.x > 0 && _dotSize.y > 0;
        PageCount = valid ? Mathf.CeilToInt(range / _pageDistance) + 1 : 0;
        float normalized = valid ? Mathf.Clamp01(_boundScroll.horizontalNormalizedPosition) : 0;
        CurrentPage = PageCount <= 0 ? 0 : normalized >= 1f - .00001f ? PageCount - 1
            : Mathf.Clamp(Mathf.RoundToInt(normalized * range / _pageDistance), 0, PageCount - 1);
        for (int i = _dots.Count; i < PageCount; i++)
        {
            var child = new GameObject("Scroll Page " + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(transform, false);
            _dots.Add(child.GetComponent<Image>());
        }
        float width = PageCount * _dotSize.x + Mathf.Max(0, PageCount - 1) * _spacing;
        for (int i = 0; i < _dots.Count; i++)
        {
            var dot = _dots[i];
            if (dot == null) continue;
            bool visible = i < PageCount;
            dot.gameObject.SetActive(visible);
            if (!visible) continue;
            var rect = dot.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = _dotSize;
            rect.anchoredPosition = new Vector2(-width * .5f + _dotSize.x * .5f + i * (_dotSize.x + _spacing), 0);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            dot.sprite = i == CurrentPage ? _activeSprite : _inactiveSprite;
            dot.type = Image.Type.Simple;
            dot.preserveAspect = true;
            dot.raycastTarget = false;
        }
    }

    private void Bind()
    {
        if (_boundScroll == _scroll) return;
        Unbind();
        _boundScroll = _scroll;
        if (_boundScroll != null) _boundScroll.onValueChanged.AddListener(OnScrolled);
    }

    private void Unbind()
    {
        if (_boundScroll != null) _boundScroll.onValueChanged.RemoveListener(OnScrolled);
        _boundScroll = null;
    }

    private void CacheGeometry(RectTransform content, RectTransform viewport)
    {
        _lastContent = content;
        _lastViewport = viewport;
        _lastContentWidth = content != null ? content.rect.width : 0;
        _lastViewportWidth = viewport != null ? viewport.rect.width : 0;
        _lastDistance = _pageDistance;
        _lastDotSize = _dotSize;
        _lastSpacing = _spacing;
        _lastActive = _activeSprite;
        _lastInactive = _inactiveSprite;
    }

    private void OnScrolled(Vector2 _) => Refresh();
    private void OnEnable() => Refresh();
    private void OnDisable()
    {
        Unbind();
        foreach (var dot in _dots) if (dot != null) dot.gameObject.SetActive(false);
        PageCount = CurrentPage = 0;
    }
    private void OnDestroy() => Unbind();
    private void LateUpdate()
    {
        var content = _scroll != null ? _scroll.content : null;
        var viewport = _scroll != null ? _scroll.viewport : null;
        if (_boundScroll != _scroll || content != _lastContent || viewport != _lastViewport
            || (content != null ? content.rect.width : 0) != _lastContentWidth
            || (viewport != null ? viewport.rect.width : 0) != _lastViewportWidth
            || _pageDistance != _lastDistance || _dotSize != _lastDotSize || _spacing != _lastSpacing
            || _activeSprite != _lastActive || _inactiveSprite != _lastInactive) Refresh();
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
