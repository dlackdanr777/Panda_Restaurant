#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Local preview policy tests use the real phase core and approved table, never an account or grant.</summary>
public sealed class PopupStorePhaseRewardRollTests
{
    private const string R1Path = "Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R1.asset";
    private const string R0Path = "Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R0.asset";
    private PopupStoreRewardTable _table;

    [SetUp]
    public void SetUp()
    {
        var asset = AssetDatabase.LoadAssetAtPath<PopupStoreRewardTable>(R1Path);
        Assert.That(asset, Is.Not.Null);
        _table = UnityEngine.Object.Instantiate(asset);
    }

    [TearDown]
    public void TearDown()
    {
        if (_table != null) UnityEngine.Object.DestroyImmediate(_table);
    }

    [Test]
    public void MissingPhaseOwnerCannotCreateARewardOwner()
    {
        Assert.That(PopupStorePhaseRewardRoll.TryCreate(null, _table, out var roll, out var error), Is.False);
        Assert.That(roll, Is.Null); Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void MissingRewardTableCannotSupplyAFallbackPolicy()
    {
        Assert.That(PopupStorePhaseRewardRoll.TryCreate(Owner(), null, out var roll, out var error), Is.False);
        Assert.That(roll, Is.Null); Assert.That(error, Is.Not.Empty);
    }

    [TestCase("R0")]
    [TestCase("policy")]
    [TestCase("missingRows")]
    [TestCase("amount")]
    public void FactoryRejectsLegacyOrUnapprovedTableData(string defect)
    {
        var table = _table;
        if (defect == "R0") table = AssetDatabase.LoadAssetAtPath<PopupStoreRewardTable>(R0Path);
        else if (defect == "policy") Set(_table, "_policyId", "not-the-approved-policy");
        else if (defect == "missingRows") Set(_table, "_entries", Array.Empty<PopupStoreRewardEntry>());
        else
        {
            Assert.That(_table.TryGetEntries(out var entries, out _), Is.True);
            entries[0] = new PopupStoreRewardEntry(PopupStoreGradeTier.C, 2, 3, 5, 900, 97, 3);
            Set(_table, "_entries", entries);
        }
        Assert.That(PopupStorePhaseRewardRoll.TryCreate(Owner(), table, out var roll, out var error), Is.False);
        Assert.That(roll, Is.Null); Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void CreatingTheFrozenPolicyDoesNotDrawFinalizeOrStartTheBusiness()
    {
        var owner = Owner(); var roll = Create(owner); var random = new Source(_ => 0);
        Assert.That(roll.PolicySnapshot.PolicyId, Is.EqualTo(PopupStoreRewardTable.R1PolicyId));
        Assert.That(roll.PolicySnapshot.Denominator, Is.EqualTo(1000));
        Assert.That(roll.TrySetRandomSource(null), Is.False);
        Assert.That(roll.TrySetRandomSource(random), Is.True);
        Assert.That(owner.State, Is.EqualTo(PopupStorePhaseState.Ready));
        Assert.That(owner.FinalizationCount, Is.Zero); Assert.That(owner.TotalSuccessfulOrders, Is.Zero);
        Assert.That(random.Calls, Is.Zero); Assert.That(roll.RandomDrawCount, Is.Zero);
        Assert.That(roll.DecisionCount, Is.Zero); Assert.That(roll.Decision, Is.Null);
    }

    [Test]
    public void SnapshotOutlivesAssetEditsDestructionAndReturnedArrayEdits()
    {
        var owner = Owner(); var roll = Create(owner);
        Assert.That(roll.PolicySnapshot.TryGetEntries(out var detached, out _), Is.True);
        detached[0] = new PopupStoreRewardEntry(PopupStoreGradeTier.C, 999, 999, 999, 900, 97, 3);
        Set(_table, "_entries", detached); Set(_table, "_policyId", "edited-later");
        UnityEngine.Object.DestroyImmediate(_table); _table = null;
        Finish(owner, PopupStoreGradeTier.C);
        Assert.That(roll.TrySetRandomSource(new Source(_ => 0)), Is.True);
        Assert.That(roll.TryDecide(owner.FinalResult), Is.True, roll.LastError);
        Assert.That(roll.Decision.Diamonds, Is.EqualTo(1));
        Assert.That(roll.Decision.PolicyId, Is.EqualTo(PopupStoreRewardTable.R1PolicyId));
    }

    [Test]
    public void ReadyPlayingAndVisualCleanupDoNotPrematurelyDraw()
    {
        var owner = Owner(); var roll = Create(owner); var source = new Source(_ => 0);
        roll.TrySetRandomSource(source);
        Assert.That(roll.TryDecide(owner.FinalResult), Is.False);
        Assert.That(owner.TryStart(), Is.True);
        Assert.That(roll.TryDecide(owner.FinalResult), Is.False);
        ReachGoalAndDeadline(owner);
        Assert.That(owner.State, Is.EqualTo(PopupStorePhaseState.Clearing));
        Assert.That(roll.TryDecide(owner.FinalResult), Is.False);
        Assert.That(source.Calls, Is.Zero); Assert.That(roll.Decision, Is.Null);
        Cleanup(owner); AdvanceNoticeAndCountdown(owner);
        Timeout(owner);
        Assert.That(roll.TryDecide(owner.FinalResult), Is.True, roll.LastError);
        Assert.That(roll.RandomDrawCount, Is.EqualTo(1));
    }

    [Test]
    public void AnotherOwnersFinalResultIsRejectedEvenWhenSessionIdAndFactsMatch()
    {
        var owner = Owner("same-business-id"); var other = Owner("same-business-id");
        Finish(owner, PopupStoreGradeTier.C); Finish(other, PopupStoreGradeTier.C);
        var roll = Create(owner); var random = new Source(_ => 0); roll.TrySetRandomSource(random);
        Assert.That(roll.TryDecide(other.FinalResult), Is.False);
        Assert.That(random.Calls, Is.Zero);
        Assert.That(roll.TryDecide(owner.FinalResult), Is.True, roll.LastError);
        var decision = roll.Decision;
        Assert.That(roll.TryDecide(other.FinalResult), Is.False);
        Assert.That(roll.Decision, Is.SameAs(decision));
        Assert.That(roll.TryDecide(owner.FinalResult), Is.True);
        Assert.That(random.Calls, Is.EqualTo(1));
    }

    [Test]
    public void FinalizationCounterMustBeExactlyOne()
    {
        var owner = Owner(); Finish(owner, PopupStoreGradeTier.C); var roll = Create(owner);
        roll.TrySetRandomSource(new Source(_ => 0));
        Set(owner, "<FinalizationCount>k__BackingField", 2);
        Assert.That(roll.TryDecide(owner.FinalResult), Is.False);
        Assert.That(roll.RandomDrawCount, Is.Zero); Assert.That(roll.Decision, Is.Null);
        Set(owner, "<FinalizationCount>k__BackingField", 1);
        Assert.That(roll.TryDecide(owner.FinalResult), Is.True, roll.LastError);
    }

    [TestCase(PopupStoreGradeTier.C, 1)]
    [TestCase(PopupStoreGradeTier.B, 3)]
    [TestCase(PopupStoreGradeTier.A, 5)]
    [TestCase(PopupStoreGradeTier.S, 10)]
    [TestCase(PopupStoreGradeTier.SS, 20)]
    [TestCase(PopupStoreGradeTier.SSS, 30)]
    public void HighestClearedGradeOwnsTheR1RowRatherThanTheLastAttempt(PopupStoreGradeTier grade, int expected)
    {
        var owner = Owner(); var roll = Create(owner); var source = new Source(_ => 0);
        roll.TrySetRandomSource(source); Finish(owner, grade);
        Assert.That(owner.FinalResult.HighestClearedGrade, Is.EqualTo(grade));
        Assert.That(owner.FinalResult.EndReason, Is.EqualTo(grade == PopupStoreGradeTier.SSS
            ? PopupStorePhaseEndReason.Completed : PopupStorePhaseEndReason.TimedOut));
        Assert.That(roll.TryDecide(owner.FinalResult), Is.True, roll.LastError);
        Assert.That(source.LastBound, Is.EqualTo(1000)); Assert.That(source.Calls, Is.EqualTo(1));
        Assert.That(roll.Decision.Grade, Is.EqualTo(grade)); Assert.That(roll.Decision.Diamonds, Is.EqualTo(expected));
        Assert.That(roll.Decision.SessionId, Is.EqualTo(owner.BusinessSessionId));
        Assert.That(roll.Decision.IsLocalPreview, Is.True); Assert.That(roll.Decision.DecisionId, Is.Not.Empty);
    }

    [TestCase("None")]
    [TestCase("Abandoned")]
    [TestCase("CleanupFailed")]
    [TestCase("CleanupCancelled")]
    public void NonRewardableTerminalFactsNeverDrawOrEraseProgress(string reason)
    {
        var owner = Owner(); var roll = Create(owner); var source = new Source(_ => 0);
        roll.TrySetRandomSource(source); Assert.That(owner.TryStart(), Is.True);
        if (reason == "None") Timeout(owner);
        else
        {
            ReachGoalAndDeadline(owner); Cleanup(owner); AdvanceNoticeAndCountdown(owner);
            if (reason == "Abandoned")
            { Assert.That(owner.TryAbandon(), Is.True); Cleanup(owner); }
            else
            {
                Assert.That(owner.TryAdvanceTime(owner.RemainingSeconds), Is.True);
                Cleanup(owner, reason == "CleanupFailed" ? PopupStorePhaseCleanupStatus.Failed : PopupStorePhaseCleanupStatus.Cancelled);
            }
        }
        var result = owner.FinalResult; long successful = owner.TotalSuccessfulOrders;
        var highest = owner.HighestClearedGrade;
        for (int i = 0; i < 3; i++) Assert.That(roll.TryDecide(result), Is.False);
        Assert.That(source.Calls, Is.Zero); Assert.That(roll.RandomDrawCount, Is.Zero);
        Assert.That(roll.DecisionCount, Is.Zero); Assert.That(roll.Decision, Is.Null);
        Assert.That(owner.FinalResult, Is.SameAs(result)); Assert.That(owner.FinalizationCount, Is.EqualTo(1));
        Assert.That(owner.TotalSuccessfulOrders, Is.EqualTo(successful)); Assert.That(owner.HighestClearedGrade, Is.EqualTo(highest));
    }

    [Test]
    public void ApprovedPermilleBoundariesReturnOneAlternativeNotCumulativeAmounts()
    {
        int[] values = { 899, 900, 996, 997, 999 };
        int[] amounts = { 30, 50, 50, 300, 300 };
        var kinds = new[] { PopupStoreRewardKind.Basic, PopupStoreRewardKind.Bonus, PopupStoreRewardKind.Bonus,
            PopupStoreRewardKind.Jackpot, PopupStoreRewardKind.Jackpot };
        for (int i = 0; i < values.Length; i++)
        {
            var owner = Owner(); var roll = Create(owner); int value = values[i];
            roll.TrySetRandomSource(new Source(_ => value)); Finish(owner, PopupStoreGradeTier.SSS);
            Assert.That(roll.TryDecide(owner.FinalResult), Is.True, roll.LastError);
            Assert.That(roll.Decision.Diamonds, Is.EqualTo(amounts[i]), "roll=" + value);
            Assert.That(roll.Decision.Kind, Is.EqualTo(kinds[i]));
            Assert.That(roll.RandomDrawCount, Is.EqualTo(1)); Assert.That(roll.DecisionCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void RepeatedQueriesKeepOneDecisionAndEveryOriginalProgressFact()
    {
        var owner = Owner(); Finish(owner, PopupStoreGradeTier.B); var roll = Create(owner);
        var result = owner.FinalResult; int transitions = owner.TransitionCount;
        var random = new Source(_ => 997); Assert.That(roll.TrySetRandomSource(random), Is.True);
        Assert.That(roll.TryDecide(result), Is.True); var decision = roll.Decision;
        for (int i = 0; i < 8; i++) Assert.That(roll.TryDecide(result), Is.True);
        Assert.That(roll.TrySetRandomSource(new Source(_ => 0)), Is.False);
        Assert.That(roll.Decision, Is.SameAs(decision)); Assert.That(roll.Decision.Diamonds, Is.EqualTo(15));
        Assert.That(random.Calls, Is.EqualTo(1)); Assert.That(roll.DecisionCount, Is.EqualTo(1));
        Assert.That(owner.FinalResult, Is.SameAs(result)); Assert.That(owner.FinalizationCount, Is.EqualTo(1));
        Assert.That(owner.TransitionCount, Is.EqualTo(transitions)); Assert.That(roll.LastError, Is.Null);
    }

    [TestCase("negative")]
    [TestCase("upperBound")]
    [TestCase("exception")]
    public void InvalidOrThrowingProviderConsumesItsSingleAttemptWithoutReroll(string failure)
    {
        var owner = Owner(); Finish(owner, PopupStoreGradeTier.C); var roll = Create(owner);
        var source = new Source(_ => failure == "exception" ? throw new InvalidOperationException("provider failed")
            : failure == "negative" ? -1 : 1000);
        Assert.That(roll.TrySetRandomSource(source), Is.True);
        Assert.That(roll.TryDecide(owner.FinalResult), Is.False); string error = roll.LastError;
        Assert.That(error, Is.Not.Empty);
        Assert.That(roll.TrySetRandomSource(new Source(_ => 997)), Is.False);
        for (int i = 0; i < 3; i++) Assert.That(roll.TryDecide(owner.FinalResult), Is.False);
        Assert.That(source.Calls, Is.EqualTo(1)); Assert.That(roll.RandomDrawCount, Is.EqualTo(1));
        Assert.That(roll.DecisionCount, Is.Zero); Assert.That(roll.Decision, Is.Null);
        Assert.That(roll.LastError, Is.EqualTo(error));
    }

    [Test]
    public void ReentrantProviderCannotDrawAgainReplaceRandomOrPublishHalfADecision()
    {
        var owner = Owner(); Finish(owner, PopupStoreGradeTier.S); var roll = Create(owner);
        var source = new Source(_ =>
        {
            Assert.That(roll.RandomDrawCount, Is.EqualTo(1)); Assert.That(roll.Decision, Is.Null);
            Assert.That(roll.TryDecide(owner.FinalResult), Is.False);
            Assert.That(roll.TrySetRandomSource(new Source(__ => 997)), Is.False);
            Assert.That(roll.DecisionCount, Is.Zero);
            return 900;
        });
        Assert.That(roll.TrySetRandomSource(source), Is.True);
        Assert.That(roll.TryDecide(owner.FinalResult), Is.True, roll.LastError);
        Assert.That(source.Calls, Is.EqualTo(1)); Assert.That(roll.DecisionCount, Is.EqualTo(1));
        Assert.That(roll.Decision.Diamonds, Is.EqualTo(20)); Assert.That(roll.LastError, Is.Null);
    }

    [Test]
    public void OwnerInvariantChangedInsideProviderCannotPublishOrRetryAReward()
    {
        var owner = Owner(); Finish(owner, PopupStoreGradeTier.C);
        var foreign = Owner(); Finish(foreign, PopupStoreGradeTier.C);
        var original = owner.FinalResult; var roll = Create(owner);
        var source = new Source(_ =>
        {
            // Deliberate fault injection models a callback crossing the presentation-owner boundary.
            Set(owner, "<FinalResult>k__BackingField", foreign.FinalResult);
            return 997;
        });
        Assert.That(roll.TrySetRandomSource(source), Is.True);
        Assert.That(roll.TryDecide(original), Is.False);
        Assert.That(roll.Decision, Is.Null); Assert.That(roll.DecisionCount, Is.Zero);
        Set(owner, "<FinalResult>k__BackingField", original);
        Assert.That(roll.TryDecide(original), Is.False);
        Assert.That(roll.TrySetRandomSource(new Source(_ => 0)), Is.False);
        Assert.That(source.Calls, Is.EqualTo(1)); Assert.That(roll.RandomDrawCount, Is.EqualTo(1));
        Assert.That(owner.FinalizationCount, Is.EqualTo(1)); Assert.That(owner.FinalResult, Is.SameAs(original));
    }

    [Test]
    public void DefaultLocalRandomDoesNotAdvanceUnityRandomAndBusinessesStayIndependent()
    {
        var first = Owner(); var second = Owner(); Finish(first, PopupStoreGradeTier.C); Finish(second, PopupStoreGradeTier.C);
        var firstRoll = Create(first); var secondRoll = Create(second);
        var unityRandomBefore = JsonUtility.ToJson(UnityEngine.Random.state);
        Assert.That(firstRoll.TryDecide(first.FinalResult), Is.True, firstRoll.LastError);
        Assert.That(secondRoll.Decision, Is.Null); Assert.That(secondRoll.RandomDrawCount, Is.Zero);
        Assert.That(secondRoll.TryDecide(second.FinalResult), Is.True, secondRoll.LastError);
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(unityRandomBefore));
        Assert.That(firstRoll.Decision.DecisionId, Is.Not.EqualTo(secondRoll.Decision.DecisionId));
        Assert.That(firstRoll.Decision.SessionId, Is.Not.EqualTo(secondRoll.Decision.SessionId));
        CollectionAssert.Contains(new[] { 1, 3, 5 }, firstRoll.Decision.Diamonds);
        CollectionAssert.Contains(new[] { 1, 3, 5 }, secondRoll.Decision.Diamonds);
    }

    private PopupStorePhaseRewardRoll Create(PopupStorePhaseSession owner)
    {
        Assert.That(PopupStorePhaseRewardRoll.TryCreate(owner, _table, out var roll, out var error), Is.True, error);
        return roll;
    }

    private static PopupStorePhaseSession Owner(string id = null)
    {
        Assert.That(PopupStorePhaseSession.TryCreate(id ?? "TEST_ONLY-phase-reward-" + Guid.NewGuid().ToString("N"),
            PopupStorePhaseTestProfiles.CreateSnapshot(), PopupStorePhaseCompletionPolicy.Deadline,
            out var session, out var error), Is.True, error);
        return session;
    }

    private static void Finish(PopupStorePhaseSession owner, PopupStoreGradeTier grade)
    {
        Assert.That(owner.TryStart(), Is.True);
        while ((int)owner.CurrentPhase.Grade <= (int)grade)
        {
            ReachGoalAndDeadline(owner); Cleanup(owner);
            if (owner.CurrentPhase.Grade == PopupStoreGradeTier.SSS)
            { Assert.That(owner.TryAdvanceTime(double.MaxValue), Is.True); return; }
            AdvanceNoticeAndCountdown(owner);
        }
        Timeout(owner);
    }

    private static void ReachGoalAndDeadline(PopupStorePhaseSession owner)
    {
        for (int i = 0; i < owner.CurrentPhase.TargetSuccessfulOrders; i++)
        {
            string identity = owner.PhaseRunId + ":" + i;
            Assert.That(owner.TryRecordPacking(new PopupStorePhasePackingEvidence(owner.BusinessSessionId,
                owner.PhaseRunId, "order-" + identity, "receipt-" + identity)), Is.True);
        }
        Assert.That(owner.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Assert.That(owner.TryAdvanceTime(owner.RemainingSeconds), Is.True);
    }

    private static void Cleanup(PopupStorePhaseSession owner, PopupStorePhaseCleanupStatus status = PopupStorePhaseCleanupStatus.Completed)
        => Assert.That(owner.TryCompleteCleanup(new PopupStorePhaseCleanupSignal(owner.BusinessSessionId, owner.PhaseRunId, status)), Is.True);

    private static void AdvanceNoticeAndCountdown(PopupStorePhaseSession owner)
    {
        Assert.That(owner.TryAdvanceTime(double.MaxValue), Is.True);
        foreach (int value in new[] { 3, 2, 1 })
        {
            Assert.That(owner.CountdownValue, Is.EqualTo(value));
            Assert.That(owner.TryAdvanceTime(double.MaxValue), Is.True);
        }
        Assert.That(owner.State, Is.EqualTo(PopupStorePhaseState.Playing));
    }

    private static void Timeout(PopupStorePhaseSession owner)
    {
        Assert.That(owner.TryAdvanceTime(owner.RemainingSeconds), Is.True);
        Cleanup(owner);
        Assert.That(owner.State, Is.EqualTo(PopupStorePhaseState.TimedOut));
    }

    private static void Set(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name); field.SetValue(target, value);
    }

    private sealed class Source : IPopupStoreRewardRandomSource
    {
        private readonly Func<int, int> _next;
        public int Calls { get; private set; }
        public int LastBound { get; private set; }
        public Source(Func<int, int> next) { _next = next; }
        public int NextInt(int exclusiveUpperBound)
        { Calls++; LastBound = exclusiveUpperBound; return _next(exclusiveUpperBound); }
    }
}
#endif
