using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Opt-in shared phase presentation. Account persistence is injected; Editor review never receives it.</summary>
[DisallowMultipleComponent]
public sealed class PopupStorePhasePreview : MonoBehaviour, IPopupStorePhasePreviewClock, IPopupStorePhasePreviewSupply
{
    private PopupStoreBusinessClock _clock;
    private PopupStoreStartController _start;
    private PopupStorePacking _packing;
    private PopupStoreScore _score;
    private PopupStorePackingPresentation _presentation;
    private PopupStoreCustomerEntrance _entrance;
    private PopupStoreOrderDisplay _orders;
    private PopupStoreSalesCounter _counter;
    private PopupStoreMaterialList[] _lists;
    private PopupStorePhasePreviewOrderSource _source;
    private string _approvalId, _loggedState;
    private bool _ready, _preparing, _cleaning, _confirming;
    private enum Gate { None, InitialStage, Three, Two, One, InitialStart, NextStage, NextStart, GameOver }
    private Gate _gate;
    private double _gateRemaining, _gateDuration;
    private bool _startArmed, _contextAvailable, _resultReady, _tutorialStartRequested;
    internal bool CanRequestTutorialAdmission => (_view == null || _tutorialStartRequested)
        && (!IsAccountMode || AccountAuthorityReady);
    private PopupStorePhasePreviewView _view;
    private PopupStorePhasePreviewPolish _polish;
    private bool _rewardClosing, _receiving;
    private IPopupStoreAccountPhaseBridge _accountBridge;
    private PopupStoreRewardPolicySnapshot _accountRewardPolicy;
    private PopupStorePhaseResult _preparedResult;
    private bool _prepareAttempted, _preparePending, _accountClaimPending, _accountClaimConfirmed;
    private long _accountRequestSerial;
    private int _accountConfirmedDiamonds;
    private double _prepareElapsed, _claimElapsed;
    public bool IsAccountMode => _accountBridge != null;
    public bool AccountAuthorityReady => _accountBridge != null && _accountBridge.IsReady
        && ReferenceEquals(_accountBridge.AdmissionHost, _start?.Host) && _accountBridge.AdmissionHost?.IsEditorReview == false;
    public bool AccountClaimConfirmed => _accountClaimConfirmed && AccountAuthorityReady;
    public int AccountConfirmedDiamonds => _accountConfirmedDiamonds;
    public string AccountStatus { get; private set; }
    public PopupStorePhaseRewardPresentation RewardPresentation { get; private set; }
    public PopupStorePhaseRewardRoll RewardRoll { get; private set; }
    private UnityEngine.UI.Button _originalClose;
    private bool _closeWasActive;
    private string _lastCue;
    public double StageNoticeSeconds => _view != null ? _view.StageNoticeSeconds : 1.5d;
    public double GameStartSeconds => _view != null ? _view.GameStartSeconds : .7d;
    public double GameOverSeconds => _view != null ? _view.GameOverSeconds : 1.1d;
    public bool IsResultPresentationReady => Session != null && Session.IsTerminal && _resultReady;
    public PopupStorePhasePreviewView TutorialView => _view;
    public string PresentationCue => _gate == Gate.InitialStage || _gate == Gate.NextStage ? "Stage"
        : _gate == Gate.Three ? "3" : _gate == Gate.Two ? "2" : _gate == Gate.One ? "1"
        : _gate == Gate.InitialStart || _gate == Gate.NextStart ? "GameStart"
        : _gate == Gate.GameOver ? "GameOver"
        : Session?.State == PopupStorePhaseState.ClearNotice ? "Success"
        : Session?.State == PopupStorePhaseState.Countdown ? Session.CountdownValue.ToString() : string.Empty;
    public float PresentationProgress => _gate != Gate.None
        ? (float)(1d - _gateRemaining / _gateDuration)
        : Session?.State == PopupStorePhaseState.ClearNotice
            ? (float)(1d - Session.TransitionRemainingSeconds / Math.Max(.001d, Session.CurrentPhase.StageClearNoticeSeconds))
        : Session?.State == PopupStorePhaseState.Countdown
            ? (float)(1d - Session.TransitionRemainingSeconds / Math.Max(.001d, Session.CurrentPhase.CountdownStepSeconds)) : 0f;
    private readonly List<PopupStorePackingReceipt> _accepted = new List<PopupStorePackingReceipt>();
    internal Func<int, int> OrderRandomOverride; // Automated fixtures only; the menu never injects it.
    public PopupStorePhaseSession Session { get; private set; }
    public UIPopupStore Popup => GetComponent<UIPopupStore>();
    public string LastError { get; private set; }
    public bool IsInitialized => _ready;
    public int CleanupCount { get; private set; }
    public int ConfirmationCount { get; private set; }
    public int AcceptedReceiptCount => _accepted.Count;
    public bool CanPlay => _ready && (!IsAccountMode || AccountAuthorityReady) && _gate == Gate.None && LastError == null && Session != null && Session.CanAcceptPacking
        && _entrance != null && !_entrance.EditorPhaseSupplyFaulted;
    public bool CanEnter => CanPlay && RemainingSeconds > 10d;
    public bool CanSupply => CanEnter;
    public bool CanCleanup => _ready && Session != null && Session.IsAwaitingCleanup && !Session.IsPaused;
    public float EntryInterval => Session == null ? 0f : (float)Session.CurrentPhase.EntryIntervalSeconds;
    public double OnePlushPatienceSeconds => Session?.CurrentPhase.CustomerPatienceSeconds ?? 0d;
    public double RemainingSeconds => _startArmed && Session?.State == PopupStorePhaseState.Ready
        ? Session.CurrentPhase.TimeLimitSeconds : Session?.RemainingSeconds ?? 0d;
    public PopupStoreGradeTier SecuredGrade => Session == null || Session.State == PopupStorePhaseState.Abandoned
        || Session.State == PopupStorePhaseState.Faulted ? PopupStoreGradeTier.None : Session.HighestClearedGrade;
    public string HudText => Session == null ? "단계 프리뷰 준비"
        : GoalText + " | 확보 "
            + (SecuredGrade == PopupStoreGradeTier.None ? "없음" : SecuredGrade + "상자");
    public string GoalText => Session == null ? "단계 프리뷰 준비"
        : "도전 " + Session.CurrentPhase.Grade + " · 목표 "
            + (Session.Profile.GoalMetric == PopupStorePhaseGoalMetric.CumulativeScore
                ? Session.CurrentPhase.TargetScore + "점"
                : Session.PhaseSuccessfulOrders + "/" + Session.CurrentPhase.TargetSuccessfulOrders + "건");
    public float GoalProgress => Session == null ? 0f
        : Session.Profile.GoalMetric == PopupStorePhaseGoalMetric.CumulativeScore
            ? (float)Session.TotalScore / Session.CurrentPhase.TargetScore
            : (float)Session.PhaseSuccessfulOrders / Session.CurrentPhase.TargetSuccessfulOrders;
    public string TransitionText => LastError != null ? "진행 확인 필요"
        : IsAccountMode && (Session == null || Session.State == PopupStorePhaseState.Ready) ? "6단계 도전"
        : IsAccountMode && Session.IsTerminal ? "게임 종료"
        : Session == null || Session.State == PopupStorePhaseState.Ready ? "6단계 검수 · 미지급"
        : Session.State == PopupStorePhaseState.Clearing ? "전달·퇴장 정리 중"
        : Session.State == PopupStorePhaseState.ClearNotice ? Session.CurrentPhase.Grade + " 통과!"
        : Session.State == PopupStorePhaseState.Countdown ? Session.CountdownValue.ToString()
        : Session.IsTerminal ? "프리뷰 종료 · 실제 미지급" : "6단계 검수 · 미지급";

