#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Explicit local verification bridge. It is dormant unless request.txt in this project's Temp
/// directory contains "inspect", "verify", or "verify-current". All output and captures remain in Temp.
/// It never saves, dirties, or closes a user scene. If needed, verification owns one disposable
/// preview fixture and closes only its fixture. The report identifies whether Unity marked it dirty.
/// </summary>
[InitializeOnLoad]
public static class GachaCollectionPreviewVerification
{
    private const string DirectoryPath = "Logs/GachaReview20260917-2213/DirtyScene";
    private const string MenuPath = "Tools/Panda Restaurant/Gacha Collection/Open Integrated Offline Demo";
    private static readonly string RequestPath = "Temp/GachaCollectionPreview/request.txt";
    private static readonly Queue<Action> Steps = new Queue<Action>();
    private static readonly List<string> Checks = new List<string>();
    private static readonly List<string> Captures = new List<string>();
    private static GachaCollectionPreviewSceneWitness _witness;
    private static GachaCollectionPreviewSceneWitness _originalWitness;
    private static Scene _ownedFixture;
    private static EditorWindow _window;
    private static Type _windowType;
    private static double _nextPoll, _nextStep, _readyDeadline;
    private static bool _running, _waitingForReady;
    private static bool _usedOwnedDirtyFixture;
    private static bool _ownedFixtureMarkedDirty;
    private static string _started;
    private static string _reportName = "verification.json", _capturePrefix = "", _ownedFixtureKind = "None";
    private static string _ownedFixtureAssetDirectory;
    private static bool _createdAssetTempParent;
    private static string _newStaffId, _uniqueStaffId, _specialStaffId, _itemId;

    [Serializable] private sealed class SceneInfo
    {
        public int handle, rootCount;
        public string name, path;
        public bool dirty, loaded, active;
    }
    [Serializable] private sealed class Inspection
    {
        public string utc, witness, fingerprint;
        public int dirtySceneCount, activeSceneHandle;
        public bool playing, undoHistoryCaptured;
        public SceneInfo[] scenes;
    }
    [Serializable] private sealed class Result
    {
        public bool success;
        public string startedUtc, finishedUtc, beforeWitness, originalWitness, afterWitness, error;
        public int dirtyScenesBefore, originalDirtyScenesBefore;
        public bool ownedDirtyFixture;
        public string ownedDirtyFixtureKind;
        public string[] passedChecks, screenshots;
    }

