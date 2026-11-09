#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit, EditMode-only evidence for the existing safety latch. This probe never
// enters Play, reloads/saves a scene, authenticates, or calls a backend operation.
// Script assembly reload is real; runtime BeforeSceneLoad/Scene Reload is NOT RUN.
[InitializeOnLoad]
public static class EditorAccountSafetyReloadVerification
{
    private const string Folder = "Library/PopupStoreVerification/DEVFINAL01C";
    private const string RequestPath = Folder + "/reload-request.txt";
    private const string ResultPath = Folder + "/reload-result.json";
    private const string PendingKey = "Panda.PopupStore.Final01C.ReloadProbe";
    private const string MenuRoot = "Tools/Panda Restaurant/Popup Store/실계정 안전 진입/";
    private const string PrepareMenu = MenuRoot + "안전 잠금 준비 (Play 실행 안 함)";
    private const string DisarmMenu = MenuRoot + "안전 잠금 해제 (Play OFF)";
    private static readonly string DomainEpoch = Guid.NewGuid().ToString("N");
    private static double _nextPoll;

    [Serializable] private sealed class Check
    {
        public string name, status, detail;
        public Check(string label, bool passed, string message = null)
        { name = label; status = passed ? "PASS" : "FAIL"; detail = message; }
    }

    [Serializable] private sealed class Workspace
    {
        public string scenes, activeScene, startScene, sceneRoots, selection, managers, gameViews, undo;
        public bool playOptionsEnabled;
        public int playOptions;
    }

    [Serializable] private sealed class Evidence
    {
        public string status, startedUtc, finishedUtc, beforeReloadUtc, afterReloadUtc, error;
        public string originalEpoch, returnedEpoch;
        public bool pending, beforeReloadObserved, originallyArmed, originalSuppression, ownsLatch;
        public bool restoreAttempted, safetyStateRestored, workspaceUnchanged;
        public bool actualAssemblyReload, probeRequestedPlay, runtimeSceneReload;
        public int backendOperationCalls, realTicketUses, realDiamondGrants;
        public Workspace before, after;
        public List<Check> checks = new List<Check>();
        public string scope = "Existing safety latch in original Unity, real EditMode script assembly reload. No Play, scene reload, login, SDK request, account restoration or reward operation. Runtime Scene Reload, real account readiness and server balance are NOT RUN. Operation counters describe probe code only, not a server-side audit.";
    }

    static EditorAccountSafetyReloadVerification()
    {
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
        AssemblyReloadEvents.afterAssemblyReload += AfterAssemblyReload;
        if (ReadPending() != null) EditorApplication.delayCall += CompleteAfterReload;
    }

