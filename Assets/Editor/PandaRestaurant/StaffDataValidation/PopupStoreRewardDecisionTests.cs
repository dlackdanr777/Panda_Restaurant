using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Muks.BackEnd;
using Muks.MobileUI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Local decisions from authentic finalized sales. No reward payment, saving or Unity random draws.</summary>
public sealed class PopupStoreRewardDecisionTests
{
    private const string TablePath = "Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R0.asset";
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";

    [TestCase(PopupStoreGradeTier.C, 1, 2, 10)]
    [TestCase(PopupStoreGradeTier.B, 2, 3, 12)]
    [TestCase(PopupStoreGradeTier.A, 3, 4, 15)]
    [TestCase(PopupStoreGradeTier.S, 4, 6, 20)]
    [TestCase(PopupStoreGradeTier.SS, 5, 8, 25)]
    [TestCase(PopupStoreGradeTier.SSS, 6, 10, 30)]
    public void ActualFinalGradeUsesItsThreeAlternativeR0AmountsAndOneDrawPerSession(PopupStoreGradeTier grade, int basic, int bonus, int jackpot)
    {
        int[] rolls = { 0, 85, 99 }, amounts = { basic, bonus, jackpot };
        var identities = new HashSet<string>();
        for (int i = 0; i < rolls.Length; i++)
        {
            var source = new Source(rolls[i]);
            using (var host = new Host(source))
            {
                host.Finish(grade);
                var result = host.Session.Result;
                Assert.That(result, Is.Not.Null); Assert.That(result.Grade, Is.EqualTo(grade));
                Assert.That(result.Kind, Is.EqualTo((PopupStoreRewardKind)i)); Assert.That(result.Diamonds, Is.EqualTo(amounts[i]));
                Assert.That(result.Diamonds, Is.Not.EqualTo(basic + bonus + jackpot));
                Assert.That(result.SessionId, Is.EqualTo(host.Grade.FinalResult.SessionId));
                Assert.That(result.IsDecided && result.IsLocalPreview, Is.True); Assert.That(result.DecisionId, Is.Not.Null.And.Not.Empty);
                Assert.That(identities.Add(result.DecisionId), Is.True);
                AssertOneDecision(host, source);
                Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
                host.Show(); Assert.That(host.Box.DisplayedSprite, Is.SameAs(Approved(grade)));
            }
        }
    }

    [TestCase(0, PopupStoreRewardKind.Basic, 1)]
    [TestCase(84, PopupStoreRewardKind.Basic, 1)]
    [TestCase(85, PopupStoreRewardKind.Bonus, 2)]
    [TestCase(98, PopupStoreRewardKind.Bonus, 2)]
    [TestCase(99, PopupStoreRewardKind.Jackpot, 10)]
    public void IntegerR0BoundariesSelectExactlyOneAlternative(int roll, PopupStoreRewardKind expectedKind, int expectedAmount)
    {
        Assert.That(Authored().TryGetReward(PopupStoreGradeTier.C, out var entry, out _), Is.True);
        Assert.That(PopupStoreRewardSelector.TrySelect(entry, roll, out var kind, out int diamonds, out string error), Is.True, error);
        Assert.That(kind, Is.EqualTo(expectedKind)); Assert.That(diamonds, Is.EqualTo(expectedAmount));
    }

