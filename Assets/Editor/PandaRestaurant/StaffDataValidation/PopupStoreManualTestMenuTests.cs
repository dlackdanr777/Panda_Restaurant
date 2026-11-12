#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// <summary>Editor command boundaries, without entering Play or modifying the user's request/session state.</summary>
public sealed class PopupStoreManualTestMenuTests
{
    private const string Folder = "Assets/Editor/PandaRestaurant/StaffDataValidation/";
    private GachaCollectionPreviewSceneWitness _workspace;
    private UnityEngine.Random.State _random;
    private bool _sdk, _login, _playing;
    private float _timeScale;

    [SetUp]
    public void ObserveWorkspace()
    {
        _workspace = GachaCollectionPreviewSceneWitness.Capture();
        _random = UnityEngine.Random.state;
        _sdk = BackEnd.Backend.IsInitialized; _login = BackEnd.Backend.IsLogin;
        _playing = EditorApplication.isPlaying; _timeScale = Time.timeScale;
    }

    [TearDown]
    public void CommandsLeaveTheActualEditorAndSdkUntouched()
    {
        _workspace.AssertUnchanged("manual popup command boundary");
        Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
        Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_sdk));
        Assert.That(BackEnd.Backend.IsLogin, Is.EqualTo(_login));
        Assert.That(EditorApplication.isPlaying, Is.EqualTo(_playing));
        Assert.That(Time.timeScale, Is.EqualTo(_timeScale));
    }

    [TestCase("Playing")]
    [TestCase("Transitioning")]
    [TestCase("Compiling")]
    [TestCase("Updating")]
    [TestCase("CompilationFailed")]
    [TestCase("RegressionRunning")]
    [TestCase("LiveAccount")]
    [TestCase("DirtyScene")]
    [TestCase("PrefabStage")]
    [TestCase("ReloadDisabled")]
    [TestCase("OtherBackendOwner")]
    [TestCase("PendingRequest")]
    [TestCase("OtherReview")]
    public void BothCommandsRefuseUnsafeEditorStatesBeforeAnyDispatch(string condition)
    {
        var actions = new FakeActions();
        typeof(PopupStoreManualTestStatus).GetField(condition).SetValue(actions.State, true);
        Assert.That(PopupStoreManualTestCommands.TryStart(actions, out string start), Is.False);
        Assert.That(PopupStoreManualTestCommands.TryReset(actions, out string reset), Is.False);
        Assert.That(start, Is.Not.Null.And.Not.Empty); Assert.That(reset, Is.Not.Null.And.Not.Empty);
        Assert.That(actions.StartCalls + actions.ResetCalls, Is.Zero);
        Assert.That((bool)typeof(PopupStoreManualTestStatus).GetField(condition).GetValue(actions.State), Is.True);
        Assert.That(actions.ReadCalls, Is.EqualTo(2));
    }

    [TestCase("HasRecord")]
    [TestCase("OwnerPresent")]
    [TestCase("OwnerLost")]
    [TestCase("AdmissionBound")]
    [TestCase("StartPending")]
    [TestCase("OriginalMemoryOwner")]
    public void ExistingOwnedEvidenceCannotBeSilentlyOverwrittenByStart(string evidence)
    {
        var actions = new FakeActions();
        typeof(PopupStoreManualTestStatus).GetField(evidence).SetValue(actions.State, true);
        Assert.That(PopupStoreManualTestCommands.TryStart(actions, out _), Is.False);
        Assert.That(actions.StartCalls, Is.Zero);
        Assert.That((bool)typeof(PopupStoreManualTestStatus).GetField(evidence).GetValue(actions.State), Is.True);
    }

    [Test]
    public void FreshStartDispatchesOnceAndAnImmediateSecondClickCannotStartAgain()
    {
        var actions = new FakeActions();
        Assert.That(PopupStoreManualTestCommands.TryStart(actions, out string reason), Is.True);
        Assert.That(reason, Does.Contain("사용자"));
        Assert.That(actions.State.StartPending && actions.State.HasRecord, Is.True);
        Assert.That(PopupStoreManualTestCommands.TryStart(actions, out _), Is.False);
        Assert.That(actions.StartCalls, Is.EqualTo(1)); Assert.That(actions.ResetCalls, Is.Zero);
    }

    [Test]
    public void ExplicitIdleResetPermitsAFreshStartAndRepeatedEmptyResetIsSafe()
    {
        var actions = new FakeActions
        {
            State = new PopupStoreManualTestStatus { HasRecord = true, OwnerLost = true, Stopped = true, Remaining = 0, Limit = 1, Used = 1 }
        };
        Assert.That(PopupStoreManualTestCommands.TryStart(actions, out _), Is.False);
        Assert.That(PopupStoreManualTestCommands.TryReset(actions, out _), Is.True);
        Assert.That(PopupStoreManualTestCommands.TryReset(actions, out _), Is.True);
        Assert.That(actions.State.HasRecord || actions.State.OwnerLost, Is.False);
        Assert.That(PopupStoreManualTestCommands.TryStart(actions, out _), Is.True);
        Assert.That(actions.StartCalls, Is.EqualTo(1)); Assert.That(actions.ResetCalls, Is.EqualTo(2));
        Assert.That(actions.ForeignRecord, Is.EqualTo("unrelated-review-must-survive"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ChangedSafetyAtDispatchPreservesTheAdaptersRefusalWithoutRetry(bool reset)
    {
        var actions = new FakeActions { AcceptDispatch = false, DispatchReason = "The existing owner changed; no mutation." };
        string reason;
        bool result = reset ? PopupStoreManualTestCommands.TryReset(actions, out reason)
            : PopupStoreManualTestCommands.TryStart(actions, out reason);
        Assert.That(result, Is.False); Assert.That(reason, Is.EqualTo(actions.DispatchReason));
        Assert.That(actions.StartCalls + actions.ResetCalls, Is.EqualTo(1));
        Assert.That(actions.State.HasRecord, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DispatchExceptionNeverRetriesOrInvokesTheOtherCommand(bool reset)
    {
        var actions = new FakeActions { ThrowOnDispatch = true };
        string reason;
        bool result = reset ? PopupStoreManualTestCommands.TryReset(actions, out reason)
            : PopupStoreManualTestCommands.TryStart(actions, out reason);
        Assert.That(result, Is.False); Assert.That(reason, Does.Contain("InvalidOperationException"));
        Assert.That(reason, Does.Not.Contain("private-account-secret"));
        Assert.That(actions.StartCalls, Is.EqualTo(reset ? 0 : 1));
        Assert.That(actions.ResetCalls, Is.EqualTo(reset ? 1 : 0));
    }

    [Test]
    public void MissingOrUnreadableStatusFailsClosedWithoutDispatch()
    {
        Assert.That(PopupStoreManualTestCommands.TryStart(null, out _), Is.False);
        Assert.That(PopupStoreManualTestCommands.TryReset(null, out _), Is.False);
        var actions = new FakeActions { State = null };
        Assert.That(PopupStoreManualTestCommands.TryStart(actions, out _), Is.False);
        Assert.That(PopupStoreManualTestCommands.TryReset(actions, out _), Is.False);
        actions.ThrowOnRead = true;
        Assert.That(PopupStoreManualTestCommands.TryStart(actions, out _), Is.False);
        Assert.That(PopupStoreManualTestCommands.TryReset(actions, out _), Is.False);
        Assert.That(PopupStoreManualTestCommands.ReadStatus(actions), Does.Contain("안전 차단"));
        Assert.That(actions.StartCalls + actions.ResetCalls, Is.Zero);
    }

    [TestCase("empty", "미준비")]
    [TestCase("pending", "준비 중")]
    [TestCase("bound", "준비됨")]
    [TestCase("running", "진행 중")]
    [TestCase("paused", "진행 중")]
    [TestCase("used", "사용 완료")]
    [TestCase("ended", "사용 완료")]
    [TestCase("stopped", "종료됨")]
    [TestCase("foreign-play", "안전 차단")]
    [TestCase("before-popup", "준비 중")]
    public void PlannerStatusSeparatesReadinessGameplayConsumptionAndStoppedRecords(string phase, string expected)
    {
        var state = Phase(phase);
        var actions = new FakeActions { State = state };
        string before = JsonUtility.ToJson(state);
        string text = PopupStoreManualTestCommands.ReadStatus(actions);
        Assert.That(text, Does.StartWith("상태: " + expected + "\n"));
        Assert.That(actions.ReadCalls, Is.EqualTo(1));
        Assert.That(actions.StartCalls + actions.ResetCalls, Is.Zero);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        Assert.That(text, Does.Contain("Play: " + (state.Playing ? "ON" : "OFF")));
        Assert.That(text, Does.Contain("검수 기록: " + (state.HasRecord ? "있음" : "없음")));
    }

    [Test]
    public void UnknownQuotaIsNotInventedAsZeroOrOneAndNullStatusIsExplicit()
    {
        string text = PopupStoreManualTestPolicy.FormatStatus(new PopupStoreManualTestStatus());
        Assert.That(text, Does.Contain("영업권: 미확인"));
        Assert.That(text, Does.Not.Contain("0/0").And.Not.Contain("1/1"));
        Assert.That(PopupStoreManualTestCommands.ReadStatus(null), Does.Contain("안전 차단").And.Contain("미확인"));
        var state = Phase("bound");
        Assert.That(PopupStoreManualTestPolicy.FormatStatus(state), Does.Contain("영업권: 1/1 · 사용 0회"));
        state.Remaining = 0; state.Used = 1;
        Assert.That(PopupStoreManualTestPolicy.FormatStatus(state), Does.Contain("영업권: 0/1 · 사용 1회"));
    }

    [Test]
    public void StatusHidesInternalIdentitiesAndProvidesTheExactExistingManualRoute()
    {
        var state = Phase("bound"); state.AccountId = "private-account-123"; state.LocalSessionId = "private-session-456";
        state.Detail = "account=" + state.AccountId + "; session=" + state.LocalSessionId;
        string text = PopupStoreManualTestPolicy.FormatStatus(state);
        Assert.That(text, Does.Not.Contain(state.AccountId).And.Not.Contain(state.LocalSessionId));
        Assert.That(text, Does.Contain("Tools → Panda Restaurant → Customer Parties → Manual Play (Original)"));
        Assert.That(text, Does.Contain("원본 90초").And.Contain("일반 R1 추첨").And.Contain("사용자 수동 플레이"));
        Assert.That(text, Does.Contain("미지급").Or.Contain("지급 금지"));
        Assert.That(text, Does.Contain("테스트 종료 (Play 중지)").And.Contain("테스트 초기화"));
    }

    [Test]
    public void LiveAccountSafetyMessageCannotClaimTheCurrentServerIsDisconnected()
    {
        string text = PopupStoreManualTestPolicy.FormatStatus(new PopupStoreManualTestStatus { LiveAccount = true });
        Assert.That(text, Does.StartWith("상태: 안전 차단\n"));
        Assert.That(text, Does.Contain("검수 정책").And.Contain("실제 서버 연결·계정 반영·지급 금지"));
        Assert.That(text, Does.Not.Contain("실제 서버 미연결"));
    }

    [Test]
    public void ThreeMenuCommandsAreRegisteredOnTheEditorMenuWithoutValidationSideEffects()
    {
        var methods = typeof(PopupStoreManualTestMenu).GetMethods(BindingFlags.Static | BindingFlags.NonPublic);
        var entries = methods.SelectMany(method => method.GetCustomAttributes(typeof(MenuItem), false)
            .Cast<MenuItem>().Select(attribute => new { Method = method, Attribute = attribute })).ToArray();
        CollectionAssert.AreEquivalent(new[] { PopupStoreManualTestMenu.StartPath, PopupStoreManualTestMenu.ResetPath,
            PopupStoreManualTestMenu.StatusPath }, entries.Select(entry => entry.Attribute.menuItem));
        Assert.That(entries, Has.Length.EqualTo(3));
        foreach (var entry in entries)
        {
            Assert.That(entry.Attribute.validate, Is.False);
            Assert.That(entry.Method.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(entry.Method.GetParameters(), Is.Empty);
        }
    }

    [Test]
    public void MenuAndTestSourcesAreEditorOnlyAndExcludedFromThePlayerCompilationGraph()
    {
        var playerSources = CompilationPipeline.GetAssemblies(AssembliesType.Player).SelectMany(assembly => assembly.sourceFiles)
            .Select(path => path.Replace('\\', '/')).ToArray();
        foreach (string name in new[] { "PopupStoreManualTestMenu", "PopupStoreManualTestMenuTests", "PopupStorePlannerReviewBridge" })
        {
            string path = Folder + name + ".cs";
            Assert.That(File.ReadAllText(path), Does.StartWith("#if UNITY_EDITOR"));
            Assert.That(playerSources.Any(source => source.EndsWith("/" + name + ".cs", StringComparison.Ordinal)), Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>(path), Is.Not.Null);
        }
        Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>(Folder + "PopupStoreManualTestMenu.cs").GetClass(), Is.EqualTo(typeof(PopupStoreManualTestMenu)));
        Assert.That(typeof(PopupStoreManualTestMenu).Assembly, Is.Not.SameAs(typeof(UIPopupStore).Assembly));
    }

    [Test]
    public void DefaultMenuAdapterConnectsOnlyToTheExistingRunnerBridgeContract()
    {
        var adapter = typeof(PopupStoreManualTestMenu).GetNestedType("ExistingReviewActions", BindingFlags.NonPublic);
        Assert.That(adapter, Is.Not.Null);
        Assert.That(typeof(IPopupStoreManualTestActions).IsAssignableFrom(adapter), Is.True);
        var runner = typeof(PopupStoreVerificationRunner);
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Assert.That(runner.GetMethod("ReadPlannerManualTestStatus", flags).ReturnType, Is.EqualTo(typeof(PopupStoreManualTestStatus)));
        foreach (string name in new[] { "TryStartPlannerManualTest", "TryResetPlannerManualTest" })
        {
            var method = runner.GetMethod(name, flags);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));
            Assert.That(method.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(method.GetParameters()[0].IsOut, Is.True);
            Assert.That(method.GetParameters()[0].ParameterType, Is.EqualTo(typeof(string).MakeByRefType()));
        }
        // No actual menu execution here: the original Stage1 path is verified separately in real Play.
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RealIsolatedOwnerStatusReadsPreserveQuotaQueriesTransactionsAndOtherAccounts(bool consumed)
    {
        using (var owner = new PopupStoreAccessReviewOwner("manual-status-test"))
        {
            owner.Store.RegisterAccount("unrelated-isolated-account");
            if (consumed)
            {
                var access = owner.Store.ReadAccess(owner.Session);
                var request = new PopupStoreBusinessStartRequest(owner.AccountId, "status-read-test", PopupStoreBusinessMode.Rewarded, access.PolicyVersion);
                var transaction = new PopupStoreBusinessStartTransaction(request, owner.Repository, () => owner.Session, new PopupStoreBusinessStartContext());
                Assert.That(transaction.TrySubmit(), Is.True);
                Assert.That(transaction.State, Is.EqualTo(PopupStoreBusinessStartState.Started));
            }
            Assert.That(PopupStoreDailyUsage.TryGetDayKey(owner.AuthorityTime, out string day), Is.True);
            var before = owner.Store.Read(owner.AccountId, day);
            int submissions = owner.Repository.SubmitCalls, queries = owner.Repository.QueryCalls, accessQueries = owner.Repository.AccessQueryCalls;
            long issued = owner.Store.IssuedBusinessCount;
            int clockReads = 0; owner.Store.Clock = () => { clockReads++; return owner.AuthorityTime; };
            for (int i = 0; i < 5; i++)
            {
                var status = new PopupStoreManualTestStatus();
                PopupStoreVerificationRunner.FillPlannerOwnerSnapshot(status, owner);
                Assert.That(status.AccountId, Is.EqualTo(owner.AccountId));
                Assert.That(status.Limit, Is.EqualTo(1)); Assert.That(status.Used, Is.EqualTo(consumed ? 1 : 0));
                Assert.That(status.Remaining, Is.EqualTo(consumed ? 0 : 1));
                PopupStoreManualTestPolicy.FormatStatus(status);
            }
            var after = owner.Store.Read(owner.AccountId, day);
            Assert.That(after.Usage, Is.EqualTo(before.Usage)); Assert.That(after.Starts, Is.EqualTo(before.Starts));
            Assert.That(owner.Store.Read("unrelated-isolated-account", day).Usage, Is.Zero);
            Assert.That(owner.Store.IssuedBusinessCount, Is.EqualTo(issued));
            Assert.That(owner.Repository.SubmitCalls, Is.EqualTo(submissions));
            Assert.That(owner.Repository.QueryCalls, Is.EqualTo(queries));
            Assert.That(owner.Repository.AccessQueryCalls, Is.EqualTo(accessQueries));
            Assert.That(clockReads, Is.Zero, "A status read cannot re-query authority or advance its observed time.");
            Assert.That(owner.Host.Transaction, Is.Null); Assert.That(owner.Host.BindingCount, Is.Zero);
        }
    }

    private static PopupStoreManualTestStatus Phase(string phase)
    {
        if (phase == "empty") return new PopupStoreManualTestStatus();
        if (phase == "pending") return new PopupStoreManualTestStatus { StartPending = true, HasRecord = true };
        if (phase == "foreign-play") return new PopupStoreManualTestStatus { Playing = true };
        var status = new PopupStoreManualTestStatus { Playing = true, HasRecord = true, OwnerPresent = true,
            OriginalMemoryOwner = true, AdmissionBound = true, Remaining = 1, Limit = 1, Used = 0, ClockState = "NotStarted" };
        switch (phase)
        {
            case "bound": break;
            case "running": status.ClockState = "Running"; status.Remaining = 0; status.Used = 1; break;
            case "paused": status.ClockState = "Paused"; status.Remaining = 0; status.Used = 1; break;
            case "used": status.Remaining = 0; status.Used = 1; break;
            case "ended": status.ClockState = "Ended"; status.Remaining = 0; status.Used = 1; break;
            case "stopped": status.Playing = false; status.Stopped = true; status.Remaining = 0; status.Used = 1; break;
            case "before-popup": status.AdmissionBound = false; break;
            default: throw new ArgumentOutOfRangeException(nameof(phase));
        }
        return status;
    }

    private sealed class FakeActions : IPopupStoreManualTestActions
    {
        internal PopupStoreManualTestStatus State = new PopupStoreManualTestStatus();
        internal int ReadCalls, StartCalls, ResetCalls;
        internal bool AcceptDispatch = true, ThrowOnRead, ThrowOnDispatch;
        internal string DispatchReason;
        internal readonly string ForeignRecord = "unrelated-review-must-survive";
        public PopupStoreManualTestStatus ReadStatus()
        {
            ReadCalls++;
            if (ThrowOnRead) throw new InvalidOperationException("private-account-secret");
            return State;
        }
        public bool TryStart(out string reason)
        {
            StartCalls++; reason = DispatchReason;
            if (ThrowOnDispatch) throw new InvalidOperationException("private-account-secret");
            if (!AcceptDispatch) return false;
            State.HasRecord = true; State.StartPending = true; return true;
        }
        public bool TryReset(out string reason)
        {
            ResetCalls++; reason = DispatchReason;
            if (ThrowOnDispatch) throw new InvalidOperationException("private-account-secret");
            if (!AcceptDispatch) return false;
            State = new PopupStoreManualTestStatus(); return true;
        }
    }
}
#endif
