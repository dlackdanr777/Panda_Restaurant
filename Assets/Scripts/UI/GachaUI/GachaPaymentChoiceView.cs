using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using E = GachaCollectionUiElements;

/// <summary>A reusable modal; opening/cancelling never reserves currency or rolls results.</summary>
public sealed class GachaPaymentChoiceView : MonoBehaviour, IPointerClickHandler
{
    public const string PrefabResourcePath = "UI/GachaCollection/GachaPaymentChoiceView";

    private GachaCollectionUiTheme _theme;
    private TMP_FontAsset _font;
    [SerializeField] private Image _shade, _diamondIcon;
    [SerializeField] private Button _diamonds, _tickets, _closeButton;
    [SerializeField] private TextMeshProUGUI _product, _diamondText, _ticketText;
    [SerializeField] private RectTransform _frame;
    private CanvasGroup _sourceSingle, _sourceTen;
    private float _sourceSingleAlpha, _sourceTenAlpha;
    private Action<bool> _choose;
    public Action Closed;
    public bool IsOpen => gameObject.activeSelf;
    public Button DiamondButton => _diamonds;
    public Button TicketButton => _tickets;

    public static GachaPaymentChoiceView Attach(Transform parent, GachaCollectionUiTheme theme)
    {
        if (parent == null || theme == null || theme.PaymentFrame == null)
            throw new ArgumentException("결제 선택 UI 테마가 필요합니다.");

        GachaPaymentChoiceView prefab = Resources.Load<GachaPaymentChoiceView>(PrefabResourcePath);
        bool usePrefab = prefab != null && prefab.HasPrefabLayout();
        GachaPaymentChoiceView view;
        if (usePrefab)
        {
            view = Instantiate(prefab, parent, false);
            view.gameObject.name = "Gacha Payment Choice";
        }
        else
        {
            var root = E.Rect("Gacha Payment Choice", parent, 0, 0, 0, 0);
            E.Stretch(root);
            view = root.gameObject.AddComponent<GachaPaymentChoiceView>();
        }

        view.Configure(theme, parent);
        if (!usePrefab)
            view.Build();
        view.BindCallbacks();
        view.Close();
        return view;
    }

    private bool HasPrefabLayout()
    {
        return _shade != null && _frame != null && _closeButton != null && _product != null
            && _diamonds != null && _tickets != null && _diamondIcon != null
            && _diamondText != null && _ticketText != null;
    }

    private void Configure(GachaCollectionUiTheme theme, Transform parent)
    {
        _theme = theme;
        _font = E.FontFrom(parent, theme);
    }

