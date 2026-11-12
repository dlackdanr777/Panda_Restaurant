#if UNITY_EDITOR
// Editor admission only; no account, wallet, SDK, or game policy is modified here.
public static class EditorAccountSafetyPolicy
{
    public static bool IsDebugActionBlocked(bool armed, bool legacySuppression) => armed || legacySuppression;

    public static bool CanEnterPlay(bool armed, bool playingOrChanging, bool compiling, bool updating,
        bool sceneDirty, bool isFirstLoadingScene, bool reloadDomain, bool reloadScene,
        bool sdkInitialized, bool sdkLoggedIn, bool hasBackendOwner, out string reason)
    {
        reason = null;
        if (!armed) return true;
        if (playingOrChanging || compiling || updating) reason = "Editor 작업이 끝난 Play OFF 상태가 필요합니다.";
        else if (sceneDirty || !isFirstLoadingScene) reason = "저장되지 않은 변경이 없는 원본 FirstLoadingScene만 허용합니다.";
        else if (!reloadDomain || !reloadScene) reason = "안전 검수는 Domain Reload와 Scene Reload가 모두 켜져 있어야 합니다. 설정을 자동 변경하지 않습니다.";
        else if (sdkInitialized || sdkLoggedIn || hasBackendOwner) reason = "기존 SDK/계정 세션이 남아 있습니다. 강제 로그아웃하거나 기존 세션에 검수를 연결하지 않습니다.";
        return reason == null;
    }

    public static bool IsReady(EditorAccountReadiness state, out string reason)
    {
        reason = null;
        if (state == null) reason = "계정 준비 증거가 없습니다.";
        else if (!state.SdkLoggedIn || !state.ManagerLoggedIn) reason = "인증 완료를 기다립니다.";
        else if (!state.GameDataReady || !state.CurrentQuery || !state.SameQuery || !state.SameAccount)
            reason = "현재 계정·조회에 속한 GameData 복원이 필요합니다.";
        else if (!state.FirstTutorialClear || !state.HasNickname)
            reason = "기존 튜토리얼 완료 계정과 닉네임이 필요합니다. 신규 계정 경로를 허용하지 않습니다.";
        else if (!state.StagesCurrent || state.RequiredStageCount != 3 || state.CompletedStageCount != 3
            || !state.AllStagesApplied) reason = "같은 조회의 Stage1/2/3 적용 완료를 기다립니다.";
        else if (!state.SaveIdle) reason = "진행 중이거나 미확정인 저장이 있습니다.";
        return reason == null;
    }
}

public sealed class EditorAccountReadiness
{
    public bool SdkLoggedIn, ManagerLoggedIn, GameDataReady, CurrentQuery, SameQuery, SameAccount;
    public bool FirstTutorialClear, HasNickname, StagesCurrent, AllStagesApplied, SaveIdle;
    public int RequiredStageCount, CompletedStageCount;
}
#endif
