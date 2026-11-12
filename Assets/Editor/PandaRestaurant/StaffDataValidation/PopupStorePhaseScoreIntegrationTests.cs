#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Host = PopupStorePhasePreviewIntegrationTests.Host;

/// <summary>Real approved prefab, isolated admission, generated orders and committed receipts; no fabricated successful awards.</summary>
public sealed class PopupStorePhaseScoreIntegrationTests
{
    private static readonly int[] CumulativeTargets = { 100, 250, 400, 620, 840, 1060 };
    private GachaCollectionPreviewSceneWitness _workspace;
    private int _diamonds;
    [SetUp] public void Before()
    { _workspace = GachaCollectionPreviewSceneWitness.Capture(); _diamonds = UserInfo.Dia; }
    [TearDown] public void After()
    {
        Assert.That(UserInfo.Dia, Is.EqualTo(_diamonds));
        _workspace.AssertUnchanged("actual phase cumulative-score integration");
    }

    [TestCase(PopupStoreGradeTier.C, 3, 100, 100)]
    [TestCase(PopupStoreGradeTier.B, 4, 150, 250)]
    [TestCase(PopupStoreGradeTier.A, 4, 150, 400)]
    [TestCase(PopupStoreGradeTier.S, 5, 220, 620)]
    [TestCase(PopupStoreGradeTier.SS, 5, 220, 840)]
    [TestCase(PopupStoreGradeTier.SSS, 5, 220, 1060)]
    public void ActualCommittedMaterialLengthUsesFrozenPointsAndCumulativeGoal(
        PopupStoreGradeTier grade, int materials, int award, int total)
    {
        using (var h = ScoreHost())
        {
            Reach(h, grade); h.Ready(); var receipt = h.Pack();
            Assert.That(receipt.Materials.Count, Is.EqualTo(materials));
            Assert.That(h.Score.TryGetEntry(receipt.ReceiptId, out var entry), Is.True);
            Assert.That(entry.Points, Is.EqualTo(award));
            Assert.That(entry.PlushCount, Is.EqualTo(materials - 2));
            Assert.That(entry.OrderId, Is.EqualTo(receipt.OrderId));
            Assert.That(entry.CustomerId, Is.EqualTo(receipt.CustomerId));
            Assert.That(entry.SlotIndex, Is.EqualTo(receipt.SlotIndex));
            AssertLedger(h, total, (int)grade);
            Assert.That(h.Preview.Session.PhaseScore, Is.EqualTo(award));
            Assert.That(h.Preview.GoalText, Does.Contain(total + "점"));
            Assert.That(h.Preview.GoalProgress, Is.EqualTo(1).Within(.0001f));
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Playing));
            Assert.That(h.Preview.Session.HighestClearedGrade, Is.EqualTo((PopupStoreGradeTier)((int)grade - 1)));
            h.AssertNoR1();
        }
    }

    [Test]
    public void ReachingAndExceedingTheScoreDoesNotSpendOrSkipTheRemainingStageTime()
    {
        using (var h = ScoreHost())
        {
            h.Ready(); h.Pack(); h.Deliver(); h.Entrance.TickEntrance(2); h.Pack();
            AssertLedger(h, 200, 2);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(22));
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Playing));
            h.Clock.AdvanceTime(21);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(1));
            Assert.That(h.Preview.Session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
            h.Clock.AdvanceTime(1);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Clearing));
            Assert.That(h.Preview.Session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
            Assert.That(h.Preview.TickCleanup(), Is.False, "The last real receipt is still travelling.");
            h.Deliver(); h.Entrance.TickEntrance(2);
            Assert.That(h.Preview.TickCleanup(), Is.True);
        }
    }

    [Test]
    public void NextStageCarriesTheTotalButResetsOnlyItsOwnScoreAndTimer()
    {
        using (var h = ScoreHost())
        {
            h.Ready(); h.Pack(); h.Deliver(); h.Entrance.TickEntrance(2); h.Pack();
            FinishCurrent(h); double elapsed = h.Clock.ElapsedSeconds;
            h.NextPhase();
            AssertLedger(h, 200, 2);
            Assert.That(h.Preview.Session.PhaseScore, Is.Zero);
            Assert.That(h.Preview.Session.PhaseSuccessfulOrders, Is.Zero);
            Assert.That(h.Preview.Session.CurrentPhase.TargetScore, Is.EqualTo(250));
            Assert.That(h.Preview.GoalProgress, Is.EqualTo(.8f).Within(.0001f));
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(27));
            Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(elapsed), "Announcements cannot spend business time.");
            h.Ready(); h.Pack();
            AssertLedger(h, 350, 3);
            Assert.That(h.Preview.Session.PhaseScore, Is.EqualTo(150));
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Playing));
        }
    }

    [Test]
    public void OneSuccessfulOrderCannotClearAStillUnmetScoreTargetAtDeadline()
    {
        var profile = EditableProfile(); profile.Phases[0].TargetScore = 200;
        using (var h = ScoreHost(Freeze(profile)))
        {
            h.Ready(); h.Pack(); h.Deliver(); h.Entrance.TickEntrance(2);
            Assert.That(h.Preview.Session.PhaseSuccessfulOrders, Is.EqualTo(1));
            Assert.That(h.Preview.Session.CurrentPhase.TargetSuccessfulOrders, Is.EqualTo(1));
            h.Clock.AdvanceTime(22);
            Assert.That(h.Preview.Session.PendingEndReason, Is.EqualTo(PopupStorePhaseEndReason.TimedOut));
            Assert.That(h.Preview.TickCleanup(), Is.True); h.ShowResult();
            Assert.That(h.Preview.Session.FinalResult.TotalScore, Is.EqualTo(100));
            Assert.That(h.Preview.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.None));
            h.AssertNoR1();
        }
    }

    [Test]
    public void ANewReceiptExactlyAtZeroCannotSupplyMissingTargetPoints()
    {
        using (var h = ScoreHost())
        {
            h.Ready(); foreach (var material in h.Entries(0).Reverse()) Assert.That(h.Counter.TryAdd(material), Is.True);
            h.Clock.AdvanceTime(22);
            Assert.That(h.Packing.TryPack().Success, Is.False);
            Assert.That(h.Counter.Count, Is.EqualTo(3));
            AssertLedger(h, 0, 0);
            Assert.That(h.Preview.Session.PendingEndReason, Is.EqualTo(PopupStorePhaseEndReason.TimedOut));
        }
    }

    [Test]
    public void SaleBeforeDeadlineRemainsScoredWhileActualDeliveryFinishesAfterZero()
    {
        using (var h = ScoreHost())
        {
            h.Ready(); h.Clock.AdvanceTime(21.9); var receipt = h.Pack();
            h.Clock.AdvanceTime(.2);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Clearing));
            Assert.That(h.Preview.TickCleanup(), Is.False);
            AssertLedger(h, 100, 1);
            h.Deliver(); h.Entrance.TickEntrance(2); Assert.That(h.Preview.TickCleanup(), Is.True);
            Assert.That(h.Packing.IsCommittedReceipt(receipt), Is.True);
            Assert.That(h.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Assert.That(h.Entrance.EditorReleasedReceipts.Count, Is.EqualTo(1));
            AssertLedger(h, 100, 1);
        }
    }

    [Test]
    public void DuplicateScoreAndPhaseCallbacksCannotAwardTheSameReceiptTwice()
    {
        using (var h = ScoreHost())
        {
            h.Ready(); var receipt = h.Pack();
            for (int i = 0; i < 5; i++)
            {
                Assert.That(h.Score.TryRecordReceipt(receipt), Is.False);
                Assert.That(h.Preview.AcceptCommitted(receipt), Is.False);
                Assert.That(h.Score.Refresh(), Is.True);
            }
            AssertLedger(h, 100, 1);
            Assert.That(h.Preview.AcceptedReceiptCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void PriorStageReceiptCannotAwardAgainAfterThePhaseIdentityChanges()
    {
        using (var h = ScoreHost())
        {
            h.Ready(); var receipt = h.Pack(); string phase = h.Preview.Session.PhaseRunId;
            FinishCurrent(h); h.NextPhase();
            Assert.That(h.Preview.Session.PhaseRunId, Is.Not.EqualTo(phase));
            Assert.That(h.Preview.AcceptCommitted(receipt), Is.False);
            Assert.That(h.Score.TryRecordReceipt(receipt), Is.False);
            AssertLedger(h, 100, 1);
            Assert.That(h.Preview.Session.PhaseScore, Is.Zero);
        }
    }

    [Test]
    public void AnotherIsolatedSessionsAuthenticReceiptIsNotThisSessionsIncome()
    {
        using (var first = ScoreHost()) using (var second = ScoreHost())
        {
            first.Ready(); var receipt = first.Pack();
            Assert.That(first.Preview.Session.BusinessSessionId, Is.Not.EqualTo(second.Preview.Session.BusinessSessionId));
            Assert.That(second.Preview.AcceptCommitted(receipt), Is.False);
            Assert.That(second.Score.TryRecordReceipt(receipt), Is.False);
            AssertLedger(first, 100, 1); AssertLedger(second, 0, 0);
        }
    }

    [TestCase("order")] [TestCase("customer")] [TestCase("slot")] [TestCase("same-fields-new-object")]
    public void ACalculatedButUncommittedIdentityCannotSupplyPoints(string difference)
    {
        using (var h = ScoreHost())
        {
            h.Ready(); var receipt = h.Pack();
            var forged = new PopupStorePackingReceipt(difference == "order" ? "unowned-order" : receipt.OrderId,
                difference == "slot" ? (receipt.SlotIndex + 1) % 3 : receipt.SlotIndex,
                difference == "customer" ? "CUSTOMER04" : receipt.CustomerId,
                receipt.Materials.Select(x => h.Material(x.Id)).ToArray());
            Assert.That(PopupStoreScore.TryCalculate(forged, h.Score.Policy, out _, out int points, out _), Is.True);
            Assert.That(points, Is.EqualTo(100), "Calculation alone is deliberately insufficient authorization.");
            Assert.That(h.Score.TryRecordReceipt(forged), Is.False);
            Assert.That(h.Preview.AcceptCommitted(forged), Is.False);
            AssertLedger(h, 100, 1);
        }
    }

    [TestCase("Points", 101)] [TestCase("OrderId", "different-order")]
    [TestCase("CustomerId", "CUSTOMER11")] [TestCase("SlotIndex", 2)]
    public void CompleteLedgerAuditRejectsCorruptedAwardIdentityWithoutRewritingHistory(string property, object invalid)
    {
        using (var h = ScoreHost())
        {
            h.Ready(); var receipt = h.Pack(); Assert.That(h.Score.TryGetEntry(receipt.ReceiptId, out var entry), Is.True);
            var field = typeof(PopupStoreScoreEntry).GetField("<" + property + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null); field.SetValue(entry, invalid);
            Assert.That(h.Score.TryReadCompleteSnapshot(out int score, out int count, out string error), Is.False);
            Assert.That(error, Does.Contain("does not match")); Assert.That(score, Is.Zero); Assert.That(count, Is.Zero);
            Assert.That(h.Packing.CommittedReceipts.Count, Is.EqualTo(1));
            Assert.That(h.Score.TotalScore, Is.EqualTo(100)); Assert.That(h.Preview.Session.TotalScore, Is.EqualTo(100));
            Assert.That(h.Preview.AcceptCommitted(receipt), Is.False);
        }
    }

    [Test]
    public void RepeatedCompleteAuditUsesTheFrozen150And220PolicyWithoutConsumingDeliveries()
    {
        using (var h = ScoreHost())
        {
            Reach(h, PopupStoreGradeTier.S); h.Ready(); var receipt = h.Pack();
            int completed = h.Presentation.CompletedDeliveryCount;
            for (int i = 0; i < 10; i++) AssertLedger(h, 620, 4);
            Assert.That(h.Presentation.CompletedDeliveryCount, Is.EqualTo(completed));
            h.Deliver();
            Assert.That(h.Presentation.CompletedDeliveryCount, Is.EqualTo(completed + 1));
            Assert.That(h.Packing.IsCommittedReceipt(receipt), Is.True);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void TheScorePolicyCannotBeReboundBeforeOrAfterStartApproval(bool requestStart)
    {
        var policy = Policy();
        using (var h = new Host(requestStart: requestStart, profile: Freeze(EditableProfile()), scoring: policy))
        {
            Assert.That(h.Score.TryBindEditorPhasePolicy(h.Preview, PopupStoreScorePolicySnapshot.Ordinary, out var error), Is.False);
            Assert.That(error, Does.Contain("fresh isolated preview"));
            Assert.That(h.Score.Policy, Is.SameAs(policy));
            Assert.That(h.Score.Policy.FourMaterialPoints, Is.EqualTo(150));
        }
    }

    [Test]
    public void EditingTheSourceProfileCannotChangeAnAlreadyAdmittedTargetOrTimer()
    {
        var editable = EditableProfile(); var snapshot = Freeze(editable);
        using (var h = ScoreHost(snapshot))
        {
            editable.Phases[0].TargetScore = 9999; editable.Phases[0].TimeLimitSeconds = 1;
            editable.Phases[0].AllowedPlushIds[0] = "INVALID"; editable.GoalMetric = PopupStorePhaseGoalMetric.SuccessfulOrders;
            h.Ready(); h.Pack();
            Assert.That(h.Preview.Session.CurrentPhase.TargetScore, Is.EqualTo(100));
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(22));
            Assert.That(h.Preview.Session.Profile.GoalMetric, Is.EqualTo(PopupStorePhaseGoalMetric.CumulativeScore));
            FinishCurrent(h); Assert.That(h.Preview.Session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
        }
    }

    [Test]
    public void AdapterRecoversARealLedgerBeforeAcceptingRegardlessOfObserverSubscriptionOrder()
    {
        using (var h = ScoreHost())
        {
            // Memory EditMode does not dispatch OnDisable on non-ExecuteAlways components.
            typeof(PopupStoreScore).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(h.Score, null);
            h.Ready(); var receipt = h.Pack();
            AssertLedger(h, 100, 1);
            Assert.That(h.Score.TryGetEntry(receipt.ReceiptId, out _), Is.True);
            Assert.That(h.Preview.LastError, Is.Null);
            Assert.That(h.Preview.AcceptedReceiptCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void SynchronousReentrantScoreObserverStillRecordsTheRealReceiptOnce()
    {
        using (var h = ScoreHost())
        {
            int acceptedFromObserver = 0;
            h.Score.Changed += () => { if (h.Preview.AcceptCommitted(h.Packing.LastReceipt)) acceptedFromObserver++; };
            h.Ready(); h.Pack();
            Assert.That(acceptedFromObserver, Is.EqualTo(1));
            AssertLedger(h, 100, 1); Assert.That(h.Preview.AcceptedReceiptCount, Is.EqualTo(1));
            Assert.That(h.Preview.LastError, Is.Null);
        }
    }

    [Test]
    public void LedgerRecoveryWithAReentrantScoreObserverCannotFaultAnAlreadyAcceptedReceipt()
    {
        using (var h = ScoreHost())
        {
            typeof(PopupStoreScore).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(h.Score, null);
            int acceptedFromObserver = 0;
            h.Score.Changed += () => { if (h.Preview.AcceptCommitted(h.Packing.LastReceipt)) acceptedFromObserver++; };
            h.Ready(); var receipt = h.Pack();
            Assert.That(acceptedFromObserver, Is.EqualTo(1));
            AssertLedger(h, 100, 1);
            Assert.That(h.Preview.AcceptedReceiptCount, Is.EqualTo(1));
            Assert.That(h.Preview.LastError, Is.Null);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Playing));
            Assert.That(h.Preview.AcceptCommitted(receipt), Is.False, "A later direct duplicate is still rejected.");
            h.Deliver(); h.Entrance.TickEntrance(2);
            Assert.That(h.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Assert.That(h.Entrance.EditorReleasedReceipts.Count, Is.EqualTo(1));
        }
    }

    [TestCase(PopupStoreGradeTier.C, 100)] [TestCase(PopupStoreGradeTier.B, 140)] [TestCase(PopupStoreGradeTier.S, 180)]
    public void DefaultCalculationAndUnchangedAuthoredPolicyKeepOrdinary100140180(PopupStoreGradeTier grade, int expected)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Host.Prefab);
        Assert.That(prefab.GetComponent<PopupStoreScore>().Policy, Is.SameAs(PopupStoreScorePolicySnapshot.Ordinary));
        using (var h = ScoreHost())
        {
            Reach(h, grade); h.Ready(); var receipt = h.Pack();
            Assert.That(PopupStoreScore.TryCalculate(receipt, out _, out int points, out var error), Is.True, error);
            Assert.That(points, Is.EqualTo(expected));
            Assert.That(h.Score.Policy, Is.Not.SameAs(PopupStoreScorePolicySnapshot.Ordinary));
        }
    }

    [Test]
    public void ExistingDefaultFixtureStillAwardsOrdinaryPolicyFromActualPacking()
    {
        using (var h = new Host())
        {
            Assert.That(h.Score.Policy, Is.SameAs(PopupStoreScorePolicySnapshot.Ordinary));
            h.Ready(); var first = h.Pack();
            Assert.That(h.Score.TryGetEntry(first.ReceiptId, out var one), Is.True);
            Assert.That(one.Points, Is.EqualTo(100));
            FinishCurrent(h); h.NextPhase();
            h.Ready(); var second = h.Pack();
            Assert.That(h.Score.TryGetEntry(second.ReceiptId, out var two), Is.True);
            Assert.That(two.Points, Is.EqualTo(140));
            h.Deliver(); h.Entrance.TickEntrance(2); h.Pack(); FinishCurrent(h); h.NextPhase();
            h.ClearPhase(); h.NextPhase();
            h.Ready(); var last = h.Pack();
            Assert.That(last.Materials.Count, Is.EqualTo(5));
            Assert.That(h.Score.TryGetEntry(last.ReceiptId, out var three), Is.True);
            Assert.That(three.Points, Is.EqualTo(180));
            Assert.That(h.Score.TryReadCompleteSnapshot(out int total, out int count, out var error), Is.True, error);
            Assert.That(total, Is.EqualTo(840)); Assert.That(count, Is.EqualTo(6));
        }
    }

    [Test]
    public void SixActualStagesFreezeOneFinalCumulativeScoreWithoutAnotherOrdinaryResultOrDraw()
    {
        using (var h = ScoreHost())
        {
            string business = h.Preview.Session.BusinessSessionId;
            for (int i = 0; i < 6; i++)
            {
                h.Ready(); h.Pack(); FinishCurrent(h);
                AssertLedger(h, CumulativeTargets[i], i + 1);
                if (i < 5) h.NextPhase();
            }
            h.Clock.AdvanceTime(100); h.ShowResult();
            var final = h.Preview.Session.FinalResult;
            Assert.That(final.EndReason, Is.EqualTo(PopupStorePhaseEndReason.Completed));
            Assert.That(final.TotalScore, Is.EqualTo(1060));
            Assert.That(final.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.SSS));
            Assert.That(h.Preview.Session.BusinessSessionId, Is.EqualTo(business));
            for (int i = 0; i < 4; i++)
            { h.Clock.AdvanceTime(100); h.Score.Refresh(); h.Panel.TickResult(1); }
            Assert.That(h.Preview.Session.FinalResult, Is.SameAs(final));
            Assert.That(final.TotalScore, Is.EqualTo(1060));
            Assert.That(h.Preview.Session.FinalizationCount, Is.EqualTo(1));
            Assert.That(h.Score.Entries.Select(x => x.Points), Is.EqualTo(new[] { 100, 150, 150, 220, 220, 220 }));
            Assert.That(h.Presentation.CompletedDeliveryCount, Is.EqualTo(6));
            Assert.That(h.Entrance.EditorReleasedReceipts.Count, Is.EqualTo(6)); h.AssertNoR1();
        }
    }

    private static Host ScoreHost(PopupStorePhaseProfileSnapshot profile = null)
        => new Host(profile: profile ?? Freeze(EditableProfile()), scoring: Policy());
    private static PopupStoreScorePolicySnapshot Policy()
    {
        Assert.That(PopupStoreScorePolicySnapshot.TryCreate(100, 150, 220, out var result, out var error), Is.True, error);
        return result;
    }
    private static PopupStorePhaseProfile EditableProfile()
    {
        var catalog = Resources.Load<PopupStoreMaterialCatalog>("UI/PopupStore/PopupStoreMaterials");
        Assert.That(PopupStorePhasePreviewSettings.CreateProposalV0().TryCreateSnapshot(catalog, out var legacy, out var error), Is.True, error);
        return new PopupStorePhaseProfile {
            ProfileId = "TEST_ACTUAL_CUMULATIVE_RECEIPTS", Version = 1, GoalMetric = PopupStorePhaseGoalMetric.CumulativeScore,
            Phases = legacy.Phases.Select((p, i) => new PopupStorePhaseDefinition {
                PhaseNumber = p.PhaseNumber, Grade = p.Grade, TimeLimitSeconds = p.TimeLimitSeconds,
                TargetSuccessfulOrders = 1, TargetScore = CumulativeTargets[i], AllowedPlushIds = p.AllowedPlushIds.ToArray(),
                CustomerPatienceSeconds = p.CustomerPatienceSeconds, EntryIntervalSeconds = p.EntryIntervalSeconds,
                StageClearNoticeSeconds = p.StageClearNoticeSeconds, CountdownStepSeconds = p.CountdownStepSeconds
            }).ToArray()
        };
    }
    private static PopupStorePhaseProfileSnapshot Freeze(PopupStorePhaseProfile profile)
    {
        Assert.That(profile.TryCreateSnapshot(Resources.Load<PopupStoreMaterialCatalog>("UI/PopupStore/PopupStoreMaterials"),
            out var snapshot, out var error), Is.True, error); return snapshot;
    }
    private static void Reach(Host h, PopupStoreGradeTier grade)
    {
        while ((int)h.Preview.Session.CurrentPhase.Grade < (int)grade)
        { h.Ready(); h.Pack(); FinishCurrent(h); h.NextPhase(); }
    }
    private static void FinishCurrent(Host h)
    {
        h.Deliver(); h.Entrance.TickEntrance(2);
        Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Playing));
        h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
        Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Clearing));
        Assert.That(h.Preview.TickCleanup(), Is.True, h.Preview.LastError);
        Assert.That(h.Preview.PresentationCue, Is.EqualTo("Success"));
    }
    private static void AssertLedger(Host h, int score, int count)
    {
        Assert.That(h.Score.TryReadCompleteSnapshot(out int audited, out int auditedCount, out var error), Is.True, error);
        Assert.That(audited, Is.EqualTo(score)); Assert.That(auditedCount, Is.EqualTo(count));
        Assert.That(h.Score.TotalScore, Is.EqualTo(score)); Assert.That(h.Score.SuccessfulOrderCount, Is.EqualTo(count));
        Assert.That(h.Preview.Session.TotalScore, Is.EqualTo(score));
        Assert.That(h.Preview.Session.TotalSuccessfulOrders, Is.EqualTo(count));
        Assert.That(h.Score.Entries.Sum(x => x.Points), Is.EqualTo(score));
        Assert.That(h.Packing.CommittedReceipts.Count, Is.EqualTo(count));
    }
}
#endif
