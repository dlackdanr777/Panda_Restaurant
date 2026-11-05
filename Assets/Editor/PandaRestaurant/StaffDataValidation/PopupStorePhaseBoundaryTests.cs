using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>TEST_ONLY explicit elapsed samples and adapter assertions, never real app/receipt authorization.</summary>
public sealed class PopupStorePhaseBoundaryTests
{
    [TestCase(PopupStorePhasePauseReason.ApplicationBackground)]
    [TestCase(PopupStorePhasePauseReason.FocusLost)]
    [TestCase(PopupStorePhasePauseReason.ViewHidden)]
    [TestCase(PopupStorePhasePauseReason.ExitConfirmation)]
    public void EachPauseReasonBlocksTimePackingAndEntryWithoutCatchup(PopupStorePhasePauseReason reason)
    {
        var session = Started();
        Assert.That(session.TryAdvanceTime(1), Is.True);
        double remaining = session.RemainingSeconds;
        int transitions = session.TransitionCount;
        var evidence = Evidence(session, "paused");
        Assert.That(session.TrySetPauseReason(reason, true), Is.True);
        Assert.That(session.TrySetPauseReason(reason, true), Is.False);
        Assert.That(session.CanAcceptPacking, Is.False);
        Assert.That(session.CanStartCustomerEntry, Is.False);
        Assert.That(session.TryAdvanceTime(9999), Is.False);
        Assert.That(session.TryRecordPacking(evidence), Is.False);
        Assert.That(session.RemainingSeconds, Is.EqualTo(remaining));
        Assert.That(session.TransitionCount, Is.EqualTo(transitions));
        Assert.That(session.TrySetPauseReason(reason, false), Is.True);
        // The future adapter must reset its elapsed baseline. A fresh one-second sample is all that is supplied.
        Assert.That(session.TryAdvanceTime(1), Is.True);
        Assert.That(session.RemainingSeconds, Is.EqualTo(remaining - 1));
        Assert.That(session.TryRecordPacking(evidence), Is.True, "A rejected paused input must not burn its IDs.");
    }

    [Test]
    public void OverlappingPauseReasonsAreIndependentAndReturnedReasonsCannotBeMutated()
    {
        var session = Started();
        var reasons = (PopupStorePhasePauseReason[])Enum.GetValues(typeof(PopupStorePhasePauseReason));
        foreach (var reason in reasons) Assert.That(session.TrySetPauseReason(reason, true), Is.True);
        var exposed = session.PauseReasons;
        Assert.Throws<NotSupportedException>(() => ((IList<PopupStorePhasePauseReason>)exposed).Clear());
        foreach (var reason in reasons.Take(reasons.Length - 1))
        {
            Assert.That(session.TrySetPauseReason(reason, false), Is.True);
            Assert.That(session.IsPaused, Is.True);
            Assert.That(session.TryAdvanceTime(1), Is.False);
        }
        Assert.That(exposed.Count, Is.EqualTo(4), "The returned list is a snapshot, not mutable owner state.");
        Assert.That(session.TrySetPauseReason(reasons.Last(), false), Is.True);
        Assert.That(session.IsPaused, Is.False);
        Assert.That(session.TrySetPauseReason((PopupStorePhasePauseReason)999, true), Is.False);
        Assert.That(session.TrySetPauseReason((PopupStorePhasePauseReason)999, false), Is.False);
        Assert.That(session.CanAcceptPacking, Is.True);
    }

    [TestCase(PopupStorePhaseState.Clearing)]
    [TestCase(PopupStorePhaseState.ClearNotice)]
    [TestCase(PopupStorePhaseState.Countdown)]
    public void PauseFreezesEveryTransitionStateAndCleanupCanBeRetried(PopupStorePhaseState state)
    {
        var session = At(state);
        string before = Fingerprint(session);
        var signal = Clean(session);
        Assert.That(session.TrySetPauseReason(PopupStorePhasePauseReason.ViewHidden, true), Is.True);
        Assert.That(session.TryAdvanceTime(1000), Is.False);
        Assert.That(session.TryCompleteCleanup(signal), Is.False);
        Assert.That(Fingerprint(session), Is.EqualTo(before));
        Assert.That(session.TrySetPauseReason(PopupStorePhasePauseReason.ViewHidden, false), Is.True);
        if (state == PopupStorePhaseState.Clearing)
        {
            Assert.That(session.TryCompleteCleanup(signal), Is.True);
            Assert.That(session.State, Is.EqualTo(PopupStorePhaseState.ClearNotice));
        }
        else
        {
            double beforeTransition = session.TransitionRemainingSeconds;
            Assert.That(session.TryAdvanceTime(.125), Is.True);
            Assert.That(session.TransitionRemainingSeconds, Is.EqualTo(beforeTransition - .125));
        }
    }

