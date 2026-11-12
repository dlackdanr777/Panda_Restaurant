#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Muks.BackEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in Editor safety only. Preparing this latch never starts Play, authenticates,
// restores an account, changes account mode, or performs a backend request.
public static class EditorAccountSafety
{
    private const string ArmedKey = "Panda.PopupStore.AccountSafety.Armed";
    private const string PreviousSuppressionKey = "Panda.PopupStore.AccountSafety.PreviousSuppression";
    private const string MenuRoot = "Tools/Panda Restaurant/Popup Store/실계정 안전 진입/";
    public static bool IsArmed => SessionState.GetBool(ArmedKey, false);
    public static bool DebugActionsBlocked => EditorAccountSafetyPolicy.IsDebugActionBlocked(
        IsArmed, MainScene.SuppressEditorDebugGrantsForTesting);

    [InitializeOnLoadMethod]
    private static void Install()
    {
        RestoreSuppressionBeforeScene();
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RestoreAtSubsystemRegistration() => RestoreSuppressionBeforeScene();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RestoreSuppressionBeforeScene()
    {
        if (IsArmed) MainScene.SuppressEditorDebugGrantsForTesting = true;
    }

    [MenuItem(MenuRoot + "안전 잠금 준비 (Play 실행 안 함)")]
    private static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { Debug.LogWarning("ACCOUNT_SAFETY: Play OFF 및 컴파일 완료 후 준비하세요."); return; }
        if (!IsArmed) SessionState.SetBool(PreviousSuppressionKey, MainScene.SuppressEditorDebugGrantsForTesting);
        SessionState.SetBool(ArmedKey, true);
        RestoreSuppressionBeforeScene();
        InspectPreparation();
    }

    [MenuItem(MenuRoot + "준비 상태 확인 (읽기 전용)")]
    private static void InspectPreparation()
    {
        bool canEnter = CanEnterPlay(false, out string reason);
        Debug.Log("ACCOUNT_SAFETY: armed=" + IsArmed + "; entry=" + (canEnter ? "LOCAL_PREFLIGHT_ONLY" : "BLOCKED")
            + "; " + (reason ?? "실제 계정 원본·기존 계정 여부 및 허용된 쓰기 범위 확인 전에는 Play를 실행하지 마세요.")
            + " 서버 조회·로그인·저장·계정 모드 변경은 수행하지 않았습니다.");
    }

