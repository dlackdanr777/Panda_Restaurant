using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using E = GachaCollectionUiElements;

/// <summary>A reusable modal; opening/cancelling never reserves currency or rolls results.</summary>
public sealed class GachaPaymentChoiceView : MonoBehaviour, IPointerClickHandler
{
    private Button _diamonds, _tickets;
    private TextMeshProUGUI _product, _diamondText, _ticketText;
    private Image _diamondIcon;
    private RectTransform _frame;
    private CanvasGroup _sourceSingle, _sourceTen;
    private float _sourceSingleAlpha, _sourceTenAlpha;
    private Action<bool> _choose;
    public Action Closed;
    public bool IsOpen => gameObject.activeSelf;
    public Button DiamondButton => _diamonds;
    public Button TicketButton => _tickets;

    public static GachaPaymentChoiceView Attach(Transform parent, GachaCollectionUiTheme theme)
    {
        var root = E.Rect("Gacha Payment Choice", parent, 0, 0, 0, 0);
        E.Stretch(root);
        var shade = root.gameObject.AddComponent<Image>();
        shade.color = new Color(0, 0, 0, .56f);
        var view = root.gameObject.AddComponent<GachaPaymentChoiceView>();
        var font = E.FontFrom(parent, theme);
        var frame = E.Icon("Payment Frame", root, theme.PaymentFrame, 0, 0, 620, 315);
        frame.preserveAspect = false;
        frame.raycastTarget = true;
        var plate = frame.rectTransform;
        view._frame = plate;
        plate.anchorMin = plate.anchorMax = new Vector2(.5f, 1);
        plate.pivot = Vector2.one * .5f;
        plate.localScale = Vector3.one * 1.5f;
        // Keep the capsule below the top-anchored guarantee gauge on wide screens too.
        plate.anchoredPosition = new Vector2(0, -604);
        AddCapsule(plate, "Blue Capsule", theme.PaymentBlueCapsuleTop, theme.PaymentBlueCapsuleBottom,
            new Vector2(243, -4), 118, 18);
        AddCapsule(plate, "Purple Capsule", theme.PaymentPurpleCapsuleTop, theme.PaymentPurpleCapsuleBottom,
            new Vector2(384, 2), 124, -28);
        AddCapsule(plate, "Capsule Badge", theme.PaymentCapsuleTop, theme.PaymentCapsuleBottom,
            new Vector2(310, -20), 170, -15);
        AddCapsule(plate, "Left Capsule", theme.PaymentLeftCapsuleTop, theme.PaymentLeftCapsuleBottom,
            new Vector2(178, 29), 76, 30);
        AddCapsule(plate, "Right Capsule", theme.PaymentRightCapsuleTop, theme.PaymentRightCapsuleBottom,
            new Vector2(444, 35), 70, -23);
        AddCapsule(plate, "Tiny Gold Capsule", theme.PaymentCapsuleTop, theme.PaymentCapsuleBottom,
            new Vector2(229, 57), 42, -35);
        AddCapsule(plate, "Tiny Green Capsule", theme.PaymentLeftCapsuleTop, theme.PaymentLeftCapsuleBottom,
            new Vector2(405, 60), 46, 15);
        E.ArtButton("Close Payment", plate, font, "", theme.PaymentClose, 568, 8, 44, 44,
            theme.Ink, view.Close, SoundEffectType.ButtonExitSound);
        view._product = E.Label("Selected Product", plate, font, "", 50, 36, 520, 102, 36, theme.Ink);
        var prompt = view._product.rectTransform;
        prompt.anchorMin = prompt.anchorMax = prompt.pivot = Vector2.one * .5f;
        prompt.anchoredPosition = Vector2.zero;
        view._product.fontSizeMin = 34;
        view._product.textWrappingMode = TextWrappingModes.NoWrap;
        view._diamonds = E.ArtButton("Choose Diamonds", plate, font, "", theme.PaymentButton,
            123, 118, 104, 45, theme.Ink, () => view.Choose(false));
        view._tickets = E.ArtButton("Choose Machine Tickets", plate, font, "", theme.PaymentButton,
            393, 118, 104, 45, theme.Ink, () => view.Choose(true));
        foreach (var button in new[] { view._diamonds, view._tickets })
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
            rect.localScale = Vector3.one * 2;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 23);
        }
        view._diamondIcon = E.Icon("Diamond Icon", view._diamonds.transform, null, 7, 10, 16, 16);
        E.Icon("Ticket Icon", view._tickets.transform, theme.Ticket, 5, 11, 18, 13);
        view._diamondText = E.Label("Diamond Cost", view._diamonds.transform, font,
            "", 27, 10, 72, 22, 14, theme.Ink);
        view._ticketText = E.Label("Ticket Cost", view._tickets.transform, font,
            "", 27, 10, 72, 22, 14, theme.Ink);
        view._diamondText.fontSizeMin = view._ticketText.fontSizeMin = 11;
        view.Close();
        return view;
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