    [TestCase(PopupStorePhaseState.Playing, -1d)]
    [TestCase(PopupStorePhaseState.Playing, double.NaN)]
    [TestCase(PopupStorePhaseState.Playing, double.PositiveInfinity)]
    [TestCase(PopupStorePhaseState.Playing, double.NegativeInfinity)]
    [TestCase(PopupStorePhaseState.ClearNotice, -1d)]
    [TestCase(PopupStorePhaseState.ClearNotice, double.NaN)]
    [TestCase(PopupStorePhaseState.ClearNotice, double.PositiveInfinity)]
    [TestCase(PopupStorePhaseState.Countdown, -1d)]
    [TestCase(PopupStorePhaseState.Countdown, double.NaN)]
    [TestCase(PopupStorePhaseState.Countdown, double.PositiveInfinity)]
    public void InvalidElapsedSamplesLeaveAllProgressUntouched(PopupStorePhaseState state, double elapsed)
    {
        var session = At(state);
        string before = Fingerprint(session);
        Assert.That(session.TryAdvanceTime(elapsed), Is.False);
        Assert.That(Fingerprint(session), Is.EqualTo(before));
        Assert.That(session.FinalResult, Is.Null);
    }

    [Test]
    public void HugeSamplesShowNoticeThenEveryCountdownDigitAndDoNotSpendNextPhaseTime()
    {
        var session = At(PopupStorePhaseState.Clearing);
        string phaseId = session.PhaseRunId;
        double playRemaining = session.RemainingSeconds;
        int version = session.TransitionCount;
        Assert.That(session.TryAdvanceTime(double.MaxValue), Is.False, "Only a genuine cleanup signal can release Clearing.");
        Assert.That(session.TransitionCount, Is.EqualTo(version));
        Assert.That(session.TryCompleteCleanup(Clean(session)), Is.True);
        Assert.That(session.State, Is.EqualTo(PopupStorePhaseState.ClearNotice));
        Assert.That(session.TryAdvanceTime(double.MaxValue), Is.True);
        Assert.That(session.CountdownValue, Is.EqualTo(3));
        foreach (int next in new[] { 2, 1 })
        {
            Assert.That(session.TryAdvanceTime(double.MaxValue), Is.True);
            Assert.That(session.State, Is.EqualTo(PopupStorePhaseState.Countdown));
            Assert.That(session.CountdownValue, Is.EqualTo(next));
            Assert.That(session.RemainingSeconds, Is.EqualTo(playRemaining));
            Assert.That(session.PhaseRunId, Is.EqualTo(phaseId));
        }
        Assert.That(session.TryAdvanceTime(double.MaxValue), Is.True);
        Assert.That(session.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Assert.That(session.CurrentPhase.Grade, Is.EqualTo(PopupStoreGradeTier.B));
        Assert.That(session.RemainingSeconds, Is.EqualTo(session.CurrentPhase.TimeLimitSeconds));
        Assert.That(session.PhaseSuccessfulOrders, Is.Zero);
        Assert.That(session.PhaseRunId, Is.Not.EqualTo(phaseId));
        Assert.That(session.FinalizationCount, Is.Zero);
    }

    [Test]
    public void ZeroDurationNoticeStillNeedsExplicitTransitionAndDoesNotSkipThree()
    {
        var profile = PopupStorePhaseTestProfiles.CreateDefinitionProfile();
        profile.Phases[0].StageClearNoticeSeconds = 0;
        Assert.That(profile.TryCreateSnapshot(Catalog(), out var snapshot, out string error), Is.True, error);
        Assert.That(PopupStorePhaseSession.TryCreate("TEST_ONLY-zero-notice", snapshot, out var session, out error), Is.True, error);
        Assert.That(session.TryStart(), Is.True);
        CompleteGoal(session);
        Assert.That(session.TryCompleteCleanup(Clean(session)), Is.True);
        Assert.That(session.State, Is.EqualTo(PopupStorePhaseState.ClearNotice));
        Assert.That(session.CountdownValue, Is.Zero);
        Assert.That(session.TryAdvanceTime(0), Is.True);
        Assert.That(session.State, Is.EqualTo(PopupStorePhaseState.Countdown));
        Assert.That(session.CountdownValue, Is.EqualTo(3));
        Assert.That(session.TransitionRemainingSeconds, Is.EqualTo(snapshot.Phases[0].CountdownStepSeconds));
    }

    [Test]
    public void TimeSettledToZeroBeforePackingCannotBecomeACompletedPhase()
    {
        var session = Started();
        for (int i = 1; i < session.CurrentPhase.TargetSuccessfulOrders; i++)
            Assert.That(session.TryRecordPacking(Evidence(session, "early-" + i)), Is.True);
        var delayed = Evidence(session, "last");
        Assert.That(session.TryAdvanceTime(session.RemainingSeconds), Is.True);
        Assert.That(session.RemainingSeconds, Is.Zero);
        Assert.That(session.PendingEndReason, Is.EqualTo(PopupStorePhaseEndReason.TimedOut));
        Assert.That(session.TryRecordPacking(delayed), Is.False);
        Assert.That(session.TryAbandon(), Is.False, "The accepted timeout must not be relabelled by a late abandon.");
        Assert.That(session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
        Assert.That(session.TryCompleteCleanup(Clean(session)), Is.True);
        Assert.That(session.State, Is.EqualTo(PopupStorePhaseState.TimedOut));
        Assert.That(session.FinalResult.PhaseSummaries.Single().UsedSeconds, Is.EqualTo(session.CurrentPhase.TimeLimitSeconds));
        Assert.That(session.FinalResult.PhaseSummaries.Single().Cleared, Is.False);
    }

    [Test]
    public void PositiveTimeGoalCommittedFirstCannotBeCancelledByLateTimeSample()
    {
        var session = Started();
        Assert.That(session.TryAdvanceTime(session.RemainingSeconds - .125), Is.True);
        CompleteGoal(session);
        double remaining = session.RemainingSeconds;
        Assert.That(remaining, Is.EqualTo(.125));
        Assert.That(session.TryAdvanceTime(double.MaxValue), Is.False);
        Assert.That(session.RemainingSeconds, Is.EqualTo(remaining));
        Assert.That(session.PendingEndReason, Is.Null);
        Assert.That(session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
        Assert.That(session.FinalResult, Is.Null);
        Assert.That(session.TryCompleteCleanup(Clean(session)), Is.True);
        Assert.That(session.State, Is.EqualTo(PopupStorePhaseState.ClearNotice));
    }

    [Test]
    public void TimeAfterClearingAndPresentationIsExcludedFromPerPhasePlaySummary()
    {
        var session = Started();
        Assert.That(session.TryAdvanceTime(2.25), Is.True);
        CompleteGoal(session);
        Assert.That(session.TryAdvanceTime(999), Is.False);
        MoveToNextPhase(session);
        Assert.That(session.TryAdvanceTime(3.5), Is.True);
        Assert.That(session.TryAbandon(), Is.True);
        Assert.That(session.TryCompleteCleanup(Clean(session)), Is.True);
        var result = session.FinalResult;
        Assert.That(result.PhaseSummaries.Count, Is.EqualTo(2));
        Assert.That(result.PhaseSummaries[0].UsedSeconds, Is.EqualTo(2.25));
        Assert.That(result.PhaseSummaries[1].UsedSeconds, Is.EqualTo(3.5));
        Assert.That(result.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
        Assert.That(result.EndReason, Is.EqualTo(PopupStorePhaseEndReason.Abandoned));
    }

    [Test]
    public void PriorReceiptAndOrderIdsRemainSpentAfterChangingPhaseToken()
    {
        var session = Started();
        var accepted = Evidence(session, "unique");
        Assert.That(session.TryRecordPacking(accepted), Is.True);
        CompleteGoal(session);
        MoveToNextPhase(session);
        var sameReceipt = new PopupStorePhasePackingEvidence(session.BusinessSessionId, session.PhaseRunId, "new-order", accepted.ReceiptId);
        var sameOrder = new PopupStorePhasePackingEvidence(session.BusinessSessionId, session.PhaseRunId, accepted.OrderId, "new-receipt");
        Assert.That(session.TryRecordPacking(sameReceipt), Is.False);
        Assert.That(session.TryRecordPacking(sameOrder), Is.False);
        Assert.That(session.TryRecordPacking(accepted), Is.False);
        Assert.That(session.PhaseSuccessfulOrders, Is.Zero);
        Assert.That(session.TryRecordPacking(new PopupStorePhasePackingEvidence(session.BusinessSessionId, session.PhaseRunId, "new-order", "new-receipt")), Is.True);
    }

    [Test]
    public void SameBusinessIdInSeparateModelInstancesDoesNotAcceptOtherInstancePhaseToken()
    {
        var first = Started();
        var second = Started();
        Assert.That(first.BusinessSessionId, Is.EqualTo(second.BusinessSessionId));
        Assert.That(first.PhaseRunId, Is.Not.EqualTo(second.PhaseRunId));
        Assert.That(second.TryRecordPacking(Evidence(first, "foreign-instance")), Is.False);
        Assert.That(second.TotalSuccessfulOrders, Is.Zero);
        Assert.That(first.TryAbandon(), Is.True);
        Assert.That(second.State, Is.EqualTo(PopupStorePhaseState.Playing));
    }

    [Test]
    public void FinalSummaryAndCollectionsRemainImmutableAfterRepeatedCommandsAndReads()
    {
        var session = Started();
        CompleteGoal(session);
        MoveToNextPhase(session);
        Assert.That(session.TryAdvanceTime(double.MaxValue), Is.True);
        Assert.That(session.TryCompleteCleanup(Clean(session)), Is.True);
        var result = session.FinalResult;
        string before = Fingerprint(session);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)result.ClearedPhases).Add(6));
        Assert.Throws<NotSupportedException>(() => ((IList<PopupStorePhaseProgressSummary>)result.PhaseSummaries).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<int>)session.ClearedPhases).Clear());
        for (int i = 0; i < 10; i++)
        {
            Assert.That(session.TryStart(), Is.False);
            Assert.That(session.TryAdvanceTime(1), Is.False);
            Assert.That(session.TryRecordPacking(Evidence(session, "late" + i)), Is.False);
            Assert.That(session.TryCompleteCleanup(Clean(session)), Is.False);
            Assert.That(session.TryAbandon(), Is.False);
            Assert.That(session.TrySetPauseReason(PopupStorePhasePauseReason.FocusLost, true), Is.False);
            Assert.That(session.FinalResult, Is.SameAs(result));
            Assert.That(Fingerprint(session), Is.EqualTo(before));
        }
        Assert.That(result.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
        Assert.That(result.FinalizationCount, Is.EqualTo(1));
        Assert.That(typeof(PopupStorePhaseResult).GetProperties().All(p => p.SetMethod == null), Is.True);
        Assert.That(typeof(PopupStorePhaseProgressSummary).GetProperties().All(p => p.SetMethod == null), Is.True);
    }

