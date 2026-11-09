using System;
using UnityEngine;

/// <summary>Displays an original persisted result after restart. Does not manufacture a phase, score, receipt or roll.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreAccountRecoveryPresentation : MonoBehaviour
{
    private IPopupStoreAccountPhaseBridge _bridge;
    private IPopupStoreAccountRewardRecovery _recovery;
    private PopupStorePhaseRewardPresentation _reward;
    private bool _pending, _confirmed, _closing;
    private long _serial;
    private float _elapsed;
    public PopupStoreAccountRecoverySnapshot Snapshot { get; private set; }
    public bool IsBound => Snapshot != null;
    public bool IsCurrent => IsBound && _bridge != null && _bridge.IsReady;
    public bool IsConfirmed => _confirmed && IsCurrent;
    public PopupStorePhaseRewardPresentation Reward => _reward;

    internal bool Bind(IPopupStoreAccountPhaseBridge bridge, IPopupStoreAccountRewardRecovery recovery,
        PopupStorePhaseRewardPresentation reward, PopupStoreRewardTable table)
    {
        if (IsBound || bridge == null || !bridge.IsReady || bridge.AdmissionHost?.IsEditorReview != false
            || recovery?.Recovery == null || reward == null || !reward.transform.IsChildOf(transform)) return false;
        var snapshot = recovery.Recovery;
        if (string.IsNullOrWhiteSpace(snapshot.SessionId) || snapshot.Score < 0 || snapshot.Diamonds < 0
            || (snapshot.IsInterrupted && snapshot.EligibleReward)) return false;
        if (snapshot.EligibleReward)
        {
            if (table == null || !table.TryCreateSnapshot(out var policy, out _)
                || policy.PolicyId != PopupStoreRewardTable.R1PolicyId || policy.Schema != PopupStoreRewardSchema.R1Permille
                || !policy.TryGetReward(snapshot.Grade, out var row, out _)
                || (snapshot.Diamonds != row.BasicDiamonds && snapshot.Diamonds != row.BonusDiamonds
                    && snapshot.Diamonds != row.JackpotDiamonds)) return false;
        }
        else if (snapshot.Diamonds != 0) return false;
        _bridge = bridge; _recovery = recovery; _reward = reward; Snapshot = snapshot;
        var clock = GetComponent<PopupStoreBusinessClock>();
        if (clock == null || !clock.BindAccountRecovery(this) || !reward.BindClock(clock)
            || !GetComponent<PopupStoreResultPanel>().BindAccountRecovery(this))
        { Snapshot = null; return false; }
        reward.HidePresentation();
        GetComponent<UIPopupStore>().PhaseCloseButton.gameObject.SetActive(false);
        return true;
    }

    public bool TryReceive()
    {
        var popup = GetComponent<UIPopupStore>();
        var panel = GetComponent<PopupStoreResultPanel>();
        if (!IsCurrent || _pending || popup == null || !popup.CanRequestExit || !panel.IsVisible || panel.IsAppearing) return false;
        if (_confirmed) return BeginConfirmedPresentation();
        _pending = true; _elapsed = 0; long serial = ++_serial;
        panel.SetAccountClaimStatus("수령 확인 중", false);
        try
        {
            _recovery.ClaimRecovery(response => {
                if (this == null || serial != _serial || !_pending || !IsCurrent) return;
                _pending = false;
                if (response == null || response.BusinessSessionId != Snapshot.SessionId
                    || response.State != PopupStoreAccountClaimState.Confirmed)
                { panel.SetAccountClaimStatus("다시 확인", response?.State != PopupStoreAccountClaimState.Rejected); return; }
                if (response.Diamonds != Snapshot.Diamonds)
                { panel.SetAccountClaimStatus("확인 필요", false); return; }
                _confirmed = true;
                if (!BeginConfirmedPresentation()) panel.SetAccountClaimStatus("다시 확인", true);
            });
            return true;
        }
        catch { _pending = false; panel.SetAccountClaimStatus("다시 확인", true); return false; }
    }

    private bool BeginConfirmedPresentation()
    {
        if (!IsConfirmed) return false;
        if (!Snapshot.EligibleReward) return TryClose();
        var sprite = GetComponentInChildren<PopupStoreCurrentGradeHud>(true)?.SpriteFor(Snapshot.Grade);
        if (!_reward.BeginAccountRecovery(this, sprite, TryClose)) return false;
        GetComponent<PopupStoreResultPanel>().EditorHideSummaryForReward();
        return true;
    }

    private bool TryClose()
    {
        if (_closing || !IsConfirmed || (_reward.BeginCount != 0 && _reward.State != PopupStorePhaseRewardVisualState.Confirming)) return false;
        var popup = GetComponent<UIPopupStore>();
        var lifetime = GetComponent<PopupStoreSessionLifetime>();
        if (!popup.CanRequestExit || lifetime == null || !lifetime.MarkAccountRecoveryClosed(this)) return false;
        _closing = true;
        try
        {
            bool closed = popup.CloseWithoutConfirmation();
            if (!closed) lifetime.UndoCompletedClose();
            return closed;
        }
        finally { _closing = false; }
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        (_bridge as IPopupStoreAccountProgress)?.ObserveElapsed(Time.unscaledDeltaTime);
        if (_pending && (_elapsed += Time.unscaledDeltaTime) >= 10f)
        { _pending = false; ++_serial; GetComponent<PopupStoreResultPanel>()?.SetAccountClaimStatus("다시 확인", true); }
    }
    private void OnDestroy() { ++_serial; _reward?.HidePresentation(); }
}