    public bool Initialize(PopupStorePhaseProfileSnapshot profile, out string error)
        => Initialize(profile, PopupStoreScorePolicySnapshot.Ordinary, out error);

    public bool InitializeAccount(PopupStorePhaseProfileSnapshot profile, PopupStoreScorePolicySnapshot scoring,
        IPopupStoreAccountPhaseBridge bridge, out string error)
    {
        error = null;
        if (_ready || Session != null || _accountBridge != null || bridge == null || !bridge.IsReady
            || bridge.AdmissionHost == null || bridge.AdmissionHost.IsEditorReview
            || !ReferenceEquals(GetComponent<PopupStoreStartController>()?.Host, bridge.AdmissionHost))
        { error = "Account phase requires an explicit current GameData admission owner."; return false; }
        _accountBridge = bridge;
        return Initialize(profile, scoring, out error);
    }

    public bool Initialize(PopupStorePhaseProfileSnapshot profile, PopupStoreScorePolicySnapshot scoring, out string error)
    {
        error = null;
        if (_ready || Session != null) { error = "A phase preview cannot be initialized twice."; return false; }
        _clock = GetComponent<PopupStoreBusinessClock>(); _start = GetComponent<PopupStoreStartController>();
        _packing = GetComponent<PopupStorePacking>(); _presentation = GetComponent<PopupStorePackingPresentation>();
        _score = GetComponent<PopupStoreScore>();
        _entrance = GetComponent<PopupStoreCustomerEntrance>(); _orders = GetComponent<PopupStoreOrderDisplay>();
        _counter = GetComponentInChildren<PopupStoreSalesCounter>(true);
        _lists = GetComponentsInChildren<PopupStoreMaterialList>(true);
        var catalog = Resources.Load<PopupStoreMaterialCatalog>("UI/PopupStore/PopupStoreMaterials");
        if (Popup == null || _clock == null || _clock.State != PopupStoreBusinessState.NotStarted
            || _start == null || _start.Host == null || (!IsAccountMode && !_start.Host.IsEditorReview)
            || (IsAccountMode && !AccountAuthorityReady) || _start.IsApproved
            || scoring == null || _score == null || _packing == null || _packing.CommittedReceipts.Count != 0 || _presentation == null
            || _entrance == null || _orders == null || !_orders.IsReady || _counter == null || _counter.Count != 0
            || _lists.Length != 3 || catalog == null || !catalog.TryValidate(out error))
        { error = error ?? "Preview requires a fresh popup with an explicitly isolated Editor admission host."; return false; }
        if (!PopupStorePhaseSession.TryCreate(_start.LocalSessionId, profile,
            PopupStorePhaseCompletionPolicy.Deadline, out var model, out error)) return false;
        var customers = new string[3];
        for (int i = 0; i < 3; i++)
        {
            if (!_orders.TryGetEntryBinding(i, out var order, out _))
            { error = "Three original customer bindings are required."; return false; }
            customers[i] = order.CustomerId;
        }
        Session = model;
        if (!_score.TryBindEditorPhasePolicy(this, scoring, out error)) { FailClosed(error); return false; }
        var random = new System.Random();
        _source = new PopupStorePhasePreviewOrderSource(catalog, customers, OrderRandomOverride ?? new Func<int, int>(random.Next));
        if (!_clock.TryBindPhasePreview(this)) { error = "Clock preview ownership was rejected."; FailClosed(error); return false; }
        _ready = true;
        if (!_counter.BindEditorPhaseInput(this, () => CanPlay, MaterialAllowed, () => CanCleanup)
            || !ApplyFilter(out error))
        { error = error ?? "Preview input ownership was rejected."; FailClosed(error); return false; }
        _packing.SucceededCommitted += OnCommitted;
        GetComponent<PopupStoreGradeSession>()?.RefreshHud();
        GetComponentInChildren<PopupStoreCurrentGradeHud>(true)?.RefreshDisplay();
        Observe();
        return true;
    }

