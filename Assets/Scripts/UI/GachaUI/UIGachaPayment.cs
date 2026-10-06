using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class UIGacha
{
    private GachaPaymentChoiceView _collectionPayment;
    public GachaPaymentChoiceView CollectionPayment => _collectionPayment;
    public bool IsChoosingPayment => _collectionPayment != null && _collectionPayment.IsOpen;
#if UNITY_EDITOR
    // Disposable preview/test owners never fall back to live purchases or stores.
    public Action EditorOfflineDiamondStore;
    public Action<GachaEconomyTransaction> EditorOfflineCollectionPresentation;
#endif

    public void OpenCollectionPayment(GachaMachineParent source, bool eleven)
    {
        if (!CanOpenCollectionPayment(source) || IsChoosingPayment) return;
        if (_collectionPayment == null)
        {
            _collectionPayment = GachaPaymentChoiceView.Attach(transform, _collectionTheme);
            _collectionPayment.Closed = () => SetStartGacha(_isStartGacha);
        }
        var service = Economy;
        var machine = CollectionMachine;
        var state = service.Snapshot;
        if (state?.Account == null) return;
        var ticket = eleven ? GachaPaymentKind.TicketEleven : GachaPaymentKind.TicketSingle;
        var entry = source is UIStaffGacha staffSource ? staffSource.SingleButton : ((UIItemGacha)source).SingleButton;
        var money = entry.transform.Find("Money Image")?.GetComponent<Image>();
        _collectionPayment.Show(machine, eleven, state.Diamonds, state.Account.GachaEconomy.Tickets(machine),
            money != null ? money.sprite : null, service.CanDraw(machine, ticket, out _), useTicket =>
            {
                if (!ReferenceEquals(Economy, service) || !CanOpenCollectionPayment(source)) return;
                var payment = useTicket ? ticket : eleven ? GachaPaymentKind.DiamondsEleven : GachaPaymentKind.DiamondsSingle;
                // Insufficient diamonds must never enter the transaction/RNG path.
                if (!useTicket && service.Snapshot.Diamonds < GachaDrawPlan.ForPayment(payment).DiamondCost)
                { OpenCollectionDiamondStore(); return; }
                if (!service.TryDraw(machine, payment, transaction =>
                {
                    if (this == null || !ReferenceEquals(Economy, service) || !isActiveAndEnabled ||
                        !IsCurrentMachine(source) || VisibleState != VisibleState.Appeared) return;
#if UNITY_EDITOR
                    if (_editorOfflineConfigured && EditorOfflineCollectionPresentation != null)
                    { EditorOfflineCollectionPresentation(transaction); return; }
#endif
                    if (source is UIItemGacha item) item.PresentCollectionTransaction(transaction);
                    if (source is UIStaffGacha staff) staff.PresentCollectionTransaction(service, transaction);
                }, out string error)) ReportCollectionError(error);
            });
        _collectionPayment.HideSourceButtons(entry, source is UIStaffGacha staffButtons
            ? staffButtons.TenButton : ((UIItemGacha)source).TenButton);
        SetNavigationButtonsActive(false);
        if (_scrollRect != null) { _scrollRect.StopMovement(); _scrollRect.enabled = false; }
    }

    private bool CanOpenCollectionPayment(GachaMachineParent source) => source != null &&
        Economy != null && !Economy.IsBusy && !_isStartGacha && !_questStaffEntry &&
        isActiveAndEnabled && VisibleState == VisibleState.Appeared && IsCurrentMachine(source) &&
        (_collectionExchange == null || !_collectionExchange.IsOpen);

    private void OpenCollectionDiamondStore()
    {
#if UNITY_EDITOR
        if (_editorOfflineConfigured) { EditorOfflineDiamondStore?.Invoke(); return; }
#endif
        if (_uiNav == null || _uiNav.GetVisibleState("UIPayment") != VisibleState.Disappeared) return;
        // Resolve within this view's Stage1 UI owner, including the inactive purchase view.
        var payment = transform.parent != null ? transform.parent.GetComponentInChildren<UIPayment>(true) : null;
        if (payment != null) payment.ShowDiaUI();
    }

    internal static void ConfigureCollectionButton(Button button, bool eleven, Button plainTemplate = null)
    {
        if (button == null) return;
        GachaButtonInputSound.Bind(button);
        if (eleven && plainTemplate != null)
        {
            // The old bonus banner is baked into the multi-draw sprite. Reuse the plain wood button.
            button.image.sprite = plainTemplate.image.sprite;
            var rect = (RectTransform)button.transform;
            var template = (RectTransform)plainTemplate.transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, template.sizeDelta.y);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, template.anchoredPosition.y);
        }
        var label = button.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = eleven ? "10+1회 뽑기" : "1회 뽑기";
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.enableAutoSizing = true;
        }
        var money = button.transform.Find("Money Image");
        if (money != null) money.gameObject.SetActive(false);
        var description = button.transform.Find("Description Text");
        if (description != null) description.gameObject.SetActive(false);
    }
}
