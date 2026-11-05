#if UNITY_EDITOR
using System;
using System.Collections;
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

/// <summary>Explicit UI04 memory render diagnostics using the existing original-prefab test host and renderer.</summary>
public static class PopupStoreFinalGuideEvidence
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Folder = "Temp/PopupStoreVerification/UI04MemoryEvidence";

    [Serializable] private sealed class Frame
    {
        public string file, state, grade, hudSprite, clockState;
        public int width, height, score, successfulOrders, counterCount, finalizations, rewardDraws, revealCount;
        public int[] orderMaterialCounts;
        public string[] textMetrics;
        public bool displayOnlyFillPreview;
        public float previewFill = -1;
    }

    [Serializable] private sealed class Report
    {
        public string scope = "UI04 original-prefab memory RenderTexture diagnostics. Existing isolated admission, order, counter, packing, score and result APIs; NOT actual GameView, Stage1 Play, user approval, Player or device evidence. Fill-preview frames change only temporary Image.fillAmount on the isolated memory instance before this diagnostic camera renders.";
        public string startedUtc, finishedUtc, error;
        public bool success, workspaceUnchanged;
        public Frame[] frames;
    }

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorUtility.scriptCompilationFailed || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
            throw new InvalidOperationException("UI04 memory evidence requires settled EditMode with no active live SDK/account.");
        Directory.CreateDirectory(Folder);
        var report = new Report { startedUtc = DateTime.UtcNow.ToString("O") };
        var frames = new List<Frame>();
        var workspace = GachaCollectionPreviewSceneWitness.Capture();
        var random = UnityEngine.Random.state;
        try
        {
            foreach (var resolution in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
                RenderResolution(resolution, frames);
            report.success = true;
        }
        catch (Exception exception)
        {
            report.error = Unwrap(exception).ToString();
            Debug.LogError("POPUP_STORE_UI04_MEMORY_EVIDENCE_FAILED " + report.error);
        }
        finally
        {
            // Any normal preview reward draw belongs only to the temporary diagnostic session.
            UnityEngine.Random.state = random;
            try { workspace.AssertUnchanged("UI04 memory evidence"); report.workspaceUnchanged = true; }
            catch (Exception exception)
            {
                report.success = false;
                report.error = (report.error == null ? "" : report.error + "\n") + exception;
            }
            report.finishedUtc = DateTime.UtcNow.ToString("O");
            report.frames = frames.ToArray();
            File.WriteAllText(Folder + "/UI04-report.json", JsonUtility.ToJson(report, true));
            Debug.Log("POPUP_STORE_UI04_MEMORY_EVIDENCE success=" + report.success + "; frames=" + frames.Count
                + "; workspaceUnchanged=" + report.workspaceUnchanged
                + "; scope=memory-render-not-GameView; report=" + Folder + "/UI04-report.json");
        }
    }

    private static void RenderResolution(Vector2Int resolution, List<Frame> frames)
    {
        object host = null;
        StaffGachaOfflineFonts fonts = null;
        GameObject cameraObject = null;
        RenderTexture target = null;
        var previousTarget = RenderTexture.active;
        try
        {
            var type = typeof(PopupStoreResultTests).GetNestedType("Host", BindingFlags.NonPublic);
            host = Activator.CreateInstance(type, Instance, null, new object[] { null }, CultureInfo.InvariantCulture);
            var root = Read<GameObject>(host, "Root");
            // No frame has rendered from the synchronous host constructor; isolate every font reference first.
            root.SetActive(false);
            fonts = new StaffGachaOfflineFonts();
            fonts.BindBeforeActivation(root);
            foreach (var font in root.GetComponentsInChildren<TMP_Text>(true).Select(text => text.font).Distinct())
            {
                if (font == null || EditorUtility.IsPersistent(font))
                    throw new InvalidOperationException("Memory evidence must use only cloned font assets.");
                string glyphs = string.Concat(root.GetComponentsInChildren<TMP_Text>(true).Select(text => text.text))
                    + "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz—+/:·% 영업권 점수 보상 없음 다음 최고 등급까지 남은 시간 초 미지급 실제 계정에 반영되지 않음 R1 검수 보상 완료 주문";
                font.TryAddCharacters(glyphs, out _);
            }
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            var scaler = root.GetComponent<CanvasScaler>();
            float scale = Mathf.Pow(2, Mathf.Lerp(Mathf.Log(resolution.x / scaler.referenceResolution.x, 2),
                Mathf.Log(resolution.y / scaler.referenceResolution.y, 2), scaler.matchWidthOrHeight));
            scaler.enabled = false;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(resolution.x / scale, resolution.y / scale);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            cameraObject = new GameObject("popup-ui03-memory-camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.orthographic = true;
            camera.orthographicSize = rect.sizeDelta.y * .5f;
            camera.nearClipPlane = .3f; camera.farClipPlane = 2000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .12f, .12f, 1);
            camera.cullingMask = 1 << 31;
            camera.allowHDR = false; camera.allowMSAA = false;
            target = new RenderTexture(resolution.x, resolution.y, 24, RenderTextureFormat.ARGB32)
                { hideFlags = HideFlags.HideAndDontSave };
            target.Create(); camera.targetTexture = target;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            { canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera; }
            root.SetActive(true);
            Call(host, "RefreshServices");
            // The result-test host predates reward preview and does not call this observer's
            // EditMode lifecycle. Bind it explicitly, as the existing preview tests do.
            var reward = root.GetComponent<PopupStoreRewardSession>();
            if (!reward.Refresh()) throw new InvalidOperationException("Memory reward observer could not bind: " + reward.LastError);
            PopupStoreApprovedTestSession.Attach(root, 1);
            Capture(root, camera, target, "start", frames);

            Call(host, "StartCustomers", 3, 90d);
            Capture(root, camera, target, "none-empty-tray-orders-3-4-5", frames);
            var orders = root.GetComponent<PopupStoreOrderDisplay>();
            var counter = root.GetComponentInChildren<PopupStoreSalesCounter>(true);
            foreach (var material in orders.GetDisplayedMaterials(2))
                if (!counter.TryAdd(material)) throw new InvalidOperationException("Could not fill the diagnostic tray with the existing five-material order.");
            Capture(root, camera, target, "none-full-tray-orders-3-4-5", frames);
            counter.Clear();
            foreach (var material in orders.GetDisplayedMaterials(0))
                if (!counter.TryAdd(material)) throw new InvalidOperationException("Could not fill three-slot evidence.");
            Capture(root, camera, target, "none-three-tray-orders-3-4-5", frames);
            counter.Clear();

            foreach (float amount in new[] { 0f, .01f, .5f, 1f })
                Capture(root, camera, target, "display-only-fills-" + Mathf.RoundToInt(amount * 100), frames, amount);

            Call(host, "Pack", 0);
            Call(host, "CompleteVisual", true);
            Call(host, "FinishDepartures");
            Capture(root, camera, target, "actual-sale-100-C", frames);

            var exit = root.GetComponent<PopupStoreExitConfirmation>();
            if (!exit.TryRequestExit()) throw new InvalidOperationException("Existing exit confirmation did not open.");
            Capture(root, camera, target, "existing-exit-confirmation", frames);
            if (!exit.TryContinue()) throw new InvalidOperationException("Existing exit cancellation did not continue.");

            Call(host, "End");
            Call(host, "ShowResult");
            root.GetComponent<PopupStoreResultPanel>().TickResult(1);
            Capture(root, camera, target, "existing-result-100-C", frames);
            var reveal = root.GetComponent<PopupStoreRewardRevealPreview>();
            if (!reveal.TryReveal()) throw new InvalidOperationException("Existing unpaid result preview did not open: "
                + reveal.LastError + "; reward=" + reward.LastError);
            reveal.TickPreview(1);
            Capture(root, camera, target, "existing-unpaid-preview", frames);
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            try { (host as IDisposable)?.Dispose(); }
            finally { fonts?.Dispose(); }
        }
    }

    private static void Capture(GameObject root, Camera camera, RenderTexture target, string state,
        List<Frame> frames, float previewFill = -1)
    {
        root.GetComponent<PopupStoreFinalGuideLayout>()?.ApplyLayout();
        root.GetComponent<PopupStoreOrderDisplay>().Refresh();
        foreach (var clip in root.GetComponentsInChildren<PopupStoreCustomerVisualClip>(true)) clip.Prepare();
        Canvas.ForceUpdateCanvases();
        foreach (var indicator in root.GetComponentsInChildren<PopupStoreScrollPageIndicator>(true)) indicator.Refresh();
        foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        var patienceSlots = Read<PopupStorePatienceSlot[]>(root.GetComponent<PopupStoreCustomerPatience>(), "_slots");
        var grade = root.GetComponent<PopupStoreGradeSession>();
        var progress = Read<Image>(grade, "_progressImage");
        var fills = patienceSlots.Select(slot => slot.Fill).Concat(new[] { progress }).ToArray();
        var original = fills.Select(fill => fill.fillAmount).ToArray();
        var originalSprites = fills.Select(fill => fill.sprite).ToArray();
        if (previewFill >= 0)
        {
            // The existing renderer flushes Canvas meshes before Camera.Render. Setting fills in
            // onPreRender is too late for that UI batch; apply to this isolated view first.
            foreach (var fill in fills) fill.fillAmount = previewFill;
            // Color-state samples affect only this non-saving diagnostic view.
            string[] colors = { "Green", "Yellow", "Red" };
            for (int i = 0; i < patienceSlots.Length; i++)
                patienceSlots[i].Fill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PopupStoreCleanPartsIntegration.Folder + "V4_Patience_Fill_" + colors[i] + ".png");
            Canvas.ForceUpdateCanvases();
        }
        try
        {
            // Reuse the existing protected memory renderer rather than create another screenshot implementation.
            var evidenceType = typeof(PopupStoreApprovedUiEvidence);
            var frameType = evidenceType.GetNestedType("Frame", BindingFlags.NonPublic);
            var existingFrames = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(frameType));
            evidenceType.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { root, camera, target, Folder, "UI04-" + state, existingFrames });
        }
        finally
        {
            for (int i = 0; i < fills.Length; i++) { fills[i].fillAmount = original[i]; fills[i].sprite = originalSprites[i]; }
        }
        var score = root.GetComponent<PopupStoreScore>();
        var orders = root.GetComponent<PopupStoreOrderDisplay>();
        var hud = root.GetComponent<PopupStoreCurrentGradeHud>();
        frames.Add(new Frame
        {
            file = Path.GetFullPath(Folder + "/" + target.width + "x" + target.height + "-UI04-" + state + ".png"),
            state = state, width = target.width, height = target.height, score = score.TotalScore,
            successfulOrders = score.SuccessfulOrderCount,
            counterCount = root.GetComponentInChildren<PopupStoreSalesCounter>(true).Count,
            grade = grade.Current.Grade.ToString(), hudSprite = hud.DisplayedSprite != null ? hud.DisplayedSprite.name : null,
            finalizations = grade.FinalizationCount, rewardDraws = root.GetComponent<PopupStoreRewardSession>().RandomDrawCount,
            revealCount = root.GetComponent<PopupStoreRewardRevealPreview>().RevealCount,
            clockState = root.GetComponent<PopupStoreBusinessClock>().State.ToString(),
            orderMaterialCounts = Enumerable.Range(0, 3).Select(index => orders.GetDisplayedMaterials(index).Count).ToArray(),
            textMetrics = root.GetComponentsInChildren<TMP_Text>(true)
                .Where(text => text.name == "Time" || text.name == "Score"
                    || text.transform.parent != null && text.transform.parent.name == "Clear Counter Button")
                .Select(TextMetric).ToArray(),
            displayOnlyFillPreview = previewFill >= 0, previewFill = previewFill
        });
    }

    private static string TextMetric(TMP_Text text)
    {
        text.ForceMeshUpdate(true);
        var rect = text.rectTransform.rect;
        return string.Format(CultureInfo.InvariantCulture,
            "{0}/{1}; text={2}; preferredWidth={3:0.###}; preferredHeight={4:0.###}; rectW={5:0.###}; rectH={6:0.###}; fontSize={7:0.###}; isTextOverflowing={8}",
            text.transform.parent != null ? text.transform.parent.name : "", text.name,
            text.text.Replace("\n", "\\n"), text.preferredWidth, text.preferredHeight,
            rect.width, rect.height, text.fontSize, text.isTextOverflowing);
    }

    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, Instance).GetValue(target);
    private static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Instance).Invoke(target, args);
    private static Exception Unwrap(Exception exception)
        => exception is TargetInvocationException invocation && invocation.InnerException != null ? invocation.InnerException : exception;
}
#endif
