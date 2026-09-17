#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Read-only evidence of the user's scene workspace. This never saves a scene, changes selection,
/// records Undo, applies a SerializedObject, or changes which scene is active.
/// Demo preview scenes created after Capture are deliberately excluded.
/// </summary>
public sealed class GachaCollectionPreviewSceneWitness
{
    private readonly SortedDictionary<string, string> _state;
    private readonly Scene[] _additionalScenes;
    public string Fingerprint { get; }
    public int DirtySceneCount { get; }
    public int UserSceneCount { get; }
    public bool HasUndoHistoryEvidence { get; }

    private GachaCollectionPreviewSceneWitness(Scene[] additionalScenes)
    {
        _additionalScenes = additionalScenes ?? new Scene[0];
        _state = ReadState(_additionalScenes, out int dirty, out int sceneCount, out bool undoHistory);
        DirtySceneCount = dirty;
        UserSceneCount = sceneCount;
        HasUndoHistoryEvidence = undoHistory;
        using (var sha = SHA256.Create())
            Fingerprint = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                string.Join("\n", _state.Select(x => x.Key + "=" + x.Value))))).Replace("-", "");
    }

    public static GachaCollectionPreviewSceneWitness Capture(params Scene[] additionalScenes)
        => new GachaCollectionPreviewSceneWitness(additionalScenes);

    public string[] Differences()
    {
        var current = ReadState(_additionalScenes, out _, out _, out _);
        return _state.Keys.Union(current.Keys).OrderBy(x => x, StringComparer.Ordinal)
            .Where(key => !_state.TryGetValue(key, out string before) ||
                          !current.TryGetValue(key, out string after) || before != after)
            .Select(key => key + ": " + Describe(_state, key) + " -> " + Describe(current, key)).ToArray();
    }

    public void AssertUnchanged(string phase = "offline preview")
    {
        string[] differences = Differences();
        if (differences.Length != 0)
            throw new InvalidOperationException("Scene workspace changed during " + phase + ":\n" +
                string.Join("\n", differences.Take(20)));
    }

    public string Summary => "scenes=" + UserSceneCount + "; dirty=" + DirtySceneCount +
        "; sha256=" + Fingerprint + "; undoHistory=" + HasUndoHistoryEvidence;

    private static string Describe(IDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out string value)) return "<missing>";
        return value.Length <= 200 ? value : value.Substring(0, 200) + "...";
    }

    private static SortedDictionary<string, string> ReadState(Scene[] additionalScenes,
        out int dirtySceneCount, out int userSceneCount, out bool hasUndoHistory)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var scenes = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!EditorSceneManager.IsPreviewScene(scene)) scenes.Add(scene);
        }
        userSceneCount = scenes.Count;
        values["workspace.sceneOrder"] = string.Join(",", scenes.Select(x => x.handle));
        values["workspace.activeScene"] = SceneManager.GetActiveScene().handle.ToString();
        values["workspace.setup"] = string.Join("\n", EditorSceneManager.GetSceneManagerSetup()
            .Select(x => x.path + "|" + x.isLoaded + "|" + x.isActive));
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        values["workspace.prefabStage"] = prefabStage == null ? "" :
            prefabStage.assetPath + "|" + prefabStage.scene.handle;
        if (prefabStage != null && !scenes.Any(x => x.handle == prefabStage.scene.handle))
            scenes.Add(prefabStage.scene);
        foreach (Scene extra in additionalScenes)
            if (!scenes.Any(x => x.handle == extra.handle)) scenes.Add(extra);

        values["selection.objects"] = string.Join(",", Selection.instanceIDs);
        values["selection.active"] = Selection.activeInstanceID.ToString();
        values["selection.context"] = Id(Selection.activeContext).ToString();
        values["undo.group"] = Undo.GetCurrentGroup().ToString();
        values["undo.name"] = Undo.GetCurrentGroupName() ?? "";
        hasUndoHistory = TryReadUndoHistory(out string undoHistory);
        values["undo.history"] = undoHistory;
        values["editor.playing"] = EditorApplication.isPlaying.ToString();
        values["editor.playTransition"] = EditorApplication.isPlayingOrWillChangePlaymode.ToString();
        values["editor.playStartScene"] = Id(EditorSceneManager.playModeStartScene).ToString();
        dirtySceneCount = 0;
        foreach (Scene scene in scenes)
        {
            string key = "scene[" + scene.handle + "]";
            values[key + ".valid"] = scene.IsValid().ToString();
            if (!scene.IsValid()) continue;
            if (scene.isDirty) dirtySceneCount++;
            values[key + ".path"] = scene.path ?? "";
            values[key + ".name"] = scene.name ?? "";
            values[key + ".loaded"] = scene.isLoaded.ToString();
            values[key + ".dirty"] = scene.isDirty.ToString();
            if (!scene.isLoaded) continue;
            GameObject[] roots = scene.GetRootGameObjects();
            values[key + ".roots"] = string.Join(",", roots.Select(Id));
            foreach (GameObject root in roots) ReadObject(values, key, root);
        }
        return values;
    }

    private static void ReadObject(IDictionary<string, string> values, string sceneKey, GameObject gameObject)
    {
        string key = sceneKey + ".object[" + gameObject.GetInstanceID() + "]";
        values[key + ".json"] = EditorJsonUtility.ToJson(gameObject);
        values[key + ".dirty"] = EditorUtility.IsDirty(gameObject).ToString();
        values[key + ".parent"] = Id(gameObject.transform.parent).ToString();
        values[key + ".order"] = gameObject.transform.GetSiblingIndex().ToString();
        Component[] components = gameObject.GetComponents<Component>();
        values[key + ".components"] = string.Join(",", components.Select(Id));
        foreach (Component component in components)
        {
            if (component == null) continue;
            string componentKey = key + ".component[" + component.GetInstanceID() + "]";
            values[componentKey + ".type"] = component.GetType().AssemblyQualifiedName;
            values[componentKey + ".json"] = EditorJsonUtility.ToJson(component);
            values[componentKey + ".dirty"] = EditorUtility.IsDirty(component).ToString();
        }
        foreach (Transform child in gameObject.transform) ReadObject(values, sceneKey, child.gameObject);
    }

    private static int Id(Object value) => value == null ? 0 : value.GetInstanceID();

    // Unity exposes group/name publicly. When this editor provides its read-only history accessor,
    // record the full undo/redo labels and cursor as additional evidence without flushing Undo.
    private static bool TryReadUndoHistory(out string result)
    {
        MethodInfo method = typeof(Undo).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(x => x.Name == "GetRecords" && x.GetParameters().Length == 2 &&
                x.GetParameters()[0].ParameterType == typeof(List<string>) &&
                x.GetParameters()[1].ParameterType == typeof(int).MakeByRefType());
        if (method == null) { result = "unavailable"; return false; }
        try
        {
            var records = new List<string>();
            var arguments = new object[] { records, 0 };
            method.Invoke(null, arguments);
            result = arguments[1] + "|" + string.Join("\n", records);
            return true;
        }
        catch (Exception exception)
        {
            result = "unavailable:" + exception.GetType().Name;
            return false;
        }
    }
}