    [TestCase(-1)]
    [TestCase(100)]
    public void SelectorRejectsValuesOutsideTheHalfOpenRangeWithoutAnAmount(int roll)
    {
        Assert.That(Authored().TryGetReward(PopupStoreGradeTier.C, out var entry, out _), Is.True);
        Assert.That(PopupStoreRewardSelector.TrySelect(entry, roll, out _, out int diamonds, out string error), Is.False);
        Assert.That(diamonds, Is.Zero); Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void AllOneHundredInputsMatchTheValidatedWeightsIncludingEditedAndZeroWeightRows()
    {
        Assert.That(Authored().TryGetReward(PopupStoreGradeTier.C, out var r0, out _), Is.True);
        foreach (var row in new[] { r0,
            new PopupStoreRewardEntry(PopupStoreGradeTier.C, 2, 5, 20, 50, 30, 20),
            new PopupStoreRewardEntry(PopupStoreGradeTier.C, 2, 5, 20, 0, 0, 100),
            new PopupStoreRewardEntry(PopupStoreGradeTier.C, 2, 5, 20, 0, 100, 0) })
        {
            int[] counts = new int[3], amounts = { row.BasicDiamonds, row.BonusDiamonds, row.JackpotDiamonds };
            for (int roll = 0; roll < 100; roll++)
            {
                Assert.That(PopupStoreRewardSelector.TrySelect(row, roll, out var kind, out int diamonds, out string error), Is.True, error);
                counts[(int)kind]++; Assert.That(diamonds, Is.EqualTo(amounts[(int)kind]));
            }
            CollectionAssert.AreEqual(new[] { row.BasicWeight, row.BonusWeight, row.JackpotWeight }, counts);
        }
    }

    [Test]
    public void PureSelectorCannotBypassRewardTableValidation()
    {
        var invalid = new PopupStoreRewardEntry(PopupStoreGradeTier.C, 1, 2, 10, int.MaxValue, int.MaxValue, 102);
        Assert.That(PopupStoreRewardSelector.TrySelect(invalid, 0, out _, out int diamonds, out string error), Is.False);
        Assert.That(diamonds, Is.Zero); Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void NoneResolvesOnceWithoutATableRandomCallOrDecisionEvenAfterReentry()
    {
        var source = new Source(99);
        using (var host = new Host(source))
        {
            Write(host.Session, "_table", null); host.Finish(PopupStoreGradeTier.None); host.Show();
            Assert.That(host.Session.IsResolved, Is.True); Assert.That(host.Session.Result, Is.Null);
            Assert.That(host.Session.ResolvedSessionId, Is.EqualTo(host.Grade.SessionId));
            for (int i = 0; i < 3; i++) { host.PublishFinalized(); Assert.That(host.Session.TryDecide(), Is.True); }
            host.Close(); host.Reopen(); host.Show();
            Assert.That(host.Session.Result, Is.Null); Assert.That(host.Session.DecisionCount, Is.Zero);
            Assert.That(host.Session.RandomDrawCount, Is.Zero); Assert.That(source.Calls, Is.Zero);
            Assert.That(host.Box.IsVisible, Is.False); Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
            Assert.That(host.Session.TrySetRandomSource(new Source(0)), Is.False);
            host.Confirm();
        }
    }

    [TestCase(PopupStoreBusinessState.NotStarted)]
    [TestCase(PopupStoreBusinessState.Running)]
    [TestCase(PopupStoreBusinessState.Paused)]
    public void UnfinalizedBusinessCannotDrawOrCreateAReward(PopupStoreBusinessState state)
    {
        var source = new Source(0);
        using (var host = new Host(source))
        {
            if (state != PopupStoreBusinessState.NotStarted) host.Call("Start", 90d);
            if (state == PopupStoreBusinessState.Paused) host.Clock.SetManualPaused(true);
            Assert.That(host.Session.Refresh(), Is.True);
            ExpectError(); Assert.That(host.Session.TryDecide(), Is.False);
            Assert.That(host.Grade.FinalResult, Is.Null); Assert.That(host.Session.Result, Is.Null);
            Assert.That(host.Session.IsResolved, Is.False); Assert.That(source.Calls, Is.Zero);
            Assert.That(host.Clock.State, Is.EqualTo(state)); Assert.That(host.Grade.FinalizationCount, Is.Zero);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingOrInvalidTableConsumesNoRandomAndCanBeCorrectedWithoutBreakingTheResult(bool missing)
    {
        var source = new Source(85);
        using (var table = new TableScope())
        using (var host = new Host(source))
        {
            table.SetC(new PopupStoreRewardEntry(PopupStoreGradeTier.C, 1, 2, 10, 85, 14, 0));
            Write(host.Session, "_table", missing ? null : table.Table);
            ExpectError(); host.Finish(PopupStoreGradeTier.C);
            Assert.That(host.Session.Result, Is.Null); Assert.That(source.Calls, Is.Zero); Assert.That(host.Session.RandomDrawCount, Is.Zero);
            host.Show(); Assert.That(host.Result.IsVisible, Is.True); Assert.That(host.Box.DisplayedSprite, Is.SameAs(Approved(PopupStoreGradeTier.C)));
            var snapshot = new Transactions(host);
            Write(host.Session, "_table", Authored()); Assert.That(host.Session.TryDecide(), Is.True);
            AssertOneDecision(host, source); Assert.That(host.Session.Result.Diamonds, Is.EqualTo(2));
            snapshot.AssertUnchanged(host); host.Confirm();
        }
    }

    [TestCase(-1)]
    [TestCase(100)]
    public void InvalidRandomValueLatchesOneAttemptAndNeverRerollsAfterRefreshOrReentry(int roll)
    {
        var source = new Source(roll);
        using (var host = new Host(source))
        {
            ExpectError(); host.Finish(PopupStoreGradeTier.C);
            AssertFailedAttemptCannotRetry(host, source);
            host.Show(); host.Close(); host.Reopen(false); host.Show();
            AssertFailedAttemptCannotRetry(host, source);
            Assert.That(host.Result.IsVisible, Is.True); Assert.That(host.Box.DisplayedSprite, Is.SameAs(Approved(PopupStoreGradeTier.C)));
            host.Confirm();
        }
    }

    [Test]
    public void ThrowingRandomSourceCannotBecomeAFallbackAwardOrARerollOpportunity()
    {
        var source = new Source(() => throw new InvalidOperationException("controlled reward source failure"));
        using (var host = new Host(source))
        {
            ExpectError(); host.Finish(PopupStoreGradeTier.C);
            AssertFailedAttemptCannotRetry(host, source);
            Assert.That(host.Session.LastError, Does.Contain("controlled reward source failure"));
            host.Show(); host.Confirm();
        }
    }

    [Test]
    public void DuplicateFinalEventsQueriesLifecycleAndReturnToFloor3PreserveTheOriginalDecision()
    {
        var witness = GachaCollectionPreviewSceneWitness.Capture(); var source = new Source(85);
        using (var host = new Host(source))
        {
            host.Finish(PopupStoreGradeTier.S); host.Show();
            var decision = host.Session.Result; var snapshot = new Transactions(host); var random = UnityEngine.Random.state;
            for (int i = 0; i < 3; i++)
            {
                host.PublishFinalized(); Assert.That(host.Session.TryDecide(), Is.True); Assert.That(host.Session.TryDecide(host.Grade.FinalResult), Is.True);
                Invoke(host.Session, "OnDisable"); Invoke(host.Session, "OnEnable");
                Assert.That(host.Session.Refresh(), Is.True); Assert.That(host.Box.Present(host.Grade.FinalResult), Is.True);
                Assert.That(host.Session.Result, Is.SameAs(decision));
            }
            host.Confirm(); host.Reopen(); host.Show();
            Assert.That(host.Session.Result, Is.SameAs(decision)); AssertOneDecision(host, source); snapshot.AssertUnchanged(host);
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random)); host.Account.AssertUnchanged();
            Assert.That(host.Session.TrySetRandomSource(new Source(99)), Is.False);
            Debug.Log("POPUP_STORE_REWARD_DECISION_TEST_EVIDENCE session=" + decision.SessionId + " decision=" + decision.DecisionId
                + " grade=" + decision.Grade + " kind=" + decision.Kind + " diamonds=" + decision.Diamonds
                + " draws=" + source.Calls + " decisions=" + host.Session.DecisionCount + " reentrySame=True accountUnchanged=True localOnly=True");
        }
        witness.AssertUnchanged("popup-store reward decision lifecycle");
    }

    [Test]
    public void DecisionIsImmutableAndPropertyReadsDoNotCallTheProvider()
    {
        var source = new Source(99);
        using (var host = new Host(source))
        {
            host.Finish(PopupStoreGradeTier.C); var decision = host.Session.Result;
            Assert.That(typeof(PopupStoreRewardDecision).IsSealed, Is.True);
            foreach (var property in typeof(PopupStoreRewardDecision).GetProperties(BindingFlags.Instance | BindingFlags.Public))
                Assert.That(property.GetSetMethod(true), Is.Null, property.Name);
            Assert.That(typeof(PopupStoreRewardDecision).GetFields(BindingFlags.Instance | BindingFlags.Public), Is.Empty);
            for (int i = 0; i < 32; i++)
            {
                Assert.That(host.Session.Result, Is.SameAs(decision)); Assert.That(decision.Diamonds, Is.EqualTo(10));
                Assert.That(decision.Kind, Is.EqualTo(PopupStoreRewardKind.Jackpot)); Assert.That(decision.IsLocalPreview, Is.True);
            }
            AssertOneDecision(host, source);
        }
    }

    [Test]
    public void AnotherPopupsAuthenticFinalResultCannotReplaceThisPopupsDecision()
    {
        var source = new Source(0);
        using (var host = new Host(source))
        using (var other = new Host(new Source(99)))
        {
            host.Finish(PopupStoreGradeTier.C); other.Finish(PopupStoreGradeTier.S);
            var decision = host.Session.Result;
            ExpectError(); Assert.That(host.Session.TryDecide(other.Grade.FinalResult), Is.False);
            Assert.That(host.Session.Result, Is.SameAs(decision)); Assert.That(host.Session.Result, Is.Not.SameAs(other.Session.Result));
            Assert.That(decision.SessionId, Is.Not.EqualTo(other.Session.Result.SessionId)); Assert.That(decision.DecisionId, Is.Not.EqualTo(other.Session.Result.DecisionId));
            Assert.That(host.Session.TryDecide(), Is.True); Assert.That(host.Session.Result, Is.SameAs(decision));
            AssertOneDecision(host, source);
        }
    }

    [Test]
    public void MatchingReferenceWithAnotherActualSessionIdStillCannotDraw()
    {
        var source = new Source(0);
        using (var host = new Host(source))
        using (var other = new Host(new Source(0)))
        {
            Write(host.Session, "_table", null); ExpectError(); host.Finish(PopupStoreGradeTier.C); other.Finish(PopupStoreGradeTier.C);
            var original = host.Grade.FinalResult;
            try
            {
                // This is an authentic foreign result, not a fabricated score or receipt.
                Write(host.Grade, "<FinalResult>k__BackingField", other.Grade.FinalResult);
                ExpectError(); Assert.That(host.Session.TryDecide(), Is.False);
                Assert.That(source.Calls, Is.Zero); Assert.That(host.Session.Result, Is.Null);
            }
            finally { Write(host.Grade, "<FinalResult>k__BackingField", original); }
            Write(host.Session, "_table", Authored()); Assert.That(host.Session.TryDecide(), Is.True); AssertOneDecision(host, source);
        }
    }

    [Test]
    public void GradeSourceMustRemainOnItsOriginalPopupRoot()
    {
        var source = new Source(0);
        using (var host = new Host(source))
        using (var other = new Host(new Source(0)))
        {
            Write(host.Session, "_grade", other.Grade);
            ExpectError(); Assert.That(host.Session.Refresh(), Is.False);
            Assert.That(source.Calls, Is.Zero); Assert.That(other.Session.Result, Is.Null);
            Write(host.Session, "_grade", host.Grade); Assert.That(host.Session.Refresh(), Is.True);
            host.Finish(PopupStoreGradeTier.C); AssertOneDecision(host, source);
        }
    }

    [Test]
    public void EditingTheTableToInvalidAfterDecisionCannotChangeItsCachedAmountOrIdentity()
    {
        var source = new Source(99);
        using (var table = new TableScope())
        using (var host = new Host(source))
        {
            Write(host.Session, "_table", table.Table); host.Finish(PopupStoreGradeTier.C); var result = host.Session.Result;
            table.SetC(new PopupStoreRewardEntry(PopupStoreGradeTier.C, 100, 200, 999, 85, 14, 0));
            for (int i = 0; i < 3; i++) { host.PublishFinalized(); Assert.That(host.Session.Refresh(), Is.True); }
            Assert.That(host.Session.Result, Is.SameAs(result)); Assert.That(result.Diamonds, Is.EqualTo(10)); AssertOneDecision(host, source);
        }
    }

    [Test]
    public void ANewPopupUsesAnEditedValidTableWhileThePreviousSessionKeepsItsOwnDecision()
    {
        using (var table = new TableScope())
        using (var first = new Host(new Source(99)))
        {
            Write(first.Session, "_table", table.Table); first.Finish(PopupStoreGradeTier.C); var original = first.Session.Result;
            table.SetC(new PopupStoreRewardEntry(PopupStoreGradeTier.C, 7, 12, 50, 85, 14, 1));
            using (var second = new Host(new Source(99)))
            {
                Write(second.Session, "_table", table.Table); second.Finish(PopupStoreGradeTier.C);
                Assert.That(second.Session.Result.Diamonds, Is.EqualTo(50)); Assert.That(original.Diamonds, Is.EqualTo(10));
                Assert.That(second.Session.Result.SessionId, Is.Not.EqualTo(original.SessionId));
                Assert.That(second.Session.Result.DecisionId, Is.Not.EqualTo(original.DecisionId));
                Assert.That(first.Session.TryDecide(), Is.True); Assert.That(first.Session.Result, Is.SameAs(original));
            }
        }
    }

    [Test]
    public void ProviderReentryCannotDrawTwiceOrReplaceItsDependencyBeforeCommit()
    {
        Host active = null; bool nested = true, replaced = true;
        var source = new Source(() => { nested = active.Session.TryDecide(); replaced = active.Session.TrySetRandomSource(new Source(99)); return 85; });
        using (var host = new Host(source))
        {
            active = host; host.Finish(PopupStoreGradeTier.C);
            Assert.That(nested, Is.False); Assert.That(replaced, Is.False); AssertOneDecision(host, source);
            Assert.That(host.Session.Result.Diamonds, Is.EqualTo(2));
        }
    }

    [Test]
    public void ATableEditInsideTheProviderCannotChangeTheAlreadyValidatedRowSnapshot()
    {
        using (var table = new TableScope())
        {
            var source = new Source(() => { table.SetC(new PopupStoreRewardEntry(PopupStoreGradeTier.C, 7, 12, 999, 100, 0, 0)); return 99; });
            using (var host = new Host(source))
            {
                Write(host.Session, "_table", table.Table); host.Finish(PopupStoreGradeTier.C);
                Assert.That(host.Session.Result.Kind, Is.EqualTo(PopupStoreRewardKind.Jackpot)); Assert.That(host.Session.Result.Diamonds, Is.EqualTo(10));
                AssertOneDecision(host, source);
            }
        }
    }

    [Test]
    public void ChangingTheFinalSourceInsideTheProviderRejectsAndLatchesTheAttempt()
    {
        Host active = null;
        using (var other = new Host(new Source(0)))
        {
            var source = new Source(() => { Write(active.Session, "_grade", other.Grade); return 0; });
            using (var host = new Host(source))
            {
                active = host; ExpectError(); host.Finish(PopupStoreGradeTier.C);
                Assert.That(host.Session.Result, Is.Null); Assert.That(source.Calls, Is.EqualTo(1));
                Write(host.Session, "_grade", host.Grade);
                AssertFailedAttemptCannotRetry(host, source);
                Assert.That(host.Grade.FinalResult.Grade, Is.EqualTo(PopupStoreGradeTier.C)); Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
            }
        }
    }

    [Test]
    public void RemovingOnlyThePopupComponentInsideTheProviderPreventsCommitAndAnyFurtherDraw()
    {
        Host active = null;
        var source = new Source(() => { Object.DestroyImmediate(active.Get<UIPopupStore>("View")); return 0; });
        using (var host = new Host(source))
        {
            active = host; Write(host.Session, "_table", null);
            ExpectError(); host.Finish(PopupStoreGradeTier.C);
            var final = host.Grade.FinalResult;
            Assert.That(source.Calls, Is.Zero); Assert.That(host.Session.RandomDrawCount, Is.Zero);
            // Close through real navigation while its view exists, so ordinary EditMode observers are unbound before disposal.
            host.Close(); Assert.That(host.Root.activeSelf, Is.False); Assert.That(host.Grade.FinalResult, Is.SameAs(final));
            // The hidden observer still owns this final result; remove only its popup during the actual provider call.
            Write(host.Session, "_table", Authored());
            ExpectError(); Assert.That(host.Session.TryDecide(final), Is.False);
            Assert.That(host.Grade.FinalResult, Is.SameAs(final));
            Assert.That(host.Root.GetComponent<UIPopupStore>(), Is.Null);
            Assert.That(host.Grade.FinalResult.Grade, Is.EqualTo(PopupStoreGradeTier.C));
            Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
            Assert.That(host.Session.Result, Is.Null); Assert.That(host.Session.IsResolved, Is.False);
            Assert.That(host.Session.DecisionCount, Is.Zero); Assert.That(host.Session.RandomDrawCount, Is.EqualTo(1));
            // This next rejection identifies the now-missing popup; its message differs from the in-flight invalidation.
            ExpectError(); Assert.That(host.Session.TryDecide(), Is.False);
            for (int i = 0; i < 3; i++) Assert.That(host.Session.Refresh(), Is.False);
            Assert.That(source.Calls, Is.EqualTo(1)); Assert.That(host.Session.RandomDrawCount, Is.EqualTo(1));
            Assert.That(host.Session.Result, Is.Null); Assert.That(host.Session.TrySetRandomSource(new Source(99)), Is.False);
        }
    }

    [Test]
    public void DestroyedPinnedGradeCannotBeReplacedWithANewGradeOnTheSamePopupRoot()
    {
        var source = new Source(0);
        using (var host = new Host(source))
        {
            host.Close(); var original = host.Grade;
            Object.DestroyImmediate(original);
            var replacement = host.Root.AddComponent<PopupStoreGradeSession>();
            Write(host.Session, "_grade", replacement);
            ExpectError(); Assert.That(host.Session.Refresh(), Is.False);
            Assert.That(ReferenceEquals(Read(host.Session, "_ownerGrade"), original), Is.True);
            Assert.That(host.Session.LastError, Does.Contain("cannot be rebound"));
            Assert.That(host.Session.Result, Is.Null); Assert.That(host.Session.IsResolved, Is.False);
            Assert.That(source.Calls, Is.Zero); Assert.That(host.Session.RandomDrawCount, Is.Zero);
            Assert.That(host.Session.DecisionCount, Is.Zero);
        }
    }

    [Test]
    public void TemporarilyDisabledObserverStillResolvesFinalizationAndReenableDoesNotReroll()
    {
        var source = new Source(85);
        using (var host = new Host(source))
        {
            host.Session.enabled = false; Invoke(host.Session, "OnDisable"); host.Finish(PopupStoreGradeTier.C);
            var result = host.Session.Result; AssertOneDecision(host, source);
            host.Session.enabled = true; Invoke(host.Session, "OnEnable"); Assert.That(host.Session.Result, Is.SameAs(result));
            host.Show(); host.Confirm(); Assert.That(source.Calls, Is.EqualTo(1));
        }
    }

    [Test]
    public void DecisionDoesNotConsumeQueuedReceiptsOrSkipDeliveryDepartureAndResultNavigation()
    {
        var source = new Source(0);
        using (var host = new Host(source))
        {
            host.Call("StartCustomers", 3, 90d); var receipt = (PopupStorePackingReceipt)host.Call("Pack", 0);
            host.Call("End"); var snapshot = new Transactions(host);
            AssertOneDecision(host, source); Assert.That(host.Queue.Single(), Is.SameAs(receipt));
            Assert.That(host.Presentation.StartedPresentationCount, Is.Zero);
            host.Result.TickResult(1); Assert.That(host.Result.IsVisible, Is.False); snapshot.AssertUnchanged(host);
            host.Call("CompleteVisual", true); host.Result.TickResult(1); Assert.That(host.Result.IsVisible, Is.False);
            host.Call("FinishDepartures"); host.Show();
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt)); Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Assert.That(host.Get<PopupStoreCustomerEntrance>("Entrance").ReleasedCount, Is.EqualTo(1));
            Assert.That(host.Box.DisplayedSprite, Is.SameAs(Approved(PopupStoreGradeTier.C))); Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
            host.Confirm(); Assert.That(source.Calls, Is.EqualTo(1));
        }
    }

