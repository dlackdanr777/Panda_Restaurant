#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public sealed class PopupStoreGameDataOperationTests
{
    private GameDataSaveCoordinator[] _owners;
    [SetUp] public void Before() { _owners = Owners().ToArray(); }
    [TearDown] public void After() { Owners().Clear(); foreach (var owner in _owners) Owners().Add(owner); }

    [Test]
    public void StartWaitsForExactConfirmedSaveAndNeverSendsDia()
    {
        var state = new State(); var host = new Host(state); var next = PopupStoreGameDataTestData.Started();
        var op = host.Operation(next);
        Assert.That(op.TryStart(out var error), Is.True, error);
        Assert.That(state.Record.State, Is.EqualTo(PopupStoreGameDataState.Empty));
        Assert.That(state.Commits, Is.Zero); Assert.That(JObject.Parse(host.Calls[0].Values.GetJson()).Count, Is.EqualTo(1));
        Assert.That(host.Journal.Prepared, Is.EqualTo(1)); Assert.That(op.TryStart(out _), Is.False);
        host.Reply(0); host.Reply(0);
        Assert.That(state.Record.Matches(next), Is.True); Assert.That(state.Commits, Is.EqualTo(1));
        Assert.That(state.Diamonds, Is.EqualTo(100)); Assert.That(host.Journal.Completed, Is.EqualTo(1));
        Assert.That(host.Calls.Count, Is.EqualTo(2)); host.Reply(1);
    }
    [Test]
    public void ClaimSavesRecordAndBalanceTogetherThenAddsOnceToLatestWallet()
    {
        var source = PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.SSS, 997);
        var state = new State { Record = source }; var host = new Host(state);
        Assert.That(source.TryClaim("claim", out var next, out _), Is.True); var op = host.Operation(next);
        Assert.That(op.TryStart(out _), Is.True);
        var sent = JObject.Parse(host.Calls[0].Values.GetJson());
        Assert.That(sent.Count, Is.EqualTo(2)); Assert.That((int)sent["Dia"], Is.EqualTo(400));
        Assert.That((string)sent[PopupStoreGameDataRecord.FieldName], Is.EqualTo(next.ToJson()));
        Assert.That(state.Diamonds, Is.EqualTo(100)); state.Diamonds += 8; // Existing recipe/ad reward during the request.
        host.Coordinator.RequestAutosave(); host.Coordinator.RequestAutosave();
        host.Reply(0); host.Reply(0);
        Assert.That(state.Diamonds, Is.EqualTo(408)); Assert.That(state.Commits, Is.EqualTo(1));
        Assert.That(state.Record.State, Is.EqualTo(PopupStoreGameDataState.Claimed));
        Assert.That(host.Calls.Count, Is.EqualTo(2));
        Assert.That((int)JObject.Parse(host.Calls[1].Values.GetJson())["Dia"], Is.EqualTo(408)); host.Reply(1);
    }
    [TestCase("500")] [TestCase("408")] [TestCase("null")]
    public void UnknownResponseKeepsIntentAndCannotRetryOrReroll(string response)
    {
        var source = PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 900);
        var state = new State { Record = source }; var host = new Host(state); source.TryClaim("claim", out var next, out _);
        var op = host.Operation(next); Assert.That(op.TryStart(out _), Is.True);
        host.Reply(0, response == "null" ? null : new GameDataRawResponse(false, response, "", "TEST_ONLY"));
        Assert.That(host.Coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Indeterminate));
        Assert.That(state.Diamonds, Is.EqualTo(100)); Assert.That(state.Commits, Is.Zero);
        Assert.That(host.Journal.Completed, Is.Zero); Assert.That(host.Journal.Rejected, Is.Zero);
        Assert.That(op.TryStart(out _), Is.False); Assert.That(host.Operation(next).TryStart(out _), Is.False);
        Assert.That(host.Calls.Count, Is.EqualTo(1)); host.Reply(0);
        Assert.That(state.Diamonds, Is.EqualTo(103)); Assert.That(state.Commits, Is.EqualTo(1));
        Assert.That(host.Journal.Completed, Is.EqualTo(1)); host.Reply(1);
    }
    [Test]
    public void JournalCleanupFailureAfterAckStillNotifiesAndSavesLatestWalletWithoutDoubleReward()
    {
        var source = PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0);
        var state = new State { Record = source }; var host = new Host(state); host.Journal.ThrowComplete = true;
        source.TryClaim("claim", out var next, out _); var op = host.Operation(next);
        Assert.That(op.TryStart(out _), Is.True); state.Diamonds += 8;
        host.Reply(0); host.Reply(0);
        Assert.That(op.Committed, Is.True); Assert.That(op.Error, Does.Contain("정리"));
        Assert.That(state.Diamonds, Is.EqualTo(109)); Assert.That(state.Commits, Is.EqualTo(1));
        Assert.That(state.Notifications, Is.EqualTo(1)); Assert.That(host.Calls.Count, Is.EqualTo(2));
        Assert.That((int)JObject.Parse(host.Calls[1].Values.GetJson())["Dia"], Is.EqualTo(109));
        host.Reply(1); Assert.That(op.Error, Does.Contain("정리"));
        Assert.That(host.Coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));
    }
    [Test]
    public void SentExceptionStaysUnknownAndNoAutosaveMayEraseIntent()
    {
        var state = new State(); var host = new Host(state) { ThrowAfterSend = true }; var op = host.Operation(PopupStoreGameDataTestData.Started());
        Assert.That(op.TryStart(out _), Is.True); Assert.That(state.Commits, Is.Zero);
        Assert.That(host.Coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Indeterminate));
        host.Coordinator.RequestAutosave(); Assert.That(host.Calls.Count, Is.EqualTo(1));
        Assert.That(host.Journal.Prepared, Is.EqualTo(1)); Assert.That(host.Journal.Rejected, Is.Zero);
    }
    [Test]
    public void AccountAToBToAInvalidationNeverAppliesOldCallback()
    {
        var state = new State(); var host = new Host(state); var op = host.Operation(PopupStoreGameDataTestData.Started());
        Assert.That(op.TryStart(out _), Is.True);
        state.IsCurrent = false; host.Coordinator.InvalidateSession(); state.IsCurrent = true;
        host.Reply(0); Assert.That(state.Commits, Is.Zero); Assert.That(host.Journal.Completed, Is.Zero);
        Assert.That(host.Coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.InvalidatedAfterSend));
        Assert.That(GameDataSaveCoordinator.IsTargetBlocked(host.Target), Is.True);
    }
    [Test]
    public void WrongAccountRowRequestAndPayloadCannotAuthorizeCompletion()
    {
        var state = new State(); var host = new Host(state); var op = host.Operation(PopupStoreGameDataTestData.Started()); op.TryStart(out _);
        host.Calls[0].Reply(new GameDataSaveIdentity(op.Identity.RequestId, new GameDataSaveTarget("other", host.Target.RowInDate)), Host.Success());
        host.Calls[0].Reply(new GameDataSaveIdentity("other", host.Target), Host.Success());
        Assert.That(state.Commits, Is.Zero);
        var p = new Param(); p.Add(PopupStoreGameDataRecord.FieldName, PopupStoreGameDataRecord.Empty.ToJson());
        GameDataSavePayload.TryCapture(p, out var altered, out _);
        Assert.That(op.ValidateTransmission(op.Identity, altered), Is.Not.Null);
        Assert.That(op.ValidateTransmission(op.Identity, op.Payload), Is.Null);
        host.Reply(0); host.Reply(1);
    }
    [TestCase(true)] [TestCase(false)]
    public void ClaimOverflowOrChangedSourceRejectsBeforeTransport(bool overflow)
    {
        var source = PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.SSS, 997);
        var state = new State { Record = source, Diamonds = overflow ? int.MaxValue : 100 }; var host = new Host(state);
        source.TryClaim("claim", out var next, out _); var op = host.Operation(next);
        if (!overflow) state.Record = PopupStoreGameDataRecord.Empty;
        Assert.That(op.TryStart(out _), Is.False); Assert.That(host.Calls, Is.Empty); Assert.That(state.Commits, Is.Zero);
        Assert.That(host.Journal.Prepared, Is.Zero);
    }
    [Test]
    public void ExistingDurableIntentCannotGrantPermissionForANewSend()
    {
        var state = new State(); var host = new Host(state); host.Journal.AllowPrepare = false;
        Assert.That(host.Operation(PopupStoreGameDataTestData.Started()).TryStart(out _), Is.False);
        Assert.That(host.Calls, Is.Empty); Assert.That(host.Journal.Rejected, Is.Zero); Assert.That(state.Commits, Is.Zero);
    }
    [Test]
    public void WalletChangeDuringJournalPreparationRejectsTheStaleBalanceBeforeSending()
    {
        var source = PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0); source.TryClaim("claim", out var next, out _);
        var state = new State { Record = source }; var host = new Host(state);
        host.Journal.AfterPrepare = () => state.Diamonds += 7;
        Assert.That(host.Operation(next).TryStart(out _), Is.False);
        Assert.That(host.Calls, Is.Empty); Assert.That(state.Diamonds, Is.EqualTo(107)); Assert.That(state.Commits, Is.Zero);
        Assert.That(host.Journal.Rejected, Is.EqualTo(1));
    }
    [Test]
    public void PreparedIntentIsReleasedOnlyWhenTransmissionIsProvedNotStarted()
    {
        var state = new State(); var host = new Host(state); host.Journal.AfterPrepare = () => state.Record = PopupStoreGameDataTestData.Started();
        Assert.That(host.Operation(PopupStoreGameDataTestData.Started()).TryStart(out _), Is.False);
        Assert.That(host.Calls, Is.Empty); Assert.That(host.Journal.Prepared, Is.EqualTo(1)); Assert.That(host.Journal.Rejected, Is.EqualTo(1));
    }
    [Test]
    public void DefinitePreSendRejectionRetriesTheSameDecisionUnderANewTransportAttempt()
    {
        var source = PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.SSS, 997);
        source.TryClaim("claim", out var next, out _);
        var state = new State { Record = source }; var host = new Host(state) { BlockTransmission = true };
        var first = host.Operation(next); string frozen = next.ToJson();
        Assert.That(first.TryStart(out _), Is.False); Assert.That(first.RejectedBeforeSend, Is.True);
        Assert.That(host.Calls, Is.Empty); Assert.That(host.Coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));
        host.BlockTransmission = false;
        var retry = host.Operation(next, next.LastRequestId + ":attempt:" + Guid.NewGuid().ToString("N"));
        Assert.That(retry.TryStart(out var error), Is.True, error);
        Assert.That(retry.Candidate, Is.SameAs(next)); Assert.That(retry.Candidate.ToJson(), Is.EqualTo(frozen));
        Assert.That(retry.Identity.RequestId, Is.Not.EqualTo(first.Identity.RequestId));
        host.Calls[0].Reply(first.Identity, Host.Success()); Assert.That(state.Commits, Is.Zero);
        host.Reply(0); Assert.That(state.Diamonds, Is.EqualTo(400)); Assert.That(state.Commits, Is.EqualTo(1)); host.Reply(1);
    }
    [Test]
    public void LocalCommitFailureKeepsCoordinatorAndJournalBlocked()
    {
        var state = new State { FailCommit = true }; var host = new Host(state); var op = host.Operation(PopupStoreGameDataTestData.Started());
        op.TryStart(out _); host.Reply(0);
        Assert.That(host.Coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.LocalCompletionFailed));
        Assert.That(op.Committed, Is.False); Assert.That(host.Journal.Completed, Is.Zero); Assert.That(host.Calls.Count, Is.EqualTo(1));
    }
    [Test]
    public void ObserverReentrancyCannotDoubleClaimAndDisplayFailureDoesNotUndoCommit()
    {
        var state = new State { ThrowNotify = true }; var host = new Host(state); var op = host.Operation(PopupStoreGameDataTestData.Started());
        int callbacks = 0; op.Changed += () => { callbacks++; Assert.That(op.TryStart(out _), Is.False); };
        op.TryStart(out _); host.Reply(0); host.Reply(0);
        Assert.That(op.Committed, Is.True); Assert.That(state.Commits, Is.EqualTo(1)); Assert.That(callbacks, Is.GreaterThanOrEqualTo(1)); host.Reply(1);
    }
    [Test]
    public void RestartRestoresCommittedRecordWithoutAddingRewardAgain()
    {
        var record = PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 997); record.TryClaim("claim", out var claimed, out _);
        Assert.That(PopupStoreGameDataRecord.TryRead(claimed.ToJson(), true, out var loaded, out _), Is.True);
        var state = new State { Record = loaded, Diamonds = 105 }; var host = new Host(state);
        Assert.That(loaded.TryClaim("new-claim", out _, out _), Is.False);
        Assert.That(state.Diamonds, Is.EqualTo(105)); Assert.That(state.Commits, Is.Zero); Assert.That(host.Calls, Is.Empty);
    }
    [Test]
    public void OrdinaryOldPopupSaveIsRejectedButUnrelatedPartialFieldRemainsValid()
    {
        var current = PopupStoreGameDataTestData.Started(); var old = new Param(); old.Add(PopupStoreGameDataRecord.FieldName, PopupStoreGameDataRecord.Empty.ToJson());
        GameDataSavePayload.TryCapture(old, out var payload, out _);
        Assert.That(PopupStoreGameDataOperation.ValidateOrdinarySave(payload, current), Is.Not.Null);
        var unrelated = new Param(); unrelated.Add("Money", 42); GameDataSavePayload.TryCapture(unrelated, out payload, out _);
        Assert.That(PopupStoreGameDataOperation.ValidateOrdinarySave(payload, current), Is.Null);
    }

    private static HashSet<GameDataSaveCoordinator> Owners() => (HashSet<GameDataSaveCoordinator>)typeof(GameDataSaveCoordinator)
        .GetField("BlockedCoordinators", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    private sealed class State : IPopupStoreGameDataState
    {
        public bool IsCurrent { get; set; } = true;
        public PopupStoreGameDataRecord Record { get; set; } = PopupStoreGameDataRecord.Empty;
        public int Diamonds { get; set; } = 100;
        public int Commits, Notifications;
        public bool FailCommit, ThrowNotify;
        public bool TryCommit(PopupStoreGameDataRecord expected, PopupStoreGameDataRecord next, int delta, out string error)
        {
            error = null;
            if (FailCommit || !IsCurrent || !expected.Matches(Record) || Diamonds < 0 || (long)Diamonds + delta > int.MaxValue)
            { error = "TEST_ONLY local state changed"; return false; }
            Diamonds += delta; Record = next; Commits++; return true;
        }
        public void Notify() { Notifications++; if (ThrowNotify) throw new InvalidOperationException("TEST_ONLY observer"); }
    }
    private sealed class Journal : IPopupStoreGameDataOperationJournal
    {
        public int Prepared, Completed, Rejected; public bool AllowPrepare = true, ThrowComplete; public Action AfterPrepare;
        public bool TryPrepare(GameDataSaveIdentity id, PopupStoreGameDataRecord source, PopupStoreGameDataRecord next,
            GameDataSavePayload payload, out string error)
        { error = null; if (!AllowPrepare) { error = "Existing intent"; return false; } Prepared++; AfterPrepare?.Invoke(); return true; }
        public void Complete(GameDataSaveIdentity id, PopupStoreGameDataRecord next) { Completed++; if (ThrowComplete) throw new System.IO.IOException("TEST_ONLY cleanup"); }
        public void RejectBeforeSend(GameDataSaveIdentity id, PopupStoreGameDataRecord next) { Rejected++; }
    }
    private sealed class Host : IGameDataUpdateTransport
    {
        public readonly GameDataSaveTarget Target = new GameDataSaveTarget("TEST_ONLY-account", "TEST_ONLY-row-" + Guid.NewGuid().ToString("N"));
        public readonly List<Call> Calls = new List<Call>(); public readonly Journal Journal = new Journal();
        public readonly GameDataSaveCoordinator Coordinator; private readonly State _state; public bool ThrowAfterSend, BlockTransmission;
        private PopupStoreGameDataOperation _operation;
        public Host(State state)
        {
            _state = state; bool initialized = false;
            var settings = new GameDataSdkRetrySettings(new Version(5, 15, 0, 0), false, false, true);
            object[] args = { (Func<GameDataSdkRetrySettings>)(() => settings), (Func<bool>)(() => initialized),
                (Func<GameDataRawResponse>)(() => { initialized = true; return new GameDataRawResponse(true, "200", "", ""); }), null };
            typeof(GameDataSdkInitializationPolicy).GetMethod("ObserveInitialization", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            var policy = (GameDataSdkInitializationPolicy)args[3];
            var updater = new GameDataSingleUpdate(() => new GameDataSaveReadiness(true, true, state.IsCurrent, true,
                Target.AccountInDate, Target, initializationPolicy: policy), this, isSessionCurrent: () => state.IsCurrent,
                validateRequest: (id, payload) => BlockTransmission ? "TEST_ONLY proven pre-send rejection"
                    : _operation != null && _operation.Identity.Matches(id) ? _operation.ValidateTransmission(id, payload) : null);
            Coordinator = new GameDataSaveCoordinator(Target, updater, () =>
            { var p = new Param(); p.Add("Dia", state.Diamonds); p.Add(PopupStoreGameDataRecord.FieldName, state.Record.ToJson()); return p; },
                isSessionCurrent: () => state.IsCurrent);
        }
        public PopupStoreGameDataOperation Operation(PopupStoreGameDataRecord next, string transportId = null)
            => _operation = new PopupStoreGameDataOperation(new GameDataSaveIdentity(transportId ?? next.LastRequestId, Target), _state.Record, next, Coordinator, _state, Journal);
        public void UpdateOnce(GameDataSaveIdentity id, Param values, Action<GameDataSaveIdentity, GameDataRawResponse> reply)
        { Calls.Add(new Call { Id = id, Values = values, Reply = reply }); if (ThrowAfterSend) throw new InvalidOperationException("TEST_ONLY after send"); }
        public void Reply(int i) => Reply(i, Success());
        public void Reply(int i, GameDataRawResponse raw) => Calls[i].Reply(Calls[i].Id, raw);
        public static GameDataRawResponse Success() => new GameDataRawResponse(true, "204", "", "");
    }
    private sealed class Call { public GameDataSaveIdentity Id; public Param Values; public Action<GameDataSaveIdentity, GameDataRawResponse> Reply; }
}
#endif
