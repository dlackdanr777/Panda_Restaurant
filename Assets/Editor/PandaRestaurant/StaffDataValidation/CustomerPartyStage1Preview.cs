#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Muks.BackEnd;
using Muks.PathFinding.AStar;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// This entry only plays a marked, disposable project copy. The source editor never
// changes scene, selection, Undo, Play state, account, or assets. No SDK initialization.
[InitializeOnLoad]
public static partial class CustomerPartyStage1Preview
{
    private const string Marker = "party-preview-owned.json";
    private const string Pending = "Panda.PartyPreview.Pending";
    private static Task copy;
    private static string copyPath;
    private static CustomerController controller;
    private static TableManager manager;
    private static FurnitureSystem furniture;
    private static Camera camera;
    private static CustomerVisitParty party;
    private static RenderTexture target;
    private static GachaCollectionVideoRecorder recorder;
    private static string output;
    private static int floor, phase;
    private static double began, readyAt, seatedAt;
    private static bool finishing;
    private static readonly List<string> observations = new List<string>();

    static CustomerPartyStage1Preview()
    {
        EditorApplication.playModeStateChanged += PlayChanged;
        if (SessionState.GetBool(Pending, false)) EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Panda Restaurant/Customer Parties/Open isolated Stage1 preview")]
    public static void OpenCopy()
    {
        if (copy != null && !copy.IsCompleted) return;
        string source = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        copyPath = Path.Combine(Path.GetTempPath(), "PandaPartyPreview", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        string destination = copyPath;
        copy = Task.Run(() =>
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, Marker), "{\"owned\":true,\"purpose\":\"PHASE2 isolated Stage1 preview\"}");
            foreach (string folder in new[] { "Assets", "Packages", "ProjectSettings" })
                CopyFolder(Path.Combine(source, folder), Path.Combine(destination, folder));
            string cache = Path.Combine(source, "Library", "PackageCache");
            if (Directory.Exists(cache)) CopyFolder(cache, Path.Combine(destination, "Library", "PackageCache"));
            string encoder = Directory.GetFiles(Path.Combine(source, "Logs"), "ffmpeg*.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (encoder != null) File.WriteAllText(Path.Combine(destination, "party-preview-encoder.txt"), encoder);
        });
        EditorApplication.update += FinishCopy;
        UnityEngine.Debug.Log("Copying an isolated party preview project to " + destination + ". The current editor remains unchanged.");
    }

    private static void CopyFolder(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
        foreach (string folder in Directory.GetDirectories(source)) CopyFolder(folder, Path.Combine(destination, Path.GetFileName(folder)));
    }
    private static void FinishCopy()
    {
        if (!copy.IsCompleted) return;
        EditorApplication.update -= FinishCopy;
        if (copy.IsFaulted) { UnityEngine.Debug.LogException(copy.Exception); return; }
        Process.Start(new ProcessStartInfo(EditorApplication.applicationPath,
            "-projectPath \"" + copyPath + "\" -executeMethod CustomerPartyStage1Preview.Start -logFile \"" + Path.Combine(copyPath, "party-preview.log") + "\"") { UseShellExecute = false });
    }

