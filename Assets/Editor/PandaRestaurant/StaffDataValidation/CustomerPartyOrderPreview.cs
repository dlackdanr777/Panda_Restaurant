#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static partial class CustomerPartyStage1Preview
{
    private const string OrderModeKey = "Panda.PartyPreview.OrderMode";
    private static int OrderMode => SessionState.GetInt(OrderModeKey, 0);
    private static Camera orderCamera;
    private static Canvas orderCanvas;
    private static EventSystem orderEvents;
    private static GraphicRaycaster orderRaycaster;
    private static CustomerPartyOrderPreviewWindow orderWindow;
    private static PartyOrderBatch observedBatch;
    private static TableData observedTable;
    private static int mouseClicks;
    private static double completedAt;
    private static double clickedAt;
    private static bool reloadLocked;
    internal static RenderTexture OrderTexture => target;
    internal static string OrderStatus => "Offline memory / timeScale=1 / clicks=" + mouseClicks
        + " / satisfaction=" + UserInfo.GetSatisfaction(UserInfo.CurrentStage)
        + " / cooked=" + UserInfo.TotalCookCount + " / "
        + (observedBatch == null ? "Joint arrival" : observedBatch.State + " : " + string.Join(", ", observedBatch.Orders.Select(o => o.Food.Id + "=" + o.State)));

    [MenuItem("Tools/Panda Restaurant/Customer Parties/Open existing isolated Phase3A project")]
    public static void OpenExistingOrderProject()
    {
        string path = EditorUtility.OpenFolderPanel("Select the existing owned party preview project", "", "");
        if (string.IsNullOrEmpty(path)) return;
        if (!File.Exists(Path.Combine(path,Marker)) || Directory.Exists(Path.Combine(path,".git")) || File.Exists(Path.Combine(path,".git")))
            throw new InvalidOperationException("Select the existing owned preview project, not the source checkout.");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(EditorApplication.applicationPath,
            "-projectPath \""+path+"\" -executeMethod CustomerPartyStage1Preview.StartOrdersSuccess") { UseShellExecute = false });
    }

    [MenuItem("Tools/Panda Restaurant/Customer Parties/Run Phase3A success (owned copy only)")]
    public static void StartOrdersSuccess() { StartOrderMode(1); }
    [MenuItem("Tools/Panda Restaurant/Customer Parties/Run Phase3A rejection (owned copy only)")]
    public static void StartOrdersRejection() { StartOrderMode(2); }
    private static void StartOrderMode(int mode)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        if (!File.Exists(Path.Combine(root,Marker)) || Directory.Exists(Path.Combine(root,".git")) || File.Exists(Path.Combine(root,".git")))
            throw new InvalidOperationException("Orders require the existing owned preview project.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Pending,false))
            throw new InvalidOperationException("Stop the current isolated preview before changing its scenario.");
        SessionState.SetInt(OrderModeKey,mode); Start();
    }

    private static void InitializeOrderData(GameObject services)
    {
        var foods = Service<FoodDataManager>(services); Static(typeof(FoodDataManager), "_instance", foods);
        typeof(FoodDataManager).GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(foods, null);
        var zeroFood = Enum.GetValues(typeof(FoodType)).Cast<FoodType>().ToDictionary(t => t, t => 0f);
        Set(GameManager.Instance, "_addGachaItemFoodPriceMulDic", new Dictionary<FoodType, float>(zeroFood));
        Set(GameManager.Instance, "_addGachaItemFoodCookingTimeMulDic", new Dictionary<FoodType, float>(zeroFood));
        var recipes = new Dictionary<string, int> { ["FOOD01"] = 1, ["FOOD13"] = 1 };
        if (OrderMode != 2) recipes["FOOD04"] = 1;
        Static(typeof(UserInfo), "_giveRecipeLevelDic", recipes);
        ((Dictionary<string, SaveCustomerData>)Field(typeof(UserInfo), "_enabledCustomerDic").GetValue(null))["CUSTOMER02"] = new SaveCustomerData("CUSTOMER02", "", 0);
        mouseClicks = 0; observedBatch = null; observedTable = null; completedAt = 0;
    }

    private static void InitializeOrderViewport(Scene scene)
    {
        // Render the authored UI in screen-pixel world coordinates. Only Canvas/camera/input
        // infrastructure is created; every menu/burner visual is its existing saved prefab.
        orderCamera = new GameObject("Owned order UI camera").AddComponent<Camera>();
        orderCamera.orthographic = true; orderCamera.orthographicSize = 360; orderCamera.aspect = 16f/9;
        orderCamera.transform.position = new Vector3(640, 360, -100);
        orderCamera.clearFlags = CameraClearFlags.Nothing; orderCamera.allowMSAA = false; orderCamera.cullingMask = 1 << 5;
        orderCamera.targetTexture = target; orderCamera.enabled = false;
        orderCanvas = new GameObject("Owned authored UI viewport", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        orderCanvas.renderMode = RenderMode.WorldSpace; orderCanvas.worldCamera = orderCamera;
        ((RectTransform)orderCanvas.transform).sizeDelta = new Vector2(1280,720);
        orderRaycaster = orderCanvas.GetComponent<GraphicRaycaster>();
        orderEvents = new GameObject("Owned preview pointer input").AddComponent<EventSystem>();
        camera.cullingMask &= ~(1 << 5);
        MoveUiParent(Get<RectTransform>(furniture, "_buttonParent"));
        foreach (var group in Get<KitchenUtensilGroup[]>(Find<KitchenSystem>(scene), "_kitchenUtensilGroups"))
            MoveUiParent(Get<RectTransform>(group, "_burnerTimerParent"));
        orderWindow = EditorWindow.GetWindow<CustomerPartyOrderPreviewWindow>(true, "Phase3A isolated orders");
        orderWindow.position = new Rect(60, 70, 1280, 780); orderWindow.Show(); orderWindow.Focus();
    }
    private static void MoveUiParent(RectTransform parent)
    {
        parent.SetParent(orderCanvas.transform, false); parent.gameObject.SetActive(true);
        parent.localScale = Vector3.one; parent.localPosition = Vector3.zero;
    }
    private static void InitializeOrderKitchen(Scene scene)
    {
        camera.enabled = true; // Prefab Awake captures the authored MainCamera.
        var kitchen = Find<KitchenSystem>(scene); kitchen.enabled = true; kitchen.gameObject.SetActive(true);
        foreach (var group in Get<KitchenUtensilGroup[]>(kitchen, "_kitchenUtensilGroups"))
        {
            group.enabled = true;
            foreach (var burner in group.GetComponentsInChildren<BurnerKitchenUtensil>(true)) burner.enabled = true;
            if (IsCompletionPreview)
                foreach (var sink in group.GetComponentsInChildren<SinkKitchenUtensil>(true))
                    Get<TMPro.TextMeshPro>(Get<SinkGaugeBar>(sink, "_sinkGaugeBar"), "_tmp").enabled = true;
        }
        foreach (var child in orderCanvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        foreach (var image in orderCanvas.GetComponentsInChildren<Graphic>(true)) image.enabled = true;
        camera.enabled = false;
        // Some installed Editor resolvers schedule define changes on startup. The disposable
        // run must keep its memory-only account and callbacks in one domain until cleanup.
        EditorApplication.LockReloadAssemblies(); reloadLocked = true;
    }
    private static void RenderPreview()
    {
        if (IsCompletionPreview) DrawCompletionEvidence();
        camera.Render();
        if (orderCamera != null) { Canvas.ForceUpdateCanvases(); orderCamera.Render(); orderWindow?.Repaint(); }
    }
    internal static GameObject RouteOrderPointer(Vector2 point, GameObject down, bool mouseDown)
    {
        if (orderEvents == null || orderRaycaster == null) return null;
        var pointer = new PointerEventData(orderEvents) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); orderRaycaster.Raycast(pointer, hits);
        var hit = hits.OrderByDescending(h => h.depth).FirstOrDefault().gameObject;
        if (mouseDown)
        {
            if (hit != null) ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            return hit;
        }
        if (down != null) ExecuteEvents.ExecuteHierarchy(down, pointer, ExecuteEvents.pointerUpHandler);
        if (hit != null && hit == down)
        {
            mouseClicks++; clickedAt = EditorApplication.timeSinceStartup;
            observations.Add("MOUSE," + EditorApplication.timeSinceStartup.ToString("F3") + "," + point + "," + hit.name);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
        }
        return null;
    }
    private static void TickOrders(double now)
    {
        if (phase < 6 && now-began > 195) throw new TimeoutException("Order evidence must finish before recording expires.");
        if (phase == 4 && observedBatch == null)
        {
            observedTable = party.CurrentTable; observedBatch = observedTable.PartyOrders;
            Require(observedBatch != null && observedBatch.Orders.Count == 2, "Both initial menus must be prepared by production code");
            Require(observedTable.OrderButton.gameObject.activeInHierarchy && observedTable.CompanionOrderButton.gameObject.activeInHierarchy, "Both authored bubbles must be visible");
            SaveFrame("two-menus.png");
            File.WriteAllText(Path.Combine(output, "awaiting-mouse.txt"), OrderStatus);
        }
        if (observedBatch != null)
        {
            if (OrderMode == 1 && observedBatch.State == PartyOrderBatchState.Accepted && now-clickedAt > 2)
            {
                var kitchen = Find<KitchenSystem>(SceneManager.GetActiveScene());
                var group = Get<KitchenUtensilGroup[]>(kitchen, "_kitchenUtensilGroups").First(g => g.FloorType == observedTable.FloorType);
                var burner = group.GetComponentsInChildren<BurnerKitchenUtensil>(true).First(b => b.Type == KitchenUtensilType.Burner1);
                var view = phase == 6 ? new Vector3(3,12,-20) : new Vector3(burner.transform.position.x, burner.transform.position.y+4,-20);
                camera.transform.position = Vector3.Lerp(camera.transform.position,view,.12f);
            }
            observations.Add("ORDER," + now.ToString("F3") + "," + OrderStatus);
            if (phase == 4 && ((OrderMode == 1 && observedBatch.Orders.All(o => o.State == PartyFoodState.ReadyForServing))
                || (OrderMode == 2 && observedBatch.State == PartyOrderBatchState.Rejected && observedTable.CurrentParty == null)))
            {
                Require(mouseClicks == 1, "Exactly one actual pointer click is required for this evidence run");
                Require(UserInfo.TotalCookCount == (OrderMode == 1 ? 2 : 0), "Cooking total does not match the scenario");
                Require(UserInfo.GetSatisfaction(EStage.Stage1) == (OrderMode == 1 ? 0 : -10), "Unexpected recipe rejection satisfaction");
                Require(UserInfo.TotalCumulativeCustomerCount == 0, "No meal visits may be rewarded");
                completedAt = now; phase = 6; SaveFrame("result.png");
            }
        }
        if (phase == 6 && now-completedAt > 4) { recorder?.Stop(); phase = 7; }
        if (phase != 7 || (recorder != null && !recorder.IsFinished)) return;
        recorder?.PollCompletion(); recorder = null;
        var video = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(output, "orders-real-time.json")));
        Require((int)video["encoderExitCode"] == 0 && string.IsNullOrEmpty((string)video["error"]), "Video encoding failed");
        File.WriteAllLines(Path.Combine(output, "actual-order-playerloop.csv"), observations);
        File.WriteAllText(Path.Combine(output, "completed.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new {
            scenario = OrderMode == 1 ? "success" : "rejection", clicks = mouseClicks, timeScale = Time.timeScale,
            cooked = UserInfo.TotalCookCount, satisfaction = UserInfo.GetSatisfaction(EStage.Stage1),
            orders = observedBatch.Orders.Select(o => new { food = o.Food.Id, customer = o.Member.Customer.NormalCustomerData.Id, seat = o.SeatIndex, state = o.State.ToString() }),
            backendInitialized = BackEnd.Backend.IsInitialized, method = "Actual PlayerLoop, existing AStar/Tween/Kitchen Update, OS mouse to authored Button"
        }, Newtonsoft.Json.Formatting.Indented));
        Stop();
    }
    private static void CleanupOrders()
    {
        foreach (var group in Object.FindObjectsByType<KitchenUtensilGroup>(FindObjectsInactive.Include,FindObjectsSortMode.None)) group.enabled = false;
        foreach (var burner in Object.FindObjectsByType<BurnerKitchenUtensil>(FindObjectsInactive.Include,FindObjectsSortMode.None)) burner.enabled = false;
        if (observedTable != null && observedTable.CurrentParty != null) manager.CancelPartyGuide(observedTable);
        if (orderCamera != null) Object.DestroyImmediate(orderCamera.gameObject);
        if (orderCanvas != null) Object.DestroyImmediate(orderCanvas.gameObject);
        if (orderEvents != null) Object.DestroyImmediate(orderEvents.gameObject);
        if (orderWindow != null) orderWindow.Close();
        orderCamera = null; orderCanvas = null; orderEvents = null; orderWindow = null;
        observedBatch = null; observedTable = null; SessionState.SetInt(OrderModeKey, 0);
        if (reloadLocked) { reloadLocked = false; EditorApplication.UnlockReloadAssemblies(); }
    }
}

