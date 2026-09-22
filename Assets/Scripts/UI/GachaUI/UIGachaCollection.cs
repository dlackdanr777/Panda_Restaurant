using System;
using System.Collections.Generic;
using Muks.MobileUI;
using UnityEngine;

/// <summary>The existing two-machine view owns one exchange modal and one account-scoped service.</summary>
public partial class UIGacha
{
    public GachaEconomyService Economy { get; private set; }
    public TokenExchangeView CollectionExchange => _collectionExchange;
    public GachaCollectionMachineHud CollectionHud => _collectionHud;
    public string CollectionError { get; private set; }
    private TokenExchangeView _collectionExchange;
    private GachaCollectionMachineHud _collectionHud;
    private GachaCollectionUiTheme _collectionTheme;
    private GachaMachineKind _collectionHudMachine;
    private bool _collectionWasDrawing;
    private float _nextCollectionRefresh;
    private Action<bool, string> _pendingExchangeReply;
    private GachaEconomyTransaction _pendingExchangeTransaction;
    private readonly HashSet<GachaAcquisitionResult> _revealedCollectionResults = new HashSet<GachaAcquisitionResult>();
    private GachaEconomyTransaction _revealTransaction;
    private string _reportedCollectionFailure;
    private readonly List<HiddenCollectionLayer> _hiddenCollectionLayers = new List<HiddenCollectionLayer>();
    private sealed class HiddenCollectionLayer
    {
        public CanvasGroup Group;
        public bool Interactable, BlocksRaycasts;
        public float Alpha;
    }

    public void BindCollectionEconomy(GachaEconomyService economy)
    {
        if (ReferenceEquals(Economy, economy) && _collectionHud != null) return;
        HideCollectionUI();
        _collectionExchange?.ResetSession();
        _pendingExchangeReply = null;
        _pendingExchangeTransaction = null;
        _reportedCollectionFailure = null;
        _revealedCollectionResults.Clear();
        Economy = economy;
        foreach (var machine in _gachaMachines ?? Array.Empty<GachaMachineParent>())
        {
            if (machine is UIStaffGacha staff) staff.BindCollectionEconomy(this, economy);
            if (machine is UIItemGacha item) item.BindCollectionEconomy(this, economy);
        }
        if (_collectionHud == null)
        {
            _collectionTheme = GachaCollectionUiTheme.Load();
            if (_collectionTheme == null || _collectionTheme.PandaToken == null)
            { CollectionError = "토큰 교환소 UI 설정이 없습니다."; return; }
            _collectionHud = GachaCollectionMachineHud.Attach((RectTransform)transform, _collectionTheme,
                OpenCollectionExchange, DrawCollectionTicket);
            _collectionExchange = TokenExchangeView.Attach(transform, _collectionTheme,
                ReadExchangeSnapshot, PurchaseExchangeProduct, RefreshExchangeDisplay, () =>
                {
                    RestoreCollectionLayers();
                    _collectionHud?.SnapToCommitted();
                });
        }
        _nextCollectionRefresh = 0;
    }

    private GachaMachineKind CollectionMachine => _currentGachaMachine is UIStaffGacha
        ? GachaMachineKind.Staff : GachaMachineKind.Item;

    private void UpdateCollectionUI()
    {
        var observed = Economy?.LastTransaction;
        if (observed != null && (observed.Status == GachaTransactionStatus.Rejected ||
            observed.Status == GachaTransactionStatus.Indeterminate))
        {
            string identity = observed.Id + ":" + observed.Status;
            if (_reportedCollectionFailure != identity)
            { _reportedCollectionFailure = identity; ReportCollectionError(observed.Error ?? "거래 결과를 확인하고 있습니다."); }
        }
        if (_pendingExchangeReply != null && _pendingExchangeTransaction != null &&
            (_pendingExchangeTransaction.Status == GachaTransactionStatus.Rejected ||
             _pendingExchangeTransaction.Status == GachaTransactionStatus.Indeterminate))
        {
            var reply = _pendingExchangeReply;
            _pendingExchangeReply = null;
            reply(false, _pendingExchangeTransaction.Error ?? "저장 결과를 확인하고 있습니다.");
        }
#if UNITY_EDITOR
        bool allowRuntime = !_editorOfflineConfigured;
#else
        const bool allowRuntime = true;
#endif
        if (allowRuntime && _isInitialized && VisibleState == VisibleState.Appeared)
        {
            var current = Muks.BackEnd.BackendManager.Instance.GachaEconomy;
            if (!ReferenceEquals(Economy, current)) BindCollectionEconomy(current);
        }
        if (_collectionHud == null) return;
        bool visible = Economy != null && !_questStaffEntry && VisibleState == VisibleState.Appeared;
        _collectionHud.gameObject.SetActive(visible);
        if (!visible) { _collectionExchange?.SetVisible(false); return; }
        _collectionHud.SetResultPresentation(_isStartGacha);
        var machine = CollectionMachine;
        if (machine != _collectionHudMachine || (_collectionWasDrawing && !_isStartGacha))
            _collectionHud.SnapToCommitted();
        _collectionHudMachine = machine;
        _collectionWasDrawing = _isStartGacha;
        if (Time.unscaledTime < _nextCollectionRefresh) return;
        _nextCollectionRefresh = Time.unscaledTime + .1f;
        var state = Economy.Snapshot?.Account?.GachaEconomy;
        _collectionHud.Refresh(state?.Counter(machine) ?? 0, state?.Tickets(machine) ?? 0,
            Economy.IsBusy || _isStartGacha || _collectionExchange.IsOpen,
            Economy.CanDraw(machine, GachaPaymentKind.TicketSingle, out _));
    }

