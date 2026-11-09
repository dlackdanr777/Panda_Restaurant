#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit six-phase review ownership with frozen Editor-only balance data. No automated gameplay or Play exit.</summary>
public static partial class PopupStoreVerificationRunner
{
    private const string PhasePreviewStateKey = "Panda.PopupStore.PhasePreview.State";
    private const string PhasePreviewProfileKey = "Panda.PopupStore.PhasePreview.FrozenProfile";
    private const string PhasePreviewOwnerKey = "Panda.PopupStore.PhasePreview.OwnerCreated";
    private const string PhasePreviewWorkspaceKey = "Panda.PopupStore.PhasePreview.Workspace";
    private const string PhasePreviewErrorKey = "Panda.PopupStore.PhasePreview.Error";
    private static PopupStoreAccessReviewOwner _phasePreviewAccessOwner;
    private static PopupStorePhasePreview _phasePreviewView;
    private static bool _phasePreviewRestorePending;

    internal static bool PhasePreviewReviewActive
    {
        get
        {
            string state = SessionState.GetString(PhasePreviewStateKey, "");
            return state == "starting" || state == "ready" || _phasePreviewRestorePending
                || (state == "failed" && EditorApplication.isPlayingOrWillChangePlaymode);
        }
    }

    internal static void ExecutePhasePreviewCommand(string command)
    {
        if (command == "phase-preview-status")
        { Debug.Log("POPUP_STORE_PHASE_PREVIEW_STATUS " + ReadPhasePreviewStatus()); return; }
        string reason;
        bool accepted;
        if (command == "phase-preview-start") accepted = TryStartPhasePreview(out reason);
        else if (command == "phase-preview-reset") accepted = TryResetPhasePreview(out reason);
        else { Debug.LogWarning("POPUP_STORE_PHASE_PREVIEW_REFUSED unknown explicit command"); return; }
        if (accepted) Debug.Log("POPUP_STORE_PHASE_PREVIEW_COMMAND " + command + "; " + reason);
        else Debug.LogWarning("POPUP_STORE_PHASE_PREVIEW_REFUSED " + command + "; " + reason);
    }

    internal static bool TryStartPhasePreview(out string reason)
    {
        reason = null;
        if (SessionState.GetString(PhasePreviewStateKey, "").Length != 0 || _phasePreviewAccessOwner != null
            || _phasePreviewView != null || _phasePreviewRestorePending)
        { reason = "기존 단계 프리뷰 기록이 있습니다. Play 종료 후 '6단계 프리뷰 초기화'를 먼저 실행하세요."; return false; }
        var status = ReadPlannerManualTestStatus();
        // A stopped ordinary review's history remains untouched; it is not an active data owner.
        if (status.Stopped && !status.OwnerPresent && !status.StartPending && !status.OwnerLost)
        { status.HasRecord = false; status.AdmissionBound = false; }
        reason = PopupStoreManualTestPolicy.StartBlockReason(status);
        if (reason != null || !CustomerPartyStage1Preview.CanStart(out reason)) return false;
        var catalog = Resources.Load<PopupStoreMaterialCatalog>("UI/PopupStore/PopupStoreMaterials");
        if (!PopupStorePhasePreviewSettings.TryLoadForExplicitStart(catalog, out string frozenJson,
            out var snapshot, out var scorePolicy, out reason)) return false;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var staff = typeof(CustomerPartyStage1Preview).GetField("staffAi", flags);
        var natural = typeof(CustomerPartyStage1Preview).GetField("naturalAi", flags);
        if (staff == null || natural == null)
        { reason = "원본 메모리 검수 옵션을 확인할 수 없습니다."; return false; }
        object previousStaff = staff.GetValue(null), previousNatural = natural.GetValue(null);
        try
        {
            SessionState.SetString(PhasePreviewWorkspaceKey, JsonUtility.ToJson(ReadWorkspace()));
            SessionState.SetString(PhasePreviewProfileKey, frozenJson);
            SessionState.EraseBool(PhasePreviewOwnerKey); SessionState.EraseString(PhasePreviewErrorKey);
            SessionState.SetString(PhasePreviewStateKey, "starting");
            staff.SetValue(null, true); natural.SetValue(null, false);
            CustomerPartyStage1Preview.StartManual(ERestaurantFloorType.Floor1, false, false, null, 0);
            if (!SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false)
                || !EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("원본 메모리 검수기가 시작 요청을 승인하지 않았습니다.");
            reason = "원본 Stage1에 단계 프리뷰를 준비합니다. 팝업 진입 후 튜토리얼 마지막 페이지에서 플레이를 누릅니다. "
                + "누적 점수 제안 v1·검수용·실제 미지급. Inspector 설정: " + PopupStorePhasePreviewSettings.BalanceAssetPath;
            Debug.Log("POPUP_STORE_PHASE_PREVIEW_ARMED profile=" + snapshot.ProfileId + "; version=" + snapshot.Version
                + "; goal=" + snapshot.GoalMetric + "; points=" + scorePolicy.ThreeMaterialPoints + "/"
                + scorePolicy.FourMaterialPoints + "/" + scorePolicy.FiveMaterialPoints
                + "; explicit=True; manualGameplay=True; liveAccount=False; operatingMode=False");
            return true;
        }
        catch (Exception exception)
        {
            reason = "단계 프리뷰 시작이 중단됐습니다: " + Unwrap(exception).Message;
            RecordPhasePreviewFailure(reason);
            return false;
        }
        finally { staff.SetValue(null, previousStaff); natural.SetValue(null, previousNatural); }
    }