    public bool TryStart()
    {
        if (!_ready || LastError != null || _start == null || !_start.IsApproved || (IsAccountMode && !AccountAuthorityReady)
            || _startArmed || string.IsNullOrEmpty(_start.Host.BusinessSessionId)
            || Session.State != PopupStorePhaseState.Ready || Session.IsPaused) return false;
        _approvalId = _start.Host.BusinessSessionId;
        _startArmed = true;
        SetGate(Gate.InitialStage, StageNoticeSeconds);
        Observe();
        return true;
    }

    private bool BeginSuppliedPhase(bool first)
    {
        if (first && !Session.TryStart()) { FailClosed("Initial phase start was rejected."); return false; }
        _preparing = true;
        try
        {
            if (!ApplyFilter(out var error)
                || !(first ? _entrance.BindEditorPhaseSupply(this, out error)
                    : _entrance.TryBeginEditorPhase(this, out error))) { FailClosed(error); return false; }
            Observe();
            return true;
        }
        finally { _preparing = false; }
    }

    public void Advance(double elapsed)
    {
        if (!_ready || LastError != null || Session == null || !_contextAvailable
            || double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0d) return;
        if (_gate != Gate.None)
        {
            _gateRemaining = Math.Max(0d, _gateRemaining - elapsed);
            if (_gateRemaining == 0d) CompleteGate();
            Observe();
            return; // Never carry residual time across a visible cue.
        }
        int generation = Session.PhaseGeneration;
        var before = Session.State;
        Session.TryAdvanceTime(elapsed); // Exactly once; never carry residual time across a visible state.
        if (generation != Session.PhaseGeneration) SetGate(Gate.NextStart, GameStartSeconds);
        else if (before == PopupStorePhaseState.ClearNotice && Session.State == PopupStorePhaseState.Countdown)
            SetGate(Gate.NextStage, StageNoticeSeconds);
        UpdateTerminalPresentation();
        _polish?.TickWarning(elapsed);
        Observe();
    }