    // Command-line entry is also guarded: it can never switch or save a user's source scene.
    public static void Start()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!File.Exists(Path.Combine(root, Marker)) || Directory.Exists(Path.Combine(root, ".git")) || File.Exists(Path.Combine(root, ".git")))
            throw new InvalidOperationException("Party preview requires its owned project copy.");
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Preview already playing");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage1.unity", OpenSceneMode.Single);
        foreach (var item in scene.GetRootGameObjects()) item.SetActive(false);
        // Separate authored services from geometry; their original serialized references
        // and the original collider/map/chair assets remain intact in this owned copy.
        var m = CopyService(Find<TableManager>(scene));
        var c = CopyService(Find<CustomerController>(scene));
        var f = CopyService(Find<FurnitureSystem>(scene));
        CopyService(Find<AStar>(scene));
        if (IsServicePreview)
        {
            CopyService(Find<StaffController>(scene));
            if(IsCompletionPreview) Set(m,"_feverSystem",CopyService(Find<FeverSystem>(scene)));
            // StaffGroup binds the authored marketer image even for an unequipped slot.
            // Preserve this reference chain, but never activate the customer-call UI.
            var customerUi = CopyService(Get<UIAddCutomerController>(c,"_uiController"));
            Set(c,"_uiController",customerUi);
        }
        var k = CopyService(Find<KitchenSystem>(scene)); Set(m, "_kitchenSystem", k);
        Set(c, "_tableManager", m); Set(m, "_customerController", c); Set(m, "_furnitureSystem", f); Set(f, "_tableManager", m);
        var scripts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(s => s != null).ToArray();
        foreach (var script in scripts) script.enabled = false;
        foreach (var script in scripts.Reverse())
        {
            if (script != null && !Retain(script)) Object.DestroyImmediate(script);
        }
        var remaining = scripts.Where(s => s != null && !Retain(s)).ToArray();
        foreach (var script in remaining.Reverse()) Object.DestroyImmediate(script);
        Require(!scripts.Any(s => s != null && !Retain(s)), "Unsafe scene script remains; Play is refused");
        foreach (var canvas in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true))) canvas.gameObject.SetActive(false);
        EditorSceneManager.SaveScene(scene, "Assets/PartyPreviewOwned.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    private static bool Retain(MonoBehaviour script) => script is CustomerController || script is TableManager || script is FurnitureSystem
        || (IsServicePreview && (script is StaffController || script is StaffGroup || script is UIAddCutomerController || script is UIMarketerImage)) || script is AStar || script is FurnitureGroup || script is Furniture || script is TableData
        || (IsCompletionPreview && (script is FeverSystem || script is UIFever || script is UITweenFillAmountImage || script is UIButtonAndPressEffect || script is ButtonPressEffect)) || script is DropCoinArea || script is DropGarbageArea || script is FoodDistributionCounter
        || script is KitchenSystem || script is KitchenUtensilGroup || script is KitchenUtensil
        || script is SpriteTouchEvent || script is SinkGaugeBar || script is SpriteFillAmount
        || script is WorldToSceenPosition || script is UIBurnerTimer || script is UIImageFillAmount || script is TableButton
        || script is PointerDownSpriteRenderer || script is SatisfactionSystem || script is FloorLocker || script is FloorLockGroup
        || script.GetType().Namespace?.StartsWith("UnityEngine.") == true
        || script.GetType().Namespace?.StartsWith("TMPro") == true;

    private static T CopyService<T>(T source) where T : MonoBehaviour
    {
        var go = new GameObject("Party preview " + typeof(T).Name); go.SetActive(false);
        T result = go.AddComponent<T>(); EditorUtility.CopySerialized(source, result);
        Object.DestroyImmediate(source); return result;
    }
    private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).First();
    private static FieldInfo Field(Type type, string name)
    {
        for (; type != null; type = type.BaseType)
        { var value = type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); if (value != null) return value; }
        throw new MissingFieldException(name);
    }
    private static void Set(object obj, string field, object value) => Field(obj.GetType(), field).SetValue(obj, value);
    private static T Get<T>(object obj, string field) => (T)Field(obj.GetType(), field).GetValue(obj);
    private static void Static(Type type, string field, object value) => Field(type, field).SetValue(null, value);
    private static void CallStatic(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    private static T Service<T>(GameObject root) where T : Component => root.AddComponent<T>();
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }

    private static void PlayChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        { try { Initialize(); } catch (Exception error) { Fail(error); } }
        if (state == PlayModeStateChange.ExitingPlayMode) { Cleanup(); SessionState.SetBool(Pending, false); }
    }
    private static void Initialize()
    {
        Require(!BackEnd.Backend.IsInitialized && !BackEnd.Backend.IsLogin && Field(typeof(BackendManager), "_instance").GetValue(null) == null, "Backend must remain offline");
        Time.timeScale = 1; Application.runInBackground = true;
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        output = Environment.GetEnvironmentVariable("PANDA_PARTY_VIDEO_DIR") ?? Path.Combine(root, "PartyEvidence");
        Directory.CreateDirectory(output);
        var scene = SceneManager.GetActiveScene();
        controller = Find<CustomerController>(scene); manager = Find<TableManager>(scene); furniture = Find<FurnitureSystem>(scene);
        var astar = Find<AStar>(scene); camera = Find<Camera>(scene);
        var services = new GameObject("Offline memory services"); services.SetActive(false);
        Static(typeof(UserInfo), "_stageInfos", new[] { new StageInfo(), new StageInfo(), new StageInfo() });
        if (IsCompletionPreview)
            foreach (var stage in (StageInfo[])Field(typeof(UserInfo), "_stageInfos").GetValue(null))
                stage.OnChangeSinkBowlHandler += () => CallStatic(typeof(UserInfo), "OnAddSinkBowlEvent");
        Static(typeof(UserInfo), "_currentStage", EStage.Stage1);
        UserInfo.IsTutorialStart = false; UserInfo.IsFirstTutorialClear = true;
        Static(typeof(GameManager), "_instance", Service<GameManager>(services));
        var sound = Service<SoundManager>(services); Static(typeof(SoundManager), "_instance", sound); Set(sound, "_masterVolume", 0f);
        Static(typeof(TimeManager), "_instance", Service<TimeManager>(services));
        Static(typeof(CustomerDataManager), "_instance", Service<CustomerDataManager>(services));
        CallStatic(typeof(CustomerDataManager), "LoadNormalCustomerSprites");
        CallStatic(typeof(CustomerDataManager), "LoadCustomerThumbnailSprites");
        CallStatic(typeof(CustomerDataManager), "NormalCustomerDataParse", "CustomerData/CSVData/NormalCustomerData");
        var skins = Service<SkinDataManager>(services); Static(typeof(SkinDataManager), "_instance", skins);
        CallStatic(typeof(SkinDataManager), "LoadCustomerSkinSprites");
        typeof(SkinDataManager).GetMethod("SkinCustomerDataParse", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(skins, new object[] { "CustomerData/CSVData/Skin/CustomerSkinDataList" });
        Static(typeof(UserInfo), "_enabledCustomerDic", new Dictionary<string, SaveCustomerData> {
            ["CUSTOMER01"] = new SaveCustomerData("CUSTOMER01", "", 0), ["CUSTOMER04"] = new SaveCustomerData("CUSTOMER04", "", 0) });
        InitializeOrderData(services);
        if (IsServicePreview) InitializeServiceMemory(services);
        Static(typeof(ObjectPoolManager), "_instance", Service<ObjectPoolManager>(services));
        if(IsCompletionPreview)
        {
            CallStatic(typeof(ObjectPoolManager),"CoinPooling"); CallStatic(typeof(ObjectPoolManager),"GarbagePooling");
            foreach(var field in new[]{"_coinParent","_garbageParent"})
                ((GameObject)Field(typeof(ObjectPoolManager),field).GetValue(null)).transform.SetParent(null,true);
        }
        Static(typeof(ObjectPoolManager), "_customerParent", new GameObject("Preview customer pool"));
        Static(typeof(ObjectPoolManager), "_normalCustomerPool", new Queue<NormalCustomer>());
        Static(typeof(ObjectPoolManager), "_normalCustomerPrefab", Resources.Load<GameObject>("ObjectPool/NormalCustomer").GetComponent<NormalCustomer>());
        foreach (var go in scene.GetRootGameObjects())
            if (go != services && go != controller.gameObject && go != manager.gameObject && go != furniture.gameObject && go != astar.gameObject && go != Find<KitchenSystem>(scene).gameObject
                && go.GetComponent<FeverSystem>() == null && go.GetComponent<UIAddCutomerController>() == null && (!IsServicePreview || go != Find<StaffController>(scene).gameObject)) go.SetActive(true);
        Physics2D.SyncTransforms(); astar.enabled = true; astar.gameObject.SetActive(true);
        Require(AStar.Instance == astar, "Authored AStar must own pathfinding");
        foreach (var locks in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FloorLockGroup>(true)))
            if (UserInfo.IsFloorValid(UserInfo.CurrentStage, Get<ERestaurantFloorType>(locks, "_floorType")))
                typeof(FloorLockGroup).GetMethod("CheckFloorLock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(locks, null);
        camera.orthographic = true; camera.orthographicSize = 14; camera.aspect = 16f / 9;
        camera.transform.position = new Vector3(3, 12, -20);
        if (target == null) { target = new RenderTexture(1280, 720, 24); target.Create(); camera.targetTexture = target; }
        camera.enabled = true;
        if (OrderMode != 0) InitializeOrderViewport(scene);
        var groups = Get<FurnitureGroup[]>(furniture, "_furnitureGroups");
        Set(furniture, "_furnitureGroupDic", groups.ToDictionary(g => g.FloorType, g => g));
        foreach (var group in groups) group.Init(manager, Get<RectTransform>(furniture, "_buttonParent"), Get<TableButton>(furniture, "_orderButtonPrefab"), Get<TableButton>(furniture, "_servingButtonPrefab"));
        // CopySerialized retained the source Inspector layout. Never replace it with
        // fixture coordinates; changes made in the isolated Inspector also persist.
        controller.enabled = true; controller.gameObject.SetActive(true);
        manager.enabled = true; manager.gameObject.SetActive(true);
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 14; camera.aspect = 16f / 9;
        camera.transform.position = new Vector3(3, 12, -20);
        if (target == null) { target = new RenderTexture(1280, 720, 24); target.Create(); camera.targetTexture = target; }
        if (OrderMode != 0) InitializeOrderKitchen(scene);
        if (IsServicePreview) InitializeServiceStaff(scene,services);
        began = EditorApplication.timeSinceStartup; floor = 0; phase = 0; finishing = false;
        if (IsCompletionPreview && Environment.GetEnvironmentVariable("PANDA_PARTY_SINGLE_FLOOR") == "VIP") floor = 1;
        UnityEngine.Debug.Log("PARTY_PREVIEW_READY: real Stage1 colliders, original sprites, original player loop; backend offline");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || controller == null || finishing) return;
        try
        {
            Require(!BackEnd.Backend.IsInitialized && !BackEnd.Backend.IsLogin && Field(typeof(BackendManager), "_instance").GetValue(null) == null, "Unexpected backend activation");
            double now = EditorApplication.timeSinceStartup;
            if (now - began > (OrderMode == 0 ? 95 : 240)) throw new TimeoutException("Actual movement did not finish within the preview budget");
            if (phase == 0 && now - began > 1)
            {
                // Editor updates may run before the first PlayerLoop Start. Wait for
                // production restore to finish instead of racing it with fixture spawn.
                if (controller.WaitingLoadDiagnostic == null) return;
                Require(controller.WaitingLoadDiagnostic == "Restored", controller.WaitingLoadDiagnostic);
                if (IsServicePreview) ResetServiceObservation();
                BeginVideo();
                if (floor == 1) ((Dictionary<string, SaveCustomerData>)Field(typeof(UserInfo), "_enabledCustomerDic").GetValue(null))["CUSTOMER01"]
                    = new SaveCustomerData("CUSTOMER01", "SKIN_CUSTOMER01", 0);
                Require(controller.TrySpawnWaitingParty(new[] { "CUSTOMER01", OrderMode == 0 ? "CUSTOMER04" : "CUSTOMER02" }, out party), controller.LastVisitDiagnostic);
                if(IsCompletionPreview) PrepareCompletionParty();
                else Require(controller.TrySpawnWaitingParty(new[] { "CUSTOMER04" }, out _), controller.LastVisitDiagnostic);
                phase = 1;
            }
            if (phase == 1 && controller.FirstReadyParty == party) { readyAt = now; phase = 2; }
            if (phase == 2 && now - readyAt > 3)
            {
                Require(manager.OnCustomerGuideEvent((ERestaurantFloorType)floor), "Joint guide rejected"); phase = 3;
            }
            if (party != null)
            {
                foreach (var member in party.Members) Require(!member.Customer.HasPathFailure, "Real pathfinding failed");
                if (floor == 1 && phase >= 3 && (!IsCompletionPreview || !departureObserved))
                {
                    float y = party.Members.Average(m => m.Customer.transform.position.y) + 4;
                    var p = camera.transform.position; p.y = Mathf.Lerp(p.y, y, .06f); camera.transform.position = p;
                }
                observations.Add(now.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "," + floor + "," + phase + "," +
                    string.Join(";", party.Members.Select(m => m.Customer.transform.position.ToString("F3"))) + "," + (party.CurrentTable?.TableState.ToString() ?? "Waiting"));
            }
            if (phase == 3 && party.CurrentTable?.TableState == ETableState.PartySeatedAwaitingOrders)
            { seatedAt = now; phase = 4; SaveFrame("floor" + (floor + 1) + "-seated.png"); }
            RenderPreview(); recorder?.Capture(target, now);
            if (IsCompletionPreview) { TickCompletion(now); return; }
            if (IsServicePreview) { TickService(now); return; }
            if (OrderMode != 0) { TickOrders(now); return; }
            if (phase == 4 && now - seatedAt > 5) { recorder?.Stop(); phase = 5; }
            if (phase == 5 && (recorder == null || recorder.IsFinished))
            {
                recorder?.PollCompletion(); recorder = null;
                string video = Path.Combine(output, floor == 0 ? "floor1-real-time" : "vip-real-time");
                var evidence = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(video + ".json"));
                Require(File.Exists(video + ".mp4") && (int)evidence["encoderExitCode"] == 0
                    && string.IsNullOrEmpty((string)evidence["error"]) && (int)evidence["capturedFrames"] > 1, "Video encoding failed");
                manager.CancelPartyGuide(party.CurrentTable);
                foreach (var waiting in controller.WaitingParties.ToArray()) controller.CancelWaitingParty(waiting);
                if (floor == 0) { floor = 1; phase = 0; began = now; camera.transform.position = new Vector3(3, 12, -20); }
                else
                {
                    File.WriteAllLines(Path.Combine(output, "actual-movement.csv"), observations);
                    File.WriteAllText(Path.Combine(output, "completed.json"), "{\"floors\":2,\"timeScale\":1,\"backendInitialized\":false,\"method\":\"Production AStar MoveRoutine and Tween, actual PlayerLoop\"}");
                    Stop();
                }
            }
        }
        catch (Exception error) { Fail(error); }
    }
    private static void BeginVideo()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string encoder = Environment.GetEnvironmentVariable("PANDA_PARTY_ENCODER");
        if (string.IsNullOrEmpty(encoder) && File.Exists(Path.Combine(root, "party-preview-encoder.txt"))) encoder = File.ReadAllText(Path.Combine(root, "party-preview-encoder.txt"));
        Require(File.Exists(encoder), "An FFmpeg encoder is required for real-time evidence");
        recorder = new GachaCollectionVideoRecorder(encoder, Path.Combine(output, IsServicePreview ? (floor==0 ? "floor1-service.mp4" : "vip-service.mp4") : OrderMode == 0 ? (floor == 0 ? "floor1-real-time.mp4" : "vip-real-time.mp4") : "orders-real-time.mp4"), target.width, target.height, OrderMode == 0 ? 90 : 210, false);
    }
    private static void SaveFrame(string name)
    {
        RenderPreview(); var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
        File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG()); Object.DestroyImmediate(texture); RenderTexture.active = previous;
    }
    private static void Fail(Exception error)
    {
        UnityEngine.Debug.LogException(error);
        if (!string.IsNullOrEmpty(output)) File.WriteAllText(Path.Combine(output, "failed.txt"), error.ToString());
        Stop();
    }
    [MenuItem("Tools/Panda Restaurant/Customer Parties/Stop isolated preview")]
    public static void Stop()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        finishing = true; Cleanup(); SessionState.SetBool(Pending, false);
        EditorApplication.update -= Tick; EditorApplication.ExitPlaymode();
        if (Environment.GetEnvironmentVariable("PANDA_PARTY_EXIT") == "1") EditorApplication.delayCall += () => EditorApplication.Exit(!string.IsNullOrEmpty(output) && File.Exists(Path.Combine(output, "completed.json")) ? 0 : 1);
    }
    private static void Cleanup()
    {
        recorder?.Dispose(); recorder = null;
        CleanupService();
        CleanupOrders();
        if (controller != null) typeof(CustomerController).GetMethod("EndVisitParties", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
        if (camera != null) camera.targetTexture = null;
        if (target != null) { target.Release(); Object.DestroyImmediate(target); target = null; }
    }
}
#endif