    internal static bool TryResetPhasePreview(out string reason)
    {
        reason = PopupStoreManualTestPolicy.ResetBlockReason(ReadPlannerManualTestStatus());
        if (reason != null) return false;
        if (_phasePreviewAccessOwner != null || _phasePreviewView != null || _phasePreviewRestorePending)
        { reason = "단계 프리뷰 소유자 또는 종료 복원이 남아 있습니다. 기존 테스트 종료를 먼저 완료하세요."; return false; }
        SessionState.EraseString(PhasePreviewStateKey); SessionState.EraseString(PhasePreviewProfileKey);
        SessionState.EraseBool(PhasePreviewOwnerKey); SessionState.EraseString(PhasePreviewErrorKey);
        SessionState.EraseString(PhasePreviewWorkspaceKey);
        reason = "단계 프리뷰 기록만 초기화했습니다. Inspector 밸런스 에셋·이전 v0 파일·일반 90초 테스트 기록은 보존합니다.";
        return true;
    }

    internal static string ReadPhasePreviewStatus()
    {
        string state = SessionState.GetString(PhasePreviewStateKey, "");
        string error = SessionState.GetString(PhasePreviewErrorKey, "");
        string ownership = _phasePreviewAccessOwner == null ? "미연결" : "격리 메모리 계정";
        string clock = _phasePreviewView != null
            ? _phasePreviewView.GetComponent<PopupStoreBusinessClock>()?.State.ToString() : "미준비";
        return "상태=" + (state.Length == 0 ? "미준비" : state) + "; Play=" + EditorApplication.isPlaying
            + "; 영업=" + clock + "; 소유=" + ownership + (error.Length == 0 ? "" : "; 차단=" + error)
            + "\n누적 점수 제안 v1 단계형 프리뷰 · 수동 플레이 · 검수용/실제 미지급 · 최고 통과 등급 R1 검수 추첨 1회"
            + "\n다음 시작의 Inspector 설정=" + PopupStorePhasePreviewSettings.BalanceAssetPath
            + "\n이전 v0 파일 보존(현재 시작에서 사용하지 않음)=" + PopupStorePhasePreviewSettings.SettingsPath
            + "\n진입: 원본 Manual Play 검수창의 카메라: VIP 홀 → Game 빈 배경을 아래로 드래그 → 3층 지도 → 패스트푸드 아이콘 → 튜토리얼 마지막 페이지 플레이."
            + "\n반복: 기존 테스트 종료 (Play 중지) → 6단계 프리뷰 초기화 → 6단계 프리뷰 시작. 일반 테스트 메뉴는 원래 90초 경로입니다.";
    }