    [Test]
    public void OriginalPrefabAndSceneDependenciesHaveNoPhaseCoreBinding()
    {
        var paths = new[] { "Assets/Resources/UI/PopupStore/UIPopupStore.prefab", "Assets/Scenes/Stage1.unity", "Assets/Scenes/FirstLoadingScene.unity" };
        var before = paths.Select(File.ReadAllBytes).ToArray();
        foreach (string path in paths)
        {
            Assert.That(AssetDatabase.GetDependencies(path, true).Any(dependency => dependency.Contains("PopupStorePhase")), Is.False, path);
        }
        var session = Started();
        CompleteGoal(session);
        MoveToNextPhase(session);
        Assert.That(session.TryAbandon(), Is.True);
        Assert.That(session.TryCompleteCleanup(Clean(session)), Is.True);
        for (int i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]), paths[i]);
        Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(typeof(PopupStorePhaseSession)), Is.False);
        Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(typeof(PopupStorePhaseProfile)), Is.False);
        Assert.That(typeof(PopupStorePhaseSession).GetEvents().Length, Is.Zero, "No implicit live-game subscription or callback chain.");
    }

    [Test]
    public void NewRuntimeCoreHasNoEditorClockRewardAccountOrAutomaticInitializationCalls()
    {
        foreach (string file in new[] { "PopupStorePhaseDefinition", "PopupStorePhaseProfile", "PopupStorePhaseSession", "PopupStorePhaseResult" })
        {
            string source = File.ReadAllText("Assets/Scripts/UI/Floor3UI/" + file + ".cs");
            source = Regex.Replace(source, @"/\*[\s\S]*?\*/|//[^\r\n]*", "");
            Assert.That(Regex.IsMatch(source, @"\b(UnityEditor|MonoBehaviour|ScriptableObject|RuntimeInitializeOnLoadMethod|InitializeOnLoad|PlayerPrefs|Backend|BackEnd|UserInfo|PopupStoreRewardSession|PopupStoreGradeSession|PopupStoreScore|PopupStoreAdmissionHost|PopupStoreBusinessClock)\b|\bTime\s*\.|\bRandom\s*\."), Is.False, file);
        }
    }

    private static PopupStoreMaterialCatalog Catalog()
        => AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>("Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset");
    private static PopupStorePhaseSession Started()
    {
        Assert.That(PopupStorePhaseSession.TryCreate("TEST_ONLY-boundary-business", PopupStorePhaseTestProfiles.CreateSnapshot(), out var session, out string error), Is.True, error);
        Assert.That(session.TryStart(), Is.True);
        return session;
    }
    private static PopupStorePhaseSession At(PopupStorePhaseState state)
    {
        var session = Started();
        if (state == PopupStorePhaseState.Playing) return session;
        CompleteGoal(session);
        if (state == PopupStorePhaseState.Clearing) return session;
        Assert.That(session.TryCompleteCleanup(Clean(session)), Is.True);
        if (state == PopupStorePhaseState.ClearNotice) return session;
        Assert.That(session.TryAdvanceTime(session.CurrentPhase.StageClearNoticeSeconds), Is.True);
        Assert.That(session.State, Is.EqualTo(PopupStorePhaseState.Countdown));
        return session;
    }
    private static void CompleteGoal(PopupStorePhaseSession session)
    {
        while (session.State == PopupStorePhaseState.Playing)
            Assert.That(session.TryRecordPacking(Evidence(session, "goal-" + session.PhaseGeneration + "-" + session.PhaseSuccessfulOrders)), Is.True);
        Assert.That(session.State, Is.EqualTo(PopupStorePhaseState.Clearing));
    }
    private static void MoveToNextPhase(PopupStorePhaseSession session)
    {
        Assert.That(session.TryCompleteCleanup(Clean(session)), Is.True);
        for (int i = 0; i < 4; i++) Assert.That(session.TryAdvanceTime(9999), Is.True);
        Assert.That(session.State, Is.EqualTo(PopupStorePhaseState.Playing));
    }
    private static PopupStorePhasePackingEvidence Evidence(PopupStorePhaseSession session, string key)
        => new PopupStorePhasePackingEvidence(session.BusinessSessionId, session.PhaseRunId, "TEST_ONLY-order-" + key, "TEST_ONLY-receipt-" + key);
    private static PopupStorePhaseCleanupSignal Clean(PopupStorePhaseSession session)
        => new PopupStorePhaseCleanupSignal(session.BusinessSessionId, session.PhaseRunId, PopupStorePhaseCleanupStatus.Completed);
    private static string Fingerprint(PopupStorePhaseSession session)
        => string.Join("|", session.State, session.PhaseRunId, session.PhaseGeneration,
            session.RemainingSeconds.ToString("R"), session.TransitionRemainingSeconds.ToString("R"), session.CountdownValue,
            session.PhaseSuccessfulOrders, session.TotalSuccessfulOrders, session.HighestClearedGrade,
            session.FinalizationCount, session.TransitionCount, string.Join(",", session.ClearedPhases));
}
