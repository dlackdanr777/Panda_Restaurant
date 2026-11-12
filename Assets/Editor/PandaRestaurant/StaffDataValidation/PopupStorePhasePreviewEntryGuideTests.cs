#if UNITY_EDITOR
using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Pure entry guidance and the existing refusal policy; never enters Play or resets SDK/session state.</summary>
public sealed class PopupStorePhasePreviewEntryGuideTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void SdkAndLoginAreReportedSeparatelyAndExistingPolicyStillRefusesEither(bool sdk, bool login)
    {
        var state = Safe();
        state.SdkInitialized = sdk; state.LoggedIn = login; state.Policy.LiveAccount = sdk || login;
        string text = PopupStorePhasePreviewEntryGuide.Format(state);
        Assert.That(text, Does.Contain("SDK 초기화=" + sdk).And.Contain("로그인 세션=" + login));
        Assert.That(PopupStoreManualTestPolicy.StartBlockReason(state.Policy) == null, Is.EqualTo(!sdk && !login));
        Assert.That(PopupStoreManualTestPolicy.ResetBlockReason(state.Policy) == null, Is.EqualTo(!sdk && !login));
        if (login) Assert.That(text, Does.Contain("로그인 세션이 확인됩니다."));
        else if (sdk) Assert.That(text, Does.Contain("SDK만 초기화됐고 로그인 세션은 확인되지 않습니다."));
        if (sdk || login)
            Assert.That(text, Does.Contain("프리뷰 초기화로 해결되지 않습니다").And.Contain("정상 종료")
                .And.Contain("일반 FirstLoadingScene Play를 누르지 말고"));
    }

    [TestCase("Playing", "현재 Play에 검수 저장소를 연결하거나 초기화하지 않습니다")]
    [TestCase("Transitioning", "Play 전환 또는 종료 복원")]
    [TestCase("Compiling", "임포트·컴파일")]
    [TestCase("Updating", "임포트·컴파일")]
    [TestCase("CompilationFailed", "C# 컴파일 오류")]
    [TestCase("RegressionRunning", "자동검사")]
    [TestCase("DirtyScene", "저장되지 않은 Scene")]
    [TestCase("PrefabStage", "Prefab 편집")]
    [TestCase("ReloadDisabled", "Reload Domain과 Reload Scene")]
    [TestCase("OtherBackendOwner", "다른 Backend 소유자")]
    [TestCase("PendingRequest", "기존 검수 요청")]
    [TestCase("OtherReview", "다른 검수 실행")]
    public void ExistingUnsafeStateRemainsBlockedAndHasSpecificNextAction(string flag, string expected)
    {
        var state = Safe();
        typeof(PopupStoreManualTestStatus).GetField(flag).SetValue(state.Policy, true);
        Assert.That(PopupStoreManualTestPolicy.StartBlockReason(state.Policy), Is.Not.Null.And.Not.Empty);
        Assert.That(PopupStoreManualTestPolicy.ResetBlockReason(state.Policy), Is.Not.Null.And.Not.Empty);
        Assert.That(PopupStorePhasePreviewEntryGuide.NextAction(state), Does.Contain(expected));
        Assert.That((bool)typeof(PopupStoreManualTestStatus).GetField(flag).GetValue(state.Policy), Is.True);
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(false, false)]
    public void BothReloadRequirementsRemainVisibleWithoutChangingEditorSettings(bool domain, bool scene)
    {
        var state = Safe(); state.DomainReloadEnabled = domain; state.SceneReloadEnabled = scene;
        state.Policy.ReloadDisabled = !domain || !scene;
        string text = PopupStorePhasePreviewEntryGuide.Format(state);
        Assert.That(text, Does.Contain("Domain Reload=" + domain).And.Contain("Scene Reload=" + scene));
        Assert.That(text, Does.Contain("설정은 자동 변경하지 않습니다"));
        Assert.That(PopupStoreManualTestPolicy.StartBlockReason(state.Policy), Is.Not.Null);
    }

    [TestCase("stopped")]
    [TestCase("failed")]
    public void CompletedPhaseRecordOnlySuggestsExplicitPhaseReset(string record)
    {
        var state = Safe(); state.PhaseState = record; state.PhaseOwnerRecorded = true;
        string text = PopupStorePhasePreviewEntryGuide.Format(state);
        Assert.That(text, Does.Contain("종료된 단계 프리뷰 기록만 남아 있습니다"));
        Assert.That(text, Does.Contain("'6단계 프리뷰 초기화' → '6단계 프리뷰 시작'"));
        Assert.That(text, Does.Contain("SDK·로그인 세션·인증정보·실계정 저장은 정리하지 않습니다"));
        Assert.That(state.PhaseState, Is.EqualTo(record)); Assert.That(state.PhaseOwnerRecorded, Is.True);
    }

    [Test]
    public void SdkWithAStoppedRecordRequiresRestartInsteadOfAnotherReset()
    {
        var state = Safe(); state.PhaseState = "stopped"; state.PhaseOwnerRecorded = true;
        state.SdkInitialized = state.Policy.LiveAccount = true;
        string next = PopupStorePhasePreviewEntryGuide.NextAction(state);
        Assert.That(next, Does.Contain("프리뷰 초기화로 해결되지 않습니다"));
        Assert.That(next, Does.Not.Contain("'6단계 프리뷰 초기화' → '6단계 프리뷰 시작'"));
        Assert.That(PopupStoreManualTestPolicy.ResetBlockReason(state.Policy), Is.Not.Null);
    }

    [TestCase("OwnerPresent")]
    [TestCase("OwnerLost")]
    [TestCase("OriginalMemoryOwner")]
    [TestCase("StartPending")]
    public void RemainingOwnerOrReservationDoesNotSuggestForcedReset(string flag)
    {
        var state = Safe(); typeof(PopupStoreManualTestStatus).GetField(flag).SetValue(state.Policy, true);
        Assert.That(PopupStorePhasePreviewEntryGuide.NextAction(state), Does.Contain("종료와 복원")
            .And.Contain("소유자를 강제 삭제하지 않습니다"));
        Assert.That(PopupStoreManualTestPolicy.StartBlockReason(state.Policy), Is.Not.Null);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void APhaseOwnerOrViewRemainingAfterPlayMustFinishRestoration(bool owner, bool view)
    {
        var state = Safe(); state.PhaseOwnerPresent = owner; state.PhaseViewPresent = view;
        Assert.That(PopupStorePhasePreviewEntryGuide.NextAction(state), Does.Contain("종료와 복원"));
    }

    [Test]
    public void RestorationPendingWaitsWithoutRepeatedInitialization()
    {
        var state = Safe(); state.PhaseState = "stopped"; state.RestorePending = true;
        Assert.That(PopupStorePhasePreviewEntryGuide.NextAction(state), Does.Contain("복원이 끝날 때까지")
            .And.Contain("초기화를 반복하지 마세요"));
    }

    [Test]
    public void ReadyIsolatedPlayDirectsToTheExistingTutorialWithoutStartingAgain()
    {
        var state = Safe(); state.PhaseState = "ready"; state.Policy.Playing = true;
        state.Policy.OriginalMemoryOwner = state.PhaseOwnerPresent = state.PhaseViewPresent = true;
        Assert.That(PopupStorePhasePreviewEntryGuide.NextAction(state), Does.StartWith("현재 격리 프리뷰를 유지하세요")
            .And.Contain("승인 튜토리얼 마지막 페이지"));
        Assert.That(PopupStoreManualTestPolicy.StartBlockReason(state.Policy), Is.Not.Null);
        Assert.That(PopupStoreManualTestPolicy.ResetBlockReason(state.Policy), Is.Not.Null);
    }

    [Test]
    public void ALoginDetectedInReadyPlayNeverGetsTheLocalPlayInstruction()
    {
        var state = Safe(); state.PhaseState = "ready"; state.Policy.Playing = true;
        state.Policy.OriginalMemoryOwner = state.PhaseOwnerPresent = state.PhaseViewPresent = true;
        state.LoggedIn = state.Policy.LiveAccount = true;
        Assert.That(PopupStorePhasePreviewEntryGuide.NextAction(state), Does.Contain("현재 Play에 검수 저장소를 연결하거나 초기화하지 않습니다")
            .And.Not.Contain("승인 튜토리얼 마지막 페이지"));
    }

    [Test]
    public void StartingPlayKeepsTheExistingRunAndNamesTheObservedNavigation()
    {
        var state = Safe(); state.PhaseState = "starting"; state.Policy.Playing = true;
        Assert.That(PopupStorePhasePreviewEntryGuide.NextAction(state), Does.Contain("다시 누르지 마세요")
            .And.Contain("카메라: VIP 홀 → 3층 지도 → 패스트푸드 아이콘"));
    }

    [Test]
    public void CleanEditorDoesNotNeedAFirstLoadingScenePlayOrASdkReset()
    {
        var state = Safe();
        Assert.That(PopupStorePhasePreviewEntryGuide.NextAction(state), Does.Contain("Play OFF")
            .And.Contain("일반 FirstLoadingScene Play는 누르지 않습니다")
            .And.Contain("Stage1 격리 메모리 실행을 먼저 예약"));
    }

    [Test]
    public void MissingStatusFailsClosedAndPrivateStatusPayloadIsNeverPrinted()
    {
        Assert.That(PopupStorePhasePreviewEntryGuide.Format(null), Does.Contain("확인 불가"));
        Assert.That(PopupStorePhasePreviewEntryGuide.Format(new PopupStorePhasePreviewEntryState()), Does.Contain("확인 불가"));
        var state = Safe(); state.Policy.AccountId = "private-account";
        state.Policy.LocalSessionId = "private-session"; state.Policy.Detail = "private-detail";
        Assert.That(PopupStorePhasePreviewEntryGuide.Format(state), Does.Not.Contain("private-"));
    }

    [Test]
    public void ObservingRealEditorStateDoesNotChangeSdkReloadOrPreviewSessionMetadata()
    {
        bool sdk = BackEnd.Backend.IsInitialized, login = BackEnd.Backend.IsLogin;
        bool playing = EditorApplication.isPlaying, enabled = EditorSettings.enterPlayModeOptionsEnabled;
        var options = EditorSettings.enterPlayModeOptions;
        const string phase = "Panda.PopupStore.PhasePreview.State";
        const string owner = "Panda.PopupStore.PhasePreview.OwnerCreated";
        string phaseBefore = SessionState.GetString(phase, ""); bool ownerBefore = SessionState.GetBool(owner, false);
        var observed = PopupStoreVerificationRunner.ReadPhasePreviewEntryState();
        string text = PopupStorePhasePreviewEntryGuide.Format(observed);
        Assert.That(text, Is.Not.Null.And.Not.Empty);
        Assert.That(observed.SdkInitialized, Is.EqualTo(sdk)); Assert.That(observed.LoggedIn, Is.EqualTo(login));
        Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(sdk)); Assert.That(BackEnd.Backend.IsLogin, Is.EqualTo(login));
        Assert.That(EditorApplication.isPlaying, Is.EqualTo(playing));
        Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.EqualTo(enabled));
        Assert.That(EditorSettings.enterPlayModeOptions, Is.EqualTo(options));
        Assert.That(SessionState.GetString(phase, ""), Is.EqualTo(phaseBefore));
        Assert.That(SessionState.GetBool(owner, false), Is.EqualTo(ownerBefore));
    }

    [Test]
    public void VisibleWindowEnableReloadAndRefreshOnlyObserveAndNeverDispatchCommands()
    {
        var actions = new FakeActions();
        var model = new PopupStorePhasePreviewWindowSession(actions);
        var window = ScriptableObject.CreateInstance<PopupStorePhasePreviewWindow>();
        bool playing = EditorApplication.isPlaying, sdk = BackEnd.Backend.IsInitialized, login = BackEnd.Backend.IsLogin;
        bool reserved = SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false);
        var selection = Selection.objects;
        try
        {
            typeof(PopupStorePhasePreviewWindow).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, model);
            for (int i = 0; i < 3; i++)
            {
                typeof(PopupStorePhasePreviewWindow).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                typeof(PopupStorePhasePreviewWindow).GetMethod("OnInspectorUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                model.Refresh();
            }
            Assert.That(actions.Reads, Is.EqualTo(10));
            Assert.That(actions.Starts + actions.Resets + actions.Stops, Is.Zero);
            Assert.That(model.LastAccepted, Is.Null); Assert.That(model.LastResult, Is.Null);
            Assert.That(EditorApplication.isPlaying, Is.EqualTo(playing));
            Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(sdk)); Assert.That(BackEnd.Backend.IsLogin, Is.EqualTo(login));
            Assert.That(SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false), Is.EqualTo(reserved));
            Assert.That(Selection.objects, Is.EqualTo(selection));
        }
        finally { Object.DestroyImmediate(window); }
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void VisibleStartAndResetRejectSdkOrLoginEvenIfTheOlderCombinedObservationDisagrees(bool sdk, bool login)
    {
        var actions = new FakeActions(); actions.State.SdkInitialized = sdk; actions.State.LoggedIn = login;
        Assert.That(actions.State.Policy.LiveAccount, Is.False);
        var model = new PopupStorePhasePreviewWindowSession(actions);
        foreach (string command in new[] { "phase-preview-start", "phase-preview-reset" })
        {
            Assert.That(PopupStorePhasePreviewCommands.BlockReason(model.State, command), Is.Not.Null);
            Assert.That(model.ExecuteExplicit(command), Is.False);
            Assert.That(model.LastResult, Does.Contain(login ? "실제 로그인 세션" : "SDK가 초기화")
                .And.Contain("원본 Editor를 정상 재시작").And.Contain("일반 Play는 누르지 마세요"));
        }
        Assert.That(actions.Starts + actions.Resets + actions.Stops, Is.Zero);
        Assert.That(actions.State.SdkInitialized, Is.EqualTo(sdk)); Assert.That(actions.State.LoggedIn, Is.EqualTo(login));
    }

    [TestCase("phase-preview-start")]
    [TestCase("phase-preview-reset")]
    [TestCase("phase-preview-stop")]
    public void AnExplicitControlDispatchesExactlyOnceAndSubsequentObservationDoesNotRepeatIt(string command)
    {
        var actions = new FakeActions();
        if (command == "phase-preview-stop")
        { actions.State.PhaseState = "ready"; actions.State.ManualPlayReserved = actions.State.Policy.Playing = true; }
        var model = new PopupStorePhasePreviewWindowSession(actions);
        Assert.That(model.ExecuteExplicit(command), Is.True);
        for (int i = 0; i < 5; i++) model.Refresh();
        Assert.That(actions.Starts, Is.EqualTo(command == "phase-preview-start" ? 1 : 0));
        Assert.That(actions.Resets, Is.EqualTo(command == "phase-preview-reset" ? 1 : 0));
        Assert.That(actions.Stops, Is.EqualTo(command == "phase-preview-stop" ? 1 : 0));
        Assert.That(model.LastResult, Is.EqualTo("accepted " + command)); Assert.That(model.LastAccepted, Is.True);
    }

    [TestCase("starting", true)]
    [TestCase("ready", true)]
    [TestCase("failed", true)]
    [TestCase("", false)]
    [TestCase("stopped", false)]
    public void StopRequiresActivePhaseOwnershipInAdditionToAnOriginalManualPlayReservation(string phase, bool accepted)
    {
        var actions = new FakeActions(); actions.State.PhaseState = phase;
        actions.State.ManualPlayReserved = actions.State.Policy.Playing = true;
        Assert.That(PopupStorePhasePreviewCommands.TryExecute(actions, "phase-preview-stop", out string reason), Is.EqualTo(accepted));
        Assert.That(actions.Stops, Is.EqualTo(accepted ? 1 : 0));
        if (!accepted) Assert.That(reason, Does.Contain("다른 Play는 중지하지 않습니다"));
    }

    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public void PhaseRecordAloneCannotStopAnUnreservedOrStoppedOrTransitioningPlay(bool reserved, bool playing, bool transition)
    {
        var actions = new FakeActions(); actions.State.PhaseState = "ready";
        actions.State.ManualPlayReserved = reserved; actions.State.Policy.Playing = playing; actions.State.Policy.Transitioning = transition;
        Assert.That(PopupStorePhasePreviewCommands.TryExecute(actions, "phase-preview-stop", out _), Is.False);
        Assert.That(actions.Stops, Is.Zero);
    }

    [Test]
    public void AStoppedOrdinaryReviewHistoryIsPreservedWhenTheExistingPhaseBridgeAllowsANewStart()
    {
        var actions = new FakeActions(); actions.State.Policy.Stopped = actions.State.Policy.HasRecord = actions.State.Policy.AdmissionBound = true;
        Assert.That(PopupStorePhasePreviewCommands.TryExecute(actions, "phase-preview-start", out _), Is.True);
        Assert.That(actions.Starts, Is.EqualTo(1));
        Assert.That(actions.State.Policy.Stopped && actions.State.Policy.HasRecord && actions.State.Policy.AdmissionBound, Is.True);
        actions.State.Policy.OriginalMemoryOwner = true;
        Assert.That(PopupStorePhasePreviewCommands.TryExecute(actions, "phase-preview-start", out _), Is.False);
        Assert.That(actions.Starts, Is.EqualTo(1));
    }

    [Test]
    public void StateReadFailureAndUnknownCommandNeverInvokeAnActionOrAutomaticRecovery()
    {
        var actions = new FakeActions { ThrowOnRead = true };
        var model = new PopupStorePhasePreviewWindowSession(actions);
        Assert.That(model.State, Is.Null);
        Assert.That(model.ExecuteExplicit("phase-preview-start"), Is.False);
        Assert.That(model.LastResult, Does.Contain("자동 재시도·Play 종료·SDK 초기화는 실행하지 않습니다"));
        actions.ThrowOnRead = false;
        Assert.That(model.ExecuteExplicit("unknown"), Is.False);
        Assert.That(actions.Starts + actions.Resets + actions.Stops, Is.Zero);
    }

    [Test]
    public void AdapterRejectionAfterAReadyObservationIsShownWithoutRetryOrCleanup()
    {
        var actions = new FakeActions { Accept = false };
        var model = new PopupStorePhasePreviewWindowSession(actions);
        Assert.That(model.ExecuteExplicit("phase-preview-start"), Is.False);
        model.Refresh();
        Assert.That(actions.Starts, Is.EqualTo(1)); Assert.That(actions.Resets + actions.Stops, Is.Zero);
        Assert.That(model.LastResult, Is.EqualTo("safety changed")); Assert.That(model.LastAccepted, Is.False);
    }

    private sealed class FakeActions : IPopupStorePhasePreviewActions
    {
        internal readonly PopupStorePhasePreviewEntryState State = Safe();
        internal int Reads, Starts, Resets, Stops;
        internal bool ThrowOnRead, Accept = true;
        public PopupStorePhasePreviewEntryState ReadState()
        { Reads++; if (ThrowOnRead) throw new System.InvalidOperationException(); return State; }
        public bool TryStart(out string reason) { Starts++; return Result("phase-preview-start", out reason); }
        public bool TryReset(out string reason) { Resets++; return Result("phase-preview-reset", out reason); }
        public bool TryStop(out string reason) { Stops++; return Result("phase-preview-stop", out reason); }
        private bool Result(string command, out string reason) { reason = Accept ? "accepted " + command : "safety changed"; return Accept; }
    }

    private static PopupStorePhasePreviewEntryState Safe() => new PopupStorePhasePreviewEntryState {
        Policy = new PopupStoreManualTestStatus(), PhaseState = "",
        DomainReloadEnabled = true, SceneReloadEnabled = true
    };
}
#endif
