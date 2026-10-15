using System;
using System.Linq;
using System.Collections.Generic;
using Muks.MobileUI;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The existing two-machine view owns one exchange modal and one account-scoped service.</summary>
public partial class UIGacha
{
    public GachaEconomyService Economy { get; private set; }
    public TokenExchangeView CollectionExchange => _collectionExchange;
    public GachaCollectionMachineHud CollectionHud => _collectionHud;
    public string CollectionError { get; private set; }
    [Header("Token Exchange")]
    [SerializeField] private TokenExchangeView _collectionExchange;
    [SerializeField] private Button _collectionExchangeButton;
    private GachaCollectionMachineHud _collectionHud;
    private GachaCollectionUiTheme _collectionTheme;
    private GachaMachineKind _collectionHudMachine;
    private bool _collectionWasDrawing;
    private float _nextCollectionRefresh;
    private Action<bool, string> _pendingExchangeReply;
    private GachaEconomyTransaction _pendingExchangeTransaction;
    private readonly HashSet<GachaAcquisitionResult> _revealedCollectionResults = new HashSet<GachaAcquisitionResult>();
    private GachaEconomyTransaction _revealTransaction;
    private bool _hasReportedCollectionFailure;
    private string _reportedCollectionFailureId;
    private GachaTransactionStatus _reportedCollectionFailureStatus;
    private bool _collectionHudShown = true;
    private static readonly ProfilerMarker ExchangeSnapshotMarker = new ProfilerMarker("UIGacha.ReadExchangeSnapshot.Rebuild");
    private TokenExchangeSnapshot _exchangeSnapshotCache;
    private GachaEconomyService _exchangeCacheEconomy;
    private StaffAccountSaveData _exchangeCacheAccount;
    private bool _exchangeCacheBusy, _exchangeCacheClockReady;
    private int _exchangeCacheRemaining;
    private string _exchangeCacheDay, _exchangeCacheMessage;
    private string _collectionLayoutError;
    private readonly List<HiddenCollectionLayer> _hiddenCollectionLayers = new List<HiddenCollectionLayer>();
    private sealed class HiddenCollectionLayer
    {
        public CanvasGroup Group;
        public bool Interactable, BlocksRaycasts;
        public float Alpha;
    }

    public void BindCollectionEconomy(GachaEconomyService economy)
    {
        if (ReferenceEquals(Economy, economy) && _collectionHud != null &&
            _collectionExchange != null && _collectionExchange.IsConfigured && _collectionLayoutError == null &&
            ValidateCollectionReferences(out _)) return;
        HideCollectionUI();
        if (IsCollectionChild(_collectionExchange)) _collectionExchange.ResetSession();
        _pendingExchangeReply = null;
        _pendingExchangeTransaction = null;
        _hasReportedCollectionFailure = false;
        _reportedCollectionFailureId = null;
        _exchangeSnapshotCache = null;
        _revealedCollectionResults.Clear();
        Economy = economy;
        foreach (var machine in _gachaMachines ?? Array.Empty<GachaMachineParent>())
        {
            if (machine is UIStaffGacha staff) staff.BindCollectionEconomy(this, economy);
            if (machine is UIItemGacha item) item.BindCollectionEconomy(this, economy);
        }
        if (_collectionTheme == null) _collectionTheme = GachaCollectionUiTheme.Load();
        if (_collectionTheme == null || _collectionTheme.PandaToken == null)
        { RejectCollectionLayout("GachaCollectionUiTheme 또는 PandaToken 참조가 없습니다."); return; }
        if (_collectionHud == null)
        {
            _collectionHud = GachaCollectionMachineHud.Attach((RectTransform)transform, _collectionTheme,
                OpenCollectionExchange, DrawCollectionTicket, null, false);
            var frame = _gachaItemList != null ? _gachaItemList.transform.Find("Background Image") as RectTransform : null;
            if (frame != null)
            {
                frame.anchoredPosition += new Vector2(0, 90);
                var title = _uiComponents != null ? _uiComponents.transform.Find("List Title Image") as RectTransform : null;
                if (title != null) title.anchoredPosition += new Vector2(-16, 90);
                var exit = _uiComponents != null ? _uiComponents.transform.Find("Exit Button") as RectTransform : null;
                _collectionHud.BindCatalogLayout(frame, title, _gachaItemList, exit);
            }
        }
        if (!ValidateCollectionReferences(out string layoutError)) RejectCollectionLayout(layoutError);
        else if (_collectionExchange.Bind(null, ReadExchangeSnapshot,
            PurchaseExchangeProduct, RefreshExchangeDisplay, OnCollectionExchangeClosed))
        {
            _collectionLayoutError = null;
            CollectionError = null;
            _collectionHud.BindExchangeButton(_collectionExchangeButton, OpenCollectionExchange);
            _collectionHud.SetExchangeAvailable(true);
        }
        else RejectCollectionLayout("UIGacha._collectionExchange 초기화에 실패했습니다. 교환소의 참조 진단을 확인하세요.");
        _nextCollectionRefresh = 0;
    }