    public void OpenCollectionExchange()
    {
        if (Economy == null || Economy.IsBusy || _isStartGacha || _questStaffEntry ||
            VisibleState != VisibleState.Appeared) return;
        CollectionError = null;
        HideCollectionLayers();
        _collectionExchange.SetVisible(true);
        var service = Economy;
        if (!service.ExchangeCatalogReady && !service.TryEnsureExchangeCatalog(transaction =>
        {
            if (this != null && ReferenceEquals(Economy, service)) _collectionExchange?.Refresh();
        }, out string error)) ReportCollectionError(error);
        _collectionExchange.Refresh();
    }

    public void DrawCollectionTicket()
    {
        if (Economy == null || Economy.IsBusy || _isStartGacha || _questStaffEntry ||
            VisibleState != VisibleState.Appeared || _collectionExchange.IsOpen) return;
        var machine = CollectionMachine;
        var source = _currentGachaMachine;
        var service = Economy;
        if (!service.TryDraw(machine, GachaPaymentKind.TicketSingle, transaction =>
        {
            if (this == null || !ReferenceEquals(Economy, service) || !gameObject.activeInHierarchy ||
                !IsCurrentMachine(source)) return;
            if (source is UIItemGacha item) item.PresentCollectionTransaction(transaction);
            if (source is UIStaffGacha staff) staff.PresentCollectionTransaction(service, transaction);
        }, out string error)) ReportCollectionError(error);
    }

    private TokenExchangeSnapshot ReadExchangeSnapshot()
    {
        var result = new TokenExchangeSnapshot { Busy = Economy == null || Economy.IsBusy };
        if (Economy == null) return result;
        result.PandaTokens = Economy.Snapshot?.Account?.PandaTokens ?? 0;
        result.StaffVersion = Economy.GetExchangeDisplay(GachaExchangeCategory.Staff)?.Version ?? 0;
        result.ItemVersion = Economy.GetExchangeDisplay(GachaExchangeCategory.Items)?.Version ?? 0;
        var refresh = Economy.GetRefreshStatus();
        result.RemainingRefreshes = refresh.Remaining; result.RefreshLimit = refresh.Limit;
        result.RefreshMessage = refresh.Message;
        result.CanRefreshStaff = Economy.CanRefreshExchange(GachaExchangeCategory.Staff, result.StaffVersion, out _);
        result.CanRefreshItems = Economy.CanRefreshExchange(GachaExchangeCategory.Items, result.ItemVersion, out _);
        var products = new List<TokenExchangeProductView>();
        foreach (GachaExchangeCategory category in new[] { GachaExchangeCategory.Tickets, GachaExchangeCategory.Staff, GachaExchangeCategory.Items })
        foreach (var product in Economy.GetDisplayedProducts(category))
        {
            int version = category == GachaExchangeCategory.Staff ? result.StaffVersion :
                category == GachaExchangeCategory.Items ? result.ItemVersion : 0;
            bool available = Economy.CanExchange(product.Id, 1, version, out string error);
            products.Add(new TokenExchangeProductView
            {
                Id = product.Id, Name = product.Name, Description = product.Description,
                Sprite = product.Sprite != null ? product.Sprite :
                    product.Category == GachaExchangeCategory.Tickets ? _collectionTheme.Ticket : null,
                Price = product.Price, Quantity = product.Quantity, DisplayVersion = version,
                Rank = product.Data != null ? product.Data.Rank : Rank.Normal1,
                Category = (TokenExchangeTab)product.Category, CanPurchase = available,
                Status = available ? (product.IsRepeatable ? "반복 교환 가능" :
                    product.Category == GachaExchangeCategory.Staff ? "미보유 직원" : "미해금 레시피") : error
            });
        }
        result.Products = products;
        return result;
    }