    private void SetGate(Gate gate, double duration)
    { _gate = gate; _gateDuration = _gateRemaining = duration; }
    private void CompleteGate()
    {
        var completed = _gate;
        _gate = Gate.None;
        switch (completed)
        {
            case Gate.InitialStage: SetGate(Gate.Three, Session.CurrentPhase.CountdownStepSeconds); break;
            case Gate.Three: SetGate(Gate.Two, Session.CurrentPhase.CountdownStepSeconds); break;
            case Gate.Two: SetGate(Gate.One, Session.CurrentPhase.CountdownStepSeconds); break;
            case Gate.One: SetGate(Gate.InitialStart, GameStartSeconds); break;
            case Gate.InitialStart: BeginSuppliedPhase(true); break;
            case Gate.NextStart: BeginSuppliedPhase(false); break;
            case Gate.GameOver: _resultReady = true; break;
        }
    }
    private void UpdateTerminalPresentation()
    {
        if (Session == null || !Session.IsTerminal || _resultReady || _gate == Gate.GameOver) return;
        if (Session.State == PopupStorePhaseState.TimedOut) SetGate(Gate.GameOver, GameOverSeconds);
        else { _gate = Gate.None; _resultReady = true; }
    }

    public bool TryPenalty(double seconds)
    {
        if (!CanPlay || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) return false;
        bool accepted = Session.TryAdvanceTime(seconds); // A penalty is not elapsed patience time.
        Observe();
        return accepted;
    }

    public void SetContext(bool visible, bool paused, bool focused, bool exitPaused)
    {
        _contextAvailable = visible && !paused && focused && !exitPaused;
        if (Session == null) return;
        Session.TrySetPauseReason(PopupStorePhasePauseReason.ViewHidden, !visible);
        Session.TrySetPauseReason(PopupStorePhasePauseReason.ApplicationBackground, paused);
        Session.TrySetPauseReason(PopupStorePhasePauseReason.FocusLost, !focused);
        Session.TrySetPauseReason(PopupStorePhasePauseReason.ExitConfirmation, exitPaused);
    }

    public bool TryCreateOrder(int slot, out PopupStoreOrder order)
    {
        order = null;
        return (_preparing || CanSupply) && _source != null && _source.TryCreate(Session, slot, out order);
    }
    public bool OwnsOrder(PopupStoreOrder order) => _source != null && _source.Owns(Session, order);
    private bool MaterialAllowed(PopupStoreMaterialEntry entry)
        => entry != null && (entry.Kind != PopupStoreMaterialKind.Plush || Session.CurrentPhase.IsPlushAllowed(entry.Id));
    private bool ApplyFilter(out string error)
    {
        error = null;
        foreach (var list in _lists)
            if (list.Kind == PopupStoreMaterialKind.Plush
                && !list.TrySetEditorPlushFilter(this, Session.CurrentPhase.AllowedPlushIds, out error)) return false;
        return true;
    }

    internal bool AcceptCommitted(PopupStorePackingReceipt receipt)
    {
        if (!CanPlay || !_start.IsApproved || _start.LocalSessionId != Session.BusinessSessionId
            || _start.Host.BusinessSessionId != _approvalId || !_packing.IsCommittedReceipt(receipt)
            || !_source.OwnsReceipt(Session, receipt)) return false;
        // Authentication precedes scoring. Resolve the original ledger instead of relying on event subscription order.
        if (_score == null || !_score.Refresh() || !_score.TryGetEntry(receipt.ReceiptId, out var award)
            || award.OrderId != receipt.OrderId || award.CustomerId != receipt.CustomerId || award.SlotIndex != receipt.SlotIndex
            || !_score.TryReadCompleteSnapshot(out int recordedScore, out _, out _)
            || (long)Session.TotalScore + award.Points != recordedScore) return false;
        if (!Session.TryRecordPacking(new PopupStorePhasePackingEvidence(Session.BusinessSessionId,
            Session.PhaseRunId, receipt.OrderId, receipt.ReceiptId, award.Points))) return false;
        _accepted.Add(receipt);
        // No time resampling after an already committed transaction, and no receipt queue consumption.
        _clock.RefreshPhasePreview();
        Log("PACK receipt=" + receipt.ReceiptId + "; order=" + receipt.OrderId + "; customer=" + receipt.CustomerId
            + "; slot=" + receipt.SlotIndex + "; accepted=" + _accepted.Count
            + "; points=" + award.Points + "; totalScore=" + Session.TotalScore);
        Observe();
        return true;
    }
    private void OnCommitted(PopupStorePackingReceipt receipt)
    {
        foreach (var accepted in _accepted) if (ReferenceEquals(accepted, receipt)) return;
        if (AcceptCommitted(receipt)) return;
        // Recovering the ledger may synchronously notify another local observer which already accepted
        // this same authenticated receipt. Such reentry is not a second sale or a foreign transaction.
        foreach (var accepted in _accepted) if (ReferenceEquals(accepted, receipt)) return;
        FailClosed("A committed receipt did not belong to the current preview phase.");
    }

