using System;
using UnityEngine;

/// <summary>Observes one popup's immutable final grade and keeps one local reward preview in memory.
/// No score refresh, receipt consumption, UI mutation, account storage or payment.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreRewardSession : MonoBehaviour
{
    [SerializeField] private PopupStoreGradeSession _grade;
    [SerializeField] private PopupStoreRewardTable _table;

    private PopupStoreGradeSession _ownerGrade, _boundGrade;
    private PopupStoreGradeResult _resolvedResult;
    private IPopupStoreRewardRandomSource _random;
    private bool _deciding, _randomAttempted;
    private string _randomError;
    private bool _policyPrepared;
    private string _policySessionId;

    public PopupStoreRewardPolicySnapshot PolicySnapshot { get; private set; }
    public bool HasPolicySnapshot => PolicySnapshot != null;
    public string PolicyError { get; private set; }

    public PopupStoreRewardDecision Result { get; private set; }
    public bool IsResolved { get; private set; }
    public string ResolvedSessionId { get; private set; }
    public int DecisionCount { get; private set; }
    public int RandomDrawCount { get; private set; }
    public string LastError { get; private set; }

    /// <summary>Called only at the existing approved clock-start commit boundary. No random call or result.
    /// Invalid data is latched too: repairing/swapping an asset cannot change an already started business.</summary>
    internal void PreparePolicyForApprovedStart()
    {
        if (_policyPrepared) return;
        var clock = GetComponent<PopupStoreBusinessClock>();
        if (clock == null || clock.State != PopupStoreBusinessState.NotStarted || !clock.HasStartApproval
            || _grade == null || _grade.gameObject != gameObject) return;
        _policyPrepared = true;
        _policySessionId = _grade.SessionId;
        if (_table == null) PolicyError = "The approved-start reward table is missing.";
        else if (!_table.TryCreateSnapshot(out var snapshot, out string error)) PolicyError = error;
        else { PolicySnapshot = snapshot; PolicyError = null; }
    }

    /// <summary>Dependency injection is allowed only before any terminal decision or random attempt.</summary>
    public bool TrySetRandomSource(IPopupStoreRewardRandomSource source)
    {
        if (source == null || _deciding || IsResolved || _randomAttempted) return false;
        _random = source;
        return true;
    }

    public bool Refresh()
    {
        if (!Bind(out string error)) return Reject(error);
        return !_ownerGrade.IsFinalized || TryDecide(_ownerGrade.FinalResult);
    }

    public bool TryDecide() => TryDecide(_grade != null ? _grade.FinalResult : null);

    public bool TryDecide(PopupStoreGradeResult finalResult)
    {
        // Reserve the entry before invoking an injectable provider or an external log observer.
        if (_deciding) return false;
        _deciding = true;
        try
        {
            if (!Bind(out string error) || !ValidateFinal(finalResult, out error)) return Reject(error);
            if (IsResolved)
            {
                if (!ReferenceEquals(_resolvedResult, finalResult)
                    || !string.Equals(ResolvedSessionId, finalResult.SessionId, StringComparison.Ordinal))
                    return Reject("A resolved preview cannot be replaced by another final result.");
                LastError = null;
                return true;
            }
            // A malformed provider must not become a reroll opportunity on reentry or another event.
            if (_randomAttempted) return Reject(_randomError ?? "This session already attempted its random selection.");
            if (finalResult.Grade == PopupStoreGradeTier.None)
            {
                Resolve(finalResult);
                return true;
            }
            if (!_policyPrepared || !string.Equals(_policySessionId, finalResult.SessionId, StringComparison.Ordinal))
                return Reject("This business has no approved-start reward policy snapshot.");
            if (PolicySnapshot == null) return Reject(PolicyError ?? "The approved-start reward policy is invalid.");
            if (!PolicySnapshot.TryGetReward(finalResult.Grade, out var entry, out error)) return Reject(error);
            _randomAttempted = true;
            int roll;
            try
            {
                if (_random == null) _random = new PopupStoreRewardRandomSource();
                RandomDrawCount++;
                roll = _random.NextInt(PolicySnapshot.Denominator);
            }
            catch (Exception exception)
            {
                _randomError = "Reward random source failed: " + exception.Message;
                return Reject(_randomError);
            }
            if (!PopupStoreRewardSelector.TrySelect(entry, roll, PolicySnapshot.Denominator, out var kind, out int diamonds, out error))
            { _randomError = error; return Reject(error); }
            if (!Bind(out error) || !ValidateFinal(finalResult, out error))
            {
                _randomError = "The final-result source changed during selection.";
                return Reject(_randomError);
            }
            Result = new PopupStoreRewardDecision(finalResult.SessionId, finalResult.Grade, kind, diamonds, PolicySnapshot.PolicyId);
            DecisionCount++;
            Resolve(finalResult);
#if UNITY_EDITOR
            Debug.Log("POPUP_STORE_REWARD_DECIDED session=" + Result.SessionId + " grade=" + Result.Grade
                + " kind=" + Result.Kind + " diamonds=" + Result.Diamonds + " decision=" + Result.DecisionId
                + " policy=" + Result.PolicyId + " denominator=" + PolicySnapshot.Denominator
                + " draws=" + RandomDrawCount + " localOnly=True", this);
#endif
            return true;
        }
        finally { _deciding = false; }
    }

    private bool ValidateFinal(PopupStoreGradeResult result, out string error)
    {
        error = null;
        var clock = GetComponent<PopupStoreBusinessClock>();
        if (clock == null || !clock.HasStartApproval || clock.State != PopupStoreBusinessState.Ended
            || clock.EndReason != PopupStoreBusinessEndReason.Completed)
            error = "Reward selection requires an approved, normally completed business.";
        else if (_ownerGrade == null || !_ownerGrade.IsFinalized || result == null || !result.IsFinalized
            || !ReferenceEquals(result, _ownerGrade.FinalResult) || string.IsNullOrEmpty(result.SessionId)
            || !string.Equals(result.SessionId, _ownerGrade.SessionId, StringComparison.Ordinal))
            error = "Reward selection requires this session's original finalized result.";
        else if (result.Grade < PopupStoreGradeTier.None || result.Grade > PopupStoreGradeTier.SSS
            || result.Score < 0 || result.SuccessfulOrders < 0
            || (result.Grade == PopupStoreGradeTier.None && (result.Score != 0 || result.SuccessfulOrders != 0))
            || (result.Grade != PopupStoreGradeTier.None && (result.Score <= 0 || result.SuccessfulOrders <= 0)))
            error = "The finalized reward grade or sale counts are invalid.";
        return error == null;
    }

    private void Resolve(PopupStoreGradeResult result)
    {
        _resolvedResult = result; ResolvedSessionId = result.SessionId;
        IsResolved = true; LastError = null;
    }

    private bool Bind(out string error)
    {
        error = null;
        if (this == null || GetComponent<UIPopupStore>() == null || _grade == null || _grade.gameObject != gameObject)
            error = "Reward grade source must belong to this popup root.";
        else if (!ReferenceEquals(_ownerGrade, null) && !ReferenceEquals(_ownerGrade, _grade))
            error = "A reward session cannot be rebound to another grade session.";
        if (error != null) return false;
        if (ReferenceEquals(_ownerGrade, null)) _ownerGrade = _grade;
        if (_boundGrade != _ownerGrade)
        {
            Unbind(); _boundGrade = _ownerGrade;
            _boundGrade.Finalized += OnFinalized;
        }
        return true;
    }

    private bool Reject(string error)
    {
        if (LastError != error) Debug.LogError("PopupStoreRewardSession: local preview not decided. " + error, this);
        LastError = error;
        return false;
    }
    private void OnFinalized(PopupStoreGradeResult result) => TryDecide(result);
    private void Unbind()
    {
        if (_boundGrade != null) _boundGrade.Finalized -= OnFinalized;
        _boundGrade = null;
    }
    private void OnEnable() => Refresh();
    // Like GradeSession, retain the terminal observer while hidden. Reentry never resets local data.
    private void OnDisable() { }
    private void OnDestroy() => Unbind();

#if UNITY_EDITOR
    [ContextMenu("Diagnostics/Log Local Reward Decision (Read Only)")]
    private void LogLocalDecision()
    {
        Debug.Log("POPUP_STORE_REWARD_QUERY session=" + (_ownerGrade != null ? _ownerGrade.SessionId : "unbound")
            + " resolved=" + IsResolved + " decision=" + (Result != null ? Result.DecisionId : "none")
            + " grade=" + (Result != null ? Result.Grade.ToString() : "none")
            + " kind=" + (Result != null ? Result.Kind.ToString() : "none")
            + " diamonds=" + (Result != null ? Result.Diamonds : 0) + " draws=" + RandomDrawCount
            + " policy=" + (Result != null ? Result.PolicyId : PolicySnapshot?.PolicyId ?? "none")
            + " decisions=" + DecisionCount + " localOnly=True error=" + LastError, this);
    }
#endif
}
