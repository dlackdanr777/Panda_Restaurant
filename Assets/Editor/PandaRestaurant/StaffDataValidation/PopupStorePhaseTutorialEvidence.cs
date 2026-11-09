#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit original-prefab memory render diagnostics; not actual GameView or player approval.</summary>
public static class PopupStorePhaseTutorialEvidence
{
    private const string Folder = "Temp/PopupStoreVerification/PhaseTutorialMemoryEvidence";
    [Serializable] private sealed class Frame
    {
        public string file, state, evidenceKind, phaseResultSession, phaseResultReason, rewardSprite, rewardDisclaimer;
        public int width, height, page, playRequests, closeRequests, cueShows, score, finalizations, rewardDraws;
        public int phaseFinalizations, phaseRewardDraws, phaseRewardDecisions, phaseRewardAmount, rewardOpens;
        public string[] textMetrics;
    }
    [Serializable] private sealed class Report
    {
        public string scope = "Editor memory RenderTexture visual diagnostics at 1920x1080 and 1280x720. Tutorial and cue samples are display-only before approval. Additional modal/summary/reward frames use actual isolated approval, two real C packing/receipts/deliveries/releases, cumulative-score C deadline clear, B timeout and one approved R1 local draw per resolution. Reward values are observed draw results, not a forced example. No account, server or currency write. NOT actual GameView, Stage1 Play, user visual approval, Player or device evidence.";
        public string startedUtc, finishedUtc, error;
        public bool success, workspaceUnchanged;
        public Frame[] frames;
    }
    [Serializable] private sealed class GameplayReport
    {
        public string scope = "Current original-prefab gameplay rendered in isolated EditMode memory. Uses real V1 phase approval, orders, counter selection, packing receipt and delivery movement. No tutorial overlay, image compositing, forced score, account or server write. This is not actual Stage1 GameView, user approval, Player or device evidence.";
        public string startedUtc, finishedUtc, file, receiptId, orderId, customerId, businessSessionId, phaseRunId, error;
        public int width, height, slotIndex, score;
        public bool success, workspaceUnchanged;
    }

