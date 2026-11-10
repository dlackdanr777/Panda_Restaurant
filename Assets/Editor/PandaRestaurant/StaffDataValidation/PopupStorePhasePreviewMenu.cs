#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>Separate explicit planner path; the existing ninety-second test menu remains unchanged.</summary>
public static class PopupStorePhasePreviewMenu
{
    internal const string StartPath = "Tools/Panda Restaurant/Popup Store/6단계 프리뷰 시작";
    internal const string ResetPath = "Tools/Panda Restaurant/Popup Store/6단계 프리뷰 초기화";
    internal const string StatusPath = "Tools/Panda Restaurant/Popup Store/6단계 프리뷰 상태 확인";

    [MenuItem(StartPath)]
    private static void StartPreview()
        => PopupStorePhasePreviewWindow.ShowCommand("phase-preview-start");

    [MenuItem(ResetPath)]
    private static void ResetPreview()
        => PopupStorePhasePreviewWindow.ShowCommand("phase-preview-reset");

    [MenuItem(StatusPath)]
    private static void StatusPreview()
        => PopupStorePhasePreviewWindow.ShowCommand(null);
}

internal interface IPopupStorePhasePreviewActions
{
    PopupStorePhasePreviewEntryState ReadState();
    bool TryStart(out string reason);
    bool TryReset(out string reason);
    bool TryStop(out string reason);
}

internal sealed class PopupStoreExistingPhasePreviewActions : IPopupStorePhasePreviewActions
{
    public PopupStorePhasePreviewEntryState ReadState() => PopupStoreVerificationRunner.ReadPhasePreviewEntryState();
    public bool TryStart(out string reason) => PopupStoreVerificationRunner.TryStartPhasePreview(out reason);
    public bool TryReset(out string reason) => PopupStoreVerificationRunner.TryResetPhasePreview(out reason);
    public bool TryStop(out string reason) => PopupStoreVerificationRunner.TryStopPhasePreview(out reason);
}

/// <summary>The visible controls and explicit request adapter share the same fail-closed dispatch.</summary>
internal static class PopupStorePhasePreviewCommands
{
    internal static string BlockReason(PopupStorePhasePreviewEntryState state, string command)
    {
        if (state == null || state.Policy == null) return "현재 상태를 확인할 수 없어 실행하지 않습니다.";
        var policy = state.Policy;
        if (command == "phase-preview-stop")
            return policy.Playing && !policy.Transitioning && state.ManualPlayReserved
                && (state.PhaseState == "starting" || state.PhaseState == "ready" || state.PhaseState == "failed")
                ? null : "이 창에서 시작한 단계 프리뷰의 원본 메모리 Play만 정상 종료할 수 있습니다. 다른 Play는 중지하지 않습니다.";
        if (command != "phase-preview-start" && command != "phase-preview-reset")
            return "알 수 없는 명령은 실행하지 않습니다.";
        if (state.SdkInitialized || state.LoggedIn)
            return (state.LoggedIn ? "실제 로그인 세션이 남아 있습니다." : "SDK가 초기화된 상태입니다(로그인 세션 없음).")
                + " 시작·초기화를 차단합니다. 검수 초기화로 해결되지 않으므로 사용자 작업을 처리하고 원본 Editor를 정상 재시작하세요. 일반 Play는 누르지 마세요.";
        if (state.RestorePending || state.PhaseOwnerPresent || state.PhaseViewPresent)
            return "단계 프리뷰 소유자 또는 종료 복원이 남아 있습니다. 기존 검수를 정상 종료하고 복원을 기다리세요.";
        if (state.ManualPlayReserved)
            return "원본 메모리 Play 예약이 남아 있습니다. 시작·초기화를 반복하지 말고 기존 종료 복원을 확인하세요.";
        if (!state.DomainReloadEnabled || !state.SceneReloadEnabled)
            return "Domain Reload와 Scene Reload가 모두 필요합니다. 설정은 자동 변경하지 않습니다.";
        if (command == "phase-preview-reset") return PopupStoreManualTestPolicy.ResetBlockReason(policy);
        if (!string.IsNullOrEmpty(state.PhaseState))
            return "기존 단계 프리뷰 기록이 있습니다. 정상 종료·복원 후 '6단계 프리뷰 초기화'를 먼저 실행하세요.";
        // Match the existing bridge's allowance for completed ordinary-review history without mutating observations.
        if (policy.Stopped && !policy.OwnerPresent && !policy.StartPending && !policy.OwnerLost)
            return PopupStoreManualTestPolicy.ResetBlockReason(policy)
                ?? (policy.OriginalMemoryOwner ? "기존 원본 메모리 소유자의 복원을 먼저 완료하세요." : null);
        return PopupStoreManualTestPolicy.StartBlockReason(policy);
    }