public sealed class CustomerPartyOrderPreviewWindow : EditorWindow
{
    private GameObject down;
    private void OnGUI()
    {
        if (GUILayout.Button("Stop isolated preview", EditorStyles.toolbarButton)) CustomerPartyStage1Preview.Stop();
        if (!EditorApplication.isPlaying || CustomerPartyStage1Preview.OrderTexture == null) return;
        EditorGUILayout.LabelField(CustomerPartyStage1Preview.OrderStatus, EditorStyles.miniLabel);
        var texture = CustomerPartyStage1Preview.OrderTexture;
        Rect available = GUILayoutUtility.GetRect(1,10000,1,10000);
        float width = Mathf.Min(available.width, available.height * 16/9);
        var rect = new Rect(available.x+(available.width-width)/2,available.y,width,width*9/16);
        GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill,false);
        var evt = Event.current;
        if (evt.button != 0) return;
        if (!rect.Contains(evt.mousePosition))
        {
            if (evt.type == EventType.MouseUp) { CustomerPartyStage1Preview.RouteOrderPointer(new Vector2(-1,-1),down,false); down = null; }
            return;
        }
        if (evt.type != EventType.MouseDown && evt.type != EventType.MouseUp) return;
        var point = new Vector2((evt.mousePosition.x-rect.x)/rect.width*texture.width,
            (1-(evt.mousePosition.y-rect.y)/rect.height)*texture.height);
        down = CustomerPartyStage1Preview.RouteOrderPointer(point,down,evt.type == EventType.MouseDown);
        evt.Use();
    }
    private void OnLostFocus() { CustomerPartyStage1Preview.RouteOrderPointer(new Vector2(-1,-1),down,false); down = null; }
}
#endif
