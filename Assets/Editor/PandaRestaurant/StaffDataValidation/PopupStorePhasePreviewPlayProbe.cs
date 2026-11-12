#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Muks.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit bounded automation only. The designer's preview menu never starts this probe.</summary>
public static partial class PopupStoreVerificationRunner
{
    private const string PhaseProbeTokenKey = "Panda.PopupStore.PhasePreview.Probe.Token";
    private const string PhaseProbeReportKey = "Panda.PopupStore.PhasePreview.Probe.Report";
    private const string PhaseProbeResultPath = DirectoryPath + "/phase-preview-play-probe.json";
    private static PhaseProbeReport _phaseProbeReport;
    private static PhaseProbeContext _phaseProbe;

    [Serializable] private sealed class PhaseProbeReport
    {
        public string token, startedUtc, stoppedUtc, capturedUtc, error, lastStep;
        public string businessSessionId, approvalId, cPhaseId, bPhaseId;
        public string receiptId, orderId, customerId;
        public int ownerId, popupId, slotIndex, observedFrames, score, receipts, acceptedReceipts;
        public int localStarts, bindings, remainingUses, cleanupCount, transitionCount;
        public int ordinaryFinalizations, rewardDecisions, rewardDraws, complaintPenaltyCount;
        public double cInitialSeconds, cObservedSeconds, cAfterGoalSeconds, bObservedSeconds, clockElapsed, complaintPenaltySeconds;
        public bool startingAccepted, stopping, completed, success, workspaceUnchanged;
        public bool cConfiguration, liveClockAdvanced, clearingObserved, noticeObserved;
        public bool countdown3, countdown2, countdown1, delivered, visualCompleted, released;
        public bool tutorialObserved, initialStage, initial3, initial2, initial1, initialGameStart;
        public bool initialInputBlocked, goalReachedStillPlaying, nextStage, nextGameStart, soldReceiptPenaltyAbsent;
        public bool bStarted, noOrdinaryRewards, playingAfter, sdkInitializedAfter, loggedInAfter;
        public List<string> evidence = new List<string>();
        public Workspace before, after;
    }

    private sealed class PhaseProbeContext
    {
        internal int step;
        internal CustomerPartyOwnedMemory memory;
        internal CameraController camera;
        internal UIMainCanvas main;
        internal Muks.MobileUI.MobileUINavigation nav;
        internal UIPopupStore popup;
        internal PopupStorePackingReceipt receipt;
    }

    private static bool LoadPhaseProbe()
    {
        string token = SessionState.GetString(PhaseProbeTokenKey, "");
        if (token.Length == 0) return false;
        if (_phaseProbeReport == null)
        {
            string saved = SessionState.GetString(PhaseProbeReportKey, "");
            if (saved.Length != 0) _phaseProbeReport = JsonUtility.FromJson<PhaseProbeReport>(saved);
        }
        return _phaseProbeReport != null && _phaseProbeReport.token == token;
    }

    private static void SavePhaseProbe()
    {
        if (_phaseProbeReport != null && SessionState.GetString(PhaseProbeTokenKey, "") == _phaseProbeReport.token)
            SessionState.SetString(PhaseProbeReportKey, JsonUtility.ToJson(_phaseProbeReport));
    }

