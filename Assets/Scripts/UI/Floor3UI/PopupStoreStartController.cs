using UnityEngine;

/// <summary>View adapter only. The scene/review owner must inject a host; Player has no fake fallback.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreStartController : MonoBehaviour
{
    [SerializeField] private PopupStoreStartPanel _panel;
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
        { Panel.Render(false, false, false, 0, 1, string.Empty, false); return; }
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
            // The preview bridge binds on the Editor update after Stage1 first shows this view.
            // Hide the ordinary explanation immediately; the old start control cannot consume a use.
            Panel.Render(false, false, false, access?.Remaining ?? -1, access?.DailyLimit ?? -1,
                string.Empty, true);
            return;
        }
        if (preview != null && preview.TutorialView != null)
        {
            // Release the ordinary modal's input ownership before showing the authored preview tutorial.
            Panel.Render(false, false, false, access?.Remaining ?? -1, access?.DailyLimit ?? -1,
                string.Empty, true);
            bool pending = tx != null && (tx.State == PopupStoreBusinessStartState.Submitting
                || tx.State == PopupStoreBusinessStartState.ReconciliationRequired);
            preview.RenderStart(show, canStart, access?.Remaining ?? -1, access?.DailyLimit ?? -1,
                canStart ? null : status, pending, confirmedTicketExhaustion);
            return;
        }
        Panel.Render(show, canStart, true, access?.Remaining ?? -1, access?.DailyLimit ?? -1,
            status, Host?.IsEditorReview == true);
    }

    private void OnEnable() { BindPanel(); RefreshPresentation(); }
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
