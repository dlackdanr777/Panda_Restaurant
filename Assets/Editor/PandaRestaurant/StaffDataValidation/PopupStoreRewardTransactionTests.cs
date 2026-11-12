#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using Muks.BackEnd;
using NUnit.Framework;
using UnityEngine;

/// <summary>Only fake authority/transport transactions. No actual authentication, SDK request,
/// account grant, disk persistence, scene loading or Play Mode is performed.</summary>
public sealed class PopupStoreRewardTransactionTests
{
    private int _diamonds, _visits;
    private long _money, _encounterGeneration;
    private bool _sdk;
    private BackendManager _backend;
    private UnityEngine.Random.State _random;
    private string _prefabHash;
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";

    [SetUp]
    public void Before()
    {
        _diamonds = UserInfo.Dia; _money = UserInfo.Money; _visits = UserInfo.TotalCumulativeCustomerCount;
        _encounterGeneration = UserInfo.CustomerEncounterContextGeneration;
        _backend = BackendManager.ExistingInstance; _sdk = BackEnd.Backend.IsInitialized;
        _random = UnityEngine.Random.state; _prefabHash = Hash(PrefabPath);
    }
    [TearDown]
    public void After()
    {
        Assert.That(UserInfo.Dia, Is.EqualTo(_diamonds)); Assert.That(UserInfo.Money, Is.EqualTo(_money));
        Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(_visits));
        Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(_encounterGeneration));
        Assert.That(BackendManager.ExistingInstance, Is.SameAs(_backend)); Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_sdk));
        Assert.That(UnityEngine.Random.state, Is.EqualTo(_random)); Assert.That(Hash(PrefabPath), Is.EqualTo(_prefabHash));
    }

    [Test]
    public void NormalGrantAtomicallyCreatesOneLedgerBalanceVersionAndGrantId()
    {
        var h = new Harness(); var tx = h.Transaction();
        Assert.That(tx.TrySubmit(), Is.True); Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Granted));
        Assert.That(tx.Grant.Decision, Is.SameAs(h.Approved)); Assert.That(tx.Grant.GrantId, Is.Not.Empty);
        Assert.That(tx.Grant.BalanceAfter, Is.EqualTo(110)); Assert.That(tx.Grant.BalanceVersion, Is.EqualTo(1));
        Assert.That(tx.ConfirmationCount, Is.EqualTo(1)); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void SameExecutorRepeatedSubmitAndCallbackNeverReenterTheRepositoryOrLocalCompletion()
    {
        var h = new Harness(); int completed = 0; var tx = h.Transaction(g => completed++);
        tx.TrySubmit(); var grant = tx.Grant;
        for (int i = 0; i < 5; i++) { Assert.That(tx.TrySubmit(), Is.True); h.Repository.ReplayLast(); }
        Assert.That(tx.Grant, Is.SameAs(grant)); Assert.That(completed, Is.EqualTo(1));
        Assert.That(h.Repository.SubmitCalls, Is.EqualTo(1)); Assert.That(tx.ConfirmationCount, Is.EqualTo(1)); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void NewExecutorsRequestingTheSameBusinessReceiveTheOriginalImmutableGrant()
    {
        var h = new Harness(); var first = h.Transaction(); first.TrySubmit();
        for (int i = 0; i < 5; i++)
        {
            var tx = h.Transaction(); tx.TrySubmit();
            Assert.That(tx.Grant, Is.SameAs(first.Grant)); Assert.That(tx.Grant.GrantId, Is.EqualTo(first.Grant.GrantId));
        }
        AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void AnotherGrantKeyCannotCreateAnotherEntitlementEvenWhenCallingTheRepositoryDirectly()
    {
        var h = new Harness(); var first = h.Transaction(); first.TrySubmit();
        var changed = Copy(h.Approved, key: "client-generated-alternate-key");
        var tx = h.Transaction(request: changed); Assert.That(tx.TrySubmit(), Is.False);
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.RejectedBeforeSend));
        PopupStoreRewardResponse reply = null; h.Repository.Submit(h.Session, changed, r => reply = r);
        Assert.That(reply.Kind, Is.EqualTo(PopupStoreRewardResponseKind.Conflict)); AssertStore(h, 110, 1, 1);
    }

    [TestCase("amount")]
    [TestCase("grade")]
    [TestCase("decision")]
    [TestCase("kind")]
    [TestCase("policy")]
    public void ContradictoryRequestCannotOverwriteAnAuthorizedDecisionOrExistingGrant(string field)
    {
        var h = new Harness(); var first = h.Transaction(); first.TrySubmit();
        var changed = new PopupStoreRewardRequest("A", "business", field == "decision" ? "different" : "decision",
            field == "grade" ? PopupStoreGradeTier.S : PopupStoreGradeTier.C,
            field == "kind" ? PopupStoreRewardKind.Bonus : PopupStoreRewardKind.Jackpot,
            field == "amount" ? 20 : 10, field == "policy" ? "R1" : "R0");
        var tx = h.Transaction(request: changed); tx.TrySubmit();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Conflict)); Assert.That(tx.Grant, Is.Null);
        Assert.That(h.Store.ReadGrant("A", h.Approved.GrantKey), Is.SameAs(first.Grant)); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void MatchingClientAmountWithoutAnAuthorizedBusinessIsNotAnEntitlement()
    {
        var h = new Harness(); var unregistered = Copy(h.Approved, business: "invented-business");
        var tx = h.Transaction(request: unregistered); tx.TrySubmit();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.RejectedBeforeSend)); AssertStore(h, 100, 0, 0);
    }

    [Test]
    public void AClientConstructedAccountSessionIsNotFakeAuthentication()
    {
        var h = new Harness(); h.Session = new PopupStoreRewardAccountSession("A"); var tx = h.Transaction(); tx.TrySubmit();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.RejectedBeforeSend)); AssertStore(h, 100, 0, 0);
    }

    [Test]
    public void NoneIsAuthoritativelyResolvedWithoutLedgerBalanceVersionOrGrantId()
    {
        var h = new Harness(grade: PopupStoreGradeTier.None, amount: 0); var tx = h.Transaction(); tx.TrySubmit();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.NoReward)); Assert.That(tx.Grant, Is.Null);
        tx.TrySubmit(); h.Repository.ReplayLast(); Assert.That(h.Repository.SubmitCalls, Is.EqualTo(1)); AssertStore(h, 100, 0, 0);
        var restored = PopupStoreRewardRecovery.Restore(PopupStoreRewardRecovery.Capture(tx), h.Repository, () => h.Session);
        Assert.That(restored.TrySubmit(), Is.False); restored.TryReconcile();
        Assert.That(restored.State, Is.EqualTo(PopupStoreRewardTransactionState.NoReward)); AssertStore(h, 100, 0, 0);
    }

    [Test]
    public void ExplicitPreSendRejectionChangesNothingAndCanRetryTheIdenticalRequest()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeSubmitFault.RejectBeforeSend;
        var tx = h.Transaction(); tx.TrySubmit();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.RejectedBeforeSend)); AssertStore(h, 100, 0, 0);
        Assert.That(tx.TrySubmit(), Is.True); Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Granted)); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void CommitThenLostResponseRequiresQueryAndReturnsExactlyTheAlreadyCommittedGrant()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeSubmitFault.CommitThenIndeterminate;
        var tx = h.Transaction(); tx.TrySubmit();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Indeterminate)); AssertStore(h, 110, 1, 1);
        var stored = h.Store.ReadGrant("A", h.Approved.GrantKey); Assert.That(tx.TrySubmit(), Is.False);
        Assert.That(tx.TryReconcile(), Is.True); Assert.That(tx.Grant, Is.SameAs(stored)); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void UnknownWithoutCommitStillBlocksResubmitUntilAuthoritativeNoWriteProof()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeSubmitFault.UnknownWithoutCommit;
        var tx = h.Transaction(); tx.TrySubmit(); Assert.That(tx.TrySubmit(), Is.False); AssertStore(h, 100, 0, 0);
        tx.TryReconcile(); Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.ReconciliationRequired));
        Assert.That(tx.TrySubmit(), Is.False);
        h.Repository.NextQueryFault = PopupStoreFakeQueryFault.ConfirmNotApplied; tx.TryReconcile();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.NotAppliedConfirmed));
        tx.TrySubmit(); Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Granted)); AssertStore(h, 110, 1, 1);
    }

    [TestCase(PopupStoreFakeQueryFault.NotFound)]
    [TestCase(PopupStoreFakeQueryFault.Indeterminate)]
    [TestCase(PopupStoreFakeQueryFault.Throw)]
    public void ATransientQueryProblemNeverProvesAnExistingGrantWasNotApplied(PopupStoreFakeQueryFault fault)
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeSubmitFault.CommitThenIndeterminate;
        var tx = h.Transaction(); tx.TrySubmit(); h.Repository.NextQueryFault = fault; tx.TryReconcile();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.ReconciliationRequired)); Assert.That(tx.TrySubmit(), Is.False);
        AssertStore(h, 110, 1, 1); tx.TryReconcile(); Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Granted));
    }

    [Test]
    public void CompletelyMissingCallbackNeedsExplicitTimeoutAndCanAcceptItsLateSuccessOnce()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeSubmitFault.CommitThenDrop;
        var tx = h.Transaction(); tx.TrySubmit();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Submitting)); Assert.That(tx.TrySubmit(), Is.False);
        tx.MarkResponseMissing(); Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Indeterminate));
        h.Repository.ReplayLast(); h.Repository.ReplayLast();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Granted)); Assert.That(tx.ConfirmationCount, Is.EqualTo(1)); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void OriginalLateCallbackIsIgnoredAfterANewerQueryStarts()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeSubmitFault.CommitThenDefer;
        var tx = h.Transaction(); tx.TrySubmit(); tx.MarkResponseMissing();
        h.Repository.NextQueryFault = PopupStoreFakeQueryFault.Defer; tx.TryReconcile();
        h.Repository.DeliverNext(); Assert.That(tx.Grant, Is.Null); Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.ReconciliationRequired));
        h.Repository.DeliverNext(); Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Granted));
        Assert.That(tx.ConfirmationCount, Is.EqualTo(1)); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void ExceptionAfterAuthorityCommitIsUnknownNotAPreSendFailure()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeSubmitFault.ThrowAfterCommit;
        var tx = h.Transaction(); tx.TrySubmit(); Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Indeterminate));
        Assert.That(tx.TrySubmit(), Is.False); AssertStore(h, 110, 1, 1); tx.TryReconcile();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Granted)); AssertStore(h, 110, 1, 1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AdapterExceptionAfterTerminalCallbackCannotReplaceTheConfirmedOutcomeWithUnknown(bool rejected)
    {
        var h = new Harness(); int completeCalls = 0;
        if (rejected) h.Repository.NextSubmitFault = PopupStoreFakeSubmitFault.RejectBeforeSend;
        h.Repository.ThrowAfterNextDelivery = true;
        var tx = h.Transaction(g => completeCalls++); Assert.That(tx.TrySubmit(), Is.True);
        Assert.That(tx.State, Is.EqualTo(rejected ? PopupStoreRewardTransactionState.RejectedBeforeSend : PopupStoreRewardTransactionState.Granted));
        Assert.That(completeCalls, Is.EqualTo(rejected ? 0 : 1)); Assert.That(tx.ConfirmationCount, Is.EqualTo(rejected ? 0 : 1));
        AssertStore(h, rejected ? 100 : 110, rejected ? 0 : 1, rejected ? 0 : 1);
    }

    [Test]
    public void LocalCompletionFailureCannotUndoTheGrantAndOnlyRetriesTheIdempotentLocalHandler()
    {
        var h = new Harness(); int calls = 0; var applied = new HashSet<string>();
        var tx = h.Transaction(g => { applied.Add(g.GrantId); if (++calls == 1) throw new InvalidOperationException("local view failed"); });
        tx.TrySubmit(); Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Granted)); Assert.That(tx.LocalCompletionPending, Is.True);
        h.Repository.ReplayLast(); Assert.That(calls, Is.EqualTo(1));
        Assert.That(tx.RetryLocalCompletion(), Is.True); Assert.That(tx.LocalCompletionPending, Is.False);
        Assert.That(applied.Count, Is.EqualTo(1)); Assert.That(calls, Is.EqualTo(2)); Assert.That(h.Repository.SubmitCalls, Is.EqualTo(1)); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void LocalCompletionReentryDoesNotSubmitOrRecursivelyComplete()
    {
        var h = new Harness(); int calls = 0; PopupStoreRewardTransaction tx = null;
        tx = h.Transaction(g => { calls++; Assert.That(tx.TrySubmit(), Is.True); Assert.That(tx.RetryLocalCompletion(), Is.False); });
        tx.TrySubmit(); Assert.That(calls, Is.EqualTo(1)); Assert.That(h.Repository.SubmitCalls, Is.EqualTo(1)); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void NewExecutorRestoresCommittedOutcomeFromSharedFakeAuthorityWithoutAnotherSubmission()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeSubmitFault.CommitThenDrop;
        var old = h.Transaction(); old.TrySubmit(); var saved = PopupStoreRewardRecovery.Capture(old); old.Invalidate();
        var newAdapter = new PopupStoreRewardFakeRepository(h.Store);
        var restored = PopupStoreRewardRecovery.Restore(saved, newAdapter, () => h.Session);
        Assert.That(restored.State, Is.EqualTo(PopupStoreRewardTransactionState.ReconciliationRequired)); Assert.That(restored.TrySubmit(), Is.False);
        restored.TryReconcile(); Assert.That(restored.Grant, Is.SameAs(h.Store.ReadGrant("A", h.Approved.GrantKey)));
        Assert.That(newAdapter.SubmitCalls, Is.Zero); AssertStore(h, 110, 1, 1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RecoveryNeverTrustsTheCheckpointObservedStateAsPaymentAuthority(bool alreadyGranted)
    {
        var h = new Harness(); var previous = h.Transaction(); if (alreadyGranted) previous.TrySubmit();
        var restored = PopupStoreRewardRecovery.Restore(PopupStoreRewardRecovery.Capture(previous), h.Repository, () => h.Session);
        Assert.That(restored.State, Is.EqualTo(PopupStoreRewardTransactionState.ReconciliationRequired)); Assert.That(restored.Grant, Is.Null);
        Assert.That(restored.TrySubmit(), Is.False); restored.TryReconcile();
        Assert.That(restored.State, Is.EqualTo(alreadyGranted ? PopupStoreRewardTransactionState.Granted : PopupStoreRewardTransactionState.ReconciliationRequired));
        AssertStore(h, alreadyGranted ? 110 : 100, alreadyGranted ? 1 : 0, alreadyGranted ? 1 : 0);
    }

    [Test]
    public void AccountsWithIdenticalBusinessIdsKeepSeparateEntitlementsAndBalances()
    {
        var h = new Harness(); h.Store.RegisterAccount("B", 200);
        var second = Copy(h.Approved, account: "B"); h.Store.RegisterDecision(second);
        var a = h.Transaction(); a.TrySubmit(); var bSession = h.Store.OpenSession("B");
        var b = new PopupStoreRewardTransaction(second, h.Repository, () => bSession); b.TrySubmit();
        Assert.That(a.Grant.GrantKey, Is.Not.EqualTo(b.Grant.GrantKey)); Assert.That(a.Grant.GrantId, Is.Not.EqualTo(b.Grant.GrantId));
        AssertStore(h, 110, 1, 1); Assert.That(h.Store.Read("B").Balance, Is.EqualTo(210));
    }

    [Test]
    public void AccountSwitchRejectsOldCallbackButOriginalAccountCanRecoverItsOwnGrant()
    {
        var h = new Harness(); h.Store.RegisterAccount("B", 200); int localCalls = 0;
        h.Repository.NextSubmitFault = PopupStoreFakeSubmitFault.CommitThenDefer; var tx = h.Transaction(g => localCalls++);
        tx.TrySubmit(); var saved = PopupStoreRewardRecovery.Capture(tx); h.Session = h.Store.OpenSession("B"); h.Repository.DeliverNext();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Invalidated)); Assert.That(localCalls, Is.Zero);
        Assert.That(h.Store.Read("B").Balance, Is.EqualTo(200)); Assert.That(h.Store.Read("B").Grants, Is.Zero);
        h.Session = h.Store.OpenSession("A"); var restored = PopupStoreRewardRecovery.Restore(saved, h.Repository, () => h.Session);
        restored.TryReconcile(); Assert.That(restored.Grant.AccountId, Is.EqualTo("A")); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void ReloginToTheSameAccountUsesANewSessionGenerationAndRejectsOldCallbacks()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeSubmitFault.CommitThenDefer;
        var tx = h.Transaction(); tx.TrySubmit(); h.Session = h.Store.OpenSession("A"); h.Repository.DeliverNext();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Invalidated)); Assert.That(tx.Grant, Is.Null); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void RevokedFakeSessionCannotCreateANewGrant()
    {
        var h = new Harness(); h.Store.RevokeSession(h.Session); var tx = h.Transaction(); tx.TrySubmit();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.RejectedBeforeSend)); AssertStore(h, 100, 0, 0);
    }

    [Test]
    public void AForeignAccountCannotRestoreOrQueryAnOriginalAccountCheckpoint()
    {
        var h = new Harness(); var tx = h.Transaction(); tx.TrySubmit(); var saved = PopupStoreRewardRecovery.Capture(tx);
        h.Store.RegisterAccount("B"); h.Session = h.Store.OpenSession("B");
        var restored = PopupStoreRewardRecovery.Restore(saved, h.Repository, () => h.Session);
        Assert.That(restored.State, Is.EqualTo(PopupStoreRewardTransactionState.Invalidated)); Assert.That(restored.TryReconcile(), Is.False);
        Assert.That(h.Store.Read("B").Grants, Is.Zero); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void WrongBusinessIdentityCannotRecoverAnotherBusinessGrant()
    {
        var h = new Harness(); var tx = h.Transaction(); tx.TrySubmit();
        var wrong = h.Transaction(request: Copy(h.Approved, business: "different-business")); wrong.TryReconcile();
        Assert.That(wrong.Grant, Is.Null); Assert.That(wrong.State, Is.EqualTo(PopupStoreRewardTransactionState.ReconciliationRequired)); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void ConcurrentClientsCommitTheSameCanonicalGrantExactlyOnce()
    {
        var h = new Harness(); var grants = new PopupStoreRewardGrant[12];
        RunTogether(i =>
        {
            var tx = new PopupStoreRewardTransaction(h.Approved, new PopupStoreRewardFakeRepository(h.Store), () => h.Session);
            tx.TrySubmit(); if (tx.State != PopupStoreRewardTransactionState.Granted) throw new Exception("Concurrent grant failed.");
            grants[i] = tx.Grant;
        }, grants.Length);
        Assert.That(grants.All(g => ReferenceEquals(g, grants[0])), Is.True); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void ConcurrentAlternateKeysCannotBypassTheSingleBusinessEntitlement()
    {
        var h = new Harness(); var replies = new PopupStoreRewardResponse[12];
        RunTogether(i => replies[i] = h.Store.Submit(h.Session, i == 0 ? h.Approved : Copy(h.Approved, key: "alternate-" + i)), replies.Length);
        Assert.That(replies.Count(r => r.Kind == PopupStoreRewardResponseKind.Granted), Is.EqualTo(1));
        Assert.That(replies.Count(r => r.Kind == PopupStoreRewardResponseKind.Conflict), Is.EqualTo(11)); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void DifferentBusinessesOnOneAccountDoNotLoseEachOthersConcurrentBalanceIncrements()
    {
        var h = new Harness(); var second = Copy(h.Approved, business: "business-two"); h.Store.RegisterDecision(second);
        RunTogether(i =>
        {
            var reply = h.Store.Submit(h.Session, i % 2 == 0 ? h.Approved : second);
            if (reply.Kind != PopupStoreRewardResponseKind.Granted) throw new Exception("Expected grant.");
        }, 12);
        AssertStore(h, 120, 2, 2);
    }

    [Test]
    public void StaleWholeBalanceSaveCannotOverwriteAnAtomicGrant()
    {
        var h = new Harness(); var old = h.Store.Read("A"); h.Transaction().TrySubmit();
        Assert.That(h.Store.TryWriteBalance(h.Session, old.Balance, old.Version), Is.False); AssertStore(h, 110, 1, 1);
        Assert.That(h.Store.TryWriteBalance(h.Session, 111, 1), Is.True); AssertStore(h, 111, 2, 1);
    }

    [Test]
    public void ReplayingAnOldGrantDoesNotRollTheLatestAuthorityBalanceBack()
    {
        var h = new Harness(); var first = h.Transaction(); first.TrySubmit();
        var second = Copy(h.Approved, business: "second"); h.Store.RegisterDecision(second); h.Transaction(request: second).TrySubmit();
        var replay = h.Transaction(); replay.TrySubmit(); Assert.That(replay.Grant.BalanceAfter, Is.EqualTo(110));
        Assert.That(replay.Grant, Is.SameAs(first.Grant)); AssertStore(h, 120, 2, 2);
    }

    [Test]
    public void BalanceOverflowRejectsBeforeEitherLedgerOrVersionChanges()
    {
        var h = new Harness(balance: int.MaxValue - 5); var tx = h.Transaction(); tx.TrySubmit();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.RejectedBeforeSend));
        AssertStore(h, int.MaxValue - 5, 0, 0); Assert.That(h.Store.ReadGrant("A", h.Approved.GrantKey), Is.Null);
    }

    [Test]
    public void ExactMaximumBalanceIsAllowedWithoutWrappingNegative()
    {
        var h = new Harness(balance: int.MaxValue - 10); var tx = h.Transaction(); tx.TrySubmit();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Granted)); AssertStore(h, int.MaxValue, 1, 1);
    }

    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void NegativeSubmittedAmountsCannotReachTheRepositoryOrAuthorityRegistration(int invalid)
    {
        var h = new Harness(); var request = Copy(h.Approved, amount: invalid);
        Assert.Throws<ArgumentException>(() => h.Store.RegisterDecision(request));
        var tx = h.Transaction(request: request); Assert.That(tx.TrySubmit(), Is.False);
        Assert.That(h.Repository.SubmitCalls, Is.Zero); AssertStore(h, 100, 0, 0);
    }

    [Test]
    public void CanonicalKeyIsUnambiguousAcrossDelimitersUnicodeAndPolicyChanges()
    {
        string a = PopupStoreRewardRequest.CreateGrantKey("a:b", "c"), b = PopupStoreRewardRequest.CreateGrantKey("a", "b:c");
        Assert.That(a, Is.Not.EqualTo(b)); Assert.That(PopupStoreRewardRequest.CreateGrantKey("계정", "영업"), Is.Not.Empty);
        var original = Request(); Assert.That(Copy(original, policy: "R999").GrantKey, Is.EqualTo(original.GrantKey));
        Assert.That(Copy(original, decision: "new-decision").GrantKey, Is.EqualTo(original.GrantKey));
    }

    [TestCase("missing-grant")]
    [TestCase("amount")]
    [TestCase("version")]
    [TestCase("echo")]
    public void MalformedSuccessResponseCannotLocallyConfirmOrEnableAnotherSubmission(string problem)
    {
        var h = new Harness(); h.Repository.TransformNextReply = r =>
        {
            var decision = problem == "amount" ? Copy(h.Approved, amount: 11) : h.Approved;
            var grant = problem == "missing-grant" ? null : new PopupStoreRewardGrant(decision, r.Grant.GrantId, 110, problem == "version" ? 0 : 1);
            return new PopupStoreRewardResponse(problem == "echo" ? Copy(h.Approved, business: "foreign") : h.Approved, PopupStoreRewardResponseKind.Granted, grant);
        };
        var tx = h.Transaction(); tx.TrySubmit(); Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Indeterminate));
        Assert.That(tx.Grant, Is.Null); Assert.That(tx.TrySubmit(), Is.False); AssertStore(h, 110, 1, 1);
        tx.TryReconcile(); Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Granted)); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void FalseNoWriteProofOnASubmitResponseCannotEnableResubmission()
    {
        var h = new Harness(); h.Repository.TransformNextReply = r => new PopupStoreRewardResponse(r.Request, PopupStoreRewardResponseKind.NotAppliedConfirmed);
        var tx = h.Transaction(); tx.TrySubmit(); Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Indeterminate));
        Assert.That(tx.TrySubmit(), Is.False); tx.TryReconcile(); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void AuthorityDecisionCannotBeReplacedAfterTheBusinessHasBeenRegistered()
    {
        var h = new Harness(); Assert.Throws<InvalidOperationException>(() => h.Store.RegisterDecision(Copy(h.Approved, amount: 100)));
        var tx = h.Transaction(); tx.TrySubmit(); Assert.That(tx.Grant.Diamonds, Is.EqualTo(10)); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void AccountChangeDuringLocalCompletionInvalidatesExecutionWithoutChangingCommittedOwnership()
    {
        var h = new Harness(); h.Store.RegisterAccount("B", 200);
        var tx = h.Transaction(g => h.Session = h.Store.OpenSession("B")); tx.TrySubmit();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Invalidated)); Assert.That(tx.RetryLocalCompletion(), Is.False);
        Assert.That(h.Store.Read("B").Balance, Is.EqualTo(200)); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void FakeTransactionLeavesAuthenticLocalPreviewFinalGradeAndResultPresentationUnchanged()
    {
        var type = typeof(PopupStoreResultTests).GetNestedType("Host", BindingFlags.NonPublic);
        var host = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, CultureInfo.InvariantCulture);
        try
        {
            var root = (GameObject)Read(host, "Root"); var reward = root.GetComponent<PopupStoreRewardSession>();
            Assert.That(reward.TrySetRandomSource(new FixedRandom()), Is.True); Assert.That(reward.Refresh(), Is.True);
            Call(host, "Start", 90d); Call(host, "SellUntil", PopupStoreGradeTier.C); Call(host, "End"); Call(host, "ShowResult");
            var grade = root.GetComponent<PopupStoreGradeSession>(); var box = root.GetComponent<PopupStoreRewardBox>();
            var decision = reward.Result; var final = grade.FinalResult; var sprite = box.DisplayedSprite;
            int finalized = grade.FinalizationCount, draws = reward.RandomDrawCount, displayed = box.AnimationCount;
            var h = new Harness(); var tx = h.Transaction(); tx.TrySubmit(); tx.TrySubmit(); h.Repository.ReplayLast();
            Assert.That(reward.Result, Is.SameAs(decision)); Assert.That(reward.RandomDrawCount, Is.EqualTo(draws));
            Assert.That(decision.IsLocalPreview, Is.True); Assert.That(tx.Grant.DecisionId, Is.Not.EqualTo(decision.DecisionId));
            Assert.That(grade.FinalResult, Is.SameAs(final)); Assert.That(grade.FinalizationCount, Is.EqualTo(finalized));
            Assert.That(box.DisplayedSprite, Is.SameAs(sprite)); Assert.That(box.AnimationCount, Is.EqualTo(displayed));
            Debug.Log("POPUP_STORE_FAKE_GRANT_EVIDENCE account=A business=" + tx.Grant.BusinessSessionId + " grant=" + tx.Grant.GrantId
                + " fakeBalance=110 ledger=1 version=1 localPreviewUnchanged=True realPaymentCalls=0");
        }
        finally { ((IDisposable)host).Dispose(); }
    }

    [Test]
    public void TransactionContractsContainNoAccountSdkFileOrUnityRandomCallsAndHaveNoLiveAdapter()
    {
        foreach (string name in new[] { "PopupStoreRewardPersistence", "PopupStoreRewardTransaction", "PopupStoreRewardRecovery" })
        {
            string source = File.ReadAllText("Assets/Scripts/UI/Floor3UI/" + name + ".cs");
            foreach (string call in new[] { "UserInfo.", "Backend.", "PlayerPrefs.", "UnityEngine.Random.", "File.Write", "File.Append", "Directory.Create" })
                Assert.That(source, Does.Not.Contain(call), name + " contains forbidden dependency " + call);
        }
        Assert.That(typeof(PopupStoreRewardTransaction).IsSubclassOf(typeof(MonoBehaviour)), Is.False);
        // DEV-SERVER-01 authorizes this adapter, but not a default/live payout binding.
        // Its default wallet gate and latest-wallet-before-Granted behavior are exercised
        // by PopupStoreServerClaimCoordinatorTests with isolated transports.
        CollectionAssert.AreEquivalent(new[] { typeof(PopupStoreBackendPersistence) },
            typeof(IPopupStoreRewardPersistence).Assembly.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface
                && typeof(IPopupStoreRewardPersistence).IsAssignableFrom(t)));
        Assert.That(typeof(PopupStoreBackendPersistence).IsSealed, Is.True);
        Assert.That(typeof(PopupStoreBackendPersistence).GetConstructor(Type.EmptyTypes), Is.Null);
        Assert.That(typeof(PopupStoreBackendPersistence).IsSubclassOf(typeof(MonoBehaviour)), Is.False);
    }

    [Test]
    public void AuthoritativeNoEffectFencePreventsAnAlreadyStartedLateAttemptFromCommitting()
    {
        var h = new Harness(); PopupStoreRewardResponse late = null; Exception failure = null;
        using (var entered = new ManualResetEventSlim(false))
        using (var release = new ManualResetEventSlim(false))
        {
            h.Store.BeforeSubmitLock = attempt => { entered.Set(); release.Wait(); };
            var thread = new Thread(() => { try { late = h.Store.Submit(h.Session, h.Approved); } catch (Exception e) { failure = e; } });
            thread.IsBackground = true; thread.Start();
            bool ready = false, joined = false; PopupStoreRewardResponse proof = null;
            try
            {
                ready = entered.Wait(TimeSpan.FromSeconds(10));
                proof = h.Store.Query(h.Session, h.Approved, true);
            }
            finally { release.Set(); joined = thread.Join(10000); h.Store.BeforeSubmitLock = null; }
            Assert.That(ready && joined, Is.True); Assert.That(failure, Is.Null);
            Assert.That(proof.Kind, Is.EqualTo(PopupStoreRewardResponseKind.NotAppliedConfirmed));
            Assert.That(late.Kind, Is.EqualTo(PopupStoreRewardResponseKind.RejectedBeforeSend)); AssertStore(h, 100, 0, 0);
            var retry = h.Transaction(); retry.TrySubmit(); Assert.That(retry.State, Is.EqualTo(PopupStoreRewardTransactionState.Granted)); AssertStore(h, 110, 1, 1);
        }
    }

    [Test]
    public void SessionGetterInvalidatingTheExecutorCannotReviveItThroughReconcileOrALateCallback()
    {
        var h = new Harness(); bool invalidate = false; PopupStoreRewardTransaction tx = null;
        tx = new PopupStoreRewardTransaction(h.Approved, h.Repository, () =>
        {
            if (invalidate && tx != null) tx.Invalidate();
            return h.Session;
        });
        h.Repository.NextSubmitFault = PopupStoreFakeSubmitFault.CommitThenDefer; tx.TrySubmit();
        invalidate = true; Assert.That(tx.TryReconcile(), Is.False); h.Repository.DeliverNext();
        Assert.That(tx.State, Is.EqualTo(PopupStoreRewardTransactionState.Invalidated));
        Assert.That(tx.Grant, Is.Null); Assert.That(tx.QueryCount, Is.Zero); AssertStore(h, 110, 1, 1);
    }

    [Test]
    public void ConcurrentReadersNeverObserveASeparateLedgerAndBalanceCommit()
    {
        var h = new Harness(); var requests = Enumerable.Range(0, 20).Select(i => Copy(h.Approved, business: "atomic-" + i)).ToArray();
        foreach (var request in requests) h.Store.RegisterDecision(request);
        RunTogether(i =>
        {
            for (int j = i; j < requests.Length; j += 4)
            {
                h.Store.Submit(h.Session, requests[j]);
                for (int read = 0; read < 20; read++)
                {
                    var snapshot = h.Store.Read("A");
                    if (snapshot.Balance != 100 + snapshot.Grants * 10 || snapshot.Version != snapshot.Grants)
                        throw new Exception("A partial ledger/balance/version commit became observable.");
                }
            }
        }, 4);
        AssertStore(h, 300, 20, 20);
    }

    private sealed class FixedRandom : IPopupStoreRewardRandomSource { public int NextInt(int exclusiveUpperBound) => 99; }
    private sealed class Harness
    {
        internal readonly PopupStoreRewardFakeStore Store = new PopupStoreRewardFakeStore();
        internal readonly PopupStoreRewardFakeRepository Repository;
        internal readonly PopupStoreRewardRequest Approved;
        internal PopupStoreRewardAccountSession Session;
        internal Harness(int balance = 100, PopupStoreGradeTier grade = PopupStoreGradeTier.C, int amount = 10)
        {
            Store.RegisterAccount("A", balance); Session = Store.OpenSession("A");
            Approved = Request(grade: grade, amount: amount); Store.RegisterDecision(Approved);
            Repository = new PopupStoreRewardFakeRepository(Store);
        }
        internal PopupStoreRewardTransaction Transaction(Action<PopupStoreRewardGrant> complete = null, PopupStoreRewardRequest request = null)
            => new PopupStoreRewardTransaction(request ?? Approved, Repository, () => Session, complete);
    }
    private static PopupStoreRewardRequest Request(PopupStoreGradeTier grade = PopupStoreGradeTier.C, int amount = 10)
        => new PopupStoreRewardRequest("A", "business", "decision", grade, PopupStoreRewardKind.Jackpot, amount, "R0");
    private static PopupStoreRewardRequest Copy(PopupStoreRewardRequest r, string account = null, string business = null,
        string decision = null, int? amount = null, string policy = null, string key = null)
        => new PopupStoreRewardRequest(account ?? r.AccountId, business ?? r.BusinessSessionId, decision ?? r.DecisionId,
            r.Grade, r.Kind, amount ?? r.Diamonds, policy ?? r.PolicyVersion, key);
    private static void AssertStore(Harness h, int balance, long version, int grants)
    {
        var snapshot = h.Store.Read("A"); Assert.That(snapshot.Balance, Is.EqualTo(balance));
        Assert.That(snapshot.Version, Is.EqualTo(version)); Assert.That(snapshot.Grants, Is.EqualTo(grants));
    }
    private static void RunTogether(Action<int> operation, int count)
    {
        using (var ready = new CountdownEvent(count))
        using (var start = new ManualResetEventSlim(false))
        {
            var errors = new Exception[count]; var threads = new Thread[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                threads[i] = new Thread(() => { ready.Signal(); start.Wait(); try { operation(index); } catch (Exception e) { errors[index] = e; } });
                threads[i].IsBackground = true; threads[i].Start();
            }
            bool allReady = ready.Wait(TimeSpan.FromSeconds(10)); start.Set();
            bool allJoined = true; foreach (var thread in threads) allJoined &= thread.Join(10000);
            Assert.That(allReady && allJoined, Is.True, "Fake concurrent workers did not settle.");
            Assert.That(errors.Where(e => e != null).ToArray(), Is.Empty);
        }
    }
    private static string Hash(string path)
    { using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path))); }
    private static object Read(object owner, string name)
        => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(owner);
    private static object Call(object owner, string name, params object[] args)
        => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(owner, args);
}
#endif
