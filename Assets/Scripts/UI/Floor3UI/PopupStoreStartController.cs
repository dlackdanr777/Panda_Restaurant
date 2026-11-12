using UnityEngine;

/// <summary>View adapter only. The scene/review owner must inject a host; Player has no fake fallback.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreStartController : MonoBehaviour
{
    [SerializeField] private PopupStoreStartPanel _panel;
    [Header("Approved entry presentation — independent of account permission")]
    [SerializeField] private GameObject _unavailableTutorialPrefab;
    [SerializeField] private string _unavailableTicketText = "이용권 조회 불가";
    private PopupStorePhasePreviewView _unavailableTutorial;
    private UnityEngine.UI.Button _entryClose;
    private bool _entryCloseWasActive, _ownsEntryClose;
    public PopupStoreAdmissionHost Host { get; private set; }
    public bool IsApproved => Host != null && Host.IsApprovedFor(this);
    public string LastError => Host?.Transaction?.LastError ?? Host?.LastError;
    public string LocalSessionId => GetComponent<PopupStoreGradeSession>()?.SessionId;
    public bool IsRetired => GetComponent<PopupStoreSessionLifetime>()?.IsRetired == true;
    private PopupStoreBusinessClock Clock => GetComponent<PopupStoreBusinessClock>();
    private PopupStoreStartPanel Panel => _panel != null ? _panel : (_panel = GetComponent<PopupStoreStartPanel>());
#if UNITY_EDITOR
    // Bound by the existing Editor review owner before the popup's first OnEnable/Show.
    // No fake admission or runtime fallback is introduced into the Player build.
    public static System.Func<bool> EditorPhasePreviewEntryPending { get; set; }
    private bool WaitingForEditorPhaseView
    {
        get
        {
            bool pending;
            try { pending = EditorPhasePreviewEntryPending?.Invoke() == true; }
            catch { return true; } // A broken review owner must not expose ordinary admission.
            if (!pending) return false;
            var preview = GetComponent<PopupStorePhasePreview>();
            return preview == null || !preview.IsInitialized || preview.TutorialView == null;
        }
    }
#else
    private bool WaitingForEditorPhaseView => false;
#endif
    internal bool CanRequestApproval => isActiveAndEnabled && !IsRetired && !string.IsNullOrEmpty(LocalSessionId)
        && (Host?.IsEditorReview == true || (GetComponent<PopupStorePhasePreview>()?.IsAccountMode == true
            && GetComponent<PopupStorePhasePreview>().TutorialView != null))
        && !WaitingForEditorPhaseView
        && (GetComponent<PopupStorePhasePreview>()?.CanRequestTutorialAdmission ?? true)
        && Clock != null && Clock.CanPrepareForAdmission
        && (GetComponent<UIPopupStore>()?.Navigation == null || GetComponent<UIPopupStore>().CanRequestExit);

    public bool BindHost(PopupStoreAdmissionHost host)
    {
        if (host == null || (Host != null && !ReferenceEquals(Host, host)) || !host.Attach(this)) return false;
        if (Host != null) Host.Changed -= RefreshPresentation;
        Host = host;
        Host.Changed += RefreshPresentation;
        ReleaseUnavailablePresentation();
        BindPanel();
        Host.RefreshAccess();
        RefreshPresentation();
        return true;
    }

    public bool RequestStart()
    {
        if (Host == null || !isActiveAndEnabled || Clock == null
            || Clock.State != PopupStoreBusinessState.NotStarted) return false;
        bool requested = Host.RequestStart(this);
        RefreshPresentation();
        return requested;
    }
    public bool RefreshAccess() => Host != null && Host.RefreshAccess();

    internal bool TryStartApprovedClock()
    {
        if (!IsApproved || Clock == null) return false;
        // Approval may already have consumed a use while focus delays local startup.
        // Freeze its policy now; resuming that same approval must not adopt later asset edits.
        GetComponent<PopupStoreRewardSession>()?.PreparePolicyForApprovedStart();
        if (!isActiveAndEnabled) return false;
        if (Clock.State == PopupStoreBusinessState.NotStarted) return Clock.TryStartSession();
        return Clock.State == PopupStoreBusinessState.Running || Clock.State == PopupStoreBusinessState.Paused
            || Clock.State == PopupStoreBusinessState.Ended;
    }

    public void OnViewShown()
    {
        BindPanel();
        if (Host != null)
        {
            if (Clock != null && Clock.State == PopupStoreBusinessState.NotStarted)
            { Host.RetryLocalStart(this); if (Host.Transaction == null) Host.RefreshAccess(); }
        }
        RefreshPresentation();
    }

    private void BindPanel()
    {
        if (Panel == null) return;
        Panel.StartRequested -= StartRequested;
        Panel.CloseRequested -= CloseRequested;
        Panel.StartRequested += StartRequested;
        Panel.CloseRequested += CloseRequested;
    }
    private void StartRequested() => RequestStart();
    private void CloseRequested() => GetComponent<UIPopupStore>()?.TryRequestExit();

    private void RefreshPresentation()
    {
        if (Panel == null || Clock == null) return;
        if (GetComponent<PopupStoreAccountRecoveryPresentation>()?.IsBound == true)
        { ReleaseUnavailablePresentation(); Panel.Render(false, false, false, 0, 1, string.Empty, false); return; }
        bool show = Clock.State == PopupStoreBusinessState.NotStarted;
        var access = Host?.Access;
        var tx = Host?.Transaction;
        bool canStart = Host != null && !Host.IsQuerying;
        bool confirmedTicketExhaustion = false;
        string status;
        if (Host == null) { status = "현재 영업권 서비스를 이용할 수 없습니다."; canStart = false; }
        else if (Host.IsQuerying) status = "오늘의 영업권을 조회하고 있습니다.";
        else if (tx != null && tx.State == PopupStoreBusinessStartState.Submitting)
        { status = "시작 승인을 확인하고 있습니다."; canStart = false; }
        else if (tx != null && (tx.State == PopupStoreBusinessStartState.Indeterminate
            || tx.State == PopupStoreBusinessStartState.ReconciliationRequired))
            status = "시작 결과가 미확정입니다. 같은 요청을 다시 확인합니다.";
        else if (tx != null && tx.State == PopupStoreBusinessStartState.Started && tx.LocalCompletionPending)
            status = "시작 승인이 보존돼 있습니다. 같은 영업을 재개합니다.";
        else if (tx != null && (tx.State == PopupStoreBusinessStartState.Conflict || tx.State == PopupStoreBusinessStartState.Invalidated))
        { status = tx.LastError ?? "기존 시작 요청의 복구가 필요합니다."; canStart = false; }
        else if (access == null) status = Host.LastError ?? "영업권을 다시 조회해 주세요.";
        else if (access.Remaining <= 0)
        {
            status = "오늘의 영업권을 모두 사용했습니다."; canStart = false;
            confirmedTicketExhaustion = string.IsNullOrEmpty(Host.LastError);
        }
        else if (!string.IsNullOrEmpty(Host.LastError)) status = Host.LastError;
        else if (tx != null && tx.State == PopupStoreBusinessStartState.RejectedBeforeSend)
            status = "시작이 승인되지 않았습니다. 영업권 정보를 다시 확인해 주세요.";
        else status = "영업을 시작할 준비가 됐습니다.";
        var preview = GetComponent<PopupStorePhasePreview>();
        if (WaitingForEditorPhaseView)
        {
            ReleaseUnavailablePresentation();
            // The preview bridge binds on the Editor update after Stage1 first shows this view.
            // Hide the ordinary explanation immediately; the old start control cannot consume a use.
            Panel.Render(false, false, false, access?.Remaining ?? -1, access?.DailyLimit ?? -1,
                string.Empty, true);
            return;
        }
        if (preview != null && preview.TutorialView != null)
        {
            ReleaseUnavailablePresentation();
            // Release the ordinary modal's input ownership before showing the authored preview tutorial.
            Panel.Render(false, false, false, access?.Remaining ?? -1, access?.DailyLimit ?? -1,
                string.Empty, true);
            bool pending = tx != null && (tx.State == PopupStoreBusinessStartState.Submitting
                || tx.State == PopupStoreBusinessStartState.ReconciliationRequired);
            preview.RenderStart(show, canStart, access?.Remaining ?? -1, access?.DailyLimit ?? -1,
                canStart ? null : status, pending, confirmedTicketExhaustion);
            return;
        }
        // A missing transaction authority must not select an obsolete screen or a fake ticket.
        // Only an explicitly injected isolated 90-second review may use its legacy start panel.
        if (Host?.IsEditorReview != true)
        {
            Panel.Render(false, false, false, access?.Remaining ?? -1, access?.DailyLimit ?? -1,
                string.Empty, false);
            if (show && GetComponent<UIPopupStore>()?.VisibleState == VisibleState.Appeared)
                ShowUnavailablePresentation();
            else HideUnavailablePresentation();
            return;
        }
        ReleaseUnavailablePresentation();
        Panel.Render(show, canStart, true, access?.Remaining ?? -1, access?.DailyLimit ?? -1,
            status, Host?.IsEditorReview == true);
    }

    private void ShowUnavailablePresentation()
    {
        var popup = GetComponent<UIPopupStore>();
        if (popup == null || _unavailableTutorialPrefab == null) return;
        if (_unavailableTutorial == null)
        {
            var authored = _unavailableTutorialPrefab.GetComponent<PopupStorePhasePreviewView>();
            if (authored == null || !authored.ValidateConfiguration(out _)) return;
            var instance = Instantiate(_unavailableTutorialPrefab, transform, false);
            _unavailableTutorial = instance.GetComponent<PopupStorePhasePreviewView>();
            try { _unavailableTutorial.Bind(RejectUnavailableStart, CloseUnavailablePresentation); }
            catch (System.Exception)
            {
                ReleaseUnavailablePresentation();
                return; // Original X remains available; no approval or gameplay is created.
            }
        }
        _unavailableTutorial.gameObject.SetActive(true);
        if (!_ownsEntryClose && popup.PhaseCloseButton != null)
        {
            _entryClose = popup.PhaseCloseButton;
            _entryCloseWasActive = _entryClose.gameObject.activeSelf;
            _ownsEntryClose = true;
            _entryClose.gameObject.SetActive(false);
        }
        _unavailableTutorial.RenderTutorial(true, false, _unavailableTicketText);
        _unavailableTutorial.transform.SetAsLastSibling();
    }

    private bool RejectUnavailableStart() => false;
    private bool CloseUnavailablePresentation() => GetComponent<UIPopupStore>()?.TryRequestExit() == true;

    private void HideUnavailablePresentation()
    {
        if (_unavailableTutorial != null)
        {
            _unavailableTutorial.HideAll();
            _unavailableTutorial.gameObject.SetActive(false);
        }
        if (_ownsEntryClose && _entryClose != null) _entryClose.gameObject.SetActive(_entryCloseWasActive);
        _entryClose = null; _ownsEntryClose = false;
    }

    internal void ReleaseUnavailablePresentation()
    {
        HideUnavailablePresentation();
        var owned = _unavailableTutorial;
        _unavailableTutorial = null;
        if (owned == null) return;
        if (Application.isPlaying) Destroy(owned.gameObject);
        else DestroyImmediate(owned.gameObject);
    }

    private void OnEnable() { BindPanel(); RefreshPresentation(); }
    private void OnDisable() => HideUnavailablePresentation();
    private void Update()
    {
        Host?.ObserveElapsed(Time.unscaledDeltaTime);
        // A focus-restored approved start resumes the same approval; no request is created here.
        if (Host?.Transaction?.LocalCompletionPending == true && Application.isFocused) Host.RetryLocalStart(this);
    }
    private void OnDestroy()
    {
        if (Host != null) Host.Changed -= RefreshPresentation;
        if (Panel != null) { Panel.StartRequested -= StartRequested; Panel.CloseRequested -= CloseRequested; }
    }
}