    private void Build()
    {
        _shade = gameObject.AddComponent<Image>();
        _shade.color = new Color(0, 0, 0, .56f);
        _shade.raycastTarget = true;
        var frame = E.Icon("Payment Frame", transform, _theme.PaymentFrame, 0, 0, 620, 315);
        frame.preserveAspect = false;
        frame.raycastTarget = true;
        var plate = frame.rectTransform;
        _frame = plate;
        plate.anchorMin = plate.anchorMax = new Vector2(.5f, 1);
        plate.pivot = Vector2.one * .5f;
        plate.localScale = Vector3.one * 1.5f;
        // Keep the capsule below the top-anchored guarantee gauge on wide screens too.
        plate.anchoredPosition = new Vector2(0, -604);
        AddCapsule(plate, "Blue Capsule", _theme.PaymentBlueCapsuleTop, _theme.PaymentBlueCapsuleBottom,
            new Vector2(243, -4), 118, 18);
        AddCapsule(plate, "Purple Capsule", _theme.PaymentPurpleCapsuleTop, _theme.PaymentPurpleCapsuleBottom,
            new Vector2(384, 2), 124, -28);
        AddCapsule(plate, "Capsule Badge", _theme.PaymentCapsuleTop, _theme.PaymentCapsuleBottom,
            new Vector2(310, -20), 170, -15);
        AddCapsule(plate, "Left Capsule", _theme.PaymentLeftCapsuleTop, _theme.PaymentLeftCapsuleBottom,
            new Vector2(178, 29), 76, 30);
        AddCapsule(plate, "Right Capsule", _theme.PaymentRightCapsuleTop, _theme.PaymentRightCapsuleBottom,
            new Vector2(444, 35), 70, -23);
        AddCapsule(plate, "Tiny Gold Capsule", _theme.PaymentCapsuleTop, _theme.PaymentCapsuleBottom,
            new Vector2(229, 57), 42, -35);
        AddCapsule(plate, "Tiny Green Capsule", _theme.PaymentLeftCapsuleTop, _theme.PaymentLeftCapsuleBottom,
            new Vector2(405, 60), 46, 15);
        _closeButton = E.ArtButton("Close Payment", plate, _font, "", _theme.PaymentClose, 568, 8, 44, 44,
            _theme.Ink, null, SoundEffectType.ButtonExitSound);
        _product = E.Label("Selected Product", plate, _font, "", 50, 36, 520, 102, 36, _theme.Ink);
        var prompt = _product.rectTransform;
        prompt.anchorMin = prompt.anchorMax = prompt.pivot = Vector2.one * .5f;
        prompt.anchoredPosition = Vector2.zero;
        _product.fontSizeMin = 34;
        _product.textWrappingMode = TextWrappingModes.NoWrap;
        _diamonds = E.ArtButton("Choose Diamonds", plate, _font, "", _theme.PaymentButton,
            123, 118, 104, 45, _theme.Ink, null);
        _tickets = E.ArtButton("Choose Machine Tickets", plate, _font, "", _theme.PaymentButton,
            393, 118, 104, 45, _theme.Ink, null);
        foreach (var button in new[] { _diamonds, _tickets })
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
            rect.localScale = Vector3.one * 2;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 23);
        }
        _diamondIcon = E.Icon("Diamond Icon", _diamonds.transform, null, 7, 10, 16, 16);
        E.Icon("Ticket Icon", _tickets.transform, _theme.Ticket, 5, 11, 18, 13);
        _diamondText = E.Label("Diamond Cost", _diamonds.transform, _font,
            "", 11, 0, 72, 22.5556f, 14, _theme.Ink);
        _ticketText = E.Label("Ticket Cost", _tickets.transform, _font,
            "", 11, 0, 72, 22.4578f, 14, _theme.Ink);
        ConfigureCostRect(_diamondText.rectTransform, new Vector2(11, 4.1949005f), new Vector2(72, 22.5556f));
        ConfigureCostRect(_ticketText.rectTransform, new Vector2(11, 4.4450912f), new Vector2(72, 22.4578f));
        _diamondText.fontSizeMin = _ticketText.fontSizeMin = 11;
    }

    private static void ConfigureCostRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void BindCallbacks()
    {
        GachaButtonInputSound.Bind(_closeButton, SoundEffectType.ButtonExitSound);
        GachaButtonInputSound.Bind(_diamonds);
        GachaButtonInputSound.Bind(_tickets);
        _closeButton.onClick.AddListener(Close);
        _diamonds.onClick.AddListener(() => Choose(false));
        _tickets.onClick.AddListener(() => Choose(true));
    }

    public void Show(GachaMachineKind machine, bool eleven, int diamonds, int tickets,
        Sprite diamondIcon, bool ticketAvailable, Action<bool> choose)
    {
        _choose = choose;
        _diamondIcon.sprite = diamondIcon;
        _diamondIcon.enabled = diamondIcon != null;
        _product.text = (machine == GachaMachineKind.Staff ? "스텝" : "아이템") +
            (eleven ? " 10+1회 뽑기\n진행하시겠습니까?" : " 1회 뽑기\n진행하시겠습니까?");
        _diamondText.text = "다이아 x" + (eleven ? 100 : 10);
        _ticketText.text = "뽑기권 x" + (eleven ? 10 : 1);
        _diamonds.interactable = true;
        _tickets.interactable = ticketAvailable;
        gameObject.SetActive(true);
        Fit();
        transform.SetAsLastSibling();
    }

    private void OnRectTransformDimensionsChange() => Fit();
    private void Fit()
    {
        if (_frame == null) return;
        float height = ((RectTransform)transform).rect.height;
        if (height <= 0) return;
        // The enlarged choices overhang the frame by 22 units; keep them on wide screens.
        float belowCenter = (_frame.rect.height * .5f + 22) * _frame.localScale.y;
        _frame.anchoredPosition = new Vector2(0, -Mathf.Min(604, height - belowCenter - 12));
    }

    public void HideSourceButtons(Button single, Button ten)
    {
        RestoreSourceButtons();
        if (!IsOpen) return;
        _sourceSingle = HideSourceButton(single, out _sourceSingleAlpha);
        _sourceTen = HideSourceButton(ten, out _sourceTenAlpha);
    }
    private static CanvasGroup HideSourceButton(Button button, out float alpha)
    {
        alpha = 1;
        if (button == null) return null;
        var group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
        alpha = group.alpha;
        group.alpha = 0;
        return group;
    }
    private void RestoreSourceButtons()
    {
        if (_sourceSingle != null) _sourceSingle.alpha = _sourceSingleAlpha;
        if (_sourceTen != null) _sourceTen.alpha = _sourceTenAlpha;
        _sourceSingle = _sourceTen = null;
    }

    private static void AddCapsule(RectTransform parent, string name, Sprite top, Sprite bottom,
        Vector2 center, float size, float angle)
    {
        var capsule = E.Rect(name, parent, 0, 0, size, size);
        capsule.pivot = Vector2.one * .5f;
        capsule.anchoredPosition = new Vector2(center.x, -center.y);
        capsule.localRotation = Quaternion.Euler(0, 0, angle);
        var lower = E.Icon("Capsule Bottom", capsule, bottom, 0, 0, size, size);
        var upper = E.Icon("Capsule Top", capsule, top, 0, 0, size, size);
        foreach (var half in new[] { lower, upper })
            half.rectTransform.anchorMin = half.rectTransform.anchorMax = half.rectTransform.pivot = Vector2.one * .5f;
        // Scale the native closed-shell overlap together; the upper shell stays in front.
        lower.rectTransform.anchoredPosition = new Vector2(0, -20.63f * size / 170);
        upper.rectTransform.anchoredPosition = new Vector2(0, 25.74f * size / 170);
    }

    private void Choose(bool tickets)
    {
        if (!IsOpen || (tickets && !_tickets.interactable)) return;
        var choose = _choose;
        Close(); // Retire the callback before synchronous completion or a repeated click.
        choose?.Invoke(tickets);
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        // Interior whitespace and disabled buttons must never act as an outside click.
        if (IsOpen && isActiveAndEnabled && eventData != null &&
            eventData.button == PointerEventData.InputButton.Left && eventData.pointerCurrentRaycast.gameObject == gameObject)
        { GachaButtonInputSound.Play(SoundEffectType.ButtonExitSound); Close(); }
    }
    public void Close()
    {
        bool wasOpen = IsOpen;
        RestoreSourceButtons();
        _choose = null; gameObject.SetActive(false);
        if (wasOpen) Closed?.Invoke();
    }
    private void OnDisable()
    {
        _choose = null;
        RestoreSourceButtons();
    }
}
