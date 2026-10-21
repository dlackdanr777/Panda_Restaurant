#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Muks.BackEnd;
using Muks.PathFinding.AStar;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Manual Play in this Editor. No project/scene copy, source scene edit/save,
// automated game input, forced arrival, recorder or external Editor process.
[InitializeOnLoad]
public sealed partial class CustomerPartyStage1Preview : EditorWindow
{
    internal const string ScenePath = "Assets/Scenes/Stage1.unity";
    private const string PreviousStart = "Panda.PartyManual.PreviousStartScene", ModeKey = "Panda.PartyManual.UnownedMenu", FloorKey = "Panda.PartyManual.Floor";
    private const string ManagerWitnessKey = "Panda.PartyManual.ManagerWitness";
    private const string EncounterPrepKey = "Panda.PartyManual.EncounterPrep";
    private const string EncounterCustomerKey = "Panda.PartyManual.EncounterCustomer";
    private const string EncounterVisitsKey = "Panda.PartyManual.EncounterVisits";
    private static readonly string[] EncounterCustomerIds =
    {
        "CUSTOMER01", "CUSTOMER02", "CUSTOMER03", "CUSTOMER04", "CUSTOMER05", "CUSTOMER06",
        "CUSTOMER07", "CUSTOMER08", "CUSTOMER09", "CUSTOMER10", "CUSTOMER11"
    };
    private static readonly string[] EncounterCustomerNames =
    {
        "파니", "랑이", "여리", "밀키", "토식이", "치키", "고돌이", "펭리", "이삭", "리오", "울리"
    };
    private static readonly int[] EncounterVisitOptions = { 0, 24, 49 };
    private static CustomerPartyOwnedMemory memory;
    private static CustomerController controller;
    private static TableManager tables;
    private static CustomerPartySettings settings;
    private static readonly Dictionary<string, CustomerData> originalCustomers = new Dictionary<string, CustomerData>();
    private static readonly List<CustomerData> runtimeCustomers = new List<CustomerData>();
    private static bool ready, prepared, reloadLocked, guideBound;
    private static int encounterPopupConfirmCount, oneButtonPopupCount, twoButtonConfirmCount, twoButtonCancelCount;
    private static string status = "Play 전에 시작 버튼을 누르세요. 상점 조작은 Game 뷰에서 직접 합니다.";
    private bool unowned;
    private int floor;
    private bool prepareEncounterMemory;
    private int encounterCustomerIndex;
    private int encounterVisitIndex;