    [MenuItem(MenuRoot + "안전 잠금 해제 (Play OFF)")]
    private static void Disarm()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { Debug.LogWarning("ACCOUNT_SAFETY: 실행 중에는 안전 잠금을 해제할 수 없습니다."); return; }
        if (!IsArmed) return;
        MainScene.SuppressEditorDebugGrantsForTesting = SessionState.GetBool(PreviousSuppressionKey, false);
        SessionState.EraseBool(ArmedKey);
        SessionState.EraseBool(PreviousSuppressionKey);
    }

    private static bool CanEnterPlay(bool atExitEditMode, out string reason)
    {
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        bool first = scenes.Length == 1 && scenes[0].path == "Assets/Scenes/FirstLoadingScene.unity"
            && PrefabStageUtility.GetCurrentPrefabStage() == null
            && (EditorSceneManager.playModeStartScene == null
                || AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == scenes[0].path);
        bool useOptions = EditorSettings.enterPlayModeOptionsEnabled;
        var options = EditorSettings.enterPlayModeOptions;
        return EditorAccountSafetyPolicy.CanEnterPlay(true,
            atExitEditMode ? EditorApplication.isPlaying : EditorApplication.isPlayingOrWillChangePlaymode,
            EditorApplication.isCompiling, EditorApplication.isUpdating, scenes.Any(s => s.isDirty), first,
            !useOptions || (options & EnterPlayModeOptions.DisableDomainReload) == 0,
            !useOptions || (options & EnterPlayModeOptions.DisableSceneReload) == 0,
            BackEnd.Backend.IsInitialized, BackEnd.Backend.IsLogin, BackendManager.ExistingInstance != null, out reason);
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!IsArmed) return;
        RestoreSuppressionBeforeScene();
        if (state == PlayModeStateChange.ExitingEditMode && !CanEnterPlay(true, out string reason))
        {
            // Cancel admission before any scene starts. Never stop an existing live Play session.
            EditorApplication.isPlaying = false;
            Debug.LogWarning("ACCOUNT_SAFETY_ENTRY_BLOCKED: " + reason);
        }
    }

    public static void RunDebugAction(Action action)
    {
        // Check at invocation, including callbacks retained across a disabled domain reload.
        if (!DebugActionsBlocked) action?.Invoke();
    }

    public static bool RequireFreshAuthentication()
    {
        if (!IsArmed) return true;
        if (!BackEnd.Backend.IsLogin && !(BackendManager.ExistingInstance?.IsLogin ?? false)) return true;
        Debug.LogWarning("ACCOUNT_SAFETY_AUTH_BLOCKED: 기존 로그인 세션 재진입은 자동 복원·재로그인하지 않습니다.");
        return false;
    }

    public static bool IsReady(out string reason)
    {
        try
        {
            var backend = BackendManager.ExistingInstance;
            if (backend == null) { reason = "BackendManager가 없습니다."; return false; }
            var evidence = backend.RestoredGameData;
            var collection = backend.StageMigrationCollection;
            var required = new[] { EStage.Stage1, EStage.Stage2, EStage.Stage3 };
            bool current = evidence != null && backend.IsCurrentGameDataQuery(evidence.Query);
            var state = new EditorAccountReadiness
            {
                SdkLoggedIn = BackEnd.Backend.IsLogin, ManagerLoggedIn = backend.IsLogin,
                GameDataReady = backend.GameDataRestoreResult.Status == GameDataRestoreStatus.Ready,
                CurrentQuery = current,
                SameQuery = evidence != null && collection != null && ReferenceEquals(evidence.Query, collection.Query),
                SameAccount = evidence != null && string.Equals(evidence.Query.AccountInDate, BackEnd.Backend.UserInDate, StringComparison.Ordinal),
                FirstTutorialClear = UserInfo.IsFirstTutorialClear, HasNickname = !string.IsNullOrWhiteSpace(UserInfo.UserId),
                StagesCurrent = collection != null && collection.RefreshValidity(),
                RequiredStageCount = collection?.RequiredStages.Count ?? 0,
                CompletedStageCount = collection?.CompletedStageCount ?? 0,
                AllStagesApplied = collection != null && required.All(stage => collection.RequiredStages.Contains(stage)
                    && collection.Applications.Count(a => a.Stage == stage && a.Status == StaffStageApplicationStatus.Succeeded) == 1),
                SaveIdle = evidence != null && backend.CanSaveLegacyGameData
                    && !GameDataSaveCoordinator.IsTargetBlocked(evidence.Target)
                    && !backend.EditorHasPendingStageSaves
                    && (backend.CurrentGameDataSaveCoordinator == null || backend.CurrentGameDataSaveCoordinator.CanStartPurchase)
            };
            return EditorAccountSafetyPolicy.IsReady(state, out reason);
        }
        catch { reason = "준비 증거를 안전하게 읽을 수 없습니다. 진입을 차단합니다."; return false; }
    }

    public static bool RequireReady()
    {
        if (!IsArmed || IsReady(out _)) return true;
        IsReady(out string reason);
        Debug.LogWarning("ACCOUNT_SAFETY_DATA_BLOCKED: " + reason);
        return false;
    }

    public static bool AllowSceneEntry(string sceneName)
    {
        if (!IsArmed) return true;
        return sceneName != "Stage1" && sceneName != "Stage2" && sceneName != "Stage3" && sceneName != "IntroScene"
            || RequireReady();
    }

    public static IEnumerator WaitForReady(Func<bool> isCurrent, Action continuation)
    {
        double deadline = EditorApplication.timeSinceStartup + 30;
        while (IsArmed && isCurrent() && EditorApplication.timeSinceStartup < deadline)
        {
            if (IsReady(out _)) { if (IsArmed && isCurrent() && RequireReady()) continuation(); yield break; }
            yield return null;
        }
        Debug.LogWarning("ACCOUNT_SAFETY_DATA_BLOCKED: 준비 대기 종료/조회 변경. Scene 진입·닉네임 생성으로 계속하지 않습니다.");
    }
}

namespace Muks.BackEnd
{
    public partial class BackendManager
    {
        // Read only: do not drain, retry, complete, or replace existing Stage writes.
        public bool EditorHasPendingStageSaves => _stageSaveQueues.Values.Any(queue => queue.Count != 0);
    }
}
#endif
