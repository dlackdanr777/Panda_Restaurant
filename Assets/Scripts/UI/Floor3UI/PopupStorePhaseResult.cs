using System;
using System.Collections.Generic;

public enum PopupStorePhaseEndReason { Completed, TimedOut, Abandoned, CleanupFailed, CleanupCancelled }

/// <summary>Immutable phase progress and score facts, never a reward entitlement.</summary>
public sealed class PopupStorePhaseProgressSummary
{
    public int PhaseNumber { get; }
    public string PhaseRunId { get; }
    public PopupStoreGradeTier Grade { get; }
    public int SuccessfulOrders { get; }
    public double UsedSeconds { get; }
    public bool Cleared { get; }
    public int PhaseScore { get; }
    public int TotalScore { get; }

    internal PopupStorePhaseProgressSummary(int phaseNumber, string runId, PopupStoreGradeTier grade,
        int successfulOrders, double usedSeconds, bool cleared, int phaseScore = 0, int totalScore = 0)
    {
        PhaseNumber = phaseNumber; PhaseRunId = runId; Grade = grade;
        SuccessfulOrders = successfulOrders; UsedSeconds = usedSeconds; Cleared = cleared;
        PhaseScore = phaseScore; TotalScore = totalScore;
    }
}

/// <summary>Immutable local facts. HighestClearedGrade does not decide a payable reward grade.</summary>
public sealed class PopupStorePhaseResult
{
    public string BusinessSessionId { get; }
    public string ProfileId { get; }
    public int ProfileVersion { get; }
    public PopupStorePhaseEndReason EndReason { get; }
    public int LastAttemptedPhaseNumber { get; }
    public PopupStoreGradeTier HighestClearedGrade { get; }
    public long SuccessfulOrders { get; }
    public int TotalScore { get; }
    public PopupStorePhaseGoalMetric GoalMetric { get; }
    public IReadOnlyList<int> ClearedPhases { get; }
    public IReadOnlyList<PopupStorePhaseProgressSummary> PhaseSummaries { get; }
    public int FinalizationCount => 1;

    internal PopupStorePhaseResult(string businessSessionId, PopupStorePhaseProfileSnapshot profile,
        PopupStorePhaseEndReason reason, int lastPhase, PopupStoreGradeTier highest, long successful,
        IReadOnlyList<int> cleared, IReadOnlyList<PopupStorePhaseProgressSummary> summaries, int totalScore = 0)
    {
        BusinessSessionId = businessSessionId; ProfileId = profile.ProfileId; ProfileVersion = profile.Version;
        EndReason = reason; LastAttemptedPhaseNumber = lastPhase; HighestClearedGrade = highest;
        SuccessfulOrders = successful;
        TotalScore = totalScore; GoalMetric = profile.GoalMetric;
        var phases = new int[cleared.Count];
        for (int i = 0; i < phases.Length; i++) phases[i] = cleared[i];
        ClearedPhases = Array.AsReadOnly(phases);
        var progress = new PopupStorePhaseProgressSummary[summaries.Count];
        for (int i = 0; i < progress.Length; i++) progress[i] = summaries[i];
        PhaseSummaries = Array.AsReadOnly(progress);
    }
}