public sealed class GachaCollectionPreviewSafetyTests
{
    [Test]
    public void WorkspaceWitnessIsReadOnlyAndStable()
    {
        var before = GachaCollectionPreviewSceneWitness.Capture();
        var repeated = GachaCollectionPreviewSceneWitness.Capture();
        Assert.That(repeated.Fingerprint, Is.EqualTo(before.Fingerprint));
        before.AssertUnchanged("repeated read-only capture");
    }

    [Test]
    public void EmptyPreviewSceneLifecyclePreservesLoadedUserScenes()
    {
        var before = GachaCollectionPreviewSceneWitness.Capture();
        Scene preview = EditorSceneManager.NewPreviewScene();
        try { before.AssertUnchanged("preview scene open"); }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        before.AssertUnchanged("preview scene closed");
    }

    [UnityTest]
    public IEnumerator IntegratedPreviewOpenClosePreservesCurrentSceneWorkspace()
    {
        Type type = typeof(GachaCollectionOfflineDemo).Assembly.GetType("GachaCollectionPreviewWindow", true);
        PropertyInfo active = type.GetProperty("ActiveWindow", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(active, Is.Not.Null);
        Assert.That(active.GetValue(null), Is.Null, "Close a prior demo preview before running this isolated test.");
        var before = GachaCollectionPreviewSceneWitness.Capture();
        EditorWindow window = null;
        try
        {
            type.GetMethod("Open", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, null);
            window = active.GetValue(null) as EditorWindow;
            Assert.That(window, Is.Not.Null);
            PropertyInfo ready = type.GetProperty("IsReady", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(ready, Is.Not.Null);
            double deadline = EditorApplication.timeSinceStartup + 30;
            while (!(bool)ready.GetValue(window) && EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.That((bool)ready.GetValue(window), Is.True, "Integrated preview failed to become ready.");
            before.AssertUnchanged("integrated preview ready");
            yield return null;
            before.AssertUnchanged("integrated preview running");
        }
        finally
        {
            if (window != null) window.Close();
        }
        before.AssertUnchanged("integrated preview closed");
        Assert.That(active.GetValue(null), Is.Null);
    }
}
#endif