    [Test]
    public void DedicatedDefaultProviderReturnsOnlyValidIntegersWithoutChangingUnityRandomOrAccountState()
    {
        var account = new AccountSnapshot(); var random = UnityEngine.Random.state;
        var source = new PopupStoreRewardRandomSource();
        for (int i = 0; i < 128; i++) Assert.That(source.NextPercent(), Is.InRange(0, 99));
        Assert.That(UnityEngine.Random.state, Is.EqualTo(random)); account.AssertUnchanged();
    }

    [Test]
    public void ActualPrefabBindsOneDecisionObserverToItsExistingGradeAndR0Asset()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var session = prefab.GetComponent<PopupStoreRewardSession>(); Assert.That(session, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<PopupStoreRewardSession>(true), Has.Length.EqualTo(1));
        Assert.That(Read(session, "_grade"), Is.SameAs(prefab.GetComponent<PopupStoreGradeSession>()));
        Assert.That(Read(session, "_table"), Is.SameAs(Authored()));
        Assert.That(prefab.GetComponentsInChildren<PopupStoreRewardBox>(true), Has.Length.EqualTo(1));
        Assert.That(prefab.GetComponentsInChildren<PopupStoreResultPanel>(true), Has.Length.EqualTo(1));
    }

    [Test]
    public void DestroyLifecycleUnsubscribesTheFinalizedObserverWithoutADeferredDraw()
    {
        var source = new Source(0);
        using (var host = new Host(source))
        {
            Invoke(host.Session, "OnDestroy"); Invoke(host.Session, "OnDestroy");
            host.Finish(PopupStoreGradeTier.C);
            Assert.That(source.Calls, Is.Zero); Assert.That(host.Session.Result, Is.Null);
            Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
        }
    }

    private static void AssertOneDecision(Host host, Source source)
    {
        Assert.That(host.Session.IsResolved, Is.True); Assert.That(host.Session.Result, Is.Not.Null);
        Assert.That(host.Session.RandomDrawCount, Is.EqualTo(1)); Assert.That(host.Session.DecisionCount, Is.EqualTo(1));
        Assert.That(source.Calls, Is.EqualTo(1)); Assert.That(host.Session.LastError, Is.Null);
    }
    private static void AssertFailedAttemptCannotRetry(Host host, Source source)
    {
        for (int i = 0; i < 3; i++) { Assert.That(host.Session.TryDecide(), Is.False); Assert.That(host.Session.Refresh(), Is.False); host.PublishFinalized(); }
        Assert.That(host.Session.Result, Is.Null); Assert.That(host.Session.DecisionCount, Is.Zero);
        Assert.That(host.Session.RandomDrawCount, Is.EqualTo(1)); Assert.That(source.Calls, Is.EqualTo(1));
        Assert.That(host.Session.LastError, Is.Not.Null.And.Not.Empty); Assert.That(host.Session.TrySetRandomSource(new Source(0)), Is.False);
    }
    private sealed class Source : IPopupStoreRewardRandomSource
    {
        private readonly Func<int> _next;
        internal int Calls { get; private set; }
        internal Source(int value) : this(() => value) { }
        internal Source(Func<int> next) { _next = next; }
        public int NextPercent() { Calls++; return _next(); }
    }
    private sealed class TableScope : IDisposable
    {
        internal readonly PopupStoreRewardTable Table = ScriptableObject.CreateInstance<PopupStoreRewardTable>();
        private readonly PopupStoreRewardEntry[] _entries;
        internal TableScope() { Assert.That(Authored().TryGetEntries(out _entries, out _), Is.True); Write(Table, "_entries", _entries); }
        internal void SetC(PopupStoreRewardEntry entry) { _entries[0] = entry; }
        public void Dispose() { if (Table != null) Object.DestroyImmediate(Table); }
    }
    // The existing actual-prefab/navigation test host supplies sales and finalization; no synthetic final scores are installed.
    private sealed class Host : IDisposable
    {
        private readonly object _inner;
        internal readonly AccountSnapshot Account = new AccountSnapshot();
        internal Host(Source source)
        {
            var type = typeof(PopupStoreResultTests).GetNestedType("Host", BindingFlags.NonPublic);
            _inner = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, CultureInfo.InvariantCulture);
            try
            {
                Assert.That(Session, Is.Not.Null); Assert.That(Session.TrySetRandomSource(source), Is.True);
                Assert.That(Session.Refresh(), Is.True); // New ordinary MonoBehaviour lifecycle is explicit in this synchronous EditMode host.
            }
            catch { Dispose(); throw; }
        }
        internal GameObject Root => Get<GameObject>("Root");
        internal PopupStoreRewardSession Session => Root.GetComponent<PopupStoreRewardSession>();
        internal PopupStoreGradeSession Grade => Get<PopupStoreGradeSession>("Grade");
        internal PopupStoreResultPanel Result => Get<PopupStoreResultPanel>("Result");
        internal PopupStoreBusinessClock Clock => Get<PopupStoreBusinessClock>("Clock");
        internal PopupStorePacking Packing => Get<PopupStorePacking>("Packing");
        internal PopupStoreScore Score => Get<PopupStoreScore>("Score");
        internal PopupStorePackingPresentation Presentation => Get<PopupStorePackingPresentation>("Presentation");
        internal PopupStoreRewardBox Box => Root.GetComponent<PopupStoreRewardBox>();
        internal Queue<PopupStorePackingReceipt> Queue => (Queue<PopupStorePackingReceipt>)Read(Packing, "_receipts");
        internal T Get<T>(string name) => (T)Read(_inner, name);
        internal void Finish(PopupStoreGradeTier tier)
        { Call("Start", 90d); if (tier != PopupStoreGradeTier.None) Call("SellUntil", tier); Call("End"); Assert.That(Grade.FinalResult.Grade, Is.EqualTo(tier)); }
        internal void Show() => Call("ShowResult");
        internal void Close() { Call("Close"); Invoke(Session, "OnDisable"); }
        internal void Reopen(bool expectSuccess = true)
        { Call("Reopen"); Invoke(Session, "OnEnable"); Assert.That(Session.Refresh(), Is.EqualTo(expectSuccess)); }
        internal void Confirm()
        {
            Assert.That(Result.TryConfirm(), Is.True); Assert.That(Root.activeSelf, Is.False); Call("DisableObservers"); Invoke(Session, "OnDisable");
            Assert.That(Get<MobileUINavigation>("Navigation").FirstView, Is.SameAs(Get<UIFloor3>("Floor")));
            Assert.That(Get<MobileUINavigation>("Navigation").Count, Is.EqualTo(1));
        }
        internal void PublishFinalized() => ((Action<PopupStoreGradeResult>)Read(Grade, "Finalized"))?.Invoke(Grade.FinalResult);
        internal object Call(string name, params object[] args) => CallMethod(_inner, name, args);
        public void Dispose()
        {
            try { if (Session != null) Invoke(Session, "OnDestroy"); ((IDisposable)_inner).Dispose(); }
            finally { Account.AssertUnchanged(); }
        }
    }
    private sealed class Transactions
    {
        private readonly PopupStoreGradeResult _final;
        private readonly int _score, _orders, _packs, _finalizations;
        private readonly PopupStorePackingReceipt[] _committed, _queue;
        private readonly string[] _processed;
        internal Transactions(Host host)
        {
            _final = host.Grade.FinalResult; _score = host.Score.TotalScore; _orders = host.Score.SuccessfulOrderCount;
            _packs = host.Packing.SuccessfulPackCount; _finalizations = host.Grade.FinalizationCount;
            _committed = host.Packing.CommittedReceipts.ToArray(); _queue = host.Queue.ToArray(); _processed = host.Score.ProcessedReceiptIds.ToArray();
        }
        internal void AssertUnchanged(Host host)
        {
            Assert.That(host.Grade.FinalResult, Is.SameAs(_final)); Assert.That(host.Grade.FinalizationCount, Is.EqualTo(_finalizations));
            Assert.That(host.Score.TotalScore, Is.EqualTo(_score)); Assert.That(host.Score.SuccessfulOrderCount, Is.EqualTo(_orders));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(_packs));
            CollectionAssert.AreEqual(_committed, host.Packing.CommittedReceipts.ToArray()); CollectionAssert.AreEqual(_queue, host.Queue.ToArray());
            CollectionAssert.AreEquivalent(_processed, host.Score.ProcessedReceiptIds.ToArray());
        }
    }
    private sealed class AccountSnapshot
    {
        private readonly long _money = UserInfo.Money, _context = UserInfo.CustomerEncounterContextGeneration;
        private readonly int _dia = UserInfo.Dia, _visits = UserInfo.TotalCumulativeCustomerCount;
        private readonly object _score = Field(typeof(UserInfo), "_score").GetValue(null), _manager = Field(typeof(GameManager), "_instance").GetValue(null);
        private readonly BackendManager _backend = BackendManager.ExistingInstance;
        private readonly bool _sdk = BackEnd.Backend.IsInitialized;
        internal void AssertUnchanged()
        {
            Assert.That(UserInfo.Money, Is.EqualTo(_money)); Assert.That(UserInfo.Dia, Is.EqualTo(_dia));
            Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(_visits)); Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(_context));
            Assert.That(Field(typeof(UserInfo), "_score").GetValue(null), Is.EqualTo(_score)); Assert.That(Field(typeof(GameManager), "_instance").GetValue(null), Is.SameAs(_manager));
            Assert.That(BackendManager.ExistingInstance, Is.SameAs(_backend)); Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_sdk));
        }
    }
    private static PopupStoreRewardTable Authored() => AssetDatabase.LoadAssetAtPath<PopupStoreRewardTable>(TablePath);
    private static Sprite Approved(PopupStoreGradeTier grade) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/PopupStore/RewardBoxes/PopupRewardBox_" + grade + "_APPROVED.png");
    private static void ExpectError() => LogAssert.Expect(LogType.Error, new Regex("^PopupStoreRewardSession: local preview not decided\\. "));
    private static void Invoke(object target, string name) => CallMethod(target, name, Array.Empty<object>());
    private static object CallMethod(object target, string name, object[] args)
    {
        try { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }
    private static object Read(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static FieldInfo Field(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }
}
