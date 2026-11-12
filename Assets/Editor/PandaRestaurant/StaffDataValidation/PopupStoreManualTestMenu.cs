#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

internal interface IPopupStoreManualTestActions
{
    PopupStoreManualTestStatus ReadStatus();
    bool TryStart(out string reason);
    bool TryReset(out string reason);
}

/// <summary>Read-only Editor observations supplied by the existing verification owner.</summary>
[Serializable]
internal sealed class PopupStoreManualTestStatus
{
    public bool Playing, Transitioning, Compiling, Updating, CompilationFailed, RegressionRunning;
    public bool LiveAccount, DirtyScene, PrefabStage, ReloadDisabled, OtherBackendOwner;
    public bool PendingRequest, OtherReview, HasRecord, OwnerPresent, OwnerLost, OriginalMemoryOwner;
    public bool StartPending, Stopped, AdmissionBound;
    public int Remaining = -1, Limit = -1, Used = -1;
    public string AccountId, LocalSessionId, ClockState, Detail;
}

/// <summary>Editor command gates only. This does not decide admission, rewards, or game rules.</summary>
internal static class PopupStoreManualTestPolicy
{
    internal static string StartBlockReason(PopupStoreManualTestStatus status)
    {
        string blocked = IdleBlockReason(status);
        if (blocked != null) return blocked;
        if (status.HasRecord || status.OwnerPresent || status.OwnerLost || status.AdmissionBound || status.StartPending)
            return "기존 검수 기록이 있습니다. Play 종료 후 '테스트 초기화'를 먼저 실행하세요.";
        if (status.OriginalMemoryOwner)
            return "기존 원본 메모리 검수 소유자가 남아 있어 새 테스트를 시작하지 않습니다.";
        return null;
    }

    internal static string ResetBlockReason(PopupStoreManualTestStatus status)
        => IdleBlockReason(status);

    private static string IdleBlockReason(PopupStoreManualTestStatus status)
    {
        if (status == null) return "Editor 상태를 확인할 수 없어 안전하게 차단했습니다.";
        if (status.Playing) return "Play 중에는 시작·초기화를 실행하지 않습니다. 기존 검수창의 '테스트 종료 (Play 중지)'로 먼저 종료하세요.";
        if (status.Transitioning) return "Play 전환 중입니다. 전환이 끝난 뒤 다시 실행하세요.";
        if (status.Compiling || status.Updating) return "임포트·컴파일이 진행 중입니다. 완료 후 실행하세요.";
        if (status.CompilationFailed) return "C# 컴파일 오류를 해결한 뒤 실행하세요.";
        if (status.RegressionRunning) return "기존 자동검사가 진행 중입니다. 검사 완료 후 실행하세요.";
        if (status.LiveAccount) return "실제 SDK·로그인 상태가 있어 검수 시작·초기화를 차단했습니다.";
        if (status.OtherBackendOwner) return "다른 Backend 소유자가 있어 검수 데이터를 연결하거나 초기화하지 않습니다.";
        if (status.DirtyScene) return "저장되지 않은 Scene 변경이 있습니다. 사용자가 먼저 Scene 상태를 정리하세요. 자동 저장·폐기는 하지 않습니다.";
        if (status.PrefabStage) return "Prefab 편집 중입니다. 사용자가 먼저 Prefab 편집을 종료하세요.";
        if (status.ReloadDisabled) return "원본 검수에는 Domain Reload와 Scene Reload가 모두 필요합니다. 설정을 자동 변경하지 않습니다.";
        if (status.PendingRequest) return "기존 검수 요청이 대기 중입니다. 요청을 덮어쓰거나 삭제하지 않습니다.";
        if (status.OtherReview) return "다른 검수가 진행 또는 준비 중입니다. 해당 검수를 먼저 정상 종료하세요.";
        return null;
    }

    internal static string FormatStatus(PopupStoreManualTestStatus status)
    {
        if (status == null)
            return "상태: 안전 차단\n영업권: 미확인\nEditor 상태를 확인할 수 없습니다. 시작·초기화는 실행하지 않았습니다.";

        string label;
        bool unsafeState = status.OwnerLost || status.LiveAccount || status.OtherBackendOwner || status.OtherReview
            || status.CompilationFailed || status.DirtyScene || status.PrefabStage || status.ReloadDisabled
            || status.RegressionRunning || status.Compiling || status.Updating || status.PendingRequest;
        if (unsafeState) label = "안전 차단";
        else if (status.StartPending || status.Transitioning) label = "준비 중";
        else if (status.Stopped) label = "종료됨";
        else if (status.Playing && status.OriginalMemoryOwner && status.OwnerPresent && status.HasRecord && !status.AdmissionBound) label = "준비 중";
        else if (status.Playing && (!status.OriginalMemoryOwner || !status.OwnerPresent || !status.AdmissionBound)) label = "안전 차단";
        else if (status.ClockState == "Running" || status.ClockState == "Paused") label = "진행 중";
        else if (status.HasRecord && (status.Remaining == 0 || status.ClockState == "Ended" || status.ClockState == "Abandoned")) label = "사용 완료";
        else if (status.AdmissionBound && status.OwnerPresent && status.OriginalMemoryOwner && status.Remaining > 0) label = "준비됨";
        else label = "미준비";

        string quota = status.Remaining >= 0 && status.Limit >= 0
            ? status.Remaining + "/" + status.Limit + (status.Used >= 0 ? " · 사용 " + status.Used + "회" : "")
            : "미확인";
        string play = status.Transitioning ? "전환 중" : status.Playing ? "ON" : "OFF";
        string detail = status.Detail;
        // Internal isolated account/session identities are unnecessary in the planner-facing instructions.
        if (!string.IsNullOrEmpty(detail))
        {
            if (!string.IsNullOrEmpty(status.AccountId)) detail = detail.Replace(status.AccountId, "(검수 계정)");
            if (!string.IsNullOrEmpty(status.LocalSessionId)) detail = detail.Replace(status.LocalSessionId, "(로컬 영업)");
        }
        return "상태: " + label + "\n영업권: " + quota + "\nPlay: " + play + " · 검수 기록: " + (status.HasRecord ? "있음" : "없음")
            + (string.IsNullOrEmpty(detail) ? "" : "\n안내: " + detail)
            + (label == "준비 중" && !status.AdmissionBound ? "\n영업권 준비 확인은 팝업 진입 후 가능합니다." : "")
            + "\n검수 정책: 원본 90초 · 일반 R1 추첨 · 사용자 수동 플레이 · 검수용/미지급 · 실제 서버 연결·계정 반영·지급 금지"
            + "\n진입: Tools → Panda Restaurant → Customer Parties → Manual Play (Original) 검수창의 '카메라: VIP 홀' → Game 빈 배경을 아래로 드래그 → 3층 벽 지도 → 패스트푸드 아이콘 → '영업 시작'."
            + "\n반복: 검수창의 '테스트 종료 (Play 중지)' → '테스트 초기화' → '테스트 시작'. 팝업을 닫아도 사용한 영업권은 복원되지 않습니다.";
    }
}