    private static void TickPhasePreviewReview()
    {
        string state = SessionState.GetString(PhasePreviewStateKey, "");
        if ((state != "starting" && state != "ready") || !EditorApplication.isPlaying) return;
        if (!SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false)
            || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
        { RecordPhasePreviewFailure("원본 메모리 세션 안전 경계를 확인할 수 없습니다."); return; }
        if (!CustomerPartyManualPlayBootstrap.Active
            || !SceneManager.GetSceneByPath(CustomerPartyStage1Preview.ScenePath).isLoaded) return;
        if (!HasOriginalMemoryOwner())
        { RecordPhasePreviewFailure("정확한 원본 offline memory owner가 아닙니다."); return; }
        if (state == "ready")
        {
            if (_phasePreviewAccessOwner == null || _phasePreviewView == null)
                RecordPhasePreviewFailure("준비된 단계 프리뷰 소유자를 잃었습니다. 새 계정·프리뷰를 같은 Play에 재생성하지 않습니다.");
            return;
        }
        var views = Resources.FindObjectsOfTypeAll<UIPopupStore>()
            .Where(view => !EditorUtility.IsPersistent(view) && view.gameObject.scene.IsValid()).ToArray();
        if (views.Length == 0) return;
        if (views.Length != 1) { RecordPhasePreviewFailure("단계 프리뷰의 팝업 인스턴스가 중복되어 준비하지 않습니다."); return; }
        var popup = views[0];
        // Stage1 can preload the authored popup before its normal Init/OnShow has built the three order slots.
        // Host stays null in this interval, so the ordinary start button and direct RequestStart remain blocked.
        if (!popup.isActiveAndEnabled || !popup.gameObject.activeInHierarchy) return;
        var orders = popup.GetComponent<PopupStoreOrderDisplay>();
        if (orders != null && !orders.IsReady) return;
        var start = popup.GetComponent<PopupStoreStartController>();
        bool previouslyEnabled = start != null && start.enabled;
        try
        {
            if (start == null || !previouslyEnabled || start.Host != null
                || popup.GetComponent<PopupStoreBusinessClock>()?.State != PopupStoreBusinessState.NotStarted
                || popup.GetComponent<PopupStorePhasePreview>() != null)
                throw new InvalidOperationException("시작 전 원본 팝업이 아니거나 다른 검수 소유자가 연결돼 있습니다.");
            if (SessionState.GetBool(PhasePreviewOwnerKey, false) || _phasePreviewAccessOwner != null)
                throw new InvalidOperationException("기존 단계 프리뷰 계정이 생성된 기록이 있습니다. 중복 계정은 만들지 않습니다.");
            var catalog = Resources.Load<PopupStoreMaterialCatalog>("UI/PopupStore/PopupStoreMaterials");
            if (!PopupStorePhasePreviewSettings.TryParseSnapshot(SessionState.GetString(PhasePreviewProfileKey, ""),
                catalog, out var profile, out var scorePolicy, out string error)) throw new InvalidOperationException(error);
            // No user event can interleave this binding. Failure leaves direct RequestStart blocked.
            start.enabled = false;
            _phasePreviewAccessOwner = new PopupStoreAccessReviewOwner("phase-preview", 1);
            SessionState.SetBool(PhasePreviewOwnerKey, true);
            _phasePreviewAccessOwner.Bind(popup);
            _phasePreviewView = popup.gameObject.AddComponent<PopupStorePhasePreview>();
            if (!_phasePreviewView.Initialize(profile, scorePolicy, out error)) throw new InvalidOperationException(error);
            var tutorialPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupStorePhasePreviewViewBuilder.PrefabPath);
            if (tutorialPrefab == null) throw new InvalidOperationException("프리뷰 튜토리얼 Prefab 참조가 없습니다.");
            var tutorial = UnityEngine.Object.Instantiate(tutorialPrefab, popup.transform, false)
                .GetComponent<PopupStorePhasePreviewView>();
            var originalClose = new SerializedObject(popup).FindProperty("_closeButton").objectReferenceValue as UnityEngine.UI.Button;
            if (!_phasePreviewView.AttachView(tutorial, originalClose))
                throw new InvalidOperationException("프리뷰 튜토리얼 소유권 연결을 확인할 수 없습니다.");
            BindPhasePreviewPolish(popup, tutorial, _phasePreviewView);
            var rewardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupStorePhaseRewardViewBuilder.PrefabPath);
            if (rewardPrefab == null) throw new InvalidOperationException("프리뷰 보상 연출 Prefab 참조가 없습니다.");
            var rewardView = UnityEngine.Object.Instantiate(rewardPrefab, popup.transform, false)
                .GetComponent<PopupStorePhaseRewardPresentation>();
            if (!_phasePreviewView.AttachRewardView(rewardView,
                Resources.Load<PopupStoreRewardTable>("UI/PopupStore/PopupStoreRewardTable_R1")))
                throw new InvalidOperationException("프리뷰 보상 연출 및 승인 R1 스냅샷 연결을 확인할 수 없습니다.");
            start.enabled = previouslyEnabled;
            SessionState.SetString(PhasePreviewStateKey, "ready");
            Debug.Log("POPUP_STORE_PHASE_PREVIEW_READY localSession=" + start.LocalSessionId
                + "; profile=" + profile.ProfileId + "; isolated=True; userStartRequired=True; R1Draw=False", popup);
        }
        catch (Exception exception)
        {
            if (start != null) start.enabled = false;
            RecordPhasePreviewFailure(Unwrap(exception).Message);
        }
    }

    private static void BindPhasePreviewPolish(UIPopupStore popup, PopupStorePhasePreviewView tutorial,
        PopupStorePhasePreview preview)
    {
        var polish = tutorial.GetComponent<PopupStorePhasePreviewPolish>();
        var exit = new SerializedObject(popup.GetComponent<PopupStoreExitConfirmation>());
        var clock = new SerializedObject(popup.GetComponent<PopupStoreBusinessClock>());
        // Instance-only preview formatting: the authored frame already contains its static time label.
        clock.FindProperty("_notStartedText").stringValue = "—";
        clock.ApplyModifiedPropertiesWithoutUndo();
        popup.GetComponent<PopupStoreBusinessClock>().RefreshPhasePreview();
        var overlay = exit.FindProperty("_overlay").objectReferenceValue as GameObject;
        var panel = overlay != null ? overlay.transform.Find("Exit Confirmation Panel") as RectTransform : null;
        var texts = panel != null ? panel.GetComponentsInChildren<TMPro.TMP_Text>(true) : Array.Empty<TMPro.TMP_Text>();
        if (polish == null || !polish.Bind(preview,
            clock.FindProperty("_timeText").objectReferenceValue as TMPro.TMP_Text,
            texts.SingleOrDefault(x => x.name == "Exit Confirmation Title"),
            texts.SingleOrDefault(x => x.name == "Exit Confirmation Explanation"),
            exit.FindProperty("_continueButton").objectReferenceValue as UnityEngine.UI.Button,
            exit.FindProperty("_abandonButton").objectReferenceValue as UnityEngine.UI.Button, panel)
            || !preview.AttachPolish(polish))
            throw new InvalidOperationException("프리뷰 포기창·시간 경고의 로컬 참조를 확인할 수 없습니다.");
    }

    private static void RecordPhasePreviewFailure(string reason)
    {
        SessionState.SetString(PhasePreviewStateKey, "failed");
        SessionState.SetString(PhasePreviewErrorKey, reason ?? "unknown failure");
        if (_phasePreviewView != null) _phasePreviewView.FailClosed(reason);
        Debug.LogWarning("POPUP_STORE_PHASE_PREVIEW_FAILED " + reason + "; automaticRetry=False; automaticPlayStop=False");
    }

    private static void PhasePreviewPlayStateChanged(PlayModeStateChange state)
    {
        string reviewState = SessionState.GetString(PhasePreviewStateKey, "");
        // A later ordinary Play must not overwrite this completed preview's restoration evidence.
        if (reviewState != "starting" && reviewState != "ready" && reviewState != "failed") return;
        if (state != PlayModeStateChange.EnteredEditMode) return;
        _phasePreviewAccessOwner?.Dispose(); _phasePreviewAccessOwner = null; _phasePreviewView = null;
        SessionState.SetString(PhasePreviewStateKey, "stopped");
        _phasePreviewRestorePending = true;
        // Observe the original owner's restoration; do not save, clear Undo or reset the workspace ourselves.
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => {
            _phasePreviewRestorePending = false;
            try
            {
                string saved = SessionState.GetString(PhasePreviewWorkspaceKey, "");
                var before = string.IsNullOrEmpty(saved) ? null : JsonUtility.FromJson<Workspace>(saved);
                var after = ReadWorkspace();
                bool unchanged = before != null && Same(before, after);
                Directory.CreateDirectory(DirectoryPath);
                File.WriteAllText(DirectoryPath + "/phase-preview-restoration.json", JsonUtility.ToJson(new PhasePreviewRestoration {
                    capturedUtc = DateTime.UtcNow.ToString("O"), playing = EditorApplication.isPlaying,
                    sdkInitialized = BackEnd.Backend.IsInitialized, isLogin = BackEnd.Backend.IsLogin,
                    before = before, after = after, workspaceUnchanged = unchanged
                }, true));
                Debug.Log("POPUP_STORE_PHASE_PREVIEW_STOPPED Play=False; workspaceUnchanged=" + unchanged
                    + "; isolatedOwnerDisposed=True; nextRunRequiresExplicitReset=True");
            }
            catch (Exception exception)
            { Debug.LogWarning("POPUP_STORE_PHASE_PREVIEW_RESTORATION_UNVERIFIED " + exception.GetType().Name); }
        };
    }

    [Serializable] private sealed class PhasePreviewRestoration
    {
        public string capturedUtc;
        public bool playing, sdkInitialized, isLogin, workspaceUnchanged;
        public Workspace before, after;
    }
}
#endif