    static CustomerPartyStage1Preview()
    {
        EditorApplication.playModeStateChanged += OnPlayChanged;
        EditorApplication.update += Tick;
        EditorApplication.delayCall += RecoverInterruptedStart;
    }
    private static void RecoverInterruptedStart()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false))
            OnPlayChanged(PlayModeStateChange.EnteredEditMode);
    }
    [MenuItem("Tools/Panda Restaurant/Customer Parties/Manual Play (Original)")]
    public static void OpenManual()
    {
        var window = GetWindow<CustomerPartyStage1Preview>("일행 수동 검수");
        var main = EditorGUIUtility.GetMainWindowPosition();
        window.position = new Rect(main.x + 20, main.y + 180, 360, 500);
        window.Show(); window.Focus();
    }
    [MenuItem("Tools/Panda Restaurant/Customer Parties/Stop Manual Play")]
    public static void StopManual()
    { if (SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false)) EditorApplication.isPlaying = false; }

    private void OnGUI()
    {
        manualScroll = EditorGUILayout.BeginScrollView(manualScroll);
        EditorGUILayout.LabelField("원본 Stage1 · 메모리 테스트 계정", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(status, MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.LabelField("검수 층");
            floor = GUILayout.SelectionGrid(floor, new[] { "1층", "VIP층" }, 2);
            unowned = EditorGUILayout.Toggle("B 미보유 메뉴 (거절 검수)", unowned);
            staffAi = EditorGUILayout.Toggle("직원 AI 검수 준비", EditorApplication.isPlaying ? SessionState.GetBool(StaffAiKey, false) : staffAi);
            if (staffAi) naturalAi = EditorGUILayout.Toggle("자연 호출 설정 (메모리만)", EditorApplication.isPlaying ? SessionState.GetBool(NaturalAiKey, false) : naturalAi);
            prepareEncounterMemory = EditorGUILayout.Toggle("인카운터 검수 조건 (메모리만)", prepareEncounterMemory);
            if (prepareEncounterMemory)
            {
                encounterCustomerIndex = EditorGUILayout.Popup("인카운터 손님", encounterCustomerIndex, EncounterCustomerNames);
                EditorGUILayout.LabelField("시작 방문 수");
                encounterVisitIndex = GUILayout.Toolbar(encounterVisitIndex, new[] { "0회", "24회", "49회" });
                EditorGUILayout.HelpBox("선택한 손님만 메모리 계정에 추가/방문 수 설정합니다. 실제 입장·방문 완료는 Stage1 게임 경로로 진행하세요.", MessageType.None);
            }
            if (GUILayout.Button("원본 Stage1 메모리 Play 시작"))
                StartManual((ERestaurantFloorType)floor, unowned, prepareEncounterMemory,
                    EncounterCustomerIds[Mathf.Clamp(encounterCustomerIndex, 0, EncounterCustomerIds.Length - 1)],
                    EncounterVisitOptions[Mathf.Clamp(encounterVisitIndex, 0, EncounterVisitOptions.Length - 1)]);
        }
        using (new EditorGUI.DisabledScope(!ready || !EditorApplication.isPlaying || EditorApplication.isPaused))
        {
            if (GUILayout.Button("1인 + 2인 대기열 준비")) PrepareCustomers();
            if (GUILayout.Button("다음 2인 팀 생성")) SpawnPair();
            if (ready && SessionState.GetBool(EncounterPrepKey, false))
            {
                if (GUILayout.Button("선택 손님 1인 팀 생성")) SpawnEncounterCustomer(false);
                if (GUILayout.Button("선택 손님 2인 팀 생성 (A+A)")) SpawnEncounterCustomer(true);
                DrawPopupReviewControls();
            }
            if (ready && EditorApplication.isPlaying && SessionState.GetBool(StaffAiKey, false)) DrawStaffAiControls();
            if (ready)
            {
                EditorGUILayout.LabelField("대기 팀 / 사람", controller.Count + " / " + controller.WaitingPersonCount);
                EditorGUILayout.LabelField("만족도 / 코인 / 방문", UserInfo.GetSatisfaction(EStage.Stage1) + " / " + UserInfo.Money + " / " + UserInfo.TotalCumulativeCustomerCount);
                EditorGUILayout.LabelField("메모리 저장 Game / Stage", memory.GameWrites + " / " + memory.StageWrites);
                for (int f = 0; f < 2; f++)
                    foreach (var table in tables.GetTableDataList((ERestaurantFloorType)f))
                        if (table.CurrentUse != null) EditorGUILayout.LabelField((f == 0 ? "1층 " : "VIP ") + table.TableType + " · " + table.TableState,
                            table.PartyOrders == null ? table.CurrentUse.Party.Members.Count + "인" : string.Join(" | ", table.PartyOrders.Orders.Select(o => o.Food.Id + ": " + o.State)));
            }
        }
        if (ready && SessionState.GetBool(EncounterPrepKey, false)) DrawEncounterReviewStatus();
        if (!SessionState.GetBool(StaffAiKey, false)) EditorGUILayout.HelpBox("Play 일시정지를 풀고 대기열을 준비하세요. Game 뷰에서 1인을 안내한 뒤 안내 버튼이 다시 활성화되면 2인을 안내하세요. 기본 Table1은 1인, Table2는 2인입니다. Shop에서 같은 슬롯을 해제/교체하고 상점 본체를 닫으세요. 완료액 검수는 A(판다) 음식만 서빙해 실제 식사 완료 후 진행합니다. B 음식은 서빙하지 않습니다. 서비스 시계는 정상 진행됩니다.", MessageType.None);
        if (GUILayout.Button("테스트 종료 (Play 중지)")) StopManual();
        EditorGUILayout.EndScrollView();
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { StopManual(); Event.current.Use(); }
    }
    internal static bool CanStart(out string reason)
    {
        reason = null;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) reason = "현재 Play/컴파일을 먼저 종료하세요.";
        else if (GachaReviewTestRunner.IsRunning) reason = "현재 회귀 검사가 끝날 때까지 기다리세요.";
        else if (BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin || BackendManager.ExistingInstance != null) reason = "실계정 SDK가 남아 있는 Editor에서는 시작하지 않습니다. 로그인하지 않은 Editor 세션이 필요합니다.";
        else if (EditorSettings.enterPlayModeOptionsEnabled && EditorSettings.enterPlayModeOptions != EnterPlayModeOptions.None) reason = "Domain Reload와 Scene Reload가 모두 켜진 Play 설정이 필요합니다.";
        else if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) reason = "원본 Stage1 자산이 없습니다.";
        return reason == null;
    }
    internal static void StartManual(ERestaurantFloorType selectedFloor, bool missingMenu,
        bool prepareEncounter, string encounterCustomerId, int encounterVisitCount)
    {
        if (!CanStart(out string reason)) { status = reason; Debug.LogWarning(reason); return; }
        GachaReviewTestRunner.ReleaseCreationTargetBeforeManualPlay();
        SessionState.SetInt("Panda.PartyManual.Selection", Selection.activeObject == null ? 0 : Selection.activeObject.GetInstanceID());
        SessionState.SetIntArray("Panda.PartyManual.SelectionAll", Selection.instanceIDs);
        SessionState.SetInt("Panda.PartyManual.SelectionContext", Selection.activeContext == null ? 0 : Selection.activeContext.GetInstanceID());
        SessionState.SetString("Panda.PartyManual.Workspace", JsonUtility.ToJson(ReadWorkspace()));
        SessionState.SetString(ManagerWitnessKey, ReadManagerWitness());
        SessionState.SetString(PreviousStart, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(ModeKey, missingMenu); SessionState.SetInt(FloorKey, (int)selectedFloor);
        SessionState.SetBool(StaffAiKey, staffAi); SessionState.SetBool(NaturalAiKey, staffAi && naturalAi);
        SessionState.SetBool(EncounterPrepKey, prepareEncounter);
        SessionState.SetString(EncounterCustomerKey, encounterCustomerId);
        SessionState.SetInt(EncounterVisitsKey, encounterVisitCount);
        SessionState.SetBool(CustomerPartyManualPlayBootstrap.SessionKey, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        status = "Awake 이전 외부 호출 차단 후 원본 Stage1을 초기화합니다.";
        EditorApplication.EnterPlaymode();
    }
    // Both BeforeSceneLoad callbacks rendezvous before any scene Awake can request an account.
    public static void PrepareMemoryBeforeAwake()
    {
        Require(CustomerPartyManualPlayBootstrap.Active && !BackEnd.Backend.IsInitialized && !BackEnd.Backend.IsLogin, "Offline fence missing");
        EditorApplication.LockReloadAssemblies(); reloadLocked = true;
        var roots = CustomerPartyManualPlayBootstrap.Roots;
        MainScene.SuppressEditorDebugGrantsForTesting = true;
        foreach (var alarm in roots.SelectMany(r => r.GetComponentsInChildren<UIMailAlarm>(true))) alarm.enabled = false;
        foreach (var type in new[] { typeof(SoundManager), typeof(GameManager), typeof(FurnitureDataManager), typeof(SkinDataManager), typeof(ItemManager), typeof(AttendanceDataManager) })
        {
            var c = roots.SelectMany(r => r.GetComponentsInChildren(type, true)).FirstOrDefault() as Component;
            if (c != null) c.transform.root.gameObject.SetActive(true);
        }
        _ = CustomerDataManager.Instance; _ = FoodDataManager.Instance; _ = KitchenUtensilDataManager.Instance; _ = StaffDataManager.Instance;
        memory = new CustomerPartyOwnedMemory();
        memory.Initialize(true, SessionState.GetBool(StaffAiKey, false),
            SessionState.GetBool(EncounterPrepKey, false) ? SessionState.GetString(EncounterCustomerKey, "") : null,
            SessionState.GetInt(EncounterVisitsKey, 0));
        Require(ReferenceEquals(BackendManager.ExistingInstance, memory.Owner) && memory.LoggedIn, "Memory owner not installed");
        UserInfo.SetGamerId("ORIGINAL-MANUAL-MEMORY");
        if (SessionState.GetBool(ModeKey, false)) ((Dictionary<string, int>)Field(typeof(UserInfo), "_giveRecipeLevelDic").GetValue(null)).Remove("FOOD04");
        Debug.Log("PARTY_MANUAL_MEMORY_BEFORE_AWAKE: SDK initialized=" + BackEnd.Backend.IsInitialized);
    }
    public static void InitializeManualSession()
    {
        Require(memory != null && ReferenceEquals(BackendManager.ExistingInstance, memory.Owner) && memory.LoggedIn, "Memory owner not installed before Awake");
        var scene = SceneManager.GetActiveScene(); var roots = CustomerPartyManualPlayBootstrap.Roots; var active = CustomerPartyManualPlayBootstrap.ActiveRoots;
        foreach (var alarm in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UIMailAlarm>(true))) alarm.enabled = false;
        controller = Find<CustomerController>(scene); tables = Find<TableManager>(scene);
        var original = Get<CustomerPartySettings>(controller, "_naturalPartySettings");
        Require(original != null && original.MinimumNormalVisits == 25 && original.CompanionProbability == .2f, "Delivered settings changed");
        settings = Object.Instantiate(original); settings.EnableNaturalParties = false; Set(controller, "_naturalPartySettings", settings);
        settings.EnableNaturalParties = SessionState.GetBool(NaturalAiKey, false);
        var catalog = (Dictionary<string, CustomerData>)Field(typeof(CustomerDataManager), "_customerDataDic").GetValue(null);
        foreach (var pair in new[] { ("CUSTOMER01", "FOOD01"), ("CUSTOMER02", "FOOD04") })
        {
            var source = catalog[pair.Item1]; var clone = Object.Instantiate(source);
            originalCustomers.Add(pair.Item1, source); runtimeCustomers.Add(clone);
            bool encounterTarget = SessionState.GetBool(EncounterPrepKey, false)
                && SessionState.GetString(EncounterCustomerKey, "") == pair.Item1;
            if (encounterTarget)
            {
                if (!(source is NormalCustomerData normalSource))
                    throw new InvalidOperationException("Encounter review target must use normal customer data.");
                Set(clone, "_requiredDish", source.RequiredDish);
                Set(clone, "_visitCountFoodDic", new Dictionary<int, string>(normalSource.GetVisitCountFoodDic()));
            }
            else
            {
                Set(clone, "_requiredDish", pair.Item2);
                Set(clone, "_visitCountFoodDic", new Dictionary<int, string>());
            }
            ReplaceCustomer(source, clone);
        }
        string encounterTargetId = SessionState.GetString(EncounterCustomerKey, "");
        if (SessionState.GetBool(EncounterPrepKey, false)
            && encounterTargetId != "CUSTOMER01" && encounterTargetId != "CUSTOMER02")
        {
            if (!catalog.TryGetValue(encounterTargetId, out CustomerData targetSource)
                || !(targetSource is NormalCustomerData normalTarget))
                throw new InvalidOperationException("Encounter review target is missing normal customer data: " + encounterTargetId);
            var targetClone = Object.Instantiate(normalTarget);
            originalCustomers.Add(encounterTargetId, normalTarget);
            runtimeCustomers.Add(targetClone);
            Set(targetClone, "_requiredDish", normalTarget.RequiredDish);
            Set(targetClone, "_visitCountFoodDic", new Dictionary<int, string>(normalTarget.GetVisitCountFoodDic()));
            ReplaceCustomer(normalTarget, targetClone);
        }
        Find<CameraController>(scene).transform.root.gameObject.SetActive(true);
        foreach (var root in roots)
        {
            if (!active.Contains(root) || IsExternalRoot(root) || root.GetComponent<AStar>() != null || root.GetComponent<StaffController>() != null
                || root.GetComponent<FurnitureSystem>() != null || root.GetComponent<KitchenSystem>() != null
                || root.GetComponentInChildren<CameraController>(true) != null || root.GetComponentInChildren<Canvas>(true) != null) continue;
            root.SetActive(true);
        }
        Physics2D.SyncTransforms(); Find<AStar>(scene).gameObject.SetActive(true); Find<KitchenSystem>(scene).gameObject.SetActive(true);
        foreach (var root in roots) if (active.Contains(root) && root.GetComponentInChildren<Canvas>(true) != null && !IsExternalRoot(root)) root.SetActive(true);
        if (SessionState.GetBool(StaffAiKey, false)) InitializeCompletedCustomerTutorialNavigation(scene);
        Canvas.ForceUpdateCanvases(); Find<FurnitureSystem>(scene).gameObject.SetActive(true); Physics2D.SyncTransforms();
        var astar = Find<AStar>(scene);
        foreach (var map in Get<IDictionary>(astar, "_maps").Values) typeof(AStar).GetMethod("GenerateNodes", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(astar, new[] { map });
        Find<StaffController>(scene).gameObject.SetActive(true);
        ready = true; prepared = guideBound = false;
        status = "메모리 테스트 준비 완료. 대기열 준비 후 Game 뷰에서 안내·주문·상점을 직접 조작하세요. 자동 클릭/종료는 없습니다.";
        Debug.Log("PARTY_MANUAL_READY: original Stage1; SDK initialized=" + BackEnd.Backend.IsInitialized + "; memory owner=" + ReferenceEquals(BackendManager.ExistingInstance, memory.Owner));
        Debug.Log("PARTY_MANUAL_SESSION_START: " + ReadManagerWitness()
            + "; subscriptions=" + ReadManagerSubscriptions() + "; teams=" + controller.Count
            + "; people=" + controller.WaitingPersonCount + "; callTouches=" + controller.TabCount
            + "; timeScale=" + Time.timeScale + "; natural=" + settings.EnableNaturalParties
            + "; equipped=" + Enumerable.Range(0, 2).Sum(f => CustomerPartyOwnedMemory.StaffAiStaffIds.Keys.Count(
                role => UserInfo.GetEquipStaff(UserInfo.CurrentStage, (ERestaurantFloorType)f, role) != null)));
    }
    internal static bool IsExternalRoot(GameObject root) => root.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c is BackendManager || c is GoogleLoginManager || c is LevelPlayBoot || c is IAPManager || c is FirstLoadingScene);
    private static void InitializeCompletedCustomerTutorialNavigation(Scene scene)
    {
        // Stage1 keeps this child canvas inactive. Its Start has therefore not bound
        // the customer tutorial view, although completed-event commands query it.
        // Initialize only this inactive navigation for the completed memory account;
        // never reinitialize the active shop/gacha/exchange navigation.
        Require(UserInfo.IsGatecrasher1TutorialClear && UserInfo.IsGatecrasher2TutorialClear
            && UserInfo.IsSpecialCustomer1TutorialClear && UserInfo.IsSpecialCustomer2TutorialClear, "Completed tutorial memory account required");
        var nav = Find<UITutorialCanvas>(scene).GetComponent<Muks.MobileUI.MobileUINavigation>();
        if (!nav.gameObject.activeInHierarchy && Get<IDictionary>(nav, "_viewDic").Count == 0)
            typeof(Muks.MobileUI.MobileUINavigation).GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(nav, null);
        var customerTutorial = Get<UICustomerTutorial>(controller, "_uiCustomerTutorial");
        Require(ReferenceEquals(Field(typeof(Muks.UI.UIView), "_uiNav").GetValue(customerTutorial), nav), "Customer tutorial navigation not bound");
    }
    private static void PrepareCustomers()
    {
        if (!ready || prepared || controller.Count != 0) { status = "첫 대기열은 한 번만 준비합니다. 다음 검수는 다음 2인 팀 생성 또는 Play 재시작을 사용하세요."; return; }
        BindManualGuideFloor();
        Find<CameraController>(SceneManager.GetActiveScene()).MoveCamera((ERestaurantFloorType)SessionState.GetInt(FloorKey, 0), RestaurantType.Hall);
        Require(controller.TrySpawnWaitingParty(new[] { "CUSTOMER04" }, out var single), "Single preparation failed");
        if (!controller.TrySpawnWaitingParty(new[] { "CUSTOMER01", "CUSTOMER02" }, out var pair))
        { controller.CancelWaitingParty(single); throw new InvalidOperationException("Pair preparation failed"); }
        prepared = true; status = "1인 + 2인 입장 중입니다. Game 뷰에서 안내 버튼을 두 번 누르세요. 상점·주문·서빙은 자동 진행하지 않습니다.";
        Debug.Log("PARTY_MANUAL_SPAWN: teams=" + controller.Count + "; people=" + controller.WaitingPersonCount);
    }
    private static void SpawnPair()
    {
        if (!ready) return;
        BindManualGuideFloor();
        status = controller.TrySpawnWaitingParty(new[] { "CUSTOMER01", "CUSTOMER02" }, out var _) ? "다음 2인 팀이 입장합니다. Game 뷰에서 직접 안내하세요." : "현재 팀 한도/준비 상태에서 생성할 수 없습니다.";
    }
    private static void SpawnEncounterCustomer(bool sameIdPair)
    {
        if (!ready || !SessionState.GetBool(EncounterPrepKey, false)) return;
        string customerId = SessionState.GetString(EncounterCustomerKey, "");
        if (!EncounterCustomerIds.Contains(customerId)) { status = "선택된 검수 손님 ID가 유효하지 않습니다."; return; }
        BindManualGuideFloor();
        var members = sameIdPair ? new[] { customerId, customerId } : new[] { customerId };
        status = controller.TrySpawnWaitingParty(members, out _)
            ? (sameIdPair ? "선택 손님 A+A 팀이 실제 대기열 경로로 입장합니다." : "선택 손님이 실제 대기열 경로로 입장합니다.")
            : "현재 팀 한도/준비 상태에서 생성할 수 없습니다.";
    }
    private static void DrawPopupReviewControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("공용 팝업 화면 검수 · 실제 PopupManager API", EditorStyles.boldLabel);
        if (GUILayout.Button("인카운터 팝업 표시 (화면 검수)"))
        {
            string customerId = SessionState.GetString(EncounterCustomerKey, "");
            var catalog = (Dictionary<string, CustomerData>)Field(typeof(CustomerDataManager), "_customerDataDic").GetValue(null);
            if (!catalog.TryGetValue(customerId, out CustomerData customer) || customer == null || customer.ThumbnailSprite == null)
                status = "선택 손님의 초상화를 찾지 못했습니다.";
            else
                PopupManager.Instance.ShowCustomerEncounter(customer.Name, "인카운터 팝업 화면 검수", customer.ThumbnailSprite,
                    () => encounterPopupConfirmCount++);
        }
        if (GUILayout.Button("일반 이미지 없는 1버튼 팝업"))
        {
            PopupManager manager = PopupManager.Instance;
            manager.ShowPopup("일반 팝업 검수", "이미지 없는 한 버튼 팝업입니다.");
            manager.SetPopupButton1("확인", () => oneButtonPopupCount++);
        }
        if (GUILayout.Button("일반 확인/취소 2버튼 팝업"))
        {
            PopupManager manager = PopupManager.Instance;
            manager.ShowPopup("일반 팝업 검수", "확인 또는 취소를 선택하세요.");
            manager.SetPopupButton1("확인", () => twoButtonConfirmCount++);
            manager.SetPopupButton2("취소", () => twoButtonCancelCount++);
        }
        EditorGUILayout.LabelField("메모리 콜백 횟수 · 인카운터 확인 " + encounterPopupConfirmCount
            + " / 일반 확인 " + oneButtonPopupCount + " / 2버튼 확인 " + twoButtonConfirmCount
            + " / 2버튼 취소 " + twoButtonCancelCount);
    }
    private static void DrawEncounterReviewStatus()
    {
        string customerId = SessionState.GetString(EncounterCustomerKey, "");
        var customer = CustomerDataManager.Instance.GetCustomerData(customerId) as NormalCustomerData;
        if (customer == null) return;
        var progressField = typeof(UserInfo).GetField("_customerEncounterProgress", BindingFlags.Static | BindingFlags.NonPublic);
        var progress = progressField?.GetValue(null) as CustomerEncounterProgressData;
        string first = GetEncounterState(progress, customerId, CustomerEncounterEventKind.FirstAppearance, 0);
        string milestoneStatus = string.Join(" / ", CustomerEncounterCatalog.GetCustomerVisitMilestones(customerId)
            .Select(milestone => milestone + "회 " + GetEncounterState(progress, customerId,
                CustomerEncounterEventKind.VisitMilestone, milestone)));
        string baseFoodStatus = GetRecipeStatus(customer.RequiredDish);
        string stageFoodStatus = string.Join(" / ", customer.GetVisitCountFoodDic().OrderBy(pair => pair.Key)
            .Select(pair => pair.Key + "회 " + GetRecipeStatus(pair.Value)));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("인카운터 메모리 검수", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("선택 손님 공식 방문", customer.Name + " / " + UserInfo.GetVisitedCustomerCount(customerId) + "회");
        EditorGUILayout.LabelField("첫 등장", first);
        EditorGUILayout.LabelField("등록 문턱", milestoneStatus);
        EditorGUILayout.LabelField("기본 음식", baseFoodStatus);
        EditorGUILayout.LabelField("단계 음식", stageFoodStatus);
    }
    private static string GetRecipeStatus(string foodId)
        => string.IsNullOrWhiteSpace(foodId) ? "없음" : foodId
            + (UserInfo.GetGiveRecipeList().Contains(foodId) ? " · 레시피 보유" : " · 레시피 미보유");
    private static string GetEncounterState(CustomerEncounterProgressData progress, string customerId,
        CustomerEncounterEventKind kind, int milestone)
    {
        if (progress == null) return "미확인";
        if (progress.IsPending(customerId, kind, milestone)) return "Pending";
        if (progress.IsSeen(customerId, kind, milestone)) return "Seen";
        return "미등록";
    }
    private static void BindManualGuideFloor()
    {
        if (SessionState.GetBool(StaffAiKey, false)) return; // AI mode keeps the production manual-guide listener.
        if (guideBound) return;
        // Called by an explicit preparation click after TableManager.Start. The actual
        // Game-view button still invokes production guidance; only its floor is fixed
        // for this runtime session. Nothing is invoked by this setup operation.
        var button = Get<UnityEngine.UI.Button>(tables, "_guideButton");
        Require(button.onClick.GetPersistentEventCount() == 0, "Unexpected persistent guide action");
        var owner = tables;
        var selectedFloor = (ERestaurantFloorType)SessionState.GetInt(FloorKey, 0);
        button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        button.onClick.AddListener(() => owner.OnCustomerGuideEventPlaySound(selectedFloor));
        guideBound = true;
    }
    private static void Tick()
    {
        if (!ready || !EditorApplication.isPlaying) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { StopManual(); return; }
        if (BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin) { Debug.LogError("Manual session external SDK fence failed"); StopManual(); }
        TickStaffAiObservation();
        foreach (var w in Resources.FindObjectsOfTypeAll<CustomerPartyStage1Preview>()) w.Repaint();
    }
    private static void OnPlayChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false)) return;
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            StopStaffAiObservation();
            ready = false;
            if (controller != null) typeof(CustomerController).GetMethod("EndVisitParties", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            foreach (var clone in runtimeCustomers)
            {
                if (clone == null) continue;
                ReplaceCustomer(clone, originalCustomers[clone.Id]); Object.Destroy(clone);
            }
            runtimeCustomers.Clear(); originalCustomers.Clear();
            if (settings != null) Object.Destroy(settings);
            // Keep memory transport through OnApplicationQuit and normal game autosave.
        }
        if (state != PlayModeStateChange.EnteredEditMode) return;
        try { memory?.Dispose(); }
        finally
        {
            memory = null; controller = null; tables = null; settings = null; ready = prepared = guideBound = false;
            MainScene.SuppressEditorDebugGrantsForTesting = false;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousStart, ""));
            Selection.objects = SessionState.GetIntArray("Panda.PartyManual.SelectionAll", new int[0])
                .Select(EditorUtility.InstanceIDToObject).Where(o => o != null).ToArray();
            Selection.SetActiveObjectWithContext(EditorUtility.InstanceIDToObject(SessionState.GetInt("Panda.PartyManual.Selection", 0)),
                EditorUtility.InstanceIDToObject(SessionState.GetInt("Panda.PartyManual.SelectionContext", 0)));
            SessionState.EraseBool(CustomerPartyManualPlayBootstrap.SessionKey); CustomerPartyManualPlayBootstrap.Clear();
            if (reloadLocked) { reloadLocked = false; EditorApplication.UnlockReloadAssemblies(); }
            status = "테스트 종료. 메모리 계정은 폐기되었고 원래 편집 Scene으로 돌아왔습니다.";
            Debug.Log("PARTY_MANUAL_STOPPED: memory released; SDK initialized=" + BackEnd.Backend.IsInitialized);
            string managerAfter = ReadManagerWitness();
            Debug.Log("PARTY_MANUAL_MANAGER_RESTORE: matches=" + (managerAfter == SessionState.GetString(ManagerWitnessKey, ""))
                + "; before=" + SessionState.GetString(ManagerWitnessKey, "") + "; after=" + managerAfter);
            SessionState.EraseString(ManagerWitnessKey);
            EditorApplication.delayCall += VerifyWorkspace;
        }
    }
    [Serializable]
    private sealed class WorkspaceState
    {
        public string setup, dirty, selection, startScene, undo;
    }
    // Read-only lifecycle evidence, including DontDestroyOnLoad objects. Never creates or destroys managers.
    private static string ReadManagerWitness()
    {
        return string.Join(";", new[] { typeof(GameManager), typeof(ChallengeManager) }.Select(type => type.Name + "="
            + string.Join(",", Resources.FindObjectsOfTypeAll(type).OfType<Component>()
                .Where(c => c != null && c.gameObject.scene.IsValid()).Select(c => c.GetInstanceID()).OrderBy(id => id))));
    }
    private static string ReadManagerSubscriptions()
    {
        return string.Join("|", new[] { typeof(GameManager), typeof(ChallengeManager) }.SelectMany(type =>
            Resources.FindObjectsOfTypeAll(type).OfType<Component>().Where(c => c != null && c.gameObject.scene.IsValid())
                .SelectMany(c => type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(f => typeof(Delegate).IsAssignableFrom(f.FieldType)).Select(f =>
                    {
                        Delegate[] handlers = (f.GetValue(c) as Delegate)?.GetInvocationList() ?? Array.Empty<Delegate>();
                        return type.Name + "." + f.Name + "=" + handlers.Length + "/duplicates=" + (handlers.Length - handlers.Distinct().Count());
                    }))));
    }
    private static WorkspaceState ReadWorkspace()
    {
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var undoMethod = typeof(Undo).GetMethod("GetRecords", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(List<string>), typeof(int).MakeByRefType() }, null);
        string undo = "unavailable";
        if (undoMethod != null)
        {
            try { var records = new List<string>(); var args = new object[] { records, 0 }; undoMethod.Invoke(null, args); undo = args[1] + "|" + string.Join("\n", records); }
            catch { /* Read-only evidence can be unavailable on another Editor version. */ }
        }
        return new WorkspaceState
        {
            setup = string.Join("\n", EditorSceneManager.GetSceneManagerSetup().Select(s => s.path + "|" + s.isLoaded + "|" + s.isActive)),
            dirty = string.Join("\n", scenes.Select(s => s.path + "|" + s.isDirty)),
            selection = string.Join(",", Selection.instanceIDs) + "|" + Selection.activeInstanceID + "|" + (Selection.activeContext == null ? 0 : Selection.activeContext.GetInstanceID()),
            startScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene), undo = undo
        };
    }
    private static void VerifyWorkspace()
    {
        var json = SessionState.GetString("Panda.PartyManual.Workspace", "");
        if (string.IsNullOrEmpty(json)) return;
        var before = JsonUtility.FromJson<WorkspaceState>(json); var after = ReadWorkspace();
        Debug.Log("PARTY_MANUAL_WORKSPACE: setup=" + (before.setup == after.setup) + "; dirty=" + (before.dirty == after.dirty)
            + "; selection=" + (before.selection == after.selection) + "; startScene=" + (before.startScene == after.startScene)
            + "; undoHistory=" + (before.undo == "unavailable" || after.undo == "unavailable" ? "Unknown" : (before.undo == after.undo).ToString()));
    }
    private static void ReplaceCustomer(CustomerData from, CustomerData to)
    {
        ((Dictionary<string, CustomerData>)Field(typeof(CustomerDataManager), "_customerDataDic").GetValue(null))[to.Id] = to;
        foreach (string name in new[] { "_customerDataList", "_normalCustomerDataList" })
        {
            var list = (IList)Field(typeof(CustomerDataManager), name).GetValue(null);
            for (int i = 0; i < list.Count; i++) if (ReferenceEquals(list[i], from)) list[i] = to;
        }
    }
    private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).First();
    private static FieldInfo Field(Type type, string name)
    { for (; type != null; type = type.BaseType) { var f = type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f; } throw new MissingFieldException(name); }
    private static T Get<T>(object obj, string name) => (T)Field(obj.GetType(), name).GetValue(obj);
    private static void Set(object obj, string name, object value) => Field(obj.GetType(), name).SetValue(obj, value);
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
}
#endif