    public bool TickCleanup()
    {
        if (!_ready || _cleaning || Session == null || !Session.IsAwaitingCleanup || Session.IsPaused) return false;
        if (LastError != null || _entrance.EditorPhaseSupplyFaulted)
        { FailClosed(LastError ?? "The preview supply lost its original identity."); return false; }
        foreach (var receipt in _accepted)
        {
            bool completed = false, released = false;
            foreach (var outcome in _presentation.VisualOutcomes)
            {
                if (!ReferenceEquals(outcome.Receipt, receipt)) continue;
                if (outcome.Kind != PopupStoreVisualOutcomeKind.VisualCompleted)
                { FailClosed("Committed delivery was cancelled; the next phase remains blocked."); return false; }
                completed = _presentation.IsDeliveryCompleted(receipt.ReceiptId);
            }
            foreach (var r in _entrance.EditorReleasedReceipts) if (ReferenceEquals(r, receipt)) released = true;
            if (!completed || !released) return false;
        }
        if (_presentation.HasPendingVisualWork) return false;
        _cleaning = true;
        try
        {
            if (!_entrance.TryCleanupEditorPhase(this, out var error))
            { if (_entrance.EditorPhaseSupplyFaulted) FailClosed(error); return false; }
            if (!_counter.EditorClearForPhaseCleanup(this)) { FailClosed("Counter cleanup was not authorized."); return false; }
            var token = Session.PhaseRunId;
            if (!Session.TryCompleteCleanup(new PopupStorePhaseCleanupSignal(Session.BusinessSessionId,
                token, PopupStorePhaseCleanupStatus.Completed))) return false;
            CleanupCount++;
            UpdateTerminalPresentation();
            _clock.RefreshPhasePreview();
            Log("CLEANUP phase=" + token + "; count=" + CleanupCount);
            Observe();
            return true;
        }
        finally { _cleaning = false; }
    }

    public bool RequestAbandon()
    {
        if (!_ready || Session == null || Session.IsTerminal || !Session.TryAbandon()) return false;
        _gate = Gate.None;
        UpdateTerminalPresentation();
        GetComponent<PopupStoreExitConfirmation>()?.DismissForForcedHide();
        _clock.RefreshPhasePreview();
        Observe();
        return true;
    }

    public bool TryConfirmResult()
    {
        if (IsAccountMode && !AccountClaimConfirmed) return false;
        if (_confirming || !IsResultPresentationReady || Popup == null || !Popup.CanRequestExit) return false;
        var panel = GetComponent<PopupStoreResultPanel>();
        bool rewardClose = RewardPresentation != null && _rewardClosing
            && (RewardPresentation.State == PopupStorePhaseRewardVisualState.Confirming
                || RewardPresentation.State == PopupStorePhaseRewardVisualState.Confirmed);
        // Once the receive path is attached, only its button may advance/close a secured reward.
        if (RewardPresentation != null && SecuredGrade != PopupStoreGradeTier.None && !rewardClose) return false;
        if (panel == null || (!rewardClose && !panel.IsVisible) || panel.IsAppearing
            || !ReferenceEquals(panel.EditorShownPhaseResult, Session.FinalResult) || _presentation.HasPendingVisualWork) return false;
        for (int i = 0; i < 3; i++)
            if (_entrance.GetState(i) == PopupStoreCustomerState.Leaving
                || _entrance.GetState(i) == PopupStoreCustomerState.Expired) return false;
        _confirming = true;
        try
        {
            if (!Popup.ClosePhasePreviewResult(this)) return false;
            ConfirmationCount++;
            Log("CONFIRMED count=" + ConfirmationCount);
            return true;
        }
        finally { _confirming = false; }
    }

    public bool AttachPolish(PopupStorePhasePreviewPolish polish)
    {
        if (_polish != null || polish == null || !polish.IsBound || !polish.transform.IsChildOf(transform)) return false;
        _polish = polish;
        return true;
    }