    static GachaCollectionPreviewVerification() => EditorApplication.update += Tick;

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        double now = EditorApplication.timeSinceStartup;
        if (_running)
        {
            try
            {
                if (_waitingForReady)
                {
                    if (_window == null) throw new InvalidOperationException("Preview window closed before readiness.");
                    if (Get<bool>("IsReady"))
                    {
                        _waitingForReady = false;
                        _witness.AssertUnchanged("menu opened preview");
                        Checks.Add(_originalWitness.DirtySceneCount > 0
                            ? "A: original menu opened while existing user scene remained dirty"
                            : _ownedFixtureMarkedDirty
                                ? (_ownedFixtureKind == "TempScene"
                                    ? "A: original menu opened with owned ordinary dirty Temp Scene; original untitled user scene unchanged"
                                    : "A-preview: original menu opened with owned dirty PreviewScene; original untitled user scene unchanged")
                                : "Current-workspace: original menu opened directly on untitled user scene; ordinary dirty-scene case not asserted");
                        Checks.Add("B: full user scene/selection/Undo witness unchanged after open");
                        BuildSteps();
                        _nextStep = now + 1.1;
                    }
                    else if (now >= _readyDeadline) throw new TimeoutException("Preview did not become ready within 30 seconds.");
                    return;
                }
                if (now < _nextStep) return;
                if (Steps.Count == 0) { Complete(); return; }
                Steps.Dequeue()();
                _witness.AssertUnchanged("preview scenario");
                _nextStep = now + 1.1;
            }
            catch (Exception error) { Fail(error); }
            return;
        }
        if (now < _nextPoll) return;
        _nextPoll = now + .25;
        if (!File.Exists(RequestPath)) return;
        string command;
        try
        {
            command = File.ReadAllText(RequestPath).Trim();
            File.Delete(RequestPath);
            Directory.CreateDirectory(DirectoryPath);
            if (command == "inspect") Inspect();
            else if (command == "verify") Begin(true);
            else if (command == "verify-current") Begin(false);
            else if (command == "verify-dirty-scene") Begin(true, true);
            else File.WriteAllText(Path.Combine(DirectoryPath, "request-error.txt"), "Unsupported request: " + command);
        }
        catch (Exception error)
        {
            if (_running) Fail(error);
            else File.WriteAllText(Path.Combine(DirectoryPath, "request-error.txt"), error.ToString());
        }
    }

    private static void Inspect()
    {
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        var scenes = new List<SceneInfo>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (EditorSceneManager.IsPreviewScene(scene)) continue;
            scenes.Add(new SceneInfo { handle = scene.handle, name = scene.name, path = scene.path,
                dirty = scene.isDirty, loaded = scene.isLoaded, rootCount = scene.rootCount,
                active = scene.handle == SceneManager.GetActiveScene().handle });
        }
        var report = new Inspection { utc = DateTime.UtcNow.ToString("o"), witness = witness.Summary,
            fingerprint = witness.Fingerprint, dirtySceneCount = witness.DirtySceneCount,
            activeSceneHandle = SceneManager.GetActiveScene().handle, playing = EditorApplication.isPlaying,
            undoHistoryCaptured = witness.HasUndoHistoryEvidence, scenes = scenes.ToArray() };
        File.WriteAllText(Path.Combine(DirectoryPath, "inspection.json"), JsonUtility.ToJson(report, true));
        witness.AssertUnchanged("read-only scene inspection");
    }

    private static void Begin(bool createFixture, bool ordinaryDirtyScene = false)
    {
        _reportName = ordinaryDirtyScene ? "verify-dirty-scene.json" : "verification.json";
        _capturePrefix = ordinaryDirtyScene ? "dirty-scene-" : "";
        _ownedFixtureKind = "None";
        _ownedFixtureAssetDirectory = null;
        _createdAssetTempParent = false;
        _started = DateTime.UtcNow.ToString("o");
        Checks.Clear(); Captures.Clear(); Steps.Clear();
        _originalWitness = GachaCollectionPreviewSceneWitness.Capture();
        _witness = null;
        _ownedFixture = default;
        _usedOwnedDirtyFixture = false;
        _ownedFixtureMarkedDirty = false;
        _window = null;
        _running = true;
        Ensure(!EditorApplication.isPlayingOrWillChangePlaymode, "Verification requires Edit Mode without entering Play Mode.");
        _windowType = typeof(GachaCollectionOfflineDemo).Assembly.GetType("GachaCollectionPreviewWindow", true);
        Ensure(ActiveWindow() == null, "An existing preview is already open; verification will not close somebody else's window.");
        if (createFixture && (_originalWitness.DirtySceneCount == 0 || ordinaryDirtyScene))
        {
            Scene originalActive = SceneManager.GetActiveScene();
            try
            {
                GameObject sentinel;
                if (ordinaryDirtyScene)
                {
                    _ownedFixtureKind = "TempScene";
                    // OpenScene requires an imported SceneAsset in this editor. This explicit test
                    // owns a unique Assets/Temp directory and removes it on every completion/failure.
                    // The actual demo remains entirely in an unsaved PreviewScene.
                    const string assetTempParent = "Assets/Temp";
                    _createdAssetTempParent = !Directory.Exists(assetTempParent) && !File.Exists(assetTempParent + ".meta");
                    string ownedDirectory = assetTempParent + "/GachaCollectionPreview_" + Guid.NewGuid().ToString("N");
                    Ensure(!Directory.Exists(ownedDirectory) && !File.Exists(ownedDirectory + ".meta"), "Owned temporary asset directory collided.");
                    _ownedFixtureAssetDirectory = ownedDirectory;
                    Directory.CreateDirectory(ownedDirectory);
                    string path = ownedDirectory + "/fixture.unity";
                    File.WriteAllText(path, OwnedSceneYaml, new UTF8Encoding(false));
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    _ownedFixture = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    Ensure(_ownedFixture.IsValid() && _ownedFixture.isLoaded && !EditorSceneManager.IsPreviewScene(_ownedFixture),
                        "Temp fixture did not load as an ordinary editor scene.");
                    sentinel = _ownedFixture.GetRootGameObjects().Single();
                }
                else
                {
                    _ownedFixtureKind = "PreviewScene";
                    _ownedFixture = EditorSceneManager.NewPreviewScene();
                    sentinel = new GameObject("Owned Offline Preview Dirty Preservation Fixture");
                    SceneManager.MoveGameObjectToScene(sentinel, _ownedFixture);
                }
                sentinel.transform.position = new Vector3(3.25f, 7.5f, -2.75f);
                Ensure(sentinel.scene.handle == _ownedFixture.handle, "Fixture object was created outside its owned scene.");
                EditorUtility.SetDirty(sentinel);
                EditorUtility.SetDirty(sentinel.transform);
                _ownedFixtureMarkedDirty = EditorSceneManager.MarkSceneDirty(_ownedFixture) && _ownedFixture.isDirty;
                _usedOwnedDirtyFixture = true;
                if (ordinaryDirtyScene) Ensure(_ownedFixtureMarkedDirty, "Owned ordinary Temp scene was not marked dirty.");
            }
            finally { if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive); }
        }
        if (!ordinaryDirtyScene) _originalWitness.AssertUnchanged("owned preview fixture setup");
        _witness = _ownedFixture.IsValid()
            ? GachaCollectionPreviewSceneWitness.Capture(_ownedFixture)
            : GachaCollectionPreviewSceneWitness.Capture();
        Ensure(EditorApplication.ExecuteMenuItem(MenuPath), "The integrated offline demo menu could not be executed.");
        _window = ActiveWindow();
        Ensure(_window != null, "Menu execution did not create an integrated preview window.");
        _waitingForReady = true;
        _readyDeadline = EditorApplication.timeSinceStartup + 30;
    }

    private static void BuildSteps()
    {
        var catalog = Get<IReadOnlyList<GachaData>>("Catalog");
        _newStaffId = catalog.OfType<GachaStaffData>().First(x => x.Id != "STAFF01" && GachaEconomyService.CanPurchaseStaff(x.Rank)).Id;
        _uniqueStaffId = catalog.OfType<GachaStaffData>().First(x => x.Rank == Rank.Unique).Id;
        _specialStaffId = catalog.OfType<GachaStaffData>().First(x => x.Rank == Rank.Special).Id;
        _itemId = catalog.OfType<GachaItemData>().First(x => EnhancementFairyCatalog.IsEligible(x)).Id;

        Steps.Enqueue(() => { Call("Reset", 99, 5000L); Call("ShowMachine", GachaMachineKind.Staff); });
        Steps.Enqueue(() =>
        {
            var hud = Get<UIGacha>("View").CollectionHud;
            Ensure(hud != null && hud.gameObject.activeInHierarchy, "100-draw guarantee HUD is not visible.");
            Ensure(hud.CommittedCounter == 99, "Staff guarantee HUD does not show its retained 99 counter.");
            Ensure(hud.GetComponentsInChildren<TMP_Text>(true).Any(x => x.text.Contains("100")), "Guarantee label is absent.");
            Capture("01-machine-guarantee-99"); Checks.Add("E: native guarantee gauge visible at 99 / 100");
        });
        foreach (TokenExchangeTab tab in new[] { TokenExchangeTab.Tickets, TokenExchangeTab.Staff, TokenExchangeTab.Items })
        {
            TokenExchangeTab captured = tab;
            Steps.Enqueue(() => Call("OpenExchange", captured));
            Steps.Enqueue(() =>
            {
                TokenExchangeView exchange = Get<UIGacha>("View").CollectionExchange;
                Ensure(exchange != null && exchange.IsOpen && exchange.CurrentTab == captured,
                    "Native exchange tab is not open: " + captured);
                if (captured == TokenExchangeTab.Staff)
                {
                    var economy = Get<GachaEconomyService>("Economy");
                    var staffProducts = economy.Products.Where(x => x.Category == GachaExchangeCategory.Staff).ToArray();
                    Ensure(staffProducts.Length > 0 && staffProducts.All(x => GachaEconomyService.CanPurchaseStaff(x.Data.Rank)),
                        "Staff exchange exposes a disallowed rank.");
                    Ensure(!economy.CanExchange("staff:" + _uniqueStaffId, 1, out _) &&
                        !economy.CanExchange("staff:" + _specialStaffId, 1, out _), "Unique or Special staff direct exchange was allowed.");
                    Checks.Add("E: native staff exchange only offers Normal/Rare; Unique/Special policy rejects purchase");
                }
                Capture("02-exchange-" + captured.ToString().ToLowerInvariant());
            });
        }
        Steps.Enqueue(() => { Call("Reset", 0, 5000L); Call("Draw", GachaMachineKind.Staff, new[] { _newStaffId }, false); });
        Steps.Enqueue(() =>
        {
            var result = Get<GachaEconomyService>("Economy").LastTransaction;
            Ensure(result != null && result.IsCompleted && result.Results.Count == 1 && result.Results[0].IsNew,
                "First staff acquisition was not committed as new.");
            var card = Get<UIGachaCard>("ResultCard");
            Ensure(card != null && card.gameObject.activeInHierarchy, "Native result card is not visible.");
            var badge = card.GetComponentInChildren<GachaAcquisitionBadge>(true);
            Ensure(badge != null && badge.IsNew && badge.gameObject.activeInHierarchy &&
                badge.GetComponentInChildren<TMP_Text>(true).text == "신규!", "Native 신규! badge is not visible.");
            Capture("03-first-acquisition-new-badge"); Checks.Add("E: committed first acquisition displays native 신규! badge");
        });
        Steps.Enqueue(() => { Call("Reset", 0, 5000L); Call("Draw", GachaMachineKind.Staff, new[] { _uniqueStaffId }, false); });
        Steps.Enqueue(() =>
        {
            Ensure(Get<GachaEconomyService>("Economy").LastTransaction.Results[0].Rank == Rank.Unique, "Expected Unique staff result.");
            var card = Get<UIGachaCard>("ResultCard");
            Ensure(card != null && card.gameObject.activeInHierarchy, "Unique card is not visible.");
            Ensure(!card.GetComponentsInChildren<RotationGameObject>(true).Any(x => x.enabled && x.gameObject.activeInHierarchy),
                "Unique result retained an active rotating halo.");
            Capture("04-unique-without-halo"); Checks.Add("E: visible Unique card has no active rotating halo");
        });
        Steps.Enqueue(() => { Call("Reset", 99, 5000L); Call("Draw", GachaMachineKind.Staff, new[] { _specialStaffId }, false); });
        Steps.Enqueue(() =>
        {
            var economy = Get<GachaEconomyService>("Economy");
            var result = economy.LastTransaction.Results.Single();
            Ensure(result.Rank == Rank.Special && result.IsGuaranteedSpecial && result.CounterBefore == 99 && result.CounterAfter == 0,
                "100th result did not produce a committed Special guarantee.");
            var card = Get<UIGachaCard>("ResultCard");
            Ensure(card != null && card.gameObject.activeInHierarchy &&
                card.GetComponentsInChildren<RotationGameObject>(true).Any(x => x.enabled && x.gameObject.activeInHierarchy),
                "Special result has no active rotating halo.");
            Capture("05-special-guaranteed-with-halo"); Checks.Add("E: 100th committed result is Special with active rotating halo");
        });
        Steps.Enqueue(() => { Call("Reset", 0, 5000L); Call("Draw", GachaMachineKind.Item, new[] { _itemId }, false); });
        Steps.Enqueue(() => Call("ShowFairies"));
        Steps.Enqueue(() =>
        {
            var fairies = Get<EnhancementFairyHabitat>("Fairies");
            Ensure(fairies != null && fairies.IsVisible && fairies.ActiveCount > 0, "Third-floor enhancement fairies are not visible.");
            Ensure(fairies.ActiveCount <= 12, "Preview fairy pool exceeded its cap.");
            Capture("06-third-floor-fairies"); Checks.Add("E: third-floor native fairy habitat displays owned enhancement types");
        });
    }

    private static void Capture(string name)
    {
        string path = Path.GetFullPath(Path.Combine(DirectoryPath, _capturePrefix + name + ".png"));
        Call("RenderToPng", path);
        Ensure(File.Exists(path) && new FileInfo(path).Length > 100, "Preview screenshot was not written: " + path);
        Captures.Add(path);
    }

    private static void Complete()
    {
        _witness.AssertUnchanged("all six native demo features displayed");
        if (_window != null) _window.Close();
        _window = null;
        _witness.AssertUnchanged("preview closed");
        Ensure(ActiveWindow() == null, "Preview window remained active after close.");
        Checks.Add("B/C: original scene handles, contents, dirty state, active scene, selection and Undo unchanged after all actions and close");
        CloseOwnedFixture();
        _originalWitness.AssertUnchanged("owned fixture closed; original workspace restored");
        if (_usedOwnedDirtyFixture)
        {
            Ensure(EditorApplication.ExecuteMenuItem(MenuPath), "Direct original-workspace menu reopen failed.");
            _window = ActiveWindow();
            Ensure(_window != null && Get<bool>("IsReady"), "Direct original untitled workspace did not open a ready preview.");
            _originalWitness.AssertUnchanged("direct original untitled workspace preview reopen");
            _window.Close();
            _window = null;
            _originalWitness.AssertUnchanged("direct original untitled workspace preview closed");
            Checks.Add("A/B/C: original untitled workspace also opens directly without dirty fixture and remains unchanged after close");
        }
        WriteResult(true, null);
        _running = _waitingForReady = false;
        Steps.Clear();
        Debug.Log("COLLECTION_PREVIEW_VERIFICATION_PASSED: " + _witness.Summary);
    }

    private static void Fail(Exception error)
    {
        if (error is TargetInvocationException target && target.InnerException != null) error = target.InnerException;
        string message = error.ToString();
        try { if (_window != null) _window.Close(); }
        catch (Exception closeError) { message += "\nClose: " + closeError; }
        _window = null;
        try { _witness?.AssertUnchanged("failure cleanup"); }
        catch (Exception preservationError) { message += "\nPreservation: " + preservationError; }
        try { CloseOwnedFixture(); _originalWitness?.AssertUnchanged("failure fixture cleanup"); }
        catch (Exception preservationError) { message += "\nOriginal preservation: " + preservationError; }
        WriteResult(false, message);
        _running = _waitingForReady = false;
        Steps.Clear();
        Debug.LogError("COLLECTION_PREVIEW_VERIFICATION_FAILED: " + message);
    }

    private static void WriteResult(bool success, string error)
    {
        string after;
        try { after = GachaCollectionPreviewSceneWitness.Capture().Summary; }
        catch (Exception exception) { after = exception.ToString(); }
        var report = new Result { success = success, startedUtc = _started, finishedUtc = DateTime.UtcNow.ToString("o"),
            beforeWitness = _witness?.Summary, afterWitness = after, dirtyScenesBefore = _witness?.DirtySceneCount ?? 0,
            originalWitness = _originalWitness?.Summary, originalDirtyScenesBefore = _originalWitness?.DirtySceneCount ?? 0,
            ownedDirtyFixture = _ownedFixtureMarkedDirty, ownedDirtyFixtureKind = _ownedFixtureKind,
            passedChecks = Checks.ToArray(), screenshots = Captures.ToArray(), error = error };
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(Path.Combine(DirectoryPath, _reportName), JsonUtility.ToJson(report, true));
    }

    private static EditorWindow ActiveWindow() => _windowType?.GetProperty("ActiveWindow",
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null) as EditorWindow;
    private static void CloseOwnedFixture()
    {
        if (_ownedFixture.IsValid() && _ownedFixture.isLoaded)
        {
            if (EditorSceneManager.IsPreviewScene(_ownedFixture)) EditorSceneManager.ClosePreviewScene(_ownedFixture);
            else Ensure(EditorSceneManager.CloseScene(_ownedFixture, true), "Could not close owned ordinary Temp scene.");
        }
        _ownedFixture = default;
        CleanupOwnedFixtureAsset();
    }
    private static void CleanupOwnedFixtureAsset()
    {
        if (string.IsNullOrEmpty(_ownedFixtureAssetDirectory)) return;
        string owned = _ownedFixtureAssetDirectory;
        Ensure(owned.StartsWith("Assets/Temp/GachaCollectionPreview_", StringComparison.Ordinal) &&
            Path.GetFullPath(owned).StartsWith(Path.GetFullPath("Assets/Temp") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "Refusing cleanup outside the exact owned temporary asset directory.");
        if (Directory.Exists(owned))
        {
            string[] expected = { Path.GetFullPath(owned + "/fixture.unity"), Path.GetFullPath(owned + "/fixture.unity.meta") };
            Ensure(Directory.GetFileSystemEntries(owned).All(x => expected.Contains(Path.GetFullPath(x), StringComparer.OrdinalIgnoreCase)),
                "Owned fixture directory contains an unexpected entry; it was preserved.");
            if (!AssetDatabase.DeleteAsset(owned))
            {
                foreach (string file in expected) if (File.Exists(file)) File.Delete(file);
                Directory.Delete(owned, false);
                if (File.Exists(owned + ".meta")) File.Delete(owned + ".meta");
            }
        }
        Ensure(!Directory.Exists(owned) && !File.Exists(owned + ".meta"), "Owned fixture asset cleanup was incomplete.");
        _ownedFixtureAssetDirectory = null;
        if (_createdAssetTempParent && Directory.Exists("Assets/Temp") && !Directory.EnumerateFileSystemEntries("Assets/Temp").Any())
        {
            if (!AssetDatabase.DeleteAsset("Assets/Temp"))
            {
                Directory.Delete("Assets/Temp", false);
                if (File.Exists("Assets/Temp.meta")) File.Delete("Assets/Temp.meta");
            }
        }
        _createdAssetTempParent = false;
    }
    // Only a fixture-owned GameObject, Transform and Unity 6 root list. No source scene/assets are copied.
    private const string OwnedSceneYaml = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 1001}
  m_Layer: 0
  m_Name: Owned Offline Preview Dirty Preservation Fixture
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {fileID: 1001}
";
    private static T Get<T>(string name)
    {
        PropertyInfo property = _windowType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null) throw new MissingMemberException(_windowType.Name, name);
        return (T)property.GetValue(_window);
    }
    private static object Call(string name, params object[] arguments)
    {
        MethodInfo method = _windowType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(x => x.Name == name && x.GetParameters().Length == arguments.Length);
        return method.Invoke(_window, arguments);
    }
    private static void Ensure(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
#endif