    internal static bool TryExecute(IPopupStorePhasePreviewActions actions, string command, out string reason)
    {
        reason = null;
        try
        {
            reason = BlockReason(actions?.ReadState(), command);
            if (reason != null) return false;
            // The real adapter rechecks current safety and ownership immediately before the mutation.
            if (command == "phase-preview-start") return actions.TryStart(out reason);
            if (command == "phase-preview-reset") return actions.TryReset(out reason);
            return actions.TryStop(out reason);
        }
        catch (Exception exception)
        {
            reason = "검수 명령을 완료하지 못했습니다 (" + exception.GetType().Name
                + "). 자동 재시도·Play 종료·SDK 초기화는 실행하지 않습니다.";
            return false;
        }
    }
}

/// <summary>Opening, reloading and refreshing this model only reads observations.</summary>
internal sealed class PopupStorePhasePreviewWindowSession
{
    private readonly IPopupStorePhasePreviewActions actions;
    internal PopupStorePhasePreviewEntryState State { get; private set; }
    internal string LastResult { get; private set; }
    internal bool? LastAccepted { get; private set; }
    internal PopupStorePhasePreviewWindowSession(IPopupStorePhasePreviewActions actions) { this.actions = actions; Refresh(); }
    internal void Refresh()
    {
        try { State = actions?.ReadState(); }
        catch (Exception) { State = null; }
    }
    internal bool ExecuteExplicit(string command)
    {
        bool accepted = PopupStorePhasePreviewCommands.TryExecute(actions, command, out string reason);
        LastResult = reason; LastAccepted = accepted; Refresh();
        return accepted;
    }
}