    private void PurchaseExchangeProduct(string id, int displayVersion, Action<bool, string> complete)
    {
        if (Economy == null) { complete(false, "계정 복원을 기다려 주세요."); return; }
        var service = Economy;
        _pendingExchangeReply = complete;
        if (!service.TryExchange(id, 1, displayVersion, transaction =>
        {
            if (!ReferenceEquals(Economy, service)) return;
            _pendingExchangeReply = null;
            complete(true, "교환 완료!");
        }, out string error))
        { _pendingExchangeReply = null; complete(false, error); }
        if (ReferenceEquals(Economy, service)) _pendingExchangeTransaction = service.LastTransaction;
    }

    private void RefreshExchangeDisplay(TokenExchangeTab tab, int displayVersion, Action<bool, string> complete)
    {
        if (Economy == null) { complete(false, "계정 복원을 기다려 주세요."); return; }
        var service = Economy;
        _pendingExchangeReply = complete;
        string requestId = Guid.NewGuid().ToString("N");
        if (!service.TryRefreshExchange((GachaExchangeCategory)tab, displayVersion, requestId, transaction =>
        {
            if (!ReferenceEquals(Economy, service)) return;
            _pendingExchangeReply = null;
            complete(true, "새 진열이 준비되었습니다!");
        }, out string error))
        { _pendingExchangeReply = null; complete(false, error); }
        if (ReferenceEquals(Economy, service)) _pendingExchangeTransaction = service.LastTransaction;
    }

    private void HideCollectionLayers()
    {
        if (_hiddenCollectionLayers.Count != 0) return;
        foreach (Transform child in transform)
        {
            if (_collectionExchange != null && child == _collectionExchange.transform) continue;
            var group = child.GetComponent<CanvasGroup>();
            bool created = group == null;
            if (created) group = child.gameObject.AddComponent<CanvasGroup>();
            _hiddenCollectionLayers.Add(new HiddenCollectionLayer { Group = group,
                Alpha = group.alpha, Interactable = group.interactable, BlocksRaycasts = group.blocksRaycasts });
            group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
        }
    }

    private void RestoreCollectionLayers()
    {
        foreach (var layer in _hiddenCollectionLayers)
        {
            if (layer.Group == null) continue;
            layer.Group.alpha = layer.Alpha; layer.Group.interactable = layer.Interactable;
            layer.Group.blocksRaycasts = layer.BlocksRaycasts;
            // Keep neutral runtime groups for reuse. Destroying a newly added group at frame end would
            // invalidate a same-frame reopen which has already reused it to hide the native machine.
        }
        _hiddenCollectionLayers.Clear();
    }

    public void NotifyCollectionReveal(GachaAcquisitionResult result)
    {
        if (!ReferenceEquals(_revealTransaction, Economy?.LastTransaction))
        { _revealedCollectionResults.Clear(); _revealTransaction = Economy?.LastTransaction; }
        if (result != null && _collectionHud != null && _revealedCollectionResults.Add(result))
            _collectionHud.PresentResult(result.CounterBefore, result.CounterAfter, result.IsGuaranteedSpecial);
    }

    public void ReportCollectionError(string error)
    {
        CollectionError = error;
#if UNITY_EDITOR
        if (_editorOfflineConfigured) { Debug.Log("[Offline collection] " + error); return; }
#endif
        if (!string.IsNullOrEmpty(error)) PopupManager.Instance.ShowDisplayText(error);
    }

    private void HideCollectionUI()
    {
        _collectionExchange?.SetVisible(false);
        _collectionHud?.SnapToCommitted();
    }

#if UNITY_EDITOR
    public void SelectCollectionOfflineMachine(GachaMachineKind machine)
    {
        if (!_editorOfflineConfigured || !_editorOfflineNavigation) throw new InvalidOperationException("Offline navigation required.");
        HideCollectionUI();
        SetStartGacha(false);
        foreach (var value in _gachaMachines)
            if ((machine == GachaMachineKind.Staff && value is UIStaffGacha) ||
                (machine == GachaMachineKind.Item && value is UIItemGacha))
            { SetMachineNoAnime(value); SetMachineParentPos(); break; }
        _nextCollectionRefresh = 0;
        UpdateCollectionUI();
    }
#endif
}