    public bool AttachRewardView(PopupStorePhaseRewardPresentation view, PopupStoreRewardTable table)
    {
        if (!_ready || Session.State != PopupStorePhaseState.Ready || _startArmed || RewardPresentation != null
            || view == null || !view.transform.IsChildOf(transform) || !view.ValidateConfiguration(out _)
            || !view.BindClock(_clock)) return false;
        if (IsAccountMode)
        {
            if (table == null || !table.TryCreateSnapshot(out var policy, out _)
                || policy.Schema != PopupStoreRewardSchema.R1Permille || policy.Denominator != 1000
                || policy.PolicyId != PopupStoreRewardTable.R1PolicyId) return false;
            _accountRewardPolicy = policy;
        }
        else
        {
            if (!PopupStorePhaseRewardRoll.TryCreate(Session, table, out var roll, out _)) return false;
            RewardRoll = roll;
        }
        RewardPresentation = view;
        view.HidePresentation();
        return true;
    }

    public bool TryReceiveResult()
    {
        if (_receiving || _confirming || !IsResultPresentationReady || Popup == null || !Popup.CanRequestExit) return false;
        var panel = GetComponent<PopupStoreResultPanel>();
        if (panel == null || !panel.IsVisible || panel.IsAppearing
            || !ReferenceEquals(panel.EditorShownPhaseResult, Session.FinalResult) || _presentation.HasPendingVisualWork) return false;
        if (IsAccountMode)
        {
            if (!_accountClaimConfirmed) return RequestAccountClaim();
            if (RewardPresentation != null && RewardPresentation.BeginCount == 0 && SecuredGrade != PopupStoreGradeTier.None)
                return BeginConfirmedAccountPresentation();
        }
        if (RewardPresentation == null || SecuredGrade == PopupStoreGradeTier.None) return TryConfirmResult();
        if (RewardPresentation.State == PopupStorePhaseRewardVisualState.Confirmed)
        {
            _rewardClosing = true;
            try { return TryConfirmResult(); }
            finally { _rewardClosing = false; }
        }
        if (RewardPresentation.BeginCount != 0) return false;
        for (int i = 0; i < 3; i++)
            if (_entrance.GetState(i) == PopupStoreCustomerState.Leaving
                || _entrance.GetState(i) == PopupStoreCustomerState.Expired) return false;
        _receiving = true;
        try
        {
            if (RewardRoll == null || !RewardRoll.TryDecide(Session.FinalResult))
            { Log("REWARD_BLOCKED " + RewardRoll?.LastError); return false; }
            var sprite = GetComponentInChildren<PopupStoreCurrentGradeHud>(true)?.SpriteFor(SecuredGrade);
            if (!RewardPresentation.Begin(Session.FinalResult, sprite, RewardRoll.Decision.Diamonds,
                "R1 검수용 · 미지급", () => {
                    _rewardClosing = true;
                    try { return TryConfirmResult(); }
                    finally { _rewardClosing = false; }
                })) return false;
            panel.EditorHideSummaryForReward();
            Log("REWARD_RECEIVED grade=" + SecuredGrade + "; diamonds=" + RewardRoll.Decision.Diamonds
                + "; draws=" + RewardRoll.RandomDrawCount + "; decisions=" + RewardRoll.DecisionCount + "; unpaid=true");
            return true;
        }
        finally { _receiving = false; }
    }

    public void FailClosed(string reason)
    {
        bool first = LastError == null;
        if (first) LastError = reason ?? "Preview safety validation failed.";
        if (Session != null && !Session.IsTerminal)
        {
            if (first) Session.TryAbandon();
            Session.TryCompleteCleanup(new PopupStorePhaseCleanupSignal(Session.BusinessSessionId,
                Session.PhaseRunId, PopupStorePhaseCleanupStatus.Failed));
        }
        _gate = Gate.None;
        UpdateTerminalPresentation();
        _clock?.RefreshPhasePreview();
        if (first) Debug.LogError("POPUP_STORE_PHASE_PREVIEW_BLOCKED " + LastError, this);
    }

    private void PrepareAccountResult()
    {
        if (!IsAccountMode || !AccountAuthorityReady || Session?.FinalResult == null || _prepareAttempted) return;
        _prepareAttempted = _preparePending = true;
        var result = Session.FinalResult;
        AccountStatus = "결과를 저장하고 있습니다.";
        try
        {
            _accountBridge.PrepareResult(Session, result, response => {
                if (this == null || !AccountAuthorityReady || !ReferenceEquals(Session.FinalResult, result) || !_preparePending) return;
                _preparePending = false;
                if (response != null && response.BusinessSessionId == result.BusinessSessionId
                    && response.State == PopupStoreAccountClaimState.Confirmed)
                { _preparedResult = result; AccountStatus = null; }
                else AccountStatus = response?.Message ?? "결과 저장을 다시 확인해 주세요.";
            });
        }
        catch (Exception error)
        { _preparePending = false; AccountStatus = "결과 저장 확인 필요: " + error.GetType().Name; }
    }