    private bool IsCollectionChild(Component component) => component != null &&
        component.transform != transform && component.transform.IsChildOf(transform);

    private bool ValidateCollectionReferences(out string error)
    {
        if (!IsCollectionChild(_collectionExchange))
        { error = "UIGacha._collectionExchange에 이 Gacha UI 아래의 TokenExchangeView를 연결해야 합니다."; return false; }
        if (!IsCollectionChild(_collectionExchangeButton) ||
            _collectionExchangeButton.transform.IsChildOf(_collectionExchange.transform))
        { error = "UIGacha._collectionExchangeButton에 교환소 밖의 가챠 진입 버튼을 연결해야 합니다."; return false; }
        return _collectionExchange.ValidateLayout(out error);
    }

    private void RejectCollectionLayout(string error)
    {
        CollectionError = "토큰 교환소 UI 연결 오류: " + error;
        // A missing serialized field must not leave an accidentally active authored overlay
        // blocking the machine. Only this view's descendants are touched, never a foreign view.
        foreach (var exchange in GetComponentsInChildren<TokenExchangeView>(true))
            if (IsCollectionChild(exchange)) exchange.SetVisible(false);
        RestoreCollectionLayers();
        _collectionHud?.SetExchangeAvailable(false);
        if (_collectionLayoutError != error) Debug.LogError(CollectionError, this);
        _collectionLayoutError = error;
    }

    private void OnCollectionExchangeClosed()
    {
        RestoreCollectionLayers();
        _collectionHud?.SnapToCommitted();
    }

    private GachaMachineKind CollectionMachine => _currentGachaMachine is UIStaffGacha
        ? GachaMachineKind.Staff : GachaMachineKind.Item;