    /// <summary>Explicitly refresh only the new tutorial-source PNG from actual current gameplay objects.</summary>
    public static void CaptureCurrentGameplay()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorUtility.scriptCompilationFailed || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
            throw new InvalidOperationException("Tutorial source capture requires settled EditMode and an uninitialized live SDK.");
        Directory.CreateDirectory(Folder);
        var report = new GameplayReport { startedUtc = DateTime.UtcNow.ToString("O"),
            file = Path.GetFullPath(PopupStorePhasePreviewViewBuilder.GameplayPath), width = 1920, height = 1080 };
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        var random = UnityEngine.Random.state;
        PopupStorePhasePreviewNavigationTests.Harness host = null;
        StaffGachaOfflineFonts fonts = null;
        GameObject cameraObject = null;
        RenderTexture target = null;
        var previousTarget = RenderTexture.active;
        try
        {
            CreateScoreSnapshots(out var profile, out var scoring);
            int plushChoice = 0;
            host = new PopupStorePhasePreviewNavigationTests.Harness(false, false, profile, scoring,
                max => max == 100 ? 99 : max == 5 ? (plushChoice++ % 5) : 0);
            var root = host.Root;
            root.SetActive(false);
            fonts = new StaffGachaOfflineFonts(); fonts.BindBeforeActivation(root);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.font == null || EditorUtility.IsPersistent(text.font))
                    throw new InvalidOperationException("The tutorial source renderer found a persistent font reference.");
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            root.GetComponent<CanvasScaler>().enabled = false;
            var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(1920, 1080);
            rect.SetPositionAndRotation(Vector3.zero, Quaternion.identity); rect.localScale = Vector3.one;
            cameraObject = new GameObject("phase-current-gameplay-source-camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.orthographic = true; camera.orthographicSize = 540;
            camera.nearClipPlane = .3f; camera.farClipPlane = 2000f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .12f, .12f, 1);
            camera.cullingMask = 1 << 31; camera.allowHDR = false; camera.allowMSAA = false;
            target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.HideAndDontSave };
            target.Create(); camera.targetTexture = target;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            { canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera; }
            root.SetActive(true);
            if (!host.Controller.RequestStart()) throw new InvalidOperationException("The isolated source approval failed.");
            PopupStorePhasePreviewIntegrationTests.CompleteInitialPresentation(host.Preview, host.Clock);
            host.Ready();
            foreach (var entry in host.Entries(1).Reverse())
                if (!host.Counter.TryAdd(entry)) throw new InvalidOperationException("A real source material could not be selected.");
            var packed = host.Packing.TryPack();
            if (!packed.Success || packed.Receipt == null || packed.Receipt.SlotIndex != 1)
                throw new InvalidOperationException("The distinct middle order did not produce its own real packing receipt.");
            host.Presentation.TickPresentation(0);
            for (int i = 0; i < 100 && host.Presentation.CurrentPhase != PopupStorePackingPresentation.Phase.Move; i++)
                host.Presentation.TickPresentation(.02f);
            if (host.Presentation.CurrentPhase != PopupStorePackingPresentation.Phase.Move)
                throw new InvalidOperationException("The real receipt did not reach the shipping movement.");
            host.Presentation.TickPresentation(.12f);
            if (!host.Entrance.IsWaiting(0) || !host.Entrance.IsWaiting(2)
                || host.Preview.Session.State != PopupStorePhaseState.Playing
                || host.Root.GetComponent<PopupStoreStartPanel>().IsVisible || host.Panel.IsVisible)
                throw new InvalidOperationException("The source frame is not plain gameplay with two waiting orders and one delivery.");
            host.Preview.RenderHud();
            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true, true);
            Canvas.ForceUpdateCanvases(); camera.Render();
            SavePixels(target, PopupStorePhasePreviewViewBuilder.GameplayPath);
            report.receiptId = packed.Receipt.ReceiptId; report.orderId = packed.Receipt.OrderId;
            report.customerId = packed.Receipt.CustomerId; report.slotIndex = packed.Receipt.SlotIndex;
            report.businessSessionId = host.Preview.Session.BusinessSessionId;
            report.phaseRunId = host.Preview.Session.PhaseRunId; report.score = host.Score.TotalScore;
            host.AssertSingleUseAndNoR1(); report.success = true;
        }
        catch (Exception exception) { report.error = exception.ToString(); Debug.LogError("POPUP_STORE_PHASE_GAMEPLAY_SOURCE_FAILED " + report.error); }
        finally
        {
            RenderTexture.active = previousTarget;
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            try { host?.Dispose(); } finally { fonts?.Dispose(); }
            UnityEngine.Random.state = random;
            try { witness.AssertUnchanged("phase current gameplay tutorial source"); report.workspaceUnchanged = true; }
            catch (Exception exception) { report.success = false; report.error = (report.error ?? string.Empty) + "\n" + exception; }
            report.finishedUtc = DateTime.UtcNow.ToString("O");
            File.WriteAllText(Folder + "/gameplay-source-v4.json", JsonUtility.ToJson(report, true));
            Debug.Log("POPUP_STORE_PHASE_GAMEPLAY_SOURCE success=" + report.success + "; workspaceUnchanged=" + report.workspaceUnchanged
                + "; scope=actual-gameplay-memory-render-not-GameView; file=" + report.file);
        }
    }

    private static void CreateScoreSnapshots(out PopupStorePhaseProfileSnapshot profile, out PopupStoreScorePolicySnapshot scoring)
    {
        var catalog = Resources.Load<PopupStoreMaterialCatalog>("UI/PopupStore/PopupStoreMaterials");
        if (!PopupStorePhasePreviewSettings.CreateProposalV1().TryCreateSnapshot(catalog, out profile, out scoring, out var error))
            throw new InvalidOperationException("The current V1 score proposal is invalid: " + error);
    }

    private static void SavePixels(RenderTexture target, string path)
    {
        var previous = RenderTexture.active; Texture2D image = null;
        try
        {
            RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(false, false);
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; if (image != null) Object.DestroyImmediate(image); }
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorUtility.scriptCompilationFailed || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
            throw new InvalidOperationException("Phase tutorial evidence requires settled EditMode and an uninitialized live SDK.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupStorePhasePreviewViewBuilder.PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Build the authored Editor-only preview view first.");
        var rewardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupStorePhaseRewardViewBuilder.PrefabPath);
        if (rewardPrefab == null) throw new InvalidOperationException("Build the authored Editor-only reward view first.");
        Directory.CreateDirectory(Folder);
        var report = new Report { startedUtc = DateTime.UtcNow.ToString("O") };
        var frames = new List<Frame>();
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        var random = UnityEngine.Random.state;
        try
        {
            Render(prefab, rewardPrefab, new Vector2Int(1920, 1080), frames);
            Render(prefab, rewardPrefab, new Vector2Int(1280, 720), frames);
            report.success = true;
        }
        catch (Exception exception)
        {
            report.error = exception.ToString();
            Debug.LogError("POPUP_STORE_PHASE_TUTORIAL_MEMORY_FAILED " + report.error);
        }
        finally
        {
            UnityEngine.Random.state = random;
            try { witness.AssertUnchanged("phase tutorial memory evidence"); report.workspaceUnchanged = true; }
            catch (Exception exception)
            { report.success = false; report.error = (report.error ?? string.Empty) + "\n" + exception; }
            report.finishedUtc = DateTime.UtcNow.ToString("O"); report.frames = frames.ToArray();
            File.WriteAllText(Folder + "/report.json", JsonUtility.ToJson(report, true));
            Debug.Log("POPUP_STORE_PHASE_TUTORIAL_MEMORY success=" + report.success + "; frames=" + frames.Count
                + "; workspaceUnchanged=" + report.workspaceUnchanged + "; scope=memory-render-not-GameView; report=" + Folder + "/report.json");
        }
    }

    private static void Render(GameObject prefab, GameObject rewardPrefab, Vector2Int resolution, List<Frame> frames)
    {
        PopupStorePhasePreviewNavigationTests.Harness host = null;
        StaffGachaOfflineFonts fonts = null;
        GameObject cameraObject = null;
        RenderTexture target = null;
        var previousTarget = RenderTexture.active;
        try
        {
            CreateScoreSnapshots(out var profile, out var scoring);
            host = new PopupStorePhasePreviewNavigationTests.Harness(false, false, profile, scoring);
            var root = host.Root;
            // The synchronous host has not rendered a frame; every font is cloned before activation/render.
            root.SetActive(false);
            var viewObject = Object.Instantiate(prefab, root.transform, false);
            var view = viewObject.GetComponent<PopupStorePhasePreviewView>();
            var reward = Object.Instantiate(rewardPrefab, root.transform, false).GetComponent<PopupStorePhaseRewardPresentation>();
            fonts = new StaffGachaOfflineFonts(); fonts.BindBeforeActivation(root);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.font == null || EditorUtility.IsPersistent(text.font))
                    throw new InvalidOperationException("The evidence renderer found a persistent font reference.");
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            root.GetComponent<CanvasScaler>().enabled = false;
            var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(1920, 1080);
            rect.SetPositionAndRotation(Vector3.zero, Quaternion.identity); rect.localScale = Vector3.one;
            cameraObject = new GameObject("phase-tutorial-memory-camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.orthographic = true; camera.orthographicSize = 540;
            camera.nearClipPlane = .3f; camera.farClipPlane = 2000f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .12f, .12f, 1);
            camera.cullingMask = 1 << 31; camera.allowHDR = false; camera.allowMSAA = false;
            target = new RenderTexture(resolution.x, resolution.y, 24, RenderTextureFormat.ARGB32)
                { hideFlags = HideFlags.HideAndDontSave };
            target.Create(); camera.targetTexture = target;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            { canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera; }
            root.SetActive(true);
            var close = (Button)typeof(UIPopupStore).GetField("_closeButton", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(root.GetComponent<UIPopupStore>());
            if (!host.Preview.AttachView(view, close)) throw new InvalidOperationException("Could not bind the authored phase tutorial.");
            BindPolish(host, view);
            if (!host.Preview.AttachRewardView(reward,
                Resources.Load<PopupStoreRewardTable>("UI/PopupStore/PopupStoreRewardTable_R1")))
                throw new InvalidOperationException("Could not bind the authored reward and approved R1 snapshot.");
            view.RenderTutorial(true, true, "이용권 1/1");
            for (int page = 0; page < view.PageCount; page++)
            {
                if (page > 0 && !view.TryNext()) throw new InvalidOperationException("Tutorial navigation rejected page " + page);
                Capture(host, view, camera, target, "tutorial-" + (page + 1), frames);
            }
            host.Preview.RenderStart(false, false, 1, 1);
            foreach (string cue in new[] { "Stage", "3", "2", "1", "GameStart", "Success", "GameOver" })
            {
                view.RenderCue(cue, cue == "Stage" ? 2 : 1, .5f);
                Capture(host, view, camera, target, "cue-" + cue, frames);
            }
            if (view.PlayRequestCount != 0 || view.CloseRequestCount != 0 || host.Score.TotalScore != 0
                || root.GetComponent<PopupStoreGradeSession>().FinalizationCount != 0
                || root.GetComponent<PopupStoreRewardSession>().RandomDrawCount != 0
                || host.Preview.RewardRoll.RandomDrawCount != 0
                || host.Clock.State != PopupStoreBusinessState.NotStarted)
                throw new InvalidOperationException("A display-only evidence run changed business state.");

            // The following frames are an explicit isolated memory scenario, not forced visual values.
            host.Preview.RenderStart(true, true, 1, 1);
            if (!view.TryPlay()) throw new InvalidOperationException("Tutorial play refused the isolated evidence approval.");
            PopupStorePhasePreviewIntegrationTests.CompleteInitialPresentation(host.Preview, host.Clock);
            host.Ready();
            if (!host.View.TryRequestExit() || !host.Exit.IsVisible)
                throw new InvalidOperationException("The actual exit modal could not be shown for evidence.");
            Capture(host, view, camera, target, "exit-modal", frames);
            if (!host.Exit.TryContinue()) throw new InvalidOperationException("The actual exit modal could not resume.");
            host.Pack(); host.Deliver(); host.Entrance.TickEntrance(2);
            host.Pack(); host.Deliver(); host.Entrance.TickEntrance(2);
            if (!host.Clock.AdvanceTime(host.Clock.RemainingSeconds - 10))
                throw new InvalidOperationException("Could not advance the actual C clock to its warning boundary.");
            Capture(host, view, camera, target, "time-warning-10", frames);
            host.Clock.AdvanceTime(host.Clock.RemainingSeconds);
            if (!host.Preview.TickCleanup()) throw new InvalidOperationException("The actual completed C receipt did not finish cleanup.");
            host.NextPhase();
            host.Clock.AdvanceTime(host.Clock.RemainingSeconds);
            if (!host.Preview.TickCleanup()) throw new InvalidOperationException("The actual B timeout did not finish cleanup.");
            host.ShowResult();
            if (host.Preview.SecuredGrade != PopupStoreGradeTier.C || host.Preview.Session.FinalizationCount != 1)
                throw new InvalidOperationException("The observed final result must be the single C-clear/B-timeout result.");
            Capture(host, view, camera, target, "simple-summary", frames);
            if (!host.Panel.TryConfirm()) throw new InvalidOperationException("The actual receive button rejected the local R1 reward.");
            host.Panel.TickResult(reward.RiseDuration * .45f);
            Capture(host, view, camera, target, "reward-rising", frames);
            host.Panel.TickResult(reward.RiseDuration);
            Capture(host, view, camera, target, "reward-awaiting-tap", frames);
            if (!reward.TryOpen()) throw new InvalidOperationException("The actual local reward box rejected opening.");
            host.Panel.TickResult(.12f);
            Capture(host, view, camera, target, "reward-opening", frames);
            host.Panel.TickResult(reward.OpeningDuration);
            Capture(host, view, camera, target, "reward-revealed", frames);
            if (host.Preview.RewardRoll.RandomDrawCount != 1 || host.Preview.RewardRoll.DecisionCount != 1
                || reward.BeginCount != 1 || reward.OpenCount != 1
                || reward.State != PopupStorePhaseRewardVisualState.Revealed
                || host.Preview.Session.FinalizationCount != 1)
                throw new InvalidOperationException("Reward evidence did not retain one decision, appearance and opening.");
            host.AssertSingleUseAndNoR1(); // Ordinary GradeSession/RewardSession remain untouched.
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            try { host?.Dispose(); }
            finally { fonts?.Dispose(); }
        }
    }

    private static void BindPolish(PopupStorePhasePreviewNavigationTests.Harness host, PopupStorePhasePreviewView tutorial)
    {
        var polish = tutorial.GetComponent<PopupStorePhasePreviewPolish>();
        var exit = new SerializedObject(host.Exit);
        var clock = new SerializedObject(host.Clock);
        clock.FindProperty("_notStartedText").stringValue = "—";
        clock.ApplyModifiedPropertiesWithoutUndo();
        // EditMode has no automatic Update between style binding and the first tutorial frame.
        host.Clock.RefreshPhasePreview();
        var overlay = exit.FindProperty("_overlay").objectReferenceValue as GameObject;
        var panel = overlay != null ? overlay.transform.Find("Exit Confirmation Panel") as RectTransform : null;
        var texts = panel != null ? panel.GetComponentsInChildren<TMP_Text>(true) : Array.Empty<TMP_Text>();
        if (polish == null || !polish.Bind(host.Preview,
            clock.FindProperty("_timeText").objectReferenceValue as TMP_Text,
            texts.SingleOrDefault(x => x.name == "Exit Confirmation Title"),
            texts.SingleOrDefault(x => x.name == "Exit Confirmation Explanation"),
            exit.FindProperty("_continueButton").objectReferenceValue as Button,
            exit.FindProperty("_abandonButton").objectReferenceValue as Button, panel)
            || !host.Preview.AttachPolish(polish))
            throw new InvalidOperationException("Could not bind the popup-local authored modal and warning styles.");
    }

    private static void Capture(PopupStorePhasePreviewNavigationTests.Harness host, PopupStorePhasePreviewView view,
        Camera camera, RenderTexture target, string state, List<Frame> frames)
    {
        if (!state.StartsWith("tutorial-", StringComparison.Ordinal) && !state.StartsWith("cue-", StringComparison.Ordinal))
            host.Preview.RenderHud(); // Explicit real LateUpdate equivalent for this EditMode memory renderer.
        Canvas.ForceUpdateCanvases();
        foreach (var text in host.Root.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true, true);
        Canvas.ForceUpdateCanvases(); camera.Render();
        string file = Folder + "/" + target.width + "x" + target.height + "-" + state + ".png";
        var previous = RenderTexture.active; Texture2D image = null;
        try
        {
            RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(false, false);
            File.WriteAllBytes(file, image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; if (image != null) Object.DestroyImmediate(image); }
        frames.Add(new Frame
        {
            file = Path.GetFullPath(file), state = state, width = target.width, height = target.height,
            page = view.CurrentPage + 1, playRequests = view.PlayRequestCount, closeRequests = view.CloseRequestCount,
            cueShows = view.CueShowCount, score = host.Score.TotalScore,
            finalizations = host.Root.GetComponent<PopupStoreGradeSession>().FinalizationCount,
            rewardDraws = host.Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount,
            phaseFinalizations = host.Preview.Session.FinalizationCount,
            phaseResultSession = host.Preview.Session.FinalResult?.BusinessSessionId,
            phaseResultReason = host.Preview.Session.FinalResult?.EndReason.ToString(),
            phaseRewardDraws = host.Preview.RewardRoll?.RandomDrawCount ?? 0,
            phaseRewardDecisions = host.Preview.RewardRoll?.DecisionCount ?? 0,
            phaseRewardAmount = host.Preview.RewardRoll?.Decision?.Diamonds ?? 0,
            rewardSprite = host.Preview.RewardPresentation?.DisplayedSprite?.name,
            rewardOpens = host.Preview.RewardPresentation?.OpenCount ?? 0,
            rewardDisclaimer = host.Preview.RewardPresentation?.BeginCount > 0 ? "R1 검수용 · 미지급" : string.Empty,
            evidenceKind = state.StartsWith("tutorial-", StringComparison.Ordinal) || state.StartsWith("cue-", StringComparison.Ordinal)
                ? "display-only-memory-render" : "actual-isolated-memory-scenario-not-Stage1-Play",
            textMetrics = host.Root.GetComponentsInChildren<TMP_Text>(false).Select(TextMetric).ToArray()
        });
    }

    private static string TextMetric(TMP_Text text)
    {
        var rect = text.rectTransform.rect;
        return string.Format(CultureInfo.InvariantCulture,
            "{0}; text={1}; preferred={2:0.##}x{3:0.##}; rect={4:0.##}x{5:0.##}; fontSize={6:0.##}; overflowing={7}",
            text.name, text.text.Replace("\n", "\\n"), text.preferredWidth, text.preferredHeight,
            rect.width, rect.height, text.fontSize, text.isTextOverflowing);
    }
}
#endif
