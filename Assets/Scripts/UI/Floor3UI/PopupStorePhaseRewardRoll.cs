using System;

/// <summary>
/// One phase-preview owner's local R1 decision. The popup retains this instance for its whole
/// business; this factory has no global registry. Progress facts remain independent of this policy.
/// No ordinary reward-session observer, account, persistence, score or receipt writes occur here.
/// </summary>
public sealed class PopupStorePhaseRewardRoll
{
    private readonly PopupStorePhaseSession _session;
    private readonly string _sessionId;
    private IPopupStoreRewardRandomSource _random;
    private PopupStorePhaseResult _decidedResult;
    private bool _deciding, _randomAttempted;
    private string _attemptError;

    public PopupStoreRewardPolicySnapshot PolicySnapshot { get; }
    public PopupStoreRewardDecision Decision { get; private set; }
    public int RandomDrawCount { get; private set; }
    public int DecisionCount { get; private set; }
    public string LastError { get; private set; }

    private PopupStorePhaseRewardRoll(PopupStorePhaseSession session, PopupStoreRewardPolicySnapshot policy)
    {
        _session = session;
        _sessionId = session.BusinessSessionId;
        PolicySnapshot = policy;
    }

    /// <summary>Freeze the explicitly approved R1 table once, before its presentation owner is used.</summary>
    public static bool TryCreate(PopupStorePhaseSession session, PopupStoreRewardTable table,
        out PopupStorePhaseRewardRoll roll, out string error)
    {
        roll = null; error = null;
        if (session == null || string.IsNullOrWhiteSpace(session.BusinessSessionId))
        { error = "A phase-preview business owner is required."; return false; }
        if (table == null)
        { error = "The approved R1 reward table is missing."; return false; }
        if (!table.TryCreateSnapshot(out var policy, out error)) return false;
        if (policy.Schema != PopupStoreRewardSchema.R1Permille || policy.Denominator != 1000
            || !string.Equals(policy.PolicyId, PopupStoreRewardTable.R1PolicyId, StringComparison.Ordinal))
        { error = "Phase-preview rewards require the approved R1 policy in 1000 units."; return false; }
        roll = new PopupStorePhaseRewardRoll(session, policy);
        return true;
    }

    public bool TrySetRandomSource(IPopupStoreRewardRandomSource source)
    {
        if (source == null || _deciding || _randomAttempted || Decision != null) return false;
        _random = source;
        return true;
    }

    /// <summary>
    /// Completed/timed-out sessions may preview their highest cleared grade. None, abandonment
    /// and failed/cancelled cleanup never draw. One failed provider attempt cannot be retried.
    /// </summary>
    public bool TryDecide(PopupStorePhaseResult result)
    {
        if (_deciding) return false;
        _deciding = true;
        try
        {
            if (!ValidateResult(result, out string error)) return Reject(error);
            if (Decision != null)
            {
                if (!ReferenceEquals(_decidedResult, result))
                    return Reject("A phase reward decision cannot be rebound to another result.");
                LastError = null;
                return true;
            }
            if (_randomAttempted)
                return Reject(_attemptError ?? "This phase business already attempted its reward selection.");
            if (!PolicySnapshot.TryGetReward(result.HighestClearedGrade, out var entry, out error))
                return Reject(error ?? "No reward row exists for the highest cleared grade.");

            // Reserve before calling externally supplied code, including a throwing/reentrant source.
            _randomAttempted = true;
            int value;
            try
            {
                if (_random == null) _random = new PopupStoreRewardRandomSource();
                RandomDrawCount++;
                value = _random.NextInt(PolicySnapshot.Denominator);
            }
            catch (Exception exception)
            {
                _attemptError = "Phase reward random source failed: " + exception.Message;
                return Reject(_attemptError);
            }
            if (!PopupStoreRewardSelector.TrySelect(entry, value, PolicySnapshot.Denominator,
                out var kind, out int diamonds, out error))
            { _attemptError = error; return Reject(error); }
            if (!ValidateResult(result, out error))
            {
                _attemptError = "The owning final result changed during phase reward selection: " + error;
                return Reject(_attemptError);
            }
            Decision = new PopupStoreRewardDecision(_sessionId, result.HighestClearedGrade,
                kind, diamonds, PolicySnapshot.PolicyId);
            _decidedResult = result;
            DecisionCount++;
            LastError = null;
            return true;
        }
        finally { _deciding = false; }
    }

    private bool ValidateResult(PopupStorePhaseResult result, out string error)
    {
        error = null;
        if (result == null || !_session.IsTerminal || !ReferenceEquals(result, _session.FinalResult)
            || _session.FinalizationCount != 1 || result.FinalizationCount != 1
            || !string.Equals(_sessionId, _session.BusinessSessionId, StringComparison.Ordinal)
            || !string.Equals(_sessionId, result.BusinessSessionId, StringComparison.Ordinal))
            error = "Phase reward selection requires this business's original single final result.";
        else if (!string.Equals(result.ProfileId, _session.Profile.ProfileId, StringComparison.Ordinal)
            || result.ProfileVersion != _session.Profile.Version
            || result.SuccessfulOrders != _session.TotalSuccessfulOrders
            || result.HighestClearedGrade != _session.HighestClearedGrade)
            error = "The final progress facts do not belong to the owning phase session.";
        else if (!((result.EndReason == PopupStorePhaseEndReason.Completed && _session.State == PopupStorePhaseState.Completed)
            || (result.EndReason == PopupStorePhaseEndReason.TimedOut && _session.State == PopupStorePhaseState.TimedOut)))
            error = "Abandoned or failed/cancelled phase businesses have no reward preview.";
        else if (result.HighestClearedGrade < PopupStoreGradeTier.C || result.HighestClearedGrade > PopupStoreGradeTier.SSS)
            error = "A cleared grade from C through SSS is required for a phase reward preview.";
        return error == null;
    }

    private bool Reject(string error)
    {
        LastError = error;
        return false;
    }
}