/// <summary>The same bounded commands are exercised with an isolated adapter in synchronous EditMode tests.</summary>
internal static class PopupStoreManualTestCommands
{
    internal static bool TryStart(IPopupStoreManualTestActions actions, out string reason)
        => Execute(actions, false, out reason);
    internal static bool TryReset(IPopupStoreManualTestActions actions, out string reason)
        => Execute(actions, true, out reason);

    private static bool Execute(IPopupStoreManualTestActions actions, bool reset, out string reason)
    {
        reason = null;
        if (actions == null) { reason = "검수 실행기를 찾을 수 없어 실행하지 않았습니다."; return false; }
        try
        {
            var status = actions.ReadStatus();
            reason = reset ? PopupStoreManualTestPolicy.ResetBlockReason(status) : PopupStoreManualTestPolicy.StartBlockReason(status);
            if (reason != null) return false;
            bool success = reset ? actions.TryReset(out reason) : actions.TryStart(out reason);
            if (string.IsNullOrEmpty(reason)) reason = success
                ? reset ? "검수 기록을 초기화했습니다. '테스트 시작'으로 새 검수를 준비하세요." : "기존 원본 Stage1 검수 시작을 요청했습니다. 영업 시작은 사용자가 직접 누릅니다."
                : "안전 조건이 바뀌어 실행하지 않았습니다. '테스트 상태 확인'을 확인하세요.";
            return success;
        }
        catch (Exception exception)
        {
            reason = "검수 " + (reset ? "초기화" : "시작") + "을 완료하지 못했습니다 (" + exception.GetType().Name
                + "). 자동 재시도·Play 종료·환경 복원은 실행하지 않습니다. '테스트 상태 확인'을 확인하세요.";
            return false;
        }
    }

    internal static string ReadStatus(IPopupStoreManualTestActions actions)
    {
        try { return PopupStoreManualTestPolicy.FormatStatus(actions?.ReadStatus()); }
        catch (Exception exception) { return "상태: 안전 차단\n상태 조회를 완료하지 못했습니다 (" + exception.GetType().Name + "). 시작·초기화는 실행하지 않았습니다."; }
    }
}

public static class PopupStoreManualTestMenu
{
    internal const string StartPath = "Tools/Panda Restaurant/Popup Store/테스트 시작";
    internal const string ResetPath = "Tools/Panda Restaurant/Popup Store/테스트 초기화";
    internal const string StatusPath = "Tools/Panda Restaurant/Popup Store/테스트 상태 확인";

    private sealed class ExistingReviewActions : IPopupStoreManualTestActions
    {
        public PopupStoreManualTestStatus ReadStatus() => PopupStoreVerificationRunner.ReadPlannerManualTestStatus();
        public bool TryStart(out string reason) => PopupStoreVerificationRunner.TryStartPlannerManualTest(out reason);
        public bool TryReset(out string reason) => PopupStoreVerificationRunner.TryResetPlannerManualTest(out reason);
    }

    [MenuItem(StartPath)]
    private static void StartManualTest()
    {
        bool success = PopupStoreManualTestCommands.TryStart(new ExistingReviewActions(), out string reason);
        if (success) Debug.Log("POPUP_STORE_MANUAL_TEST_START " + reason);
        else Debug.LogWarning("POPUP_STORE_MANUAL_TEST_START_REFUSED " + reason);
    }

    [MenuItem(ResetPath)]
    private static void ResetManualTest()
    {
        bool success = PopupStoreManualTestCommands.TryReset(new ExistingReviewActions(), out string reason);
        if (success) Debug.Log("POPUP_STORE_MANUAL_TEST_RESET " + reason);
        else Debug.LogWarning("POPUP_STORE_MANUAL_TEST_RESET_REFUSED " + reason);
    }

    [MenuItem(StatusPath)]
    private static void ShowTestStatus()
        => Debug.Log("POPUP_STORE_MANUAL_TEST_STATUS\n" + PopupStoreManualTestCommands.ReadStatus(new ExistingReviewActions()));
}
#endif