    private bool RequestAccountClaim()
    {
        if (!AccountAuthorityReady || _accountClaimPending || _preparePending || Session?.FinalResult == null
            || _accountRewardPolicy == null || RewardPresentation == null) return false;
        var result = Session.FinalResult;
        long serial = ++_accountRequestSerial;
        _accountClaimPending = true; _claimElapsed = 0;
        SetAccountClaimStatus("수령 확인 중", false);
        try
        {
            _accountBridge.FinalizeAndClaim(Session, result, response => {
                if (this == null || serial != _accountRequestSerial || !_accountClaimPending
                    || !AccountAuthorityReady || !ReferenceEquals(Session.FinalResult, result)) return;
                _accountClaimPending = false;
                if (response == null || response.BusinessSessionId != result.BusinessSessionId
                    || response.State != PopupStoreAccountClaimState.Confirmed)
                {
                    AccountStatus = response?.Message ?? "수령 결과를 다시 확인해 주세요.";
                    SetAccountClaimStatus(response?.State == PopupStoreAccountClaimState.Rejected ? "확인 필요" : "다시 확인",
                        response?.State != PopupStoreAccountClaimState.Rejected);
                    return;
                }
                bool noReward = SecuredGrade == PopupStoreGradeTier.None;
                if ((noReward && response.Diamonds != 0) || (!noReward && !ValidAccountAmount(response.Diamonds)))
                { AccountStatus = "저장된 보상 정보를 확인해 주세요."; SetAccountClaimStatus("확인 필요", false); return; }
                _accountClaimConfirmed = true; _accountConfirmedDiamonds = response.Diamonds;
                AccountStatus = null;
                if (noReward) { TryConfirmResult(); return; }
                if (!BeginConfirmedAccountPresentation())
                { AccountStatus = "지급은 확인됐습니다. 화면을 다시 확인해 주세요."; SetAccountClaimStatus("다시 확인", true); }
            });
            return true;
        }
        catch (Exception error)
        { _accountClaimPending = false; AccountStatus = "수령 확인 필요: " + error.GetType().Name; SetAccountClaimStatus("다시 확인", true); return false; }
    }

    private bool ValidAccountAmount(int amount)
        => _accountRewardPolicy != null && _accountRewardPolicy.TryGetReward(SecuredGrade, out var row, out _)
            && (amount == row.BasicDiamonds || amount == row.BonusDiamonds || amount == row.JackpotDiamonds);
    private void SetAccountClaimStatus(string label, bool retry)
        => GetComponent<PopupStoreResultPanel>()?.SetAccountClaimStatus(label, retry);
    private bool BeginConfirmedAccountPresentation()
    {
        if (!AccountClaimConfirmed || RewardPresentation == null || Session?.FinalResult == null) return false;
        var sprite = GetComponentInChildren<PopupStoreCurrentGradeHud>(true)?.SpriteFor(SecuredGrade);
        if (!RewardPresentation.BeginAccountConfirmed(this, Session.FinalResult, sprite, _accountConfirmedDiamonds, () => {
            _rewardClosing = true;
            try { return TryConfirmResult(); }
            finally { _rewardClosing = false; }
        })) return false;
        GetComponent<PopupStoreResultPanel>()?.EditorHideSummaryForReward();
        return true;
    }