public sealed class PopupStorePhasePreviewWindow : EditorWindow
{
    private PopupStorePhasePreviewWindowSession session;
    private Vector2 scroll;
    internal static void ShowCommand(string explicitCommand)
    {
        var window = GetWindow<PopupStorePhasePreviewWindow>("팝업스토어 미지급 검수");
        window.Show(); window.Focus(); window.EnsureSession();
        if (!string.IsNullOrEmpty(explicitCommand)) window.ExecuteExplicit(explicitCommand);
        else window.session.Refresh();
    }
    private void EnsureSession()
    { if (session == null) session = new PopupStorePhasePreviewWindowSession(new PopupStoreExistingPhasePreviewActions()); }
    private void OnEnable() { EnsureSession(); session.Refresh(); }
    private void OnInspectorUpdate() { EnsureSession(); session.Refresh(); Repaint(); }
    private void ExecuteExplicit(string command)
    {
        bool accepted = session.ExecuteExplicit(command);
        string message = "POPUP_STORE_PHASE_PREVIEW_" + (accepted ? "COMMAND " : "REFUSED ") + command + "; " + session.LastResult;
        if (accepted) Debug.Log(message); else Debug.LogWarning(message + "\n" + PopupStorePhasePreviewEntryGuide.Format(session.State));
        Repaint();
    }
    private void OnGUI()
    {
        EnsureSession();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("6단계 프리뷰 · 검수용 / 실제 미지급", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Unity 상단 일반 Play는 누르지 마세요. 아래 시작 버튼이 원본 Stage1 격리 메모리 Play를 예약합니다. 실제 계정·서버 저장·다이아 지급은 사용하지 않습니다.", MessageType.Info);
        var state = session.State;
        var policy = state?.Policy;
        EditorGUILayout.LabelField("SDK 초기화", state == null ? "확인 불가" : state.SdkInitialized.ToString());
        EditorGUILayout.LabelField("로그인 세션", state == null ? "확인 불가" : state.LoggedIn.ToString());
        EditorGUILayout.LabelField("Play", policy == null ? "확인 불가" : policy.Transitioning ? "전환 중" : policy.Playing ? "ON" : "OFF");
        EditorGUILayout.LabelField("프리뷰 기록 / 시작 예약", state == null ? "확인 불가"
            : (string.IsNullOrEmpty(state.PhaseState) ? "없음" : state.PhaseState) + " / " + state.ManualPlayReserved);
        EditorGUILayout.LabelField("소유자 / 화면 연결 / 종료 복원", state == null ? "확인 불가"
            : state.PhaseOwnerPresent + " / " + state.PhaseViewPresent + " / " + state.RestorePending);
        EditorGUILayout.HelpBox(PopupStorePhasePreviewEntryGuide.NextAction(state),
            state == null || state.SdkInitialized || state.LoggedIn ? MessageType.Warning : MessageType.Info);
        if (!string.IsNullOrEmpty(session.LastResult))
            EditorGUILayout.HelpBox("마지막 요청: " + session.LastResult, session.LastAccepted == true ? MessageType.Info : MessageType.Warning);
        DrawCommand("6단계 프리뷰 시작", "phase-preview-start");
        DrawCommand("정상 종료 (Play 중지)", "phase-preview-stop");
        DrawCommand("6단계 프리뷰 초기화", "phase-preview-reset");
        if (GUILayout.Button("상태 새로고침 (조회만)")) session.Refresh();
        if (GUILayout.Button("기존 검수창 열기"))
        {
            // Do not call OpenManual(): that menu deliberately resets the existing window position.
            var manual = GetWindow<CustomerPartyStage1Preview>("일행 수동 검수");
            manual.Show(); manual.Focus();
        }
        EditorGUILayout.HelpBox("초기화는 종료된 프리뷰 기록만 정리합니다. SDK·로그인 세션이 남아 있으면 사용자 작업을 직접 처리한 뒤 원본 Editor를 정상 재시작해야 합니다. 초기화를 반복하지 마세요.\n진입: 기존 검수창의 카메라: VIP 홀 → Game 빈 배경을 아래로 드래그 → 3층 지도 → 패스트푸드 아이콘.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }
    private void DrawCommand(string label, string command)
    {
        string reason = PopupStorePhasePreviewCommands.BlockReason(session.State, command);
        using (new EditorGUI.DisabledScope(reason != null))
            if (GUILayout.Button(new GUIContent(label, reason ?? label))) ExecuteExplicit(command);
    }
}

/// <summary>Read-only diagnostics. These observations never authorize or repair a review session.</summary>
internal sealed class PopupStorePhasePreviewEntryState
{
    internal PopupStoreManualTestStatus Policy;
    internal bool SdkInitialized, LoggedIn, DomainReloadEnabled, SceneReloadEnabled;
    internal bool PhaseOwnerPresent, PhaseViewPresent, PhaseOwnerRecorded, RestorePending, ManualPlayReserved;
    internal string PhaseState;
}

internal static class PopupStorePhasePreviewEntryGuide
{
    internal static string Format(PopupStorePhasePreviewEntryState state)
    {
        if (state == null || state.Policy == null)
            return "진입 상태: 확인 불가. 시작·초기화를 실행하지 말고 '6단계 프리뷰 상태 확인'으로 다시 확인하세요.";
        var policy = state.Policy;
        return "SDK 초기화=" + state.SdkInitialized + "; 로그인 세션=" + state.LoggedIn
            + "; Play=" + (policy.Transitioning ? "전환 중" : policy.Playing ? "ON" : "OFF")
            + "; Domain Reload=" + state.DomainReloadEnabled + "; Scene Reload=" + state.SceneReloadEnabled
            + "\n프리뷰 기록=" + (string.IsNullOrEmpty(state.PhaseState) ? "없음" : state.PhaseState)
            + "; 원본 Play 예약=" + state.ManualPlayReserved
            + "; 프리뷰 소유자=" + state.PhaseOwnerPresent + "; 화면 연결=" + state.PhaseViewPresent
            + "; 소유자 생성 기록=" + state.PhaseOwnerRecorded + "; 종료 복원 대기=" + state.RestorePending
            + "; 원본 메모리 소유자=" + policy.OriginalMemoryOwner + "; 다른 Backend 소유자=" + policy.OtherBackendOwner
            + "\n다음 조작: " + NextAction(state)
            + "\n'6단계 프리뷰 초기화'는 종료된 프리뷰 기록만 지웁니다. SDK·로그인 세션·인증정보·실계정 저장은 정리하지 않습니다.";
    }

    internal static string NextAction(PopupStorePhasePreviewEntryState state)
    {
        if (state == null || state.Policy == null) return "상태를 확인할 수 없어 안전하게 대기합니다.";
        var policy = state.Policy;
        bool activePhase = state.PhaseState == "starting" || state.PhaseState == "ready";
        if (policy.Playing)
        {
            if (!state.SdkInitialized && !state.LoggedIn && !policy.LiveAccount
                && !policy.OtherBackendOwner && activePhase && !policy.OwnerLost)
            {
                if (state.PhaseState == "starting")
                    return "현재 원본 메모리 프리뷰가 준비 중입니다. 일반 Play나 시작 메뉴를 다시 누르지 마세요. 기존 검수창의 카메라: VIP 홀 → 3층 지도 → 패스트푸드 아이콘으로 이동하세요.";
                if (policy.OriginalMemoryOwner && state.PhaseOwnerPresent && state.PhaseViewPresent)
                    return "현재 격리 프리뷰를 유지하세요. 승인 튜토리얼 마지막 페이지에서 플레이합니다. 새 실행은 기존 검수창의 '테스트 종료 (Play 중지)' 후 준비합니다.";
            }
            return "현재 Play에 검수 저장소를 연결하거나 초기화하지 않습니다. 기존 실행을 정상 종료한 뒤 '6단계 프리뷰 상태 확인'을 실행하세요.";
        }
        if (state.RestorePending || policy.Transitioning)
            return "Play 전환 또는 종료 복원이 끝날 때까지 기다린 뒤 상태를 다시 확인하세요. 초기화를 반복하지 마세요.";
        if (policy.Compiling || policy.Updating) return "임포트·컴파일이 끝난 뒤 상태를 다시 확인하세요.";
        if (policy.CompilationFailed) return "Console의 C# 컴파일 오류를 해결한 뒤 상태를 다시 확인하세요.";
        if (policy.RegressionRunning) return "실행 중인 자동검사가 끝난 뒤 상태를 다시 확인하세요.";
        if (state.SdkInitialized || state.LoggedIn || policy.LiveAccount || policy.OtherBackendOwner)
        {
            string cause = state.LoggedIn ? "로그인 세션이 확인됩니다."
                : state.SdkInitialized ? "SDK만 초기화됐고 로그인 세션은 확인되지 않습니다."
                : policy.OtherBackendOwner ? "다른 Backend 소유자가 남아 있습니다."
                : "SDK·로그인 상태 판독이 일치하지 않아 안전 확인이 필요합니다.";
            return cause + " 프리뷰 초기화로 해결되지 않습니다. 저장할 사용자 작업을 직접 처리하고 Unity Editor를 정상 종료한 뒤 같은 원본을 다시 여세요. 일반 FirstLoadingScene Play를 누르지 말고 '6단계 프리뷰 상태 확인' → 안전 상태에서 '6단계 프리뷰 시작' 순서로 진행하세요.";
        }
        if (state.PhaseOwnerPresent || state.PhaseViewPresent || policy.OwnerPresent || policy.OwnerLost
            || policy.OriginalMemoryOwner || policy.StartPending || state.ManualPlayReserved || activePhase)
            return "이전 검수 소유자 또는 시작 예약이 남아 있습니다. 기존 테스트 종료와 복원을 먼저 완료하세요. Play OFF에서도 유지되면 사용자 작업을 직접 정리한 뒤 원본 Editor를 정상 재시작하세요. 소유자를 강제 삭제하지 않습니다.";
        if (policy.DirtyScene) return "저장되지 않은 Scene 변경을 사용자가 먼저 처리하세요. 자동 저장·폐기는 하지 않습니다.";
        if (policy.PrefabStage) return "사용자 작업을 보존하고 Prefab 편집을 종료한 뒤 실행하세요.";
        if (policy.ReloadDisabled || !state.DomainReloadEnabled || !state.SceneReloadEnabled)
            return "Project Settings → Editor → Enter Play Mode Settings에서 Reload Domain과 Reload Scene이 모두 활성인지 확인하세요. 설정은 자동 변경하지 않습니다.";
        if (policy.PendingRequest) return "기존 검수 요청이 끝나는지 확인하세요. 요청을 덮어쓰거나 지우지 않습니다.";
        if (policy.OtherReview) return "다른 검수 실행을 먼저 정상 종료하고 복원이 끝난 뒤 다시 확인하세요.";
        if (!string.IsNullOrEmpty(state.PhaseState))
            return "종료된 단계 프리뷰 기록만 남아 있습니다. '6단계 프리뷰 초기화' → '6단계 프리뷰 시작'으로 새 격리 실행을 준비하세요.";
        if (policy.HasRecord && !policy.Stopped)
            return "일반 90초 검수 기록을 먼저 해당 메뉴에서 정상 정리하세요. 단계 프리뷰 초기화는 다른 검수 기록을 지우지 않습니다.";
        return "원본 Editor의 Play OFF 상태에서 '6단계 프리뷰 시작'을 누르세요. 일반 FirstLoadingScene Play는 누르지 않습니다. 메뉴가 원본 Stage1 격리 메모리 실행을 먼저 예약합니다.";
    }
}
#endif
