#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Execution;
using NUnit.Framework.Internal.Filters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Explicit request-only bridge for the synchronous, memory-only popup store fixtures.
/// Tests never launch Play. An explicit result-review request may use the existing original memory-Play starter.
/// Never creates a scene, saves assets, restores a workspace or starts another Editor.
/// </summary>
[InitializeOnLoad]
public static class PopupStoreVerificationRunner
{
    private const string DirectoryPath = "Temp/PopupStoreVerification";
    private const string RequestPath = DirectoryPath + "/request.txt";
    private const string ResultReviewKey = "Panda.PopupStore.ResultReviewOnce";
    private const string ReviewModeKey = "Panda.PopupStore.ResultReviewMode";
    private const string PausePlayKey = "Panda.PopupStore.PausePlayReview";
    private const string PausePlayReportKey = "Panda.PopupStore.PausePlayReport";
    private const string SessionReviewFirstKey = "Panda.PopupStore.SessionReviewFirst";
    private const string SessionReviewAbandonedKey = "Panda.PopupStore.SessionReviewAbandoned";
    private const string AccessReviewCreatedKey = "Panda.PopupStore.AccessReviewCreated";
    private static PausePlayReport _pausePlayReport;
    private static PausePlayContext _pausePlay;
    private static PopupStoreAccessReviewOwner _accessReviewOwner;
    private static PopupStoreRewardSession _r1ReviewReward;
    private static FixedR1ReviewRandom _r1ReviewRandom;
    private enum ReviewMode { Result, RewardC, RewardS, Session, AccessA, AccessB, R1C, R1SSS }
    private static readonly string[] Fixtures =
    {
        "PopupStoreNavigationTests", "PopupStoreMaterialTests", "PopupStoreSalesCounterTests",
        "PopupStoreOrderTests", "PopupStoreOrderMatchTests", "PopupStoreExactOrderMatchTests",
        "PopupStorePackingTests", "PopupStorePackingPresentationTests", "PopupStoreDeliveryTests", "PopupStoreCustomerEntranceTests",
        "PopupStoreCustomerDepartureTests", "PopupStoreOrderSupplyTests", "PopupStoreBusinessClockTests", "PopupStoreCustomerPatienceTests", "PopupStoreComplaintTests",
        "PopupStoreScoreTests", "PopupStoreGradeTests", "PopupStoreResultTests", "PopupStoreRewardBoxTests", "PopupStoreRewardTableTests",
        "PopupStoreRewardDecisionTests", "PopupStoreRewardTransactionTests", "PopupStoreBusinessStartTests",
        "PopupStoreRewardRevealPreviewTests", "PopupStorePauseTests", "PopupStoreExitConfirmationTests", "PopupStoreSessionLifetimeTests",
        "PopupStoreBusinessAccessSettingsTests", "PopupStoreAdmissionTests",
        "PopupStoreRewardR1TableTests", "PopupStoreRewardR1SessionTests"
    };
    private static bool _running, _abort;
    private static double _nextPoll;
    private static int _editorThread;
    private static Report _report;
    private static readonly List<CaseResult> Cases = new List<CaseResult>();
    private static readonly List<LogRecord> Logs = new List<LogRecord>();

    [Serializable] private sealed class LogRecord { public string type, message, stackTrace; }
    [Serializable] private sealed class CaseResult
    {
        public string id, fullName, status, message, stackTrace;
        public double seconds;
    }
    [Serializable] private sealed class Workspace
    {
        public string setup, activeScene, dirty, selection, startScene, undo, roots;
    }
    [Serializable] private sealed class Inspection
    {
        public string capturedUtc, error;
        public bool playing, compiling, updating, sdkInitialized, isLogin;
        public int[] gameManagerIds, challengeManagerIds;
        public Workspace workspace;
    }
    [Serializable] private sealed class Report
    {
        public string startedUtc, finishedUtc, error;
        public string scope = "Popup-store fixtures, synchronous NUnit on the existing Editor main thread. Memory objects only; no scene creation/replacement/save, Play transition or account operation. A workspace difference stops further cases without automatic rollback.";
        public bool success, compiling, updating, scriptCompilationFailed, workspaceUnchanged;
        public int discovered, total, passed, failed, skipped, inconclusive;
        public Workspace before, after;
        public CaseResult[] tests;
        public LogRecord[] logs;
    }

