#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>In-memory transport only: these tests do not establish Backend persistence or cross-session safety.</summary>
public class GameDataSaveCoordinatorTests
{
    private readonly List<GachaStaffData> _wrappers = new List<GachaStaffData>();
    private readonly Dictionary<StaffData, string> _sources = new Dictionary<StaffData, string>();
    private readonly Dictionary<GachaStaffData, string> _wrapperSnapshots = new Dictionary<GachaStaffData, string>();
    private Random.State _randomState;
    private string _gameState;
    private GameDataSaveCoordinator[] _originalOwners;

    [SetUp]
    public void SetUp()
    {
        _randomState = Random.state; _gameState = ReadGameState();
        _originalOwners = Owners().ToArray();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Assert.That(Random.state, Is.EqualTo(_randomState));
            Assert.That(ReadGameState(), Is.EqualTo(_gameState), "User or payment state changed");
            foreach (var source in _sources)
                Assert.That(EditorJsonUtility.ToJson(source.Key), Is.EqualTo(source.Value), "Staff asset changed");
            foreach (var wrapper in _wrapperSnapshots)
                Assert.That(Describe(wrapper.Key), Is.EqualTo(wrapper.Value), "Staff wrapper changed");
        }
        finally
        {
            foreach (GachaStaffData wrapper in _wrappers)
                if (wrapper != null) Object.DestroyImmediate(wrapper);
            _wrappers.Clear(); _sources.Clear(); _wrapperSnapshots.Clear();
            // Test isolation only: production has no unlock/reset API for an unresolved request.
            Owners().Clear();
            foreach (var owner in _originalOwners) Owners().Add(owner);
        }
    }

    [Test]
    public void PurchaseSuccess_FreezesPayloadThenCreatesOneLatestAutosaveAfterLocalConfirmation()
    {
        StaffAccountSaveData original = Account(55);
        string originalBefore = JsonConvert.SerializeObject(original);
        var drawn = new[] { LoadStaff("STAFF23"), LoadStaff("STAFF01") };
        var eleven = new[] { drawn[0], drawn[0] }.Concat(Enumerable.Repeat(drawn[1], 9)).ToList();
        GachaStaffData[] orderBefore = eleven.ToArray();
        Assert.That(StaffGachaPurchasePlanCalculator.TryCalculate(original, 110,
            StaffGachaPurchaseType.Multi, eleven, out var plan, out string error), Is.True, error);
        Assert.That(plan.DiamondCost, Is.EqualTo(100));
        Assert.That(plan.DiamondsAfter, Is.EqualTo(10));
        Assert.That(plan.AccountResult.Acquisition.Items.Count, Is.EqualTo(11));
        Assert.That(plan.AccountResult.Acquisition.Items.Count(item => item.IsNew), Is.EqualTo(1));
        Assert.That(plan.AccountResult.Acquisition.Items.Count(item => item.IsDuplicate), Is.EqualTo(10));
        CollectionAssert.AreEqual(new[] { "STAFF23", "STAFF23" }.Concat(Enumerable.Repeat("STAFF01", 9)),
            plan.AccountResult.Acquisition.Items.Select(item => item.StaffId));
        CollectionAssert.AreEqual(new[] { 0, 10 }.Concat(Enumerable.Repeat(5, 9)),
            plan.AccountResult.Acquisition.Items.Select(item => item.PandaTokenReward));
        Assert.That(plan.AccountResult.Acquisition.TotalPandaTokens, Is.EqualTo(55));

        var target = Target();
        var transport = new FakeTransport();
        int localDiamonds = 110, confirmations = 0, latestCalls = 0, blockedFactoryCalls = 0;
        StaffAccountSaveData localAccount = original;
        var nested = new List<int> { 2, 4 };
        Param purchaseValues = Values(plan.DiamondsAfter, plan.AccountResult.UpdatedAccount);
        purchaseValues.Add("TestNested", nested);
        GameDataSaveCoordinator coordinator = null;
        coordinator = new GameDataSaveCoordinator(target, Sender(target, transport), () =>
        {
            latestCalls++;
            Assert.That(confirmations, Is.EqualTo(1), "Autosave was prepared before local confirmation");
            return Values(localDiamonds, localAccount);
        });
        var identity = Identity("purchase-eleven", target);
        Assert.That(coordinator.TryStartPurchase(identity, () => purchaseValues, receipt =>
        {
            Assert.That(coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.ApplyingConfirmedState));
            Assert.That(coordinator.CanStartPurchase, Is.False);
            confirmations++;
            localDiamonds = plan.DiamondsAfter;
            localAccount = plan.AccountResult.UpdatedAccount;
        }, out error), Is.True, error);
        Assert.That(transport.Calls.Count, Is.EqualTo(1));
        Assert.That(confirmations, Is.Zero);
        Assert.That(localDiamonds, Is.EqualTo(110));
        Assert.That(localAccount, Is.SameAs(original));
        string frozen = coordinator.CurrentPayload.Json;
        nested[0] = 999;
        nested.Add(1000);
        purchaseValues.Add("CallerAddedAfterSend", true);
        Assert.That(coordinator.CurrentPayload.Json, Is.EqualTo(frozen));
        Assert.That(Capture(transport.Calls[0].Values).Json, Is.EqualTo(frozen));
        Param independentCopy = coordinator.CurrentPayload.CreateParamCopy();
        independentCopy.Add("OnlyInCopy", true);
        Assert.That(JObject.Parse(Capture(coordinator.CurrentPayload.CreateParamCopy()).Json)["OnlyInCopy"], Is.Null);
        CollectionAssert.AreEqual(new[] { 2, 4 }, JObject.Parse(frozen)["TestNested"].Values<int>());

        Assert.That(coordinator.TryStartPurchase(Identity("not-queued", target), () =>
        { blockedFactoryCalls++; return Values(999, original); }, _ => { }, out error), Is.False);
        Assert.That(blockedFactoryCalls, Is.Zero);
        Assert.That(coordinator.MarkAutosaveNeeded(), Is.True);
        Assert.That(coordinator.MarkAutosaveNeeded(), Is.True);
        Assert.That(coordinator.HasPendingAutosave, Is.True);
        Assert.That(latestCalls, Is.Zero);
        Assert.That(transport.Calls.Count, Is.EqualTo(1));

        var success = Success();
        transport.Reply(0, identity, success);
        Assert.That(confirmations, Is.EqualTo(1));
        Assert.That(latestCalls, Is.EqualTo(1));
        Assert.That(transport.Calls.Count, Is.EqualTo(2));
        Assert.That(localDiamonds, Is.EqualTo(10));
        Assert.That(localAccount.PandaTokens, Is.EqualTo(110));
        CollectionAssert.AreEqual(new[] { "STAFF01", "STAFF03", "STAFF23" }, localAccount.Staff.Select(item => item.Id));
        CollectionAssert.AreEqual(new[] { 2, 4, 1 }, localAccount.Staff.Select(item => item.Level));
        JObject autosave = JObject.Parse(Capture(transport.Calls[1].Values).Json);
        Assert.That((int)autosave["Dia"], Is.EqualTo(10));
        var roundTrip = StaffAccountSaveConverter.Read((string)autosave["StaffAccount"]);
        Assert.That(roundTrip.Status, Is.EqualTo(StaffAccountSaveReadStatus.Success));
        Assert.That(JsonConvert.SerializeObject(roundTrip.Data), Is.EqualTo(JsonConvert.SerializeObject(localAccount)));
        transport.Reply(0, identity, success);
        Assert.That(confirmations, Is.EqualTo(1));
        Assert.That(transport.Calls.Count, Is.EqualTo(2));
        transport.Reply(1, transport.Calls[1].Identity, success);
        Assert.That(coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));
        Assert.That(coordinator.CanStartPurchase, Is.True);
        Assert.That(coordinator.HasPendingAutosave, Is.False);
        Assert.That(JsonConvert.SerializeObject(original), Is.EqualTo(originalBefore));
        CollectionAssert.AreEqual(orderBefore, eleven);
    }

    [Test]
    public void UnknownResponsesAndPostSendException_PreserveRawEvidenceAndBlockAllFollowingSends()
    {
        var responses = new[]
        {
            new GameDataRawResponse(false, "400", "HttpRequestException", "connection closed after submission"),
            new GameDataRawResponse(false, "400", "BadParameterException", "unverified server failure"),
            new GameDataRawResponse(false, "408", "RequestTimeout", "no definitive processing result"),
            new GameDataRawResponse(false, "500", "InternalServerError", "processing outcome unknown"),
            null
        };
        foreach (GameDataRawResponse raw in responses)
        {
            var target = Target();
            var transport = new FakeTransport();
            int completed = 0, latestCalls = 0;
            var coordinator = new GameDataSaveCoordinator(target, Sender(target, transport), () =>
            { latestCalls++; return Values(110, Account(55)); });
            var identity = Identity("uncertain", target);
            Assert.That(coordinator.TryStartPurchase(identity, () => Values(100, Account(55)),
                _ => completed++, out string error), Is.True, error);
            string payloadBefore = coordinator.CurrentPayload.Json;
            Assert.That(coordinator.MarkAutosaveNeeded(), Is.True);
            transport.Reply(0, identity, raw);
            AssertIndeterminate(coordinator, transport, identity, payloadBefore);
            Assert.That(coordinator.LastReceipt.SendStarted, Is.True);
            Assert.That(coordinator.LastReceipt.RawResponse, Is.SameAs(raw));
            if (raw != null)
            {
                Assert.That(coordinator.LastReceipt.RawResponse.IsSuccess, Is.EqualTo(raw.IsSuccess));
                Assert.That(coordinator.LastReceipt.RawResponse.StatusCode, Is.EqualTo(raw.StatusCode));
                Assert.That(coordinator.LastReceipt.RawResponse.ErrorCode, Is.EqualTo(raw.ErrorCode));
                Assert.That(coordinator.LastReceipt.RawResponse.Message, Is.EqualTo(raw.Message));
            }
            Assert.That(completed, Is.Zero);
            Assert.That(latestCalls, Is.Zero);
        }

        var throwTarget = Target();
        var throwing = new FakeTransport { ThrowAfterEntry = true };
        var thrownCoordinator = new GameDataSaveCoordinator(throwTarget, Sender(throwTarget, throwing),
            () => Values(110, Account(55)));
        var throwIdentity = Identity("transport-throw", throwTarget);
        thrownCoordinator.TryStartPurchase(throwIdentity, () => Values(100, Account(55)),
            _ => Assert.Fail("An exception is not a success response"), out _);
        AssertIndeterminate(thrownCoordinator, throwing, throwIdentity, thrownCoordinator.CurrentPayload.Json);
        Assert.That(thrownCoordinator.LastReceipt.SendStarted, Is.True);
        Assert.That(thrownCoordinator.LastReceipt.Reason, Does.Contain("fake post-send exception"));

        var missingTarget = Target();
        var silent = new FakeTransport();
        var missing = new GameDataSaveCoordinator(missingTarget, Sender(missingTarget, silent),
            () => Values(110, Account(55)));
        var missingIdentity = Identity("no-response", missingTarget);
        Assert.That(missing.TryStartPurchase(missingIdentity, () => Values(100, Account(55)), _ => { }, out _), Is.True);
        Assert.That(missing.MarkResponseMissing(Identity("different", missingTarget), "timeout"), Is.False);
        Assert.That(missing.State, Is.EqualTo(GameDataSaveCoordinatorState.Sending));
        Assert.That(missing.MarkResponseMissing(missingIdentity, "caller observed timeout"), Is.True);
        AssertIndeterminate(missing, silent, missingIdentity, missing.CurrentPayload.Json);
        var lateRaw = new GameDataRawResponse(false, "400", "HttpRequestException", "late response after timeout");
        silent.Reply(0, missingIdentity, lateRaw);
        Assert.That(missing.LastReceipt.RawResponse, Is.SameAs(lateRaw), "Late raw evidence must not be discarded");
        AssertIndeterminate(missing, silent, missingIdentity, missing.CurrentPayload.Json);
    }

    [Test]
    public void Correlation_IgnoresOtherAccountsRowsRequestsAndDuplicateOrLateCompletion()
    {
        var target = Target();
        var transport = new FakeTransport();
        var coordinator = new GameDataSaveCoordinator(target, Sender(target, transport), () => Values(110, Account(55)));
        int firstCompletions = 0, secondCompletions = 0;
        var first = Identity("first", target);
        Assert.That(coordinator.TryStartPurchase(first, () => Values(100, Account(55)),
            _ => firstCompletions++, out string error), Is.True, error);
        foreach (GameDataSaveIdentity wrong in new[]
        {
            Identity("other-request", target),
            Identity(first.RequestId, new GameDataSaveTarget("other-account", target.RowInDate)),
            Identity(first.RequestId, new GameDataSaveTarget(target.AccountInDate, "other-row")),
            null
        })
        {
            transport.Reply(0, wrong, Success());
            Assert.That(coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Sending));
            Assert.That(coordinator.CurrentIdentity, Is.SameAs(first));
            Assert.That(firstCompletions, Is.Zero);
        }
        // Equivalent identity values are accepted; correlation is not object-reference equality.
        transport.Reply(0, Identity(first.RequestId,
            new GameDataSaveTarget(target.AccountInDate, target.RowInDate)), Success());
        Assert.That(firstCompletions, Is.EqualTo(1));
        Assert.That(coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));
        var second = Identity("second", target);
        Assert.That(coordinator.TryStartPurchase(second, () => Values(90, Account(55)),
            _ => secondCompletions++, out error), Is.True, error);
        string secondPayload = coordinator.CurrentPayload.Json;
        transport.Reply(0, first, Success());
        transport.Reply(0, first, new GameDataRawResponse(false, "500", "late-failure", "old request"));
        transport.Reply(1, first, Success());
        Assert.That(coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Sending));
        Assert.That(coordinator.CurrentIdentity, Is.SameAs(second));
        Assert.That(coordinator.CurrentPayload.Json, Is.EqualTo(secondPayload));
        Assert.That(firstCompletions, Is.EqualTo(1));
        Assert.That(secondCompletions, Is.Zero);
        transport.Reply(1, second, Success());
        transport.Reply(1, second, Success());
        transport.Reply(1, second, new GameDataRawResponse(false, "408", "late-timeout", "already settled"));
        Assert.That(secondCompletions, Is.EqualTo(1));
        Assert.That(coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));
        Assert.That(transport.Calls.Count, Is.EqualTo(2));
    }

    [Test]
    public void ConfirmationReentrancyAndException_NeverSendPendingAutosaveBeforeLocalApplyCompletes()
    {
        var target = Target();
        var transport = new FakeTransport();
        int completed = 0, latestCalls = 0, rejectedFactoryCalls = 0, localValue = 110;
        bool callbackFinished = false;
        GameDataSaveCoordinator coordinator = null;
        coordinator = new GameDataSaveCoordinator(target, Sender(target, transport), () =>
        {
            latestCalls++;
            Assert.That(callbackFinished, Is.True);
            return Values(localValue, Account(55));
        });
        var identity = Identity("reentrant", target);
        Assert.That(coordinator.TryStartPurchase(identity, () => Values(100, Account(55)), receipt =>
        {
            completed++;
            Assert.That(coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.ApplyingConfirmedState));
            Assert.That(coordinator.MarkAutosaveNeeded(), Is.True);
            Assert.That(coordinator.TryStartPurchase(Identity("nested-purchase", target), () =>
            { rejectedFactoryCalls++; return Values(1, Account(55)); }, __ => { }, out _), Is.False);
            transport.Reply(0, identity, Success());
            Assert.That(transport.Calls.Count, Is.EqualTo(1));
            Assert.That(latestCalls, Is.Zero);
            localValue = 100;
            callbackFinished = true;
        }, out string error), Is.True, error);
        transport.Reply(0, identity, Success());
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(rejectedFactoryCalls, Is.Zero);
        Assert.That(latestCalls, Is.EqualTo(1));
        Assert.That(transport.Calls.Count, Is.EqualTo(2));
        Assert.That((int)JObject.Parse(Capture(transport.Calls[1].Values).Json)["Dia"], Is.EqualTo(100));
        transport.Reply(1, transport.Calls[1].Identity, Success());
        Assert.That(coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));

        var failingTransport = new FakeTransport();
        int failedCallbacks = 0, staleFactories = 0;
        GameDataSaveCoordinator failing = null;
        failing = new GameDataSaveCoordinator(target, Sender(target, failingTransport), () =>
        { staleFactories++; return Values(110, Account(55)); });
        var failingIdentity = Identity("local-apply-throws", target);
        Assert.That(failing.TryStartPurchase(failingIdentity, () => Values(100, Account(55)), _ =>
        {
            failedCallbacks++;
            Assert.That(failing.MarkAutosaveNeeded(), Is.True);
            throw new InvalidOperationException("local apply failed");
        }, out error), Is.True, error);
        Assert.DoesNotThrow(() => failingTransport.Reply(0, failingIdentity, Success()));
        Assert.That(failing.State, Is.EqualTo(GameDataSaveCoordinatorState.LocalCompletionFailed));
        Assert.That(failing.LastError, Does.Contain("local apply failed"));
        Assert.That(failing.CanStartPurchase, Is.False);
        Assert.That(failing.HasPendingAutosave, Is.True);
        Assert.That(staleFactories, Is.Zero);
        Assert.That(failingTransport.Calls.Count, Is.EqualTo(1));
        failingTransport.Reply(0, failingIdentity, Success());
        Assert.That(failedCallbacks, Is.EqualTo(1));
        Assert.That(failing.TryStartPurchase(Identity("blocked-after-local-failure", target),
            () => throw new InvalidOperationException("Blocked factory must not run"), _ => { }, out error), Is.False);
        Assert.That(failing.MarkAutosaveNeeded(), Is.False);
        Assert.That(staleFactories, Is.Zero);
    }

    [Test]
    public void PreSendRejection_InvalidTargetReadinessOrPayloadNeverInvokesTransport()
    {
        var target = Target();
        var transport = new FakeTransport();
        var payload = Capture(Values(110, Account(55)));
        var identity = Identity("valid-target", target);
        var appliedPolicy = AppliedPolicy(SdkSettings());
        foreach (GameDataSaveReadiness readiness in new[]
        {
            null,
            new GameDataSaveReadiness(false, true, true, true, target.AccountInDate, target, initializationPolicy: appliedPolicy),
            new GameDataSaveReadiness(true, false, true, true, target.AccountInDate, target, initializationPolicy: appliedPolicy),
            new GameDataSaveReadiness(true, true, false, true, target.AccountInDate, target, initializationPolicy: appliedPolicy),
            new GameDataSaveReadiness(true, true, true, false, target.AccountInDate, target, initializationPolicy: appliedPolicy),
            new GameDataSaveReadiness(true, true, true, true, "other-account", target, initializationPolicy: appliedPolicy),
            new GameDataSaveReadiness(true, true, true, true, target.AccountInDate, null, initializationPolicy: appliedPolicy),
            new GameDataSaveReadiness(true, true, true, true, target.AccountInDate,
                new GameDataSaveTarget(target.AccountInDate, "another-restored-row"), initializationPolicy: appliedPolicy),
            new GameDataSaveReadiness(true, true, true, true, target.AccountInDate, target,
                "Additional transport precondition is not ready", appliedPolicy)
        })
            AssertRejected(new GameDataSingleUpdate(() => readiness, transport), identity, payload);
        var sender = Sender(target, transport);
        foreach (GameDataSaveIdentity invalid in new[]
        {
            null, Identity(null, target), Identity(" \t", target), Identity("no-target", null),
            Identity("no-account", new GameDataSaveTarget("", target.RowInDate)),
            Identity("no-row", new GameDataSaveTarget(target.AccountInDate, " ")),
            Identity("another-account", new GameDataSaveTarget("other-account", target.RowInDate))
        })
            AssertRejected(sender, invalid, payload);
        AssertRejected(sender, identity, null);
        AssertRejected(new GameDataSingleUpdate(() => throw new InvalidOperationException("not ready"), transport), identity, payload);
        Assert.That(transport.Calls.Count, Is.Zero, "Pre-send rejection must not invoke UpdateV2 or a fallback");

        Assert.That(GameDataSavePayload.TryCapture(null, out var invalidPayload, out string error), Is.False);
        Assert.That(invalidPayload, Is.Null);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
        var arithmetic = new Param();
        arithmetic.Add("Dia", new Dictionary<string, object> { { "operator", "+" }, { "number", 2 } });
        Assert.That(GameDataSavePayload.TryCapture(arithmetic, out invalidPayload, out error), Is.False,
            "Only fixed final values belong in the one-shot path, not SDK arithmetic operations");
        Assert.That(invalidPayload, Is.Null);
        var invalidField = new Param();
        // Bypass Param.Add's silent refusal to verify capture does not silently drop an original field.
        ((System.Collections.SortedList)invalidField)["1bad"] = 123;
        Assert.That(GameDataSavePayload.TryCapture(invalidField, out invalidPayload, out error), Is.False);
        Assert.That(invalidPayload, Is.Null);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
        Assert.That(transport.Calls.Count, Is.Zero);
    }

    [Test]
    public void SdkPolicy_RequiresObservedSuccessfulInitializationAndRejectsUnknownOrUnsupportedProof()
    {
        var target = Target();
        var payload = Capture(Values(110, Account(55)));
        var identity = Identity("policy-preflight", target);
        var rejectedTransport = new FakeTransport();
        // All external save/login/load booleans being true is not initialization evidence.
        AssertRejected(new GameDataSingleUpdate(() => Readiness(target, null), rejectedTransport), identity, payload);
        Assert.That(typeof(GameDataSdkInitializationPolicy).GetConstructors(), Is.Empty,
            "A public settings/bool constructor would allow fabricated applied initialization proof");

        bool initialized = false;
        var failedRaw = new GameDataRawResponse(false, "400", "fake-initialization-failure", "test only");
        var failed = ObservePolicy(() => SdkSettings(), () => initialized, () =>
        { initialized = true; return failedRaw; }, out var observed);
        Assert.That(observed, Is.SameAs(failedRaw));
        Assert.That(failed, Is.Null);
        AssertRejected(new GameDataSingleUpdate(() => Readiness(target, failed), rejectedTransport), identity, payload);

        initialized = false;
        var missingResponse = ObservePolicy(() => SdkSettings(), () => initialized, () =>
        { initialized = true; return null; }, out observed);
        Assert.That(missingResponse, Is.Null);
        Assert.That(observed, Is.Null);
        var incomplete = ObservePolicy(() => SdkSettings(), () => false, InitializationSuccess, out observed);
        Assert.That(incomplete, Is.Null, "A success-looking response without SDK initialization is not applied proof");
        var alreadyInitialized = ObservePolicy(() => SdkSettings(), () => true, InitializationSuccess, out observed);
        Assert.That(alreadyInitialized, Is.Null, "Current configuration must not be mistaken for a prior applied configuration");
        initialized = false;
        var missingSettings = ObservePolicy(() => null, () => initialized, () =>
        { initialized = true; return InitializationSuccess(); }, out observed);
        Assert.That(missingSettings, Is.Null);

        initialized = false;
        GameDataSdkRetrySettings changing = SdkSettings();
        var changedDuringInitialization = ObservePolicy(() => changing, () => initialized, () =>
        {
            changing = SdkSettings(clientRetry: true);
            initialized = true;
            return InitializationSuccess();
        }, out observed);
        Assert.That(changedDuringInitialization, Is.Null, "Ambiguous initialization inputs cannot authorize a write");
        foreach (GameDataSdkInitializationPolicy absent in new[]
        { missingResponse, incomplete, alreadyInitialized, missingSettings, changedDuringInitialization })
            AssertRejected(new GameDataSingleUpdate(() => Readiness(target, absent), rejectedTransport), identity, payload);

        foreach (GameDataSdkRetrySettings unsupported in new[]
        {
            SdkSettings(clientRetry: true), SdkSettings(serverRetry: true),
            SdkSettings(clientRetry: true, serverRetry: true),
            SdkSettings(version: new Version(5, 16, 0, 0)), SdkSettings(version: new Version(5, 15, 0, 1))
        })
        {
            var applied = AppliedPolicy(unsupported);
            Assert.That(applied, Is.Not.Null, "Unsupported applied settings should remain diagnosable");
            Assert.That(applied.IsSupported, Is.False);
            Assert.That(applied.BlockReason, Is.Not.Null.And.Not.Empty);
            AssertRejected(new GameDataSingleUpdate(() => Readiness(target, applied), rejectedTransport), identity, payload);
        }
        Assert.That(rejectedTransport.Calls.Count, Is.Zero);

        foreach (bool autoRefresh in new[] { true, false })
        {
            var applied = AppliedPolicy(SdkSettings(autoRefresh: autoRefresh));
            Assert.That(applied.IsSupported, Is.True, applied.BlockReason);
            Assert.That(applied.Settings.AutoRefreshToken, Is.EqualTo(autoRefresh));
            var fake = new FakeTransport();
            var sender = new GameDataSingleUpdate(() => Readiness(target, applied), fake);
            GameDataSaveReceipt completion = null;
            sender.Send(identity, payload, receipt => completion = receipt);
            Assert.That(fake.Calls.Count, Is.EqualTo(1), "Supported proof should reach only the fake transport once");
            Assert.That(completion, Is.Null);
            fake.Reply(0, identity, Success());
            Assert.That(completion.Disposition, Is.EqualTo(GameDataSaveDisposition.SuccessConfirmed));
        }
    }

    [Test]
    public void SdkPolicy_PreservesInitializationSnapshotDespiteLaterConfigurationChanges()
    {
        var target = Target();
        var payload = Capture(Values(110, Account(55)));
        var identity = Identity("immutable-policy", target);
        bool initialized = false;
        int fakeInitializations = 0;
        GameDataSdkRetrySettings settings = SdkSettings(clientRetry: true);
        var unsupportedApplied = ObservePolicy(() => settings, () => initialized, () =>
        { fakeInitializations++; initialized = true; return InitializationSuccess(); }, out _);
        settings = SdkSettings(); // Changing the current settings cannot retroactively authorize the old initialization.
        Assert.That(unsupportedApplied.Settings.RetryWhenClientRequestFailError, Is.True);
        Assert.That(unsupportedApplied.IsSupported, Is.False);
        var rejected = new FakeTransport();
        AssertRejected(new GameDataSingleUpdate(() => Readiness(target, unsupportedApplied), rejected), identity, payload);
        Assert.That(rejected.Calls.Count, Is.Zero);

        initialized = false;
        settings = SdkSettings(autoRefresh: true);
        var supportedApplied = ObservePolicy(() => settings, () => initialized, () =>
        { fakeInitializations++; initialized = true; return InitializationSuccess(); }, out _);
        settings = SdkSettings(version: new Version(9, 0, 0, 0), clientRetry: true, serverRetry: true, autoRefresh: false);
        Assert.That(supportedApplied.Settings.SdkVersion, Is.EqualTo(new Version(5, 15, 0, 0)));
        Assert.That(supportedApplied.Settings.RetryWhenClientRequestFailError, Is.False);
        Assert.That(supportedApplied.Settings.RetryWhenServerError, Is.False);
        Assert.That(supportedApplied.Settings.AutoRefreshToken, Is.True);
        Assert.That(supportedApplied.IsSupported, Is.True, supportedApplied.BlockReason);
        Assert.That(fakeInitializations, Is.EqualTo(2));
        foreach (Type immutable in new[] { typeof(GameDataSdkInitializationPolicy), typeof(GameDataSdkRetrySettings) })
            foreach (PropertyInfo property in immutable.GetProperties())
                Assert.That(property.GetSetMethod(true), Is.Null, immutable.Name + "." + property.Name);

        var transport = new FakeTransport();
        var updater = new GameDataSingleUpdate(() => Readiness(target, supportedApplied), transport);
        int completed = 0;
        var coordinator = new GameDataSaveCoordinator(target, updater, () => Values(110, Account(55)));
        Assert.That(coordinator.TryStartPurchase(identity, () => Values(100, Account(55)),
            _ => completed++, out string error), Is.True, error);
        Assert.That(transport.Calls.Count, Is.EqualTo(1));
        var raw = new GameDataRawResponse(false, "400", "HttpRequestException", "mock response; no network");
        transport.Reply(0, identity, raw);
        AssertIndeterminate(coordinator, transport, identity, coordinator.CurrentPayload.Json);
        Assert.That(coordinator.LastReceipt.RawResponse, Is.SameAs(raw));
        Assert.That(completed, Is.Zero);
        // This verifies only applied-setting gating and fake responses, not actual SDK token refresh/network behavior.
    }

    private static void AssertIndeterminate(GameDataSaveCoordinator coordinator, FakeTransport transport,
        GameDataSaveIdentity identity, string frozen)
    {
        Assert.That(coordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Indeterminate));
        Assert.That(coordinator.LastReceipt.Disposition, Is.EqualTo(GameDataSaveDisposition.Indeterminate));
        Assert.That(coordinator.CurrentIdentity, Is.SameAs(identity));
        Assert.That(coordinator.CurrentPayload.Json, Is.EqualTo(frozen));
        Assert.That(coordinator.CanStartPurchase, Is.False);
        Assert.That(coordinator.MarkAutosaveNeeded(), Is.False);
        Assert.That(coordinator.HasPendingAutosave, Is.True);
        Assert.That(coordinator.TryStartPurchase(Identity("blocked-following", identity.Target),
            () => throw new InvalidOperationException("Blocked factory must not run"), _ => { }, out _), Is.False);
        Assert.That(transport.Calls.Count, Is.EqualTo(1), "No retry or pending autosave may be sent");
    }

    private static void AssertRejected(GameDataSingleUpdate sender, GameDataSaveIdentity identity, GameDataSavePayload payload)
    {
        GameDataSaveReceipt receipt = null;
        int deliveries = 0;
        sender.Send(identity, payload, result => { deliveries++; receipt = result; });
        Assert.That(deliveries, Is.EqualTo(1));
        Assert.That(receipt, Is.Not.Null);
        Assert.That(receipt.SendStarted, Is.False);
        Assert.That(receipt.Disposition, Is.EqualTo(GameDataSaveDisposition.RejectedBeforeSend));
        Assert.That(receipt.RawResponse, Is.Null);
        Assert.That(receipt.Reason, Is.Not.Null.And.Not.Empty);
    }

    private static HashSet<GameDataSaveCoordinator> Owners() => (HashSet<GameDataSaveCoordinator>)
        typeof(GameDataSaveCoordinator).GetField("BlockedCoordinators", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    private static GameDataSaveTarget Target() => new GameDataSaveTarget("test-account-inDate", "test-row:" + Guid.NewGuid().ToString("N"));
    private static GameDataSaveIdentity Identity(string requestId, GameDataSaveTarget target) => new GameDataSaveIdentity(requestId, target);
    private static GameDataRawResponse Success() => new GameDataRawResponse(true, "204", "", "");
    private static GameDataSingleUpdate Sender(GameDataSaveTarget target, FakeTransport transport)
    {
        var applied = AppliedPolicy(SdkSettings());
        return new GameDataSingleUpdate(() => Readiness(target, applied), transport);
    }

    private static GameDataSaveReadiness Readiness(GameDataSaveTarget target, GameDataSdkInitializationPolicy applied) =>
        new GameDataSaveReadiness(true, true, true, true, target.AccountInDate, target, initializationPolicy: applied);

    private static GameDataSdkRetrySettings SdkSettings(Version version = null,
        bool clientRetry = false, bool serverRetry = false, bool autoRefresh = true) =>
        new GameDataSdkRetrySettings(version ?? new Version(5, 15, 0, 0), clientRetry, serverRetry, autoRefresh);

    private static GameDataRawResponse InitializationSuccess() => new GameDataRawResponse(true, "200", "", "");

    private static GameDataSdkInitializationPolicy AppliedPolicy(GameDataSdkRetrySettings settings)
    {
        bool initialized = false;
        return ObservePolicy(() => settings, () => initialized, () =>
        { initialized = true; return InitializationSuccess(); }, out _);
    }

    private static GameDataSdkInitializationPolicy ObservePolicy(Func<GameDataSdkRetrySettings> readSettings,
        Func<bool> isInitialized, Func<GameDataRawResponse> initialize, out GameDataRawResponse response)
    {
        // Exercise the actual capture method without a public proof fabrication API or any real SDK initialization.
        MethodInfo capture = typeof(GameDataSdkInitializationPolicy).GetMethod("ObserveInitialization",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(capture, Is.Not.Null);
        var arguments = new object[] { readSettings, isInitialized, initialize, null };
        response = (GameDataRawResponse)capture.Invoke(null, arguments);
        return (GameDataSdkInitializationPolicy)arguments[3];
    }

    private static GameDataSavePayload Capture(Param values)
    {
        Assert.That(GameDataSavePayload.TryCapture(values, out var payload, out string error), Is.True, error);
        return payload;
    }

    private static Param Values(int diamonds, StaffAccountSaveData account)
    {
        Assert.That(StaffAccountSaveConverter.TrySerialize(account, out string json, out string error), Is.True, error);
        var values = new Param();
        values.Add("Dia", diamonds);
        values.Add("StaffAccount", json); // Test-only proposed field; no table/column is created.
        return values;
    }

    private static StaffAccountSaveData Account(long tokens) => new StaffAccountSaveData(1, new[]
    {
        new StaffAccountStaffRecord("STAFF01", 2), new StaffAccountStaffRecord("STAFF03", 4)
    }, tokens);

    private sealed class FakeTransport : IGameDataUpdateTransport
    {
        public readonly List<Call> Calls = new List<Call>();
        public bool ThrowAfterEntry;
        public void UpdateOnce(GameDataSaveIdentity identity, Param values,
            Action<GameDataSaveIdentity, GameDataRawResponse> onResponse)
        {
            Calls.Add(new Call { Identity = identity, Values = values, Respond = onResponse });
            if (ThrowAfterEntry) throw new InvalidOperationException("fake post-send exception");
        }
        public void Reply(int index, GameDataSaveIdentity identity, GameDataRawResponse response) => Calls[index].Respond(identity, response);
        public sealed class Call
        {
            public GameDataSaveIdentity Identity;
            public Param Values;
            public Action<GameDataSaveIdentity, GameDataRawResponse> Respond;
        }
    }

    private GachaStaffData LoadStaff(string id)
    {
        StaffData staff = Resources.Load<StaffData>("StaffData/" + id);
        Assert.That(staff, Is.Not.Null, id);
        Assert.That(staff.Id, Is.EqualTo(id));
        if (!_sources.ContainsKey(staff)) _sources.Add(staff, EditorJsonUtility.ToJson(staff));
        GachaStaffData wrapper = GachaStaffData.Create(staff);
        _wrappers.Add(wrapper);
        _wrapperSnapshots.Add(wrapper, Describe(wrapper));
        return wrapper;
    }

    private static string Describe(GachaStaffData data) => data == null ? "null" : JsonConvert.SerializeObject(new
    {
        Instance = data.GetInstanceID(), Wrapper = EditorJsonUtility.ToJson(data),
        Source = data.StaffData == null ? null : EditorJsonUtility.ToJson(data.StaffData)
    });

    private static string ReadGameState()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var stages = (StageInfo[])typeof(UserInfo)
            .GetField("_stageInfos", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var stageState = stages?.Select(stage => stage == null ? null : new
        {
            Owned = typeof(StageInfo).GetField("_giveStaffDic", flags).GetValue(stage),
            Equipped = ((Dictionary<ERestaurantFloorType, Dictionary<EquipStaffType, StaffData>>)
                typeof(StageInfo).GetField("_equipStaffTypeDic", flags).GetValue(stage))
                .Select(floor => new { Floor = floor.Key, Staff = floor.Value.Select(slot => new
                { Slot = slot.Key, Id = slot.Value == null ? null : slot.Value.Id }).ToArray() }).ToArray()
        }).ToArray();
        return JsonConvert.SerializeObject(new
        {
            UserInfo.Dia, UserInfo.Money, UserInfo.SkinToken, UserInfo.TotalUseGachaMachineCount,
            UserInfo.CurrentStage, UserInfo.IsFirstTutorialClear, UserInfo.IsTutorialStart, Stages = stageState,
            PaymentInfo.PaymentDatas, PaymentInfo.GachaPaymentDatas
        });
    }
}
#endif
