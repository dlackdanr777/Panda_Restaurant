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

/// <summary>Explicit original-prefab memory renders. These are Editor layout diagnostics, not GameView or user Play evidence.</summary>
public static class PopupStoreApprovedUiEvidence
{
    [Serializable] private sealed class Report
    {
        public string scope = "Original popup prefab instantiated by the existing result-test memory host; real isolated admission, sale, score and final result. RenderTexture screenshots, NOT actual GameView/Stage1 Play or user approval.";
        public string startedUtc, finishedUtc, error;
        public bool success;
        public Frame[] frames;
    }
    [Serializable] private sealed class Frame
    {
        public string file, state, grade, clockState;
        public int width, height, score, successfulOrders, finalizations, rewardDraws;
    }

    public static void Run(string label)
    {
        if (label != "before" && label != "after") throw new ArgumentException("Evidence label must be before or after.", nameof(label));
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("UI memory evidence requires a settled EditMode Editor.");
        if (BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
            throw new InvalidOperationException("UI evidence will not run while the live SDK/account is active.");
        string folder = "Temp/PopupStoreVerification/UIEvidence/" + label;
        Directory.CreateDirectory(folder);
        var report = new Report { startedUtc = DateTime.UtcNow.ToString("O") };
        var frames = new List<Frame>();
        try
        {
            foreach (var resolution in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(2340, 1080) })
            {
                object host = null;
                StaffGachaOfflineFonts fonts = null;
                GameObject cameraObject = null;
                RenderTexture target = null;
                var previousTarget = RenderTexture.active;
                try
                {
                    var type = typeof(PopupStoreResultTests).GetNestedType("Host", BindingFlags.NonPublic);
                    host = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                        new object[] { null }, CultureInfo.InvariantCulture);
                    var root = Read<GameObject>(host, "Root");
                    // The synchronous constructor has not yielded a render frame. Isolate every font/atlas before forcing any mesh.
                    root.SetActive(false);
                    fonts = new StaffGachaOfflineFonts();
                    fonts.BindBeforeActivation(root);
                    foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                    var scaler = root.GetComponent<CanvasScaler>();
                    float scale = Mathf.Pow(2, Mathf.Lerp(Mathf.Log(resolution.x / scaler.referenceResolution.x, 2),
                        Mathf.Log(resolution.y / scaler.referenceResolution.y, 2), scaler.matchWidthOrHeight));
                    scaler.enabled = false;
                    var rect = root.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(resolution.x / scale, resolution.y / scale);
                    root.transform.position = Vector3.zero;
                    root.transform.rotation = Quaternion.identity;
                    root.transform.localScale = Vector3.one;
                    cameraObject = new GameObject("popup-ui-evidence-memory-camera") { hideFlags = HideFlags.HideAndDontSave };
                    var camera = cameraObject.AddComponent<Camera>();
                    camera.enabled = false;
                    camera.transform.position = new Vector3(0, 0, -1000);
                    camera.transform.rotation = Quaternion.identity;
                    camera.orthographic = true;
                    camera.orthographicSize = rect.sizeDelta.y / 2f;
                    camera.nearClipPlane = .3f; camera.farClipPlane = 2000f;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(.12f, .12f, .12f, 1);
                    camera.cullingMask = 1 << 31;
                    camera.allowHDR = false; camera.allowMSAA = false;
                    target = new RenderTexture(resolution.x, resolution.y, 24, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.HideAndDontSave };
                    target.Create(); camera.targetTexture = target;
                    foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    { canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera; }
                    root.SetActive(true);
                    Call(host, "RefreshServices");
                    PopupStoreApprovedTestSession.Attach(root, 1);
                    Capture(root, camera, target, folder, "start", frames);

                    // Advance only the explicitly isolated diagnostic instance with existing runtime APIs.
                    Call(host, "StartCustomers", 3, 90d);
                    Capture(root, camera, target, folder, "play-ready-max-order", frames);
                    Call(host, "Pack", 0);
                    Call(host, "CompleteVisual", true);
                    Call(host, "FinishDepartures");
                    Capture(root, camera, target, folder, "play-100-C-max-order", frames);

                    Call(host, "End");
                    Call(host, "ShowResult");
                    Capture(root, camera, target, folder, "result-100-C", frames);
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
            report.success = true;
        }
        catch (Exception exception)
        {
            report.error = (exception is TargetInvocationException invocation && invocation.InnerException != null ? invocation.InnerException : exception).ToString();
            Debug.LogError("POPUP_STORE_UI_EVIDENCE_FAILED " + report.error);
        }
        finally
        {
            report.finishedUtc = DateTime.UtcNow.ToString("O"); report.frames = frames.ToArray();
            File.WriteAllText(folder + "/report.json", JsonUtility.ToJson(report, true));
            Debug.Log("POPUP_STORE_UI_EVIDENCE label=" + label + "; success=" + report.success + "; frames=" + frames.Count
                + "; scope=memory-render-not-GameView; report=" + folder + "/report.json");
        }
    }

    private static void Capture(GameObject root, Camera camera, RenderTexture target, string folder, string state, List<Frame> frames)
    {
        // This synchronous memory renderer has no player LateUpdate; flush only the display snapshot.
        root.GetComponent<PopupStoreCurrentGradeHud>()?.RefreshDisplay();
        Canvas.ForceUpdateCanvases();
        foreach (var list in root.GetComponentsInChildren<PopupStoreMaterialList>(true))
        {
            list.Refresh();
            Read<ScrollRect>(list, "_scrollRect").Rebuild(CanvasUpdate.PostLayout);
        }
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true, true);
        Canvas.ForceUpdateCanvases();
        camera.Render();
        Texture2D screenshot = null;
        var previous = RenderTexture.active;
        string path = folder + "/" + target.width + "x" + target.height + "-" + state + ".png";
        try
        {
            RenderTexture.active = target;
            screenshot = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            screenshot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            screenshot.Apply(false, false);
            File.WriteAllBytes(path, screenshot.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; if (screenshot != null) Object.DestroyImmediate(screenshot); }
        var score = root.GetComponent<PopupStoreScore>();
        var grade = root.GetComponent<PopupStoreGradeSession>();
        frames.Add(new Frame { file = Path.GetFullPath(path), state = state, width = target.width, height = target.height,
            score = score.TotalScore, successfulOrders = score.SuccessfulOrderCount, grade = grade.Current.Grade.ToString(),
            finalizations = grade.FinalizationCount, rewardDraws = root.GetComponent<PopupStoreRewardSession>().RandomDrawCount,
            clockState = root.GetComponent<PopupStoreBusinessClock>().State.ToString() });
    }

    private static T Read<T>(object target, string field)
        => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);
    private static object Call(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);
}
#endif
