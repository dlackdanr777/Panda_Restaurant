#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor menu adapter for the existing AccessA path. Its only persistent state is
// Editor SessionState metadata; the existing original memory-Play owner owns all data.
public static partial class PopupStoreVerificationRunner
{
    private const string PlannerStateKey = "Panda.PopupStore.PlannerReview.State";
    private const string PlannerSnapshotKey = "Panda.PopupStore.PlannerReview.Snapshot";
    private const string PlannerAccountKey = "Panda.PopupStore.PlannerReview.Account";
    private static bool _plannerRestorePending;
    private static string _plannerLastStatus;

    internal static PopupStoreManualTestStatus ReadPlannerManualTestStatus()
    {
        string phase = SessionState.GetString(PlannerStateKey, "");
        bool ownedActive = phase == "starting" || phase == "active";
        bool original = HasOriginalMemoryOwner();
        bool armed = SessionState.GetBool(ResultReviewKey, false);
        bool manual = SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false);
        string recordedAccount = SessionState.GetString(PlannerAccountKey, "");
        var state = new PopupStoreManualTestStatus {
            Playing = EditorApplication.isPlaying,
            Transitioning = (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
                || _plannerRestorePending || (!EditorApplication.isPlayingOrWillChangePlaymode && manual),
            Compiling = EditorApplication.isCompiling,
            Updating = EditorApplication.isUpdating,
            CompilationFailed = EditorUtility.scriptCompilationFailed,
            RegressionRunning = _running || GachaReviewTestRunner.IsRunning || IsUnityTestRunnerActive(),
            LiveAccount = BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin,
            OtherBackendOwner = Muks.BackEnd.BackendManager.ExistingInstance != null && !original,
            DirtyScene = Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty),
            PrefabStage = PrefabStageUtility.GetCurrentPrefabStage() != null,
            ReloadDisabled = EditorSettings.enterPlayModeOptionsEnabled && EditorSettings.enterPlayModeOptions != EnterPlayModeOptions.None,
            PendingRequest = File.Exists(RequestPath),
            OtherReview = PhasePreviewReviewActive || SessionState.GetBool(PausePlayKey, false)
                || (manual && !ownedActive)
                || (armed && (!ownedActive || (ReviewMode)SessionState.GetInt(ReviewModeKey, -1) != ReviewMode.AccessA))
                || (_accessReviewOwner != null && (!ownedActive || (recordedAccount.Length > 0 && recordedAccount != _accessReviewOwner.AccountId))),
            HasRecord = phase.Length > 0,
            Stopped = phase == "stopped" || phase == "failed",
            StartPending = ownedActive && armed,
            OriginalMemoryOwner = original,
            OwnerPresent = ownedActive && _accessReviewOwner != null,
            OwnerLost = ownedActive && SessionState.GetBool(AccessReviewCreatedKey, false) && _accessReviewOwner == null,
            Detail = phase == "failed" ? "시작이 거부되거나 중단됐습니다. Play OFF에서 테스트 초기화 후 다시 시도하세요." : ""
        };
        if (state.OwnerPresent && original && !state.OtherReview)
            FillPlannerOwnerSnapshot(state, _accessReviewOwner);
        else if (state.Stopped)
        {
            // Last observed review values only. Never recreate a store to answer a status query.
            string saved = SessionState.GetString(PlannerSnapshotKey, "");
            var last = string.IsNullOrEmpty(saved) ? null : JsonUtility.FromJson<PlannerSnapshot>(saved);
            if (last != null)
            {
                state.Remaining = last.remaining; state.Used = last.used; state.Limit = last.limit;
                state.AccountId = last.account; state.LocalSessionId = last.localSession;
                state.ClockState = last.clock;
            }
            state.Detail = "검수용 메모리 저장소는 종료 시 폐기됩니다. 표시된 횟수는 마지막 관찰값이며 초기화 후 새 테스트를 시작하세요.";
        }
        if (ownedActive && !armed && !state.OwnerPresent && !state.Stopped)
        {
            state.OwnerLost = true;
            state.Detail = "검수 준비가 중단됐습니다. 기존 테스트 종료 후 초기화하세요. 이 Play에 저장소를 다시 연결하지 않습니다.";
        }
        return state;
    }

    private static bool IsUnityTestRunnerActive()
    {
        var method = typeof(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi).GetMethod("IsRunActive",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        // An unknown runner state is not permission to interrupt it.
        if (method == null) return true;
        try { return (bool)method.Invoke(null, null); }
        catch { return true; }
    }

    internal static void FillPlannerOwnerSnapshot(PopupStoreManualTestStatus state, PopupStoreAccessReviewOwner owner)
    {
        if (state == null || owner == null) return;
        string day = owner.Host.Access?.GameDay;
        if (string.IsNullOrEmpty(day) && !PopupStoreDailyUsage.TryGetDayKey(owner.AuthorityTime, out day)) return;
        var stored = owner.Store.Read(owner.AccountId, day);
        state.Limit = owner.Host.Access?.DailyLimit ?? owner.DailyLimit;
        state.Used = stored.Usage; state.Remaining = Math.Max(0, state.Limit - state.Used);
        state.AccountId = owner.AccountId; state.LocalSessionId = owner.Host.LocalSessionId;
        var controller = Resources.FindObjectsOfTypeAll<PopupStoreStartController>()
            .FirstOrDefault(value => value != null && !EditorUtility.IsPersistent(value)
                && value.gameObject.scene.IsValid() && ReferenceEquals(value.Host, owner.Host)
                && value.LocalSessionId == owner.Host.LocalSessionId);
        state.AdmissionBound = controller != null && owner.Host.Access != null && owner.Host.Access.Success;
        var clock = controller != null ? controller.GetComponent<PopupStoreBusinessClock>() : null;
        state.ClockState = clock != null ? clock.State.ToString() : "unbound";
    }

    internal static bool TryStartPlannerManualTest(out string reason)
    {
        reason = PopupStoreManualTestPolicy.StartBlockReason(ReadPlannerManualTestStatus());
        if (!string.IsNullOrEmpty(reason)) return false;
        if (!CustomerPartyStage1Preview.CanStart(out reason)) return false;
        // Verify the authored limit. Never override a changed policy to manufacture 1/1.
        var settings = Resources.Load<PopupStoreBusinessAccessSettings>("PopupStore/PopupStoreBusinessAccessSettings");
        if (settings == null || !settings.TryCreateSchedule(out var schedule, out reason))
        { reason = reason ?? "승인 영업권 설정을 읽을 수 없습니다."; return false; }
        if (!schedule.TryResolve("2026-11-03", out var policy, out reason) || policy.RewardedDailyLimit != 1)
        { reason = "현재 승인 설정이 검수용 1회 정책과 다릅니다. 설정을 자동 변경하지 않습니다."; return false; }
        SessionState.SetString(PlannerStateKey, "starting");
        SessionState.EraseString(PlannerSnapshotKey); SessionState.EraseString(PlannerAccountKey);
        _plannerLastStatus = null;
        StartResultReview(ReviewMode.AccessA); // Identical entry point to start-access-review-a; no queued request.
        if (SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false)
            && EditorApplication.isPlayingOrWillChangePlaymode)
        { reason = "원본 Stage1 메모리 Play 준비를 요청했습니다. 준비됨 로그 이후 직접 영업 시작을 누르세요."; return true; }
        ClearReviewRequest();
        SessionState.SetString(PlannerStateKey, "failed");
        reason = "기존 Stage1 검수기가 Play 시작을 승인하지 않았습니다. Console의 거부 사유를 확인하세요.";
        return false;
    }

    internal static bool TryResetPlannerManualTest(out string reason)
    {
        reason = PopupStoreManualTestPolicy.ResetBlockReason(ReadPlannerManualTestStatus());
        if (!string.IsNullOrEmpty(reason)) return false;
        // The existing EnteredEditMode owner already disposed the fake store. Reset only
        // this menu's labels/history. Never delete a request or reset a live admission host.
        SessionState.EraseString(PlannerStateKey);
        SessionState.EraseString(PlannerSnapshotKey);
        SessionState.EraseString(PlannerAccountKey);
        _plannerLastStatus = null;
        reason = "검수 상태 기록을 초기화했습니다. 테스트 시작에서 새 격리 계정 1회분을 준비할 수 있습니다.";
        return true;
    }

    [Serializable] private sealed class PlannerSnapshot
    {
        public int remaining = -1, used = -1, limit = -1;
        public string account, localSession, clock;
    }

    private static void ObservePlannerReview()
    {
        string phase = SessionState.GetString(PlannerStateKey, "");
        if (phase != "starting" && phase != "active") return;
        var state = ReadPlannerManualTestStatus();
        if (state.OwnerPresent && state.OriginalMemoryOwner && !state.OtherReview)
        {
            SessionState.SetString(PlannerAccountKey, state.AccountId);
            SessionState.SetString(PlannerSnapshotKey, JsonUtility.ToJson(new PlannerSnapshot {
                remaining = state.Remaining, used = state.Used, limit = state.Limit,
                account = state.AccountId, localSession = state.LocalSessionId, clock = state.ClockState
            }));
            SessionState.SetString(PlannerStateKey, "active");
        }
        string text = PopupStoreManualTestPolicy.FormatStatus(state);
        if (text == _plannerLastStatus) return;
        _plannerLastStatus = text;
        Debug.Log("POPUP_STORE_PLANNER_STATUS " + text);
    }

    private static void PlannerReviewPlayStateChanged(PlayModeStateChange state)
    {
        string phase = SessionState.GetString(PlannerStateKey, "");
        if (phase != "starting" && phase != "active") return;
        if (state == PlayModeStateChange.ExitingPlayMode) ObservePlannerReview();
        if (state != PlayModeStateChange.EnteredEditMode) return;
        SessionState.SetString(PlannerStateKey, "stopped");
        _plannerRestorePending = true;
        // Allow the original owner's delayed workspace verification to finish first.
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => {
            _plannerRestorePending = false;
            Debug.Log("POPUP_STORE_PLANNER_STOPPED Play OFF. 원본 검수 종료 후 테스트 초기화 → 테스트 시작으로 반복할 수 있습니다.");
        };
    }
}
#endif