    private void UpdateCollectionUI()
    {
        var observed = Economy?.LastTransaction;
        if (observed != null && (observed.Status == GachaTransactionStatus.Rejected ||
            observed.Status == GachaTransactionStatus.Indeterminate))
        {
            // Compare the transaction ID and status directly, without building an "Id:Status" string every frame.
            if (!_hasReportedCollectionFailure || _reportedCollectionFailureStatus != observed.Status ||
                !string.Equals(_reportedCollectionFailureId, observed.Id, StringComparison.Ordinal))
            {
                _hasReportedCollectionFailure = true;
                _reportedCollectionFailureId = observed.Id;
                _reportedCollectionFailureStatus = observed.Status;
                ReportCollectionError(observed.Error ?? "거래 결과를 확인하고 있습니다.");
            }
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
        if (_collectionHud.gameObject.activeSelf != visible) _collectionHud.gameObject.SetActive(visible);
        if (!visible)
        {
            // Clean up once on the visible-to-hidden transition, or again only if something was left open.
            if (_collectionHudShown || _hiddenCollectionLayers.Count != 0 || IsChoosingPayment ||
                (IsCollectionChild(_collectionExchange) && _collectionExchange.IsOpen))
                HideCollectionUI();
            _collectionHudShown = false;
            return;
        }
        if (!_collectionHudShown) { _collectionHudShown = true; _nextCollectionRefresh = 0; }
        _collectionHud.SetResultPresentation(_isStartGacha);
        var machine = CollectionMachine;
        if (machine != _collectionHudMachine || (_collectionWasDrawing && !_isStartGacha))
        {
            _collectionHud.SnapToCommitted();
            if (machine != _collectionHudMachine) _nextCollectionRefresh = 0;
        }
        _collectionHudMachine = machine;
        _collectionWasDrawing = _isStartGacha;
        if (Time.unscaledTime < _nextCollectionRefresh) return;
        _nextCollectionRefresh = Time.unscaledTime + .1f;
        // HUD.Refresh only uses the counter and busy state, so skip the full snapshot copy and the CanDraw check.
        var state = Economy.CurrentAccount?.GachaEconomy;
        _collectionHud.Refresh(state?.Counter(machine) ?? 0, state?.Tickets(machine) ?? 0,
            Economy.IsBusy || _isStartGacha || (IsCollectionChild(_collectionExchange) && _collectionExchange.IsOpen) || IsChoosingPayment);
    }

    public void OpenCollectionExchange()
    {
        if (Economy == null || Economy.IsBusy || _isStartGacha || _questStaffEntry ||
            VisibleState != VisibleState.Appeared || IsChoosingPayment) return;
        if (!ValidateCollectionReferences(out string layoutError))
        { RejectCollectionLayout(layoutError); return; }
        if (!_collectionExchange.IsConfigured)
        { RejectCollectionLayout("UIGacha._collectionExchange에 거래 콜백이 연결되지 않았습니다."); return; }
        bool wasOpen = _collectionExchange.IsOpen;
        if (!_collectionExchange.SetVisible(true))
        { RejectCollectionLayout("교환소를 열지 못했습니다. TokenExchangeView의 참조 진단을 확인하세요."); return; }
        _collectionLayoutError = null;
        _collectionHud?.SetExchangeAvailable(true);
        CollectionError = null;
        HideCollectionLayers();
        var service = Economy;
        // A fresh SetVisible(true) has already drawn the latest state. Refresh again only after a catalog request
        // or if the view was already open, so a ready catalog does not rebuild twice on the same frame.
        if (!service.ExchangeCatalogReady)
        {
            if (!service.TryEnsureExchangeCatalog(transaction =>
            {
                if (this != null && ReferenceEquals(Economy, service)) _collectionExchange?.Refresh();
            }, out string error)) ReportCollectionError(error);
            _collectionExchange.Refresh();
        }
        else if (wasOpen) _collectionExchange.Refresh();
    }

    public void DrawCollectionTicket()
    {
        OpenCollectionPayment(_currentGachaMachine, false);
    }

    private TokenExchangeSnapshot ReadExchangeSnapshot()
    {
        var economy = Economy;
        if (economy == null) { _exchangeSnapshotCache = null; return new TokenExchangeSnapshot { Busy = true }; }
        // Every input to the display is in this key: the immutable account (tokens, display, sold-out state, staff
        // ownership), busy/save state, and the date-based refresh status. If the key matches, reuse the projection.
        bool busy = economy.IsBusy;
        var account = economy.CurrentAccount;
        var refresh = economy.GetRefreshStatus();
        if (_exchangeSnapshotCache != null && ReferenceEquals(_exchangeCacheEconomy, economy) &&
            ReferenceEquals(_exchangeCacheAccount, account) && _exchangeCacheBusy == busy &&
            _exchangeCacheRemaining == refresh.Remaining && _exchangeCacheClockReady == refresh.ClockReady &&
            string.Equals(_exchangeCacheDay, refresh.DayKey, StringComparison.Ordinal) &&
            string.Equals(_exchangeCacheMessage, refresh.Message, StringComparison.Ordinal))
            return _exchangeSnapshotCache;

        using (ExchangeSnapshotMarker.Auto())
        {
            var result = new TokenExchangeSnapshot { Busy = busy };
            result.PandaTokens = account?.PandaTokens ?? 0;
            var display = economy.GetExchangeDisplay(GachaExchangeCategory.Mixed);
            result.DisplayVersion = display?.Version ?? 0;
            result.RemainingRefreshes = refresh.Remaining; result.RefreshLimit = refresh.Limit;
            result.RefreshMessage = refresh.Message;
            result.CanRefresh = economy.CanRefreshExchange(GachaExchangeCategory.Mixed, result.DisplayVersion, out _);
            var displayed = economy.GetDisplayedProducts(GachaExchangeCategory.Mixed);
            var products = new List<TokenExchangeProductView>(displayed.Count);
            for (int p = 0; p < displayed.Count; p++)
            {
                var product = displayed[p];
                int version = result.DisplayVersion;
                bool available = economy.CanExchange(product.Id, 1, version, out string error);
                bool soldOut = false;
                for (int s = 0; s < display.Slots.Count; s++)
                {
                    var slot = display.Slots[s];
                    if (slot.ProductId == product.Id && slot.PurchaseCount > 0) { soldOut = true; break; }
                }
                products.Add(new TokenExchangeProductView
                {
                    Id = product.Id, Name = product.Name, Description = product.Description,
                    Sprite = product.Sprite != null ? product.Sprite :
                        product.Category == GachaExchangeCategory.Tickets ? _collectionExchange.Theme.Ticket : null,
                    Price = product.Price, Quantity = product.Quantity, DisplayVersion = version,
                    Rank = product.Data != null ? product.Data.Rank : Rank.Normal1,
                    Category = (TokenExchangeTab)product.Category, CanPurchase = available,
                    SoldOut = soldOut,
                    Status = available ? "" : error
                });
            }
            result.Products = products;

            _exchangeSnapshotCache = result;
            _exchangeCacheEconomy = economy; _exchangeCacheAccount = account; _exchangeCacheBusy = busy;
            _exchangeCacheRemaining = refresh.Remaining; _exchangeCacheClockReady = refresh.ClockReady;
            _exchangeCacheDay = refresh.DayKey; _exchangeCacheMessage = refresh.Message;
            return result;
        }
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
        if (_hiddenCollectionLayers.Count != 0 || !IsCollectionChild(_collectionExchange)) return;
        // Preserve every authored container on the route to the exchange. Hiding only
        // the root's direct children would also hide an exchange nested in a layout wrapper.
        for (Transform branch = _collectionExchange.transform; branch != transform; branch = branch.parent)
        {
            foreach (Transform sibling in branch.parent)
            {
                if (sibling == branch) continue;
                var group = sibling.GetComponent<CanvasGroup>();
                if (group == null) group = sibling.gameObject.AddComponent<CanvasGroup>();
                _hiddenCollectionLayers.Add(new HiddenCollectionLayer { Group = group,
                    Alpha = group.alpha, Interactable = group.interactable, BlocksRaycasts = group.blocksRaycasts });
                group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
            }
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
        _collectionPayment?.Close();
        if (IsCollectionChild(_collectionExchange)) _collectionExchange.SetVisible(false);
        RestoreCollectionLayers();
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
