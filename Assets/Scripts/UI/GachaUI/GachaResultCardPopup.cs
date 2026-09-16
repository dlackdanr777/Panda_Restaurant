using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only inspection of an already revealed result; never replaces the +1 card.</summary>
public sealed class GachaResultCardPopup : IDisposable
{
    private readonly GameObject _root;
    private readonly Button _background;
    private readonly RectTransform _content;
    private readonly UIGachaCard _card;
    public bool IsOpen => _root != null && _root.activeInHierarchy;
    public UIGachaCard Card => _card;

    public GachaResultCardPopup(Transform parent, UIGachaCard template)
    {
        if (parent == null || template == null) throw new ArgumentNullException();
        _root = new GameObject("Result Card Inspection", typeof(RectTransform));
        _root.SetActive(false);
        var rootRect = (RectTransform)_root.transform;
        rootRect.SetParent(parent, false);
        Stretch(rootRect);

        var backdrop = new GameObject("Dismiss Background", typeof(RectTransform), typeof(Image), typeof(Button));
        backdrop.transform.SetParent(rootRect, false);
        Stretch((RectTransform)backdrop.transform);
        var shade = backdrop.GetComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.65f);
        _background = backdrop.GetComponent<Button>();
        _background.targetGraphic = shade;
        _background.transition = Selectable.Transition.None;
        _background.onClick.AddListener(Hide);

        _content = new GameObject("Centered Card", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        _content.SetParent(rootRect, false);
        _content.anchorMin = _content.anchorMax = _content.pivot = new Vector2(0.5f, 0.5f);
        _content.sizeDelta = ((RectTransform)template.transform).rect.size;
        var blocker = _content.GetComponent<Image>();
        blocker.color = Color.clear;
        var cardInput = _content.GetComponent<Button>();
        cardInput.targetGraphic = blocker;
        cardInput.transition = Selectable.Transition.None;
        // A card click is consumed here; only the surrounding background dismisses.
        _card = UnityEngine.Object.Instantiate(template, _content, false);
        _card.name = "Inspected Result Card";
        // The bonus may still be popping in when selected. Runtime tween state is
        // not cloned, so an enabled copied tween must not overwrite this static card.
        foreach (Muks.Tween.TweenData tween in _card.GetComponentsInChildren<Muks.Tween.TweenData>(true)) tween.Clear();
        foreach (Button button in _card.GetComponentsInChildren<Button>(true))
        {
            button.onClick.RemoveAllListeners();
            button.enabled = false;
        }
        foreach (Graphic graphic in _card.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        _card.Init();
        var cardRect = (RectTransform)_card.transform;
        cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = _content.sizeDelta;
        _card.SetPosition(Vector3.zero);
        _card.SetScale(1f);
        _card.gameObject.SetActive(true);
    }

    public bool ShowStaff(GachaStaffData staff, StaffGachaAcquisitionItem acquisition)
    {
        if (!_card.TrySetStaffAcquisitionResult(staff, acquisition)) { Hide(); return false; }
        Show();
        return true;
    }

    public void ShowItem(GachaItemData item)
    {
        if (item == null) { Hide(); return; }
        _card.SetData(item);
        Show();
    }

    private void Show()
    {
        _root.SetActive(true);
        Canvas.ForceUpdateCanvases();
        BringToFront();
    }

    public void BringToFront()
    {
        if (!IsOpen) return;
        _root.transform.SetAsLastSibling();
        Vector2 viewport = ((RectTransform)_root.transform).rect.size;
        Vector2 size = _content.rect.size;
        float scale = Mathf.Min(viewport.x * 0.85f / Mathf.Max(1f, size.x), viewport.y * 0.85f / Mathf.Max(1f, size.y));
        _content.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        _content.anchoredPosition = Vector2.zero;
    }

    public void Hide() { if (_root != null) _root.SetActive(false); }

    public void Dispose()
    {
        if (_root == null) return;
        Hide();
        _background.onClick.RemoveListener(Hide);
        if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
        else UnityEngine.Object.DestroyImmediate(_root);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}

/// <summary>Temporary full-card input for the native, non-raycastable result artwork.</summary>
internal sealed class GachaResultCardHitArea : IDisposable
{
    private readonly GameObject _root;

    public GachaResultCardHitArea(Transform card)
    {
        _root = new GameObject("Result Card Hit Area", typeof(RectTransform), typeof(Image));
        _root.layer = card.gameObject.layer;
        var rect = (RectTransform)_root.transform;
        rect.SetParent(card, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = _root.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        // The parent card's Button receives the event. Merely adding a Button to
        // its non-Graphic root does not stop a click reaching the result backdrop.
    }

    public void Dispose()
    {
        if (_root == null) return;
        _root.SetActive(false);
        if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
        else UnityEngine.Object.DestroyImmediate(_root);
    }
}