    private void Observe()
    {
        if (Session == null) return;
        PrepareAccountResult();
        _polish?.TickWarning(0);
        RenderPresentation();
        string cue = PresentationCue;
        if (_lastCue != cue)
        {
            _lastCue = cue;
            if (!string.IsNullOrEmpty(cue)) Log("CUE name=" + cue + "; stage=" + PresentationStage);
        }
        string stamp = Session.PhaseRunId + ":" + Session.State + ":" + Session.CountdownValue + ":" + Session.PhaseSuccessfulOrders;
        if (_loggedState == stamp) return;
        _loggedState = stamp;
        Log("STATE state=" + Session.State + "; stage=" + Session.CurrentPhase.Grade + "; goal="
            + Session.PhaseSuccessfulOrders + "/" + Session.CurrentPhase.TargetSuccessfulOrders
            + "; score=" + Session.TotalScore + "; targetScore=" + Session.CurrentPhase.TargetScore
            + "; metric=" + Session.Profile.GoalMetric
            + "; highest=" + Session.HighestClearedGrade + "; finalized=" + Session.FinalizationCount);
    }
    private void Log(string text)
    {
        if (Application.isPlaying) Debug.Log("POPUP_STORE_PHASE_PREVIEW session=" + Session?.BusinessSessionId
            + "; phase=" + Session?.PhaseRunId + "; " + text, this);
    }
    private void LateUpdate()
    {
        if (!Application.isPlaying || !_ready) return;
        (_accountBridge as IPopupStoreAccountProgress)?.ObserveElapsed(Time.unscaledDeltaTime);
        if (_preparePending && (_prepareElapsed += Time.unscaledDeltaTime) >= 10)
        { _preparePending = false; AccountStatus = "결과 저장을 다시 확인해 주세요."; }
        if (_accountClaimPending && (_claimElapsed += Time.unscaledDeltaTime) >= 10)
        { _accountClaimPending = false; ++_accountRequestSerial; AccountStatus = "수령 결과를 다시 확인해 주세요."; SetAccountClaimStatus("다시 확인", true); }
        if (IsAccountMode && !AccountAuthorityReady)
        { AccountStatus = "계정 상태를 확인해 주세요."; _clock?.SetAccountAccessSuspended(true); return; }
        if (_entrance.EditorPhaseSupplyFaulted) FailClosed("The preview supply lost its original identity.");
        if (Session != null && !Session.IsTerminal && _start != null && !_start.IsApproved
            && Session.State != PopupStorePhaseState.Ready) FailClosed("Isolated admission ownership was lost.");
        TickCleanup(); // State/evidence observation only. No second clock or automatic gameplay.
        RenderHud();
    }
    internal void RenderHud()
    {
        if (!_ready || Session == null) return;
        _polish?.TickWarning(0);
        GetComponent<PopupStoreGradeSession>()?.EditorRenderPhaseHud(TransitionText,
            GoalProgress);
        GetComponentInChildren<PopupStoreCurrentGradeHud>(true)?.RefreshDisplay();
        RenderPresentation();
    }
    private int PresentationStage => Session == null ? 1 : Session.CurrentPhase.PhaseNumber
        + (_gate == Gate.NextStage || Session.State == PopupStorePhaseState.Countdown ? 1 : 0);

    public bool AttachView(PopupStorePhasePreviewView view, UnityEngine.UI.Button originalClose)
    {
        if (!_ready || view == null || _view != null || !view.transform.IsChildOf(transform)
            || Session.State != PopupStorePhaseState.Ready || _startArmed || originalClose == null
            || !originalClose.transform.IsChildOf(transform) || originalClose.transform.IsChildOf(view.transform)
            || !view.ValidateConfiguration(out _)) return false;
        _start.ReleaseUnavailablePresentation();
        view.SetExitControl(originalClose.transform as RectTransform);
        view.Bind(() => {
            _tutorialStartRequested = true;
            try { return _start.RequestStart(); }
            finally { _tutorialStartRequested = false; }
        }, () => Popup.TryRequestExit());
        _view = view; _originalClose = originalClose;
        _closeWasActive = originalClose.gameObject.activeSelf;
        _start.OnViewShown();
        return true;
    }
    public bool RenderStart(bool show, bool canStart, int remaining, int limit, string status = null,
        bool requestPending = false, bool confirmedTicketExhaustion = false)
    {
        if (_view == null) return false;
        _view.RenderTutorial(show, canStart, "이용권 "
            + (remaining < 0 ? "—" : remaining.ToString()) + "/" + (limit < 0 ? "—" : limit.ToString()));
        if (IsAccountMode) _view.RenderAccountAdmission(requestPending, canStart && AccountAuthorityReady, status,
            confirmedTicketExhaustion && remaining == 0 && limit > 0 && AccountAuthorityReady);
        if (_originalClose != null) _originalClose.gameObject.SetActive(!show && _closeWasActive);
        RenderPresentation();
        return true;
    }
    private void RenderPresentation()
    {
        if (_view == null) return;
        _view.RenderCue(PresentationCue, PresentationStage, PresentationProgress);
        // Exit confirmation must remain above the cue so cancel/abandon is always reachable.
        if (GetComponent<PopupStoreExitConfirmation>()?.IsVisible != true) _view.transform.SetAsLastSibling();
    }
    private void OnDestroy()
    {
        ++_accountRequestSerial;
        if (_packing != null) _packing.SucceededCommitted -= OnCommitted;
        if (_originalClose != null) _originalClose.gameObject.SetActive(_closeWasActive);
        if (_view != null) _view.HideAll();
        if (RewardPresentation != null) RewardPresentation.HidePresentation();
    }
}