    // Wired only to the explicit request "phase-preview-probe", not to either user-facing test menu.
    private static void StartPhasePreviewPlayProbe()
    {
        if (SessionState.GetString(PhaseProbeTokenKey, "").Length != 0)
        { Debug.LogWarning("POPUP_STORE_PHASE_PROBE_REFUSED another probe record is active"); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || EditorApplication.isUpdating || EditorUtility.scriptCompilationFailed
            || PrefabStageUtility.GetCurrentPrefabStage() != null
            || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
        { Debug.LogWarning("POPUP_STORE_PHASE_PROBE_REFUSED requires clean stopped original Editor"); return; }
        _phaseProbe = null;
        _phaseProbeReport = new PhaseProbeReport {
            token = Guid.NewGuid().ToString("N"), startedUtc = DateTime.UtcNow.ToString("O"),
            lastStep = "request-original-memory-preview", before = ReadWorkspace()
        };
        SessionState.SetString(PhaseProbeTokenKey, _phaseProbeReport.token);
        SavePhaseProbe();
        try
        {
            _phaseProbeReport.startingAccepted = TryStartPhasePreview(out string reason);
            SavePhaseProbe();
            if (!_phaseProbeReport.startingAccepted) FinishPhasePreviewPlayProbe(reason ?? "Preview start was refused.");
        }
        catch (Exception exception) { FinishPhasePreviewPlayProbe(Unwrap(exception).Message); }
    }

    private static void CapturePhasePreviewPlayProbeLog(string message, string stack, LogType type)
    {
        if (!LoadPhaseProbe() || _phaseProbeReport.stopping) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            _phaseProbeReport.error = (_phaseProbeReport.error ?? "") + "\n" + message + "\n" + stack;
            SavePhaseProbe();
        }
        if (message.StartsWith("POPUP_STORE_PHASE_PREVIEW", StringComparison.Ordinal)
            || message.StartsWith("POPUP_STORE_CUSTOMER_RELEASED", StringComparison.Ordinal)
            || message.StartsWith("POPUP_STORE_PACK", StringComparison.Ordinal))
        {
            // Keep bounded exact transaction evidence; warnings do not become a false success.
            if (_phaseProbeReport.evidence.Count < 120) _phaseProbeReport.evidence.Add(message);
            if (!string.IsNullOrEmpty(_phaseProbeReport.receiptId)
                && message.StartsWith("POPUP_STORE_CUSTOMER_RELEASED receipt=" + _phaseProbeReport.receiptId
                    + "; slot=" + _phaseProbeReport.slotIndex + "; customer=" + _phaseProbeReport.customerId
                    + "; order=" + _phaseProbeReport.orderId + "; reason=Packed", StringComparison.Ordinal))
                _phaseProbeReport.released = true;
            if (!string.IsNullOrEmpty(_phaseProbeReport.cPhaseId)
                && message.StartsWith("POPUP_STORE_PHASE_PREVIEW session=" + _phaseProbeReport.businessSessionId
                    + "; phase=" + _phaseProbeReport.cPhaseId + "; STATE state=Clearing;", StringComparison.Ordinal))
                _phaseProbeReport.clearingObserved = true;
            SavePhaseProbe();
        }
    }