    [MenuItem(MenuRoot + "잠금 Domain Reload 검증 (Play 실행 안 함)")]
    public static void Run()
    {
        if (!string.IsNullOrEmpty(SessionState.GetString(PendingKey, "")))
        {
            RejectSeparate("An explicit reload probe is already pending; the existing latch was left untouched.");
            return;
        }

        var evidence = new Evidence { status = "NOT RUN", startedUtc = Utc(), originalEpoch = DomainEpoch };
        if (!CanBegin(out string reason))
        {
            evidence.error = reason;
            evidence.finishedUtc = Utc();
            SaveResult(evidence);
            return; // Never disarm an existing user's latch on rejection.
        }

        try { evidence.before = ReadWorkspace(); }
        catch (Exception error)
        { evidence.error = error.ToString(); evidence.finishedUtc = Utc(); SaveResult(evidence); return; }
        evidence.originallyArmed = EditorAccountSafety.IsArmed;
        evidence.originalSuppression = MainScene.SuppressEditorDebugGrantsForTesting;
        if (evidence.originallyArmed && !evidence.originalSuppression)
        {
            evidence.error = "An existing armed latch has inconsistent suppression. No probe changes were made.";
            evidence.finishedUtc = Utc();
            SaveResult(evidence);
            return;
        }

        try
        {
            // Retained callback is created before Prepare and checked only when invoked.
            // The payload is an isolated integer, never UserInfo or a live UI action.
            int invoked = 0;
            Action retained = () => EditorAccountSafety.RunDebugAction(() => invoked++);
            evidence.pending = true;
            evidence.status = "PREPARING";
            SavePending(evidence);
            bool menuExecuted = EditorApplication.ExecuteMenuItem(PrepareMenu);
            evidence.ownsLatch = !evidence.originallyArmed && EditorAccountSafety.IsArmed;
            evidence.checks.Add(new Check("Existing Prepare menu executed", menuExecuted));
            evidence.checks.Add(new Check("Prepared latch and suppression", EditorAccountSafety.IsArmed
                && MainScene.SuppressEditorDebugGrantsForTesting && EditorAccountSafety.DebugActionsBlocked));
            retained();
            evidence.checks.Add(new Check("Callback created before Prepare is blocked when invoked", invoked == 0,
                "Integer-only callback; no real debug action or account field was invoked."));
            if (evidence.checks.Any(check => check.status != "PASS"))
                throw new InvalidOperationException("Existing preparation or callback guard did not pass.");
            if (!CanBegin(out reason)) throw new InvalidOperationException(reason);
            evidence.status = "AWAITING ASSEMBLY RELOAD";
            SavePending(evidence);
            SaveResult(evidence);
            EditorUtility.RequestScriptReload();
        }
        catch (Exception error)
        {
            evidence.error = error.ToString();
            Finish(evidence);
        }
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < _nextPoll) return;
        _nextPoll = EditorApplication.timeSinceStartup + .5;
        Evidence pending = ReadPending();
        if (pending != null)
        {
            if (pending.originalEpoch != DomainEpoch)
            {
                CompleteAfterReload();
                return;
            }
            if (DateTime.TryParse(pending.startedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind,
                    out DateTime started) && DateTime.UtcNow - started.ToUniversalTime() > TimeSpan.FromSeconds(90))
            {
                pending.error = "No completed script assembly reload was observed within 90 seconds.";
                Finish(pending);
                return;
            }
        }
        if (!File.Exists(RequestPath)) return;
        string command;
        try
        {
            command = File.ReadAllText(RequestPath).Trim();
            File.Delete(RequestPath); // One explicit request, consumed once; never an automatic retry.
        }
        catch (IOException) { return; }
        if (command != "run")
            RejectSeparate("Unknown reload probe request; expected 'run'. No safety state was changed.");
        else Run();
    }

    private static void BeforeAssemblyReload()
    {
        Evidence evidence = ReadPending();
        if (evidence == null) return;
        evidence.beforeReloadObserved = true;
        evidence.beforeReloadUtc = Utc();
        SavePending(evidence);
    }

    private static void AfterAssemblyReload()
    {
        Evidence evidence = ReadPending();
        if (evidence == null) return;
        evidence.afterReloadUtc = Utc();
        SavePending(evidence);
        EditorApplication.delayCall += CompleteAfterReload;
    }

    private static void CompleteAfterReload()
    {
        Evidence evidence = ReadPending();
        if (evidence == null || evidence.originalEpoch == DomainEpoch) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        evidence.returnedEpoch = DomainEpoch;
        evidence.afterReloadUtc = Utc();
        evidence.actualAssemblyReload = evidence.beforeReloadObserved && evidence.originalEpoch != DomainEpoch;
        evidence.checks.Add(new Check("Actual assembly reload observed", evidence.actualAssemblyReload,
            "beforeAssemblyReload event and a new static domain epoch in the same Editor session."));
        evidence.checks.Add(new Check("SessionState latch survived real assembly reload", EditorAccountSafety.IsArmed));
        evidence.checks.Add(new Check("InitializeOnLoad restored suppression", MainScene.SuppressEditorDebugGrantsForTesting));
        evidence.checks.Add(new Check("Debug actions remain blocked after reload", EditorAccountSafety.DebugActionsBlocked));
        int invoked = 0;
        EditorAccountSafety.RunDebugAction(() => invoked++);
        evidence.checks.Add(new Check("Post-reload debug callback cannot run", invoked == 0,
            "Integer-only callback; not a payout simulation."));
        evidence.checks.Add(new Check("No backend owner or SDK login was created", NoBackendOwnerOrSession()));
        evidence.checks.Add(new Check("Play remained OFF", !EditorApplication.isPlayingOrWillChangePlaymode));
        if (!CanBegin(out string reason)) evidence.error = reason;
        Finish(evidence);
    }

    private static void Finish(Evidence evidence)
    {
        // Restore only the latch this probe acquired. Never unlock a pre-existing
        // latch, and never force cleanup while a different Play/account is active.
        try
        {
            if (evidence.ownsLatch)
            {
                if (!CanRestore())
                    throw new InvalidOperationException("Probe-owned latch remains armed: Editor became busy or has a backend session. No forced cleanup was attempted.");
                evidence.restoreAttempted = true;
                if (!EditorApplication.ExecuteMenuItem(DisarmMenu))
                    throw new InvalidOperationException("Existing Disarm menu could not be executed.");
            }
            evidence.safetyStateRestored = EditorAccountSafety.IsArmed == evidence.originallyArmed
                && MainScene.SuppressEditorDebugGrantsForTesting == evidence.originalSuppression;
            evidence.after = ReadWorkspace();
            evidence.workspaceUnchanged = JsonUtility.ToJson(evidence.before) == JsonUtility.ToJson(evidence.after);
            evidence.checks.Add(new Check("Original safety state preserved/restored", evidence.safetyStateRestored));
            evidence.checks.Add(new Check("Scene, Undo, selection, managers, GameView and Play options unchanged", evidence.workspaceUnchanged));
        }
        catch (Exception error)
        {
            evidence.error = string.IsNullOrEmpty(evidence.error) ? error.ToString() : evidence.error + "\n" + error;
        }
        evidence.pending = false;
        evidence.finishedUtc = Utc();
        evidence.status = evidence.actualAssemblyReload && string.IsNullOrEmpty(evidence.error)
            && evidence.checks.All(check => check.status == "PASS") ? "PASS" : "BLOCKED";
        SaveResult(evidence);
        SessionState.EraseString(PendingKey);
        Debug.Log("ACCOUNT_SAFETY_RELOAD_PROBE: " + evidence.status + "; " + ResultPath
            + "; runtime Scene Reload, account restoration and all server operations NOT RUN.");
    }

    private static bool CanBegin(out string reason)
    {
        reason = null;
        if (!CanRestore()) reason = "Requires idle Play OFF, completed compilation/import, no SDK session and no backend owner.";
        else if (EditorUtility.scriptCompilationFailed) reason = "Compilation errors are present.";
        else if (!TestRunnersIdle()) reason = "All existing test runners and pending popup verification requests must be idle.";
        else if (PrefabStageUtility.GetCurrentPrefabStage() != null) reason = "Close Prefab Stage before verification.";
        else if (SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().path != "Assets/Scenes/FirstLoadingScene.unity"
            || SceneManager.GetActiveScene().isDirty) reason = "Requires the single clean original FirstLoadingScene. No scene will be saved or replaced.";
        else if (EditorSceneManager.playModeStartScene != null
            && AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) != "Assets/Scenes/FirstLoadingScene.unity")
            reason = "A different Play start scene is configured.";
        return reason == null;
    }

    private static bool CanRestore() => !EditorApplication.isPlayingOrWillChangePlaymode
        && !EditorApplication.isCompiling && !EditorApplication.isUpdating && NoBackendOwnerOrSession();

    private static bool TestRunnersIdle()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var unity = typeof(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi).GetMethod("IsRunActive", flags);
        var popup = typeof(PopupStoreVerificationRunner).GetField("_running", flags);
        return unity != null && popup != null && !(bool)unity.Invoke(null, null)
            && !(bool)popup.GetValue(null) && !GachaReviewTestRunner.IsRunning
            && !File.Exists("Temp/PopupStoreVerification/request.txt");
    }

    private static bool NoBackendOwnerOrSession()
    {
        if (BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin) return false;
        FieldInfo field = typeof(Muks.BackEnd.BackendManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
        // Direct field read only. Never call Instance or create/inject a synthetic singleton.
        return field != null && field.GetValue(null) == null;
    }

    private static Workspace ReadWorkspace()
    {
        Scene[] scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        return new Workspace
        {
            scenes = string.Join("\n", scenes.Select(scene => scene.path + "|" + scene.isLoaded + "|" + scene.isDirty)),
            activeScene = SceneManager.GetActiveScene().path,
            startScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),
            sceneRoots = string.Join("\n", scenes.SelectMany(scene => scene.GetRootGameObjects())
                .OrderBy(root => root.GetInstanceID()).Select(root => root.GetInstanceID() + "|" + root.name + "|" + root.activeSelf)),
            selection = string.Join(",", Selection.instanceIDs.Select(value => value.ToString())) + ";active=" + Selection.activeInstanceID,
            managers = string.Join("\n", Resources.FindObjectsOfTypeAll<GameManager>().Cast<UnityEngine.Object>()
                .Concat(Resources.FindObjectsOfTypeAll<ChallengeManager>()).OrderBy(value => value.GetInstanceID())
                .Select(value => value.GetType().FullName + "|" + value.GetInstanceID())),
            gameViews = ReadGameViews(),
            undo = ReadUndo(),
            playOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled,
            playOptions = (int)EditorSettings.enterPlayModeOptions
        };
    }

    private static string ReadUndo()
    {
        var method = typeof(Undo).GetMethod("GetRecords", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(List<string>), typeof(int).MakeByRefType() }, null);
        if (method == null) throw new NotSupportedException("Undo history cannot be observed; preservation must not be asserted.");
        var records = new List<string>();
        var arguments = new object[] { records, 0 };
        method.Invoke(null, arguments);
        return arguments[1] + "|" + string.Join("\n", records);
    }

    private static string ReadGameViews()
    {
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return string.Join("\n", Resources.FindObjectsOfTypeAll(type).OfType<EditorWindow>()
            .OrderBy(view => view.GetInstanceID()).Select(view =>
            {
                object zoom = type.GetField("m_ZoomArea", flags)?.GetValue(view);
                var size = type.GetProperty("selectedSizeIndex", flags);
                var lowResolution = type.GetProperty("lowResolutionForAspectRatios", flags);
                var scale = zoom?.GetType().GetProperty("scale", flags);
                var translation = zoom?.GetType().GetProperty("translation", flags);
                if (size == null || lowResolution == null || scale == null || translation == null)
                    throw new NotSupportedException("GameView cannot be fully observed; preservation must not be asserted.");
                return view.GetInstanceID() + "|" + size.GetValue(view) + "|" + lowResolution.GetValue(view)
                    + "|" + view.position.ToString("R") + "|" + view.maximized
                    + "|" + ((Vector2)scale.GetValue(zoom)).ToString("R")
                    + "|" + ((Vector2)translation.GetValue(zoom)).ToString("R");
            }));
    }
    private static string Utc() => DateTime.UtcNow.ToString("O");
    private static Evidence ReadPending()
    {
        string value = SessionState.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(value)) return null;
        try { Evidence evidence = JsonUtility.FromJson<Evidence>(value); return evidence != null && evidence.pending ? evidence : null; }
        catch { return null; }
    }
    private static void SavePending(Evidence evidence) => SessionState.SetString(PendingKey, JsonUtility.ToJson(evidence));
    private static void SaveResult(Evidence evidence)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(ResultPath, JsonUtility.ToJson(evidence, true));
    }
    private static void RejectSeparate(string reason)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/reload-rejected-request.json", JsonUtility.ToJson(
            new Evidence { status = "NOT RUN", startedUtc = Utc(), finishedUtc = Utc(), error = reason }, true));
    }
}
#endif