    static PopupStoreVerificationRunner()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += CapturePausePlayLog;
        Application.logMessageReceived += CaptureSessionReviewLog;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                ClearReviewRequest(); _accessReviewOwner?.Dispose(); _accessReviewOwner = null;
                _r1ReviewReward = null; _r1ReviewRandom = null;
                SessionState.EraseBool(AccessReviewCreatedKey);
            }
            if (state == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(PausePlayKey, false))
            {
                if (_pausePlayReport == null)
                    _pausePlayReport = JsonUtility.FromJson<PausePlayReport>(SessionState.GetString(PausePlayReportKey, ""));
                if (_pausePlayReport != null && !_pausePlayReport.stopping)
                {
                    _pausePlayReport.error = "Play was stopped before the probe completed.";
                    _pausePlayReport.stopping = true;
                    _pausePlayReport.finishedUtc = DateTime.UtcNow.ToString("O");
                    SessionState.SetString(PausePlayReportKey, JsonUtility.ToJson(_pausePlayReport));
                }
            }
        };
    }

    private static void Tick()
    {
        if (_running || EditorApplication.isCompiling || EditorApplication.isUpdating
            || GachaReviewTestRunner.IsRunning) return;
        TickPausePlayReview();
        if (EditorApplication.timeSinceStartup < _nextPoll) return;
        _nextPoll = EditorApplication.timeSinceStartup + .5;
        ApplyResultReviewOnce();
        if (!File.Exists(RequestPath)) return;
        string request;
        try
        {
            request = File.ReadAllText(RequestPath).Trim();
            if (SessionState.GetBool(PausePlayKey, false) && request != "inspect") return;
            if (request != "inspect" && EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath); // Consume only this explicit request; never retry a failed run automatically.
        }
        catch (IOException) { return; } // A writer may still own the request file.
        if (request == "inspect") InspectWorkspace();
        else if (request == "arm-result-review") ArmResultReview();
        else if (request == "start-result-review") StartResultReview();
        else if (request == "start-reward-review-c") StartResultReview(ReviewMode.RewardC);
        else if (request == "start-reward-review-s") StartResultReview(ReviewMode.RewardS);
        else if (request == "start-session-review") StartResultReview(ReviewMode.Session);
        else if (request == "start-access-review-a") StartResultReview(ReviewMode.AccessA);
        else if (request == "start-access-review-b") StartResultReview(ReviewMode.AccessB);
        else if (request == "start-r1-review-a") StartResultReview(ReviewMode.R1C);
        else if (request == "start-r1-review-b") StartResultReview(ReviewMode.R1SSS);
        else if (request == "run-exit-play-review" || request == "run-pause-play-review") StartPausePlayReview();
        else if (request == "run-session-play-review") StartPausePlayReview(true);
        else Run(request);
    }

    private static void StartResultReview(ReviewMode mode = ReviewMode.Result)
    {
        if (!CustomerPartyStage1Preview.CanStart(out string reason)
            || EditorUtility.scriptCompilationFailed
            || PrefabStageUtility.GetCurrentPrefabStage() != null
            || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
        { Debug.LogWarning("POPUP_STORE_RESULT_REVIEW_REFUSED start: " + reason); return; }
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var staff = typeof(CustomerPartyStage1Preview).GetField("staffAi", flags);
        var natural = typeof(CustomerPartyStage1Preview).GetField("naturalAi", flags);
        if (staff == null || natural == null) { Debug.LogWarning("POPUP_STORE_RESULT_REVIEW_REFUSED missing original review options"); return; }
        var previousStaff = staff.GetValue(null); var previousNatural = natural.GetValue(null);
        try
        {
            staff.SetValue(null, true); natural.SetValue(null, false);
            if (!ArmResultReview(mode)) return;
            // The same starter used by Manual Play (Original) owns its existing fence and restoration.
            CustomerPartyStage1Preview.StartManual(ERestaurantFloorType.Floor1, false, false, null, 0);
        }
        catch (Exception error)
        {
            ClearReviewRequest();
            Debug.LogWarning("POPUP_STORE_RESULT_REVIEW_REFUSED start: " + Unwrap(error).Message);
        }
        finally { staff.SetValue(null, previousStaff); natural.SetValue(null, previousNatural); }
    }

    // Explicit review request only. Does not start Play or change authored assets, scenes, selection or Undo.
    private static bool ArmResultReview(ReviewMode mode = ReviewMode.Result)
    {
        if (!CustomerPartyStage1Preview.CanStart(out string reason))
        { Debug.LogWarning("POPUP_STORE_RESULT_REVIEW_REFUSED " + reason); return false; }
        SessionState.SetInt(ReviewModeKey, (int)mode);
        _accessReviewOwner?.Dispose(); _accessReviewOwner = null; SessionState.EraseBool(AccessReviewCreatedKey);
        _r1ReviewReward = null; _r1ReviewRandom = null;
        SessionState.EraseString(SessionReviewFirstKey); SessionState.EraseBool(SessionReviewAbandonedKey);
        SessionState.SetBool(ResultReviewKey, true);
        Debug.Log("POPUP_STORE_RESULT_REVIEW_ARMED next original memory Play only; mode=" + mode
            + (mode == ReviewMode.Result ? "; business=30; delivery=30"
                : mode == ReviewMode.RewardC ? "; business=30; delivery=authored"
                : mode == ReviewMode.RewardS ? "; actual local orders will prepare S only after explicit admission; delivery=authored"
                : mode == ReviewMode.AccessA ? "; isolated access A; authored 90 seconds; user start approval required"
                : mode == ReviewMode.AccessB ? "; isolated access B; first memory business=30; user start approval required"
                : mode == ReviewMode.R1C ? "; R1 fixed roll=0; isolated one-use account; business=30; user start approval required"
                : mode == ReviewMode.R1SSS ? "; R1 fixed roll=997; isolated one-use account; real twenty-order preparation after user start approval"
                : "; authored 90 seconds; after user abandons and packs one fresh-session order, shorten that memory clock to 15 seconds once"));
        return true;
    }

    private static void ClearReviewRequest()
    {
        SessionState.EraseBool(ResultReviewKey);
        SessionState.EraseInt(ReviewModeKey);
        SessionState.EraseString(SessionReviewFirstKey); SessionState.EraseBool(SessionReviewAbandonedKey);
    }

    [Serializable] private sealed class PausePlayReport
    {
        public string startedUtc, finishedUtc, error, receiptId, orderId, customerId;
        public int slotIndex, observedFrames, ownerId;
        public bool waitingFrozen, deliveryFrozen, inputsBlocked, deliveredAndReleased, releasedIdentity, stopping, success, workspaceUnchanged;
        public bool sessionLifetime, abandonedWithoutResult, freshAfterAbandon, completedDeliveryReleased, completedFresh;
        public string abandonedSessionId, firstFreshSessionId, completedSessionId, nextFreshSessionId;
        public string completedReceiptId, completedOrderId, completedCustomerId;
        public int completedSlotIndex, abandonedScore, abandonedReceiptCount, completedScore, completedOrders, completedFinalizations;
        public Workspace before, after;
    }
    private sealed class PausePlayContext
    {
        internal int step, frame;
        internal double since;
        internal string frozen;
        internal CameraController camera;
        internal UIMainCanvas main;
        internal Muks.MobileUI.MobileUINavigation nav;
        internal UIPopupStore popup;
        internal PopupStorePackingReceipt receipt;
    }
    private static void StartPausePlayReview(bool sessionLifetime = false)
    {
        if (!CustomerPartyStage1Preview.CanStart(out string reason) || EditorUtility.scriptCompilationFailed
            || SessionState.GetBool(ResultReviewKey, false)
            || PrefabStageUtility.GetCurrentPrefabStage() != null
            || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
        { Debug.LogWarning("POPUP_STORE_PAUSE_PLAY_REFUSED " + reason); return; }
        _pausePlayReport = new PausePlayReport { startedUtc = DateTime.UtcNow.ToString("O"), before = ReadWorkspace(), sessionLifetime = sessionLifetime };
        _accessReviewOwner?.Dispose(); _accessReviewOwner = null; SessionState.EraseBool(AccessReviewCreatedKey);
        SessionState.SetString(PausePlayReportKey, JsonUtility.ToJson(_pausePlayReport));
        SessionState.SetBool(PausePlayKey, true);
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var staff = typeof(CustomerPartyStage1Preview).GetField("staffAi", flags);
        var natural = typeof(CustomerPartyStage1Preview).GetField("naturalAi", flags);
        object previousStaff = staff.GetValue(null), previousNatural = natural.GetValue(null);
        try
        {
            staff.SetValue(null, true); natural.SetValue(null, false);
            CustomerPartyStage1Preview.StartManual(ERestaurantFloorType.Floor1, false, false, null, 0);
        }
        catch (Exception error) { FinishPausePlayReview(Unwrap(error).Message); }
        finally { staff.SetValue(null, previousStaff); natural.SetValue(null, previousNatural); }
    }
    private static object PauseField(object value, string name)
        => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    private static T OriginalStageComponent<T>() where T : Component
        => SceneManager.GetSceneByPath(CustomerPartyStage1Preview.ScenePath).GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
    private static void CapturePausePlayLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(PausePlayKey, false) || _pausePlayReport == null || _pausePlayReport.stopping) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            _pausePlayReport.error = (_pausePlayReport.error ?? "") + "\n" + message;
        if (!string.IsNullOrEmpty(_pausePlayReport.receiptId)
            && message.StartsWith("POPUP_STORE_CUSTOMER_RELEASED receipt=" + _pausePlayReport.receiptId + "; slot=" + _pausePlayReport.slotIndex
                + "; customer=" + _pausePlayReport.customerId + "; order=" + _pausePlayReport.orderId + "; reason=Packed", StringComparison.Ordinal))
            _pausePlayReport.releasedIdentity = true;
        if (!string.IsNullOrEmpty(_pausePlayReport.completedReceiptId)
            && message.StartsWith("POPUP_STORE_CUSTOMER_RELEASED receipt=" + _pausePlayReport.completedReceiptId + "; slot=" + _pausePlayReport.completedSlotIndex
                + "; customer=" + _pausePlayReport.completedCustomerId + "; order=" + _pausePlayReport.completedOrderId + "; reason=Packed", StringComparison.Ordinal))
            _pausePlayReport.completedDeliveryReleased = true;
    }
    // Explicit, bounded original Stage1 PlayerLoop probe. No authored setting, focus state or account is assigned.
    // The session-lifetime mode alone shortens its owned memory clock after normal delivery to inspect completed re-entry.
    private static void TickPausePlayReview()
    {
        if (!SessionState.GetBool(PausePlayKey, false)) return;
        if (_pausePlayReport == null)
            _pausePlayReport = JsonUtility.FromJson<PausePlayReport>(SessionState.GetString(PausePlayReportKey, ""));
        if (_pausePlayReport == null) { SessionState.EraseBool(PausePlayKey); return; }
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (_pausePlayReport.stopping && !string.IsNullOrEmpty(_pausePlayReport.finishedUtc)
                && (DateTime.UtcNow - DateTime.Parse(_pausePlayReport.finishedUtc).ToUniversalTime()).TotalSeconds < 2) return;
            if (!_pausePlayReport.stopping)
            {
                if ((DateTime.UtcNow - DateTime.Parse(_pausePlayReport.startedUtc).ToUniversalTime()).TotalSeconds < 10) return;
                _pausePlayReport.error = "Play stopped before the automatic probe completed.";
            }
            _pausePlayReport.after = ReadWorkspace();
            _pausePlayReport.workspaceUnchanged = Same(_pausePlayReport.before, _pausePlayReport.after);
            _pausePlayReport.success = string.IsNullOrEmpty(_pausePlayReport.error) && _pausePlayReport.waitingFrozen
                && _pausePlayReport.deliveryFrozen && _pausePlayReport.inputsBlocked && _pausePlayReport.deliveredAndReleased
                && (!_pausePlayReport.sessionLifetime || (_pausePlayReport.abandonedWithoutResult && _pausePlayReport.freshAfterAbandon
                    && _pausePlayReport.completedDeliveryReleased && _pausePlayReport.completedFresh))
                && _pausePlayReport.workspaceUnchanged && !BackEnd.Backend.IsInitialized && !BackEnd.Backend.IsLogin;
            _pausePlayReport.finishedUtc = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(DirectoryPath + (_pausePlayReport.sessionLifetime ? "/session-play-result.json" : "/pause-play-result.json"), JsonUtility.ToJson(_pausePlayReport, true));
            SessionState.EraseBool(PausePlayKey); SessionState.EraseString(PausePlayReportKey); _pausePlay = null;
            Debug.Log("POPUP_STORE_PAUSE_PLAY_FINISHED success=" + _pausePlayReport.success + "; error=" + _pausePlayReport.error);
            return;
        }
        if (_pausePlayReport.stopping) return;
        try
        {
            if (!string.IsNullOrEmpty(_pausePlayReport.error)) throw new InvalidOperationException(_pausePlayReport.error);
            RequireRewardReview((DateTime.UtcNow - DateTime.Parse(_pausePlayReport.startedUtc).ToUniversalTime()).TotalSeconds < 65,
                "Original Play probe timed out (focus/visible game context must remain available).");
            if (!HasOriginalMemoryOwner()) return;
            int ownerId = Muks.BackEnd.BackendManager.ExistingInstance.GetInstanceID();
            if (_pausePlayReport.ownerId == 0) _pausePlayReport.ownerId = ownerId;
            RequireRewardReview(_pausePlayReport.ownerId == ownerId, "The probe's original memory owner was replaced.");
            if (!(bool)typeof(CustomerPartyStage1Preview).GetField("ready", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)) return;
            if (_pausePlay == null) _pausePlay = new PausePlayContext();
            var c = _pausePlay;
            _pausePlayReport.observedFrames++;
            if (c.step == 0)
            {
                c.camera = OriginalStageComponent<CameraController>(); c.main = OriginalStageComponent<UIMainCanvas>();
                c.nav = c.main.GetComponent<Muks.MobileUI.MobileUINavigation>();
                c.camera.MoveCamera(ERestaurantFloorType.Floor3, RestaurantType.Hall); c.step++; return;
            }
            if (c.step == 1)
            { if (c.camera.IsCameraMoving) return; c.main.OnShowFloor3UI(); c.step++; return; }
            if (c.step == 2)
            {
                var floor = c.nav.FirstView as UIFloor3;
                if (floor == null || floor.VisibleState != VisibleState.Appeared || !c.nav.ViewsVisibleStateCheck()) return;
                ((UnityEngine.UI.Button)PauseField(floor, "_popupStoreButton")).onClick.Invoke(); c.step++; return;
            }
            if (c.step == 9)
            {
                if (c.nav.FirstView is UIFloor3 && c.nav.ViewsVisibleStateCheck())
                { c.nav.Pop("UIFloor3"); FinishPausePlayReview(null); }
                return;
            }
            if (c.step == 10 || c.step == 15)
            {
                var floor = c.nav.FirstView as UIFloor3;
                if (floor == null || floor.VisibleState != VisibleState.Appeared || !c.nav.ViewsVisibleStateCheck()) return;
                ((UnityEngine.UI.Button)PauseField(floor, "_popupStoreButton")).onClick.Invoke();
                c.popup = null; c.step++; return;
            }
            if (c.popup == null) c.popup = c.nav.FirstView as UIPopupStore;
            if (c.popup == null || c.popup.VisibleState != VisibleState.Appeared) return;
            EnsureReviewAdmission(c.popup, _pausePlayReport.sessionLifetime ? "auto-session" : "auto-exit",
                _pausePlayReport.sessionLifetime ? (int?)3 : null);
            var clock = c.popup.GetComponent<PopupStoreBusinessClock>();
            var exit = c.popup.GetComponent<PopupStoreExitConfirmation>();
            var entrance = c.popup.GetComponent<PopupStoreCustomerEntrance>();
            var packing = c.popup.GetComponent<PopupStorePacking>();
            var visual = c.popup.GetComponent<PopupStorePackingPresentation>();
            var counter = c.popup.GetComponentInChildren<PopupStoreSalesCounter>(true);
            if (c.step == 3)
            {
                if (clock.State == PopupStoreBusinessState.NotStarted)
                { c.popup.GetComponent<PopupStoreStartController>().RequestStart(); return; }
                if (clock.State != PopupStoreBusinessState.Running || !entrance.IsWaiting(0)) return;
                var orders = c.popup.GetComponent<PopupStoreOrderDisplay>();
                RequireRewardReview(orders.TryGetEntryBinding(0, out var order, out _), "Waiting binding missing.");
                RequireRewardReview(order.TryResolve((PopupStoreMaterialCatalog)PauseField(orders, "_catalog"), out var materials, out var error), error);
                foreach (var item in materials) RequireRewardReview(counter.TryAdd(item), "Prepared material rejected.");
                RequireRewardReview(c.popup.TryRequestExit() && exit.IsVisible, "Waiting exit confirmation rejected.");
                RequireRewardReview(!counter.TryAdd(materials[0]), "Paused material API accepted input.");
                counter.Clear();
                RequireRewardReview(counter.Count == materials.Length && !packing.TryPack().Success, "Paused clear/packing modified sale.");
                _pausePlayReport.inputsBlocked = true;
                c.frozen = PausePlaySnapshot(c.popup); c.since = EditorApplication.timeSinceStartup; c.frame = Time.frameCount; c.step++; return;
            }
            if (c.step == 4 || c.step == 7)
            {
                RequireRewardReview(c.frozen == PausePlaySnapshot(c.popup), "Paused PlayerLoop changed clock, patience, material, motion or receipt state.");
                if (Time.frameCount == c.frame || EditorApplication.timeSinceStartup - c.since < 1.2) return;
                if (c.step == 4) _pausePlayReport.waitingFrozen = true; else _pausePlayReport.deliveryFrozen = true;
                RequireRewardReview(exit.TryContinue(), "Continue rejected."); c.step++; return;
            }
            if (c.step == 5)
            {
                if (clock.State != PopupStoreBusinessState.Running) return;
                var sale = packing.TryPack(); RequireRewardReview(sale.Success, "Prepared order did not pack after resume."); c.receipt = sale.Receipt;
                _pausePlayReport.receiptId = c.receipt.ReceiptId; _pausePlayReport.orderId = c.receipt.OrderId;
                _pausePlayReport.customerId = c.receipt.CustomerId; _pausePlayReport.slotIndex = c.receipt.SlotIndex; c.step++; return;
            }
            if (c.step == 6)
            {
                if (visual.CurrentPhase != PopupStorePackingPresentation.Phase.Move) return;
                RequireRewardReview(ReferenceEquals(visual.CurrentReceipt, c.receipt) && c.popup.TryRequestExit() && exit.IsVisible, "Delivery confirmation identity rejected.");
                c.frozen = PausePlaySnapshot(c.popup); c.since = EditorApplication.timeSinceStartup; c.frame = Time.frameCount; c.step++; return;
            }
            if (c.step == 8)
            {
                if (!ReferenceEquals(visual.LastDeliveredReceipt, c.receipt) || !_pausePlayReport.releasedIdentity) return;
                RequireRewardReview(visual.VisualOutcomes.Count == 1 && visual.VisualOutcomes[0].Kind == PopupStoreVisualOutcomeKind.VisualCompleted
                    && ReferenceEquals(visual.VisualOutcomes[0].Receipt, c.receipt)
                    && packing.SuccessfulPackCount == 1 && clock.ComplaintPenaltyCount == 0
                    && c.popup.GetComponent<PopupStoreScore>().TotalScore == 100, "Delivery/departure duplicated or normal sale received a penalty.");
                _pausePlayReport.deliveredAndReleased = true;
                if (_pausePlayReport.sessionLifetime)
                {
                    var grade = c.popup.GetComponent<PopupStoreGradeSession>();
                    var reward = c.popup.GetComponent<PopupStoreRewardSession>();
                    _pausePlayReport.abandonedSessionId = grade.SessionId;
                    RequireRewardReview(c.popup.TryRequestExit() && exit.IsVisible && exit.TryConfirmAbandon(), "Local abandonment was rejected.");
                    RequireRewardReview(clock.State == PopupStoreBusinessState.Abandoned && clock.EndReason == PopupStoreBusinessEndReason.DirectExit
                        && clock.AbandonCount == 1 && clock.RemainingSeconds > 0 && grade.FinalResult == null && grade.FinalizationCount == 0
                        && reward.Result == null && reward.RandomDrawCount == 0 && packing.CommittedReceipts.Count == 1
                        && c.popup.GetComponent<PopupStoreScore>().TotalScore == 100, "Abandonment finalized a result, lost history or changed score.");
                    _pausePlayReport.abandonedWithoutResult = true;
                    _pausePlayReport.abandonedScore = c.popup.GetComponent<PopupStoreScore>().TotalScore;
                    _pausePlayReport.abandonedReceiptCount = packing.CommittedReceipts.Count;
                    c.step = 10; return;
                }
                // Explicit probe cleanup, not a user abandonment action. No business result is fabricated.
                c.nav.PopNoAnime(UIPopupStore.ViewName); c.step++; return;
            }
            if (c.step == 11 || c.step == 16)
            {
                if (clock.State == PopupStoreBusinessState.NotStarted)
                { c.popup.GetComponent<PopupStoreStartController>().RequestStart(); return; }
                if (clock.State != PopupStoreBusinessState.Running) return;
                var grade = c.popup.GetComponent<PopupStoreGradeSession>();
                string previous = c.step == 11 ? _pausePlayReport.abandonedSessionId : _pausePlayReport.completedSessionId;
                RequireRewardReview(grade.SessionId != previous && !string.IsNullOrEmpty(grade.SessionId)
                    && grade.FinalResult == null && grade.FinalizationCount == 0 && clock.RemainingSeconds > 85 && clock.RemainingSeconds <= 90
                    && clock.AbandonCount == 0 && counter.Count == 0 && packing.SuccessfulPackCount == 0 && packing.CommittedReceipts.Count == 0
                    && c.popup.GetComponent<PopupStoreScore>().TotalScore == 0 && c.popup.GetComponent<PopupStoreRewardSession>().Result == null
                    && c.popup.GetComponent<PopupStoreRewardSession>().RandomDrawCount == 0, "A terminal business leaked state into the next map entry.");
                if (c.step == 11)
                { _pausePlayReport.firstFreshSessionId = grade.SessionId; _pausePlayReport.freshAfterAbandon = true; c.step = 12; }
                else
                {
                    _pausePlayReport.nextFreshSessionId = grade.SessionId; _pausePlayReport.completedFresh = true;
                    c.nav.PopNoAnime(UIPopupStore.ViewName); c.step = 9;
                }
                return;
            }
            if (c.step == 12)
            {
                if (!entrance.IsWaiting(0) || clock.State != PopupStoreBusinessState.Running) return;
                var orders = c.popup.GetComponent<PopupStoreOrderDisplay>();
                RequireRewardReview(orders.TryGetEntryBinding(0, out var order, out _), "Fresh waiting order missing.");
                RequireRewardReview(order.TryResolve((PopupStoreMaterialCatalog)PauseField(orders, "_catalog"), out var materials, out var error), error);
                foreach (var item in materials) RequireRewardReview(counter.TryAdd(item), "Fresh material rejected.");
                var sale = packing.TryPack(); RequireRewardReview(sale.Success, "Fresh business could not pack its first order.");
                c.receipt = sale.Receipt;
                _pausePlayReport.completedReceiptId = c.receipt.ReceiptId; _pausePlayReport.completedOrderId = c.receipt.OrderId;
                _pausePlayReport.completedCustomerId = c.receipt.CustomerId; _pausePlayReport.completedSlotIndex = c.receipt.SlotIndex;
                c.step = 13; return;
            }
            if (c.step == 13)
            {
                if (!ReferenceEquals(visual.LastDeliveredReceipt, c.receipt) || !_pausePlayReport.completedDeliveryReleased) return;
                RequireRewardReview(visual.CurrentPhase == PopupStorePackingPresentation.Phase.Idle && visual.VisualOutcomes.Count == 1
                    && visual.VisualOutcomes[0].Kind == PopupStoreVisualOutcomeKind.VisualCompleted && packing.SuccessfulPackCount == 1
                    && c.popup.GetComponent<PopupStoreScore>().TotalScore == 100 && clock.ComplaintPenaltyCount == 0,
                    "Fresh business normal delivery did not finish exactly once.");
                // Editor memory owner was checked above. Complete only this clock, after its real PlayerLoop delivery.
                RequireRewardReview(clock.AdvanceTime(clock.RemainingSeconds), "Owned memory deadline could not be shortened.");
                c.step = 14; return;
            }
            if (c.step == 14)
            {
                var result = c.popup.GetComponent<PopupStoreResultPanel>();
                if (!result.IsVisible || result.IsAppearing) return;
                var grade = c.popup.GetComponent<PopupStoreGradeSession>(); var final = grade.FinalResult;
                RequireRewardReview(final != null && final.Score == 100 && final.SuccessfulOrders == 1
                    && final.Grade == PopupStoreGradeTier.C && grade.FinalizationCount == 1 && clock.EndReason == PopupStoreBusinessEndReason.Completed,
                    "Normal memory business result changed before acknowledgement.");
                _pausePlayReport.completedSessionId = final.SessionId; _pausePlayReport.completedScore = final.Score;
                _pausePlayReport.completedOrders = final.SuccessfulOrders; _pausePlayReport.completedFinalizations = grade.FinalizationCount;
                RequireRewardReview(result.TryConfirm(), "Completed result acknowledgement did not return to the map.");
                c.step = 15; return;
            }
        }
        catch (Exception error) { FinishPausePlayReview(Unwrap(error).Message); }
    }
    private static string PausePlaySnapshot(UIPopupStore popup)
    {
        var clock = popup.GetComponent<PopupStoreBusinessClock>(); var entrance = popup.GetComponent<PopupStoreCustomerEntrance>();
        var visual = popup.GetComponent<PopupStorePackingPresentation>(); var patience = popup.GetComponent<PopupStoreCustomerPatience>();
        var packing = popup.GetComponent<PopupStorePacking>(); var orders = popup.GetComponent<PopupStoreOrderDisplay>();
        var image = (UnityEngine.UI.Image)PauseField(visual, "_resultImage");
        var values = new List<string> { clock.RemainingSeconds.ToString("R"), clock.ElapsedSeconds.ToString("R"), clock.State.ToString(),
            entrance.SuppliedOrderCount.ToString(), entrance.ReleasedCount.ToString(), PauseField(entrance, "_visibleElapsed").ToString(),
            visual.CurrentPhase.ToString(), PauseField(visual, "_elapsed").ToString(), visual.CurrentReceipt?.ReceiptId ?? "",
            JsonUtility.ToJson(image.rectTransform.anchoredPosition3D), JsonUtility.ToJson(image.rectTransform.localScale),
            popup.GetComponent<PopupStoreScore>().TotalScore.ToString(), clock.ComplaintPenaltyCount.ToString(),
            string.Join(",", packing.CommittedReceipts.Select(r => r.ReceiptId)),
            string.Join(",", popup.GetComponentInChildren<PopupStoreSalesCounter>(true).Materials.Select(m => m.Id)) };
        for (int i = 0; i < 3; i++)
        {
            values.Add(entrance.GetState(i).ToString()); values.Add(patience.GetTimer(i)?.RemainingPatience.ToString("R") ?? "");
            if (orders.TryGetEntryBinding(i, out var order, out var customer))
            { values.Add(order.RuntimeId); values.Add(JsonUtility.ToJson(customer.rectTransform.anchoredPosition3D)); }
        }
        return string.Join("|", values);
    }
    private static void FinishPausePlayReview(string error)
    {
        if (_pausePlayReport == null || _pausePlayReport.stopping) return;
        _pausePlayReport.error = error ?? _pausePlayReport.error; _pausePlayReport.stopping = true; _pausePlayReport.finishedUtc = DateTime.UtcNow.ToString("O");
        SessionState.SetString(PausePlayReportKey, JsonUtility.ToJson(_pausePlayReport));
        // Stop only the explicitly owned original memory session. Its starter alone restores user workspace.
        if (HasOriginalMemoryOwner() && _pausePlayReport.ownerId != 0
            && Muks.BackEnd.BackendManager.ExistingInstance.GetInstanceID() == _pausePlayReport.ownerId)
            CustomerPartyStage1Preview.StopManual();
        else if (EditorApplication.isPlaying)
        {
            _pausePlayReport.error = (_pausePlayReport.error ?? "") + " Original memory owner is no longer provable; no other Play session was stopped.";
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(DirectoryPath + (_pausePlayReport.sessionLifetime ? "/session-play-result.json" : "/pause-play-result.json"), JsonUtility.ToJson(_pausePlayReport, true));
            SessionState.EraseBool(PausePlayKey); SessionState.EraseString(PausePlayReportKey);
        }
    }

    private static bool HasOriginalMemoryOwner()
    {
        if (!EditorApplication.isPlaying || !CustomerPartyManualPlayBootstrap.Active
            || !SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false)
            || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin
            || !SceneManager.GetSceneByPath(CustomerPartyStage1Preview.ScenePath).isLoaded) return false;
        var memory = typeof(CustomerPartyStage1Preview).GetField("memory", BindingFlags.Static | BindingFlags.NonPublic)
            ?.GetValue(null) as CustomerPartyOwnedMemory;
        var owner = Muks.BackEnd.BackendManager.ExistingInstance;
        return memory != null && memory.LoggedIn && owner != null && owner.IsEditorOfflineOwner
            && ReferenceEquals(owner, memory.Owner);
    }

    private static void EnsureReviewAdmission(UIPopupStore popup, string label, int? isolatedLimit = null)
    {
        RequireRewardReview(HasOriginalMemoryOwner(), "Explicit admission review requires the original isolated memory owner.");
        if (_accessReviewOwner == null)
        {
            // A domain reload cannot silently erase quota/pending starts and mint another fake account in the same Play.
            RequireRewardReview(!SessionState.GetBool(AccessReviewCreatedKey, false),
                "The admission review owner was lost; stop this review instead of resetting its usage.");
            _accessReviewOwner = new PopupStoreAccessReviewOwner(label, isolatedLimit);
            SessionState.SetBool(AccessReviewCreatedKey, true);
        }
        _accessReviewOwner.Bind(popup);
    }

    private static void ApplyResultReviewOnce()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(ResultReviewKey, false)) return;
        if (!SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false)
            || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
        { ClearReviewRequest(); Debug.LogWarning("POPUP_STORE_RESULT_REVIEW_REFUSED memory-session fence"); return; }
        if (!CustomerPartyManualPlayBootstrap.Active
            || !SceneManager.GetSceneByPath(CustomerPartyStage1Preview.ScenePath).isLoaded) return;
        // Manual Play intentionally installs an offline BackendManager. Accept only that exact memory owner,
        // never a live singleton (nor merely any object with an offline-looking name).
        if (!HasOriginalMemoryOwner())
        { ClearReviewRequest(); Debug.LogWarning("POPUP_STORE_RESULT_REVIEW_REFUSED unowned memory backend"); return; }
        var mode = (ReviewMode)SessionState.GetInt(ReviewModeKey, (int)ReviewMode.Result);
        if (mode == ReviewMode.Session)
        {
            try { ApplySessionReviewOnce(); }
            catch (Exception error) { ClearReviewRequest(); Debug.LogWarning("POPUP_STORE_ACCESS_REVIEW_REFUSED " + Unwrap(error).Message); }
            return;
        }
        var views = Resources.FindObjectsOfTypeAll<UIPopupStore>()
            .Where(view => !EditorUtility.IsPersistent(view) && view.gameObject.scene.IsValid()).ToArray();
        if (views.Length == 0) return; // Stage1 is still loading; do not create anything.
        if (views.Length != 1) { ClearReviewRequest(); Debug.LogWarning("POPUP_STORE_RESULT_REVIEW_REFUSED ambiguous popup"); return; }
        var popup = views[0];
        try
        {
            EnsureReviewAdmission(popup, mode.ToString());
            if (mode == ReviewMode.AccessA || mode == ReviewMode.AccessB || mode == ReviewMode.R1C || mode == ReviewMode.R1SSS)
                RequireRewardReview(_accessReviewOwner.DailyLimit == 1, "A/B review requires the approved one-use settings snapshot.");
            if (mode == ReviewMode.R1C || mode == ReviewMode.R1SSS)
                EnsureFixedR1ReviewSource(popup, mode == ReviewMode.R1C ? 0 : 997);
        }
        catch (Exception error) { ClearReviewRequest(); Debug.LogWarning("POPUP_STORE_ACCESS_REVIEW_REFUSED " + Unwrap(error).Message); return; }
        if (mode == ReviewMode.AccessA)
        { ClearReviewRequest(); Debug.Log("POPUP_STORE_ACCESS_REVIEW_READY mode=A; userStartRequired=True; business=90", popup); return; }
        if (mode == ReviewMode.RewardS || mode == ReviewMode.R1SSS)
        {
            var pendingClock = popup.GetComponent<PopupStoreBusinessClock>();
            if (pendingClock != null && pendingClock.State != PopupStoreBusinessState.Ended
                && (!popup.gameObject.activeInHierarchy || popup.VisibleState != VisibleState.Appeared
                    || pendingClock.State == PopupStoreBusinessState.NotStarted
                    || pendingClock.State == PopupStoreBusinessState.Paused)) return;
            ClearReviewRequest(); // One bounded preparation attempt, never a retry of a partial sale sequence.
            try { PrepareRewardS(popup, mode == ReviewMode.R1SSS); }
            catch (Exception error) { Debug.LogWarning("POPUP_STORE_REWARD_REVIEW_REFUSED " + mode + ": " + Unwrap(error).Message, popup); }
            return;
        }
        ClearReviewRequest(); // One attempt; never overwrite a started/reentered session.
        if (mode != ReviewMode.Result && mode != ReviewMode.RewardC && mode != ReviewMode.AccessB && mode != ReviewMode.R1C)
        { Debug.LogWarning("POPUP_STORE_RESULT_REVIEW_REFUSED unknown review mode"); return; }
        var clock = popup.GetComponent<PopupStoreBusinessClock>();
        var presentation = popup.GetComponent<PopupStorePackingPresentation>();
        var packing = popup.GetComponent<PopupStorePacking>();
        var grade = popup.GetComponent<PopupStoreGradeSession>();
        var score = popup.GetComponent<PopupStoreScore>();
        var counter = popup.GetComponentInChildren<PopupStoreSalesCounter>(true);
        if (popup.gameObject.activeInHierarchy || clock == null || presentation == null || packing == null
            || grade == null || score == null || counter == null || counter.GetComponentInParent<UIPopupStore>(true) != popup
            || clock.State != PopupStoreBusinessState.NotStarted || counter.Count != 0
            || packing.CommittedReceipts.Count != 0 || grade.IsFinalized || score.TotalScore != 0 || score.SuccessfulOrderCount != 0)
        { Debug.LogWarning("POPUP_STORE_RESULT_REVIEW_REFUSED popup already active or started"); return; }
        var duration = typeof(PopupStoreBusinessClock).GetField("_initialSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
        var move = typeof(PopupStorePackingPresentation).GetField("_moveDuration", BindingFlags.Instance | BindingFlags.NonPublic);
        if (duration == null || move == null) { Debug.LogWarning("POPUP_STORE_RESULT_REVIEW_REFUSED missing fields"); return; }
        var previousDuration = duration.GetValue(clock);
        var previousMove = move.GetValue(presentation);
        // Only these review fields on the disposable Play instance; C retains the authored delivery duration.
        duration.SetValue(clock, 30d);
        if (mode == ReviewMode.Result) move.SetValue(presentation, 30f);
        Debug.Log("POPUP_STORE_RESULT_REVIEW_APPLIED popup=" + popup.GetInstanceID()
            + "; mode=" + mode + "; business=30; delivery=" + (mode == ReviewMode.Result ? "30" : "authored")
            + "; originalBusiness=" + previousDuration + "; originalDelivery=" + previousMove, popup);
    }

    private static void CaptureSessionReviewLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(ResultReviewKey, false)
            || (ReviewMode)SessionState.GetInt(ReviewModeKey, -1) != ReviewMode.Session || !HasOriginalMemoryOwner()) return;
        string first = SessionState.GetString(SessionReviewFirstKey, "");
        if (!string.IsNullOrEmpty(first)
            && message.StartsWith("POPUP_STORE_SESSION_ABANDONED session=" + first + ";", StringComparison.Ordinal))
            SessionState.SetBool(SessionReviewAbandonedKey, true);
    }

    private static void ApplySessionReviewOnce()
    {
        // Passive user A/B preparation. Never selects materials, packs, abandons, starts a session,
        // changes serialized settings, or operates a live account. Only one real second-session sale enables shortening.
        var views = Resources.FindObjectsOfTypeAll<UIPopupStore>()
            .Where(view => !EditorUtility.IsPersistent(view) && view.gameObject.scene.IsValid()
                && view.gameObject.activeInHierarchy && view.VisibleState == VisibleState.Appeared).ToArray();
        if (views.Length != 1) return;
        var popup = views[0]; var grade = popup.GetComponent<PopupStoreGradeSession>();
        EnsureReviewAdmission(popup, "session-manual", 3);
        if (grade == null || string.IsNullOrEmpty(grade.SessionId)) return;
        string first = SessionState.GetString(SessionReviewFirstKey, "");
        if (string.IsNullOrEmpty(first))
        {
            SessionState.SetString(SessionReviewFirstKey, grade.SessionId);
            Debug.Log("POPUP_STORE_SESSION_REVIEW_OBSERVING first=" + grade.SessionId + "; authored duration unchanged", popup);
            return;
        }
        if (grade.SessionId == first || !SessionState.GetBool(SessionReviewAbandonedKey, false)) return;
        var clock = popup.GetComponent<PopupStoreBusinessClock>(); var packing = popup.GetComponent<PopupStorePacking>();
        var score = popup.GetComponent<PopupStoreScore>();
        if (clock == null || packing == null || score == null || clock.State != PopupStoreBusinessState.Running
            || packing.CommittedReceipts.Count != 1 || packing.SuccessfulPackCount != 1 || score.SuccessfulOrderCount != 1
            || grade.IsFinalized || score.TotalScore != 100) return;
        string next = grade.SessionId;
        ClearReviewRequest(); // Exactly one preparation, including a refused attempt. Later businesses remain authored 90 seconds.
        double before = clock.RemainingSeconds;
        if (!HasOriginalMemoryOwner() || !clock.AdvanceTime(Math.Max(0d, before - 15d)))
        { Debug.LogWarning("POPUP_STORE_SESSION_REVIEW_REFUSED second-session clock shortening", popup); return; }
        Debug.Log("POPUP_STORE_SESSION_REVIEW_SHORTENED first=" + first + "; next=" + next
            + "; receipt=" + packing.CommittedReceipts[0].ReceiptId + "; before=" + before
            + "; remaining=" + clock.RemainingSeconds + "; serialized settings unchanged", popup);
    }

    private sealed class FixedR1ReviewRandom : IPopupStoreRewardRandomSource
    {
        internal readonly int Roll;
        internal int Calls { get; private set; }
        internal FixedR1ReviewRandom(int roll) { Roll = roll; }
        public int NextInt(int exclusiveUpperBound)
        {
            if (exclusiveUpperBound != 1000 || (Roll != 0 && Roll != 997) || Calls != 0)
                throw new InvalidOperationException("The explicitly armed R1 review permits one fixed 1000-unit draw.");
            Calls++;
            Debug.Log("POPUP_STORE_R1_FIXED_DRAW roll=" + Roll + "; upper=" + exclusiveUpperBound + "; calls=" + Calls
                + "; editorReview=True; actualRandomWin=False; unpaid=True");
            return Roll;
        }
    }

    private static void EnsureFixedR1ReviewSource(UIPopupStore popup, int roll)
    {
        RequireRewardReview(HasOriginalMemoryOwner() && popup != null && !EditorUtility.IsPersistent(popup)
            && _accessReviewOwner != null && _accessReviewOwner.DailyLimit == 1,
            "Fixed R1 review needs the original isolated one-use memory owner.");
        var reward = popup.GetComponent<PopupStoreRewardSession>();
        var table = reward != null ? PauseField(reward, "_table") as PopupStoreRewardTable : null;
        string error = null;
        RequireRewardReview(table != null && table.PolicyId == PopupStoreRewardTable.R1PolicyId
            && table.Schema == PopupStoreRewardSchema.R1Permille && table.Denominator == 1000 && table.TryValidate(out error),
            "The authored active R1 reward policy is required: " + error);
        if (_r1ReviewReward != null)
        {
            RequireRewardReview(ReferenceEquals(_r1ReviewReward, reward) && _r1ReviewRandom != null && _r1ReviewRandom.Roll == roll,
                "An armed review cannot transfer its fixed draw to another popup or policy.");
            return;
        }
        RequireRewardReview(reward != null && !reward.IsResolved && reward.Result == null && reward.RandomDrawCount == 0,
            "Fixed review randomness must be supplied before any terminal reward decision.");
        var source = new FixedR1ReviewRandom(roll);
        RequireRewardReview(reward.TrySetRandomSource(source), "The fixed R1 review source could not be installed.");
        _r1ReviewReward = reward; _r1ReviewRandom = source;
        Debug.Log("POPUP_STORE_R1_REVIEW_INJECTED popup=" + popup.GetInstanceID() + "; policy=" + table.PolicyId
            + "; roll=" + roll + "; isolatedAccount=" + _accessReviewOwner.AccountId
            + "; editorOnly=True; permanentSettingsChanged=False; actualRandomWin=False", popup);
    }

    private static void PrepareRewardS(UIPopupStore popup, bool allTwentyForR1 = false)
    {
        var targetGrade = allTwentyForR1 ? PopupStoreGradeTier.SSS : PopupStoreGradeTier.S;
        RequireRewardReview(HasOriginalMemoryOwner() && popup != null && popup.gameObject.activeInHierarchy
            && popup.VisibleState == VisibleState.Appeared, "Original visible memory popup is required.");
        var clock = popup.GetComponent<PopupStoreBusinessClock>();
        var entrance = popup.GetComponent<PopupStoreCustomerEntrance>();
        var presentation = popup.GetComponent<PopupStorePackingPresentation>();
        var orders = popup.GetComponent<PopupStoreOrderDisplay>();
        var packing = popup.GetComponent<PopupStorePacking>();
        var score = popup.GetComponent<PopupStoreScore>();
        var grade = popup.GetComponent<PopupStoreGradeSession>();
        var counter = popup.GetComponentInChildren<PopupStoreSalesCounter>(true);
        RequireRewardReview(clock != null && entrance != null && presentation != null && orders != null
            && packing != null && score != null && grade != null && counter != null
            && counter.GetComponentInParent<UIPopupStore>(true) == popup, "Local review components are required.");
        RequireRewardReview(clock.State == PopupStoreBusinessState.Running && clock.RemainingSeconds > 10
            && entrance.IsReady && entrance.IsRunning && presentation.IsReady && orders.IsReady && packing.IsReady
            && counter.IsReady && counter.Count == 0 && score.TotalScore == 0 && score.SuccessfulOrderCount == 0
            && packing.CommittedReceipts.Count == 0 && !presentation.HasPendingVisualWork && !grade.IsFinalized
            && entrance.StartedComplaintCount == 0 && entrance.StartedDepartureCount == 0 && entrance.SuppliedOrderCount <= 3,
            "The review requires a fresh Running popup with an empty counter and no sales or departures.");
        if (allTwentyForR1)
        {
            RequireRewardReview(clock.HasStartApproval && _r1ReviewReward != null && _r1ReviewReward.HasPolicySnapshot
                && _r1ReviewReward.PolicySnapshot.PolicyId == PopupStoreRewardTable.R1PolicyId
                && _r1ReviewReward.PolicySnapshot.Denominator == 1000 && _r1ReviewReward.RandomDrawCount == 0
                && _r1ReviewReward.Result == null && _accessReviewOwner.Host.LocalStartCount == 1
                && _accessReviewOwner.Host.BindingCount == 1 && _accessReviewOwner.Store.IssuedBusinessCount == 1,
                "SSS review requires one explicit approved start and its unchanged, undrawn R1 policy snapshot.");
            Debug.Log("POPUP_STORE_R1_REVIEW_APPROVED session=" + grade.SessionId
                + "; policy=" + _r1ReviewReward.PolicySnapshot.PolicyId + "; units=1000; starts=1; issued=1; draws=0", popup);
        }
        var catalog = typeof(PopupStoreOrderDisplay).GetField("_catalog", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(orders) as PopupStoreMaterialCatalog;
        RequireRewardReview(catalog != null, "The existing approved material catalog is required.");
        long money = UserInfo.Money; int diamonds = UserInfo.Dia, visits = UserInfo.TotalCumulativeCustomerCount;
        var owner = Muks.BackEnd.BackendManager.ExistingInstance;
        bool settled = false;
        // Only existing local APIs are advanced. Score/grade/final-result fields and account APIs are never assigned.
        for (int step = 0; step < 2000; step++)
        {
            RequireRewardReview(HasOriginalMemoryOwner() && ReferenceEquals(owner, Muks.BackEnd.BackendManager.ExistingInstance)
                && clock.State == PopupStoreBusinessState.Running && popup.gameObject.activeInHierarchy,
                "The original Running memory context changed during S preparation.");
            presentation.TickPresentation(.5f);
            entrance.TickEntrance(.5f);
            bool leaving = Enumerable.Range(0, 3).Any(index => entrance.GetState(index) == PopupStoreCustomerState.Leaving
                || entrance.GetState(index) == PopupStoreCustomerState.Expired);
            if (presentation.HasPendingVisualWork || leaving) continue;
            if (allTwentyForR1 ? score.SuccessfulOrderCount == 20 : score.TotalScore >= 1700)
            { settled = true; break; }
            int slot = Enumerable.Range(0, 3).Where(entrance.IsWaiting).DefaultIfEmpty(-1).First();
            if (slot < 0) continue;
            RequireRewardReview(counter.Count == 0, "The review cannot replace existing counter materials.");
            RequireRewardReview(orders.TryGetEntryBinding(slot, out var order, out _), "Waiting order binding is invalid.");
            RequireRewardReview(order.TryResolve(catalog, out var materials, out string error), error);
            foreach (var material in materials)
                RequireRewardReview(counter.TryAdd(material), "Approved material selection was rejected.");
            var packed = packing.TryPack();
            RequireRewardReview(packed.Success && packing.IsCommittedReceipt(packed.Receipt)
                && packed.Receipt.OrderId == order.RuntimeId && packed.Receipt.CustomerId == order.CustomerId
                && packed.Receipt.SlotIndex == slot, "The actual order could not be packed uniquely.");
        }
        RequireRewardReview(settled && (allTwentyForR1 ? score.TotalScore == 2640 && score.SuccessfulOrderCount == 20
                : score.TotalScore >= 1700 && score.TotalScore < 2100)
            && grade.Current.Grade == targetGrade && !grade.IsFinalized && counter.Count == 0
            && presentation.VisualOutcomes.Count == packing.CommittedReceipts.Count
            && presentation.VisualOutcomes.All(outcome => outcome.Kind == PopupStoreVisualOutcomeKind.VisualCompleted)
            && presentation.CompletedDeliveryCount == packing.CommittedReceipts.Count
            && entrance.ReleasedCount == packing.CommittedReceipts.Count,
            "The review did not settle all actual successful deliveries and departures within its bound.");
        RequireRewardReview(HasOriginalMemoryOwner() && ReferenceEquals(owner, Muks.BackEnd.BackendManager.ExistingInstance)
            && UserInfo.Money == money && UserInfo.Dia == diamonds && UserInfo.TotalCumulativeCustomerCount == visits,
            "The isolated account or memory owner changed during preparation.");
        RequireRewardReview(clock.State == PopupStoreBusinessState.Running && clock.AdvanceTime(clock.RemainingSeconds),
            "Only a Running review clock can reach the normal closing boundary.");
        RequireRewardReview(clock.State == PopupStoreBusinessState.Ended && grade.FinalResult != null
            && grade.FinalResult.Grade == targetGrade && grade.FinalizationCount == 1
            && grade.FinalResult.Score == score.TotalScore && grade.FinalResult.SuccessfulOrders == score.SuccessfulOrderCount,
            "The normal closing boundary did not produce one unchanged target-grade result.");
        if (allTwentyForR1)
        {
            var decision = _r1ReviewReward != null ? _r1ReviewReward.Result : null;
            RequireRewardReview(decision != null && decision.Grade == PopupStoreGradeTier.SSS
                && decision.Kind == PopupStoreRewardKind.Jackpot && decision.Diamonds == 300
                && decision.PolicyId == PopupStoreRewardTable.R1PolicyId && _r1ReviewRandom.Calls == 1
                && _r1ReviewReward.RandomDrawCount == 1 && _r1ReviewReward.DecisionCount == 1,
                "The normal finalization did not produce exactly one fixed R1 300D local preview.");
        }
        RequireRewardReview(HasOriginalMemoryOwner() && ReferenceEquals(owner, Muks.BackEnd.BackendManager.ExistingInstance)
            && UserInfo.Money == money && UserInfo.Dia == diamonds && UserInfo.TotalCumulativeCustomerCount == visits,
            "The closing boundary changed the isolated account or memory owner.");
        // ResultPanel is deliberately not ticked here: its next normal LateUpdate owns the visible animation.
        Debug.Log("POPUP_STORE_REWARD_REVIEW_PREPARED mode=" + targetGrade + "; popup=" + popup.GetInstanceID()
            + "; session=" + grade.SessionId + "; score=" + score.TotalScore + "; receipts=" + packing.CommittedReceipts.Count
            + "; delivered=" + presentation.CompletedDeliveryCount + "; released=" + entrance.ReleasedCount, popup);
    }

    private static void RequireRewardReview(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message ?? "Reward review prerequisite failed.");
    }

    private static void InspectWorkspace()
    {
        var inspection = new Inspection { capturedUtc = DateTime.UtcNow.ToString("O"),
            playing = EditorApplication.isPlaying, compiling = EditorApplication.isCompiling, updating = EditorApplication.isUpdating };
        try
        {
            inspection.sdkInitialized = BackEnd.Backend.IsInitialized;
            inspection.isLogin = BackEnd.Backend.IsLogin;
            inspection.gameManagerIds = Resources.FindObjectsOfTypeAll<GameManager>()
                .Where(manager => manager.gameObject.scene.IsValid()).Select(manager => manager.GetInstanceID()).OrderBy(id => id).ToArray();
            inspection.challengeManagerIds = Resources.FindObjectsOfTypeAll<ChallengeManager>()
                .Where(manager => manager.gameObject.scene.IsValid()).Select(manager => manager.GetInstanceID()).OrderBy(id => id).ToArray();
            inspection.workspace = ReadWorkspace();
        }
        catch (Exception error) { inspection.error = Unwrap(error).ToString(); }
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(DirectoryPath + "/inspection.json", JsonUtility.ToJson(inspection, true));
    }

    private static void Run(string request)
    {
        _running = true; _abort = false; Cases.Clear(); Logs.Clear();
        _editorThread = Thread.CurrentThread.ManagedThreadId;
        _report = new Report
        {
            startedUtc = DateTime.UtcNow.ToString("O"), compiling = EditorApplication.isCompiling,
            updating = EditorApplication.isUpdating, scriptCompilationFailed = EditorUtility.scriptCompilationFailed
        };
        Application.logMessageReceived += CaptureLog;
        try
        {
            if (request != "run") throw new InvalidOperationException("Only the explicit request 'run' is supported.");
            if (_report.compiling || _report.updating || _report.scriptCompilationFailed)
                throw new InvalidOperationException("Compilation must finish successfully before popup-store verification.");
            var activeRunMethod = typeof(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi).GetMethod("IsRunActive",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (activeRunMethod == null || (bool)activeRunMethod.Invoke(null, null))
                throw new InvalidOperationException("The existing Unity Test Runner must be idle; only its status is read, never its scene-changing launcher.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin
                || Muks.BackEnd.BackendManager.ExistingInstance != null)
                throw new InvalidOperationException("Verification requires idle Edit Mode without a live backend account.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Close Prefab editing before this memory-only verification; no automatic closing is performed.");
            var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            if (scenes.Length == 0 || scenes.Any(scene => scene.isDirty))
                throw new InvalidOperationException("A loaded, unmodified edit scene is required. User scenes will not be saved or discarded.");
            _report.before = ReadWorkspace();
            ExecuteFixtures();
        }
        catch (Exception error) { _report.error = Unwrap(error).ToString(); }
        finally
        {
            try
            {
                _report.after = ReadWorkspace();
                _report.workspaceUnchanged = _report.before != null && Same(_report.before, _report.after);
                if (_report.before != null && !_report.workspaceUnchanged)
                    _report.error = (_report.error ?? "") + "\nWorkspace changed. No automatic restoration was attempted.";
            }
            catch (Exception error) { _report.error = (_report.error ?? "") + "\nWorkspace evidence: " + Unwrap(error); }
            Application.logMessageReceived -= CaptureLog;
            _report.tests = Cases.ToArray(); _report.logs = Logs.ToArray();
            _report.total = Cases.Count;
            _report.passed = Cases.Count(test => test.status == "Passed");
            _report.failed = Cases.Count(test => test.status == "Failed");
            _report.skipped = Cases.Count(test => test.status == "Skipped");
            _report.inconclusive = Cases.Count(test => test.status == "Inconclusive");
            _report.finishedUtc = DateTime.UtcNow.ToString("O");
            _report.success = string.IsNullOrEmpty(_report.error) && _report.workspaceUnchanged
                && _report.total > 0 && _report.total == _report.discovered && _report.passed == _report.total;
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                File.WriteAllText(DirectoryPath + "/result.json", JsonUtility.ToJson(_report, true));
            }
            finally { _running = false; }
            Debug.Log("POPUP_STORE_VERIFICATION_COMPLETED: success=" + _report.success + "; total=" + _report.total
                + "; passed=" + _report.passed + "; failed=" + _report.failed + "; result=" + DirectoryPath + "/result.json");
        }
    }

    private static void ExecuteFixtures()
    {
        var assembly = typeof(PopupStoreVerificationRunner).Assembly;
        var names = new List<string>();
        foreach (string fixtureName in Fixtures)
        {
            Type fixture = assembly.GetType(fixtureName, true);
            int beforeCount = names.Count;
            foreach (var method in fixture.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                string[] attributes = method.GetCustomAttributes(false).Select(attribute => attribute.GetType().Name).ToArray();
                if (attributes.Contains("UnityTestAttribute"))
                    throw new NotSupportedException("Coroutine tests are outside this synchronous memory-only bridge: " + method.Name);
                if (!attributes.Any(name => name == "TestAttribute" || name == "TestCaseAttribute" || name == "TestCaseSourceAttribute")) continue;
                if (method.ReturnType != typeof(void))
                    throw new NotSupportedException("Only synchronous void tests are allowed: " + method.Name);
                names.Add(fixture.FullName + "." + method.Name);
            }
            if (names.Count == beforeCount) throw new InvalidOperationException("No cases found in " + fixtureName);
        }
        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        ITest tree = runner.Load(assembly, new Dictionary<string, object>
        {
            { "NumberOfTestWorkers", 0 }, { "TestNames", names }, { "RunOnMainThread", true }
        });
        var methods = new HashSet<string>(names, StringComparer.Ordinal);
        string[] leaves = Flatten(tree).Where(test => !test.IsSuite && methods.Contains(test.ClassName + "." + test.MethodName))
            .Select(test => test.FullName).ToArray();
        _report.discovered = leaves.Length;
        if (leaves.Length == 0) throw new InvalidOperationException("NUnit discovered no popup-store cases.");
        var filter = new OrFilter(leaves.Select(name => (ITestFilter)new FullNameFilter(name)).ToArray());
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo createContext = typeof(NUnitTestAssemblyRunner).GetMethod("CreateTestExecutionContext", flags);
        PropertyInfo contextProperty = typeof(NUnitTestAssemblyRunner).GetProperty("Context", flags);
        if (createContext == null || contextProperty == null)
            throw new NotSupportedException("The installed NUnit inline execution API is unavailable.");
        using (var listener = new Listener())
        {
            createContext.Invoke(runner, new object[] { listener });
            var context = (TestExecutionContext)contextProperty.GetValue(runner);
            context.Dispatcher = new InlineDispatcher(); context.IsSingleThreaded = true;
            context.ParallelScope = NUnit.Framework.ParallelScope.None; context.TestCaseTimeout = 0;
            WorkItem work = WorkItem.CreateWorkItem(tree, filter);
            work.InitializeContext(context);
            TestExecutionContext previous = TestExecutionContext.CurrentContext;
            try { RequireEditorThread(); work.Execute(); }
            finally { SetExecutionContext(previous); }
            if (work.State.ToString() != "Complete")
                throw new InvalidOperationException("NUnit did not finish synchronously; no background or blocking execution is allowed.");
            if (work.Result.ResultState.Status != TestStatus.Passed)
                _report.error = (_report.error ?? "") + "\nNUnit assembly result: " + work.Result.ResultState
                    + "\n" + work.Result.Message;
        }
    }

    private sealed class Listener : ITestListener, IDisposable
    {
        private IDisposable _logScope;
        public void TestStarted(ITest test)
        {
            RequireEditorThread();
            if (test.IsSuite) return;
            Type type = typeof(UnityEngine.TestTools.LogAssert).Assembly.GetType("UnityEngine.TestTools.Logging.LogScope", true);
            _logScope = (IDisposable)Activator.CreateInstance(type, true);
        }
        public void TestOutput(TestOutput output) { }
        public void TestFinished(ITestResult result)
        {
            RequireEditorThread();
            if (result.Test.IsSuite) return;
            try
            {
                if (_logScope != null) _logScope.GetType().GetMethod("EvaluateLogScope", BindingFlags.Instance | BindingFlags.Public)
                    .Invoke(_logScope, new object[] { true });
            }
            catch (Exception error) { MarkFailure(result, "Unexpected or missing Unity diagnostic: " + Unwrap(error)); }
            finally { Dispose(); }
            try
            {
                if (!Same(_report.before, ReadWorkspace()))
                    throw new InvalidOperationException("Workspace changed after " + result.FullName + "; subsequent cases are cancelled without rollback.");
            }
            catch (Exception error)
            {
                _abort = true;
                MarkFailure(result, Unwrap(error).ToString());
                _report.error = (_report.error ?? "") + "\n" + Unwrap(error);
            }
            Cases.Add(new CaseResult { id = result.Test.Id, fullName = result.FullName, status = result.ResultState.Status.ToString(),
                message = result.Message, stackTrace = result.StackTrace, seconds = result.Duration });
        }
        public void Dispose() { _logScope?.Dispose(); _logScope = null; }
    }

    private sealed class InlineDispatcher : IWorkItemDispatcher
    {
        public void Dispatch(WorkItem work)
        {
            RequireEditorThread();
            if (_abort) { work.Cancel(false); return; }
            work.Execute();
        }
        public void CancelRun(bool force) { RequireEditorThread(); _abort = true; }
    }

    private static Workspace ReadWorkspace()
    {
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var method = typeof(Undo).GetMethod("GetRecords", flags, null, new[] { typeof(List<string>), typeof(int).MakeByRefType() }, null);
        if (method == null) throw new NotSupportedException("Undo history evidence is unavailable; verification cannot claim workspace preservation.");
        var records = new List<string>(); var arguments = new object[] { records, 0 };
        method.Invoke(null, arguments);
        return new Workspace
        {
            setup = EditorApplication.isPlaying
                ? string.Join("\n", scenes.Select(scene => scene.path + "|" + scene.isLoaded + "|" + (scene == SceneManager.GetActiveScene())))
                : string.Join("\n", EditorSceneManager.GetSceneManagerSetup().Select(scene => scene.path + "|" + scene.isLoaded + "|" + scene.isActive)),
            activeScene = SceneManager.GetActiveScene().handle + "|" + SceneManager.GetActiveScene().path,
            dirty = string.Join("\n", scenes.Select(scene => scene.handle + "|" + scene.path + "|" + scene.isDirty)),
            roots = string.Join("\n", scenes.Select(scene => scene.handle + "|" + (scene.isLoaded
                ? string.Join(",", scene.GetRootGameObjects().Select(root => root.GetInstanceID()).OrderBy(id => id)) : "unloaded"))),
            selection = string.Join(",", Selection.instanceIDs) + "|" + Selection.activeInstanceID + "|" + (Selection.activeContext == null ? 0 : Selection.activeContext.GetInstanceID()),
            startScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),
            undo = arguments[1] + "|" + string.Join("\n", records)
        };
    }
    private static bool Same(Workspace first, Workspace second) => JsonUtility.ToJson(first) == JsonUtility.ToJson(second);
    private static void CaptureLog(string message, string stackTrace, LogType type)
        => Logs.Add(new LogRecord { type = type.ToString(), message = message, stackTrace = stackTrace });
    private static void MarkFailure(ITestResult result, string message)
    {
        if (result is TestResult mutable) mutable.SetResult(ResultState.Failure, (result.Message ?? "") + "\n" + message, result.StackTrace);
        else throw new InvalidOperationException(message);
    }
    private static Exception Unwrap(Exception error) => error is TargetInvocationException invocation && invocation.InnerException != null ? invocation.InnerException : error;
    private static IEnumerable<ITest> Flatten(ITest test)
    {
        yield return test;
        foreach (var child in test.Tests ?? new List<ITest>()) foreach (var nested in Flatten(child)) yield return nested;
    }
    private static void RequireEditorThread()
    {
        if (Thread.CurrentThread.ManagedThreadId != _editorThread) throw new InvalidOperationException("Popup-store tests must remain on the Editor main thread.");
    }
    private static void SetExecutionContext(TestExecutionContext context)
    {
        var setter = typeof(TestExecutionContext).GetProperty("CurrentContext", BindingFlags.Public | BindingFlags.Static)?.GetSetMethod(true);
        if (setter == null) throw new NotSupportedException("The installed NUnit context setter is unavailable.");
        setter.Invoke(null, new object[] { context });
    }
}
#endif