    private static void TickPhasePreviewPlayProbe()
    {
        if (!LoadPhaseProbe()) return;
        var report = _phaseProbeReport;
        double wallSeconds = (DateTime.UtcNow - DateTime.Parse(report.startedUtc).ToUniversalTime()).TotalSeconds;
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode && wallSeconds < 60) return;
            if (!report.stopping)
            {
                if (wallSeconds < 10) return;
                FinishPhasePreviewPlayProbe("Original Play did not remain active for the probe.");
            }
            if (!string.IsNullOrEmpty(report.stoppedUtc)
                && (DateTime.UtcNow - DateTime.Parse(report.stoppedUtc).ToUniversalTime()).TotalSeconds < 2) return;
            CompletePhasePreviewProbeReport();
            return;
        }
        if (report.stopping) return;
        try
        {
            // Capture the exact memory object before acting on a startup error, so only our Play may be stopped.
            if (HasOriginalMemoryOwner())
            {
                var memory = typeof(CustomerPartyStage1Preview).GetField("memory", BindingFlags.Static | BindingFlags.NonPublic)
                    ?.GetValue(null) as CustomerPartyOwnedMemory;
                int ownerId = Muks.BackEnd.BackendManager.ExistingInstance.GetInstanceID();
                if (_phaseProbe == null) _phaseProbe = new PhaseProbeContext { memory = memory };
                if (report.ownerId == 0) { report.ownerId = ownerId; SavePhaseProbe(); }
                RequireRewardReview(report.ownerId == ownerId && ReferenceEquals(_phaseProbe.memory, memory),
                    "The exact original memory owner changed; no replacement session is permitted.");
            }
            RequireRewardReview(wallSeconds < 60, "Phase preview probe timed out within 60 seconds; focus/visible PlayerLoop may be unavailable.");
            if (!string.IsNullOrEmpty(report.error)) throw new InvalidOperationException(report.error);
            string setupError = SessionState.GetString(PhasePreviewErrorKey, "");
            RequireRewardReview(setupError.Length == 0, setupError);
            if (!HasOriginalMemoryOwner() || _phaseProbe == null) return;
            if (!(bool)typeof(CustomerPartyStage1Preview).GetField("ready", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)) return;
            report.observedFrames++;
            var c = _phaseProbe;
            if (c.step == 0)
            {
                report.lastStep = "camera-to-floor3";
                c.camera = OriginalStageComponent<CameraController>(); c.main = OriginalStageComponent<UIMainCanvas>();
                c.nav = c.main.GetComponent<Muks.MobileUI.MobileUINavigation>();
                c.camera.MoveCamera(ERestaurantFloorType.Floor3, RestaurantType.Hall); c.step++; SavePhaseProbe(); return;
            }
            if (c.step == 1)
            {
                if (c.camera.IsCameraMoving) return;
                report.lastStep = "show-original-floor3-map"; c.main.OnShowFloor3UI(); c.step++; SavePhaseProbe(); return;
            }
            if (c.step == 2)
            {
                var floor = c.nav.FirstView as UIFloor3;
                if (floor == null || floor.VisibleState != VisibleState.Appeared || !c.nav.ViewsVisibleStateCheck()) return;
                report.lastStep = "invoke-original-popup-map-button";
                ((UnityEngine.UI.Button)PauseField(floor, "_popupStoreButton")).onClick.Invoke(); c.step++; SavePhaseProbe(); return;
            }
            if (c.popup == null) c.popup = c.nav.FirstView as UIPopupStore;
            if (c.popup == null || c.popup.VisibleState != VisibleState.Appeared) return;
            var preview = c.popup.GetComponent<PopupStorePhasePreview>();
            if (preview == null || !preview.IsInitialized || SessionState.GetString(PhasePreviewStateKey, "") != "ready") return;
            RequireRewardReview(ReferenceEquals(preview, _phasePreviewView) && preview.LastError == null, preview.LastError ?? "Preview ownership differs.");
            var clock = c.popup.GetComponent<PopupStoreBusinessClock>();
            var start = c.popup.GetComponent<PopupStoreStartController>();
            var entrance = c.popup.GetComponent<PopupStoreCustomerEntrance>();
            var packing = c.popup.GetComponent<PopupStorePacking>();
            var visual = c.popup.GetComponent<PopupStorePackingPresentation>();
            var counter = c.popup.GetComponentInChildren<PopupStoreSalesCounter>(true);
            var grade = c.popup.GetComponent<PopupStoreGradeSession>();
            var reward = c.popup.GetComponent<PopupStoreRewardSession>();
            var model = preview.Session;
            RequireRewardReview(grade.FinalResult == null && grade.FinalizationCount == 0
                && reward.Result == null && reward.DecisionCount == 0 && reward.RandomDrawCount == 0,
                "Ordinary finalization or R1 reward processing occurred in the phase probe.");
            if (c.step == 3)
            {
                report.lastStep = "actual-tutorial-pages-and-isolated-start-approval"; report.popupId = c.popup.GetInstanceID();
                RequireRewardReview(clock.State == PopupStoreBusinessState.NotStarted && start.Host != null
                    && start.Host.IsEditorReview && start.Host.Access?.Remaining == 1,
                    "The phase probe requires a fresh isolated 1/1 admission.");
                RequireRewardReview(model.CurrentPhase.Grade == PopupStoreGradeTier.C
                    && model.CurrentPhase.TimeLimitSeconds == 22 && model.CurrentPhase.TargetSuccessfulOrders == 1
                    && c.popup.GetComponentsInChildren<PopupStoreMaterialList>(true)
                        .Single(list => (PopupStoreMaterialKind)PauseField(list, "_kind") == PopupStoreMaterialKind.Plush).MaterialCount == 5
                    && (double)PauseField(clock, "_initialSeconds") == 90d,
                    "Proposal C must be 22 seconds / 1 success / 5 plushes without changing the authored 90 seconds.");
                report.cConfiguration = true; report.cInitialSeconds = model.CurrentPhase.TimeLimitSeconds;
                var tutorial = preview.TutorialView;
                RequireRewardReview(tutorial != null && tutorial.IsTutorialVisible
                    && tutorial.PageCount >= 3 && tutorial.PageCount <= 4 && tutorial.CurrentPage == 0,
                    "The authored tutorial was not visible before the isolated approval.");
                RequireRewardReview(!preview.CanPlay && packing.CommittedReceipts.Count == 0
                    && preview.AcceptedReceiptCount == 0 && clock.ElapsedSeconds == 0,
                    "Gameplay or a transaction started before the tutorial play action.");
                for (int page = 1; page < tutorial.PageCount; page++)
                    RequireRewardReview(tutorial.TryNext() && tutorial.CurrentPage == page,
                        "The actual tutorial next-page action was rejected.");
                report.tutorialObserved = true;
                RequireRewardReview(tutorial.TryPlay(), "The tutorial play action was rejected.");
                c.step++; SavePhaseProbe(); return;
            }
            if (c.step == 4)
            {
                report.lastStep = "observe-initial-cues-then-real-clock-and-waiting-order";
                report.initialStage |= preview.PresentationCue == "Stage";
                report.initial3 |= preview.PresentationCue == "3";
                report.initial2 |= preview.PresentationCue == "2";
                report.initial1 |= preview.PresentationCue == "1";
                report.initialGameStart |= preview.PresentationCue == "GameStart";
                if (!preview.CanPlay)
                {
                    RequireRewardReview(!clock.CanCommitPacking && packing.CommittedReceipts.Count == 0
                        && preview.AcceptedReceiptCount == 0 && clock.ElapsedSeconds == 0
                        && !Enumerable.Range(0, 3).Any(entrance.IsWaiting),
                        "Initial presentation permitted packing, customers or elapsed play time before GAME START finished.");
                    report.initialInputBlocked = true; SavePhaseProbe(); return;
                }
                RequireRewardReview(clock.State == PopupStoreBusinessState.Running && report.initialStage
                    && report.initial3 && report.initial2 && report.initial1 && report.initialGameStart,
                    "The initial STAGE / 3-2-1 / GAME START sequence was not completely observed.");
                RequireRewardReview(start.IsApproved && start.Host.LocalStartCount == 1 && start.Host.BindingCount == 1
                    && start.Host.Access.Remaining == 0, "Approval was not consumed exactly once in the isolated host.");
                report.businessSessionId = model.BusinessSessionId; report.approvalId = start.Host.BusinessSessionId;
                report.cPhaseId = model.PhaseRunId; report.cObservedSeconds = model.RemainingSeconds;
                report.clockElapsed = clock.ElapsedSeconds;
                report.liveClockAdvanced = clock.ElapsedSeconds > 0 && model.RemainingSeconds < 22 && model.RemainingSeconds > 0;
                if (!report.liveClockAdvanced) return;
                int slot = Enumerable.Range(0, 3).Where(entrance.IsWaiting).DefaultIfEmpty(-1).First();
                if (slot < 0) return;
                var orders = c.popup.GetComponent<PopupStoreOrderDisplay>();
                RequireRewardReview(orders.TryGetEntryBinding(slot, out var order, out _), "Waiting order is not bound.");
                RequireRewardReview(order.TryResolve((PopupStoreMaterialCatalog)PauseField(orders, "_catalog"),
                    out var materials, out string materialError), materialError);
                // Actual catalog entries and the existing unordered counter/packing path. No score or receipt injection.
                foreach (var material in materials.Reverse()) RequireRewardReview(counter.TryAdd(material), "Real order material was rejected.");
                var packed = packing.TryPack();
                RequireRewardReview(packed.Success && packed.Receipt != null && packing.IsCommittedReceipt(packed.Receipt),
                    "The actual phase C order could not be committed.");
                c.receipt = packed.Receipt;
                report.receiptId = c.receipt.ReceiptId; report.orderId = c.receipt.OrderId;
                report.customerId = c.receipt.CustomerId; report.slotIndex = c.receipt.SlotIndex;
                report.lastStep = "goal-reached-wait-until-real-stage-deadline";
                RequireRewardReview(preview.AcceptedReceiptCount == 1 && model.State == PopupStorePhaseState.Playing
                    && model.RemainingSeconds > 0 && model.HighestClearedGrade == PopupStoreGradeTier.None && preview.CanPlay,
                    "Reaching the target ended C before its deadline or prematurely secured a grade.");
                report.goalReachedStillPlaying = true; report.cAfterGoalSeconds = model.RemainingSeconds;
                c.step++; SavePhaseProbe(); return;
            }
            if (c.step == 5)
            {
                report.noticeObserved |= model.State == PopupStorePhaseState.ClearNotice;
                report.countdown3 |= model.State == PopupStorePhaseState.Countdown && model.CountdownValue == 3;
                report.countdown2 |= model.State == PopupStorePhaseState.Countdown && model.CountdownValue == 2;
                report.countdown1 |= model.State == PopupStorePhaseState.Countdown && model.CountdownValue == 1;
                report.nextStage |= preview.PresentationCue == "Stage";
                report.nextGameStart |= preview.PresentationCue == "GameStart";
                report.delivered |= ReferenceEquals(visual.LastDeliveredReceipt, c.receipt) && visual.IsDeliveryCompleted(c.receipt.ReceiptId);
                report.visualCompleted |= visual.VisualOutcomes.Any(outcome => ReferenceEquals(outcome.Receipt, c.receipt)
                    && outcome.Kind == PopupStoreVisualOutcomeKind.VisualCompleted);
                RequireRewardReview(model.BusinessSessionId == report.businessSessionId && start.Host.BusinessSessionId == report.approvalId,
                    "Advancing a phase replaced the business or approval identity.");
                if (model.CurrentPhase.Grade == PopupStoreGradeTier.C && model.RemainingSeconds > 0)
                    RequireRewardReview(model.State == PopupStorePhaseState.Playing && model.HighestClearedGrade == PopupStoreGradeTier.None,
                        "A fulfilled target ended the stage before the real deadline.");
                if (model.CurrentPhase.Grade != PopupStoreGradeTier.B || !preview.CanPlay)
                { SavePhaseProbe(); return; }
                report.bStarted = true; report.bPhaseId = model.PhaseRunId; report.bObservedSeconds = model.RemainingSeconds;
                report.score = c.popup.GetComponent<PopupStoreScore>().TotalScore; report.receipts = packing.CommittedReceipts.Count;
                report.acceptedReceipts = preview.AcceptedReceiptCount; report.cleanupCount = preview.CleanupCount;
                report.transitionCount = model.TransitionCount; report.localStarts = start.Host.LocalStartCount;
                report.bindings = start.Host.BindingCount; report.remainingUses = start.Host.Access.Remaining;
                report.ordinaryFinalizations = grade.FinalizationCount; report.rewardDecisions = reward.DecisionCount;
                report.rewardDraws = reward.RandomDrawCount;
                report.complaintPenaltyCount = clock.ComplaintPenaltyCount;
                report.complaintPenaltySeconds = clock.ComplaintPenaltySeconds;
                // The deadline policy keeps unsold customers waiting after a sale. Their ordinary
                // expiration penalties remain valid; only the committed receipt's order must be exempt.
                var penalized = PauseField(clock, "_penalizedOrders") as HashSet<string>;
                report.soldReceiptPenaltyAbsent = penalized != null && !penalized.Contains(report.orderId);
                report.noOrdinaryRewards = grade.FinalResult == null && grade.FinalizationCount == 0
                    && reward.Result == null && reward.DecisionCount == 0 && reward.RandomDrawCount == 0;
                RequireRewardReview(report.delivered && report.visualCompleted && report.released && report.noticeObserved
                    && report.clearingObserved && report.countdown3 && report.countdown2 && report.countdown1
                    && report.nextStage && report.nextGameStart && report.goalReachedStillPlaying,
                    "The actual deadline/delivery/release or SUCCESS / next STAGE / 3-2-1 / GAME START sequence was not fully observed.");
                RequireRewardReview(report.cPhaseId != report.bPhaseId && model.HighestClearedGrade == PopupStoreGradeTier.C
                    && model.PhaseSuccessfulOrders == 0 && model.CurrentPhase.TimeLimitSeconds == 27
                    && report.bObservedSeconds > 25 && report.bObservedSeconds <= 27
                    && report.score == 100 && report.receipts == 1 && report.acceptedReceipts == 1
                    && counter.Count == 0 && report.cleanupCount == 1 && report.transitionCount == 5 && model.PhaseGeneration == 2
                    && report.localStarts == 1 && report.bindings == 1 && report.remainingUses == 0
                    && report.soldReceiptPenaltyAbsent && report.noOrdinaryRewards,
                    "Phase B did not preserve a single transaction, approval and fresh phase state.");
                report.lastStep = "completed-C-to-B-live-playerloop"; report.completed = true;
                // Stop the explicit automation owner; do not fabricate an abandonment, result or reward.
                FinishPhasePreviewPlayProbe(null);
            }
        }
        catch (Exception exception) { FinishPhasePreviewPlayProbe(Unwrap(exception).Message); }
    }

    private static void FinishPhasePreviewPlayProbe(string error)
    {
        if (!LoadPhaseProbe() || _phaseProbeReport.stopping) return;
        var report = _phaseProbeReport;
        if (!string.IsNullOrEmpty(error) && report.error != error) report.error = (report.error ?? "") + "\n" + error;
        report.stopping = true; report.stoppedUtc = DateTime.UtcNow.ToString("O"); SavePhaseProbe();
        if (!EditorApplication.isPlaying) return;
        var memory = typeof(CustomerPartyStage1Preview).GetField("memory", BindingFlags.Static | BindingFlags.NonPublic)
            ?.GetValue(null) as CustomerPartyOwnedMemory;
        if (HasOriginalMemoryOwner() && _phaseProbe != null && report.ownerId != 0
            && ReferenceEquals(memory, _phaseProbe.memory)
            && Muks.BackEnd.BackendManager.ExistingInstance.GetInstanceID() == report.ownerId
            && SessionState.GetString(PhaseProbeTokenKey, "") == report.token)
            CustomerPartyStage1Preview.StopManual();
        else
        {
            report.error = (report.error ?? "") + "\nExact probe token/memory ownership could not be proven; the active Play was not stopped.";
            CompletePhasePreviewProbeReport();
        }
    }

    private static void CompletePhasePreviewProbeReport()
    {
        if (!LoadPhaseProbe()) return;
        var report = _phaseProbeReport;
        report.after = ReadWorkspace(); report.workspaceUnchanged = Same(report.before, report.after);
        report.playingAfter = EditorApplication.isPlayingOrWillChangePlaymode;
        report.sdkInitializedAfter = BackEnd.Backend.IsInitialized; report.loggedInAfter = BackEnd.Backend.IsLogin;
        report.capturedUtc = DateTime.UtcNow.ToString("O");
        report.success = report.completed && string.IsNullOrEmpty(report.error) && report.workspaceUnchanged
            && !report.playingAfter && !report.sdkInitializedAfter && !report.loggedInAfter;
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(PhaseProbeResultPath, JsonUtility.ToJson(report, true));
        SessionState.EraseString(PhaseProbeTokenKey); SessionState.EraseString(PhaseProbeReportKey); _phaseProbe = null;
        Debug.Log("POPUP_STORE_PHASE_PROBE_FINISHED success=" + report.success + "; step=" + report.lastStep
            + "; evidence=" + PhaseProbeResultPath + "; error=" + report.error);
    }
}
#endif
